using System.Text;

namespace ClaudeOS.Core.Safety;

/// <summary>Unified diffs in the same shape as <c>difflib.unified_diff</c> (three lines of
/// context), so a staged change reads like any other diff the person has seen.</summary>
public static class UnifiedDiff
{
    private enum Tag { Equal, Replace, Delete, Insert }

    private readonly record struct Op(Tag Tag, int I1, int I2, int J1, int J2);

    /// <summary>Beyond this many differing lines the diff degrades to "replace everything"
    /// instead of spending quadratic memory finding the minimal script.</summary>
    private const int MaxEditDistance = 1500;

    public static string[] SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(text[start..(i + 1)]);
                start = i + 1;
            }
            else if (text[i] == '\r')
            {
                var end = i + 1 < text.Length && text[i + 1] == '\n' ? i + 2 : i + 1;
                lines.Add(text[start..end]);
                start = end;
                i = end - 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return [.. lines];
    }

    public static string Generate(string[] a, string[] b, string fromFile, string toFile, int context = 3)
    {
        var groups = GroupedOps(Opcodes(a, b), context);
        if (groups.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append("--- ").Append(fromFile).Append('\n').Append("+++ ").Append(toFile).Append('\n');
        foreach (var group in groups)
        {
            var first = group[0];
            var last = group[^1];
            sb.Append("@@ -").Append(Range(first.I1, last.I2)).Append(" +").Append(Range(first.J1, last.J2)).Append(" @@\n");
            foreach (var op in group)
            {
                if (op.Tag == Tag.Equal)
                {
                    for (var i = op.I1; i < op.I2; i++)
                    {
                        AppendLine(sb, ' ', a[i]);
                    }

                    continue;
                }

                if (op.Tag is Tag.Replace or Tag.Delete)
                {
                    for (var i = op.I1; i < op.I2; i++)
                    {
                        AppendLine(sb, '-', a[i]);
                    }
                }

                if (op.Tag is Tag.Replace or Tag.Insert)
                {
                    for (var j = op.J1; j < op.J2; j++)
                    {
                        AppendLine(sb, '+', b[j]);
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static void AppendLine(StringBuilder sb, char prefix, string line)
    {
        sb.Append(prefix).Append(line);
        if (!line.EndsWith('\n'))
        {
            sb.Append('\n');
        }
    }

    private static string Range(int start, int stop)
    {
        var beginning = start + 1;
        var length = stop - start;
        if (length == 1)
        {
            return beginning.ToString();
        }

        if (length == 0)
        {
            beginning--;
        }

        return $"{beginning},{length}";
    }

    private static List<List<Op>> GroupedOps(List<Op> codes, int n)
    {
        var result = new List<List<Op>>();
        if (codes.Count == 0)
        {
            codes = [new Op(Tag.Equal, 0, 1, 0, 1)];
        }

        if (codes[0].Tag == Tag.Equal)
        {
            var c = codes[0];
            codes[0] = new Op(Tag.Equal, Math.Max(c.I1, c.I2 - n), c.I2, Math.Max(c.J1, c.J2 - n), c.J2);
        }

        if (codes[^1].Tag == Tag.Equal)
        {
            var c = codes[^1];
            codes[^1] = new Op(Tag.Equal, c.I1, Math.Min(c.I2, c.I1 + n), c.J1, Math.Min(c.J2, c.J1 + n));
        }

        var group = new List<Op>();
        foreach (var code in codes)
        {
            var (tag, i1, i2, j1, j2) = code;
            if (tag == Tag.Equal && i2 - i1 > n * 2)
            {
                group.Add(new Op(tag, i1, Math.Min(i2, i1 + n), j1, Math.Min(j2, j1 + n)));
                result.Add(group);
                group = [];
                i1 = Math.Max(i1, i2 - n);
                j1 = Math.Max(j1, j2 - n);
            }

            group.Add(new Op(tag, i1, i2, j1, j2));
        }

        if (group.Count > 0 && !(group.Count == 1 && group[0].Tag == Tag.Equal))
        {
            result.Add(group);
        }

        return result;
    }

    private static List<Op> Opcodes(string[] a, string[] b)
    {
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[^(suffix + 1)] == b[^(suffix + 1)])
        {
            suffix++;
        }

        var ops = new List<Op>();
        if (prefix > 0)
        {
            ops.Add(new Op(Tag.Equal, 0, prefix, 0, prefix));
        }

        var aMid = a.AsSpan(prefix, a.Length - prefix - suffix).ToArray();
        var bMid = b.AsSpan(prefix, b.Length - prefix - suffix).ToArray();
        foreach (var op in Middle(aMid, bMid))
        {
            ops.Add(new Op(op.Tag, op.I1 + prefix, op.I2 + prefix, op.J1 + prefix, op.J2 + prefix));
        }

        if (suffix > 0)
        {
            ops.Add(new Op(Tag.Equal, a.Length - suffix, a.Length, b.Length - suffix, b.Length));
        }

        return ops;
    }

    private static List<Op> Middle(string[] a, string[] b)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return [];
        }

        if (a.Length == 0)
        {
            return [new Op(Tag.Insert, 0, 0, 0, b.Length)];
        }

        if (b.Length == 0)
        {
            return [new Op(Tag.Delete, 0, a.Length, 0, 0)];
        }

        var script = Myers(a, b);
        if (script is null)
        {
            return [new Op(Tag.Replace, 0, a.Length, 0, b.Length)];
        }

        // Collapse the per-line script into runs.
        var ops = new List<Op>();
        int i = 0, j = 0, k = 0;
        while (k < script.Count)
        {
            if (script[k] == '=')
            {
                var i1 = i;
                var j1 = j;
                while (k < script.Count && script[k] == '=') { i++; j++; k++; }
                ops.Add(new Op(Tag.Equal, i1, i, j1, j));
                continue;
            }

            var si = i;
            var sj = j;
            while (k < script.Count && script[k] != '=')
            {
                if (script[k] == '-') { i++; } else { j++; }
                k++;
            }

            ops.Add(new Op(i > si && j > sj ? Tag.Replace : i > si ? Tag.Delete : Tag.Insert, si, i, sj, j));
        }

        return ops;
    }

    /// <summary>Myers' O(ND) shortest edit script as '=', '-' and '+' per step, or null when
    /// the inputs differ by more than <see cref="MaxEditDistance"/> lines.</summary>
    private static List<char>? Myers(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length, max = Math.Min(n + m, MaxEditDistance);
        var offset = max + 1;
        var v = new int[2 * max + 3];
        var trace = new List<int[]>();
        for (var d = 0; d <= max; d++)
        {
            trace.Add((int[])v.Clone());
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]))
                {
                    x = v[offset + k + 1];
                }
                else
                {
                    x = v[offset + k - 1] + 1;
                }

                var y = x - k;
                while (x < n && y < m && a[x] == b[y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    return Backtrack(trace, a, b, d, offset);
                }
            }
        }

        return null;
    }

    private static List<char> Backtrack(List<int[]> trace, string[] a, string[] b, int d, int offset)
    {
        var script = new List<char>();
        int x = a.Length, y = b.Length;
        for (; d > 0; d--)
        {
            var v = trace[d];
            var k = x - y;
            int prevK;
            if (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]))
            {
                prevK = k + 1;
            }
            else
            {
                prevK = k - 1;
            }

            var prevX = v[offset + prevK];
            var prevY = prevX - prevK;
            while (x > prevX && y > prevY)
            {
                script.Add('=');
                x--;
                y--;
            }

            script.Add(x == prevX ? '+' : '-');
            x = prevX;
            y = prevY;
        }

        while (x > 0 && y > 0)
        {
            script.Add('=');
            x--;
            y--;
        }

        script.Reverse();
        return script;
    }
}
