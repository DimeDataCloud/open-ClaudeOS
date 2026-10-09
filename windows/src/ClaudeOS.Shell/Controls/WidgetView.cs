using ClaudeOS.Core.Mods;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClaudeOS.Shell.Controls;

/// <summary>Draws a bound declarative view with native controls, so a mod's widget looks, scales and
/// themes like the rest of the system. There is no web view and no script behind it.</summary>
internal static class WidgetView
{
    public static UIElement Build(RenderedNode node)
    {
        switch (node.Kind)
        {
            case ViewKind.Stack:
            case ViewKind.Row:
                {
                    var panel = new StackPanel { Spacing = 4, Orientation = node.Kind == ViewKind.Row ? Orientation.Horizontal : Orientation.Vertical };
                    foreach (var child in node.Children)
                    {
                        panel.Children.Add(Build(child));
                    }

                    return panel;
                }

            case ViewKind.Metric:
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom };
                    row.Children.Add(new TextBlock { Text = node.Primary, Style = (Style)Application.Current.Resources["CosMetricText"] });
                    if (node.Secondary.Length > 0)
                    {
                        row.Children.Add(new TextBlock { Text = node.Secondary, Style = (Style)Application.Current.Resources["CosCaptionText"], VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 5) });
                    }

                    return row;
                }

            case ViewKind.Text:
                {
                    var panel = new StackPanel { Spacing = 0 };
                    panel.Children.Add(new TextBlock { Text = node.Primary, Style = (Style)Application.Current.Resources["CosBodyText"], TextTrimming = TextTrimming.CharacterEllipsis });
                    if (node.Secondary.Length > 0)
                    {
                        panel.Children.Add(new TextBlock { Text = node.Secondary, Style = (Style)Application.Current.Resources["CosCaptionText"], TextTrimming = TextTrimming.CharacterEllipsis });
                    }

                    return panel;
                }

            case ViewKind.Gauge:
                return new ProgressBar { Minimum = 0, Maximum = node.Max ?? 100, Value = node.Number ?? 0, MinWidth = 120 };

            case ViewKind.Button:
                return new Button { Content = node.Primary, Style = (Style)Application.Current.Resources["CosQuietButton"] };

            default:
                return new Border { Height = 8 };
        }
    }
}
