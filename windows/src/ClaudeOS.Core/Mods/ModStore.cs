using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Mods;

public sealed record CapabilityLine(string Capability, string Description, Risk Risk);

/// <summary>What the person sees before a mod is installed: what it is, what it can read in plain
/// words, a live preview, and the exact files that will be written.</summary>
public sealed record ModProposal(
    ModManifest Manifest,
    string ManifestJson,
    ImmutableArray<CapabilityLine> Capabilities,
    RenderedNode? Preview,
    ImmutableArray<string> DataPaths,
    Plan Plan,
    ImmutableArray<string> Formulas);

public enum ModStatus { Active, Disabled, NeedsReview, Invalid }

public sealed record InstalledMod(string Id, string Folder, ModManifest? Manifest, ModStatus Status, string? Problem);

/// <summary>
/// Mods are user-owned folders in <c>%APPDATA%\ClaudeOS\mods</c>. Installing one is an ordinary
/// plan of new files through the same approval card and undo journal as everything else. The
/// approval records exactly which capabilities were granted, bound to a digest of the manifest,
/// so editing a mod to ask for more makes it need review again.
/// </summary>
public sealed class ModStore(string modsRoot, CapabilityBroker broker)
{
    private const string ApprovedFile = ".approved.json";
    private const string DisabledFile = ".disabled";

    public string Root => modsRoot;

    /// <summary>Validate a manifest and prepare everything the approval card shows.</summary>
    public ModProposal Review(string manifestJson, ViewBinder? previewWith = null)
    {
        var manifest = ModManifest.Parse(manifestJson);
        if (manifest.Level == ModLevel.Web)
        {
            throw new ModException("web mods are not available yet; declarative mods (pure JSON) and scripted mods (JSON with formulas) are");
        }

        var lines = manifest.Capabilities.Select(c =>
        {
            Capabilities.TryDescribe(c, out var info, out var text);
            return new CapabilityLine(c, text, info.Risk);
        }).ToImmutableArray();

        var paths = manifest.DataPaths();
        var uncovered = paths.Where(p => !manifest.Capabilities.Any(c => CapabilityBroker.Covers(Capabilities.Known[Capabilities.Split(c).Id].DataPrefix, p))).ToList();
        if (uncovered.Count > 0)
        {
            throw new ModException($"the mod reads {string.Join(", ", uncovered)} but does not declare a capability for it");
        }

        RenderedNode? preview = null;
        if (previewWith is not null && manifest.View is not null)
        {
            var temporary = new CapabilityBroker();
            temporary.Grant(manifest.Id, manifest.Capabilities);
            preview = new ViewBinder(temporary, previewWith.Providers).Bind(manifest);
        }

        var plan = new Plan(
            $"Install the mod '{manifest.Name}'",
            "Adds one folder to your mods. Nothing outside it changes.",
            [new WriteFile($"{manifest.Id}/mod.json", manifestJson)]);
        return new ModProposal(manifest, manifestJson, lines, preview, paths, plan, Script.Describe(manifest));
    }

