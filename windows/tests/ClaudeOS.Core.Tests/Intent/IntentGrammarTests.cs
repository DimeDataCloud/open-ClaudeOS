using ClaudeOS.Core.Intent;

namespace ClaudeOS.Core.Tests.Intent;

public sealed class IntentGrammarTests
{
    private static UserIntent Parse(string text) => IntentGrammar.Parse(text).Intent;

    [Theory]
    [InlineData("open the Q3 budget", "q3 budget", null)]
    [InlineData("Open my resume", "resume", null)]
    [InlineData("pull up the invoice from Acme", "invoice from acme", null)]
    [InlineData("open notes in vscode", "notes", "vscode")]
    [InlineData("can you please open downloads?", "downloads", null)]
    [InlineData("show me the Q3 budget", "q3 budget", null)]
    [InlineData("go to my downloads folder", "downloads folder", null)]
    [InlineData("show me the Q3 report", "q3 report", null)]
    [InlineData("open the sales chart", "sales chart", null)]
    public void Opening_things_that_exist_needs_no_model(string text, string query, string? app)
    {
        var open = Assert.IsType<OpenIntent>(Parse(text));
        Assert.Equal(query, open.Query);
        Assert.Equal(app, open.App);
        Assert.True(IntentGrammar.Parse(text).Matched);
    }

    [Theory]
    [InlineData("find invoices from september", "invoices from september")]
    [InlineData("where is my passport scan", "passport scan")]
    [InlineData("where did I save the lease", "lease")]
    [InlineData("search for tax 2025", "tax 2025")]
    public void Finding_files_needs_no_model(string text, string query) =>
        Assert.Equal(query, Assert.IsType<FindIntent>(Parse(text)).Query);

    [Theory]
    [InlineData("snap left", WindowCommand.SnapLeft)]
    [InlineData("snap this to the right half", WindowCommand.SnapRight)]
    [InlineData("maximize", WindowCommand.Maximize)]
    [InlineData("minimise this window", WindowCommand.Minimize)]
    [InlineData("close this", WindowCommand.Close)]
    [InlineData("put it back", WindowCommand.PutBack)]
    [InlineData("restore my layout", WindowCommand.PutBack)]
    [InlineData("center this", WindowCommand.CenterOnScreen)]
    [InlineData("pin this on top", WindowCommand.Pin)]
    public void Window_commands_are_local(string text, WindowCommand command) =>
        Assert.Equal(command, Assert.IsType<WindowIntent>(Parse(text)).Command);

    [Theory]
    [InlineData("undo")]
    [InlineData("Undo that!")]
    [InlineData("revert the last change")]
    public void Undo_is_local(string text) => Assert.IsType<UndoIntent>(Parse(text));

    [Theory]
    [InlineData("show me spending by month from the Q3 budget as a graph", MakeKind.Chart)]
    [InlineData("graph spending by month", MakeKind.Chart)]
    [InlineData("show me a chart of revenue by region", MakeKind.Chart)]
    [InlineData("write a one-page report on these invoices", MakeKind.Report)]
    [InlineData("summarize these invoices", MakeKind.Report)]
    [InlineData("give me a summary of today's emails", MakeKind.Report)]
    [InlineData("draw a diagram of how the auth flow works", MakeKind.Diagram)]
    [InlineData("make me a widget with battery and my next meeting, top right", MakeKind.Widget)]
    [InlineData("make a dark theme with rounded corners", MakeKind.Theme)]
    [InlineData("give me a table of spending by vendor", MakeKind.Table)]
    [InlineData("make a timeline of the project milestones", MakeKind.Other)]
    public void Making_something_new_is_recognised(string text, MakeKind kind)
    {
        var make = Assert.IsType<MakeIntent>(Parse(text));
        Assert.Equal(kind, make.Kind);
    }

    [Theory]
    [InlineData("make the bars blue")]
    [InlineData("make the chart bigger")]
    [InlineData("add a legend")]
    [InlineData("sort by total")]
    [InlineData("change the title to Q3 spend")]
    [InlineData("make the clock bigger")]
    public void Editing_what_is_on_screen_is_a_change(string text) => Assert.IsType<ChangeIntent>(Parse(text));

    [Theory]
    [InlineData("what's the weather like in the movie")]
    [InlineData("")]
    [InlineData("hmm")]
    public void Anything_else_is_left_for_the_classifier_or_the_cloud(string text)
    {
        var result = IntentGrammar.Parse(text);
        Assert.False(result.Matched);
        Assert.IsType<UnclearIntent>(result.Intent);
    }

    [Fact]
    public async Task The_router_tries_grammar_then_npu_then_cloud()
    {
        var router = new IntentRouter(new FakeClassifier(IntentLabel.Make, 0.93), new FakeCloud());

        var grammar = await router.RouteAsync("open the budget");
        Assert.Equal(RouteSource.Grammar, grammar.Source);

        var npu = await router.RouteAsync("could you whip something up with last quarter's numbers");
        Assert.Equal(RouteSource.Npu, npu.Source);
        Assert.IsType<MakeIntent>(npu.Intent);

        var unsure = new IntentRouter(new FakeClassifier(IntentLabel.Make, 0.4), new FakeCloud());
        var cloud = await unsure.RouteAsync("whatever this is");
        Assert.Equal(RouteSource.Cloud, cloud.Source);
        Assert.IsType<FindIntent>(cloud.Intent);

        var offline = await new IntentRouter().RouteAsync("whatever this is");
        Assert.IsType<UnclearIntent>(offline.Intent);
    }

    [Fact]
    public async Task Grammar_routing_is_effectively_instant()
    {
        var router = new IntentRouter();
        await router.RouteAsync("open warm-up");
        var routed = await router.RouteAsync("open the Q3 budget");
        Assert.True(routed.Elapsed < TimeSpan.FromMilliseconds(20), $"took {routed.Elapsed.TotalMilliseconds} ms");
    }

    private sealed class FakeClassifier(IntentLabel label, double confidence) : IIntentClassifier
    {
        public Task<ClassifierResult> ClassifyAsync(string text, CancellationToken ct = default) => Task.FromResult(new ClassifierResult(label, confidence));
    }

    private sealed class FakeCloud : ICloudIntentResolver
    {
        public Task<UserIntent> ResolveAsync(string text, CancellationToken ct = default) =>
            Task.FromResult<UserIntent>(new FindIntent(text, text));
    }
}
