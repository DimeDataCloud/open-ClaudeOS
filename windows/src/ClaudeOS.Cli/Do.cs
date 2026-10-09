using System.Globalization;
using System.Text;
using ClaudeOS.Claude;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Artifacts;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Safety;
using ClaudeOS.Core.Search;

namespace ClaudeOS.Cli;

/// <summary>
/// `claudeos do "..."`: the Intent Bar on a terminal. The local router goes first: opening and
/// finding never touch a model. Charts and widgets are made by Claude writing a compact spec that
/// local code validates and runs. Everything else becomes a plan with the approval card.
/// </summary>
internal static class Do
{
    public static async Task<int> RunAsync(Options opts, StateDir state)
    {
        var text = string.Join(' ', opts.Positional).Trim();
        if (text.Length == 0)
        {
            return Cli.Fail("usage: claudeos do \"<what you want>\" [--root DIR]");
        }

        var root = opts.Get("root") ?? ".";
        var routing = await new IntentRouter(new LocalIntentClassifier()).RouteAsync(text);
        Console.Error.WriteLine($"  routed locally in {routing.Elapsed.TotalMilliseconds:0.0} ms: {routing.Intent.GetType().Name}");

        switch (routing.Intent)
        {
            case OpenIntent open:
                return FindLocally(open.Query, root);
            case FindIntent find:
                return FindLocally(find.Query, root);
            case UndoIntent:
                Console.WriteLine("Use `claudeos undo`.");
                return 0;
            case WindowIntent:
                Console.WriteLine("Window commands belong to the Windows shell.");
                return 0;
        }

        var ledger = new TokenLedger(Path.Combine(state.Path, "tokens.jsonl"), new Budget(DailyUsd: ParseUsd(opts.Get("daily-budget"), 5), MonthlyUsd: ParseUsd(opts.Get("monthly-budget"), 60)));
        var model = AnthropicModelClient.WithKey(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        var settings = new ModelSettings(opts.Get("model") ?? "claude-opus-5-5", ParseEffort(opts.Get("effort")));
        void Progress(AgentEvent e)
        {
            switch (e.Kind)
            {
                case "list_dir": Console.Error.WriteLine($"  looking in {Name(e.Detail)}"); break;
                case "read_file": Console.Error.WriteLine($"  reading {Name(e.Detail)}"); break;
                case "plan_rejected": Console.Error.WriteLine("  adjusting the plan after policy feedback"); break;
                case "retry": Console.Error.WriteLine("  trying again"); break;
            }
        }

        try
        {
            var workspace = new Overlay(root);
            var policy = Cli.PolicyFrom(opts);
            int code;
            if (routing.Intent is MakeIntent { Kind: MakeKind.Chart })
            {
                code = await MakeChartAsync(text, new ArtifactMaker(model, new ModelSettings(settings.Model, Effort.Medium), ledger, Progress), workspace, policy, opts, state);
            }
            else if (routing.Intent is MakeIntent { Kind: MakeKind.Widget or MakeKind.Mod })
            {
                code = await MakeModAsync(text, new ArtifactMaker(model, new ModelSettings(settings.Model, Effort.Medium), ledger, Progress), opts, state);
            }
            else
            {
                state.Log("intent", Audit.Of(("intent", Audit.Str(text)), ("root", Audit.Str(workspace.Root))));
                var plan = await new ClaudePlanner(model, settings, ledger, e => { state.Log(e.Kind, Audit.Of(("detail", Audit.Str(e.Detail)))); Progress(e); }).PlanAsync(text, workspace, policy);
                code = await Cli.ReviewAsync(plan, opts, state);
            }

            Console.Error.WriteLine($"  {ledger.Summary()}");
            return code;
        }
        catch (NoPlanException e)
        {
            Console.WriteLine(e.Message);
            return 2;
        }
        catch (Exception e) when (e is PlannerException or BudgetExceededException)
        {
            return Cli.Fail(e.Message);
        }
    }

    private static int FindLocally(string query, string root)
    {
        var hits = FileFinder.Rank(query, FileFinder.Scan([root]), DateTimeOffset.UtcNow, 5);
        if (hits.Count == 0)
        {
            Console.WriteLine($"Nothing in {Path.GetFullPath(root)} matches \"{query}\".");
            return 1;
        }

        Console.WriteLine("No model needed. Best matches:");
        foreach (var h in hits)
        {
            Console.WriteLine($"  {h.File.Path}  ({h.Reason})");
        }

        return 0;
    }

    private static async Task<int> MakeChartAsync(string request, ArtifactMaker maker, Overlay workspace, Policy policy, Options opts, StateDir state)
    {
        var data = await maker.MakeChartAsync(request, workspace, policy);
        var theme = ThemeResolver.Resolve(string.Equals(opts.Get("theme"), "dark", StringComparison.OrdinalIgnoreCase) ? Appearance.Dark : Appearance.Light);
        var size = ChartRenderer.PreferredSize(data);
        var render = ChartRenderer.Render(data, new ChartStyle(theme, size.Width, size.Height));

        var slug = new string((data.Spec.Title ?? $"{data.YColumn.Name}-by-{data.XColumn.Name}").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        var dir = $"Artifacts/{DateTime.Now:yyyy-MM-dd}";
        var artifacts = new Overlay(opts.Get("artifacts") ?? Path.Combine(workspace.Root));
        var plan = new Plan($"chart: {request}", "Saves the chart recipe and its picture as new files.", [
            new WriteFile($"{dir}/{slug}.chart.json", ChartSpecJson.Write(data.Spec) + "\n"),
            new WriteFile($"{dir}/{slug}.svg", render.Svg),
        ]);
        var result = await Creations.ApplyAsync(plan, artifacts, policy, state);
        Console.WriteLine(render.AltText);
        Console.WriteLine($"Charted {data.SourceRows:N0} rows locally; Claude saw only the profile.");
        Console.WriteLine($"Saved {dir}/{slug}.svg and {slug}.chart.json. Undo with: claudeos undo {result.CommitId}");
        return 0;
    }

    private static async Task<int> MakeModAsync(string request, ArtifactMaker maker, Options opts, StateDir state)
    {
        var modsRoot = opts.Get("mods") ?? Path.Combine(state.Path, "mods");
        Directory.CreateDirectory(modsRoot);
        var broker = new CapabilityBroker();
        var store = new ModStore(modsRoot, broker);
        var proposal = await maker.MakeModAsync(request, store, null);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Add the mod '{proposal.Manifest.Name}' ({proposal.Manifest.Kind.ToString().ToLowerInvariant()}, no code)\n\nIt can:\n");
        foreach (var c in proposal.Capabilities)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  - {c.Description} [{c.Risk}]\n");
        }

        if (proposal.Capabilities.Length == 0)
        {
            sb.Append("  - read nothing\n");
        }

        sb.Append("It cannot read your files, use the network or run code.\n");
        Console.WriteLine(sb);
        var outcome = await Session.ReviewAndApplyAsync(proposal.Plan, new Overlay(modsRoot), new Policy(), (card, _) =>
        {
            Console.WriteLine(card.ToText());
            Console.Write("\nInstall it? [y/N] ");
            return Task.FromResult(opts.Flag("yes") || Console.ReadLine()?.Trim().ToLowerInvariant() is "y" or "yes");
        }, new OutboxConnector(state.Outbox), state);

        if (outcome.Status != Session.Status.Applied)
        {
            Console.WriteLine("Nothing was installed.");
            return outcome.Status == Session.Status.Refused ? 1 : 0;
        }

        store.RecordApproval(proposal.Manifest, proposal.ManifestJson);
        Console.WriteLine($"Installed in {modsRoot}. Undo with: claudeos undo {outcome.Result!.CommitId}");
        return 0;
    }

    private static string Name(string path) => Path.GetFileName(path.TrimEnd('/', '\\')) is { Length: > 0 } n ? n : "the workspace";

    private static double ParseUsd(string? s, double fallback) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static Core.Planning.Effort ParseEffort(string? s) => s?.ToLowerInvariant() switch
    {
        "low" => Core.Planning.Effort.Low,
        "medium" => Core.Planning.Effort.Medium,
        "xhigh" => Core.Planning.Effort.XHigh,
        "max" => Core.Planning.Effort.Max,
        _ => Core.Planning.Effort.High,
    };
}
