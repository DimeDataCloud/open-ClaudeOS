using System.Collections.Immutable;
using System.Text.Json;
using ClaudeOS.Core.Planning;

namespace ClaudeOS.Core.Tests.Planning;

/// <summary>Replays canned responses and records every request, so the planner can be tested
/// with no network and no API key.</summary>
public sealed class ScriptedModel(params object[] script) : IModelClient
{
    private readonly Queue<object> _script = new(script);

    public List<ModelRequest> Requests { get; } = [];

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        return _script.Dequeue() switch
        {
            Exception e => Task.FromException<ModelResponse>(e),
            ModelResponse r => Task.FromResult(r),
            var other => throw new InvalidOperationException($"bad script entry {other}"),
        };
    }

    public static ToolUsePart Use(string id, string name, string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new ToolUsePart(id, name, doc.RootElement.Clone());
    }

    public static ModelResponse Reply(StopKind stop = StopKind.ToolUse, string? refusal = null, params ContentPart[] content) =>
        new([.. content], stop, new TokenUsage(1200, 80, 900, 0), "claude-opus-5-5", refusal);

    public static ModelResponse Calls(params ContentPart[] content) => Reply(StopKind.ToolUse, null, content);

    public static IReadOnlyDictionary<string, ToolResultPart> Results(ModelRequest request) =>
        request.Messages[^1].Content.OfType<ToolResultPart>().ToDictionary(r => r.ToolUseId);
}
