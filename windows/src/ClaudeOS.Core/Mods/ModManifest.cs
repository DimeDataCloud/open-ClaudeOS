using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeOS.Core.Mods;

public enum ModKind { Widget, ArtifactType, Command, LayoutRule, Theme, Hook, Connector }

public enum ModLevel { Declarative, Scripted, Web }

public sealed class ModException(string message) : Exception(message);

public sealed record WidgetPlacement(string Anchor, int Width, int Height);

public enum SettingType { Color, Boolean, Number, Text, Choice }

public sealed record SettingField(string Key, SettingType Type, string Default, string Label, double? Min = null, double? Max = null, ImmutableArray<string> Options = default);

public sealed record LayoutRuleSpec(string ContentKind, string Anchor, int? Width, int? Height);

public sealed record CommandSpec(string Phrase, ImmutableArray<string> Steps);

/// <summary>
/// A mod is a folder holding <c>mod.json</c>. The default level is declarative: pure JSON, no
/// code, drawn natively at full speed, themed automatically, and small enough for Claude to
/// write in a few hundred tokens. Parsing is strict: unknown fields are errors, because a
/// manifest is untrusted input from a model or from the internet.
/// </summary>
public sealed record ModManifest(
    string Id,
    string Name,
    string Version,
    ModKind Kind,
    ModLevel Level,
    ImmutableArray<string> Capabilities,
    WidgetPlacement? Placement,
    ViewNode? View,
    ImmutableArray<SettingField> Settings,
    ImmutableArray<LayoutRuleSpec> Rules,
    ImmutableArray<CommandSpec> Commands,
    JsonElement? Theme,
    ImmutableSortedDictionary<string, string>? Defs = null)
{
    /// <summary>Named formulas a scripted mod can refer to by name from its view (and from each other).</summary>
    public ImmutableSortedDictionary<string, string> Defs { get; init; } = Defs ?? ImmutableSortedDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    private static readonly Regex IdRx = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Allowed =
        ["id", "name", "version", "kind", "level", "capabilities", "placement", "view", "settings", "rules", "commands", "theme", "description", "defs"];

    public const int MaxManifestBytes = 64 * 1024;

    public static ModManifest Parse(string json)
    {
        if (json.Length > MaxManifestBytes)
        {
            throw new ModException($"mod.json is larger than {MaxManifestBytes / 1024} KB");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement.Clone());
        }
        catch (JsonException e)
        {
            throw new ModException($"mod.json is not valid JSON: {e.Message}");
        }
    }

    public static ModManifest Parse(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new ModException("mod.json must be an object");
        }

        var unknown = e.EnumerateObject().Select(p => p.Name).Where(n => !Allowed.Contains(n)).Order().ToList();
        if (unknown.Count > 0)
        {
            throw new ModException($"unknown mod.json fields: {string.Join(", ", unknown)}");
        }

        var id = Str(e, "id");
        if (!IdRx.IsMatch(id) || id.Length > 64)
        {
            throw new ModException($"id '{id}' must be lowercase words joined by hyphens, up to 64 characters");
        }

        var kind = Str(e, "kind") switch
        {
            "widget" => ModKind.Widget,
            "artifact-type" => ModKind.ArtifactType,
            "command" => ModKind.Command,
            "layout-rule" => ModKind.LayoutRule,
            "theme" => ModKind.Theme,
            "hook" => ModKind.Hook,
            "connector" => ModKind.Connector,
            var k => throw new ModException($"kind '{k}' is not one of widget, artifact-type, command, layout-rule, theme, hook, connector"),
        };

        var level = e.TryGetProperty("level", out var lv) ? lv.GetString() switch
        {
            "declarative" => ModLevel.Declarative,
            "scripted" => ModLevel.Scripted,
            "web" => ModLevel.Web,
            var l => throw new ModException($"level '{l}' is not one of declarative, scripted, web"),
        } : ModLevel.Declarative;

        var caps = ImmutableArray.CreateBuilder<string>();
        if (e.TryGetProperty("capabilities", out var cs))
        {
            if (cs.ValueKind != JsonValueKind.Array)
            {
                throw new ModException("capabilities must be a list");
            }

            foreach (var c in cs.EnumerateArray())
            {
                var cap = c.ValueKind == JsonValueKind.String ? c.GetString()! : throw new ModException("capabilities must be strings");
                if (!Mods.Capabilities.TryDescribe(cap, out _, out _))
                {
                    throw new ModException($"unknown capability '{cap}'; known: {string.Join(", ", Mods.Capabilities.Known.Keys.Order())}");
                }

                caps.Add(cap);
            }
        }

        WidgetPlacement? placement = null;
        if (e.TryGetProperty("placement", out var pl))
        {
            var size = pl.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Array && sz.GetArrayLength() == 2 ? sz : throw new ModException("placement.size must be [width, height]");
            var (w, h) = (size[0].GetInt32(), size[1].GetInt32());
            if (w is < 80 or > 1200 || h is < 40 or > 1200)
            {
                throw new ModException("placement.size must be between 80x40 and 1200x1200");
            }

            placement = new WidgetPlacement(pl.TryGetProperty("anchor", out var a) ? a.GetString() ?? "top-right" : "top-right", w, h);
        }

        ViewNode? view = e.TryGetProperty("view", out var v) ? ViewNode.Parse(v) : null;
        if (kind == ModKind.Widget && (view is null || placement is null))
        {
            throw new ModException("a widget needs both a view and a placement");
        }

        var settings = ImmutableArray.CreateBuilder<SettingField>();
        if (e.TryGetProperty("settings", out var st) && st.ValueKind == JsonValueKind.Object)
        {
            foreach (var s in st.EnumerateObject())
            {
                settings.Add(ParseSetting(s.Name, s.Value));
            }
        }

        var rules = ImmutableArray.CreateBuilder<LayoutRuleSpec>();
        if (e.TryGetProperty("rules", out var rs))
        {
            foreach (var r in rs.EnumerateArray())
            {
                var match = r.GetProperty("match").GetProperty("kind").GetString() ?? throw new ModException("rule match.kind must be text");
                var place = r.GetProperty("place");
                var sizeArr = place.TryGetProperty("size", out var sa) ? sa : default;
                rules.Add(new LayoutRuleSpec(match, place.TryGetProperty("anchor", out var an) ? an.GetString() ?? "top-right" : "top-right",
                    sizeArr.ValueKind == JsonValueKind.Array ? sizeArr[0].GetInt32() : null, sizeArr.ValueKind == JsonValueKind.Array ? sizeArr[1].GetInt32() : null));
            }
        }

        var commands = ImmutableArray.CreateBuilder<CommandSpec>();
        if (e.TryGetProperty("commands", out var cmds))
        {
            foreach (var c in cmds.EnumerateArray())
            {
                var phrase = c.GetProperty("phrase").GetString()?.Trim().ToLowerInvariant() ?? throw new ModException("command phrase must be text");
                var steps = c.GetProperty("steps").EnumerateArray().Select(x => x.GetString() ?? throw new ModException("command steps must be text")).ToImmutableArray();
                if (phrase.Length == 0 || steps.Length is 0 or > 12)
                {
                    throw new ModException("a command needs a phrase and between 1 and 12 steps");
                }

                commands.Add(new CommandSpec(phrase, steps));
            }
        }

        JsonElement? theme = e.TryGetProperty("theme", out var th) ? th.Clone() : null;
        if (kind == ModKind.Theme && theme is null)
        {
            throw new ModException("a theme mod needs a theme block");
        }

        var defs = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (e.TryGetProperty("defs", out var ds))
        {
            if (ds.ValueKind != JsonValueKind.Object)
            {
                throw new ModException("defs must be an object of name: formula");
            }

            foreach (var d in ds.EnumerateObject())
            {
                defs[d.Name] = d.Value.ValueKind == JsonValueKind.String ? d.Value.GetString()! : throw new ModException($"def '{d.Name}' must be a formula written as text");
            }
        }

        var manifest = new ModManifest(id, Str(e, "name"), e.TryGetProperty("version", out var ver) ? ver.GetString() ?? "0.1.0" : "0.1.0", kind, level, caps.ToImmutable(), placement, view, settings.ToImmutable(), rules.ToImmutable(), commands.ToImmutable(), theme, defs.ToImmutable());
        Script.Validate(manifest);
        return manifest;
    }

    /// <summary>
    /// Every piece of data the mod reads, as paths like <c>system.battery.percent</c>, so the approval
    /// card can list them and the store can check each is covered by a declared capability.
    /// </summary>
    public ImmutableArray<string> DataPaths() => Script.DataPaths(this);

    private static SettingField ParseSetting(string key, JsonElement s)
    {
        var type = (s.TryGetProperty("type", out var t) ? t.GetString() : null) switch
        {
            "color" => SettingType.Color,
            "boolean" => SettingType.Boolean,
            "number" => SettingType.Number,
            "text" => SettingType.Text,
            "choice" => SettingType.Choice,
            var other => throw new ModException($"setting '{key}' has unknown type '{other}'"),
        };
        var def = s.TryGetProperty("default", out var d) ? d.ToString() : "";
        var options = s.TryGetProperty("options", out var o) ? o.EnumerateArray().Select(x => x.GetString() ?? "").ToImmutableArray() : [];
        return new SettingField(key, type, def, s.TryGetProperty("label", out var l) ? l.GetString() ?? key : key,
            s.TryGetProperty("min", out var mn) ? mn.GetDouble() : null, s.TryGetProperty("max", out var mx) ? mx.GetDouble() : null, options);
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()!.Length > 0
            ? v.GetString()! : throw new ModException($"missing or empty '{name}'");
}
