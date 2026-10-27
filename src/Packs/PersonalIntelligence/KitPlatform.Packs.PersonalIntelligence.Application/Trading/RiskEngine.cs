namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public enum ExecutionMode
{
    Paper,
    Live,
}

public sealed record RiskLimits(decimal MaxNotional, decimal MaxLeverage, decimal MinConfidence);

public sealed record TradeProposal(
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    decimal Leverage,
    decimal Confidence,
    ExecutionMode Mode);

public sealed record RiskVerdict(bool Allowed, IReadOnlyList<string> Reasons)
{
    public static RiskVerdict Allow() => new(true, []);
}

/// <summary>
/// Independent of the agent. A veto here is sufficient to refuse execution.
/// </summary>
public static class RiskEngine
{
    public static RiskVerdict Evaluate(TradeProposal proposal, RiskLimits limits)
    {
        var reasons = new List<string>();
        if (proposal.Mode == ExecutionMode.Live)
            reasons.Add("Live execution is outside Trading Agent V1.");
        if (string.IsNullOrWhiteSpace(proposal.Symbol))
            reasons.Add("Symbol is required.");
        if (proposal.Side is not ("BUY" or "SELL"))
            reasons.Add("Side must be BUY or SELL.");
        if (proposal.Quantity <= 0 || proposal.Price <= 0)
            reasons.Add("Quantity and price must be positive.");
        if (proposal.Quantity * proposal.Price > limits.MaxNotional)
            reasons.Add("Notional exceeds the configured maximum.");
        if (proposal.Leverage > limits.MaxLeverage)
            reasons.Add("Leverage exceeds the configured maximum.");
        if (proposal.Confidence < limits.MinConfidence)
            reasons.Add("Confidence is below the configured minimum.");

        return reasons.Count == 0 ? RiskVerdict.Allow() : new RiskVerdict(false, reasons);
    }
}
