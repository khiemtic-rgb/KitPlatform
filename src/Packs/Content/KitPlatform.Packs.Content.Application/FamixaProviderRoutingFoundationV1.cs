namespace KitPlatform.Packs.Content;

/// <summary>
/// Canonical Series selection point. Explicit pin first; Router V1 after that.
/// No ML. No A→B fallback. No health probe. No CURRENT/APPROVED.
/// </summary>
public enum FamixaProviderSelectionMode
{
    Explicit,
    Routed,
    Policy,
    Legacy,
}

public enum FamixaProviderAvailabilityState
{
    Configured,
    NotConfigured,
    Disabled,
    Unknown,
    Available,
    Unavailable,
}

public enum FamixaBudgetFit
{
    WithinBudget,
    OverBudget,
    Unknown,
}

public sealed record FamixaProviderSelectionRequirements(
    FamixaProviderCapability Capability,
    decimal? MaxCost = null,
    string? Currency = null,
    string? QualityPreference = null,
    string? SpeedPreference = null,
    string? AvailabilityRequirement = null,
    string? Language = null,
    double? DurationSec = null,
    string? OutputConstraints = null,
    string? TenantOrg = null,
    string? PlanTier = null,
    FamixaProviderPolicy? Policy = null,
    string? ExplicitProviderId = null,
    string? ExplicitModelId = null,
    string? Engine = null,
    string? LipsyncModel = null,
    string? VoiceProvider = null);

public sealed record FamixaProviderSelectionDecision(
    string DecisionId,
    FamixaProviderCapability Capability,
    string ProviderId,
    string? ModelId,
    FamixaProviderPolicy Policy,
    string Reason,
    decimal? EstimatedCost,
    FamixaCostKind CostKind,
    string? Currency,
    string BillingBasis,
    double BilledQuantity,
    FamixaProviderSelectionMode SelectionMode,
    string Outcome = "SUCCESS",
    int CandidateCount = 0,
    int RejectedCount = 0,
    string? PrimaryRejectionReason = null);

public sealed record FamixaProviderAvailabilityResult(
    string ProviderId,
    string? ModelId,
    FamixaProviderAvailabilityState State,
    string Reason,
    FamixaProviderAvailabilityState Configuration = FamixaProviderAvailabilityState.Unknown,
    bool IsAvailable = false);

public sealed record FamixaProviderConfiguredSnapshot(
    bool RunwayConfigured,
    bool FalConfigured,
    bool ElevenLabsConfigured,
    bool GeminiConfigured);

/// <summary>Policy as preference weights only. No numeric quality/speed scores.</summary>
public sealed record FamixaProviderPolicyPreference(string CostPreference, string QualityPreference, string SpeedPreference);

public interface IFamixaProviderAvailability
{
    FamixaProviderAvailabilityResult Inspect(string providerId, string? modelId = null);
}

public interface IFamixaProviderSelectionService
{
    FamixaProviderSelectionResult Route(FamixaProviderSelectionRequirements requirements);
    FamixaProviderSelectionDecision Select(FamixaProviderSelectionRequirements requirements);
}

public static class FamixaProviderBudget
{
    public static FamixaBudgetFit Fit(decimal? amount, decimal? maxCost)
    {
        if (amount is null || maxCost is null) return FamixaBudgetFit.Unknown;
        return amount.Value <= maxCost.Value ? FamixaBudgetFit.WithinBudget : FamixaBudgetFit.OverBudget;
    }

    /// <summary>null amount is UNKNOWN — never within budget, never zero.</summary>
    public static FamixaBudgetFit IsKnownNumericCostWithinBudget(decimal? amount, decimal? maxCost) =>
        Fit(amount, maxCost);
}

public static class FamixaProviderPolicyPreferences
{
    public static FamixaProviderPolicyPreference Of(FamixaProviderPolicy policy) =>
        policy switch
        {
            FamixaProviderPolicy.Economy => new("HIGH", "NORMAL", "NORMAL"),
            FamixaProviderPolicy.Premium => new("NORMAL", "HIGH", "NORMAL"),
            _ => new("NORMAL", "NORMAL", "NORMAL"),
        };
}

