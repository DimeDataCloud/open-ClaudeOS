using System.Globalization;

namespace ClaudeOS.Core.Design;

/// <summary>An sRGB colour with alpha. Parses and prints the hex forms the tokens use.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static Rgba Parse(string hex)
    {
        if (!TryParse(hex, out var c))
        {
            throw new FormatException($"'{hex}' is not a colour; expected #RGB, #RRGGBB or #RRGGBBAA");
        }

        return c;
    }

    public static bool TryParse(string? hex, out Rgba color)
    {
        color = default;
        if (string.IsNullOrEmpty(hex) || hex[0] != '#')
        {
            return false;
        }

        var h = hex.AsSpan(1);
        if (h.Length is 3)
        {
            Span<char> expanded = [h[0], h[0], h[1], h[1], h[2], h[2]];
            return TryParse("#" + new string(expanded), out color);
        }

        if (h.Length is not (6 or 8) || !byte.TryParse(h[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(h.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(h.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        byte a = 255;
        if (h.Length == 8 && !byte.TryParse(h.Slice(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a))
        {
            return false;
        }

        color = new Rgba(r, g, b, a);
        return true;
    }

    /// <summary>#RRGGBB, or #RRGGBBAA when not opaque.</summary>
    public string ToHex() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    /// <summary>This colour drawn over an opaque background.</summary>
    public Rgba Over(Rgba background)
    {
        var a = A / 255.0;
        byte Mix(byte fg, byte bg) => (byte)Math.Round((fg * a) + (bg * (1 - a)));
        return new Rgba(Mix(R, background.R), Mix(G, background.G), Mix(B, background.B));
    }

    public double Luminance()
    {
        static double Lin(byte c)
        {
            var s = c / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Lin(R)) + (0.7152 * Lin(G)) + (0.0722 * Lin(B));
    }

    /// <summary>WCAG contrast ratio of this colour on a background (alpha is composited first).</summary>
    public double ContrastOn(Rgba background)
    {
        var fg = Over(background);
        var (hi, lo) = (fg.Luminance(), background.Luminance());
        if (hi < lo)
        {
            (hi, lo) = (lo, hi);
        }

        return (hi + 0.05) / (lo + 0.05);
    }

    // ---- OKLCH, used to derive an accessible accent from any hue the person picks.

    public (double L, double C, double H) ToOklch()
    {
        static double Lin(byte c)
        {
            var s = c / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        double r = Lin(R), g = Lin(G), b = Lin(B);
        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s2 = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        var L = (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s2);
        var a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s2);
        var bb = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s2);
        var h = Math.Atan2(bb, a) * 180 / Math.PI;
        return (L, Math.Sqrt((a * a) + (bb * bb)), h < 0 ? h + 360 : h);
    }

    public static Rgba FromOklch(double L, double C, double H, byte alpha = 255)
    {
        var hr = H * Math.PI / 180;
        double a = C * Math.Cos(hr), b = C * Math.Sin(hr);
        var l = Math.Pow(L + (0.3963377774 * a) + (0.2158037573 * b), 3);
        var m = Math.Pow(L - (0.1055613458 * a) - (0.0638541728 * b), 3);
        var s = Math.Pow(L - (0.0894841775 * a) - (1.2914855480 * b), 3);
        var r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var bl = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        static byte Enc(double v)
        {
            v = Math.Clamp(v, 0, 1);
            var e = v <= 0.0031308 ? 12.92 * v : (1.055 * Math.Pow(v, 1 / 2.4)) - 0.055;
            return (byte)Math.Round(e * 255);
        }

        return new Rgba(Enc(r), Enc(g), Enc(bl), alpha);
    }
}
