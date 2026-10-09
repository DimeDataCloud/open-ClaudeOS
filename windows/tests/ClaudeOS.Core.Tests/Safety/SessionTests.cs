using System.Text.Json;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Safety;

public sealed class SessionTests : IDisposable
{
    private const string PlanJson = """
        {
          "intent": "summarize invoices and tell finance",
          "summary": "Write a CSV of invoice totals and email finance.",
          "actions": [
            {"type": "write_file", "path": "reports/invoices.csv", "content": "vendor,total\nAcme Corp,120.00\nGlobex,80.50\n"},
            {"type": "send_email", "to": ["finance@corp.example"], "subject": "Invoices", "body": "Total: 200.50"}
          ]
        }
        """;

    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private Task<Session.Outcome> Review(string planJson, Func<ApprovalCard, bool> approve) =>
        Session.ReviewAndApplyAsync(
            Plan.Parse(planJson), _ws.Overlay(), new Policy(), (c, _) => Task.FromResult(approve(c)),
            new OutboxConnector(_ws.State.Outbox), _ws.State);

    [Fact]
    public async Task A_declined_plan_changes_nothing()
    {
        var outcome = await Review(PlanJson, _ => false);
        Assert.Equal(Session.Status.Declined, outcome.Status);
        Assert.False(_ws.Exists("reports"));
        Assert.Empty(Directory.EnumerateFiles(_ws.State.Outbox));
    }

    [Fact]
    public async Task An_approved_plan_commits_files_then_runs_external_actions()
    {
        var seen = new List<string>();
        var outcome = await Review(PlanJson, c => { seen.Add(c.ToText()); return true; });

        Assert.Equal(Session.Status.Applied, outcome.Status);
        Assert.Contains("+Acme Corp,120.00", seen[0]);
        Assert.StartsWith("vendor,total", _ws.ReadText("reports/invoices.csv"));
        var queued = Assert.Single(Directory.EnumerateFiles(_ws.State.Outbox));
        using var doc = JsonDocument.Parse(File.ReadAllText(queued));
        Assert.Equal("finance@corp.example", doc.RootElement.GetProperty("to")[0].GetString());
        Assert.Equal(["proposed", "approved", "committed", "external"], _ws.State.ReadLog().Select(r => r["event"]!.GetValue<string>()));
    }

    [Fact]
    public async Task A_plan_with_any_denied_action_is_refused_before_asking()
    {
        var bad = """
            {"summary":"s","actions":[
              {"type":"write_file","path":"reports/invoices.csv","content":"x"},
              {"type":"delete_file","path":".ssh/id_ed25519"}]}
            """;
        var asked = false;
        var outcome = await Review(bad, _ => { asked = true; return true; });

        Assert.Equal(Session.Status.Refused, outcome.Status);
        Assert.False(asked);
        Assert.False(_ws.Exists("reports"));
        Assert.True(_ws.Exists(".ssh/id_ed25519"));
    }

    [Fact]
    public async Task Tampering_between_approval_and_execution_is_caught()
    {
        // The grant covers the digests of the plan that was shown. Executing a different
        // plan with that grant must fail, even if the overlay already holds staged changes.
        var shown = Plan.Parse(PlanJson);
        var grant = Grant.ForPlan(shown);
        var swapped = new Plan(shown.Intent, shown.Summary, [shown.Actions[0], new SendEmail(["audit@evil.example"], "dump", "everything")]);
        var overlay = _ws.Overlay();
        new Policy().Preview(swapped, overlay);
        await Assert.ThrowsAsync<GrantException>(() =>
            Executor.ExecuteAsync(swapped, grant, overlay, new OutboxConnector(_ws.State.Outbox), _ws.State));
        Assert.False(_ws.Exists("reports"));
    }

    [Fact]
    public async Task Commits_in_the_same_second_get_distinct_journal_entries()
    {
        var clock = new FixedClock();
        for (var i = 0; i < 2; i++)
        {
            var plan = Plan.Parse("""{"summary":"s","actions":[{"type":"write_file","path":"same.txt","content":"v"}]}""");
            var overlay = _ws.Overlay();
            new Policy().Preview(plan, overlay);
            await Executor.ExecuteAsync(plan, Grant.ForPlan(plan, clock: clock), overlay, new OutboxConnector(_ws.State.Outbox), _ws.State, clock);
            File.Delete(_ws.P("same.txt"));
        }

        Assert.Equal(2, _ws.State.Commits().Count);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    }
}
