using System.Collections.Immutable;
using System.Globalization;

namespace ClaudeOS.Core.Artifacts;

public sealed record DataPoint(object X, double Y);

public sealed record Series(string Name, ImmutableArray<DataPoint> Points);

/// <summary>The result of running a recipe: what the renderer draws and what a screen reader or
/// a "view as table" toggle reads.</summary>
public sealed record ChartData(
    ChartSpec Spec,
    Column XColumn,
    Column YColumn,
    ImmutableArray<Series> Series,
    int SourceRows,
    int UsedRows)
{
    public int PointCount => Series.Sum(s => s.Points.Length);
}

/// <summary>Runs a <see cref="ChartSpec"/> over a full table, deterministically and locally.</summary>
public static class Recipe
{
    public const int MaxSeries = 8;

    public static ChartData Run(ChartSpec spec, DataTable table)
    {
        var current = table;
        foreach (var step in spec.Transforms)
        {
            current = step switch
            {
                FilterTransform f => Filter(current, f),
                DeriveTransform d => Derive(current, d),
                GroupTransform g => Group(current, g),
                SortTransform s => Sort(current, s),
                LimitTransform l => new DataTable(current.Columns, [.. current.Rows.Take(l.Count)]),
                _ => throw new SpecException("unknown transform"),
            };
        }

        var xi = Index(current, spec.X.Field, "x");
        var yi = Index(current, spec.Y.Field, "y");
        var ci = spec.Color is null ? -1 : Index(current, spec.Color.Field, "color");
        var xCol = current.Columns[xi];
        var yCol = current.Columns[yi];
        if (yCol.Type != ColumnType.Number)
        {
            throw new SpecException($"y field '{yCol.Name}' is {yCol.Type.ToString().ToLowerInvariant()}, not a number; aggregate it first (for example sum or count)");
        }

        var groups = new List<(string Name, List<DataPoint> Points)>();
        foreach (var row in current.Rows)
        {
            if (row[xi] is null || row[yi] is not double y)
            {
                continue;
            }

            var name = ci < 0 ? spec.Y.Title ?? spec.Y.Field : Convert.ToString(row[ci], CultureInfo.InvariantCulture) ?? "(blank)";
            var group = groups.FirstOrDefault(g => g.Name == name);
            if (group.Points is null)
            {
                group = (name, []);
                groups.Add(group);
            }

            group.Points.Add(new DataPoint(row[xi]!, y));
        }

        if (groups.Count > MaxSeries)
        {
            throw new SpecException($"{groups.Count} series is too many to tell apart (the limit is {MaxSeries}); filter to the ones that matter, or group by something coarser");
        }

        var series = groups.Select(g => new Series(g.Name, [.. SortPoints(g.Points, xCol)])).ToImmutableArray();
        var total = series.Sum(s => s.Points.Length);
        if (total == 0)
        {
            throw new SpecException("the recipe produced no rows; check the filters");
        }

        if (total > ChartSpec.MaxPoints)
        {
            throw new SpecException($"{total} points is too many to read; group or limit the data (the limit is {ChartSpec.MaxPoints})");
        }

        return new ChartData(spec, xCol, yCol, series, table.Rows.Count, current.Rows.Count);
    }

    private static IEnumerable<DataPoint> SortPoints(List<DataPoint> points, Column x) => x.Type switch
    {
        ColumnType.Date => points.OrderBy(p => (DateOnly)p.X),
        ColumnType.Number => points.OrderBy(p => (double)p.X),
        _ => points,
    };

    private static int Index(DataTable t, string field, string role)
    {
        var i = t.IndexOf(field);
        if (i < 0)
        {
            throw new SpecException($"{role} field '{field}' does not exist; available fields: {string.Join(", ", t.Columns.Select(c => c.Name))}");
        }

        return i;
    }

