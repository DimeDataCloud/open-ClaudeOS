using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Layout;

namespace ClaudeOS.Core.Artifacts;

public sealed record ChartStyle(ResolvedTheme Theme, int Width, int Height, bool Transparent = false);

/// <summary>A drawn mark and what it means, so the shell can show a tooltip on hover and a
/// screen reader can walk the data without parsing the picture.</summary>
public sealed record MarkInfo(Rect Bounds, string Series, string XLabel, string Value);

public sealed record ChartRender(string Svg, Size Size, ImmutableArray<MarkInfo> Marks, string AltText);

/// <summary>
/// Draws a chart as SVG from the design tokens. The same output is shown by the WinUI shell (as a
/// native SVG image), by the prototype, and in the docs, so what is designed is what ships.
///
/// The marks follow the data-visualisation rules the project holds itself to: thin marks, bars
/// anchored to the baseline with a rounded data end, two-pixel lines, a two-pixel surface gap
/// between touching fills, recessive grid and axes, text in text colours (never the series
/// colour), a legend for two or more series, and selective direct labels.
/// </summary>
public static class ChartRenderer
{
    private const double TickFont = 12;

    /// <summary>A comfortable window size, in device-independent pixels, for the data.</summary>
    public static Size PreferredSize(ChartData data)
    {
        var n = XKeys(data).Count;
        var width = Math.Clamp(180 + (n * (data.Spec.Mark == Mark.Bar ? 64 : 52)), 480, 960);
        var height = (int)Math.Clamp(width * 0.6, 300, 520);
        return new Size(width, height);
    }

    public static ChartRender Render(ChartData data, ChartStyle style)
    {
        var t = style.Theme;
        var spec = data.Spec;
        var w = style.Width;
        var h = style.Height;
        var xKeys = XKeys(data);
        var multi = data.Series.Length > 1;
        var stacked = spec.Stack && multi && spec.Mark is Mark.Bar or Mark.Area;

        // ---- scales
        double lo = 0, hi = 0;
        var allY = data.Series.SelectMany(s => s.Points.Select(p => p.Y)).ToList();
        if (stacked)
        {
            var totals = xKeys.Select(k => data.Series.Sum(s => s.Points.Where(p => Key(p.X) == Key(k)).Sum(p => p.Y))).ToList();
            hi = totals.Max();
            lo = Math.Min(0, allY.Min());
        }
        else
        {
            hi = allY.Max();
            lo = allY.Min();
        }

        var zeroBase = spec.Mark is Mark.Bar or Mark.Area || (lo >= 0 && lo < hi * 0.5);
        if (zeroBase)
        {
            lo = Math.Min(0, lo);
            hi = Math.Max(0, hi);
        }

        var (yMin, yMax, ticks) = NiceScale(lo, hi, 5);

        var yFormat = spec.Y.Format;
        var tickLabels = ticks.Select(v => Fmt.Value(v, yFormat, compact: true)).ToList();
        var yLabelW = tickLabels.Max(l => TextWidth(l, TickFont));

        var titleH = string.IsNullOrWhiteSpace(spec.Title) ? 0 : 30;
        var legendH = multi ? 34 : 0;
        var directLabels = multi && data.Series.Length <= 4 && spec.Mark is Mark.Line or Mark.Area;
        var rightPad = directLabels ? Math.Min(130, data.Series.Max(s => TextWidth(s.Name, TickFont)) + 16) : 20;
        var padL = 20 + yLabelW + 8;
        var padT = 14 + titleH + legendH;
        var padB = 34;
        var plot = new Rect((int)padL, (int)padT, (int)(w - padL - rightPad), (int)(h - padT - padB));

        double YPos(double v) => plot.Bottom - ((v - yMin) / (yMax - yMin) * plot.Height);
        var bandW = (double)plot.Width / Math.Max(1, xKeys.Count);
        var bucketed = data.XColumn.Type == ColumnType.Date && data.XColumn.Unit is "year" or "quarter" or "month";
        var useBand = spec.Mark == Mark.Bar || data.XColumn.Type == ColumnType.Text;
        var evenly = useBand || bucketed;
        double XPos(object key)
        {
            var i = xKeys.FindIndex(k => Key(k) == Key(key));
            if (useBand)
            {
                return plot.X + ((i + 0.5) * bandW);
            }

            if (bucketed)
            {
                var inset0 = 14;
                return xKeys.Count == 1 ? plot.X + (plot.Width / 2.0) : plot.X + inset0 + (i * (plot.Width - (2.0 * inset0)) / (xKeys.Count - 1));
            }

            double Num(object o) => o is DateOnly d ? d.DayNumber : (double)o;
            var min = Num(xKeys[0]);
            var max = Num(xKeys[^1]);
            var inset = 14;
            return max == min ? plot.X + (plot.Width / 2.0) : plot.X + inset + ((Num(key) - min) / (max - min) * (plot.Width - (2 * inset)));
        }

        var marks = ImmutableArray.CreateBuilder<MarkInfo>();
        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\" role=\"img\" aria-label=\"{Esc(AltTextFor(data, xKeys))}\" font-family=\"{FontAttr(t)}\" font-size=\"12\">");
        svg.Append($"<title>{Esc(spec.Title ?? DefaultTitle(data))}</title><desc>{Esc(AltTextFor(data, xKeys))}</desc>");
        if (!style.Transparent)
        {
            svg.Append($"<rect width=\"{w}\" height=\"{h}\" fill=\"{t.Hex("surface.raised")}\"/>");
        }

