using System.Text;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Artifacts;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Cli;

/// <summary>Command line front end. It is the stand-in for the Intent Bar on any platform, and
/// the way to exercise the whole core without the Windows shell.</summary>
public static class Cli
{
    private const string Usage = """
        claudeos: say what you want; review exactly what will happen; approve once.

        Usage:
          claudeos do "<intent>" [--root DIR] [--model ID] [--effort LEVEL]   plan with Claude, then review
          claudeos apply <plan.json> [--root DIR] [--yes]                      review a plan file (no model needed)
          claudeos undo [commit-id]                                             revert the last (or named) commit
          claudeos log [-n N]                                                   what was read, proposed, approved, done
          claudeos chart <spec.json> [--data FILE] [--theme light|dark] [--out FILE]   run a chart recipe and draw it
          claudeos route "<text>"                                               how a request would be routed
          claudeos theme [--accent #HEX] [--appearance light|dark]              resolve and check a theme

        Options for do / apply:
          --root DIR            the workspace the plan may touch (default: .)
          --trust-domain D      an email domain you already trust (repeatable)
          --trust-host H        an HTTP host you already trust (repeatable)
          --state-dir DIR       where the undo journal, outbox and audit log live
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        var opts = Options.Parse(args);
        if (opts.Command is null or "help" or "--help" or "-h")
        {
            Console.WriteLine(Usage);
            return opts.Command is null ? 2 : 0;
        }

        try
        {
            var state = new StateDir(opts.Get("state-dir"));
            return opts.Command switch
            {
                "do" => await Do.RunAsync(opts, state),
                "apply" => await ApplyAsync(opts, state),
                "undo" => Undo(opts, state),
                "log" => Log(opts, state),
                "chart" => Chart(opts),
                "route" => await RouteAsync(opts),
                "theme" => Theme(opts),
                _ => Fail($"unknown command '{opts.Command}'. Try: claudeos help"),
            };
        }
        catch (Exception e) when (e is ActionException or GrantException or ConflictException or CommitException or SpecException or IOException or FormatException)
        {
            return Fail(e.Message);
        }
    }

    internal static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 1;
    }

    internal static Policy PolicyFrom(Options opts) => new()
    {
        TrustedEmailDomains = [.. opts.All("trust-domain").Select(d => d.ToLowerInvariant())],
        TrustedHosts = [.. opts.All("trust-host").Select(h => h.ToLowerInvariant())],
    };

    internal static async Task<int> ReviewAsync(Plan plan, Options opts, StateDir state)
    {
        var assume = opts.Flag("yes");
        var outcome = await Session.ReviewAndApplyAsync(
            plan,
            new Overlay(opts.Get("root") ?? "."),
            PolicyFrom(opts),
            (card, _) =>
            {
                Console.WriteLine(card.ToText());
                Console.WriteLine();
                if (assume)
                {
                    Console.WriteLine("Approve and run? [y/N] y (--yes)");
                    return Task.FromResult(true);
                }

                Console.Write("Approve and run? [y/N] ");
                var answer = Console.ReadLine();
                return Task.FromResult(answer is not null && answer.Trim().ToLowerInvariant() is "y" or "yes");
            },
            new OutboxConnector(state.Outbox),
            state);

        switch (outcome.Status)
        {
            case Session.Status.Refused:
                Console.WriteLine(outcome.Card.ToText());
                return 1;
            case Session.Status.Declined:
                Console.WriteLine("Declined. Nothing was changed.");
                return 0;
            default:
                var result = outcome.Result!;
                if (result.Manifest is not null)
                {
                    Console.WriteLine($"Applied. Undo with: claudeos undo {result.CommitId}");
                }

                foreach (var (description, detail) in result.External)
                {
                    Console.WriteLine($"  {description}: {detail}");
                }

                return 0;
        }
    }

    private static async Task<int> ApplyAsync(Options opts, StateDir state)
    {
        if (opts.Positional.Count == 0)
        {
            return Fail("usage: claudeos apply <plan.json> [--root DIR]");
        }

        return await ReviewAsync(Plan.Load(opts.Positional[0]), opts, state);
    }

    private static int Undo(Options opts, StateDir state)
    {
        var manifests = state.Commits()
            .Where(m => !File.ReadAllText(m).Contains("\"undone\": true", StringComparison.Ordinal))
            .Where(m => opts.Positional.Count == 0 || Path.GetFileName(Path.GetDirectoryName(m))!.StartsWith(opts.Positional[0], StringComparison.Ordinal))
            .ToList();
        if (manifests.Count == 0)
        {
            Console.Error.WriteLine("Nothing to undo.");
            return 1;
        }

        var manifest = manifests[^1];
        var paths = Overlay.Undo(manifest);
        state.Log("undone", Audit.Of(("commit_id", Audit.Str(Path.GetFileName(Path.GetDirectoryName(manifest)))), ("paths", Audit.Strs(paths))));
        Console.WriteLine($"Undid {Path.GetFileName(Path.GetDirectoryName(manifest))}: restored {paths.Count} file(s).");
        return 0;
    }

    private static int Log(Options opts, StateDir state)
    {
        var n = int.TryParse(opts.Get("n"), out var parsed) ? parsed : 20;
        foreach (var record in state.ReadLog(n))
        {
            var detail = record.Where(kv => kv.Key is not ("ts" or "event" or "plan")).Select(kv => $"\"{kv.Key}\":{kv.Value?.ToJsonString()}");
            var text = "{" + string.Join(",", detail) + "}";
            Console.WriteLine($"{record["ts"]}  {record["event"],-13} {(text.Length > 160 ? text[..160] : text)}");
        }

        return 0;
    }

    private static int Chart(Options opts)
    {
        if (opts.Positional.Count == 0)
        {
            return Fail("usage: claudeos chart <spec.json> [--data FILE] [--theme light|dark] [--out FILE]");
        }

        var specPath = opts.Positional[0];
        var spec = ChartSpec.Parse(File.ReadAllText(specPath));
        var dataPath = opts.Get("data") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(specPath))!, spec.DataSource);
        var table = DataTable.FromCsv(File.ReadAllText(dataPath));
        var data = Recipe.Run(spec, table);

        var appearance = string.Equals(opts.Get("theme"), "dark", StringComparison.OrdinalIgnoreCase) ? Appearance.Dark : Appearance.Light;
        var theme = ThemeResolver.Resolve(appearance, opts.Get("accent") is { } a ? new ThemeOverrides { Accent = a } : null);
        var size = ChartRenderer.PreferredSize(data);
        if (opts.Get("size") is { } s && s.Split('x') is [var sw, var sh] && int.TryParse(sw, out var w) && int.TryParse(sh, out var h))
        {
            size = new ClaudeOS.Core.Layout.Size(w, h);
        }

        var render = ChartRenderer.Render(data, new ChartStyle(theme, size.Width, size.Height, opts.Flag("transparent")));
        if (opts.Get("out") is { } outPath)
        {
            File.WriteAllText(outPath, render.Svg, new UTF8Encoding(false));
            Console.WriteLine($"{outPath}: {render.Size.Width}x{render.Size.Height}, {render.Marks.Length} marks from {data.UsedRows} of {data.SourceRows} rows");
            Console.WriteLine(render.AltText);
        }
        else
        {
            Console.WriteLine(render.Svg);
        }

        return 0;
    }

    private static async Task<int> RouteAsync(Options opts)
    {
        var text = string.Join(' ', opts.Positional);
        var routing = await new IntentRouter().RouteAsync(text);
        Console.WriteLine($"{routing.Intent.GetType().Name} via {routing.Source} in {routing.Elapsed.TotalMilliseconds:0.00} ms");
        Console.WriteLine(routing.Intent);
        return 0;
    }

    private static int Theme(Options opts)
    {
        var appearance = string.Equals(opts.Get("appearance"), "dark", StringComparison.OrdinalIgnoreCase) ? Appearance.Dark : Appearance.Light;
        var theme = ThemeResolver.Resolve(appearance, new ThemeOverrides { Accent = opts.Get("accent") });
        foreach (var (role, hex) in theme.Colors.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"{role,-20} {hex}");
        }

        foreach (var error in theme.Errors)
        {
            Console.Error.WriteLine($"refused: {error}");
        }

        return theme.IsValid ? 0 : 1;
    }
}

/// <summary>Tiny argument parser: <c>command positional... --key value --flag</c>.</summary>
internal sealed class Options
{
    private static readonly HashSet<string> Flags = ["yes", "transparent", "help"];

    private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);

    public string? Command { get; private init; }

    public List<string> Positional { get; } = [];

    public static Options Parse(string[] args)
    {
        var opts = new Options { Command = args.Length > 0 ? args[0] : null };
        for (var i = 1; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var name = a[2..];
                if (name.Contains('='))
                {
                    var parts = name.Split('=', 2);
                    opts.Add(parts[0], parts[1]);
                }
                else if (Flags.Contains(name) || i + 1 >= args.Length)
                {
                    opts.Add(name, "true");
                }
                else
                {
                    opts.Add(name, args[++i]);
                }
            }
            else if (a == "-n" && i + 1 < args.Length)
            {
                opts.Add("n", args[++i]);
            }
            else
            {
                opts.Positional.Add(a);
            }
        }

        return opts;
    }

    private void Add(string key, string value)
    {
        if (!_values.TryGetValue(key, out var list))
        {
            _values[key] = list = [];
        }

        list.Add(value);
    }

    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v[^1] : null;

    public IEnumerable<string> All(string key) => _values.TryGetValue(key, out var v) ? v : [];

    public bool Flag(string key) => Get(key) == "true";
}
