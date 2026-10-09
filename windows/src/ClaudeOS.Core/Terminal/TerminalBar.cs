using System.Globalization;
using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Presence;
using ClaudeOS.Core.Search;

namespace ClaudeOS.Core.Terminal;

public enum OutcomeKind { Done, Note, Failed }

/// <summary>What a delegated piece of work (asking Claude, undoing) came to, in words for the bar.
/// A note is neither a success nor a failure: it just explains ("there is nothing to undo").</summary>
public sealed record AssistOutcome(OutcomeKind Kind, string Message)
{
    public static AssistOutcome Done(string message) => new(OutcomeKind.Done, message);

    public static AssistOutcome Note(string message) => new(OutcomeKind.Note, message);

    public static AssistOutcome Failed(string message) => new(OutcomeKind.Failed, message);
}

/// <summary>
/// The Intent Bar for a terminal: the same router, the same presence, the same local file search,
/// with a line of text instead of a window. It exists so the product can be tried on any OS today
/// (the desktop shells are a Windows app first), and it keeps the same promises: opening and
/// finding never touch a model, anything that changes files goes through the approval card the
/// delegate shows, and the Spark is a single glyph that says what the system is doing.
/// All input and output are injected, so it is tested without a terminal.
/// </summary>
public sealed class TerminalBar(TextReader input, TextWriter output, IReadOnlyList<string> roots, IntentRouter router, PresenceMachine? presence = null)
{
    private readonly PresenceMachine _presence = presence ?? new PresenceMachine();
    private List<string> _results = [];

    /// <summary>Hands a request that needs Claude to the host (which shows its own approval card).</summary>
    public Func<UserIntent, string, CancellationToken, Task<AssistOutcome>>? Assist { get; init; }

    public Func<AssistOutcome>? Undo { get; init; }

    /// <summary>Opens a file the person picked from the results. Without it, the path is just shown.</summary>
    public Action<string>? Open { get; init; }

    /// <summary>Use plain ASCII for the Spark, for terminals that cannot draw the glyphs.</summary>
    public bool Ascii { get; init; }

