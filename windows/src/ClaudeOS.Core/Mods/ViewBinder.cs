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

    public RenderedNode Bind(ModManifest mod, IReadOnlyDictionary<string, string>? settings = null)
    {
        var view = mod.View ?? throw new ModException($"mod '{mod.Id}' has no view");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in mod.Settings)
        {
            values[f.Key] = settings is not null && settings.TryGetValue(f.Key, out var v) ? v : f.Default;
        }

        return BindNode(mod, view, values);
    }

    private RenderedNode BindNode(ModManifest mod, ViewNode node, Dictionary<string, string> settings)
    {
        string Fill(string? text) => Substitute(mod, text, settings);
        var primary = Fill(node.Primary);
        var secondary = Fill(node.Secondary);
        double? number = double.TryParse(Fill(node.Value ?? node.Primary), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
        double? max = double.TryParse(Fill(node.Max), NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : null;
        return new RenderedNode(node.Kind, primary, secondary, node.Kind == ViewKind.Gauge ? number : null, max, [.. node.Children.Select(c => BindNode(mod, c, settings))]);
    }

    private string Substitute(ModManifest mod, string? text, Dictionary<string, string> settings)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? "";
        }

        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{')
            {
                sb.Append(text[i]);
                continue;
            }

            var close = text.IndexOf('}', i + 1);
            if (close < 0)
            {
                sb.Append(text, i, text.Length - i);
                break;
            }

            var path = text[(i + 1)..close].Trim();
            sb.Append(Resolve(mod, path, settings));
            i = close;
        }

        return sb.ToString();
    }

    private string Resolve(ModManifest mod, string path, Dictionary<string, string> settings)
    {
        if (path.StartsWith("settings.", StringComparison.Ordinal))
        {
            return settings.TryGetValue(path["settings.".Length..], out var s) ? s : "";
        }

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
