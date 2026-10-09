using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Safety;

/// <summary>What the approval window is told to show. These are the rules a front end must not get
/// wrong: what is labelled as leaving the PC, when approving needs a hold, and what cannot be approved.</summary>
public sealed class ApprovalModelTests : IDisposable
{
    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private ApprovalModel ModelFor(params PlanAction[] actions)
    {
        var plan = new Plan("do the thing", "what the model says it does", [.. actions]);
        var overlay = _ws.Overlay();
        var assessments = new Policy().Preview(plan, overlay);
        return ApprovalModel.FromCard(ApprovalCard.Build(plan, assessments, overlay.Diff()));
    }

    [Fact]
    public void New_files_are_a_plain_click_and_say_nothing_leaves_the_pc()
    {
        var model = ModelFor(new WriteFile("reports/a.md", "x"));
        Assert.False(model.HoldToApprove);
        Assert.Equal("Apply", model.ApproveLabel);
        Assert.Equal(Risk.Low, model.Risk);
        Assert.Contains("new files", model.RiskLine);
        Assert.All(model.Rows, r => Assert.False(r.External));
        Assert.Equal("On this PC", model.Rows[0].Badge);
    }

    [Fact]
    public void Anything_that_leaves_the_pc_needs_a_hold_and_is_quoted_in_full()
    {
        var model = ModelFor(new WriteFile("reports/a.md", "x"), new SendEmail(["a@b.example"], "Subject", "line one\nline two"));
        Assert.True(model.HoldToApprove);
        Assert.Equal(Risk.High, model.Risk);
        var email = model.Rows[1];
        Assert.True(email.External);
        Assert.Equal("Leaves this PC", email.Badge);
        Assert.Equal(["line one", "line two"], email.Quoted.ToArray());
        Assert.Contains("cannot be taken back", model.RiskLine);
    }

    [Fact]
    public void The_models_own_summary_is_kept_but_only_as_a_claim()
    {
        var model = ModelFor(new WriteFile("reports/a.md", "x"));
        Assert.Equal("what the model says it does", model.ModelClaim);
        Assert.Equal("do the thing", model.Title); // the title is the person's request, never the model's words
    }

    [Fact]
    public void A_refused_plan_is_shown_with_the_reason_and_cannot_be_approved()
    {
        var model = ModelFor(new WriteFile(".env", "TOKEN=1"), new WriteFile("ok.md", "x"));
        Assert.True(model.Refused);
        Assert.Contains(model.Rows, r => r.Denied is { Length: > 0 });
    }

    [Fact]
    public void Changing_an_existing_file_is_medium_risk_and_still_a_click()
    {
        var model = ModelFor(new WriteFile("notes.md", "new notes\n"));
        Assert.Equal(Risk.Medium, model.Risk);
        Assert.False(model.HoldToApprove);
        Assert.Contains("undo", model.RiskLine);
        Assert.Contains("notes.md", model.Diff);
    }

    [Fact]
    public void A_mod_card_lists_what_it_can_read_in_plain_words()
    {
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var proposal = store.Review("""
            {"id":"clock","name":"Clock","version":"1.0.0","kind":"widget","level":"declarative",
             "placement":{"anchor":"top-right","size":[200,100]},"capabilities":["system.time"],
             "view":{"metric":"{system.time.time}","label":"Now"}}
            """);
        var model = ApprovalModel.FromMod(proposal);
        Assert.Equal("Add “Clock”", model.Title);
        Assert.Single(model.Rows);
        Assert.Equal("See the current date and time", model.Rows[0].Text);
        Assert.False(model.HoldToApprove);
    }

    [Fact]
    public void A_mod_that_can_reach_the_network_needs_a_hold()
    {
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var proposal = store.Review("""
            {"id":"fetcher","name":"Fetcher","version":"1.0.0","kind":"widget","level":"declarative",
             "placement":{"anchor":"top-right","size":[200,100]},"capabilities":["network.fetch:api.example.com"],
             "view":{"text":"hello"}}
            """);
        var model = ApprovalModel.FromMod(proposal);
        Assert.True(model.HoldToApprove);
        Assert.Contains("api.example.com", model.Rows[0].Text);
        Assert.True(model.Rows[0].External);
    }
}
