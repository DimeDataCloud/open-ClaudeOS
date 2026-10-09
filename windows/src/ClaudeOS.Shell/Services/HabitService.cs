using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Presence;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Watches where you put charts and widgets, and after the third time you put one in the same
/// place offers a rule through the presence ("Open chart on the top right from now on?"). It
/// only offers: nothing changes until you say yes and approve the rule like any other mod.
/// Counts live in memory, so a restart starts the habit afresh.
/// </summary>
internal sealed class HabitService(PresenceMachine presence)
{
    private readonly HabitTracker _tracker = new();

    /// <summary>The offer waiting for a yes or a no, if any.</summary>
    public Suggestion? Pending { get; private set; }

    /// <summary>A window the person has moved came to rest here (device pixels).</summary>
    public void Observe(string contentKind, ClaudeOS.Core.Layout.Rect bounds)
    {
        var desktop = WindowCatalog.Capture();
        var monitor = desktop.Monitors.FirstOrDefault(m => m.WorkArea.Contains(new ClaudeOS.Core.Layout.Rect((int)bounds.Center.X, (int)bounds.Center.Y, 1, 1))) ?? desktop.FocusMonitor;
        if (_tracker.Record(contentKind, bounds, monitor.WorkArea) is { } offer)
        {
            Pending = offer;
            presence.Handle(new Suggest(offer.Message));
        }
    }

    /// <summary>The offer was answered either way; take it off the bar.</summary>
    public Suggestion? Take()
    {
        var offer = Pending;
        Pending = null;
        presence.Handle(new Suggest(""));
        return offer;
    }
}
