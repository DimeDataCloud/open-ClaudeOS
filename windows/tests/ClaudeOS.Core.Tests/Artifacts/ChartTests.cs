using System.Globalization;
using System.Text;
using System.Xml.Linq;
using ClaudeOS.Core.Artifacts;
using ClaudeOS.Core.Design;

namespace ClaudeOS.Core.Tests.Artifacts;

public sealed class ChartTests
{
    private const string Csv = """
        date,category,vendor,amount
        2026-01-03,Software,Figma,"1,200.50"
        2026-01-17,Travel,Delta,$300
        2026-02-02,Software,Notion,99
        2026-02-20,Travel,Uber,(40.00)
        2026-03-05,Software,GitHub,450
        2026-03-30,Marketing,Meta Ads,800
        """;

    private static string SpecJson(string mark = "bar", string extra = "") => """
        {
          "type": "chart", "title": "Spend", "data": {"source": "x.csv"},
          "transform": [
            {"derive": {"as": "month", "from": "date", "unit": "month"}},
            {"group": {"by": ["month"], "aggregate": [{"op": "sum", "field": "amount", "as": "spend"}]}}
          ],
          "mark": "__MARK__", "x": {"field": "month"}, "y": {"field": "spend", "format": "currency:USD"} __EXTRA__
        }
        """.Replace("__MARK__", mark).Replace("__EXTRA__", extra);

    private static ResolvedTheme Light => ThemeResolver.Resolve(Appearance.Light);

    [Fact]
    public void Csv_is_parsed_with_quotes_types_and_money()
    {
        var t = DataTable.FromCsv(Csv);
        Assert.Equal([ColumnType.Date, ColumnType.Text, ColumnType.Text, ColumnType.Number], t.Columns.Select(c => c.Type));
        Assert.Equal(6, t.Rows.Count);
        Assert.Equal(1200.5, t.Rows[0][3]);
        Assert.Equal(300.0, t.Rows[1][3]);
        Assert.Equal(-40.0, t.Rows[3][3]);
        Assert.Equal(new DateOnly(2026, 1, 3), t.Rows[0][0]);
    }

    [Fact]
    public void Csv_handles_embedded_newlines_semicolons_bom_and_blank_lines()
    {
        var t = DataTable.FromCsv("﻿name;note\r\n\"A;B\";\"line one\nline two\"\r\n\r\nC;plain\r\n");
        Assert.Equal(["name", "note"], t.Columns.Select(c => c.Name));
        Assert.Equal(2, t.Rows.Count);
        Assert.Equal("A;B", t.Rows[0][0]);
        Assert.Equal("line one\nline two", t.Rows[0][1]);
    }

    [Fact]
    public void A_recipe_groups_and_sums_over_every_row()
    {
        var data = Recipe.Run(ChartSpec.Parse(SpecJson()), DataTable.FromCsv(Csv));
        var s = Assert.Single(data.Series);
        Assert.Equal([1500.5, 59.0, 1250.0], s.Points.Select(p => p.Y));
        Assert.Equal(new DateOnly(2026, 1, 1), s.Points[0].X);
        Assert.Equal(6, data.SourceRows);
    }

    [Fact]
    public void Filters_and_series_splits_work()
    {
        var spec = ChartSpec.Parse("""
            {"type":"chart","data":{"source":"x.csv"},
             "transform":[{"filter":{"field":"category","op":"!=","value":"Marketing"}},
                          {"derive":{"as":"month","from":"date","unit":"month"}},
                          {"group":{"by":["month","category"],"aggregate":[{"op":"count","as":"n"}]}}],
             "mark":"line","x":{"field":"month"},"y":{"field":"n"},"color":{"field":"category"}}
            """);
        var data = Recipe.Run(spec, DataTable.FromCsv(Csv));
        Assert.Equal(["Software", "Travel"], data.Series.Select(s => s.Name).Order());
        Assert.Equal(5, data.PointCount);
    }

    [Theory]
    [InlineData("""{"type":"chart","data":{"source":"x.csv"},"mark":"bar","x":{"field":"nope"},"y":{"field":"amount"}}""", "x field 'nope' does not exist; available fields: date, category, vendor, amount")]
    [InlineData("""{"type":"chart","data":{"source":"x.csv"},"mark":"bar","x":{"field":"category"},"y":{"field":"vendor"}}""", "not a number")]
    [InlineData("""{"type":"chart","data":{"source":"x.csv"},"transform":[{"group":{"by":["category"],"aggregate":[{"op":"sum","field":"vendor"}]}}],"mark":"bar","x":{"field":"category"},"y":{"field":"sum_vendor"}}""", "cannot sum 'vendor'")]
    [InlineData("""{"type":"chart","data":{"source":"x.csv"},"transform":[{"filter":{"field":"category","op":"=","value":"Nothing"}}],"mark":"bar","x":{"field":"category"},"y":{"field":"amount"}}""", "no rows")]
    [InlineData("""{"type":"chart","data":{"source":"x.csv"},"transform":[{"derive":{"as":"d","from":"vendor","unit":"month"}}],"mark":"bar","x":{"field":"d"},"y":{"field":"amount"}}""", "needs a date column")]
    public void Bad_recipes_fail_with_a_message_the_model_can_act_on(string json, string expected)
    {
        var e = Assert.Throws<SpecException>(() => Recipe.Run(ChartSpec.Parse(json), DataTable.FromCsv(Csv)));
        Assert.Contains(expected, e.Message);
    }

