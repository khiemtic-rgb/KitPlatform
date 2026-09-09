namespace KitPlatform.Packs.Content;

/// <summary>
/// Router V1 — deterministic filter → policy-where-evidence → explain → select.
/// No ML. No fake quality/speed. No invented Wan price. No real health probe.
/// Lives inside the canonical Selection Service after explicit pin.
/// </summary>
public static class FamixaProviderRouterOutcomes
{
    public const string Explicit = "EXPLICIT";
    public const string Routed = "ROUTED";
    public const string NoProvider = "NO_PROVIDER";
    public const string OverBudget = "OVER_BUDGET";
    public const string NoSafeCandidate = "NO_SAFE_CANDIDATE";
}

public static class FamixaProviderRejectCodes
{
    public const string NotConfigured = "NOT_CONFIGURED";
    public const string Unavailable = "UNAVAILABLE";
    public const string UnknownAvailability = "UNKNOWN_AVAILABILITY";
    public const string OverBudget = "OVER_BUDGET";
    public const string UnknownCost = "UNKNOWN_COST";
    public const string CapabilityMismatch = "CAPABILITY_MISMATCH";
    public const string ModelUnavailable = "MODEL_UNAVAILABLE";
    public const string PolicyMismatch = "POLICY_MISMATCH";
    public const string Eligible = "ELIGIBLE";
}

public static class FamixaProviderEvidence
{
    public const string Quality = "UNKNOWN";
    public const string Speed = "UNKNOWN";
}

/// <summary>
/// Stable provider order per capability. Not a quality ranking.
/// Engine hint may promote a provider among already-eligible candidates only.
/// </summary>
public static class FamixaProviderRouterPriority
{
    public static readonly IReadOnlyList<string> Picture = [FamixaProviderIds.Gemini];
    public static readonly IReadOnlyList<string> Motion = [FamixaProviderIds.Runway, FamixaProviderIds.Wan];
    public static readonly IReadOnlyList<string> Voice = [FamixaProviderIds.ElevenLabs];
    public static readonly IReadOnlyList<string> LipSync = [FamixaProviderIds.Fal];

    public static IReadOnlyList<string> Of(FamixaProviderCapability capability) =>
        capability switch
        {
            FamixaProviderCapability.Picture => Picture,
            FamixaProviderCapability.Motion => Motion,
            FamixaProviderCapability.Voice => Voice,
            FamixaProviderCapability.LipSync => LipSync,
            _ => Array.Empty<string>(),
        };

    public static string? DefaultModel(FamixaProviderCapability capability, string providerId)
    {
        var id = (providerId ?? "").Trim().ToLowerInvariant();
        if (capability == FamixaProviderCapability.Motion && id == FamixaProviderIds.Wan) return "wan-2.1";
        if (capability == FamixaProviderCapability.Motion) return "gen4_turbo";
        if (capability == FamixaProviderCapability.Voice) return "eleven_v3";
        if (capability == FamixaProviderCapability.LipSync) return "1.9";
        if (capability == FamixaProviderCapability.Picture) return "gemini-2.5-flash-image";
        return null;
    }

    public static int IndexOf(FamixaProviderCapability capability, string providerId)
    {
        var order = Of(capability);
        var id = (providerId ?? "").Trim().ToLowerInvariant();
        var i = -1;
        for (var n = 0; n < order.Count; n++)
        {
            if (order[n] == id) { i = n; break; }
        }
        return i < 0 ? 1_000 : i;
    }
}

public sealed record FamixaProviderRouterContext(
    FamixaProviderConfiguredSnapshot? Configured = null,
    IReadOnlySet<string>? UnavailableProviderIds = null);

public sealed record FamixaProviderCandidateEvaluation(
    string ProviderId,
    string? ModelId,
    bool Eligible,
    string ReasonCode,
    string Reason);

public sealed record FamixaProviderRoutingDiagnostic(
    int CandidateCount,
    int RejectedCount,
    string? PrimaryRejectionReason,
    IReadOnlyList<FamixaProviderCandidateEvaluation> Evaluations);

public sealed record FamixaProviderSelectionResult(
    string Outcome,
    FamixaProviderSelectionDecision? Decision,
    FamixaProviderRoutingDiagnostic Diagnostic);

public sealed class FamixaProviderRoutingException : InvalidOperationException
{
    public string Outcome { get; }
    public FamixaProviderRoutingDiagnostic Diagnostic { get; }

    public FamixaProviderRoutingException(string outcome, string message, FamixaProviderRoutingDiagnostic diagnostic)
        : base(message)
    {
        Outcome = outcome;
        Diagnostic = diagnostic;
    }
}

