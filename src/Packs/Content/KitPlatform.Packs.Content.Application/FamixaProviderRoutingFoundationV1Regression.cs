namespace KitPlatform.Packs.Content;

public static class FamixaProviderRoutingFoundationV1Regression
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_ROUTING_FOUNDATION_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var t1 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            ExplicitProviderId: FamixaProviderIds.Runway));
        Ok(t1.SelectionMode == FamixaProviderSelectionMode.Explicit && t1.ProviderId == FamixaProviderIds.Runway, "T1 explicit Runway");

        var t2 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            ExplicitProviderId: FamixaProviderIds.Wan));
        Ok(t2.SelectionMode == FamixaProviderSelectionMode.Explicit && t2.ProviderId == FamixaProviderIds.Wan, "T2 explicit Wan");

        var t3 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            Engine: "turbo"));
        Ok(t3.SelectionMode == FamixaProviderSelectionMode.Routed && t3.ProviderId == FamixaProviderIds.Runway, "T3 engine turbo → ROUTED runway");

        var t4 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            Engine: "wan"));
        Ok(t4.SelectionMode == FamixaProviderSelectionMode.Routed && t4.ProviderId == FamixaProviderIds.Wan, "T4 engine wan → ROUTED wan");

        var t5 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Motion));
        Ok(t5.SelectionMode == FamixaProviderSelectionMode.Routed
           && t5.Policy == FamixaProviderPolicy.Standard
           && t5.ProviderId == FamixaProviderIds.Runway, "T5 no explicit → ROUTED stable runway");

        var t6 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Picture));
        Ok(t6.ProviderId == FamixaProviderIds.Gemini, "T6 Picture → Gemini");

        var t7 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.Voice));
        Ok(t7.ProviderId == FamixaProviderIds.ElevenLabs, "T7 Voice → ElevenLabs");

        var t8 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(FamixaProviderCapability.LipSync));
        Ok(t8.ProviderId == FamixaProviderIds.Fal, "T8 LipSync → Fal");

        var t9 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            ExplicitProviderId: FamixaProviderIds.Runway,
            DurationSec: 5));
        Ok(t9.EstimatedCost == 25 && t9.CostKind == FamixaCostKind.Estimate, "T9 Runway numeric ESTIMATE");

        var t10 = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            ExplicitProviderId: FamixaProviderIds.Wan,
            DurationSec: 5));
        Ok(t10.EstimatedCost is null && t10.CostKind == FamixaCostKind.FalBilledEstimate, "T10 Wan null FAL_BILLED_ESTIMATE");

        Ok(FamixaProviderBudget.IsKnownNumericCostWithinBudget(25, 30) == FamixaBudgetFit.WithinBudget, "T11 known <= maxCost");
        Ok(FamixaProviderBudget.IsKnownNumericCostWithinBudget(50, 25) == FamixaBudgetFit.OverBudget, "T12 known > maxCost");
        Ok(FamixaProviderBudget.IsKnownNumericCostWithinBudget(null, 30) == FamixaBudgetFit.Unknown, "T13 unknown cost");

        var empty = new FamixaProviderConfiguredSnapshot(false, false, false, false);
        var t14 = FamixaProviderAvailabilityRules.Classify(FamixaProviderIds.Runway, empty);
        Ok(t14.State == FamixaProviderAvailabilityState.NotConfigured && !t14.IsAvailable, "T14 no key → NOT_CONFIGURED");

        var keyed = new FamixaProviderConfiguredSnapshot(true, true, true, true);
        var t15 = FamixaProviderAvailabilityRules.Classify(FamixaProviderIds.Runway, keyed);
        Ok(t15.State == FamixaProviderAvailabilityState.Unknown
           && t15.Configuration == FamixaProviderAvailabilityState.Configured
           && !t15.IsAvailable
           && t15.State != FamixaProviderAvailabilityState.Available, "T15 key + no health → UNKNOWN not AVAILABLE");

        Ok(!string.IsNullOrWhiteSpace(t1.Reason), "T16 SelectionDecision has reason");
        Ok(!string.IsNullOrWhiteSpace(t1.DecisionId), "T17 SelectionDecision has DecisionId");

        var overBudgetExplicit = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            MaxCost: 1,
            ExplicitProviderId: FamixaProviderIds.Runway,
            DurationSec: 10));
        Ok(overBudgetExplicit.SelectionMode == FamixaProviderSelectionMode.Explicit
           && overBudgetExplicit.ProviderId == FamixaProviderIds.Runway, "explicit over budget does not switch");

        var pref = FamixaProviderPolicyPreferences.Of(FamixaProviderPolicy.Premium);
        Ok(pref.QualityPreference == "HIGH" && pref.CostPreference == "NORMAL", "policy is preference labels only");

        var compat = FamixaProviderResolver.Resolve(FamixaProviderCapability.Motion, new FamixaProviderSelection(Engine: "wan"));
        Ok(compat.ProviderId == FamixaProviderIds.Wan, "legacy Resolve wraps Foundation");

        return fail;
    }
}
