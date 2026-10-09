using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Artifacts;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Safety;
using static ClaudeOS.Core.Tests.Planning.ScriptedModel;

namespace ClaudeOS.Core.Tests.Planning;

public sealed class ArtifactMakerTests : IDisposable
{
    private const string Chart = """{"type":"chart","title":"Spend","data":{"source":"budget.csv"},"transform":[{"derive":{"as":"month","from":"date","unit":"month"}},{"group":{"by":["month"],"aggregate":[{"op":"sum","field":"amount","as":"spend"}]}}],"mark":"bar","x":{"field":"month"},"y":{"field":"spend","format":"currency:USD"}}""";

    private readonly TestWorkspace _ws = new();

    public ArtifactMakerTests()
    {
        var rows = string.Concat(Enumerable.Range(0, 3000).Select(i => $"2026-0{1 + (i % 9)}-{1 + (i % 27):00},Software,{100 + (i % 40)}.5\n"));
        File.WriteAllText(_ws.P("budget.csv"), "date,category,amount\n" + rows);
    }

    public void Dispose() => _ws.Dispose();

    [Fact]
    public async Task Claude_sees_the_profile_not_the_data_and_the_recipe_runs_over_every_row()
    {
        var model = new ScriptedModel(
            Calls(Use("t1", "describe_data", """{"path":"budget.csv"}""")),
            Calls(Use("t2", "create_chart", Chart)));
        var data = await new ArtifactMaker(model).MakeChartAsync("spending by month", _ws.Overlay(), new Policy());

        var profile = Results(model.Requests[1])["t1"].Content;
        Assert.Contains("data: budget.csv (3000 rows)", profile);
        Assert.Contains("amount (number)", profile);
        Assert.True(profile.Length < 800, $"the profile was {profile.Length} characters");
        Assert.Equal(3000, data.SourceRows);
        Assert.Equal(9, data.Series[0].Points.Length);
    }

    [Fact]
    public async Task A_bad_recipe_goes_back_to_Claude_as_an_error_it_can_fix()
    {
        var bad = Chart.Replace("\"field\":\"spend\",\"format\"", "\"field\":\"spent\",\"format\"");
        var model = new ScriptedModel(Calls(Use("t1", "create_chart", bad)), Calls(Use("t2", "create_chart", Chart)));
        var data = await new ArtifactMaker(model).MakeChartAsync("x", _ws.Overlay(), new Policy());

        var error = Results(model.Requests[1])["t1"];
        Assert.True(error.IsError);
        Assert.Contains("y field 'spent' does not exist", error.Content);
        Assert.Contains("spend", error.Content); // lists the fields that do
        Assert.Equal("spend", data.YColumn.Name);
    }

    [Fact]
    public async Task Protected_files_and_non_tables_are_refused()
    {
        File.WriteAllText(_ws.P(".env.csv"), "a,b\n1,2\n");
        var model = new ScriptedModel(
            Calls(Use("a", "describe_data", """{"path":".env"}"""), Use("b", "describe_data", """{"path":"notes.md"}""")),
            Calls(Use("t", "create_chart", Chart)));
        await new ArtifactMaker(model).MakeChartAsync("x", _ws.Overlay(), new Policy());
        var results = Results(model.Requests[1]);
        Assert.Contains("protected", results["a"].Content);
        Assert.Contains("not a CSV", results["b"].Content);
    }

    [Fact]
    public async Task Editing_a_chart_shows_Claude_the_recipe_on_screen_and_runs_the_revision()
    {
        var current = ChartSpecJson.Write(ChartSpec.Parse(Chart));
        var revised = Chart.Replace("\"mark\":\"bar\"", "\"mark\":\"bar\",\"style\":{\"color\":\"#2A78D6\"}");
        var model = new ScriptedModel(Calls(Use("t1", "create_chart", revised)));
        var data = await new ArtifactMaker(model).EditChartAsync(current, "make the bars blue", _ws.Overlay(), new Policy());

        var opening = Assert.IsType<TextPart>(model.Requests[0].Messages[0].Content[0]).Text;
        Assert.Contains("recipe of the chart on screen", opening);
        Assert.Contains("make the bars blue", opening);
        Assert.Contains("\"derive\"", opening);
        Assert.Equal("#2A78D6", data.Spec.SeriesColor);
    }

    [Fact]
    public async Task A_question_instead_of_a_recipe_is_surfaced()
    {
        var model = new ScriptedModel(Reply(StopKind.EndTurn, null, new TextPart("Which column holds the amount?")));
        var e = await Assert.ThrowsAsync<NoPlanException>(() => new ArtifactMaker(model).MakeChartAsync("chart it", _ws.Overlay(), new Policy()));
        Assert.Contains("Which column", e.Message);
    }

    [Fact]
    public async Task A_mod_that_asks_for_more_than_it_reads_is_sent_back()
    {
        var fixedManifest = """{"manifest":{"id":"clock","name":"Clock","kind":"widget","placement":{"size":[160,60]},"capabilities":["system.time"],"view":{"metric":"{system.time.hour}"}}}""";
        // The model forgot a capability the view reads, then fixes it.
        var forgot = """{"manifest":{"id":"clock","name":"Clock","kind":"widget","placement":{"size":[160,60]},"capabilities":[],"view":{"metric":"{system.time.hour}"}}}""";
        var model = new ScriptedModel(Calls(Use("a", "create_mod", forgot)), Calls(Use("b", "create_mod", fixedManifest)));
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var proposal = await new ArtifactMaker(model).MakeModAsync("a clock", store, null);

        Assert.Contains("does not declare a capability", Results(model.Requests[1])["a"].Content);
        Assert.Equal("clock", proposal.Manifest.Id);
    }

    [Fact]
    public async Task New_files_apply_straight_away_with_undo_and_nothing_else_does()
    {
        var overlay = _ws.Overlay();
        var chartPlan = Plan.Parse("""{"summary":"s","actions":[{"type":"write_file","path":"Artifacts/2026-10-09/spend.chart.json","content":"{}"},{"type":"write_file","path":"Artifacts/2026-10-09/spend.svg","content":"<svg/>"}]}""");
        var result = await Creations.ApplyAsync(chartPlan, overlay, new Policy(), _ws.State);
        Assert.True(_ws.Exists("Artifacts/2026-10-09/spend.svg"));
        Overlay.Undo(result.Manifest!);
        Assert.False(_ws.Exists("Artifacts"));

        var overwrite = Plan.Parse("""{"summary":"s","actions":[{"type":"write_file","path":"notes.md","content":"changed"}]}""");
        var e1 = await Assert.ThrowsAsync<PolicyDeniedException>(() => Creations.ApplyAsync(overwrite, _ws.Overlay(), new Policy(), _ws.State));
        Assert.Contains("not a new file", e1.Message);
        Assert.Equal("old notes\n", _ws.ReadText("notes.md"));

        var mail = Plan.Parse("""{"summary":"s","actions":[{"type":"send_email","to":["a@b.example"],"subject":"s","body":"b"}]}""");
        await Assert.ThrowsAsync<PolicyDeniedException>(() => Creations.ApplyAsync(mail, _ws.Overlay(), new Policy(), _ws.State));
        Assert.Empty(Directory.EnumerateFiles(_ws.State.Outbox));
    }
}
