using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// The one place that turns "show this" into a position on screen: it reads the desktop, asks the
/// layout engine for room, moves whatever the engine says must move, and remembers it all so
/// "put it back" restores the screen exactly. Opening a file and showing a chart go through here.
/// </summary>
internal sealed class Placer(StateDir state)
{
    public LayoutHistory History { get; } = new();

    /// <summary>Find room for a window of the given size in device pixels.</summary>
    public Placement Place(Size physical, Anchor anchor = Anchor.Auto, long? ignoreWindow = null, string kind = "content")
    {
        var desktop = WindowCatalog.Capture();
        var placement = LayoutEngine.Place(desktop, new PlacementRequest(physical, Anchor: anchor, IgnoreWindow: ignoreWindow));
        foreach (var move in placement.Moves)
        {
            WindowCatalog.Move(move.Handle, move.To);
        }

        if (placement.Moves.Count > 0)
        {
            History.Record(placement);
        }

        state.Log("placed", Audit.Of(("kind", Audit.Str(kind)), ("method", Audit.Str(placement.Method.ToString()))));
        return placement;
    }

    /// <summary>Size in device pixels on the monitor the person is working on.</summary>
    public static Size Physical(int widthDips, int heightDips) =>
        Scaling.ToPhysical(widthDips, heightDips, WindowCatalog.Capture().FocusMonitor.Scale);
}
