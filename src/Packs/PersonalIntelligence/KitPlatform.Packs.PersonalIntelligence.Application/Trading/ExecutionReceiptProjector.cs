using KitPlatform.Packs.PersonalIntelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record ReceiptInput(
    string Source,
    string Symbol,
    string Timeframe,
    int CandleCount,
    DateTimeOffset FirstOpen,
    DateTimeOffset LastClose);

public sealed record ReceiptResearch(string Status, int FillCount);

public sealed record ReceiptRecommendation(
    bool Recorded,
    string Autonomy,
    string Kind,
    string Rationale,
    IReadOnlyList<string> EvidenceIds,
    decimal Confidence,
    string Mode);

public sealed record ReceiptLimits(decimal MaxNotional, decimal MaxLeverage, decimal MinConfidence);

public sealed record ReceiptRisk(bool Allowed, IReadOnlyList<string> Reasons, ReceiptLimits Limits);

public sealed record ReceiptApproval(string State, string? ApprovedBy);

public sealed record ReceiptProvenance(string Database, Guid? TenantId);

public sealed record ExecutionReceipt(
    string SessionKey,
    string PayloadHash,
    ReceiptInput Input,
    ReceiptResearch Research,
    ReceiptRecommendation Recommendation,
    ReceiptRisk Risk,
    ReceiptApproval Approval,
    string Outcome,
    string? OutcomeReason,
    IReadOnlyList<string> EvidenceRefs,
    DateTimeOffset RecordedAt,
    ReceiptProvenance Provenance);

public sealed record ExecutionReceiptView(bool Recorded, ExecutionReceipt? Receipt);

/// <summary>
/// Read-only projection of one research recommendation. It does not place an order or open a second store.
/// </summary>
public static class ExecutionReceiptProjector
{
    public const string DatabaseName = "trading_agent";
    public const string ApprovalRequired = "approval_required";
    public const string PaperExecuted = "paper_executed";
    public const string RiskRejected = "risk_rejected";
    public const string LiveBlocked = "live_blocked";
    public const string Failed = "failed";
    public const string Replayed = "replayed";
    public const string ApprovalAbsent = "absent";
    public const string ApprovalPresent = "present";

    public static ExecutionReceiptView Read(ResearchSnapshot snapshot)
        => snapshot.Receipt is null ? new ExecutionReceiptView(false, null) : new ExecutionReceiptView(true, snapshot.Receipt);

    public static ExecutionReceipt Project(
        string sessionKey,
        string payloadHash,
        CandleIntake intake,
        ResearchSnapshot snapshot,
        TradingRecommendation recommendation,
        RiskVerdict verdict,
        PaperExecutionResult paper,
        RiskLimits limits,
        ExecutionApproval? approval,
        DateTimeOffset recordedAt,
        string database)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(paper);
        ArgumentNullException.ThrowIfNull(limits);
        if (!string.Equals(sessionKey, payloadHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Session key and payload hash must match.");
        if (!string.Equals(database, DatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException("A research receipt only records database trading_agent.");
        if (recommendation.Proposal.Mode != ExecutionMode.Paper)
            throw new InvalidOperationException("A recorded recommendation must already be paper.");
        if (!string.Equals(recommendation.Autonomy, TradingAgent.Autonomy, StringComparison.Ordinal))
            throw new InvalidOperationException("A recorded recommendation must stay recommend_only.");
        if (recommendation.Kind != EpistemicKind.Inference)
            throw new InvalidOperationException("An agent recommendation is an inference.");
        if (intake.Accepted.Count == 0)
            throw new InvalidOperationException("A research receipt requires accepted candles.");

        var first = intake.Accepted[0];
        var source = first.Source;
        var symbol = first.Symbol;
        var timeframe = first.Interval;
        if (intake.Accepted.Any(candle =>
                !string.Equals(candle.Source, source, StringComparison.Ordinal)
                || !string.Equals(candle.Symbol, symbol, StringComparison.Ordinal)
                || !string.Equals(candle.Interval, timeframe, StringComparison.Ordinal)))
            throw new InvalidOperationException("A research receipt requires one candle source, symbol, and timeframe.");

        var approved = approval is not null
            && approval.DecisionId != Guid.Empty
            && !string.IsNullOrWhiteSpace(approval.ApprovedBy);
        var outcome = OutcomeOf(snapshot.Status, verdict, paper, approved);
        if (outcome == PaperExecuted && snapshot.Fills.Count > 0 && paper.Fill is null)
            throw new InvalidOperationException("Research fills do not prove a recommendation execution.");
        var reason = outcome == PaperExecuted
            ? null
            : paper.Reasons.FirstOrDefault() ?? verdict.Reasons.FirstOrDefault() ?? outcome;
        if (outcome != PaperExecuted && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A receipt that did not execute requires a reason.");

        var evidenceRefs = snapshot.SignalsRecorded.Select(signal => signal.SignalKey)
            .Concat(snapshot.Fills.Select(fill => fill.OperationKey))
            .Concat(snapshot.RiskDecisions.Select(decision => decision.OperationKey))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ExecutionReceipt(
            sessionKey,
            payloadHash,
            new ReceiptInput(
                source,
                symbol,
                timeframe,
                intake.Accepted.Count,
                intake.Accepted.Min(candle => candle.OpenTime),
                intake.Accepted.Max(candle => candle.CloseTime)),
            new ReceiptResearch(snapshot.Status, snapshot.Fills.Count),
            new ReceiptRecommendation(
                true,
                recommendation.Autonomy,
                recommendation.Kind.ToString(),
                recommendation.Rationale,
                recommendation.EvidenceIds.ToArray(),
                recommendation.Proposal.Confidence,
                recommendation.Proposal.Mode.ToString()),
            new ReceiptRisk(verdict.Allowed, verdict.Reasons.ToArray(), new ReceiptLimits(limits.MaxNotional, limits.MaxLeverage, limits.MinConfidence)),
            new ReceiptApproval(approved ? ApprovalPresent : ApprovalAbsent, approved ? approval!.ApprovedBy.Trim() : null),
            outcome,
            reason,
            evidenceRefs,
            recordedAt,
            new ReceiptProvenance(DatabaseName, null));
    }

    private static string OutcomeOf(string researchStatus, RiskVerdict verdict, PaperExecutionResult paper, bool approved)
    {
        if (researchStatus == ResearchSession.LiveBlocked
            || paper.Reasons.Any(reason => reason.Contains("Live orders are not sent.", StringComparison.Ordinal)))
            return LiveBlocked;
        if (!verdict.Allowed)
            return RiskRejected;
        if (paper.Executed && paper.Fill is { Mode: "PAPER" } && approved)
            return PaperExecuted;
        if (!approved)
            return ApprovalRequired;
        return Failed;
    }
}
