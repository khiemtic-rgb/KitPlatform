using KitPlatform.Packs.PersonalIntelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record TradingRecommendation(
    EpistemicKind Kind,
    string Rationale,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<string> Risks,
    decimal Confidence,
    IReadOnlyList<string> Unknowns,
    RiskVerdict Verdict,
    TradeProposal Proposal,
    string Autonomy);

public static class TradingAgent
{
    public const string Autonomy = "recommend_only";

    public static TradingRecommendation Recommend(
        Guid tenantId,
        IReadOnlyList<ObservedTrade> evidence,
        TradeProposal proposal,
        RiskLimits limits,
        IReadOnlyList<string> unknowns)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A recommendation requires a tenant.");

        var verdict = RiskEngine.Evaluate(proposal, limits);
        var risks = verdict.Reasons.ToList();
        if (evidence.Count == 0)
            risks.Add("No observed trades were supplied as evidence.");

        return new TradingRecommendation(
            EpistemicGuard.AsAgentClaim(EpistemicKind.Fact),
            Rationale: evidence.Count == 0
                ? "No observed history was available. This is a hypothesis, not a verified outcome."
                : $"Based on {evidence.Count} observed fill(s). This does not create a decision or an order.",
            EvidenceIds: evidence.Select(row => row.ExternalTradeId).ToArray(),
            Risks: risks,
            proposal.Confidence,
            unknowns,
            verdict,
            proposal with { Mode = ExecutionMode.Paper },
            Autonomy);
    }
}