    [Theory]
    [InlineData("""{"type":"chart","mark":"bar","x":{"field":"a"},"y":{"field":"b"}}""", "data.source")]
    [InlineData("""{"type":"chart","data":{"source":"x"},"mark":"pie","x":{"field":"a"},"y":{"field":"b"}}""", "mark must be one of")]
    [InlineData("""{"type":"chart","data":{"source":"x"},"mark":"bar","x":{"field":"a"},"y":{"field":"b"},"script":"alert(1)"}""", "unknown chart fields: script")]
    [InlineData("""{"type":"chart","data":{"source":"x"},"transform":[{"eval":"x"}],"mark":"bar","x":{"field":"a"},"y":{"field":"b"}}""", "unknown transform 'eval'")]
    [InlineData("not json", "not valid JSON")]
    public void Malformed_specs_are_rejected_at_the_door(string json, string expected) =>
        Assert.Contains(expected, Assert.Throws<SpecException>(() => ChartSpec.Parse(json)).Message);

    [Fact]
    public void Too_many_series_are_refused_rather_than_inventing_a_ninth_colour()
    {
        var sb = new StringBuilder("g,v\n");
        for (var i = 0; i < 9; i++)
        {
            sb.Append($"s{i},{i + 1}\n");
        }

        var spec = ChartSpec.Parse("""{"type":"chart","data":{"source":"x"},"mark":"bar","x":{"field":"g"},"y":{"field":"v"},"color":{"field":"g"}}""");
        var e = Assert.Throws<SpecException>(() => Recipe.Run(spec, DataTable.FromCsv(sb.ToString())));
        Assert.Contains("9 series is too many", e.Message);
    }

    [Fact]
    public void The_profile_Claude_sees_costs_the_same_at_any_row_count()
    {
        static string Make(int rows)
        {
            var sb = new StringBuilder("date,category,amount\n");
            var d = new DateOnly(2026, 1, 1);
            for (var i = 0; i < rows; i++)
            {
                sb.Append(CultureInfo.InvariantCulture, $"{d.AddDays(i % 270):yyyy-MM-dd},{(i % 4 == 0 ? "Software" : "Travel")},{100 + (i % 37)}.50\n");
            }

            return sb.ToString();
        }

        var small = DataProfile.Of("q3.csv", DataTable.FromCsv(Make(60))).ToPromptText();
        var large = DataProfile.Of("q3.csv", DataTable.FromCsv(Make(20_000))).ToPromptText();
        Assert.True(Math.Abs(large.Length - small.Length) < 40, $"{small.Length} vs {large.Length} characters");
        Assert.Contains("20000 rows", large);

        // ...and the recipe still runs over every row locally.
        var data = Recipe.Run(ChartSpec.Parse(SpecJson().Replace("\"x.csv\"", "\"q3.csv\"")), DataTable.FromCsv(Make(20_000)));
        Assert.Equal(20_000, data.SourceRows);
        var expectedTotal = Enumerable.Range(0, 20_000).Sum(i => 100 + (i % 37) + 0.5);
        Assert.Equal(expectedTotal, data.Series.Sum(x => x.Points.Sum(p => p.Y)), 3); // every row was summed
    }

    [Fact]
    public void A_bar_chart_is_wellformed_labelled_and_accessible()
    {
        var data = Recipe.Run(ChartSpec.Parse(SpecJson()), DataTable.FromCsv(Csv));
        var render = ChartRenderer.Render(data, new ChartStyle(Light, 640, 380));

        var doc = XDocument.Parse(render.Svg); // well-formed XML
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal("img", doc.Root!.Attribute("role")!.Value);
        Assert.Contains("Bar chart: Spend", doc.Root.Attribute("aria-label")!.Value);
        Assert.Contains("Highest: Jan at $1,501", render.AltText);
        Assert.Equal(3, render.Marks.Length);
        Assert.Equal(["Jan", "Feb", "Mar"], render.Marks.Select(m => m.XLabel));
        Assert.Contains(doc.Descendants(ns + "text"), t => t.Value == "Spend");
        Assert.DoesNotContain(doc.Descendants(ns + "circle"), _ => true); // a single series needs no legend
    }

