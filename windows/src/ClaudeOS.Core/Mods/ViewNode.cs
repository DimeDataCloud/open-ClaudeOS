using System.Collections.Immutable;
using System.Text.Json;

namespace ClaudeOS.Core.Mods;

public enum ViewKind { Stack, Row, Metric, Text, Gauge, Spark, List, Button, Spacer }

/// <summary>
/// One element of a declarative view. Text fields may contain <c>{path}</c> placeholders bound to
/// data the mod has permission for. Nodes are native: the shell draws them with its own controls,
/// so a mod's widget looks and animates like the rest of the system and follows the theme.
/// </summary>
public sealed record ViewNode(
    ViewKind Kind,
    string? Primary = null,
    string? Secondary = null,
    string? Value = null,
    string? Max = null,
    ImmutableArray<ViewNode> Children = default)
{
    public const int MaxDepth = 6;
    public const int MaxNodes = 60;

    public ImmutableArray<ViewNode> Children { get; init; } = Children.IsDefault ? [] : Children;

    private static readonly Dictionary<string, ViewKind> Keys = new()
    {
        ["stack"] = ViewKind.Stack, ["row"] = ViewKind.Row, ["metric"] = ViewKind.Metric, ["text"] = ViewKind.Text,
        ["gauge"] = ViewKind.Gauge, ["spark"] = ViewKind.Spark, ["list"] = ViewKind.List, ["button"] = ViewKind.Button, ["spacer"] = ViewKind.Spacer,
    };

    public static ViewNode Parse(JsonElement e)
    {
        var count = new int[1];
        return Parse(e, 0, count);
    }

    private static ViewNode Parse(JsonElement e, int depth, int[] count)
    {
        if (++count[0] > MaxNodes)
        {
            throw new ModException($"a view may have at most {MaxNodes} elements");
        }

        if (depth > MaxDepth)
        {
            throw new ModException($"a view may be nested at most {MaxDepth} levels deep");
        }

        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new ModException("every view element must be an object");
        }

        var kinds = e.EnumerateObject().Where(p => Keys.ContainsKey(p.Name)).ToList();
        if (kinds.Count != 1)
        {
            throw new ModException($"each view element needs exactly one of: {string.Join(", ", Keys.Keys)}");
        }

        var kind = Keys[kinds[0].Name];
        var primary = kinds[0].Value;
        var extras = e.EnumerateObject().Where(p => !Keys.ContainsKey(p.Name)).ToDictionary(p => p.Name, p => p.Value);

        string? Get(string key) => extras.TryGetValue(key, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;

        var allowedExtras = kind switch
        {
            ViewKind.Metric => new[] { "label" },
            ViewKind.Text => ["subtext"],
            ViewKind.Gauge => ["label", "max"],
            ViewKind.Spark => ["label"],
            ViewKind.Button => ["action"],
            ViewKind.List => ["template", "limit"],
            _ => Array.Empty<string>(),
        };
        var stray = extras.Keys.Except(allowedExtras).Order().ToList();
        if (stray.Count > 0)
        {
            throw new ModException($"{kinds[0].Name} does not take: {string.Join(", ", stray)}");
        }

        switch (kind)
        {
            case ViewKind.Stack or ViewKind.Row:
                if (primary.ValueKind != JsonValueKind.Array)
                {
                    throw new ModException($"{kinds[0].Name} must be a list of elements");
                }

                return new ViewNode(kind) { Children = [.. primary.EnumerateArray().Select(c => Parse(c, depth + 1, count))] };
            case ViewKind.Spacer:
                return new ViewNode(kind);
            default:
                return new ViewNode(kind, primary.ValueKind == JsonValueKind.String ? primary.GetString() : primary.ToString(), Get("label") ?? Get("subtext") ?? Get("action") ?? Get("template"), Get("value"), Get("max"));
        }
    }

    /// <summary>Every piece of text in the view, so placeholders can be found wherever they are.</summary>
    internal IEnumerable<string> Texts()
    {
        foreach (var text in new[] { Primary, Secondary, Value, Max })
        {
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }

        foreach (var c in Children)
        {
            foreach (var t in c.Texts())
            {
                yield return t;
            }
        }
    }

    /// <summary>Every <c>{path}</c> the view reads, so the approval card can show what data it touches.</summary>
    public IEnumerable<string> Paths()
    {
        foreach (var text in new[] { Primary, Secondary, Value, Max })
        {
            foreach (var p in Placeholders.Find(text))
            {
                yield return p;
            }
        }

        foreach (var c in Children)
        {
            foreach (var p in c.Paths())
            {
                yield return p;
            }
        }
    }
}

internal static class Placeholders
{
    /// <summary>
    /// The text between each pair of braces, in order. A formula may contain braces inside a quoted
    /// string, so when <paramref name="quoteAware"/> is set a closing brace inside quotes does not end it.
    /// </summary>
    public static IEnumerable<(int Start, int End, string Body)> Spans(string text, bool quoteAware)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{')
            {
                continue;
            }

            var j = i + 1;
            var quote = '\0';
            while (j < text.Length)
            {
                var c = text[j];
                if (quote != '\0')
                {
                    if (c == '\\')
                    {
                        j++;
                    }
                    else if (c == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (quoteAware && c is '"' or '\'')
                {
                    quote = c;
                }
                else if (c == '}')
                {
                    break;
                }

                j++;
            }

            if (j >= text.Length)
            {
                yield break;
            }

            yield return (i, j, text[(i + 1)..j].Trim());
            i = j;
        }
    }

    public static IEnumerable<string> Find(string? text)
    {
        if (text is null)
        {
            yield break;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{')
            {
                continue;
            }

            var close = text.IndexOf('}', i + 1);
            if (close < 0)
            {
                yield break;
            }

            yield return text[(i + 1)..close].Trim();
            i = close;
        }
    }
}
