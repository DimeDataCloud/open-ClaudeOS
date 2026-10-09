using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace ClaudeOS.Core.Mods;

/// <summary>A source of live values for a family of paths, such as <c>system.battery.*</c>.</summary>
public interface IDataProvider
{
    /// <summary>The path prefix this provider answers, for example <c>system.battery</c>.</summary>
    string Prefix { get; }

    bool TryGet(string path, out string value);
}

/// <summary>A view with every placeholder replaced. This is what the shell draws.</summary>
public sealed record RenderedNode(ViewKind Kind, string Primary, string Secondary, double? Number, double? Max, ImmutableArray<RenderedNode> Children);

/// <summary>
/// Resolves a mod's view against live data. Every placeholder is checked with the capability
/// broker first, so a mod can only ever read what the person approved, even if its manifest was
/// edited afterwards.
/// </summary>
public sealed class ViewBinder(CapabilityBroker broker, IEnumerable<IDataProvider> providers)
{
    private readonly List<IDataProvider> _providers = [.. providers];

    public IReadOnlyList<IDataProvider> Providers => _providers;

    private readonly ConcurrentDictionary<string, Formula> _formulas = new(StringComparer.Ordinal);

    public RenderedNode Bind(ModManifest mod, IReadOnlyDictionary<string, string>? settings = null)
    {
        var view = mod.View ?? throw new ModException($"mod '{mod.Id}' has no view");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in mod.Settings)
        {
            values[f.Key] = settings is not null && settings.TryGetValue(f.Key, out var v) ? v : f.Default;
        }

        var run = mod.Level == ModLevel.Scripted ? new FormulaRun(this, mod, values) : null;
        return BindNode(mod, view, values, run);
    }

    private RenderedNode BindNode(ModManifest mod, ViewNode node, Dictionary<string, string> settings, FormulaRun? run)
    {
        string Fill(string? text) => Substitute(mod, text, settings, run);
        var primary = Fill(node.Primary);
        var secondary = Fill(node.Secondary);
        double? number = double.TryParse(Fill(node.Value ?? node.Primary), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
        double? max = double.TryParse(Fill(node.Max), NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : null;
        return new RenderedNode(node.Kind, primary, secondary, node.Kind == ViewKind.Gauge ? number : null, max, [.. node.Children.Select(c => BindNode(mod, c, settings, run))]);
    }

    private string Substitute(ModManifest mod, string? text, Dictionary<string, string> settings, FormulaRun? run)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? "";
        }

        var sb = new StringBuilder();
        var last = 0;
        foreach (var (start, end, body) in Placeholders.Spans(text, quoteAware: run is not null))
        {
            sb.Append(text, last, start - last);
            sb.Append(run is null ? Resolve(mod, body, settings) : run.Text(body));
            last = end + 1;
        }

        // An opening brace that never closes is plain text, as it always was.
        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }

    private string Resolve(ModManifest mod, string path, Dictionary<string, string> settings) =>
        path.StartsWith("settings.", StringComparison.Ordinal)
            ? settings.TryGetValue(path["settings.".Length..], out var s) ? s : ""
            : ReadPath(mod, path);

    /// <summary>Read one piece of data for a mod. The broker is asked first, every time.</summary>
    private string ReadPath(ModManifest mod, string path)
    {
        broker.Require(mod.Id, path);
        foreach (var p in _providers.OrderByDescending(p => p.Prefix.Length))
        {
            if ((path == p.Prefix || path.StartsWith(p.Prefix + ".", StringComparison.Ordinal)) && p.TryGet(path, out var value))
            {
                return value;
            }
        }

        return "—";
    }

    private Formula Compile(string source)
    {
        if (_formulas.Count > 512)
        {
            _formulas.Clear();
        }

        return _formulas.GetOrAdd(source, Formula.Parse);
    }

    /// <summary>One draw of a scripted mod: its own step budget, its defs worked out once each.</summary>
    private sealed class FormulaRun(ViewBinder owner, ModManifest mod, Dictionary<string, string> settings)
    {
        private readonly FormulaBudget _budget = new(FormulaBudget.PerWidget);
        private readonly Dictionary<string, FormulaValue> _defs = new(StringComparer.Ordinal);

        /// <summary>The shown text of one <c>{formula}</c>. Running out of budget shows a dash; a refused read is not caught.</summary>
        public string Text(string body)
        {
            try
            {
                return owner.Compile(body).Evaluate(Lookup, _budget).Display;
            }
            catch (FormulaLimitException)
            {
                return "—";
            }
        }

        private FormulaValue Lookup(string name)
        {
            if (name.StartsWith("settings.", StringComparison.Ordinal))
            {
                return settings.TryGetValue(name["settings.".Length..], out var s) ? FormulaValue.FromData(s) : FormulaValue.Missing;
            }

            if (name.Contains('.'))
            {
                return FormulaValue.FromData(owner.ReadPath(mod, name));
            }

            if (_defs.TryGetValue(name, out var done))
            {
                return done;
            }

            // Cycles were refused when the manifest was read, so this always ends.
            var value = mod.Defs.TryGetValue(name, out var source) ? owner.Compile(source).Evaluate(Lookup, _budget) : FormulaValue.Missing;
            _defs[name] = value;
            return value;
        }
    }
}

/// <summary>Built-in provider for the clock, which needs nothing from the shell.</summary>
public sealed class ClockProvider(TimeProvider? clock = null) : IDataProvider
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string Prefix => "system.time";

    public bool TryGet(string path, out string value)
    {
        var now = _clock.GetLocalNow();
        value = path switch
        {
            "system.time.hour" => now.ToString("h:mm", CultureInfo.InvariantCulture),
            "system.time.time" => now.ToString("h:mm tt", CultureInfo.InvariantCulture),
            "system.time.date" => now.ToString("dddd, MMMM d", CultureInfo.InvariantCulture),
            _ => "",
        };
        return value.Length > 0;
    }
}