public static class FamixaProviderRouter
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_ROUTER_V1";

    public static FamixaProviderSelectionResult Route(
        FamixaProviderSelectionRequirements req,
        FamixaProviderRouterContext? context = null)
    {
        var ctx = context ?? new FamixaProviderRouterContext();
        var policy = req.Policy ?? FamixaProviderPolicy.Standard;
        var evaluations = new List<FamixaProviderCandidateEvaluation>();

        foreach (var desc in FamixaProviderCatalog.All.OrderBy(d => d.ProviderId, StringComparer.Ordinal))
        {
            evaluations.Add(Evaluate(req, desc, ctx));
        }

        var considered = evaluations.Where(e => e.ReasonCode != FamixaProviderRejectCodes.CapabilityMismatch).ToList();
        var eligible = considered.Where(e => e.Eligible).ToList();
        var rejected = considered.Where(e => !e.Eligible).ToList();
        var primary = PrimaryReject(rejected);
        var diagnostic = new FamixaProviderRoutingDiagnostic(
            considered.Count,
            rejected.Count,
            primary,
            evaluations);

        if (eligible.Count == 0)
        {
            var outcome = OutcomeWhenEmpty(rejected);
            return new(outcome, null, diagnostic);
        }

        var chosen = Choose(req, policy, eligible);
        var quote = FamixaProviderCostCatalog.Quote(chosen.ProviderId, chosen.ModelId, req.DurationSec);
        var reason = SelectedReason(req, ctx, chosen.ProviderId, quote.EstimatedAmount);
        var decision = new FamixaProviderSelectionDecision(
            Guid.NewGuid().ToString("N"),
            req.Capability,
            chosen.ProviderId,
            chosen.ModelId,
            policy,
            reason,
            quote.EstimatedAmount,
            quote.CostKind,
            quote.Currency,
            quote.BillingBasis,
            quote.BilledQuantity,
            FamixaProviderSelectionMode.Routed,
            FamixaProviderRouterOutcomes.Routed,
            diagnostic.CandidateCount,
            diagnostic.RejectedCount,
            diagnostic.PrimaryRejectionReason);
        return new(FamixaProviderRouterOutcomes.Routed, decision, diagnostic);
    }

    public static FamixaProviderCandidateEvaluation Evaluate(
        FamixaProviderSelectionRequirements req,
        FamixaProviderDescriptor desc,
        FamixaProviderRouterContext? context = null)
    {
        var ctx = context ?? new FamixaProviderRouterContext();
        if (!desc.Capabilities.Contains(req.Capability))
        {
            return new(
                desc.ProviderId,
                null,
                false,
                FamixaProviderRejectCodes.CapabilityMismatch,
                $"{desc.DisplayName} rejected: capability mismatch.");
        }

        var model = ModelFor(req, desc);
        if (!ModelOk(desc, model))
        {
            return new(
                desc.ProviderId,
                model,
                false,
                FamixaProviderRejectCodes.ModelUnavailable,
                $"{desc.DisplayName} rejected: model unavailable.");
        }

        var id = desc.ProviderId;
        if (IsUnavailable(ctx, id))
        {
            return new(
                id,
                model,
                false,
                FamixaProviderRejectCodes.Unavailable,
                $"{desc.DisplayName} rejected: UNAVAILABLE.");
        }

        if (ctx.Configured is not null)
        {
            var avail = FamixaProviderAvailabilityRules.Classify(id, ctx.Configured, model);
            if (avail.State == FamixaProviderAvailabilityState.NotConfigured
                || avail.Configuration == FamixaProviderAvailabilityState.NotConfigured)
            {
                return new(
                    id,
                    model,
                    false,
                    FamixaProviderRejectCodes.NotConfigured,
                    $"{desc.DisplayName} rejected: NOT_CONFIGURED.");
            }
        }

        var quote = FamixaProviderCostCatalog.Quote(id, model, req.DurationSec);
        if (req.MaxCost is not null)
        {
            if (quote.EstimatedAmount is null)
            {
                return new(
                    id,
                    model,
                    false,
                    FamixaProviderRejectCodes.UnknownCost,
                    $"{desc.DisplayName} rejected: billed amount unknown; cannot prove budget fit.");
            }

            if (quote.EstimatedAmount.Value > req.MaxCost.Value)
            {
                return new(
                    id,
                    model,
                    false,
                    FamixaProviderRejectCodes.OverBudget,
                    $"{desc.DisplayName} rejected: OVER_BUDGET.");
            }
        }

        return new(
            id,
            model,
            true,
            FamixaProviderRejectCodes.Eligible,
            EligibleNote(req, ctx, desc.DisplayName, quote.EstimatedAmount));
    }

    private static string? ModelFor(FamixaProviderSelectionRequirements req, FamixaProviderDescriptor desc)
    {
        if (req.Capability == FamixaProviderCapability.LipSync)
            return FamixaProviderResolver.NormalizeLipsyncModel(req.ExplicitModelId ?? req.LipsyncModel);
        if (!string.IsNullOrWhiteSpace(req.ExplicitModelId))
            return req.ExplicitModelId.Trim();
        return FamixaProviderRouterPriority.DefaultModel(req.Capability, desc.ProviderId);
    }

    private static bool ModelOk(FamixaProviderDescriptor desc, string? model)
    {
        if (string.IsNullOrWhiteSpace(model) || desc.Models.Count == 0) return true;
        return desc.Models.Any(m => m.Equals(model, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnavailable(FamixaProviderRouterContext ctx, string providerId)
    {
        var set = ctx.UnavailableProviderIds;
        if (set is null || set.Count == 0) return false;
        return set.Contains(providerId) || set.Contains(providerId.ToLowerInvariant());
    }

    private static FamixaProviderCandidateEvaluation Choose(
        FamixaProviderSelectionRequirements req,
        FamixaProviderPolicy policy,
        IReadOnlyList<FamixaProviderCandidateEvaluation> eligible)
    {
        var ordered = eligible
            .OrderBy(e => EngineRank(req, e.ProviderId))
            .ThenBy(e => CostRank(req, policy, eligible, e))
            .ThenBy(e => FamixaProviderRouterPriority.IndexOf(req.Capability, e.ProviderId))
            .ThenBy(e => e.ProviderId, StringComparer.Ordinal)
            .ThenBy(e => e.ModelId ?? "", StringComparer.Ordinal)
            .ToList();
        return ordered[0];
    }

    /// <summary>Legacy engine is a preference among eligible candidates only — not a pin.</summary>
    private static int EngineRank(FamixaProviderSelectionRequirements req, string providerId)
    {
        if (req.Capability != FamixaProviderCapability.Motion || string.IsNullOrWhiteSpace(req.Engine))
            return 0;
        var wan = FamixaProviderResolver.IsWanEngine(req.Engine);
        if (wan) return providerId == FamixaProviderIds.Wan ? 0 : 1;
        return providerId == FamixaProviderIds.Runway ? 0 : 1;
    }

    /// <summary>
    /// ECONOMY may prefer lower known cost only when every remaining eligible has a numeric amount.
    /// Never treats UNKNOWN as cheaper. PREMIUM does not invent quality.
    /// </summary>
    private static int CostRank(
        FamixaProviderSelectionRequirements req,
        FamixaProviderPolicy policy,
        IReadOnlyList<FamixaProviderCandidateEvaluation> eligible,
        FamixaProviderCandidateEvaluation current)
    {
        if (policy != FamixaProviderPolicy.Economy) return 0;
        var quotes = eligible
            .Select(e => FamixaProviderCostCatalog.Quote(e.ProviderId, e.ModelId, req.DurationSec))
            .ToList();
        if (quotes.Any(q => q.EstimatedAmount is null)) return 0;
        var amount = FamixaProviderCostCatalog.Quote(current.ProviderId, current.ModelId, req.DurationSec).EstimatedAmount;
        return amount is null ? 0 : (int)(amount.Value * 100);
    }

    private static string OutcomeWhenEmpty(IReadOnlyList<FamixaProviderCandidateEvaluation> rejected)
    {
        var codes = rejected.Select(r => r.ReasonCode).ToHashSet(StringComparer.Ordinal);
        var over = codes.Contains(FamixaProviderRejectCodes.OverBudget);
        var unknownCost = codes.Contains(FamixaProviderRejectCodes.UnknownCost);
        if (over && unknownCost) return FamixaProviderRouterOutcomes.NoSafeCandidate;
        if (unknownCost) return FamixaProviderRouterOutcomes.NoSafeCandidate;
        if (over) return FamixaProviderRouterOutcomes.OverBudget;
        return FamixaProviderRouterOutcomes.NoProvider;
    }

    private static string? PrimaryReject(IReadOnlyList<FamixaProviderCandidateEvaluation> rejected)
    {
        if (rejected.Count == 0) return null;
        string[] rank =
        [
            FamixaProviderRejectCodes.UnknownCost,
            FamixaProviderRejectCodes.OverBudget,
            FamixaProviderRejectCodes.NotConfigured,
            FamixaProviderRejectCodes.Unavailable,
            FamixaProviderRejectCodes.ModelUnavailable,
            FamixaProviderRejectCodes.PolicyMismatch,
        ];
        foreach (var code in rank)
        {
            if (rejected.Any(r => r.ReasonCode == code)) return code;
        }
        return rejected[0].ReasonCode;
    }

    private static string SelectedReason(
        FamixaProviderSelectionRequirements req,
        FamixaProviderRouterContext ctx,
        string providerId,
        decimal? amount)
    {
        var name = FamixaProviderCatalog.Find(providerId)?.DisplayName ?? providerId;
        var config = ctx.Configured is null
            ? "availability UNKNOWN"
            : "configured";
        var budget = req.MaxCost is null
            ? "no maxCost requirement"
            : amount is not null
                ? "known estimate within budget"
                : "no proven budget comparison";
        return $"{name} selected: {config}, capability compatible, {budget}; quality and speed evidence unavailable.";
    }

    private static string EligibleNote(
        FamixaProviderSelectionRequirements req,
        FamixaProviderRouterContext ctx,
        string displayName,
        decimal? amount)
    {
        var avail = ctx.Configured is null ? "UNKNOWN_AVAILABILITY" : "CONFIGURED";
        var budget = req.MaxCost is null
            ? "NO_MAX_COST"
            : amount is not null ? "WITHIN_BUDGET" : "UNKNOWN_COST";
        return $"{displayName} eligible ({avail}, {budget}). Quality evidence unavailable. Speed evidence unavailable.";
    }
}
