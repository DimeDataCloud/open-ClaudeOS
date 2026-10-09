using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudeOS.Core.Actions;

/// <summary>
/// A plan: the intent, the model's own summary, and the exact actions proposed. The digest
/// covers the actions only: consent is given to what will happen, not to how the model
/// chose to describe it.
/// </summary>
public sealed record Plan(string Intent, string Summary, ImmutableArray<PlanAction> Actions, ImmutableArray<string> Reads = default)
{
    public ImmutableArray<string> Reads { get; init; } = Reads.IsDefault ? [] : Reads;

    public string Digest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var action in Actions)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(action.Digest()));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public string ToJson(bool indented = false)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("intent", Intent);
            w.WriteString("summary", Summary);
            w.WriteStartArray("actions");
            foreach (var a in Actions)
            {
                a.WriteJson(w);
            }

            w.WriteEndArray();
            w.WriteStartArray("reads");
            foreach (var r in Reads)
            {
                w.WriteStringValue(r);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static Plan Parse(JsonElement payload, string intent = "")
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new ActionException("plan must be an object");
        }

        if (!payload.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array || actions.GetArrayLength() == 0)
        {
            throw new ActionException("plan must contain a non-empty 'actions' list");
        }

        var parsed = ImmutableArray.CreateBuilder<PlanAction>();
        var i = 0;
        foreach (var raw in actions.EnumerateArray())
        {
            try
            {
                parsed.Add(ActionParser.Parse(raw));
            }
            catch (ActionException e)
            {
                throw new ActionException($"actions[{i}]: {e.Message}");
            }

            i++;
        }

        var summary = "";
        if (payload.TryGetProperty("summary", out var s))
        {
            summary = s.ValueKind == JsonValueKind.String ? s.GetString()! : throw new ActionException("plan 'summary' must be a string");
        }

        if (payload.TryGetProperty("intent", out var it) && it.ValueKind == JsonValueKind.String && it.GetString()!.Length > 0)
        {
            intent = it.GetString()!;
        }

        return new Plan(intent, summary, parsed.ToImmutable());
    }

    /// <summary>Builds a plan from a model's <c>submit_plan</c> arguments. Only <c>summary</c> and
    /// <c>actions</c> are read: the model cannot rewrite the person's intent.</summary>
    public static Plan FromToolInput(JsonElement input, string intent)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            if (input.TryGetProperty("summary", out var summary))
            {
                w.WritePropertyName("summary");
                summary.WriteTo(w);
            }

            if (input.TryGetProperty("actions", out var actions))
            {
                w.WritePropertyName("actions");
                actions.WriteTo(w);
            }

            w.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return Parse(doc.RootElement, intent);
    }

    public static Plan Parse(string json, string intent = "")
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement, intent);
        }
        catch (JsonException e)
        {
            throw new ActionException($"plan is not valid JSON: {e.Message}");
        }
    }

    public static Plan Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));
}