        // ---- title and legend
        if (titleH > 0)
        {
            svg.Append(Text(20, 14 + 17, spec.Title!, t.Hex("ink.primary"), 15, 600, "start"));
        }

        if (multi)
        {
            double lx = 20;
            var ly = 14 + titleH + 6;
            for (var i = 0; i < data.Series.Length; i++)
            {
                svg.Append($"<circle cx=\"{N(lx + 4)}\" cy=\"{N(ly + 6)}\" r=\"4\" fill=\"{SeriesColor(t, i)}\"/>");
                svg.Append(Text(lx + 14, ly + 10, data.Series[i].Name, t.Hex("ink.secondary"), 12, 400, "start"));
                lx += 14 + TextWidth(data.Series[i].Name, 12) + 18;
            }
        }

        // ---- grid and y labels (recessive)
        foreach (var (v, label) in ticks.Zip(tickLabels))
        {
            var y = YPos(v);
            var isBase = Math.Abs(v - Math.Max(yMin, Math.Min(0, yMax))) < 1e-9 && zeroBase;
            svg.Append($"<line x1=\"{plot.X}\" x2=\"{plot.Right}\" y1=\"{N(y)}\" y2=\"{N(y)}\" stroke=\"{(isBase ? t.Hex("line.strong") : t.Hex("line.hairline"))}\" stroke-width=\"1\"/>");
            svg.Append(Text(plot.X - 8, y + 4, label, t.Hex("ink.tertiary"), TickFont, 400, "end", numeric: true));
        }

        // ---- x labels (thinned when crowded)
        var xLabels = Fmt.XLabels(xKeys, data.XColumn);
        var maxLabelW = xLabels.Max(l => TextWidth(l, TickFont)) + 10;
        var slot = useBand ? bandW : Math.Max(1, (plot.Width - 28.0) / Math.Max(1, xKeys.Count - 1));
        _ = evenly;
        var every = Math.Max(1, (int)Math.Ceiling(maxLabelW / slot));
        for (var i = 0; i < xKeys.Count; i++)
        {
            if (i % every != 0 && i != xKeys.Count - 1 || (i == xKeys.Count - 1 && i % every != 0 && (xKeys.Count - 1 - (i / every * every)) * slot < maxLabelW))
            {
                continue;
            }

            var label = every == 1 && data.XColumn.Type == ColumnType.Text ? Truncate(xLabels[i], (int)(slot / 6.6)) : xLabels[i];
            svg.Append(Text(XPos(xKeys[i]), plot.Bottom + 20, label, t.Hex("ink.secondary"), TickFont, 400, "middle"));
        }

        // ---- marks
        switch (spec.Mark)
        {
            case Mark.Bar:
                DrawBars(svg, marks, data, t, xKeys, plot, bandW, stacked, YPos, XPos, yMin, yFormat);
                break;
            case Mark.Line or Mark.Area:
                DrawLines(svg, marks, data, t, xKeys, plot, spec.Mark == Mark.Area, stacked, directLabels, YPos, XPos, yFormat, Math.Max(yMin, 0));
                break;
            default:
                DrawPoints(svg, marks, data, t, YPos, XPos, yFormat);
                break;
        }

