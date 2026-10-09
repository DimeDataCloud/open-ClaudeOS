using System.Globalization;
using System.Text.Json.Nodes;
using ClaudeOS.Core.Actions;

namespace ClaudeOS.Core.Safety;

/// <summary>Everything that leaves the machine goes through a connector, so it can be shown in
/// full, approved and logged. Credentials live in the connector, never with the model.</summary>
public interface IConnector
{
    Task<string> SendEmailAsync(SendEmail action, CancellationToken ct = default);

    Task<string> HttpRequestAsync(HttpRequest action, CancellationToken ct = default);
}

/// <summary>Records external actions instead of performing them. Real connectors (mail, HTTP,
/// MCP servers) plug in behind the same interface once the consent path is trusted.</summary>
public sealed class OutboxConnector(string outbox, TimeProvider? clock = null) : IConnector
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public Task<string> SendEmailAsync(SendEmail action, CancellationToken ct = default) => Queue(action);

    public Task<string> HttpRequestAsync(HttpRequest action, CancellationToken ct = default) => Queue(action);

    private Task<string> Queue(PlanAction action)
    {
        var stamp = _clock.GetUtcNow().ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(outbox, $"{stamp}-{action.Digest()[..8]}.json");
        using var stream = File.Create(path);
        using (var w = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            action.WriteJson(w);
        }

        return Task.FromResult($"dry run: written to {path}, nothing was sent");
    }
}

public sealed record ExecutionResult(string CommitId, string? Manifest, IReadOnlyList<(string Description, string Outcome)> External);

/// <summary>
/// Runs an approved plan, and nothing else. Local changes were already staged in the overlay
/// during preview; executing commits them (with an undo journal) and then hands external
/// actions to connectors. Every action is checked against the grant first.
/// </summary>
public static class Executor
{
    public static async Task<ExecutionResult> ExecuteAsync(
        Plan plan, Grant grant, Overlay overlay, IConnector connector, StateDir state, TimeProvider? clock = null, CancellationToken ct = default)
    {
        clock ??= TimeProvider.System;
        grant.Check(plan);
        var id = $"{clock.GetUtcNow().ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}-{plan.Digest()[..8]}";
        for (var n = 2; Directory.Exists(Path.Combine(state.Journal, id)); n++)
        {
            id = $"{id.Split('~')[0]}~{n}";
        }

        string? manifest = overlay.Changes().Count > 0 ? overlay.Commit(state.Journal, id) : null;
        state.Log("committed", Audit.Of(("commit_id", Audit.Str(id)), ("manifest", Audit.Str(manifest))));

        var external = new List<(string, string)>();
        foreach (var action in plan.Actions.Where(a => a.Effect == Effect.External))
        {
            grant.Require(action);
            var outcome = action switch
            {
                SendEmail e => await connector.SendEmailAsync(e, ct).ConfigureAwait(false),
                HttpRequest h => await connector.HttpRequestAsync(h, ct).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"no connector for {action.Kind}"),
            };
            state.Log("external", Audit.Of(("action", Audit.Parse(ToJson(action))), ("outcome", Audit.Str(outcome))));
            external.Add((action.Describe(), outcome));
        }

        return new ExecutionResult(id, manifest, external);
    }

    internal static string ToJson(PlanAction action)
    {
        using var stream = new MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(stream))
        {
            action.WriteJson(w);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>One pass of the loop: preview a plan, ask once, run exactly what was approved.</summary>
public static class Session
{
    public enum Status { Refused, Declined, Applied }

    public sealed record Outcome(Status Status, ApprovalCard Card, IReadOnlyList<Assessment> Assessments, ExecutionResult? Result = null);

    public static async Task<Outcome> ReviewAndApplyAsync(
        Plan plan,
        Overlay overlay,
        Policy policy,
        Func<ApprovalCard, CancellationToken, Task<bool>> approve,
        IConnector connector,
        StateDir state,
        TimeProvider? clock = null,
        CancellationToken ct = default)
    {
        var assessments = policy.Preview(plan, overlay);
        var card = ApprovalCard.Build(plan, assessments, overlay.Diff());
        state.Log("proposed", Audit.Of(("plan", Audit.Parse(plan.ToJson())), ("digest", Audit.Str(plan.Digest()))));

        if (card.IsRefused)
        {
            state.Log("refused", Audit.Of(("digest", Audit.Str(plan.Digest())), ("reasons", Audit.Strs(assessments.Where(a => a.Denied is not null).Select(a => a.Denied!)))));
            return new Outcome(Status.Refused, card, assessments);
        }

        if (!await approve(card, ct).ConfigureAwait(false))
        {
            state.Log("declined", Audit.Of(("digest", Audit.Str(plan.Digest()))));
            return new Outcome(Status.Declined, card, assessments);
        }

        var grant = Grant.ForPlan(plan, clock: clock);
        state.Log("approved", Audit.Of(("digest", Audit.Str(plan.Digest()))));
        try
        {
            var result = await Executor.ExecuteAsync(plan, grant, overlay, connector, state, clock, ct).ConfigureAwait(false);
            return new Outcome(Status.Applied, card, assessments, result);
        }
        catch (Exception e)
        {
            state.Log("failed", Audit.Of(("digest", Audit.Str(plan.Digest())), ("error", Audit.Str(e.Message))));
            throw;
        }
    }
}

/// <summary>
/// "Making something new produces new files only, so it applies straight away with Undo available."
/// This is that rule, enforced: the plan is applied without an approval card only if every action
/// creates a file that does not exist yet. Anything else is refused here and must go through
/// <see cref="Session.ReviewAndApplyAsync"/>.
/// </summary>
public static class Creations
{
    public static async Task<ExecutionResult> ApplyAsync(Plan plan, Overlay overlay, Policy policy, StateDir state, TimeProvider? clock = null, CancellationToken ct = default)
    {
        var assessments = policy.Preview(plan, overlay);
        var denied = assessments.FirstOrDefault(a => a.Denied is not null);
        if (denied is not null)
        {
            throw new PolicyDeniedException(denied.Denied!);
        }

        var notNew = assessments.FirstOrDefault(a => a.Action is not ClaudeOS.Core.Actions.WriteFile || a.Risk != Risk.Low);
        if (notNew is not null)
        {
            throw new PolicyDeniedException($"'{notNew.Action.Describe()}' is not a new file, so it needs your approval first");
        }

        state.Log("created", Audit.Of(("digest", Audit.Str(plan.Digest())), ("files", Audit.Strs(plan.Actions.Select(a => a.Describe())))));
        return await Executor.ExecuteAsync(plan, Grant.ForPlan(plan, clock: clock), overlay, new OutboxConnector(state.Outbox, clock), state, clock, ct).ConfigureAwait(false);
    }
}
