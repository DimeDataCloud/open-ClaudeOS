using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClaudeOS.Shell.Controls;

/// <summary>Reads back the text a piece of UI is actually showing. Used by the self-test to prove a
/// window rendered its content, not just that it opened.</summary>
internal static class TreeText
{
    public static string Of(DependencyObject? root, int max = 12)
    {
        var found = new List<string>();
        Walk(root, found, max);
        return string.Join(" | ", found);
    }

    private static void Walk(DependencyObject? node, List<string> found, int max)
    {
        if (node is null || found.Count >= max)
        {
            return;
        }

        if (node is TextBlock { Text.Length: > 0 } text && (node as UIElement)?.Visibility != Visibility.Collapsed)
        {
            found.Add(text.Text.Replace('\n', ' '));
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), found, max);
        }
    }
}