    /// <summary>Record the approval after the plan has been applied.</summary>
    public void RecordApproval(ModManifest manifest, string manifestJson)
    {
        var dir = Path.Combine(modsRoot, manifest.Id);
        Directory.CreateDirectory(dir);
        var record = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = manifest.Id,
            ["capabilities"] = Audit.Strs(manifest.Capabilities),
            ["manifest_sha256"] = Sha(manifestJson),
        };
        File.WriteAllText(Path.Combine(dir, ApprovedFile), record.ToJsonString());
        broker.Grant(manifest.Id, manifest.Capabilities);
    }

    public IReadOnlyList<InstalledMod> LoadAll()
    {
        var mods = new List<InstalledMod>();
        if (!Directory.Exists(modsRoot))
        {
            return mods;
        }

        foreach (var dir in Directory.EnumerateDirectories(modsRoot).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileName(dir);
            var file = Path.Combine(dir, "mod.json");
            if (!File.Exists(file))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(file);
                var manifest = ModManifest.Parse(json);
                if (manifest.Id != id)
                {
                    mods.Add(new InstalledMod(id, dir, manifest, ModStatus.Invalid, $"folder name '{id}' does not match the mod id '{manifest.Id}'"));
                    continue;
                }

                if (File.Exists(Path.Combine(dir, DisabledFile)))
                {
                    broker.Revoke(id);
                    mods.Add(new InstalledMod(id, dir, manifest, ModStatus.Disabled, null));
                    continue;
                }

                if (manifest.Capabilities.Length > 0 && !IsApproved(dir, json))
                {
                    broker.Revoke(id);
                    mods.Add(new InstalledMod(id, dir, manifest, ModStatus.NeedsReview, "this mod changed since you approved it"));
                    continue;
                }

                if (manifest.Capabilities.Length > 0)
                {
                    broker.Grant(id, manifest.Capabilities);
                }

                mods.Add(new InstalledMod(id, dir, manifest, ModStatus.Active, null));
            }
            catch (Exception e) when (e is ModException or IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                mods.Add(new InstalledMod(id, dir, null, ModStatus.Invalid, e.Message));
            }
        }

        return mods;
    }

    public void SetEnabled(string id, bool enabled)
    {
        var marker = Path.Combine(modsRoot, id, DisabledFile);
        if (enabled)
        {
            File.Delete(marker);
        }
        else
        {
            File.WriteAllText(marker, "");
            broker.Revoke(id);
        }
    }

    private static bool IsApproved(string dir, string manifestJson)
    {
        var path = Path.Combine(dir, ApprovedFile);
        if (!File.Exists(path))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.TryGetProperty("manifest_sha256", out var sha) && sha.GetString() == Sha(manifestJson);
    }

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>Theme mods become validated overrides; layout-rule mods adjust placement requests;
/// command mods add phrases the router understands before it asks any model.</summary>
public static class ModEffects
{
    public static ThemeOverrides ToThemeOverrides(ModManifest theme)
    {
        if (theme.Kind != ModKind.Theme || theme.Theme is not { } t)
        {
            throw new ModException("not a theme mod");
        }

        var colors = ImmutableDictionary.CreateBuilder<string, string>();
        if (t.TryGetProperty("colors", out var cs))
        {
            foreach (var c in cs.EnumerateObject())
            {
                colors[c.Name] = c.Value.GetString() ?? "";
            }
        }

        return new ThemeOverrides
        {
            Accent = t.TryGetProperty("accent", out var a) ? a.GetString() : null,
            RadiusScale = t.TryGetProperty("radiusScale", out var r) ? r.GetDouble() : 1,
            TypeScale = t.TryGetProperty("typeScale", out var ts) ? ts.GetDouble() : 1,
            FontUi = t.TryGetProperty("font", out var f) ? f.GetString() : null,
            Density = t.TryGetProperty("density", out var d) ? Enum.Parse<Density>(d.GetString() ?? "Comfortable", ignoreCase: true) : Density.Comfortable,
            Backdrop = t.TryGetProperty("backdrop", out var b) ? Enum.Parse<Backdrop>(b.GetString() ?? "Mica", ignoreCase: true) : Backdrop.Mica,
            Colors = colors.ToImmutable(),
        };
    }

    public static PlacementRequest ApplyRules(IEnumerable<ModManifest> mods, string contentKind, PlacementRequest request)
    {
        foreach (var rule in mods.Where(m => m.Kind == ModKind.LayoutRule).SelectMany(m => m.Rules).Where(r => string.Equals(r.ContentKind, contentKind, StringComparison.OrdinalIgnoreCase)))
        {
            var anchor = rule.Anchor switch
            {
                "top-left" => Anchor.TopLeft,
                "top-right" => Anchor.TopRight,
                "bottom-left" => Anchor.BottomLeft,
                "bottom-right" => Anchor.BottomRight,
                "left-half" => Anchor.LeftHalf,
                "right-half" => Anchor.RightHalf,
                "center" => Anchor.Center,
                _ => Anchor.Auto,
            };
            request = request with { Anchor = anchor };
            if (rule.Width is { } w && rule.Height is { } h)
            {
                request = request with { Desired = new Size(w, h) };
            }
        }

        return request;
    }

    public static CommandSpec? MatchCommand(IEnumerable<ModManifest> mods, string text)
    {
        var phrase = text.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
        return mods.Where(m => m.Kind == ModKind.Command).SelectMany(m => m.Commands).FirstOrDefault(c => c.Phrase == phrase);
    }
}
