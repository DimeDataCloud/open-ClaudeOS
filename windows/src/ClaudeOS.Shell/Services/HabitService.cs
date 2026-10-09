using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Presence;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Watches where you put charts and widgets, and after the third time you put one in the same
/// place offers a rule through the presence ("Open chart on the top right from now on?"). It
/// only offers: nothing changes until you say yes and approve the rule like any other mod.
/// What it has seen, and which offers it has already made, is kept in a small file in the app's
/// state folder, so a restart does not forget a habit and an offer is never made twice.
/// </summary>
internal sealed class HabitService(PresenceMachine presence, string? file = null)
{
    private HabitTracker _tracker = Restore(file);
    private bool _persist = true;

    /// <summary>The offer waiting for a yes or a no, if any.</summary>
    public Suggestion? Pending { get; private set; }

    private static HabitTracker Restore(string? file)
    {
        try
        {
            return HabitTracker.Load(file is not null && File.Exists(file) ? File.ReadAllText(file) : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new HabitTracker();
        }
    }

    /// <summary>For the self-test: start from nothing and never write over the person's saved habits.</summary>
    public void Isolate()
    {
        _tracker = new HabitTracker();
        _persist = false;
        Pending = null;
    }

    private void Save()
    {
        if (!_persist || file is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(file, _tracker.Save());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Forgetting a habit is harmless; failing to move a window is not.
        }
    }

    /// <summary>A window the person has moved came to rest here (device pixels).</summary>
    public void Observe(string contentKind, ClaudeOS.Core.Layout.Rect bounds)
    {
        var desktop = WindowCatalog.Capture();
        var monitor = desktop.Monitors.FirstOrDefault(m => m.WorkArea.Contains(new ClaudeOS.Core.Layout.Rect((int)bounds.Center.X, (int)bounds.Center.Y, 1, 1))) ?? desktop.FocusMonitor;
        var made = _tracker.Record(contentKind, bounds, monitor.WorkArea);
        Save();
        if (made is { } offer)
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
