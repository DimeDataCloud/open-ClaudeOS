using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClaudeOS.Shell.Controls;

/// <summary>
/// A numeric stand-in for "does it look right": walks a window's visual tree and reports anything
/// that spills outside the window, and how much content has to scroll. A screenshot shows a person
/// whether a layout is good; this tells CI when one has broken.
/// </summary>
internal static class LayoutAudit
{
    public sealed record Result(int Elements, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes)
    {
        public string Summary => $"{Elements} elements, {Problems.Count} spill(s)" + (Notes.Count > 0 ? "; " + string.Join("; ", Notes) : "");
    }

    public static Result Check(FrameworkElement? root)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        var count = 0;
        if (root is not null)
        {
            Walk(root, root, inScroll: false, problems, notes, ref count);
        }

        return new Result(count, problems, notes);
    }

    private static void Walk(DependencyObject node, FrameworkElement root, bool inScroll, List<string> problems, List<string> notes, ref int count)
    {
        if (node is FrameworkElement fe)
        {
            if (fe.Visibility != Visibility.Visible)
            {
                return;
            }

            count++;
            if (!inScroll && fe != root && fe.ActualWidth > 0 && fe.ActualHeight > 0)
            {
                try
                {
                    var bounds = fe.TransformToVisual(root).TransformBounds(new Windows.Foundation.Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
                    if (bounds.Right > root.ActualWidth + 1.5 || bounds.Bottom > root.ActualHeight + 1.5 || bounds.X < -1.5 || bounds.Y < -1.5)
                    {
                        problems.Add($"{Describe(fe)} spills outside the window ({bounds.X:0},{bounds.Y:0} {bounds.Width:0}x{bounds.Height:0} in {root.ActualWidth:0}x{root.ActualHeight:0})");
                    }
                }
                catch (ArgumentException)
                {
                    // Not in the tree (yet): nothing to measure.
                }
            }

            if (fe is ScrollViewer { ScrollableHeight: > 1 } scroller)
            {
                notes.Add($"scrolls {scroller.ScrollableHeight:0}px");
            }

            if (fe is TextBlock { IsTextTrimmed: true } text)
            {
                notes.Add($"trims \"{(text.Text.Length > 24 ? text.Text[..24] + "…" : text.Text)}\"");
            }

            inScroll |= fe is ScrollViewer;
        }

        var children = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < children; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), root, inScroll, problems, notes, ref count);
        }
    }

    private static string Describe(FrameworkElement fe) =>
        fe.Name.Length > 0 ? $"{fe.GetType().Name} '{fe.Name}'" : fe.GetType().Name + (fe is TextBlock t ? $" \"{t.Text}\"" : "");
}
