using ClaudeOS.Core.Design;

namespace ClaudeOS.Core.Tests.Design;

/// <summary>
/// The categorical chart palette has to stay tellable-apart for people with colour-vision
/// deficiency, because a chart with several series is useless if two of them merge. Neighbouring
/// series are checked in normal vision and under simulated protanopia, deuteranopia and tritanopia
/// (Machado et al. 2009, full severity), using distance in OKLab scaled by 100. It mirrors the
/// checks in the data-visualization palette validator, so the shipped palette cannot drift out of them.
/// </summary>
public sealed class PaletteTests
{
    private static readonly double[][] Protan = [[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]];
    private static readonly double[][] Deutan = [[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]];
    private static readonly double[][] Tritan = [[1.255528, -0.076749, -0.178779], [-0.078411, 0.930809, 0.147602], [0.004733, 0.691367, 0.303900]];

    [Theory]
    [InlineData(Appearance.Light)]
    [InlineData(Appearance.Dark)]
    public void Neighbouring_series_stay_apart_in_normal_vision(Appearance appearance)
    {
        var series = ThemeResolver.Resolve(appearance).Series;
        for (var i = 0; i + 1 < series.Length; i++)
        {
            var d = Distance(series[i], series[i + 1], null);
            Assert.True(d >= 15, $"{series[i]} and {series[i + 1]} are only {d:0.0} apart");
        }
    }

    [Theory]
    [InlineData(Appearance.Light, "protan")]
    [InlineData(Appearance.Light, "deutan")]
    [InlineData(Appearance.Light, "tritan")]
    [InlineData(Appearance.Dark, "protan")]
    [InlineData(Appearance.Dark, "deutan")]
    [InlineData(Appearance.Dark, "tritan")]
    public void Neighbouring_series_stay_apart_under_colour_vision_deficiency(Appearance appearance, string kind)
    {
        var matrix = kind switch { "protan" => Protan, "deutan" => Deutan, _ => Tritan };
        var series = ThemeResolver.Resolve(appearance).Series;
        for (var i = 0; i + 1 < series.Length; i++)
        {
            // 6 is the floor for pairs that also have a second cue, and charts always add a legend and
            // direct labels. Tritanopia affects about one person in ten thousand and no palette of
            // eight hues separates fully under it, so it only has to stay clearly above merging.
            var floor = kind == "tritan" ? 3.5 : 6;
            var d = Distance(series[i], series[i + 1], matrix);
            Assert.True(d >= floor, $"{kind}: {series[i]} and {series[i + 1]} are only {d:0.0} apart");
        }
    }

    private static double Distance(string a, string b, double[][]? cvd)
    {
        var (l1, a1, b1) = Oklab(Rgba.Parse(a), cvd);
        var (l2, a2, b2) = Oklab(Rgba.Parse(b), cvd);
        return 100 * Math.Sqrt(((l1 - l2) * (l1 - l2)) + ((a1 - a2) * (a1 - a2)) + ((b1 - b2) * (b1 - b2)));
    }

    private static (double L, double A, double B) Oklab(Rgba c, double[][]? cvd)
    {
        double[] rgb = [Linear(c.R), Linear(c.G), Linear(c.B)];
        if (cvd is not null)
        {
            rgb = [.. cvd.Select(row => Math.Clamp((row[0] * rgb[0]) + (row[1] * rgb[1]) + (row[2] * rgb[2]), 0, 1))];
        }

        var l = Math.Cbrt((0.4122214708 * rgb[0]) + (0.5363325363 * rgb[1]) + (0.0514459929 * rgb[2]));
        var m = Math.Cbrt((0.2119034982 * rgb[0]) + (0.6806995451 * rgb[1]) + (0.1073969566 * rgb[2]));
        var s = Math.Cbrt((0.0883024619 * rgb[0]) + (0.2817188376 * rgb[1]) + (0.6299787005 * rgb[2]));
        return (
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
    }

    private static double Linear(byte v)
    {
        var x = v / 255.0;
        return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
    }
}
