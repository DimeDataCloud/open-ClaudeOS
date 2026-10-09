using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Planning;

namespace ClaudeOS.Core.Presence;

public enum PresenceState
{
    /// <summary>Nothing on screen. The tray dot is dim.</summary>
    Dormant,

    /// <summary>The Intent Bar is open and waiting. The presence breathes, slowly.</summary>
    Idle,

    /// <summary>You are typing or speaking. It tightens and brightens, and answers each keystroke.</summary>
    Listening,

    /// <summary>A moment of local routing. Under 100 ms, so it is a shimmer, not a state you read.</summary>
    Understanding,

    /// <summary>Claude is working. The label says what it is really doing.</summary>
    Working,

    /// <summary>It needs a decision. Stays visible even if the bar is dismissed.</summary>
    NeedsYou,

    /// <summary>Finished. Settles to a tick, then back to idle.</summary>
    Done,

    /// <summary>Refused by policy, declined by the model, or failed. A steady ring, no shaking.</summary>
    Refused,
}

/// <summary>What the shell draws. One value, read by every surface that shows the presence.</summary>
public sealed record PresenceFrame(
    PresenceState State,
    string Label,
    string? Hint = null,
    double Intensity = 0,
    double? Progress = null,
    bool Offline = false,
    string? Suggestion = null)
{
    public static readonly PresenceFrame Asleep = new(PresenceState.Dormant, "");
}

public abstract record PresenceEvent;

public sealed record BarShown : PresenceEvent;

public sealed record BarHidden : PresenceEvent;

public sealed record Typing(int Length) : PresenceEvent;

public sealed record Routed(Routing Routing, string? What = null) : PresenceEvent;

public sealed record Agent(AgentEvent Event) : PresenceEvent;

public sealed record PlanReady(int Actions, string Risk, bool External) : PresenceEvent;

public sealed record Approved : PresenceEvent;

public sealed record Declined : PresenceEvent;

public sealed record Finished(string Message, bool Undoable = false) : PresenceEvent;

public sealed record Failed(string Message) : PresenceEvent;

public sealed record Connectivity(bool Online) : PresenceEvent;

public sealed record Suggest(string Message) : PresenceEvent;

public sealed record Tick(DateTimeOffset Now) : PresenceEvent;

/// <summary>
/// The presence is a pure state machine: events in, one frame out. It never animates by itself and
/// never says anything it does not know. The label under it is built from what the core is
/// actually doing (reading this file, waiting for your approval), which is what makes it feel
/// alive rather than decorated. The shell draws the frame; tests drive it without a screen.
/// </summary>
public sealed class PresenceMachine(TimeProvider? clock = null, bool reducedMotion = false)
{
    private static readonly TimeSpan DoneLinger = TimeSpan.FromMilliseconds(2400);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private bool _barOpen;
    private bool _online = true;
    private DateTimeOffset _doneAt;
    private string? _suggestion;

    public PresenceFrame Frame { get; private set; } = PresenceFrame.Asleep;

    public bool ReducedMotion { get; } = reducedMotion;

    public event Action<PresenceFrame>? Changed;

    public PresenceFrame Handle(PresenceEvent e)
    {
        var next = e switch
        {
            BarShown => OnBarShown(),
            BarHidden => Frame.State is PresenceState.NeedsYou or PresenceState.Working ? Frame : PresenceFrame.Asleep,
            Typing t => t.Length > 0 ? Make(PresenceState.Listening, "Listening", intensity: 0.7) : (_barOpen ? Idle() : Frame),
            Routed r => OnRouted(r),
            Agent a => OnAgent(a.Event),
            PlanReady p => Make(PresenceState.NeedsYou, "Review what I'll do", $"{p.Actions} {(p.Actions == 1 ? "action" : "actions")}{(p.External ? " · something leaves this computer" : " · nothing leaves this computer")}", intensity: 1.0),
            Approved => Make(PresenceState.Working, "Applying", intensity: 0.8),
            Declined => _barOpen ? Idle("Okay. Nothing changed.") : PresenceFrame.Asleep,
            Finished f => Done(f),
            Failed f => Make(PresenceState.Refused, f.Message, "Nothing was changed", 0.5),
            Connectivity c => OnConnectivity(c.Online),
            Suggest s => OnSuggest(s.Message),
            Tick t => OnTick(t.Now),
            _ => Frame,
        };

        if (next != Frame)
        {
            Frame = next;
            Changed?.Invoke(Frame);
        }

        return Frame;
    }

    private PresenceFrame OnBarShown()
    {
        _barOpen = true;
        return Frame.State is PresenceState.NeedsYou or PresenceState.Working ? Frame : Idle();
    }

    private PresenceFrame Idle(string? label = null) =>
        Make(PresenceState.Idle, label ?? (_online ? "What do you want to do?" : "Offline. Opening and moving things still works."), intensity: 0.35, suggestion: _suggestion);

    private PresenceFrame OnRouted(Routed r)
    {
        // Local routes are over before you could read a state; show the shimmer with the result.
        if (r.Routing.Source is RouteSource.Grammar or RouteSource.Npu && r.Routing.Intent is OpenIntent or FindIntent or WindowIntent or UndoIntent)
        {
            return Make(PresenceState.Understanding, r.What ?? "On it", intensity: 0.9);
        }

        return _online
            ? Make(PresenceState.Working, "Thinking", progress: null, intensity: 0.8)
            : Make(PresenceState.Refused, "That needs a connection", "Opening files and moving windows still work offline", 0.4, offline: true);
    }

    private PresenceFrame OnAgent(AgentEvent e) => e.Kind switch
    {
        "request" => Make(PresenceState.Working, "Thinking", intensity: 0.8),
        "read_file" => Make(PresenceState.Working, $"Reading {FileName(e.Detail)}", intensity: 0.8),
        "list_dir" => Make(PresenceState.Working, $"Looking in {FileName(e.Detail)}", intensity: 0.8),
        "plan_rejected" => Make(PresenceState.Working, "Adjusting the plan", "Policy asked for a change", 0.8),
        "retry" => Make(PresenceState.Working, "Trying again", intensity: 0.8),
        _ => Frame,
    };

    private PresenceFrame Done(Finished f)
    {
        _doneAt = _clock.GetUtcNow();
        return Make(PresenceState.Done, f.Message, f.Undoable ? "Say \"undo\" to take it back" : null, 0.6);
    }

    private PresenceFrame OnTick(DateTimeOffset now)
    {
        if (Frame.State == PresenceState.Done && now - _doneAt >= DoneLinger)
        {
            return _barOpen ? Idle() : PresenceFrame.Asleep;
        }

        return Frame;
    }

    private PresenceFrame OnConnectivity(bool online)
    {
        _online = online;
        return Frame with { Offline = !online, Label = Frame.State == PresenceState.Idle ? Idle().Label : Frame.Label };
    }

    private PresenceFrame OnSuggest(string message)
    {
        _suggestion = message;
        return Frame.State is PresenceState.Idle or PresenceState.Dormant && _barOpen ? Idle() : Frame with { Suggestion = message };
    }

    private PresenceFrame Make(PresenceState state, string label, string? hint = null, double intensity = 0.5, double? progress = null, bool offline = false, string? suggestion = null) =>
        new(state, label, hint, intensity, progress, offline || !_online, suggestion);

    /// <summary>Only the file's name is shown: the label may end up in a screenshot or a screen share.</summary>
    private static string FileName(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        var i = trimmed.LastIndexOfAny(['/', '\\']);
        var name = i < 0 ? trimmed : trimmed[(i + 1)..];
        return name is "" or "." ? "your workspace" : name;
    }
}
