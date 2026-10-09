using System.Collections.Immutable;
using System.Text.Json;

namespace ClaudeOS.Core.Planning;

public enum ModelRole { User, Assistant }

/// <summary>A piece of a model message, independent of any SDK. Thinking blocks are opaque: they
/// are carried back unchanged on the next turn, as the API requires.</summary>
public abstract record ContentPart;

public sealed record TextPart(string Text) : ContentPart;

public sealed record ToolUsePart(string Id, string Name, JsonElement Input) : ContentPart;

public sealed record ToolResultPart(string ToolUseId, string Content, bool IsError = false) : ContentPart;

public sealed record ThinkingPart(string Thinking, string Signature) : ContentPart;

public sealed record RedactedThinkingPart(string Data) : ContentPart;

public sealed record ModelMessage(ModelRole Role, ImmutableArray<ContentPart> Content)
{
    public static ModelMessage User(string text) => new(ModelRole.User, [new TextPart(text)]);
}

/// <summary>A tool the model may call. The schema is JSON Schema, kept as text so Core needs no SDK.</summary>
public sealed record ToolDefinition(string Name, string Description, string InputSchemaJson);

public enum Effort { Low, Medium, High, XHigh, Max }

public sealed record ModelRequest(
    string Model,
    string System,
    ImmutableArray<ToolDefinition> Tools,
    ImmutableArray<ModelMessage> Messages,
    Effort Effort,
    int MaxTokens);

public enum StopKind { EndTurn, ToolUse, MaxTokens, Refusal, Other }

public sealed record TokenUsage(long Input, long Output, long CacheRead = 0, long CacheWrite = 0)
{
    public static readonly TokenUsage None = new(0, 0);

    public long Total => Input + Output + CacheRead + CacheWrite;

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.Input + b.Input, a.Output + b.Output, a.CacheRead + b.CacheRead, a.CacheWrite + b.CacheWrite);
}

public sealed record ModelResponse(ImmutableArray<ContentPart> Content, StopKind Stop, TokenUsage Usage, string Model, string? RefusalCategory = null);

/// <summary>The model's tool input could not be parsed (a streaming or SDK problem, not the
/// model's fault). The loop re-issues the request instead of answering a tool id it never saw.</summary>
public sealed class ModelParseException(string message) : Exception(message);

/// <summary>
/// The one seam between the core and a language model. The official Anthropic C# SDK is
/// still in beta, so it lives behind this interface in <c>ClaudeOS.Claude</c>: an upgrade can only
/// break that project, and every test drives the planner with a scripted stand-in.
/// </summary>
public interface IModelClient
{
    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct = default);
}
