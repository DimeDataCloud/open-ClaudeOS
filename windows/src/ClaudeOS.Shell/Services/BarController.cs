using System.Diagnostics;
using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Presence;
using ClaudeOS.Core.Safety;
using ClaudeOS.Shell.Interop;

namespace ClaudeOS.Shell.Services;

internal enum ResultKind { File, Window, Command, Hint }

/// <summary>One row in the bar's list.</summary>
internal sealed record BarResult(ResultKind Kind, string Title, string Subtitle, string Glyph, string? Target = null);

/// <summary>
/// The logic behind the Intent Bar, with no UI in it: what to suggest while you type, and what
/// Enter does. Opening, finding, moving windows and undo are decided and done here, locally and
/// instantly, with no model. Anything that needs Claude is handed to the agent service.
/// </summary>
internal sealed class BarController(PresenceMachine presence, FileIndex files, StateDir state, Placer placer, HabitService habits)
{
    private readonly IntentRouter _router = new(new LocalIntentClassifier());
    private LayoutHistory History => placer.History;
    private nint _previousForeground;

    public PresenceMachine Presence => presence;

    /// <summary>Claude. Set once the UI exists; everything that reaches it has already missed the local routes.</summary>
    public AgentService? Agent { get; set; }

    /// <summary>The window that had focus before the bar appeared: what "snap left" and "close this" mean.</summary>
    public void RememberForeground(nint hwnd) => _previousForeground = hwnd;

    public IReadOnlyList<BarResult> Suggest(string text)
    {
        var results = new List<BarResult>();
        var parsed = IntentGrammar.Parse(text);
        var query = parsed.Intent switch
        {
            OpenIntent o => o.Query,
            FindIntent f => f.Query,
            _ => parsed.Matched ? "" : text.Trim(),
        };

        if (parsed.Intent is WindowIntent w)
        {
            results.Add(new BarResult(ResultKind.Command, WindowCommandTitle(w.Command), "Moves the window; \"put it back\" restores it", ""));
        }
        else if (parsed.Intent is SuggestionReplyIntent reply && habits.Pending is { } offered)
        {
            results.Add(new BarResult(ResultKind.Command, reply.Accepted ? "Yes, do that" : "No thanks", offered.Message, "", null));
        }
        else if (parsed.Intent is UndoIntent)
        {
            results.Add(new BarResult(ResultKind.Command, "Undo the last change", "Restores the files exactly as they were", ""));
        }

        if (query.Length >= 2)
        {
            foreach (var match in files.Search(query, 5))
            {
                var name = Path.GetFileName(match.File.Path);
                results.Add(new BarResult(ResultKind.File, name, FriendlyFolder(match.File.Path), "", match.File.Path));
            }
        }

        if (results.Count == 0 && text.Trim().Length > 2)
        {
            results.Add(new BarResult(ResultKind.Hint, "Ask Claude", "Press Enter. It will show you exactly what it plans to do first.", ""));
        }

        return results;
    }

    /// <summary>What Enter does. Returns a short line for the bar to show, or null to keep it open.</summary>
    public async Task<string?> SubmitAsync(string text, BarResult? selected)
    {
        var routing = await _router.RouteAsync(text);
        switch (routing.Intent)
        {
            case OpenIntent or FindIntent when selected is { Kind: ResultKind.File, Target: { } path }:
                presence.Handle(new Routed(routing, $"Opening {Path.GetFileName(path)}"));
                var launched = await OpenFileAsync(path);
                presence.Handle(new Finished($"Opened {Path.GetFileName(path)}"));
                return launched;

            case WindowIntent w:
                presence.Handle(new Routed(routing, WindowCommandTitle(w.Command)));
                var message = RunWindowCommand(w.Command);
                presence.Handle(new Finished(message));
                return message;

            case UndoIntent:
                return Undo();

            case SuggestionReplyIntent reply:
                presence.Handle(new Routed(routing, reply.Accepted ? "Setting that up" : "Okay"));
                if (habits.Take() is not { } offer)
                {
                    presence.Handle(new Failed("There is nothing to confirm right now"));
                    return "There is nothing to confirm right now";
                }

                if (!reply.Accepted)
                {
                    presence.Handle(new Finished("Okay. I won't suggest that again"));
                    return "Okay";
                }

                return Agent is null ? "Not available yet" : await Agent.InstallRuleAsync(offer);

            default:
                presence.Handle(new Routed(routing));
                if (Agent is null)
                {
                    return null;
                }

                return await Agent.RunAsync(routing.Intent, text);
        }
    }

    private async Task<string?> OpenFileAsync(string path)
    {
        var before = WindowCatalog.Capture();
        var known = before.Windows.Select(x => x.Handle).ToHashSet();
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            presence.Handle(new Failed($"Could not open {Path.GetFileName(path)}"));
            return e.Message;
        }

