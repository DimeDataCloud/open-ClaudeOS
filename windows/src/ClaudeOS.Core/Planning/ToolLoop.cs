using System.Collections.Immutable;

namespace ClaudeOS.Core.Planning;

public class PlannerException(string message) : Exception(message);

/// <summary>The model answered without proposing anything: a question, or a refusal to guess.</summary>
public sealed class NoPlanException(string message) : PlannerException(message);

/// <summary>What the loop shows the shell while it works, so the presence can say what is
/// really happening ("reading Q3 budget.csv") rather than animate a spinner.</summary>
public sealed record AgentEvent(string Kind, string Detail = "");

public sealed record ModelSettings(string Model = "claude-opus-5-5", Effort Effort = Effort.High, int MaxTokens = 16000);

public abstract record ToolStep<T>
{
    /// <summary>Answer the tool call and keep going.</summary>
    public sealed record Reply(string Content, bool IsError = false) : ToolStep<T>;

    /// <summary>The model reached its goal.</summary>
    public sealed record Finish(T Value) : ToolStep<T>;

    /// <summary>The model's submission was refused; tell it why so it can revise. Counts against the limit.</summary>
    public sealed record Reject(string Reason) : ToolStep<T>;
}

/// <summary>
/// The agent loop shared by the planner and the artifact maker: call the model, run its tool
/// calls through deterministic handlers, answer every tool call, and stop when a handler says
/// the work is finished. The model never touches the machine; handlers do, under policy.
/// </summary>
public sealed class ToolLoop(IModelClient client, ModelSettings settings, TokenLedger? ledger = null, string purpose = "agent", Action<AgentEvent>? onEvent = null)
{
    public int MaxTurns { get; init; } = 40;

    public int MaxRejections { get; init; } = 3;

    public async Task<T> RunAsync<T>(
        string system,
        ImmutableArray<ToolDefinition> tools,
        string firstMessage,
        Func<ToolUsePart, ToolStep<T>> handle,
        CancellationToken ct = default)
    {
        var messages = ImmutableArray.Create(ModelMessage.User(firstMessage));
        var rejections = 0;
        for (var turn = 0; turn < MaxTurns; turn++)
        {
            var response = await CallAsync(system, tools, messages, ct).ConfigureAwait(false);
            if (response.Stop == StopKind.Refusal)
            {
                throw new PlannerException($"the model declined this request (category: {response.RefusalCategory ?? "unspecified"})");
            }

            if (response.Stop == StopKind.MaxTokens)
            {
                throw new PlannerException("the model ran out of output tokens; try a narrower request");
            }

            messages = messages.Add(new ModelMessage(ModelRole.Assistant, response.Content));
            var uses = response.Content.OfType<ToolUsePart>().ToList();
            if (uses.Count == 0)
            {
                var text = string.Join("\n", response.Content.OfType<TextPart>().Select(t => t.Text)).Trim();
                throw new NoPlanException(text.Length > 0 ? text : "the model finished without proposing anything");
            }

            var results = ImmutableArray.CreateBuilder<ContentPart>();
            foreach (var use in uses)
            {
                switch (handle(use))
                {
                    case ToolStep<T>.Finish done:
                        return done.Value;
                    case ToolStep<T>.Reject rejected:
                        rejections++;
                        onEvent?.Invoke(new AgentEvent("plan_rejected", rejected.Reason));
                        if (rejections > MaxRejections)
                        {
                            throw new PlannerException($"still invalid after {MaxRejections} revisions: {rejected.Reason}");
                        }

                        results.Add(new ToolResultPart(use.Id, rejected.Reason, IsError: true));
                        break;
                    case ToolStep<T>.Reply reply:
                        results.Add(new ToolResultPart(use.Id, reply.Content, reply.IsError));
                        break;
                }
            }

            messages = messages.Add(new ModelMessage(ModelRole.User, results.ToImmutable()));
        }

        throw new PlannerException($"no result after {MaxTurns} turns");
    }

    private async Task<ModelResponse> CallAsync(string system, ImmutableArray<ToolDefinition> tools, ImmutableArray<ModelMessage> messages, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            ledger?.EnsureWithinBudget();
            onEvent?.Invoke(new AgentEvent("request"));
            try
            {
                var response = await client.CompleteAsync(new ModelRequest(settings.Model, system, tools, messages, settings.Effort, settings.MaxTokens), ct).ConfigureAwait(false);
                ledger?.Record(purpose, response.Model, response.Usage);
                return response;
            }
            catch (ModelParseException)
            {
                // The SDK could not parse streamed tool input; there is no tool_use id to answer, so re-issue.
                onEvent?.Invoke(new AgentEvent("retry", "unparseable tool input"));
            }
        }

        throw new PlannerException("the model produced unparseable tool input three times");
    }
}
