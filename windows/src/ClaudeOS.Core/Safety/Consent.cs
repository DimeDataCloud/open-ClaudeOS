using System.Collections.Immutable;
using System.Text;
using ClaudeOS.Core.Actions;

namespace ClaudeOS.Core.Safety;

public sealed class GrantException(string message) : Exception(message);

/// <summary>A capability for one plan: exactly these action digests, until it expires.</summary>
public sealed record Grant(string PlanDigest, ImmutableHashSet<string> ActionDigests, DateTimeOffset ExpiresAt, TimeProvider Clock)
{
    public static Grant ForPlan(Plan plan, TimeSpan? ttl = null, TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new Grant(plan.Digest(), [.. plan.Actions.Select(a => a.Digest())], clock.GetUtcNow() + (ttl ?? TimeSpan.FromMinutes(5)), clock);
    }

    public void Check(Plan plan)
    {
        if (Clock.GetUtcNow() > ExpiresAt)
        {
            throw new GrantException("approval expired; review the plan again");
        }

        if (plan.Digest() != PlanDigest)
        {
            throw new GrantException("plan differs from the one that was approved");
        }

        foreach (var action in plan.Actions)
        {
            Require(action);
        }
    }

    public void Require(PlanAction action)
    {
        if (!ActionDigests.Contains(action.Digest()))
        {
            throw new GrantException($"action was not approved: {action.Describe()}");
        }
    }
}

/// <summary>One row of the approval card, rendered from a typed action.</summary>
public sealed record CardItem(
    int Number,
    PlanAction Action,
    Effect Effect,
    string Description,
    Risk Risk,
    ImmutableArray<string> Notes,
    string? Denied,
    ImmutableArray<string> ExternalContent);

/// <summary>
/// The approval card: generated from the typed actions and the policy's notes, never from
/// model prose, so what the person approves is what will run. The model's own summary is
/// included, but labelled as the model's claim. The shell binds to this model; the CLI
/// prints <see cref="ToText"/>.
/// </summary>
public sealed record ApprovalCard(
    string Intent,
    string ModelSummary,
    string PlanDigest,
    ImmutableArray<CardItem> Items,
    ImmutableArray<string> Reads,
    string Diff,
    Risk Risk)
{
    public int DeniedCount => Items.Count(i => i.Denied is not null);

    public bool IsRefused => DeniedCount > 0;

    public static ApprovalCard Build(Plan plan, IReadOnlyList<Assessment> assessments, string diff)
    {
        var items = assessments.Select((a, i) => new CardItem(
            i + 1,
            a.Action,
            a.Action.Effect,
            a.Action.Describe(),
            a.Risk,
            a.Notes,
            a.Denied,
            a.Action.Effect == Effect.External ? [.. ExternalBody(a.Action)] : [])).ToImmutableArray();
        var risk = assessments.Count == 0 ? Risk.Low : assessments.Max(a => a.Risk);
        return new ApprovalCard(plan.Intent, plan.Summary, plan.Digest(), items, plan.Reads, diff, risk);
    }

    private static IEnumerable<string> ExternalBody(PlanAction action)
    {
        var body = action switch { SendEmail e => e.Body, HttpRequest h => h.Body, _ => "" };
        return UnifiedDiff.SplitLines(body).Select(l => l.TrimEnd('\r', '\n'));
    }

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.Append(Intent.Length > 0 ? $"Intent:  {Intent}" : "Intent:  (none given)").Append('\n');
        sb.Append($"Model's summary (its own words): {(ModelSummary.Length > 0 ? ModelSummary : "(none)")}\n\n");
        sb.Append($"What will actually happen ({Items.Length} actions, plan {PlanDigest[..12]}):\n");
        foreach (var item in Items)
        {
            var tag = item.Effect == Effect.Local ? "LOCAL   " : "EXTERNAL";
            sb.Append($"  {item.Number,2}. {tag} {item.Description}\n");
            foreach (var note in item.Notes)
            {
                sb.Append($"            - {note}\n");
            }

            if (item.Denied is not null)
            {
                sb.Append($"            x DENIED: {item.Denied}\n");
            }

            foreach (var line in item.ExternalContent)
            {
                sb.Append($"            | {line}\n");
            }
        }

        if (Reads.Length > 0)
        {
            sb.Append($"\nFiles read while planning ({Reads.Length}): {string.Join(", ", Reads)}\n");
        }

        if (Diff.Length > 0)
        {
            sb.Append("\nStaged file changes (nothing is written until you approve):\n").Append(Diff.TrimEnd()).Append('\n');
        }

        sb.Append($"\nOverall risk: {Risk.ToString().ToUpperInvariant()}");
        if (IsRefused)
        {
            sb.Append($"\nPlan refused by policy: {DeniedCount} action(s) denied. Nothing will run.");
        }

        return sb.ToString();
    }
}
