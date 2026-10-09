using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Terminal;

namespace ClaudeOS.Core.Tests.Terminal;

public sealed class TerminalBarTests : IDisposable
{
    private readonly TestWorkspace _ws = new();

    public TerminalBarTests()
    {
        File.WriteAllText(Path.Combine(_ws.Root, "Q3 budget.xlsx"), "x");
        File.WriteAllText(Path.Combine(_ws.Root, "lease agreement.pdf"), "x");
        File.WriteAllText(Path.Combine(_ws.Root, "notes.md"), "x");
    }

    public void Dispose() => _ws.Dispose();

    private async Task<(string Transcript, TerminalBar Bar)> RunAsync(string script, Func<UserIntent, string, CancellationToken, Task<AssistOutcome>>? assist = null,
        Func<AssistOutcome>? undo = null, Action<string>? open = null)
    {
        var output = new StringWriter();
        var bar = new TerminalBar(new StringReader(script), output, [_ws.Root], new IntentRouter(new LocalIntentClassifier()))
        {
            Assist = assist,
            Undo = undo,
            Open = open,
        };
        Assert.Equal(0, await bar.RunAsync());
        return (output.ToString(), bar);
    }

    [Fact]
    public async Task Opening_and_finding_are_answered_on_the_device_with_ranked_files()
    {
        var (text, _) = await RunAsync("open the q3 budget\n");
        Assert.Contains("on this device", text);
        Assert.Contains("no model", text);
        Assert.Contains("1  ", text);
        Assert.Contains("Q3 budget.xlsx", text);
        Assert.Contains("✓ Found it. Type 1 to open it", text);
        Assert.DoesNotContain("asking Claude", text);
    }

    [Fact]
    public async Task A_number_opens_the_result_it_names_and_only_that()
    {
        var opened = new List<string>();
        var (text, _) = await RunAsync("find the lease\n1\n", open: opened.Add);
        Assert.Equal([Path.Combine(_ws.Root, "lease agreement.pdf")], opened);
        Assert.Contains("✓ Opened lease agreement.pdf", text);
    }

    [Theory]
    [InlineData("5", "Nothing to open yet")]
    [InlineData("0", "Nothing to open yet")]
    public async Task A_number_with_nothing_listed_opens_nothing(string line, string expected)
    {
        var opened = new List<string>();
        var (text, _) = await RunAsync(line + "\n", open: opened.Add);
        Assert.Empty(opened);
        Assert.Contains(expected, text);
    }

    [Fact]
    public async Task A_number_outside_the_list_is_refused_with_the_range()
    {
        var opened = new List<string>();
        var (text, _) = await RunAsync("open notes\n9\n", open: opened.Add);
        Assert.Empty(opened);
        Assert.Contains("Pick a number from 1 to", text);
    }

    [Fact]
    public async Task Nothing_found_says_so_and_a_failed_open_is_reported_not_thrown()
    {
        var (text, _) = await RunAsync("open the tax return from 1999\n");
        Assert.Contains("Nothing matches", text);

        var (failed, _) = await RunAsync("open notes\n1\n", open: _ => throw new InvalidOperationException("no opener installed"));
        Assert.Contains("⊘ Could not open notes.md: no opener installed", failed);

        var (noApp, _) = await RunAsync("open notes\n1\n", open: _ => throw new System.ComponentModel.Win32Exception("xdg-open not found"));
        Assert.Contains("⊘ Could not open notes.md: there is no default app to open it with", noApp);
    }

    [Fact]
    public async Task Anything_that_needs_claude_goes_to_the_host_which_owns_the_approval()
    {
        var seen = new List<(string Kind, string Text)>();
        var (text, _) = await RunAsync("summarise the invoices and email finance\n", assist: (intent, line, _) =>
        {
            seen.Add((intent.GetType().Name, line));
            return Task.FromResult(AssistOutcome.Done("Done. The message is queued."));
        });
        Assert.Single(seen);
        Assert.Contains("asking Claude", text);
        Assert.Contains("✓ Done. The message is queued.", text);
    }

    [Fact]
    public async Task Without_a_connection_to_claude_the_local_things_still_work_and_the_rest_says_why()
    {
        var (text, _) = await RunAsync("sing me a song\nopen notes\n");
        Assert.Contains("⊘ Claude is not connected; opening and finding still work", text);
        Assert.Contains("notes.md", text);
    }

    [Fact]
    public async Task A_host_that_throws_becomes_a_message_and_the_bar_carries_on()
    {
        var (text, _) = await RunAsync("sing me a song\nopen notes\n", assist: (_, _, _) => throw new InvalidOperationException("the network went away"));
        Assert.Contains("⊘ the network went away", text);
        Assert.Contains("notes.md", text);
    }

    [Fact]
    public async Task Window_commands_and_unanswerable_yes_are_explained()
    {
        var (text, _) = await RunAsync("snap left\nyes\n");
        Assert.Contains("  There are no windows to move in a terminal", text);
        Assert.Contains("  There is nothing to confirm right now", text);
        Assert.DoesNotContain("⊘", text); // an explanation is not a failure
    }

    [Fact]
    public async Task Undo_goes_to_the_host_and_reports_what_it_did()
    {
        var (text, _) = await RunAsync("undo\n", undo: () => AssistOutcome.Done("Undid the last change"));
        Assert.Contains("✓ Undid the last change", text);
        var (without, _) = await RunAsync("undo\n");
        Assert.Contains("  Undo is not available here", without);
    }

    [Fact]
    public async Task Help_blank_lines_exit_and_end_of_input_all_leave_cleanly()
    {
        var (text, _) = await RunAsync("\n   \nhelp\nexit\nopen notes\n");
        Assert.Contains("Anything that changes your files shows what will happen first", text);
        Assert.DoesNotContain("notes.md", text); // nothing after exit runs

        var (eof, _) = await RunAsync("");
        Assert.Contains("open-ClaudeOS", eof);
    }

    [Fact]
    public async Task The_spark_has_a_plain_ascii_form_for_terminals_that_cannot_draw_it()
    {
        var output = new StringWriter();
        var bar = new TerminalBar(new StringReader("open notes\n"), output, [_ws.Root], new IntentRouter(new LocalIntentClassifier())) { Ascii = true };
        await bar.RunAsync();
        var text = output.ToString();
        Assert.Contains("v Found it", text);
        Assert.DoesNotContain("✓", text);
        Assert.DoesNotContain("○", text);
    }

    [Fact]
    public async Task Random_lines_never_crash_the_bar()
    {
        var rng = new Random(20261013);
        var lines = Enumerable.Range(0, 400).Select(_ => string.Concat(Enumerable.Range(0, rng.Next(0, 40)).Select(_ => (char)rng.Next(9, rng.Next(2) == 0 ? 127 : 0x2FFF)))
            .Replace('\n', ' ').Replace('\r', ' '));
        var (text, _) = await RunAsync(string.Join('\n', lines) + "\n", assist: (_, _, _) => Task.FromResult(AssistOutcome.Done("ok")), undo: () => AssistOutcome.Done("ok"));
        Assert.Contains("open-ClaudeOS", text);
    }
}