        // Place the new window where the layout engine says there is room, once it appears.
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 2500)
        {
            await Task.Delay(60);
            var now = WindowCatalog.Capture();
            var fresh = now.Windows.FirstOrDefault(x => !known.Contains(x.Handle) && !x.IsMinimized);
            if (fresh is null)
            {
                continue;
            }

            var request = new PlacementRequest(new Size(Math.Min(fresh.Bounds.Width, 1280), Math.Min(fresh.Bounds.Height, 860)), IgnoreWindow: fresh.Handle);
            var placement = LayoutEngine.Place(now, request);
            if (placement.Method is PlacementMethod.FreeSpace or PlacementMethod.FreeSpaceShrunk or PlacementMethod.MadeRoom)
            {
                foreach (var move in placement.Moves)
                {
                    WindowCatalog.Move(move.Handle, move.To);
                }

                WindowCatalog.Move(fresh.Handle, placement.Bounds);
                History.Record(placement with { Moves = [.. placement.Moves, new WindowMove(fresh.Handle, fresh.Bounds, placement.Bounds)] });
                state.Log("placed", Audit.Of(("title", Audit.Str(fresh.Title)), ("method", Audit.Str(placement.Method.ToString()))));
            }

            break;
        }

        return null;
    }

    private string RunWindowCommand(WindowCommand command)
    {
        var desktop = WindowCatalog.Capture();
        if (command == WindowCommand.PutBack)
        {
            var restore = History.PutBack(desktop.Windows);
            if (restore is null)
            {
                return "Nothing to put back";
            }

            foreach (var move in restore.Moves)
            {
                WindowCatalog.Move(move.Handle, move.To);
            }

            return restore.Skipped.Count > 0 ? "Put back (left alone the window you moved yourself)" : "Put everything back";
        }

        var target = _previousForeground;
        if (target == 0 || !desktop.Windows.Any(w => w.Handle == target))
        {
            return "There is no window to move";
        }

        var window = desktop.Windows.First(w => w.Handle == target);
        var monitor = desktop.Monitors.FirstOrDefault(m => m.Id == window.MonitorId) ?? desktop.FocusMonitor;
        var gap = Scaling.ToPhysical(8, monitor.Scale);
        var area = monitor.WorkArea.Deflate(gap);
        var half = (area.Width - gap) / 2;
        Rect? destination = command switch
        {
            WindowCommand.SnapLeft => new Rect(area.X, area.Y, half, area.Height),
            WindowCommand.SnapRight => new Rect(area.X + half + gap, area.Y, area.Width - half - gap, area.Height),
            WindowCommand.CenterOnScreen => new Rect(area.X + ((area.Width - window.Bounds.Width) / 2), area.Y + ((area.Height - window.Bounds.Height) / 2), window.Bounds.Width, window.Bounds.Height),
            _ => null,
        };

        if (destination is { } to)
        {
            WindowCatalog.Move(target, to);
            History.Record(new Placement(to, PlacementMethod.MadeRoom, monitor.Id, [new WindowMove(target, window.Bounds, to)], command.ToString()));
            return "Done";
        }

        switch (command)
        {
            case WindowCommand.Maximize: Native.ShowWindow(target, 3); return "Maximized";
            case WindowCommand.Minimize: Native.ShowWindow(target, 6); return "Minimized";
            case WindowCommand.Close: Native.SendMessage(target, 0x0010, 0, 0); return "Closed";
            case WindowCommand.Pin: Native.SetWindowPos(target, -1, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE); return "Pinned on top";
            default: return "Done";
        }
    }

    private string Undo()
    {
        var manifest = state.Commits().LastOrDefault(m => !File.ReadAllText(m).Contains("\"undone\": true", StringComparison.Ordinal));
        if (manifest is null)
        {
            presence.Handle(new Failed("There is nothing to undo"));
            return "There is nothing to undo";
        }

        try
        {
            var paths = Overlay.Undo(manifest);
            presence.Handle(new Finished($"Undone: restored {paths.Count} {(paths.Count == 1 ? "file" : "files")}"));
            return "Undone";
        }
        catch (ConflictException e)
        {
            presence.Handle(new Failed(e.Message));
            return e.Message;
        }
    }

    private static string WindowCommandTitle(WindowCommand c) => c switch
    {
        WindowCommand.SnapLeft => "Snap this window to the left half",
        WindowCommand.SnapRight => "Snap this window to the right half",
        WindowCommand.Maximize => "Maximize this window",
        WindowCommand.Minimize => "Minimize this window",
        WindowCommand.Close => "Close this window",
        WindowCommand.CenterOnScreen => "Centre this window",
        WindowCommand.Pin => "Keep this window on top",
        _ => "Put the windows back",
    };

    private static string FriendlyFolder(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return dir.StartsWith(profile, StringComparison.OrdinalIgnoreCase) ? "~" + dir[profile.Length..] : dir;
    }
}