        svg.Append("</svg>");
        return new ChartRender(svg.ToString(), new Size(w, h), marks.ToImmutable(), AltTextFor(data, xKeys));
    }

    // ------------------------------------------------------------------ bars
    private static void DrawBars(StringBuilder svg, ImmutableArray<MarkInfo>.Builder marks, ChartData data, ResolvedTheme t, List<object> xKeys, Rect plot, double bandW, bool stacked, Func<double, double> yPos, Func<object, double> xPos, double yMin, string? yFormat)
    {
        var n = data.Series.Length;
        var zeroY = yPos(Math.Max(yMin, 0));
        var radius = Math.Min(4.0, t.Radius(4));
        var surface = t.Hex("surface.raised");
        var labelAll = n == 1 && xKeys.Count <= 8;
        var maxPoint = data.Series.SelectMany(s => s.Points).MaxBy(p => p.Y)!;

        for (var xi = 0; xi < xKeys.Count; xi++)
        {
            var key = xKeys[xi];
            var cx = xPos(key);
            var groupW = stacked || n == 1 ? Math.Min(bandW * 0.62, 56) : Math.Min(bandW * 0.78, 40 * n);
            var barW = stacked || n == 1 ? groupW : (groupW - (2.0 * (n - 1))) / n;
            double stackTop = zeroY;
            double stackTotal = 0;

            for (var si = 0; si < n; si++)
            {
                var p = data.Series[si].Points.FirstOrDefault(q => Key(q.X) == Key(key));
                if (p is null)
                {
                    continue;
                }

                var x = stacked || n == 1 ? cx - (barW / 2) : cx - (groupW / 2) + (si * (barW + 2));
                var value = p.Y;
                double top, bottom;
                if (stacked)
                {
                    bottom = stackTop;
                    top = bottom - Math.Abs(zeroY - yPos(value)) + (value >= 0 ? 0 : 0);
                    stackTop = top - 2; // the surface gap between touching fills
                    stackTotal += value;
                }
                else
                {
                    top = Math.Min(zeroY, yPos(value));
                    bottom = Math.Max(zeroY, yPos(value));
                }

                var height = Math.Max(1.0, bottom - top);
                var isCap = !stacked || si == n - 1 || data.Series.Skip(si + 1).All(s => s.Points.All(q => Key(q.X) != Key(key)));
                var color = SeriesColor(t, si, lone: n == 1, data.Spec.SeriesColor);
                svg.Append(BarPath(x, value >= 0 ? top : bottom - height, barW, height, isCap ? radius : 0, value >= 0, color));
                var label = Fmt.Value(value, yFormat, compact: false);
                marks.Add(new MarkInfo(new Rect((int)x, (int)top, (int)Math.Ceiling(barW), (int)Math.Ceiling(height)), data.Series[si].Name, Fmt.XLabel(key, data.XColumn, xKeys), label));

                var showLabel = !stacked && n == 1 && (labelAll || ReferenceEquals(p, maxPoint));
                if (showLabel)
                {
                    var ly = value >= 0 ? top - 6 : bottom + 14;
                    svg.Append(Text(x + (barW / 2), ly, label, t.Hex("ink.primary"), 12, 500, "middle", numeric: true));
                }
            }

            if (stacked && xKeys.Count <= 8)
            {
                svg.Append(Text(cx, stackTop - 4, Fmt.Value(stackTotal, yFormat, compact: false), t.Hex("ink.primary"), 12, 500, "middle", numeric: true));
            }
        }

        _ = surface;
        _ = plot;
    }

    /// <summary>A bar anchored square to the baseline with a rounded data end.</summary>
    private static string BarPath(double x, double y, double w, double h, double r, bool up, string fill)
    {
        r = Math.Min(r, Math.Min(w / 2, h));
        if (r <= 0.5)
        {
            return $"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(w)}\" height=\"{N(h)}\" fill=\"{fill}\"/>";
        }

        var x2 = x + w;
        var y2 = y + h;
        string d = up
            ? $"M{N(x)},{N(y2)} L{N(x)},{N(y + r)} Q{N(x)},{N(y)} {N(x + r)},{N(y)} L{N(x2 - r)},{N(y)} Q{N(x2)},{N(y)} {N(x2)},{N(y + r)} L{N(x2)},{N(y2)} Z"
            : $"M{N(x)},{N(y)} L{N(x)},{N(y2 - r)} Q{N(x)},{N(y2)} {N(x + r)},{N(y2)} L{N(x2 - r)},{N(y2)} Q{N(x2)},{N(y2)} {N(x2)},{N(y2 - r)} L{N(x2)},{N(y)} Z";
        return $"<path d=\"{d}\" fill=\"{fill}\"/>";
    }

    // ------------------------------------------------------------------ lines and areas
    private static void DrawLines(StringBuilder svg, ImmutableArray<MarkInfo>.Builder marks, ChartData data, ResolvedTheme t, List<object> xKeys, Rect plot, bool area, bool stacked, bool directLabels, Func<double, double> yPos, Func<object, double> xPos, string? yFormat, double floorValue)
    {
        var surface = t.Hex("surface.raised");
        var running = new Dictionary<string, double>();
        var seriesIndex = 0;
        var pending = new List<(string Name, double Y, double X)>();
        foreach (var s in data.Series)
        {
            var color = SeriesColor(t, seriesIndex, lone: data.Series.Length == 1, data.Spec.SeriesColor);
            var pts = new List<(double X, double Y, DataPoint P)>();
            var floors = new List<(double X, double Y)>();
            foreach (var p in s.Points)
            {
                var k = Key(p.X);
                var baseline = stacked ? running.GetValueOrDefault(k) : floorValue;
                if (stacked)
                {
                    running[k] = baseline + p.Y;
                }

                var px = xPos(p.X);
                pts.Add((px, yPos(stacked ? baseline + p.Y : p.Y), p));
                floors.Add((px, yPos(stacked ? baseline : floorValue)));
            }

            if (area)
            {
                // Between this series' line and the one below it (or the baseline).
                var path = new StringBuilder($"M{N(pts[0].X)},{N(pts[0].Y)}");
                foreach (var pt in pts.Skip(1))
                {
                    path.Append($" L{N(pt.X)},{N(pt.Y)}");
                }

                for (var i = floors.Count - 1; i >= 0; i--)
                {
                    path.Append($" L{N(floors[i].X)},{N(floors[i].Y)}");
                }

                path.Append(" Z");
                svg.Append($"<path d=\"{path}\" fill=\"{color}\" fill-opacity=\"{(stacked ? "0.55" : "0.14")}\"/>");
            }

            var line = string.Join(" ", pts.Select((pt, i) => $"{(i == 0 ? "M" : "L")}{N(pt.X)},{N(pt.Y)}"));
            svg.Append($"<path d=\"{line}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");

            foreach (var pt in pts)
            {
                marks.Add(new MarkInfo(new Rect((int)pt.X - 6, (int)pt.Y - 6, 12, 12), s.Name, Fmt.XLabel(pt.P.X, data.XColumn, xKeys), Fmt.Value(pt.P.Y, yFormat, compact: false)));
            }

            var last = pts[^1];
            if (!(stacked && area))
            {
                svg.Append($"<circle cx=\"{N(last.X)}\" cy=\"{N(last.Y)}\" r=\"4\" fill=\"{color}\" stroke=\"{surface}\" stroke-width=\"2\"/>");
            }

            if (directLabels)
            {
                // A stacked band is named at its middle; a line is named at its end.
                var anchorY = stacked && area ? (last.Y + floors[^1].Y) / 2 : last.Y;
                pending.Add((s.Name, anchorY + 4, last.X + 10));
            }
            else if (data.Series.Length == 1)
            {
                var valueLabel = Fmt.Value(last.P.Y, yFormat, compact: false);
                var nearEdge = last.X + (TextWidth(valueLabel, 12) / 2) > plot.Right + 12;
                svg.Append(Text(nearEdge ? last.X + 10 : last.X, last.Y - 12, valueLabel, t.Hex("ink.primary"), 12, 500, nearEdge ? "end" : "middle", numeric: true));
            }

            seriesIndex++;
        }

    }

    // ------------------------------------------------------------------ points
    private static void DrawPoints(StringBuilder svg, ImmutableArray<MarkInfo>.Builder marks, ChartData data, ResolvedTheme t, Func<double, double> yPos, Func<object, double> xPos, string? yFormat)
    {
        var surface = t.Hex("surface.raised");
        var xKeys = XKeys(data);
        for (var si = 0; si < data.Series.Length; si++)
        {
            foreach (var p in data.Series[si].Points)
            {
                double cx = xPos(p.X), cy = yPos(p.Y);
                svg.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"4.5\" fill=\"{SeriesColor(t, si, lone: data.Series.Length == 1, data.Spec.SeriesColor)}\" stroke=\"{surface}\" stroke-width=\"1.5\"/>");
                marks.Add(new MarkInfo(new Rect((int)cx - 8, (int)cy - 8, 16, 16), data.Series[si].Name, Fmt.XLabel(p.X, data.XColumn, xKeys), Fmt.Value(p.Y, yFormat, compact: false)));
            }
        }
    }

    // ------------------------------------------------------------------ helpers
    internal static List<object> XKeys(ChartData data)
    {
        var keys = data.Series.SelectMany(s => s.Points.Select(p => p.X)).GroupBy(Key).Select(g => g.First()).ToList();
        return data.XColumn.Type switch
        {
            ColumnType.Date => [.. keys.OrderBy(k => (DateOnly)k)],
            ColumnType.Number => [.. keys.OrderBy(k => (double)k)],
            _ => keys,
        };
    }

    private static string Key(object o) => o switch
    {
        DateOnly d => d.DayNumber.ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        _ => o.ToString() ?? "",
    };

    /// <summary>A lone series wears the accent: it is the one place colour is free. Two or more series
    /// use the validated categorical palette in its fixed order, whatever the accent is, so a custom
    /// accent can never collide with another series.</summary>
    private static string SeriesColor(ResolvedTheme t, int i, bool lone = false, string? chosen = null)
    {
        if (!lone)
        {
            return t.Series[i % t.Series.Length];
        }

        // "Make the bars blue": the person's colour, nudged only as far as needed to stay visible.
        return chosen is not null && Rgba.TryParse(chosen, out var c)
            ? ThemeResolver.DeriveAccent(c, t.Appearance).First(kv => kv.Key == "accent.mark").Value
            : t.Hex("accent.mark");
    }

    private static string DefaultTitle(ChartData d) => $"{d.Spec.Mark} chart of {d.Spec.Y.Field} by {d.Spec.X.Field}";

    private static string AltTextFor(ChartData data, List<object> xKeys)
    {
        var all = data.Series.SelectMany(s => s.Points.Select(p => (s.Name, p))).ToList();
        var max = all.MaxBy(x => x.p.Y);
        var min = all.MinBy(x => x.p.Y);
        var y = data.Spec.Y.Format;
        var kind = data.Spec.Mark.ToString().ToLowerInvariant();
        var name = data.Spec.Title ?? $"{data.YColumn.Name} by {data.XColumn.Name}";
        var sb = new StringBuilder($"{char.ToUpperInvariant(kind[0])}{kind[1..]} chart: {name}. {data.PointCount} points");
        if (data.Series.Length > 1)
        {
            sb.Append($" in {data.Series.Length} series ({string.Join(", ", data.Series.Select(s => s.Name))})");
        }

        sb.Append($". Highest: {Fmt.XLabel(max.p.X, data.XColumn, xKeys)} at {Fmt.Value(max.p.Y, y, false)}. Lowest: {Fmt.XLabel(min.p.X, data.XColumn, xKeys)} at {Fmt.Value(min.p.Y, y, false)}.");
        return sb.ToString();
    }

    private static string FontAttr(ResolvedTheme t) => string.Join(", ", t.FontUi.Split(',').Select(f => f.Trim()).Select(f => f.Contains(' ') ? $"'{f}'" : f));

    private static string Text(double x, double y, string text, string fill, double size, int weight, string anchor, bool numeric = false) =>
        $"<text x=\"{N(x)}\" y=\"{N(y)}\" fill=\"{fill}\" font-size=\"{size}\" font-weight=\"{weight}\" text-anchor=\"{anchor}\"{(numeric ? " style=\"font-variant-numeric:tabular-nums\"" : "")}>{Esc(text)}</text>";

    internal static double TextWidth(string s, double size) => s.Length * size * 0.56;

    private static string Truncate(string s, int max) => max < 3 || s.Length <= max ? (max < 3 ? "" : s) : s[..(max - 1)] + "…";

    private static string N(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Esc(string s) => WebUtility.HtmlEncode(s);

    /// <summary>A "nice" axis: round step sizes (1, 2, 2.5, 5 × 10ⁿ) with about <paramref name="count"/> ticks.</summary>
    internal static (double Min, double Max, List<double> Ticks) NiceScale(double lo, double hi, int count)
    {
        if (hi <= lo)
        {
            hi = lo + 1;
        }

        var raw = (hi - lo) / Math.Max(1, count);
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var norm = raw / mag;
        var step = (norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10) * mag;
        var min = Math.Floor(lo / step) * step;
        var max = Math.Ceiling(hi / step) * step;
        var ticks = new List<double>();
        for (var v = min; v <= max + (step / 2); v += step)
        {
            ticks.Add(Math.Round(v, 10));
        }

        return (min, max, ticks);
    }
}

