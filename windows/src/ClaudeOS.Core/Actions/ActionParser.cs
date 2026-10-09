using System.Collections.Immutable;
using System.Text.Json;

namespace ClaudeOS.Core.Actions;

/// <summary>Builds actions from untrusted JSON, rejecting anything malformed. Whatever the
/// model sends goes through here; nothing else can create an action from text.</summary>
public static class ActionParser
{
    private static readonly string[] KnownKinds = ["delete_file", "http_request", "move_file", "send_email", "write_file"];

    private static readonly Dictionary<string, (string[] Required, string[] Optional)> Shapes = new()
    {
        ["write_file"] = (["path", "content"], []),
        ["delete_file"] = (["path"], []),
        ["move_file"] = (["src", "dst"], []),
        ["send_email"] = (["to", "subject", "body"], []),
        ["http_request"] = (["method", "url"], ["body"]),
    };

    public static PlanAction Parse(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new ActionException($"action must be an object, got {KindName(payload.ValueKind)}");
        }

        var kind = payload.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : null;
        if (kind is null || !Shapes.TryGetValue(kind, out var shape))
        {
            var shown = payload.TryGetProperty("type", out var raw) && raw.ValueKind == JsonValueKind.String ? PyRepr.Of(raw.GetString()!) : "None";
            throw new ActionException($"unknown action type {shown}; expected one of {PyRepr.List(KnownKinds)}");
        }

        var declared = shape.Required.Concat(shape.Optional).ToHashSet(StringComparer.Ordinal);
        foreach (var name in shape.Required)
        {
            if (!payload.TryGetProperty(name, out _))
            {
                throw new ActionException($"{kind}: missing field {PyRepr.Of(name)}");
            }
        }

        // The planner's tool schema shares one object shape across action types, so empty
        // values in fields this type does not use are ignored.
        var unknown = payload.EnumerateObject()
            .Where(p => p.Name != "type" && !declared.Contains(p.Name) && !IsEmpty(p.Value))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (unknown.Count > 0)
        {
            throw new ActionException($"{kind}: unknown fields {PyRepr.List(unknown)}");
        }

        string Str(string name, string? fallback = null)
        {
            if (!payload.TryGetProperty(name, out var v))
            {
                return fallback ?? throw new ActionException($"{kind}: missing field {PyRepr.Of(name)}");
            }

            return v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new ActionException($"{kind}: field {PyRepr.Of(name)} must be a string");
        }

        switch (kind)
        {
            case "write_file":
                return new WriteFile(Str("path"), Str("content"));
            case "delete_file":
                return new DeleteFile(Str("path"));
            case "move_file":
                return new MoveFile(Str("src"), Str("dst"));
            case "send_email":
                {
                    var to = payload.GetProperty("to");
                    var ok = to.ValueKind == JsonValueKind.Array && to.GetArrayLength() > 0
                        && to.EnumerateArray().All(a => a.ValueKind == JsonValueKind.String && a.GetString()!.Contains('@'));
                    if (!ok)
                    {
                        throw new ActionException("send_email: 'to' must be a non-empty list of email addresses");
                    }

                    var addresses = to.EnumerateArray().Select(a => a.GetString()!).ToImmutableArray();
                    return new SendEmail(addresses, Str("subject"), Str("body"));
                }

            default:
                {
                    var url = Str("url");
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        throw new ActionException("http_request: url must be http(s)");
                    }

                    return new HttpRequest(Str("method").ToUpperInvariant(), url, Str("body", ""));
                }
        }
    }

    private static bool IsEmpty(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null => true,
        JsonValueKind.String => v.GetString()!.Length == 0,
        JsonValueKind.Array => v.GetArrayLength() == 0,
        _ => false,
    };

    private static string KindName(JsonValueKind k) => k switch
    {
        JsonValueKind.Array => "list",
        JsonValueKind.String => "str",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.Null => "NoneType",
        _ => k.ToString(),
    };
}
