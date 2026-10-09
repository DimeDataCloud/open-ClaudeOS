using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Artifacts;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Safety;
using ClaudeOS.Shell.Views;
using Windows.Storage;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Runs the shell against itself on a real desktop and writes what happened to a file, so a CI
/// machine can say whether the windows really open, render and respond. It is started by a marker
/// file in the app's own private folder (<c>LocalState\selftest.flag</c>), which only the signed-in
/// user can create; nothing in the product sets it. It never touches the person's files or key: it
/// uses a temporary workspace, a temporary state folder and a scripted stand-in for Claude.
/// </summary>
internal sealed class SelfTest(App app, AppServices services, IntentBarWindow bar, ShellUi ui)
{
    private readonly List<string> _lines = [];
    private int _failures;
    private string _workspace = "";

    /// <summary>The marker's path if a self-test was requested; null in normal use.</summary>
    public static string? FlagPath()
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "selftest.flag");
            return File.Exists(path) ? path : null;
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null; // not packaged
        }
    }

    public async Task RunAsync(string flag)
    {
        var folder = Path.GetDirectoryName(flag)!;
        File.Delete(flag);
        var total = Stopwatch.StartNew();
        _workspace = Directory.CreateTempSubdirectory("claudeos-selftest-").FullName;
        File.WriteAllText(Path.Combine(_workspace, "q3.csv"), SampleCsv());

        Note($"open-ClaudeOS self-test, {DateTime.Now:s}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        await WaitForFileIndex();

        await Step("intent bar", BarAsync);
        await Step("chart window", ChartWindowAsync);
        await Step("agent: chart from a sentence", AgentChartAsync);
        await Step("agent: widget from a sentence", AgentWidgetAsync);
        await Step("approval card: hold to send", ApprovalAsync);
        await Step("key window", KeyWindowAsync);
        await Step("device check", DeviceCheckAsync);

        ui.CloseAll();
        Note($"memory in use at the end: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB; total {total.Elapsed.TotalSeconds:0.0} s");
        Note(_failures == 0 ? "RESULT: all checks passed" : $"RESULT: {_failures} check(s) FAILED");
        File.WriteAllText(Path.Combine(folder, "selftest.txt"), string.Join('\n', _lines) + "\n");
        app.Exit();
    }

    // ------------------------------------------------------------------------------ steps

    private async Task<string> BarAsync()
    {
        bar.Summon();
        await Task.Delay(500);
        var probe = bar.Probe("q3");
        await Task.Delay(200);
        Expect(probe.Visible, "the bar is visible after being summoned");
        Expect(probe.Width > 300 && probe.Height > 40, $"the bar has a sensible size ({probe.Width}x{probe.Height})");
        bar.Dismiss();
        return $"visible, {probe.Width}x{probe.Height}px, summon {probe.SummonMs:0} ms, input focused: {probe.Focused}. Typing \"q3\" gave {probe.Results} result(s)" +
               (probe.First.Length > 0 ? $" (first: {probe.First})" : "") + $". Drew: {probe.Texts}";
    }

    private async Task<string> ChartWindowAsync()
    {
        var data = Recipe.Run(ChartSpec.Parse(JsonDocument.Parse(ChartSpecJsonText).RootElement), DataTable.FromCsv(SampleCsv()));
        var size = ChartRenderer.PreferredSize(data);
        var render = ChartRenderer.Render(data, new ChartStyle(ThemeResolver.Resolve(Appearance.Light), size.Width, size.Height));
        var window = (SubjectWindow)await ui.ShowChartAsync(render.AltText, render.Svg, render.Size.Width, render.Size.Height, _ => Task.CompletedTask);
        await Task.Delay(600);
        Expect(window.LoadStatus == "Success", $"the SVG chart loads (status: {window.LoadStatus})");
        Expect(window.PictureWidth > 50, $"the chart is laid out ({window.PictureWidth:0}px wide)");
        window.Close();
        return $"{render.Size.Width}x{render.Size.Height} chart, SVG {window.LoadStatus}, drawn {window.PictureWidth:0}px wide. {render.AltText}";
    }

    private async Task<string> AgentChartAsync()
    {
        var agent = MakeAgent();
        var message = await agent.RunAsync(new MakeIntent("chart spend by month from q3.csv", MakeKind.Chart, "spend by month"), "chart spend by month from q3.csv");
        await Task.Delay(600);
        Expect(message.StartsWith("Charted", StringComparison.Ordinal), $"the agent reports a chart (said: {message})");
        var saved = Directory.GetFiles(Path.Combine(_workspace, "Claude"), "*.svg", SearchOption.AllDirectories);
        Expect(saved.Length == 1, $"the chart was saved as a new file ({saved.Length} svg)");
        var windows = ui.Open.OfType<SubjectWindow>().ToList();
        Expect(windows.Count == 1 && windows[0].LoadStatus == "Success", "the chart window is open and loaded");
        ui.CloseAll();
        return $"\"{message}\", saved {Path.GetFileName(saved[0])}, presence now: {services.Bar.Presence.Frame.State}";
    }

    private async Task<string> AgentWidgetAsync()
    {
        var agent = MakeAgent();
        var run = agent.RunAsync(new MakeIntent("make a clock widget", MakeKind.Widget, "clock"), "make a clock widget");
        ApprovalWindow? card = null;
        for (var i = 0; i < 50 && card is null; i++)
        {
            await Task.Delay(100);
            card = ui.Open.OfType<ApprovalWindow>().FirstOrDefault();
        }

        Expect(card is not null, "the approval card for the widget appears");
        Expect(card!.RowCount >= 1, $"the card lists what the widget can read ({card.RowCount} row(s))");
        card.PressForTest(); // a local, low-risk approval is a plain click
        var message = await run;
        await Task.Delay(1300);
        var widget = ui.Open.OfType<WidgetWindow>().FirstOrDefault();
        Expect(widget is not null, $"the widget window is open (said: {message})");
        Expect(System.Text.RegularExpressions.Regex.IsMatch(widget!.Text, @"\d{1,2}:\d\d"), $"the widget shows a live time (it shows: {widget.Text})");
        ui.CloseAll();
        return $"\"{message}\"; widget shows: {widget.Text}";
    }

    private async Task<string> ApprovalAsync()
    {
        var overlay = new Overlay(_workspace);
        var plan = new Plan("Send the summary to finance", "Writes a file and emails it.", [
            new WriteFile("summary.md", "# Summary\n"),
            new SendEmail(["finance@example.com", "audit@elsewhere.example"], "Summary", "Hi,\n\nTotal: 3,420.00 USD\n"),
        ]);
        var card = ApprovalCard.Build(plan, new Policy().Preview(plan, overlay), overlay.Diff());
        var model = ApprovalModel.FromCard(card);
        Expect(model.HoldToApprove, "a plan that sends email needs a hold");
        var window = new ApprovalWindow(model);
        var answer = window.AskAsync();
        await Task.Delay(500);
        Expect(window.RowCount == 2, $"the card shows both actions ({window.RowCount})");

        window.PressForTest();
        await Task.Delay(150);
        window.ReleaseForTest(); // let go early
        await Task.Delay(100);
        Expect(!window.IsAnswered, "letting go early does not approve");

        window.PressForTest();
        await Task.Delay(1000); // hold
        Expect(window.IsAnswered && await answer, "holding the button approves");
        return $"2 rows, hold-to-approve required, early release ignored, full hold approved. Card says: {Controls.TreeText.Of(window.Content, 8)}";
    }

    private async Task<string> KeyWindowAsync()
    {
        var window = new KeyWindow();
        var asked = window.AskAsync();
        await Task.Delay(400);
        var text = Controls.TreeText.Of(window.Content, 6);
        window.Close();
        await asked;
        Expect(text.Contains("Connect Claude", StringComparison.Ordinal), "the key window draws its title");
        return text;
    }

    private async Task<string> DeviceCheckAsync()
    {
        var window = new DiagnosticsWindow(() => bar.LastSummonMilliseconds, () => services.Files.Count, services.Bar.Presence.ReducedMotion);
        await window.RunAsync();
        var report = window.ReportText;
        window.Close();
        Expect(report.Contains("Windows OCR", StringComparison.Ordinal), "the device check produced a report");
        return "\n" + string.Join('\n', report.TrimEnd().Split('\n').Select(l => "      " + l));
    }

    // ------------------------------------------------------------------------------ plumbing

    private AgentService MakeAgent()
    {
        var state = new StateDir(Path.Combine(_workspace, "state"));
        var ledger = new TokenLedger(Path.Combine(state.Path, "tokens.jsonl"));
        return new AgentService(services.Bar.Presence, state, ledger, services.Files, ui, () => Appearance.Light, () => new SelfTestModel(), _workspace);
    }

    private async Task WaitForFileIndex()
    {
        for (var i = 0; i < 40 && services.Files.Count == 0; i++)
        {
            await Task.Delay(250);
        }

        Note($"file index: {services.Files.Count} files");
    }

    private async Task Step(string name, Func<Task<string>> run)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var work = run();
            if (await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30))) != work)
            {
                throw new TimeoutException("took more than 30 seconds");
            }

            Note($"ok    {name} ({clock.ElapsedMilliseconds} ms): {await work}");
        }
        catch (Exception e)
        {
            _failures++;
            Note($"FAIL  {name} ({clock.ElapsedMilliseconds} ms): {e.GetType().Name}: {e.Message}");
        }
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("expected: " + what);
        }
    }

    private void Note(string line) => _lines.Add(line);

    private static string SampleCsv()
    {
        var sb = new StringBuilder("date,category,vendor,amount\n");
        string[] vendors = ["Marriott", "Notion", "Canva", "Meta Ads"];
        for (var month = 1; month <= 6; month++)
        {
            for (var i = 0; i < 4; i++)
            {
                sb.Append(CultureInfo.InvariantCulture, $"2026-{month:00}-{i + 1:00},Ops,{vendors[i]},{100 + (month * 17) + (i * 9)}.50\n");
            }
        }

        return sb.ToString();
    }

    private const string ChartSpecJsonText = """
        {"type":"chart","title":"Spending by month","data":{"source":"q3.csv"},
         "transform":[{"derive":{"as":"month","from":"date","unit":"month"}},
                      {"group":{"by":["month"],"aggregate":[{"op":"sum","field":"amount","as":"spend"}]}}],
         "mark":"bar","x":{"field":"month"},"y":{"field":"spend","title":"Spend","format":"currency:USD"}}
        """;

    private const string ClockManifest = """
        {"id":"selftest-clock","name":"Self-test clock","version":"1.0.0","kind":"widget","level":"declarative",
         "placement":{"anchor":"top-right","size":[220,110]},"capabilities":["system.time"],
         "view":{"stack":[{"metric":"{system.time.time}","label":"Now"},{"text":"{system.time.date}"}]}}
        """;

    /// <summary>Stands in for Claude: answers any chart or widget request with a fixed, valid tool call.</summary>
    private sealed class SelfTestModel : IModelClient
    {
        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct = default)
        {
            var (name, json) = request.Tools.Any(t => t.Name == "create_mod")
                ? ("create_mod", "{\"manifest\":" + ClockManifest + "}")
                : ("create_chart", ChartSpecJsonText);
            using var doc = JsonDocument.Parse(json);
            ImmutableArray<ContentPart> content = [new ToolUsePart("selftest-1", name, doc.RootElement.Clone())];
            return Task.FromResult(new ModelResponse(content, StopKind.ToolUse, new TokenUsage(100, 20), "selftest"));
        }
    }
}
