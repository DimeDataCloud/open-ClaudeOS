using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeOS.Core.Layout;

/// <summary>
/// Turns an accepted habit suggestion into an ordinary layout-rule mod. Nothing is learned
/// silently: the tracker only offers, and accepting goes through the same approval and undo as
/// every other mod, so the person can read, edit or delete the rule as a file.
/// </summary>
public static partial class HabitRules
{
    public static string AnchorOf(Region region) => region switch
    {
        Region.LeftHalf => "left-half",
        Region.RightHalf => "right-half",
        Region.TopLeft => "top-left",
        Region.TopRight => "top-right",
        Region.BottomLeft => "bottom-left",
        Region.BottomRight => "bottom-right",
        _ => "center",
    };

    /// <summary>The mod folder name, which is also its id: stable, so accepting twice replaces nothing.</summary>
    public static string IdOf(Suggestion suggestion) =>
        $"open-{NonWordRx().Replace(suggestion.ContentKind.ToLowerInvariant(), "-").Trim('-')}-{AnchorOf(suggestion.Region)}";

    public static string ToManifestJson(Suggestion suggestion)
    {
        var anchor = AnchorOf(suggestion.Region);
        var name = suggestion.Message.TrimEnd('?').Replace(" from now on", "", StringComparison.Ordinal);
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("id", IdOf(suggestion));
            w.WriteString("name", name);
            w.WriteString("version", "1.0.0");
            w.WriteString("kind", "layout-rule");
            w.WriteString("level", "declarative");
            w.WriteString("description", "Created from a habit you accepted. Delete this folder to stop.");
            w.WriteStartArray("rules");
            w.WriteStartObject();
            w.WriteStartObject("match");
            w.WriteString("kind", suggestion.ContentKind);
            w.WriteEndObject();
            w.WriteStartObject("place");
            w.WriteString("anchor", anchor);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWordRx();
}
