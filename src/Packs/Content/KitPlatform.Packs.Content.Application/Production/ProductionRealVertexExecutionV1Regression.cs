using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionRealVertexExecutionV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_REAL_VERTEX_EXECUTION_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try
        {
            RealVertexExecutionV1Lock.ResetForTests();
            RunCore(Ok);
        }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        finally { RealVertexExecutionV1Lock.ResetForTests(); }
        return fail;
    }

    static void RunCore(Action<bool, string> Ok)
    {
        T_CatalogCostKnown(Ok);
        T_ArmedOfficialStopsBeforeNetwork(Ok);
        T_WrongTargetBlocked(Ok);
        T_InventedCostRejected(Ok);
        T_DuplicateStartFailClosed(Ok);
        T_ProductionResolverStillClosed(Ok);
        T_NoPermitCreatedOrConsumed(Ok);
        T_Ck11StillClosed(Ok);
    }

    static void T_CatalogCostKnown(Action<bool, string> Ok)
    {
        var cost = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10);
        Ok(cost.Kind == CostPolicyKind.Allowed && cost.EstimatedAmount == 0.20m
            && cost.BillingBasis == FamixaProviderCostCatalog.BasisGoogleVeo31PerCount,
            "catalog quote is official $0.20 / count — not invented");
    }

    static void T_ArmedOfficialStopsBeforeNetwork(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var permits = new CountingProductionExecutionPermitStore();
        var selection = OfficialSelection();
        var request = RealVertexExecutionV1Runner.ArmOfficialTarget(
            selection,
            adapter: graph.Adapter,
            authenticator: new ProductionVeoHttpAuthenticator(),
            httpCompositionAvailable: true);
        var result = RealVertexExecutionV1Runner.Attempt(request, graph.Transport, permits);
        Ok(!result.Executed && !result.Preflight.Allowed, "armed official preflight BLOCKED");
        Ok(!result.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost),
            "UNKNOWN_COST removed for official VIDEO_ONLY 720p");
        Ok(result.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "permit null → OFFICIAL_FILM_FORBIDDEN");
        Ok(result.Preflight.Blockers.Contains(ProductionFailureReasons.InputNotFrozen), "no CREATE_VIDEO snapshot");
        Ok(result.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady),
            "production authenticator not ready — GetAccessToken not called");
        Ok(!result.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownProvider),
            "one-shot resolver is enabled for this target only");
        Ok(!result.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden),
            "paid-network authorized for this job, still no Start");
        Ok(result.StartCount == 0 && graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0,
            "StartCount=0 HTTP=0");
        Ok(graph.Authenticator.Calls == 0 && permits.ConsumeCalls == 0, "no OAuth / no permit consume");
    }

    static void T_WrongTargetBlocked(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var request = RealVertexExecutionV1Runner.ArmOfficialTarget(OfficialSelection(), adapter: graph.Adapter);
        request = request with
        {
            Scope = new ProductionScope(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "EP99", "SC01", "TEST-SC01-SH02"),
        };
        var result = RealVertexExecutionV1Runner.Attempt(request, graph.Transport);
        Ok(!result.Executed
            && result.Preflight.Blockers.Contains(ProductionFailureReasons.UnauthorizedVertexTarget)
            && graph.Transport.StartCount == 0,
            "TEST / other shot is BLOCKED");
    }

    static void T_InventedCostRejected(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var request = RealVertexExecutionV1Runner.ArmOfficialTarget(OfficialSelection(), adapter: graph.Adapter);
        request = request with { Cost = CostPolicyDecision.ExplicitAllowed(10) };
        var result = RealVertexExecutionV1Runner.Attempt(request, graph.Transport);
        Ok(!result.Executed
            && !result.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost)
            && result.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "live runner strips EXPLICIT_TEST_POLICY; catalog ALLOWED still blocked on permit");
        Ok(graph.Transport.StartCount == 0, "invented cost does not Start");
    }

    static void T_DuplicateStartFailClosed(Action<bool, string> Ok)
    {
        RealVertexExecutionV1Lock.ResetForTests();
        var first = RealVertexExecutionV1Lock.RecordStart();
        var secondFailed = false;
        try { RealVertexExecutionV1Lock.RecordStart(); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.DuplicateProviderStart)
        {
            secondFailed = true;
        }
        Ok(first == 1 && secondFailed && RealVertexExecutionV1Lock.StartCount == 1,
            "second Start fail-closed");
        RealVertexExecutionV1Lock.ResetForTests();
    }

    static void T_ProductionResolverStillClosed(Action<bool, string> Ok)
    {
        var closed = false;
        try { ProductionVeoReadinessComposition.CreateProductionResolver().Resolve("google", "veo-3.1"); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UnknownProvider)
        {
            closed = true;
        }
        Ok(closed && !ProductionVeoReadinessComposition.ProductionResolverIncludesVeo,
            "production resolver remains CLOSED");
    }

    static void T_NoPermitCreatedOrConsumed(Action<bool, string> Ok)
    {
        var permits = new CountingProductionExecutionPermitStore();
        var result = RealVertexExecutionV1Runner.Attempt(
            RealVertexExecutionV1Runner.ArmOfficialTarget(OfficialSelection()),
            permits: permits);
        Ok(permits.ConsumeCalls == 0 && result.PermitConsumeCalls == 0, "permit consume = 0");
        Ok(result.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "did not create a permit to pass preflight");
    }

    static void T_Ck11StillClosed(Action<bool, string> Ok)
    {
        var ck11 = ProductionRealVertexExecutionPreflightV1Regression.Run();
        Ok(ck11.Count == 0, ck11.Count == 0 ? "CK11 preflight still closed" : "CK11:" + string.Join(",", ck11));
        Ok(!ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "CK10/11 execution-open flag stays false");
    }

    static ProductionAiSelection OfficialSelection() =>
        new(
            Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8"),
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            "google", "veo-3.1",
            ProductionAiSelectionModes.Explicit,
            "checkpoint-12",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck12");
}
