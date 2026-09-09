namespace KitPlatform.Packs.Content;

public static class FamixaProviderRouterV1Regression
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_ROUTER_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var both = new FamixaProviderConfiguredSnapshot(true, true, true, true);
        var none = new FamixaProviderConfiguredSnapshot(false, false, false, false);
        var runwayOnly = new FamixaProviderConfiguredSnapshot(true, false, true, true);
        var ctxBoth = new FamixaProviderRouterContext(both);
        var ctxNone = new FamixaProviderRouterContext(none);
        var ctxRunway = new FamixaProviderRouterContext(runwayOnly);

        var t1 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion, ExplicitProviderId: FamixaProviderIds.Runway), ctxBoth);
        Ok(t1.SelectionMode == FamixaProviderSelectionMode.Explicit && t1.ProviderId == FamixaProviderIds.Runway, "T1 explicit Runway");

        var t2 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion, ExplicitProviderId: FamixaProviderIds.Wan), ctxBoth);
        Ok(t2.SelectionMode == FamixaProviderSelectionMode.Explicit && t2.ProviderId == FamixaProviderIds.Wan, "T2 explicit Wan");

        var t3 = FamixaProviderRouter.Evaluate(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion, MaxCost: 30, DurationSec: 5),
            FamixaProviderCatalog.Runway,
            ctxBoth);
        Ok(t3.Eligible && t3.ReasonCode == FamixaProviderRejectCodes.Eligible, "T3 budget 30 Runway 25 eligible");

        var t4 = FamixaProviderRouter.Evaluate(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion, MaxCost: 15, DurationSec: 5),
            FamixaProviderCatalog.Runway,
            ctxBoth);
        Ok(!t4.Eligible && t4.ReasonCode == FamixaProviderRejectCodes.OverBudget, "T4 budget 15 Runway 25 OVER_BUDGET");

        var t5 = FamixaProviderRouter.Evaluate(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion, MaxCost: 15, DurationSec: 5),
            FamixaProviderCatalog.Wan,
            ctxBoth);
        Ok(!t5.Eligible && t5.ReasonCode == FamixaProviderRejectCodes.UnknownCost
           && t5.Reason.Contains("cannot prove budget fit", StringComparison.Ordinal), "T5 Wan NOT proven in budget");

        var t6 = FamixaProviderSelectionFoundation.Route(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion, MaxCost: 15, DurationSec: 5),
            ctxBoth);
        Ok(t6.Outcome == FamixaProviderRouterOutcomes.NoSafeCandidate && t6.Decision is null, "T6 NO_SAFE_CANDIDATE");

        var t7 = FamixaProviderSelectionFoundation.Select(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion, DurationSec: 5),
            ctxRunway);
        Ok(t7.SelectionMode == FamixaProviderSelectionMode.Routed && t7.ProviderId == FamixaProviderIds.Runway, "T7 no budget Runway configured");

        var t8 = FamixaProviderRouter.Evaluate(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion),
            FamixaProviderCatalog.Runway,
            ctxNone);
        Ok(t8.ReasonCode == FamixaProviderRejectCodes.NotConfigured, "T8 no key NOT_CONFIGURED");

        var t9 = FamixaProviderAvailabilityRules.Classify(FamixaProviderIds.Runway, both);
        Ok(t9.State == FamixaProviderAvailabilityState.Unknown && !t9.IsAvailable, "T9 key + no health UNKNOWN");

        var t10 = FamixaProviderRouter.Evaluate(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion),
            FamixaProviderCatalog.Runway,
            new FamixaProviderRouterContext(both, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { FamixaProviderIds.Runway }));
        Ok(t10.ReasonCode == FamixaProviderRejectCodes.Unavailable, "T10 unavailable reject");

        var t11 = FamixaProviderRouter.Evaluate(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion),
            FamixaProviderCatalog.Gemini,
            ctxBoth);
        Ok(t11.ReasonCode == FamixaProviderRejectCodes.CapabilityMismatch, "T11 capability mismatch");

        var t12a = FamixaProviderSelectionFoundation.Select(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion),
            ctxBoth);
        var t12b = FamixaProviderSelectionFoundation.Select(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion),
            ctxBoth);
        Ok(t12a.ProviderId == FamixaProviderIds.Runway && t12b.ProviderId == FamixaProviderIds.Runway, "T12 deterministic runway");

        var premium = FamixaProviderSelectionFoundation.Select(
            new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion, Policy: FamixaProviderPolicy.Premium),
            ctxBoth);
        Ok(premium.ProviderId == t12a.ProviderId
           && !premium.Reason.Contains("0.9", StringComparison.Ordinal)
           && premium.Reason.Contains("quality and speed evidence unavailable", StringComparison.Ordinal), "T13 PREMIUM no fake quality");

        Ok(FamixaProviderEvidence.Speed == "UNKNOWN" && !premium.Reason.Contains("latency", StringComparison.Ordinal), "T14 speed evidence UNKNOWN");

        Ok(!string.IsNullOrWhiteSpace(t7.Reason), "T15 reason");
        Ok(!string.IsNullOrWhiteSpace(t7.DecisionId), "T16 DecisionId");

        var cost = FamixaProviderCostCatalog.Quote(t7.ProviderId, t7.ModelId, 5);
        Ok(t7.EstimatedCost == cost.EstimatedAmount && t7.CostKind == cost.CostKind, "T17 cost from SoT");

        var wan = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion, ExplicitProviderId: FamixaProviderIds.Wan, DurationSec: 5));
        Ok(wan.EstimatedCost is null && wan.CostKind == FamixaCostKind.FalBilledEstimate, "T18 Wan amount null");

        Ok(FamixaProviderRouterPriority.Motion[0] == FamixaProviderIds.Runway
           && FamixaProviderRouterPriority.Motion[1] == FamixaProviderIds.Wan, "T19 priority in routing config");

        var explicitOver = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion, MaxCost: 1, ExplicitProviderId: FamixaProviderIds.Runway, DurationSec: 10));
        Ok(explicitOver.SelectionMode == FamixaProviderSelectionMode.Explicit, "explicit pin skips budget filter");

        return fail;
    }
}