/// <summary>Number and label formatting shared by the axes, labels and tooltips.</summary>
public static class Fmt
{
    public static string Value(double v, string? format, bool compact)
    {
        var (kind, arg) = (format ?? "").Split(':', 2) is [var k, var a] ? (k, a) : ((format ?? ""), "");
        var abs = Math.Abs(v);
        string body;
        if (kind == "percent")
        {
            body = $"{Trim(v * (abs <= 1 ? 100 : 1), 1)}%";
        }
        else
        {
            var scaled = compact && abs >= 10_000 ? Compact(v) : Group(v, kind == "currency");
            body = kind == "currency" ? $"{Symbol(arg)}{scaled.TrimStart('-')}" : scaled.TrimStart('-');
            if (v < 0)
            {
                body = "−" + body;
            }
        }

        return body;
    }

    private static string Symbol(string code) => code.ToUpperInvariant() switch { "EUR" => "€", "GBP" => "£", "JPY" => "¥", _ => "$" };

    private static string Compact(double v)
    {
        var abs = Math.Abs(v);
        var (div, suffix) = abs >= 1e9 ? (1e9, "B") : abs >= 1e6 ? (1e6, "M") : (1e3, "k");
        return $"{Trim(v / div, 1)}{suffix}";
    }

    private static string Group(double v, bool money = false)
    {
        var abs = Math.Abs(v);
        var decimals = abs >= 1000 || Math.Abs(v - Math.Round(v)) < 1e-9 || (money && abs >= 100) ? 0 : abs >= 10 && !money ? 1 : 2;
        return v.ToString("#,##0." + new string('#', decimals), CultureInfo.InvariantCulture).TrimEnd('.');
    }

