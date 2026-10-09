using System.Collections.Immutable;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Mods;

namespace ClaudeOS.Core.Safety;

/// <summary>One line of the approval card: what will happen, in the system's words.</summary>
public sealed record ApprovalRow(string Badge, bool External, string Text, ImmutableArray<string> Notes, string? Denied, ImmutableArray<string> Quoted);

/// <summary>
/// What an approval surface shows (the Windows shell's window, or any other front end), with no UI types in it. It is built from the core's
/// <see cref="ApprovalCard"/> (typed actions, policy notes, the staged diff) or from a mod proposal,
/// never from the model's own description of what it will do. That description is shown, but only
/// as a labelled claim.
/// </summary>
public sealed record ApprovalModel(
    string Title,
    string? ModelClaim,
    ImmutableArray<ApprovalRow> Rows,
    string Diff,
    Risk Risk,
    bool HoldToApprove,
    string ApproveLabel,
    string RiskLine)
{
    public bool Refused => Rows.Any(r => r.Denied is not null);

    public static ApprovalModel FromCard(ApprovalCard card)
    {
        var rows = card.Items.Select(i => new ApprovalRow(
            i.Effect == Effect.Local ? "On this PC" : "Leaves this PC",
            i.Effect == Effect.External,
            i.Description,
            i.Notes,
            i.Denied,
            i.ExternalContent)).ToImmutableArray();
        var external = card.Items.Any(i => i.Effect == Effect.External);
        var title = card.Intent.Length > 0 ? card.Intent : "Review what I'll do";
        var risk = card.Risk;
        return new ApprovalModel(
            title,
            card.ModelSummary.Length > 0 ? card.ModelSummary : null,
            rows,
            card.Diff,
            risk,
            HoldToApprove: external,
            ApproveLabel: external ? "Hold to send" : "Apply",
            RiskLine: risk switch
            {
                Risk.High => "Something leaves this computer and cannot be taken back.",
                Risk.Medium => "Changes existing files. You can undo it afterwards.",
                _ => "Only creates new files. Nothing existing is touched.",
            });
    }

    public static ApprovalModel FromMod(ModProposal proposal)
    {
        var rows = proposal.Capabilities.Select(c => new ApprovalRow(
            c.Risk == Risk.High ? "Leaves this PC" : "Can read",
            c.Risk == Risk.High,
            c.Description,
            [],
            null,
            [])).ToImmutableArray();
        if (rows.IsEmpty)
        {
            rows = [new ApprovalRow("Can read", false, "Nothing. It only shows what it was given.", [], null, [])];
        }

        var risk = proposal.Capabilities.Length == 0 ? Risk.Low : proposal.Capabilities.Max(c => c.Risk);
        return new ApprovalModel(
            $"Add “{proposal.Manifest.Name}”",
            null,
            rows,
            "",
            risk,
            HoldToApprove: risk == Risk.High,
            ApproveLabel: risk == Risk.High ? "Hold to add" : "Add",
            RiskLine: "A small folder you own. It contains no code, and you can remove it any time.");
    }
}