public static class FamixaProviderAvailabilityRules
{
    public static FamixaProviderAvailabilityResult Classify(
        string providerId,
        FamixaProviderConfiguredSnapshot snapshot,
        string? modelId = null)
    {
        var id = (providerId ?? "").Trim().ToLowerInvariant();
        var configured = id switch
        {
            FamixaProviderIds.Runway => snapshot.RunwayConfigured,
            FamixaProviderIds.Wan => snapshot.FalConfigured,
            FamixaProviderIds.Fal => snapshot.FalConfigured,
            FamixaProviderIds.ElevenLabs => snapshot.ElevenLabsConfigured,
            FamixaProviderIds.Gemini => snapshot.GeminiConfigured,
            _ => false,
        };
        if (!configured)
        {
            return new(
                id,
                modelId,
                FamixaProviderAvailabilityState.NotConfigured,
                "no key — NOT_CONFIGURED",
                FamixaProviderAvailabilityState.NotConfigured,
                false);
        }

        return new(
            id,
            modelId,
            FamixaProviderAvailabilityState.Unknown,
            "key configured; health unknown — not AVAILABLE",
            FamixaProviderAvailabilityState.Configured,
            false);
    }
}

public static class FamixaProviderSelectionFoundation
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_ROUTING_FOUNDATION_V1";

    public static FamixaProviderSelectionRequirements From(
        FamixaProviderCapability capability,
        FamixaProviderSelection? selection = null,
        decimal? maxCost = null,
        double? durationSec = null,
        string? currency = null) =>
        new(
            capability,
            MaxCost: maxCost,
            Currency: currency,
            Policy: selection?.Policy,
            ExplicitProviderId: selection?.ExplicitProviderId,
            Engine: selection?.Engine,
            LipsyncModel: selection?.LipsyncModel,
            VoiceProvider: selection?.VoiceProvider,
            DurationSec: durationSec);

    public static FamixaProviderSelectionResult Route(
        FamixaProviderSelectionRequirements req,
        FamixaProviderRouterContext? context = null)
    {
        var policy = req.Policy ?? FamixaProviderPolicy.Standard;
        if (!string.IsNullOrWhiteSpace(req.ExplicitProviderId))
        {
            var resolved = FamixaProviderResolver.ResolveLegacyMap(
                req.Capability,
                new FamixaProviderSelection(
                    ExplicitProviderId: req.ExplicitProviderId,
                    LipsyncModel: req.ExplicitModelId ?? req.LipsyncModel,
                    VoiceProvider: req.VoiceProvider,
                    Policy: policy));
            var decision = Decide(
                req,
                resolved.ProviderId,
                resolved.ModelId,
                policy,
                FamixaProviderSelectionMode.Explicit,
                "explicit provider selected by Director",
                FamixaProviderRouterOutcomes.Explicit);
            return new(
                FamixaProviderRouterOutcomes.Explicit,
                decision,
                new FamixaProviderRoutingDiagnostic(0, 0, null, []));
        }

        return FamixaProviderRouter.Route(req, context);
    }

    public static FamixaProviderSelectionDecision Select(
        FamixaProviderSelectionRequirements req,
        FamixaProviderRouterContext? context = null)
    {
        var result = Route(req, context);
        if (result.Decision is not null) return result.Decision;
        throw new FamixaProviderRoutingException(
            result.Outcome,
            $"{result.Outcome}: {result.Diagnostic.PrimaryRejectionReason ?? "no eligible provider"}.",
            result.Diagnostic);
    }

    private static FamixaProviderSelectionDecision Decide(
        FamixaProviderSelectionRequirements req,
        string providerId,
        string? modelId,
        FamixaProviderPolicy policy,
        FamixaProviderSelectionMode mode,
        string reason,
        string outcome)
    {
        var quote = FamixaProviderCostCatalog.Quote(providerId, modelId, req.DurationSec);
        return new(
            Guid.NewGuid().ToString("N"),
            req.Capability,
            providerId,
            modelId,
            policy,
            reason,
            quote.EstimatedAmount,
            quote.CostKind,
            quote.Currency,
            quote.BillingBasis,
            quote.BilledQuantity,
            mode,
            outcome);
    }
}
