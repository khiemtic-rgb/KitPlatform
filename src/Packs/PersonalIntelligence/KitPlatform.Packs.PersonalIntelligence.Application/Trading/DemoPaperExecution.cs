namespace KitPlatform.Packs.PersonalIntelligence.Trading;

/// <summary>
/// Approval is a separate step from the recommendation and from the risk verdict.
/// </summary>
public sealed record ExecutionApproval(Guid DecisionId, string ApprovedBy);

public sealed record PaperFill(
    Guid DecisionId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    string Mode,
    string Basis);

public sealed record PaperExecutionResult(bool Executed, string Status, PaperFill? Fill, IReadOnlyList<string> Reasons);

public static class DemoPaperExecution
{
    public static PaperExecutionResult Execute(
        TradeProposal proposal,
        RiskVerdict verdict,
        ExecutionApproval? approval)
    {
        if (proposal.Mode == ExecutionMode.Live)
            return Refuse("Live orders are not sent.");
        if (!verdict.Allowed)
            return Refuse(verdict.Reasons.Count > 0 ? verdict.Reasons[0] : "Risk engine vetoed the proposal.");
        if (approval is null || approval.DecisionId == Guid.Empty || string.IsNullOrWhiteSpace(approval.ApprovedBy))
            return Refuse("Paper execution requires a separate approval.");

        return new PaperExecutionResult(
            true,
            "paper_filled",
            new PaperFill(
                approval.DecisionId,
                proposal.Symbol,
                proposal.Side,
                proposal.Quantity,
                proposal.Price,
                "PAPER",
                "FIXTURE"),
            []);
    }

    private static PaperExecutionResult Refuse(string reason) =>
        new(false, "refused", null, [reason]);
}
