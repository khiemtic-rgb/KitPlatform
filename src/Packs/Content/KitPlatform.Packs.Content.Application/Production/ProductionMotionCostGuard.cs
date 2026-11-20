using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionCostDecision(
    bool Allowed,
    string? BlockReason,
    decimal? EstimatedCredits,
    decimal MaxCredits,
    FamixaCostKind CostKind,
    string BillingBasis);

/// <summary>Server cost policy. Client MaxCost is not authority. UNKNOWN is never zero.</summary>
public static class ProductionMotionCostGuard
{
    public const decimal DefaultMaxCredits = 30;

    public static ProductionCostDecision Evaluate(
        string providerId,
        string modelId,
        decimal durationSec,
        bool isPaid,
        decimal? serverMaxCredits = null)
    {
        var max = serverMaxCredits is > 0 ? serverMaxCredits.Value : DefaultMaxCredits;
        if (!isPaid)
        {
            return new ProductionCostDecision(
                true, null, 0, max, FamixaCostKind.Estimate, "UNPAID_FIXTURE");
        }

        if (!ProductionMotionCatalog.TryGet(providerId, modelId, out var catalog) || !catalog.Enabled)
            return Block(ProductionFailureReasons.ProviderDisabled, max, FamixaCostKind.Unknown, ProductionFailureReasons.UnknownCost);

        var quote = FamixaProviderCostCatalog.Quote(providerId, modelId, (double)durationSec);
        return EvaluateQuote(quote, max);
    }

    public static ProductionCostDecision EvaluateQuote(FamixaProviderCostQuote quote, decimal? serverMaxCredits = null)
    {
        var max = serverMaxCredits is > 0 ? serverMaxCredits.Value : DefaultMaxCredits;
        if (quote.CostKind == FamixaCostKind.Unknown || quote.EstimatedAmount is null)
            return Block(ProductionFailureReasons.UnknownCost, max, quote.CostKind, quote.BillingBasis);
        if (quote.EstimatedAmount.Value > max)
            return Block(ProductionFailureReasons.OverBudget, max, quote.CostKind, quote.BillingBasis, quote.EstimatedAmount);
        return new ProductionCostDecision(true, null, quote.EstimatedAmount, max, quote.CostKind, quote.BillingBasis);
    }

    public static void RequireAllowed(
        string providerId,
        string modelId,
        decimal durationSec,
        bool isPaid,
        decimal? serverMaxCredits = null)
    {
        var decision = Evaluate(providerId, modelId, durationSec, isPaid, serverMaxCredits);
        if (!decision.Allowed)
            throw new InvalidOperationException(decision.BlockReason ?? ProductionFailureReasons.UnknownCost);
    }

    static ProductionCostDecision Block(
        string reason,
        decimal max,
        FamixaCostKind kind,
        string basis,
        decimal? estimated = null) =>
        new(false, reason, estimated, max, kind, basis);
}
