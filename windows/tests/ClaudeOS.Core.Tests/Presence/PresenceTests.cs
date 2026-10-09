using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Presence;

namespace ClaudeOS.Core.Tests.Presence;

public sealed class PresenceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static Routing Local(UserIntent i) => new(i, RouteSource.Grammar, TimeSpan.FromMilliseconds(1));

    [Fact]
    public void It_sleeps_until_summoned_and_breathes_while_waiting()
    {
        var p = new PresenceMachine();
        Assert.Equal(PresenceState.Dormant, p.Frame.State);
        p.Handle(new BarShown());
        Assert.Equal(PresenceState.Idle, p.Frame.State);
        Assert.Equal("What do you want to do?", p.Frame.Label);
        p.Handle(new BarHidden());
        Assert.Equal(PresenceState.Dormant, p.Frame.State);
    }

    [Fact]
    public void It_answers_typing_and_relaxes_when_the_box_is_empty_again()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new Typing(3));
        Assert.Equal(PresenceState.Listening, p.Frame.State);
        Assert.True(p.Frame.Intensity > 0.5);
        p.Handle(new Typing(0));
        Assert.Equal(PresenceState.Idle, p.Frame.State);
    }

    [Fact]
    public void Opening_something_is_a_shimmer_not_a_wait()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new Routed(Local(new OpenIntent("open budget", "budget")), "Opening Q3 Budget.xlsx"));
        Assert.Equal(PresenceState.Understanding, p.Frame.State);
        Assert.Equal("Opening Q3 Budget.xlsx", p.Frame.Label);
    }

    [Fact]
    public void Its_label_reports_what_the_agent_is_really_doing_and_shows_names_not_paths()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new Routed(new Routing(new MakeIntent("x", MakeKind.Chart, "x"), RouteSource.Cloud, TimeSpan.Zero)));
        Assert.Equal((PresenceState.Working, "Thinking"), (p.Frame.State, p.Frame.Label));
        p.Handle(new Agent(new AgentEvent("read_file", @"C:\Users\me\Finance\q3-budget.csv")));
        Assert.Equal("Reading q3-budget.csv", p.Frame.Label);
        p.Handle(new Agent(new AgentEvent("list_dir", ".")));
        Assert.Equal("Looking in your workspace", p.Frame.Label);
        p.Handle(new Agent(new AgentEvent("plan_rejected", "protected path")));
        Assert.Equal("Adjusting the plan", p.Frame.Label);
    }

    [Fact]
    public void A_decision_waits_for_you_even_when_the_bar_is_dismissed()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new PlanReady(3, "High", External: true));
        Assert.Equal(PresenceState.NeedsYou, p.Frame.State);
        Assert.Contains("3 actions", p.Frame.Hint);
        Assert.Contains("leaves this computer", p.Frame.Hint);

        p.Handle(new BarHidden());
        Assert.Equal(PresenceState.NeedsYou, p.Frame.State); // the tray keeps asking
        p.Handle(new BarShown());
        Assert.Equal(PresenceState.NeedsYou, p.Frame.State);
    }

    [Fact]
    public void Approving_then_finishing_settles_and_returns_to_rest()
    {
        var clock = new Clock();
        var p = new PresenceMachine(clock);
        p.Handle(new BarShown());
        p.Handle(new PlanReady(1, "Low", External: false));
        Assert.Contains("nothing leaves", p.Frame.Hint);
        p.Handle(new Approved());
        Assert.Equal("Applying", p.Frame.Label);
        p.Handle(new Finished("Done", Undoable: true));
        Assert.Equal(PresenceState.Done, p.Frame.State);
        Assert.Equal("Ctrl+Z to undo", p.Frame.Hint);

        clock.Now += TimeSpan.FromSeconds(1);
        p.Handle(new Tick(clock.Now));
        Assert.Equal(PresenceState.Done, p.Frame.State);
        clock.Now += TimeSpan.FromSeconds(2);
        p.Handle(new Tick(clock.Now));
        Assert.Equal(PresenceState.Idle, p.Frame.State);
    }

    [Fact]
    public void Declining_changes_nothing_and_says_so()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new PlanReady(2, "Medium", External: false));
        p.Handle(new Declined());
        Assert.Equal(PresenceState.Idle, p.Frame.State);
        Assert.Equal("Okay. Nothing changed.", p.Frame.Label);
    }

    [Fact]
    public void Failure_is_calm_and_explicit_about_what_did_not_happen()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new Failed("Policy refused the plan"));
        Assert.Equal(PresenceState.Refused, p.Frame.State);
        Assert.Equal("Nothing was changed", p.Frame.Hint);
    }

    [Fact]
    public void Offline_still_opens_things_but_says_cloud_work_needs_a_connection()
    {
        var p = new PresenceMachine();
        p.Handle(new BarShown());
        p.Handle(new Connectivity(false));
        Assert.True(p.Frame.Offline);
        Assert.StartsWith("Offline", p.Frame.Label);

        p.Handle(new Routed(Local(new OpenIntent("open x", "x")), "Opening x"));
        Assert.Equal(PresenceState.Understanding, p.Frame.State);

        p.Handle(new Routed(new Routing(new MakeIntent("x", MakeKind.Chart, "x"), RouteSource.Cloud, TimeSpan.Zero)));
        Assert.Equal(PresenceState.Refused, p.Frame.State);
        Assert.Equal("That needs a connection", p.Frame.Label);
    }

    [Fact]
    public void A_suggestion_waits_quietly_until_the_bar_is_open()
    {
        var p = new PresenceMachine();
        p.Handle(new Suggest("Open PDFs on the right half from now on?"));
        Assert.Equal(PresenceState.Dormant, p.Frame.State);
        p.Handle(new BarShown());
        Assert.Equal("Open PDFs on the right half from now on?", p.Frame.Suggestion);
    }

    [Fact]
    public void Observers_hear_only_real_changes()
    {
        var p = new PresenceMachine();
        var count = 0;
        p.Changed += _ => count++;
        p.Handle(new BarShown());
        p.Handle(new BarShown());
        p.Handle(new Typing(0));
        Assert.Equal(1, count);
    }
}
