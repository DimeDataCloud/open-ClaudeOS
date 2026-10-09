using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudeOS.Core.Actions;

/// <summary>Where an action's effects land. This, not the model's description, decides how
/// an action is staged, reviewed and executed.</summary>
public enum Effect
{
    /// <summary>Changes files on this machine; staged, previewed and undoable.</summary>
    Local,

    /// <summary>Leaves this machine; cannot be undone once executed.</summary>
    External,
}

public sealed class ActionException(string message) : Exception(message);

/// <summary>
/// A typed action: the only things a plan can ask the system to do. Each action has a
/// canonical form whose SHA-256 is its identity; a consent grant is a set of those digests.
/// The canonical form is byte-identical to the Python prototype's, so a plan file has the
/// same digest in both implementations.
/// </summary>
public abstract record PlanAction
{
    public abstract string Kind { get; }

    public abstract Effect Effect { get; }

    public abstract string Describe();

    /// <summary>Fields in wire form (strings, or a string list for recipients).</summary>
    protected internal abstract IEnumerable<KeyValuePair<string, object>> Fields();

    public SortedDictionary<string, object> ToFields()
    {
        var fields = new SortedDictionary<string, object>(StringComparer.Ordinal) { ["type"] = Kind };
        foreach (var (key, value) in Fields())
        {
            fields[key] = value;
        }

        return fields;
    }

    public string Digest() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson.Serialize(ToFields()))));

    public void WriteJson(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in ToFields())
        {
            if (value is ImmutableArray<string> list)
            {
                writer.WriteStartArray(key);
                foreach (var item in list)
                {
                    writer.WriteStringValue(item);
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WriteString(key, (string)value);
            }
        }

        writer.WriteEndObject();
    }
}

public sealed record WriteFile(string Path, string Content) : PlanAction
{
    public override string Kind => "write_file";
    public override Effect Effect => Effect.Local;
    public override string Describe() => $"write {Path} ({Encoding.UTF8.GetByteCount(Content)} bytes)";

    protected internal override IEnumerable<KeyValuePair<string, object>> Fields()
    {
        yield return new("path", Path);
        yield return new("content", Content);
    }
}

public sealed record DeleteFile(string Path) : PlanAction
{
    public override string Kind => "delete_file";
    public override Effect Effect => Effect.Local;
    public override string Describe() => $"delete {Path}";

    protected internal override IEnumerable<KeyValuePair<string, object>> Fields()
    {
        yield return new("path", Path);
    }
}

public sealed record MoveFile(string Src, string Dst) : PlanAction
{
    public override string Kind => "move_file";
    public override Effect Effect => Effect.Local;
    public override string Describe() => $"move {Src} -> {Dst}";

    protected internal override IEnumerable<KeyValuePair<string, object>> Fields()
    {
        yield return new("src", Src);
        yield return new("dst", Dst);
    }
}

public sealed record SendEmail(ImmutableArray<string> To, string Subject, string Body) : PlanAction
{
    public override string Kind => "send_email";
    public override Effect Effect => Effect.External;
    public override string Describe() => $"send email to {string.Join(", ", To)}: {PyRepr.Of(Subject)}";

    /// <summary>The lower-cased domain of every recipient.</summary>
    public IReadOnlySet<string> Domains() =>
        To.Select(a => a[(a.LastIndexOf('@') + 1)..].ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

    protected internal override IEnumerable<KeyValuePair<string, object>> Fields()
    {
        yield return new("to", To);
        yield return new("subject", Subject);
        yield return new("body", Body);
    }
}

public sealed record HttpRequest(string Method, string Url, string Body = "") : PlanAction
{
    public override string Kind => "http_request";
    public override Effect Effect => Effect.External;
    public override string Describe() => $"{Method} {Url}";

    public string Host() => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : "";

    protected internal override IEnumerable<KeyValuePair<string, object>> Fields()
    {
        yield return new("method", Method);
        yield return new("url", Url);
        yield return new("body", Body);
    }
}

/// <summary>Python-style <c>repr</c> for strings, so approval cards read the same everywhere.</summary>
internal static class PyRepr
{
    public static string Of(string value)
    {
        var quote = value.Contains('\'') && !value.Contains('"') ? '"' : '\'';
        var sb = new StringBuilder().Append(quote);
        foreach (var c in value)
        {
            if (c == quote || c == '\\')
            {
                sb.Append('\\').Append(c);
            }
            else if (c == '\n')
            {
                sb.Append("\\n");
            }
            else if (c == '\r')
            {
                sb.Append("\\r");
            }
            else if (c == '\t')
            {
                sb.Append("\\t");
            }
            else if (c < 0x20 || c == 0x7f)
            {
                sb.Append("\\x").Append(((int)c).ToString("x2"));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.Append(quote).ToString();
    }

    public static string List(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(Of)) + "]";
}

/// <summary>JSON with sorted keys, no whitespace, and every non-ASCII character escaped: the
/// same bytes Python's <c>json.dumps(sort_keys=True, separators=(",", ":"))</c> produces.</summary>
internal static class CanonicalJson
{
    public static string Serialize(SortedDictionary<string, object> fields)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var (key, value) in fields)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            AppendString(sb, key);
            sb.Append(':');
            if (value is ImmutableArray<string> list)
            {
                sb.Append('[');
                for (var i = 0; i < list.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    AppendString(sb, list[i]);
                }

                sb.Append(']');
            }
            else
            {
                AppendString(sb, (string)value);
            }
        }

        return sb.Append('}').ToString();
    }

    private static void AppendString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20 || c > 0x7e)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