    private static DataTable Filter(DataTable t, FilterTransform f)
    {
        var i = Index(t, f.Field, "filter");
        var col = t.Columns[i];
        bool Keep(object? cell)
        {
            if (cell is null)
            {
                return false;
            }

            switch (f.Op)
            {
                case FilterOp.Eq: return f.Values.Any(v => Compare(cell, v, col) == 0);
                case FilterOp.Ne: return f.Values.All(v => Compare(cell, v, col) != 0);
                case FilterOp.In: return f.Values.Any(v => Compare(cell, v, col) == 0);
                case FilterOp.Gt: return Compare(cell, f.Values[0], col) > 0;
                case FilterOp.Ge: return Compare(cell, f.Values[0], col) >= 0;
                case FilterOp.Lt: return Compare(cell, f.Values[0], col) < 0;
                case FilterOp.Le: return Compare(cell, f.Values[0], col) <= 0;
                case FilterOp.Contains: return Convert.ToString(cell, CultureInfo.InvariantCulture)!.Contains(Convert.ToString(f.Values[0], CultureInfo.InvariantCulture)!, StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }

        return new DataTable(t.Columns, [.. t.Rows.Where(r => Keep(r[i]))]);
    }

    private static int Compare(object cell, object value, Column col)
    {
        switch (col.Type)
        {
            case ColumnType.Number:
                return value is double d ? ((double)cell).CompareTo(d) : DataTable.TryNumber(value.ToString()!, out var n) ? ((double)cell).CompareTo(n) : 1;
            case ColumnType.Date:
                return DataTable.TryDate(value.ToString()!, out var date) ? ((DateOnly)cell).CompareTo(date) : 1;
            default:
                return string.Compare((string)cell, Convert.ToString(value, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DataTable Derive(DataTable t, DeriveTransform d)
    {
        var from = Index(t, d.From, "derive");
        if (t.Columns[from].Type != ColumnType.Date)
        {
            throw new SpecException($"derive needs a date column; '{d.From}' is {t.Columns[from].Type.ToString().ToLowerInvariant()}");
        }

        DateOnly Bucket(DateOnly x) => d.Unit switch
        {
            "year" => new DateOnly(x.Year, 1, 1),
            "quarter" => new DateOnly(x.Year, ((x.Month - 1) / 3 * 3) + 1, 1),
            "month" => new DateOnly(x.Year, x.Month, 1),
            "week" => x.AddDays(-(((int)x.DayOfWeek + 6) % 7)),
            _ => x,
        };

        if (d.Unit == "weekday")
        {
            var names = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var rows = t.Rows.Select(r => (object?[])[.. r, r[from] is DateOnly x ? names[((int)x.DayOfWeek + 6) % 7] : null]).ToList();
            return new DataTable([.. t.Columns, new Column(d.As, ColumnType.Text)], rows);
        }

        var withBucket = t.Rows.Select(r => (object?[])[.. r, r[from] is DateOnly x ? Bucket(x) : null]).ToList();
        return new DataTable([.. t.Columns, new Column(d.As, ColumnType.Date, d.Unit)], withBucket);
    }

    private static DataTable Group(DataTable t, GroupTransform g)
    {
        var byIdx = g.By.Select(b => Index(t, b, "group")).ToArray();
        var outCols = byIdx.Select(i => t.Columns[i]).ToList();
        foreach (var a in g.Aggregates)
        {
            if (a.Field is not null)
            {
                var col = t.Columns[Index(t, a.Field, "aggregate")];
                if (a.Op is AggregateOp.Sum or AggregateOp.Mean or AggregateOp.Median && col.Type != ColumnType.Number)
                {
                    throw new SpecException($"cannot {a.Op.ToString().ToLowerInvariant()} '{a.Field}': it is {col.Type.ToString().ToLowerInvariant()}, not a number");
                }
            }

            outCols.Add(new Column(a.As, ColumnType.Number));
        }

        var buckets = new Dictionary<string, (object?[] Key, List<object?[]> Rows)>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var row in t.Rows)
        {
            var key = byIdx.Select(i => row[i]).ToArray();
            var id = string.Join("\u001f", key.Select(k => Convert.ToString(k, CultureInfo.InvariantCulture)));
            if (!buckets.TryGetValue(id, out var b))
            {
                b = (key, []);
                buckets[id] = b;
                order.Add(id);
            }

            b.Rows.Add(row);
        }

        var rows = new List<object?[]>();
        foreach (var id in order)
        {
            var (key, members) = buckets[id];
            var result = new List<object?>(key);
            foreach (var a in g.Aggregates)
            {
                var values = a.Field is null ? [] : members.Select(r => r[t.IndexOf(a.Field)]).Where(v => v is not null).ToList();
                result.Add(Aggregate(a.Op, members.Count, values));
            }

            rows.Add([.. result]);
        }

        return new DataTable(outCols, rows);
    }

    private static object? Aggregate(AggregateOp op, int rowCount, List<object?> values)
    {
        switch (op)
        {
            case AggregateOp.Count: return (double)rowCount;
            case AggregateOp.Distinct: return (double)values.Distinct().Count();
            case AggregateOp.Sum: return values.Sum(v => (double)v!);
            case AggregateOp.Mean: return values.Count == 0 ? null : values.Average(v => (double)v!);
            case AggregateOp.Median:
                {
                    var sorted = values.Select(v => (double)v!).Order().ToList();
                    if (sorted.Count == 0)
                    {
                        return null;
                    }

                    var mid = sorted.Count / 2;
                    return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
                }

            case AggregateOp.Min: return values.Count == 0 ? null : values.Min(v => v is double d ? (IComparable)d : v is DateOnly o ? o : (string)v!);
            default: return values.Count == 0 ? null : values.Max(v => v is double d ? (IComparable)d : v is DateOnly o ? o : (string)v!);
        }
    }

    private static DataTable Sort(DataTable t, SortTransform s)
    {
        var i = Index(t, s.Field, "sort");
        IEnumerable<object?[]> ordered = s.Descending ? t.Rows.OrderByDescending(r => r[i], CellComparer.Instance) : t.Rows.OrderBy(r => r[i], CellComparer.Instance);
        return new DataTable(t.Columns, [.. ordered]);
    }

    private sealed class CellComparer : IComparer<object?>
    {
        public static readonly CellComparer Instance = new();

        public int Compare(object? a, object? b) => (a, b) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            (double x, double y) => x.CompareTo(y),
            (DateOnly x, DateOnly y) => x.CompareTo(y),
            _ => string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase),
        };
    }
}
