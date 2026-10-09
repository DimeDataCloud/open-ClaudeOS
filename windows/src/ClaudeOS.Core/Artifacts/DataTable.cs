using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace ClaudeOS.Core.Artifacts;

public enum ColumnType { Number, Date, Text }

/// <summary>A column. <see cref="Unit"/> is set on a date column produced by a derive step (year, quarter, month, week, day).</summary>
public sealed record Column(string Name, ColumnType Type, string? Unit = null);

/// <summary>
/// Typed tabular data. Cells are <c>double</c>, <c>DateOnly</c>, <c>string</c> or null. The data
/// stays local: Claude is only ever shown a <see cref="DataProfile"/>, and local code runs the
/// chart recipe over every row.
/// </summary>
public sealed class DataTable
{
    public DataTable(IReadOnlyList<Column> columns, IReadOnlyList<object?[]> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<Column> Columns { get; }

    public IReadOnlyList<object?[]> Rows { get; }

    public int IndexOf(string name)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public Column? Find(string name) => IndexOf(name) is var i and >= 0 ? Columns[i] : null;

    /// <summary>Parse CSV text (RFC 4180 quoting) and infer each column's type from all of its rows.</summary>
    public static DataTable FromCsv(string csv)
    {
        var records = CsvReader.Read(csv);
        if (records.Count == 0)
        {
            return new DataTable([], []);
        }

        var header = records[0].Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"column{i + 1}" : h.Trim()).ToArray();
        var raw = records.Skip(1).Where(r => r.Any(c => c.Length > 0)).ToList();

        var columns = new List<Column>();
        for (var c = 0; c < header.Length; c++)
        {
            columns.Add(new Column(header[c], InferType(raw.Select(r => c < r.Length ? r[c] : ""))));
        }

        var rows = raw.Select(r => columns.Select((col, c) => Convert(c < r.Length ? r[c] : "", col.Type)).ToArray()).ToList();
        return new DataTable(columns, rows);
    }

    private static ColumnType InferType(IEnumerable<string> values)
    {
        var any = false;
        var allNumber = true;
        var allDate = true;
        foreach (var v in values.Where(v => v.Length > 0))
        {
            any = true;
            allNumber &= TryNumber(v, out _);
            allDate &= TryDate(v, out _);
            if (!allNumber && !allDate)
            {
                return ColumnType.Text;
            }
        }

        return !any ? ColumnType.Text : allNumber ? ColumnType.Number : allDate ? ColumnType.Date : ColumnType.Text;
    }

    private static object? Convert(string v, ColumnType type)
    {
        if (v.Length == 0)
        {
            return null;
        }

        return type switch
        {
            ColumnType.Number => TryNumber(v, out var n) ? n : null,
            ColumnType.Date => TryDate(v, out var d) ? d : null,
            _ => v,
        };
    }

    /// <summary>Plain numbers, thousands separators, currency symbols, percentages and accounting
    /// negatives: "1,234.50", "$1,200", "(45.00)", "12%".</summary>
    public static bool TryNumber(string text, out double value)
    {
        value = 0;
        var s = text.Trim();
        if (s.Length == 0)
        {
            return false;
        }

        var negative = s.StartsWith('(') && s.EndsWith(')');
        if (negative)
        {
            s = s[1..^1];
        }

        s = s.Trim().TrimStart('$', '€', '£', '¥').TrimEnd('%').Replace(",", "").Replace(" ", "");
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
        {
            return false;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "yyyy/MM/dd", "M/d/yyyy", "MM/dd/yyyy", "d MMM yyyy", "MMM d, yyyy", "MMMM d, yyyy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM"];

    public static bool TryDate(string text, out DateOnly value)
    {
        value = default;
        var s = text.Trim();
        if (s.Length < 6)
        {
            return false;
        }

        if (DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt))
        {
            value = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }
}

internal static class CsvReader
{
    public static List<string[]> Read(string text)
    {
        var records = new List<string[]>();
        var field = new StringBuilder();
        var record = new List<string>();
        var quoted = false;
        var i = 0;
        if (text.Length > 0 && text[0] == '﻿')
        {
            i = 1;
        }

        var delimiter = DetectDelimiter(text);
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (c == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                record.Add(field.ToString());
                field.Clear();
                records.Add([.. record]);
                record.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add([.. record]);
        }

        return records;
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        var counts = new[] { ',', ';', '\t' }.Select(d => (d, n: firstLine.Count(c => c == d))).OrderByDescending(x => x.n).First();
        return counts.n > 0 ? counts.d : ',';
    }
}

/// <summary>
/// What Claude is allowed to see of a table: column names, types, ranges and a few sample rows.
/// A 5,000-row spreadsheet profiles to the same size as a 5-row one, which is why a chart costs
/// the same tokens at any row count.
/// </summary>
public sealed record DataProfile(string Source, int RowCount, ImmutableArray<ColumnProfile> Columns, ImmutableArray<ImmutableArray<string>> SampleRows)
{
    public const int SampleSize = 5;

    public static DataProfile Of(string source, DataTable table)
    {
        var columns = table.Columns.Select((col, i) => ColumnProfile.Of(col, table.Rows.Select(r => r[i]))).ToImmutableArray();
        var sample = table.Rows.Take(SampleSize).Select(r => r.Select((v, i) => Format(v, table.Columns[i].Type)).ToImmutableArray()).ToImmutableArray();
        return new DataProfile(source, table.Rows.Count, columns, sample);
    }

    internal static string Format(object? v, ColumnType type) => v switch
    {
        null => "",
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        string s => s.Length > 40 ? s[..40] + "…" : s,
        _ => v.ToString() ?? "",
    };

    /// <summary>A compact text rendering for the prompt.</summary>
    public string ToPromptText()
    {
        var sb = new StringBuilder();
        sb.Append($"data: {Source} ({RowCount} rows)\ncolumns:\n");
        foreach (var c in Columns)
        {
            sb.Append($"- {c.Name} ({c.Type.ToString().ToLowerInvariant()}){(c.Range is null ? "" : ", " + c.Range)}, {c.Distinct} distinct\n");
        }

        sb.Append("sample rows:\n");
        foreach (var r in SampleRows)
        {
            sb.Append("  ").Append(string.Join(" | ", r)).Append('\n');
        }

        return sb.ToString();
    }
}

public sealed record ColumnProfile(string Name, ColumnType Type, int Distinct, string? Range)
{
    public static ColumnProfile Of(Column column, IEnumerable<object?> values)
    {
        var list = values.Where(v => v is not null).ToList();
        var distinct = list.Distinct().Count();
        string? range = null;
        if (list.Count > 0)
        {
            if (column.Type == ColumnType.Number)
            {
                range = $"{list.Cast<double>().Min().ToString("0.##", CultureInfo.InvariantCulture)} to {list.Cast<double>().Max().ToString("0.##", CultureInfo.InvariantCulture)}";
            }
            else if (column.Type == ColumnType.Date)
            {
                range = $"{list.Cast<DateOnly>().Min():yyyy-MM-dd} to {list.Cast<DateOnly>().Max():yyyy-MM-dd}";
            }
        }

        return new ColumnProfile(column.Name, column.Type, distinct, range);
    }
}
