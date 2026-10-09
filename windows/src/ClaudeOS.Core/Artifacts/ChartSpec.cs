using System.Collections.Immutable;
using System.Text.Json;

namespace ClaudeOS.Core.Artifacts;

public enum Mark { Bar, Line, Area, Point }

public sealed class SpecException(string message) : Exception(message);

public sealed record FieldEncoding(string Field, string? Title = null, string? Format = null);

public enum AggregateOp { Sum, Mean, Count, Min, Max, Distinct, Median }

public sealed record Aggregate(AggregateOp Op, string? Field, string As);

public abstract record Transform;

public enum FilterOp { Eq, Ne, Gt, Ge, Lt, Le, In, Contains }

public sealed record FilterTransform(string Field, FilterOp Op, ImmutableArray<object> Values) : Transform;

public sealed record DeriveTransform(string As, string From, string Unit) : Transform;

public sealed record GroupTransform(ImmutableArray<string> By, ImmutableArray<Aggregate> Aggregates) : Transform;

public sealed record SortTransform(string Field, bool Descending) : Transform;

public sealed record LimitTransform(int Count) : Transform;

/// <summary>
/// The compact recipe Claude writes for a chart: a few hundred tokens, a small subset of
/// Vega-Lite, with the grouping and sums included. Local code runs it over every row of the
/// data, so a 5,000-row spreadsheet costs about the same as a 5-row one.
/// </summary>
public sealed record ChartSpec(
    string? Title,
    string DataSource,
    ImmutableArray<Transform> Transforms,
    Mark Mark,
    FieldEncoding X,
    FieldEncoding Y,
    FieldEncoding? Color = null,
    bool Stack = false)
{
    public const int MaxPoints = 600;

    public static ChartSpec Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement);
        }
        catch (JsonException e)
        {
            throw new SpecException($"chart spec is not valid JSON: {e.Message}");
        }
    }

    public static ChartSpec Parse(JsonElement e)
    {
        Require(e.ValueKind == JsonValueKind.Object, "chart spec must be an object");
        var allowed = new HashSet<string> { "type", "title", "data", "transform", "mark", "x", "y", "color", "stack" };
        var unknown = e.EnumerateObject().Select(p => p.Name).Where(n => !allowed.Contains(n)).Order().ToList();
        Require(unknown.Count == 0, $"unknown chart fields: {string.Join(", ", unknown)}");

        var source = e.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String
            ? src.GetString()! : throw new SpecException("chart spec needs data.source (the file name or path of the table)");

        var transforms = ImmutableArray.CreateBuilder<Transform>();
        if (e.TryGetProperty("transform", out var tr) && tr.ValueKind != JsonValueKind.Null)
        {
            Require(tr.ValueKind == JsonValueKind.Array, "transform must be a list");
            foreach (var step in tr.EnumerateArray())
            {
                transforms.Add(ParseTransform(step));
            }
        }

        var mark = Str(e, "mark") switch
        {
            "bar" => Mark.Bar,
            "line" => Mark.Line,
            "area" => Mark.Area,
            "point" => Mark.Point,
            var other => throw new SpecException($"mark must be one of bar, line, area, point (got '{other}')"),
        };

        return new ChartSpec(
            e.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
            source,
            transforms.ToImmutable(),
            mark,
            Encoding(e, "x") ?? throw new SpecException("chart spec needs x: {\"field\": ...}"),
            Encoding(e, "y") ?? throw new SpecException("chart spec needs y: {\"field\": ...}"),
            Encoding(e, "color"),
            e.TryGetProperty("stack", out var st) && st.ValueKind == JsonValueKind.True);
    }

    private static FieldEncoding? Encoding(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Require(v.ValueKind == JsonValueKind.Object, $"{name} must be an object like {{\"field\": \"amount\"}}");
        var unknown = v.EnumerateObject().Select(p => p.Name).Where(n => n is not ("field" or "title" or "format")).ToList();
        Require(unknown.Count == 0, $"unknown {name} fields: {string.Join(", ", unknown)}");
        return new FieldEncoding(Str(v, "field"), v.TryGetProperty("title", out var t) ? t.GetString() : null, v.TryGetProperty("format", out var f) ? f.GetString() : null);
    }

    private static Transform ParseTransform(JsonElement step)
    {
        Require(step.ValueKind == JsonValueKind.Object && step.EnumerateObject().Count() == 1, "each transform step must be an object with exactly one key: filter, derive, group, sort or limit");
        var prop = step.EnumerateObject().First();
        var v = prop.Value;
        switch (prop.Name)
        {
            case "filter":
                {
                    var op = Str(v, "op") switch
                    {
                        "=" or "==" => FilterOp.Eq,
                        "!=" => FilterOp.Ne,
                        ">" => FilterOp.Gt,
                        ">=" => FilterOp.Ge,
                        "<" => FilterOp.Lt,
                        "<=" => FilterOp.Le,
                        "in" => FilterOp.In,
                        "contains" => FilterOp.Contains,
                        var o => throw new SpecException($"filter op must be one of = != > >= < <= in contains (got '{o}')"),
                    };
                    Require(v.TryGetProperty("value", out var val), "filter needs a value");
                    var values = val.ValueKind == JsonValueKind.Array
                        ? val.EnumerateArray().Select(Scalar).ToImmutableArray()
                        : [Scalar(val)];
                    return new FilterTransform(Str(v, "field"), op, values);
                }

            case "derive":
                {
                    var unit = Str(v, "unit");
                    Require(unit is "year" or "quarter" or "month" or "week" or "day" or "weekday", "derive unit must be year, quarter, month, week, day or weekday");
                    return new DeriveTransform(Str(v, "as"), Str(v, "from"), unit);
                }

            case "group":
                {
                    Require(v.TryGetProperty("by", out var by) && by.ValueKind == JsonValueKind.Array, "group needs by: [fields]");
                    Require(v.TryGetProperty("aggregate", out var agg) && agg.ValueKind == JsonValueKind.Array && agg.GetArrayLength() > 0, "group needs aggregate: [{op, field, as}]");
                    var aggregates = agg.EnumerateArray().Select(a =>
                    {
                        var op = Str(a, "op") switch
                        {
                            "sum" => AggregateOp.Sum,
                            "mean" or "avg" => AggregateOp.Mean,
                            "count" => AggregateOp.Count,
                            "min" => AggregateOp.Min,
                            "max" => AggregateOp.Max,
                            "distinct" => AggregateOp.Distinct,
                            "median" => AggregateOp.Median,
                            var o => throw new SpecException($"aggregate op must be one of sum mean count min max distinct median (got '{o}')"),
                        };
                        var field = a.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
                        Require(op == AggregateOp.Count || field is not null, $"aggregate {op.ToString().ToLowerInvariant()} needs a field");
                        return new Aggregate(op, field, a.TryGetProperty("as", out var asName) && asName.ValueKind == JsonValueKind.String ? asName.GetString()! : DefaultName(op, field));
                    }).ToImmutableArray();
                    return new GroupTransform([.. by.EnumerateArray().Select(b => b.GetString() ?? throw new SpecException("group.by entries must be strings"))], aggregates);
                }

            case "sort":
                return new SortTransform(Str(v, "field"), v.TryGetProperty("order", out var ord) && ord.GetString() == "desc");

            case "limit":
                Require(v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n is > 0 and <= 10_000, "limit must be a whole number from 1 to 10000");
                return new LimitTransform(v.GetInt32());

            default:
                throw new SpecException($"unknown transform '{prop.Name}'; use filter, derive, group, sort or limit");
        }
    }

    private static string DefaultName(AggregateOp op, string? field) => op == AggregateOp.Count ? "count" : $"{op.ToString().ToLowerInvariant()}_{field}";

    private static object Scalar(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String => v.GetString()!,
        JsonValueKind.True => 1.0,
        JsonValueKind.False => 0.0,
        _ => throw new SpecException("filter values must be numbers or strings"),
    };

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()! : throw new SpecException($"missing or non-string '{name}'");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new SpecException(message);
        }
    }
}
