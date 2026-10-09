using System.Collections.Immutable;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using ClaudeOS.Core.Planning;
using CoreEffort = ClaudeOS.Core.Planning.Effort;

namespace ClaudeOS.Claude;

/// <summary>
/// <see cref="IModelClient"/> on the official Anthropic C# SDK (NuGet <c>Anthropic</c>, still in
/// beta, so the version is pinned). This is the only place SDK types appear; if an upgrade
/// changes them, only this file changes.
/// </summary>
public sealed class AnthropicModelClient : IModelClient
{
    private readonly AnthropicClient _client;

    public AnthropicModelClient(AnthropicClient? client = null) => _client = client ?? new AnthropicClient();

    /// <summary>A client with an explicit key (the shell keeps it encrypted with DPAPI); with
    /// none, the SDK reads <c>ANTHROPIC_API_KEY</c> or the active <c>ant auth login</c> profile.</summary>
    public static AnthropicModelClient WithKey(string? apiKey) =>
        new(string.IsNullOrWhiteSpace(apiKey) ? new AnthropicClient() : new AnthropicClient { ApiKey = apiKey });

    public async Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct = default)
    {
        var parameters = new MessageCreateParams
        {
            Model = request.Model,
            MaxTokens = request.MaxTokens,
            System = request.System,
            Messages = [.. request.Messages.Select(ToParam)],
            Tools = [.. request.Tools.Select(ToTool)],
            OutputConfig = new OutputConfig { Effort = ToEffort(request.Effort) },
            // The system prompt and tool definitions are identical between requests; cache them.
            CacheControl = new CacheControlEphemeral(),
        };

        Message response;
        try
        {
            response = await _client.Messages.Create(parameters, ct).ConfigureAwait(false);
        }
        catch (AnthropicUnauthorizedException)
        {
            throw new PlannerException("Claude rejected the API key. Set ANTHROPIC_API_KEY, or run `ant auth login`.");
        }
        catch (AnthropicRateLimitException)
        {
            throw new PlannerException("Claude is rate limiting this key; try again in a minute.");
        }
        catch (AnthropicIOException e)
        {
            throw new PlannerException($"cannot reach Claude: {e.InnerException?.Message ?? e.Message}");
        }
        catch (AnthropicApiException e)
        {
            throw new PlannerException($"Claude returned an error: {e.Message}");
        }
        catch (HttpRequestException e)
        {
            throw new PlannerException($"cannot reach Claude: {e.Message}");
        }

        return FromMessage(response);
    }

    /// <summary>The tool input as a JSON object, written by hand so no reflection-based serializer is needed (Native AOT).</summary>
    private static JsonElement InputElement(IReadOnlyDictionary<string, JsonElement> input)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in input)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static ModelResponse FromMessage(Message response)
    {
        var parts = ImmutableArray.CreateBuilder<ContentPart>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                parts.Add(new TextPart(text.Text));
            }
            else if (block.TryPickThinking(out ThinkingBlock? thinking))
            {
                parts.Add(new ThinkingPart(thinking.Thinking, thinking.Signature));
            }
            else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
            {
                parts.Add(new RedactedThinkingPart(redacted.Data));
            }
            else if (block.TryPickToolUse(out ToolUseBlock? use))
            {
                try
                {
                    parts.Add(new ToolUsePart(use.ID, use.Name, InputElement(use.Input)));
                }
                catch (JsonException e)
                {
                    throw new ModelParseException(e.Message);
                }
            }
        }

        var reason = response.StopReason?.Raw() ?? "";
        var stop = reason switch
        {
            "end_turn" or "stop_sequence" => StopKind.EndTurn,
            "tool_use" => StopKind.ToolUse,
            "max_tokens" => StopKind.MaxTokens,
            "refusal" => StopKind.Refusal,
            _ => StopKind.Other,
        };

        var usage = new TokenUsage(
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.CacheReadInputTokens ?? 0,
            response.Usage.CacheCreationInputTokens ?? 0);
        return new ModelResponse(parts.ToImmutable(), stop, usage, response.Model.ToString(), response.StopDetails?.Category?.Raw());
    }

    private static MessageParam ToParam(ModelMessage message)
    {
        var content = new List<ContentBlockParam>();
        foreach (var part in message.Content)
        {
            switch (part)
            {
                case TextPart t:
                    content.Add(new TextBlockParam { Text = t.Text });
                    break;
                case ThinkingPart th:
                    // The signature must be preserved; the API rejects tampering.
                    content.Add(new ThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
                    break;
                case RedactedThinkingPart r:
                    content.Add(new RedactedThinkingBlockParam { Data = r.Data });
                    break;
                case ToolUsePart u:
                    content.Add(new ToolUseBlockParam
                    {
                        ID = u.Id,
                        Name = u.Name,
                        Input = u.Input.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
                    });
                    break;
                case ToolResultPart r:
                    content.Add(new ToolResultBlockParam { ToolUseID = r.ToolUseId, Content = r.Content, IsError = r.IsError ? true : null });
                    break;
            }
        }

        return new MessageParam { Role = message.Role == ModelRole.User ? Role.User : Role.Assistant, Content = content };
    }

    private static Tool ToTool(ToolDefinition definition)
    {
        using var schema = JsonDocument.Parse(definition.InputSchemaJson);
        var root = schema.RootElement;
        var properties = root.TryGetProperty("properties", out var props)
            ? props.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
            : [];
        var required = root.TryGetProperty("required", out var req) ? req.EnumerateArray().Select(r => r.GetString()!).ToList() : [];
        return new Tool
        {
            Name = definition.Name,
            Description = definition.Description,
            InputSchema = new() { Properties = properties, Required = required },
        };
    }

    private static Anthropic.Models.Messages.Effort ToEffort(CoreEffort effort) => effort switch
    {
        CoreEffort.Low => Anthropic.Models.Messages.Effort.Low,
        CoreEffort.Medium => Anthropic.Models.Messages.Effort.Medium,
        CoreEffort.High => Anthropic.Models.Messages.Effort.High,
        CoreEffort.XHigh => Anthropic.Models.Messages.Effort.Xhigh,
        _ => Anthropic.Models.Messages.Effort.Max,
    };
}
