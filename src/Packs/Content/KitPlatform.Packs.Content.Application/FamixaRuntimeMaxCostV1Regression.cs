namespace KitPlatform.Packs.Content;

public static class FamixaRuntimeMaxCostV1Regression
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_RUNTIME_MAXCOST_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var both = new FamixaProviderConfiguredSnapshot(true, true, true, true);
        var ctx = new FamixaProviderRouterContext(both);

        var omitted = new ContentSeriesTurboStartRequest("SH01", "move", null, null, 5, "16:9", "turbo");
        Ok(omitted.MaxCost is null && FamixaRuntimeMaxCost.Validate(omitted.MaxCost) is null, "T1 MaxCost omitted valid");
        var req1 = FamixaRuntimeMaxCost.MotionRequirements(omitted);
        Ok(req1.MaxCost is null, "T1 requirements MaxCost null");
        var r1 = FamixaProviderSelectionFoundation.Route(req1, ctx);
        Ok(r1.Decision?.ProviderId == FamixaProviderIds.Runway
           && r1.Outcome == FamixaProviderRouterOutcomes.Routed, "T1 omitted → no budget filter");

        var req30 = FamixaRuntimeMaxCost.MotionRequirements(
            omitted with { MaxCost = 30, Currency = "credit" });
        Ok(req30.MaxCost == 30 && req30.Currency == "credit", "T8 MaxCost+Currency survive request → requirements");
        var t2 = FamixaProviderRouter.Evaluate(req30, FamixaProviderCatalog.Runway, ctx);
        Ok(t2.Eligible, "T2 MaxCost 30 Runway 25 eligible");

        var req15 = FamixaRuntimeMaxCost.MotionRequirements(omitted with { MaxCost = 15 });
        Ok(req15.MaxCost == 15, "T9 MaxCost survives into Router input");
        var t3 = FamixaProviderRouter.Evaluate(req15, FamixaProviderCatalog.Runway, ctx);
        Ok(!t3.Eligible && t3.ReasonCode == FamixaProviderRejectCodes.OverBudget, "T3 MaxCost 15 OVER_BUDGET");

        var t4 = FamixaProviderSelectionFoundation.Route(req15, ctx);
        Ok(t4.Outcome == FamixaProviderRouterOutcomes.NoSafeCandidate, "T4 Runway 25 + Wan unknown → NO_SAFE_CANDIDATE");

        var t5 = FamixaProviderSelectionFoundation.Route(
            FamixaRuntimeMaxCost.MotionRequirements(omitted with { MaxCost = 30 }), ctx);
        Ok(t5.Decision?.ProviderId == FamixaProviderIds.Runway, "T5 MaxCost 30 Runway eligible");

        var t6 = FamixaProviderRouter.Evaluate(
            FamixaRuntimeMaxCost.MotionRequirements(omitted with { MaxCost = 0 }),
            FamixaProviderCatalog.Runway,
            ctx);
        Ok(!t6.Eligible && t6.ReasonCode == FamixaProviderRejectCodes.OverBudget, "T6 MaxCost 0 → OVER_BUDGET");

        Ok(FamixaRuntimeMaxCost.Validate(-1) == FamixaRuntimeMaxCost.InvalidNegative, "T7 negative invalid");
        var threw = false;
        try { FamixaRuntimeMaxCost.MotionRequirements(omitted with { MaxCost = -5 }); }
        catch (InvalidOperationException ex) { threw = ex.Message.StartsWith("MAXCOST_INVALID", StringComparison.Ordinal); }
        Ok(threw, "T7 negative request rejected");

        var explicitOver = FamixaProviderSelectionFoundation.Select(new FamixaProviderSelectionRequirements(
            FamixaProviderCapability.Motion,
            MaxCost: 15,
            ExplicitProviderId: FamixaProviderIds.Runway,
            DurationSec: 5));
        Ok(explicitOver.SelectionMode == FamixaProviderSelectionMode.Explicit
           && explicitOver.ProviderId == FamixaProviderIds.Runway, "T10 explicit + MaxCost 15 no switch");

        var wan = FamixaProviderCostCatalog.Quote(FamixaProviderIds.Wan, "wan-2.1", 5);
        Ok(wan.EstimatedAmount is null && wan.CostKind == FamixaCostKind.FalBilledEstimate, "T13 Wan amount null");

        Ok(typeof(FamixaProviderSelectionDecision).GetProperty("DecisionId") is not null
           && typeof(FamixaProviderSelectionDecision).GetProperty("Reason") is not null, "T14 Decision contract kept");

        return fail;
    }
}