    private static string Trim(double v, int decimals) => Math.Round(v, decimals).ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);

    /// <summary>Axis labels for a whole axis: a year is written on the first label and wherever it
    /// changes, not on every tick.</summary>
    public static List<string> XLabels(IReadOnlyList<object> keys, Column column)
    {
        var labels = new List<string>();
        int? lastYear = null;
        foreach (var key in keys)
        {
            if (key is DateOnly d && column.Unit is "month" or "week" or "day" or null)
            {
                var showYear = lastYear != d.Year;
                lastYear = d.Year;
                var ci = CultureInfo.InvariantCulture;
                var core = column.Unit == "month" ? d.ToString("MMM", ci) : d.ToString("MMM d", ci);
                labels.Add(showYear && keys.OfType<DateOnly>().Select(x => x.Year).Distinct().Count() > 1 ? $"{core} ’{d.Year % 100:00}" : core);
            }
            else
            {
                labels.Add(XLabel(key, column, keys));
            }
        }

        return labels;
    }

    public static string XLabel(object key, Column column, IReadOnlyList<object> all)
    {
        if (key is not DateOnly d)
        {
            return key is double n ? Trim(n, 2) : key.ToString() ?? "";
        }

        var multiYear = all.OfType<DateOnly>().Select(x => x.Year).Distinct().Count() > 1;
        var ci = CultureInfo.InvariantCulture;
        return column.Unit switch
        {
            "year" => d.Year.ToString(ci),
            "quarter" => $"Q{((d.Month - 1) / 3) + 1}{(multiYear ? $" ’{d.Year % 100:00}" : "")}",
            "month" => multiYear ? d.ToString("MMM ’yy", ci) : d.ToString("MMM", ci),
            _ => multiYear ? d.ToString("MMM d, ’yy", ci) : d.ToString("MMM d", ci),
        };
    }
}
