using System.Globalization;
using System.Xml.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace ClaudeOS.Shell.Controls;

/// <summary>
/// Draws the SVG the chart renderer emits with native XAML shapes and text. The system's own SVG
/// image control was tried first and drops exactly what a chart needs (text, and colours with an
/// alpha channel), so charts came out as bare bars. The renderer only uses a small, fixed subset
/// (rect, line, circle, path with M, L, Q and Z, and text), which is all this reads. Native text
/// is also sharper, follows the system font and is available to screen readers.
/// </summary>
internal static class SvgScene
{
    public static Canvas Build(string svg)
    {
        var root = XDocument.Parse(svg).Root ?? throw new FormatException("the picture has no content");
        var family = FontFamilyFrom(root.Attribute("font-family")?.Value);
        var canvas = new Canvas
        {
            Width = Number(root, "width", 400),
            Height = Number(root, "height", 300),
        };

        foreach (var node in root.Elements())
        {
            UIElement? shape = node.Name.LocalName switch
            {
                "rect" => Rect(node),
                "line" => Line(node),
                "circle" => Circle(node),
                "path" => PathShape(node),
                "text" => Text(node, family),
                _ => null, // title and desc describe the picture; they are not drawn
            };

            if (shape is not null)
            {
                canvas.Children.Add(shape);
            }
        }

        return canvas;
    }

    private static UIElement Rect(XElement e)
    {
        var rect = new Rectangle
        {
            Width = Number(e, "width"),
            Height = Number(e, "height"),
            Fill = Brush(e.Attribute("fill")?.Value),
            RadiusX = Number(e, "rx"),
            RadiusY = Number(e, "ry", Number(e, "rx")),
        };
        Canvas.SetLeft(rect, Number(e, "x"));
        Canvas.SetTop(rect, Number(e, "y"));
        return rect;
    }

    private static UIElement Line(XElement e) => new Microsoft.UI.Xaml.Shapes.Line
    {
        X1 = Number(e, "x1"),
        X2 = Number(e, "x2"),
        Y1 = Number(e, "y1"),
        Y2 = Number(e, "y2"),
        Stroke = Brush(e.Attribute("stroke")?.Value),
        StrokeThickness = Number(e, "stroke-width", 1),
    };

    private static UIElement Circle(XElement e)
    {
        var r = Number(e, "r");
        var ellipse = new Ellipse
        {
            Width = 2 * r,
            Height = 2 * r,
            Fill = Brush(e.Attribute("fill")?.Value),
            Stroke = Brush(e.Attribute("stroke")?.Value),
            StrokeThickness = Number(e, "stroke-width"),
        };
        Canvas.SetLeft(ellipse, Number(e, "cx") - r);
        Canvas.SetTop(ellipse, Number(e, "cy") - r);
        return ellipse;
    }

    private static UIElement? PathShape(XElement e)
    {
        var data = e.Attribute("d")?.Value;
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var fill = Brush(e.Attribute("fill")?.Value);
        if (fill is SolidColorBrush solid && e.Attribute("fill-opacity") is { } opacity)
        {
            solid.Opacity = double.Parse(opacity.Value, CultureInfo.InvariantCulture);
        }

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            // The path mini-language (M, L, Q, Z) is the same one XAML uses for geometry.
            Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data),
            Fill = fill,
            Stroke = Brush(e.Attribute("stroke")?.Value),
            StrokeThickness = Number(e, "stroke-width"),
            StrokeStartLineCap = Cap(e.Attribute("stroke-linecap")?.Value),
            StrokeEndLineCap = Cap(e.Attribute("stroke-linecap")?.Value),
            StrokeLineJoin = e.Attribute("stroke-linejoin")?.Value switch
            {
                "round" => PenLineJoin.Round,
                "bevel" => PenLineJoin.Bevel,
                _ => PenLineJoin.Miter,
            },
        };
    }

    private static UIElement Text(XElement e, FontFamily family)
    {
        var size = Number(e, "font-size", 12);
        var block = new TextBlock
        {
            Text = e.Value,
            FontSize = size,
            FontFamily = family,
            FontWeight = Number(e, "font-weight", 400) >= 600 ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Brush(e.Attribute("fill")?.Value) ?? new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
            IsHitTestVisible = false,
        };
        if (e.Attribute("style")?.Value.Contains("tabular-nums", StringComparison.Ordinal) == true)
        {
            Typography.SetNumeralAlignment(block, FontNumeralAlignment.Tabular);
        }

        // SVG gives the baseline and an anchor; XAML wants the top-left corner.
        block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = block.DesiredSize.Width;
        var x = Number(e, "x");
        var left = e.Attribute("text-anchor")?.Value switch
        {
            "middle" => x - (width / 2),
            "end" => x - width,
            _ => x,
        };
        var baseline = block.BaselineOffset > 0 ? block.BaselineOffset : size * 0.8;
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, Number(e, "y") - baseline);
        return block;
    }

    private static PenLineCap Cap(string? value) => value switch
    {
        "round" => PenLineCap.Round,
        "square" => PenLineCap.Square,
        _ => PenLineCap.Flat,
    };

    private static double Number(XElement e, string name, double fallback = 0) =>
        e.Attribute(name) is { } a && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>"'Segoe UI Variable Text', 'Segoe UI', Inter, system-ui" becomes a XAML font list.</summary>
    private static FontFamily FontFamilyFrom(string? list)
    {
        var names = (list ?? "Segoe UI").Split(',').Select(n => n.Trim().Trim('\'', '"')).Where(n => n.Length > 0 && n != "system-ui" && n != "sans-serif");
        return new FontFamily(string.Join(", ", names));
    }

    /// <summary>Hex colours in #RGB, #RRGGBB and #RRGGBBAA forms, and "none".</summary>
    private static SolidColorBrush? Brush(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "none" || value[0] != '#')
        {
            return null;
        }

        var hex = value[1..];
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits))
        {
            return null;
        }

        var alpha = hex.Length == 8 ? (byte)(bits & 0xFF) : (byte)255;
        var rgb = hex.Length == 8 ? bits >> 8 : bits;
        return new SolidColorBrush(Color.FromArgb(alpha, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF)));
    }
}