    public PresenceFrame Frame => _presence.Frame;

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        _presence.Handle(new BarShown());
        output.WriteLine("open-ClaudeOS  ·  say what you want  ·  help  ·  exit");
        while (!ct.IsCancellationRequested)
        {
            _presence.Handle(new BarShown()); // back to waiting, unless a decision is still pending
            output.Write($"{Glyph(_presence.Frame.State)} › ");
            var line = await input.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                output.WriteLine();
                break;
            }

            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (!await HandleAsync(line.Trim(), ct).ConfigureAwait(false))
            {
                break;
            }
        }

        _presence.Handle(new BarHidden());
        return 0;
    }

    /// <summary>One line of input. Returns false when the person asked to leave.</summary>
    public async Task<bool> HandleAsync(string text, CancellationToken ct = default)
    {
        var lower = text.ToLowerInvariant();
        if (lower is "exit" or "quit" or ":q" or ":quit")
        {
            return false;
        }

        if (lower is "help" or "?" or ":help")
        {
            Help();
            return true;
        }

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            OpenResult(number);
            return true;
        }

        _presence.Handle(new Typing(text.Length));
        var routing = await router.RouteAsync(text, ct).ConfigureAwait(false);
        _presence.Handle(new Routed(routing, What(routing.Intent)));
        // Only say "no model" when no model will be used: understanding a make request locally still
        // leaves the making to Claude.
        output.WriteLine(routing.Intent is OpenIntent or FindIntent or WindowIntent or UndoIntent or SuggestionReplyIntent
            ? $"  on this device, {routing.Elapsed.TotalMilliseconds:0.0} ms, no model"
            : "  asking Claude");

        try
        {
            switch (routing.Intent)
            {
                case OpenIntent open:
                    Results(open.Query);
                    break;
                case FindIntent find:
                    Results(find.Query);
                    break;
                case WindowIntent:
                    Note("There are no windows to move in a terminal");
                    break;
                case SuggestionReplyIntent:
                    Note("There is nothing to confirm right now");
                    break;
                case UndoIntent:
                    Finish(Undo?.Invoke() ?? AssistOutcome.Note("Undo is not available here"));
                    break;
                default:
                    Finish(Assist is null
                        ? AssistOutcome.Failed("Claude is not connected; opening and finding still work")
                        : await Assist(routing.Intent, text, ct).ConfigureAwait(false));
                    break;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Finish(AssistOutcome.Failed(e.Message));
        }

        return true;
    }

    private void Results(string query)
    {
        var hits = roots.Count == 0 ? [] : FileFinder.Rank(query, FileFinder.Scan(roots), DateTimeOffset.UtcNow, 5);
        _results = [.. hits.Select(h => h.File.Path)];
        if (hits.Count == 0)
        {
            Note($"Nothing matches “{query}”");
            return;
        }

        for (var i = 0; i < hits.Count; i++)
        {
            output.WriteLine($"  {i + 1}  {hits[i].File.Path}  ({hits[i].Reason})");
        }

        Done(hits.Count == 1 ? "Found it. Type 1 to open it" : $"Found {hits.Count}. Type a number to open one");
    }

    private void OpenResult(int number)
    {
        if (number < 1 || number > _results.Count)
        {
            Note(_results.Count == 0 ? "Nothing to open yet. Ask for a file first" : $"Pick a number from 1 to {_results.Count}");
            return;
        }

        var path = _results[number - 1];
        if (Open is null)
        {
            Done(path);
            return;
        }

        try
        {
            Open(path);
            Done($"Opened {Path.GetFileName(path)}");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            var why = e is System.ComponentModel.Win32Exception ? "there is no default app to open it with" : e.Message;
            Finish(AssistOutcome.Failed($"Could not open {Path.GetFileName(path)}: {why}"));
        }
    }

    private void Finish(AssistOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case OutcomeKind.Done:
                Done(outcome.Message);
                break;
            case OutcomeKind.Note:
                Note(outcome.Message);
                break;
            default:
                Show(new Failed(outcome.Message));
                break;
        }
    }

    private void Done(string message) => Show(new Finished(message));

    /// <summary>An explanation that is neither a success nor a failure: no state change, just a quiet line.</summary>
    private void Note(string message) => output.WriteLine($"  {message}");

    private void Show(PresenceEvent e)
    {
        _presence.Handle(e);
        var frame = _presence.Frame;
        output.WriteLine($"{Glyph(frame.State)} {frame.Label}");
        if (frame.Hint is { Length: > 0 })
        {
            output.WriteLine($"  {frame.Hint}");
        }
    }

    private void Help()
    {
        output.WriteLine("""
              open the q3 budget               find invoices from september
              chart spend by month from q3.csv    make a clock widget
              summarise the invoices and email finance
              undo                              exit

            Opening and finding run on this device with no model. A number opens a result.
            Anything that changes your files shows what will happen first and waits for you.
            """);
    }

    private static string What(UserIntent intent) => intent switch
    {
        OpenIntent o => $"Looking for {o.Query}",
        FindIntent f => $"Looking for {f.Query}",
        UndoIntent => "Undoing",
        WindowIntent => "On it",
        _ => "Thinking",
    };

    private string Glyph(PresenceState state) => Ascii
        ? state switch
        {
            PresenceState.Dormant => ".", PresenceState.Idle => "o", PresenceState.Listening => ":", PresenceState.Understanding => "*",
            PresenceState.Working => "~", PresenceState.NeedsYou => "!", PresenceState.Done => "v", _ => "x",
        }
        : state switch
        {
            PresenceState.Dormant => "·", PresenceState.Idle => "○", PresenceState.Listening => "◌", PresenceState.Understanding => "◍",
            PresenceState.Working => "◐", PresenceState.NeedsYou => "◉", PresenceState.Done => "✓", _ => "⊘",
        };
}