    [Fact]
    public void Text_never_wears_a_series_colour_and_a_legend_appears_for_two_or_more_series()
    {
        var spec = ChartSpec.Parse("""
            {"type":"chart","title":"By category","data":{"source":"x.csv"},
             "transform":[{"derive":{"as":"month","from":"date","unit":"month"}},
                          {"group":{"by":["month","category"],"aggregate":[{"op":"sum","field":"amount","as":"spend"}]}}],
             "mark":"line","x":{"field":"month"},"y":{"field":"spend"},"color":{"field":"category"}}
            """);
        var data = Recipe.Run(spec, DataTable.FromCsv(Csv));
        var render = ChartRenderer.Render(data, new ChartStyle(Light, 640, 380));
        var doc = XDocument.Parse(render.Svg);
        XNamespace ns = "http://www.w3.org/2000/svg";

        var inks = new HashSet<string>(new[] { "ink.primary", "ink.secondary", "ink.tertiary" }.Select(Light.Hex), StringComparer.OrdinalIgnoreCase);
        Assert.All(doc.Descendants(ns + "text"), t => Assert.Contains(t.Attribute("fill")!.Value, inks));

        var legend = doc.Descendants(ns + "text").Select(t => t.Value).ToList();
        Assert.Contains("Software", legend);
        Assert.Contains("Travel", legend);
        Assert.Equal(data.Series.Length, doc.Descendants(ns + "path").Count(p => p.Attribute("stroke") is not null && p.Attribute("stroke-width")?.Value == "2"));
    }

    [Fact]
    public void Bars_are_anchored_square_to_the_baseline_with_a_rounded_data_end()
    {
        var data = Recipe.Run(ChartSpec.Parse(SpecJson()), DataTable.FromCsv(Csv));
        var render = ChartRenderer.Render(data, new ChartStyle(Light, 640, 380));
        var bar = XDocument.Parse(render.Svg).Descendants().First(e => e.Name.LocalName == "path" && e.Attribute("fill")?.Value == Light.Hex("accent.mark"));
        var d = bar.Attribute("d")!.Value;
        Assert.Contains("Q", d); // quadratic data-end corners
        Assert.StartsWith("M", d);
        // Starts at the bottom-left corner, which is on the baseline: its y equals the bar's lowest y.
        var ys = System.Text.RegularExpressions.Regex.Matches(d, @"[ML]\s*[\d.]+,([\d.]+)").Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(ys.Max(), ys[0], 2);
    }

    [Theory]
    [InlineData(0, 10193, 5, 0, 12500)]
    [InlineData(0, 100, 5, 0, 100)]
    [InlineData(0, 0.8, 5, 0, 0.8)]
    [InlineData(-30, 55, 5, -40, 60)]
    public void Axes_use_round_numbers(double lo, double hi, int count, double min, double max)
    {
        var (a, b, ticks) = ChartRenderer.NiceScale(lo, hi, count);
        Assert.Equal(min, a, 6);
        Assert.Equal(max, b, 6);
        Assert.True(ticks.Count is >= 3 and <= 10);
    }

    [Theory]
    [InlineData(1500.5, null, true, "1,501")]
    [InlineData(12500, "currency:USD", true, "$12.5k")]
    [InlineData(12500, "currency:USD", false, "$12,500")]
    [InlineData(475.3, "currency:USD", false, "$475")]
    [InlineData(4.5, "currency:USD", false, "$4.5")]
    [InlineData(-1200, "currency:EUR", false, "−€1,200")]
    [InlineData(0.256, "percent", false, "25.6%")]
    [InlineData(2_400_000, null, true, "2.4M")]
    public void Numbers_are_formatted_for_people(double v, string? format, bool compact, string expected) =>
        Assert.Equal(expected, Fmt.Value(v, format, compact));

    [Fact]
    public void Dark_charts_use_the_dark_palette_and_surface()
    {
        var data = Recipe.Run(ChartSpec.Parse(SpecJson()), DataTable.FromCsv(Csv));
        var dark = ThemeResolver.Resolve(Appearance.Dark);
        var svg = ChartRenderer.Render(data, new ChartStyle(dark, 640, 380)).Svg;
        Assert.Contains(dark.Hex("surface.raised"), svg);
        Assert.Contains(dark.Hex("accent.mark"), svg);
        Assert.DoesNotContain("#FFFFFF", svg);
    }

    [Fact]
    public void Preferred_size_grows_with_the_data_but_stays_reasonable()
    {
        var data = Recipe.Run(ChartSpec.Parse(SpecJson()), DataTable.FromCsv(Csv));
        var size = ChartRenderer.PreferredSize(data);
        Assert.InRange(size.Width, 480, 960);
        Assert.InRange(size.Height, 300, 520);
    }
}
