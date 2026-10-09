using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Presence;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Layout;

/// <summary>A habit is offered, never applied: accepting writes a normal layout-rule mod that goes
/// through the same approval card, and that the person can read, edit or delete.</summary>
public sealed class HabitRulesTests : IDisposable
{
    private static readonly Rect Area = new(0, 0, 2880, 1800);
    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static Suggestion ThirdTime()
    {
        var tracker = new HabitTracker();
        Suggestion? offered = null;
        for (var i = 0; i < 3; i++)
        {
            offered = tracker.Record("chart", new Rect(1900, 200, 800, 500), Area);
        }

        return Assert.IsType<Suggestion>(offered);
    }

    [Fact]
    public void The_third_time_it_offers_and_only_offers()
    {
        var tracker = new HabitTracker();
        Assert.Null(tracker.Record("chart", new Rect(1900, 200, 800, 500), Area));
        Assert.Null(tracker.Record("chart", new Rect(1950, 220, 800, 500), Area));
        var offer = tracker.Record("chart", new Rect(1920, 210, 800, 500), Area);
        Assert.Equal("Open chart on the top right from now on?", offer!.Message);
        Assert.Null(tracker.Record("chart", new Rect(1900, 200, 800, 500), Area)); // never nags twice
    }

    [Fact]
    public void An_accepted_habit_becomes_a_layout_rule_that_changes_where_the_next_chart_lands()
    {
        var suggestion = ThirdTime();
        var json = HabitRules.ToManifestJson(suggestion);
        var manifest = ModManifest.Parse(json);

        Assert.Equal("open-chart-top-right", manifest.Id);
        Assert.Equal(ModKind.LayoutRule, manifest.Kind);
        Assert.Empty(manifest.Capabilities);

        var request = ModEffects.ApplyRules([manifest], "chart", new PlacementRequest(new Size(800, 500)));
        Assert.Equal(Anchor.TopRight, request.Anchor);
        Assert.Equal(Anchor.Auto, ModEffects.ApplyRules([manifest], "widget", new PlacementRequest(new Size(800, 500))).Anchor);
    }

    [Fact]
    public void The_rule_goes_through_the_normal_mod_approval_as_a_plain_low_risk_card()
    {
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var proposal = store.Review(HabitRules.ToManifestJson(ThirdTime()));
        var card = ApprovalModel.FromMod(proposal);

        Assert.Equal("Add “Open chart on the top right”", card.Title);
        Assert.False(card.HoldToApprove);
        Assert.Equal(Risk.Low, card.Risk);
        var row = Assert.Single(card.Rows);
        Assert.Equal("New chart windows open on top right", row.Text);
        Assert.Contains("runs no code", row.Notes[0]);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("Yes please")]
    [InlineData("sure")]
    [InlineData("do that")]
    [InlineData("make it automatic")]
    [InlineData("go ahead")]
    public void Saying_yes_to_an_offer_is_understood_locally(string text)
    {
        var reply = Assert.IsType<SuggestionReplyIntent>(IntentGrammar.Parse(text).Intent);
        Assert.True(reply.Accepted);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("not now")]
    [InlineData("no thanks")]
    [InlineData("never mind")]
    public void Saying_no_is_understood_locally(string text)
    {
        var reply = Assert.IsType<SuggestionReplyIntent>(IntentGrammar.Parse(text).Intent);
        Assert.False(reply.Accepted);
    }

    [Fact]
    public void Ordinary_requests_are_not_mistaken_for_replies()
    {
        Assert.IsNotType<SuggestionReplyIntent>(IntentGrammar.Parse("open the yes folder").Intent);
        Assert.IsNotType<SuggestionReplyIntent>(IntentGrammar.Parse("undo").Intent);
        Assert.IsNotType<SuggestionReplyIntent>(IntentGrammar.Parse("summarize the invoices").Intent);
    }

    [Fact]
    public void The_offer_waits_in_the_bar_and_clears_once_answered()
    {
        var presence = new PresenceMachine();
        presence.Handle(new Suggest("Open chart on the top right from now on?"));
        presence.Handle(new BarShown());
        Assert.Equal("Open chart on the top right from now on?", presence.Frame.Suggestion);

        presence.Handle(new Suggest(""));
        presence.Handle(new BarShown());
        Assert.Null(presence.Frame.Suggestion);
    }
}
