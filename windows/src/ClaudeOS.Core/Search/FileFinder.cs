using System.Text;

namespace ClaudeOS.Core.Search;

public sealed record FileCandidate(string Path, DateTimeOffset Modified, long Size = 0);

public sealed record FileMatch(FileCandidate File, double Score, string Reason);

/// <summary>
/// "Open the Q3 budget" is answered by local code: the Windows Search index first (the shell
/// feeds its hits in as candidates), then a scoped scan of Desktop, Documents and Downloads.
/// This ranks candidates by how well their name and folders match the words, with a nudge for
/// recent files. No model, no tokens, milliseconds.
/// </summary>
public static class FileFinder
{
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "my", "our", "of", "from", "to", "for", "in", "on", "that", "this", "file", "files", "document", "doc", "latest", "newest", "last",
    };

    private static readonly Dictionary<string, string[]> KindWords = new(StringComparer.Ordinal)
    {
        ["spreadsheet"] = [".xlsx", ".xls", ".csv", ".numbers", ".ods"],
        ["sheet"] = [".xlsx", ".xls", ".csv", ".numbers", ".ods"],
        ["pdf"] = [".pdf"],
        ["slides"] = [".pptx", ".ppt", ".key"],
        ["deck"] = [".pptx", ".ppt", ".key"],
        ["presentation"] = [".pptx", ".ppt", ".key"],
        ["photo"] = [".jpg", ".jpeg", ".png", ".heic", ".webp"],
        ["picture"] = [".jpg", ".jpeg", ".png", ".heic", ".webp"],
        ["image"] = [".jpg", ".jpeg", ".png", ".heic", ".webp", ".gif"],
        ["screenshot"] = [".png", ".jpg"],
        ["notes"] = [".md", ".txt", ".docx"],
    };

    private static readonly string[] SkipSegments = ["node_modules", ".git", "bin", "obj", "AppData", "$Recycle.Bin", "Windows"];

    public static IReadOnlyList<FileMatch> Rank(string query, IEnumerable<FileCandidate> files, DateTimeOffset now, int take = 8)
    {
        var tokens = Tokenize(query).Where(t => !Stop.Contains(t)).ToList();
        if (tokens.Count == 0)
        {
            return [];
        }

        var wanted = tokens.Where(KindWords.ContainsKey).SelectMany(t => KindWords[t]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var content = tokens.Where(t => !KindWords.ContainsKey(t) || tokens.Count == 1).ToList();
        var joined = string.Join(' ', content);

        var matches = new List<FileMatch>();
        foreach (var file in files)
        {
            var (folder, fileName) = Split(file.Path);
            var dot = fileName.LastIndexOf('.');
            var name = dot > 0 ? fileName[..dot] : fileName;
            var ext = dot > 0 ? fileName[dot..] : "";
            var nameTokens = Tokenize(name).ToList();
            var folderTokens = Tokenize(folder).TakeLast(4).ToList();

            double score = 0;
            var ok = true;
            foreach (var t in content)
            {
                var s = TokenScore(t, nameTokens, folderTokens);
                if (s <= 0)
                {
                    ok = false;
                    break;
                }

                score += s;
            }

            if (!ok || (content.Count == 0 && wanted.Count == 0))
            {
                continue;
            }

            if (wanted.Count > 0)
            {
                if (!wanted.Contains(ext))
                {
                    continue;
                }

                score += 1.5;
            }

            if (string.Equals(string.Join(' ', nameTokens), joined, StringComparison.Ordinal))
            {
                score += 5;
            }
            else if (string.Join(' ', nameTokens).StartsWith(joined, StringComparison.Ordinal))
            {
                score += 2;
            }

            var ageDays = Math.Max(0, (now - file.Modified).TotalDays);
            score += 1.5 * Math.Pow(0.5, ageDays / 30);
            score -= 0.05 * Math.Max(0, file.Path.Count(c => c is '\\' or '/') - 4);
            matches.Add(new FileMatch(file, score, Reason(nameTokens, content)));
        }

        return [.. matches.OrderByDescending(m => m.Score).ThenByDescending(m => m.File.Modified).Take(take)];
    }

    /// <summary>Splits on either separator so Windows paths rank the same wherever the code runs.</summary>
    private static (string Folder, string Name) Split(string path)
    {
        var i = path.LastIndexOfAny(['\\', '/']);
        return i < 0 ? ("", path) : (path[..i], path[(i + 1)..]);
    }

    private static string Reason(List<string> nameTokens, List<string> content) =>
        content.All(nameTokens.Contains) ? "name matches" : "name and folder match";

    private static double TokenScore(string t, List<string> name, List<string> folders)
    {
        double best = 0;
        foreach (var n in name)
        {
            if (n == t)
            {
                best = Math.Max(best, 3);
            }
            else if (n.StartsWith(t, StringComparison.Ordinal) && t.Length >= 2)
            {
                best = Math.Max(best, 2);
            }
            else if (t.Length >= 4 && n.Length >= 4 && Distance(n, t) <= 1)
            {
                best = Math.Max(best, 1.2);
            }
        }

        if (best == 0 && folders.Any(f => f == t || (t.Length >= 3 && f.StartsWith(t, StringComparison.Ordinal))))
        {
            best = 1;
        }

        return best;
    }

    /// <summary>Lower-case words from a name, splitting on punctuation, digits boundaries and camelCase.</summary>
    internal static IEnumerable<string> Tokenize(string text)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var boundary = !char.IsLetterOrDigit(c)
                || (i > 0 && char.IsUpper(c) && char.IsLower(text[i - 1]));
            if (boundary && sb.Length > 0)
            {
                yield return sb.ToString().ToLowerInvariant();
                sb.Clear();
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString().ToLowerInvariant();
        }
    }

    /// <summary>Damerau-Levenshtein distance, early-out beyond 1 (typo tolerance).</summary>
    internal static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
        {
            return 2;
        }

        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) { d[i, 0] = i; }
        for (var j = 0; j <= b.Length; j++) { d[0, j] = j; }
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }

    /// <summary>A scoped scan: the fallback when the search index has nothing.</summary>
    public static IEnumerable<FileCandidate> Scan(IEnumerable<string> roots, int maxFiles = 50_000, int maxDepth = 6)
    {
        var count = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System | FileAttributes.Hidden };
        var stack = new Stack<(string Dir, int Depth)>(roots.Where(Directory.Exists).Select(r => (r, 0)));
        while (stack.Count > 0 && count < maxFiles)
        {
            var (dir, depth) = stack.Pop();
            foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", options))
            {
                if (entry is DirectoryInfo sub)
                {
                    if (depth < maxDepth && !SkipSegments.Contains(sub.Name, StringComparer.OrdinalIgnoreCase) && sub.LinkTarget is null)
                    {
                        stack.Push((sub.FullName, depth + 1));
                    }
                }
                else if (entry is FileInfo f)
                {
                    count++;
                    yield return new FileCandidate(f.FullName, f.LastWriteTimeUtc, f.Length);
                    if (count >= maxFiles)
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
