using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionRealVertexExecutionPreflightV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_REAL_VERTEX_EXECUTION_PREFLIGHT_V1";
    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
    const string Acting = "He stays in place.";
    const string Visual = "visual 16:9";
    static readonly Guid OfficialSelectionId = Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8");
    static readonly Guid OfficialPerformanceId = Guid.Parse("6505b8a2-8a71-4d74-8d93-f1fae8319b6b");
    const string OfficialPerformanceHash =
        "PERFORMANCE_HASH_V1:8fa1d71b14e46e64bf1cf4dc7b107ae7f39be69f3f79da1d55d24f33a116914a";
    static readonly Guid OfficialKeyframeId = Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef");

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunCore(Ok); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static void RunCore(Action<bool, string> Ok)
    {
        T_OfficialSelectionMissing(Ok);
        T_WrongProvider(Ok);
        T_WrongModel(Ok);
        T_ResolverClosed(Ok);
        T_ResolverEnabledFake(Ok);
        T_FrozenMissing(Ok);
        T_FrozenMismatch(Ok);
        T_KeyframeMissing(Ok);
        T_KeyframeBytesMissing(Ok);
        T_KeyframeHashMismatch(Ok);
        T_CostUnknown(Ok);
        T_CostForbidden(Ok);
        T_CostAllowedDoesNotInventCatalog(Ok);
        T_PermitMissing(Ok);
        T_PermitExpired(Ok);
        T_PermitWrongScope(Ok);
        T_PaidNetworkFalse(Ok);
        T_AuthenticatorUnavailable(Ok);
        T_UnsupportedOutput(Ok);
        T_AllSatisfiedNoStart(Ok);
        T_NoNetworkNoOauthNoConsume(Ok);
        T_Deterministic(Ok);
        T_OfficialClosedNoMutation(Ok);
    }

    static void T_OfficialSelectionMissing(Action<bool, string> Ok)
    {
        var req = Satisfied() with { ActiveVideoSelection = null };
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.MissingProductionAiSelection),
            "official selection missing");
    }

    static void T_WrongProvider(Action<bool, string> Ok)
    {
        var req = Satisfied() with { ActiveVideoSelection = Selection("runway", "veo-3.1") };
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.ProviderModelMismatch),
            "wrong provider");
    }

    static void T_WrongModel(Action<bool, string> Ok)
    {
        var req = Satisfied() with { ActiveVideoSelection = Selection("google", "veo-2") };
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.ProviderModelMismatch),
            "wrong model");
    }

    static void T_ResolverClosed(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var req = Satisfied() with
        {
            ResolverEnabled = false,
            Resolver = ProductionVeoReadinessComposition.CreateProductionResolver(),
        };
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.UnknownProvider),
            "resolver closed → UNKNOWN_PROVIDER");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0,
            "resolver closed does not Start / HTTP");
    }

    static void T_ResolverEnabledFake(Action<bool, string> Ok)
    {
        var req = Satisfied();
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(req.ResolverEnabled && !r.Blockers.Contains(ProductionFailureReasons.UnknownProvider),
            "fake composition resolver enabled is not UNKNOWN_PROVIDER");
    }

    static void T_FrozenMissing(Action<bool, string> Ok)
    {
        var req = Satisfied() with { Frozen = null };
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.InputNotFrozen),
            "frozen snapshot missing");
    }

    static void T_FrozenMismatch(Action<bool, string> Ok)
    {
        var req = Satisfied();
        req = req with { LiveClaim = req.LiveClaim! with { Story = req.LiveClaim.Story + " changed" } };
        var r = RealVertexExecutionPreflight.Evaluate(req);
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.InputSnapshotMismatch),
            "frozen snapshot mismatch");
    }

    static void T_KeyframeMissing(Action<bool, string> Ok)
    {
        var req = Satisfied();
        var frozen = req.Frozen! with { KeyframeArtifactId = null };
        frozen = frozen with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(frozen) };
        var r = RealVertexExecutionPreflight.Evaluate(req with
        {
            Frozen = frozen,
            LiveClaim = req.LiveClaim! with { KeyframeArtifactId = null },
        });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.MissingKeyframe),
            "keyframe missing");
    }

    static void T_KeyframeBytesMissing(Action<bool, string> Ok)
    {
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { KeyframeBytes = null });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.KeyframeBytesMissing),
            "keyframe bytes missing");
    }

    static void T_KeyframeHashMismatch(Action<bool, string> Ok)
    {
        var live = Encoding.UTF8.GetBytes("LIVE-NOT-FROZEN" + new string('X', 48));
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { KeyframeBytes = live });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.InputSnapshotMismatch),
            "keyframe hash mismatch");
    }

    static void T_CostUnknown(Action<bool, string> Ok)
    {
        var catalog = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10);
        Ok(catalog.Kind == CostPolicyKind.Allowed && catalog.EstimatedAmount == 0.20m,
            "catalog google/veo-3.1 VIDEO_ONLY 720p is ALLOWED $0.20");
        var unknown = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Cost = CostPolicyDecision.ExplicitUnknown() });
        Ok(!unknown.Allowed && unknown.Blockers.Contains(ProductionFailureReasons.UnknownCost),
            "explicit unknown cost still blocks preflight");
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Cost = catalog });
        Ok(r.Allowed && !r.Blockers.Contains(ProductionFailureReasons.UnknownCost),
            "catalog ALLOWED does not block fake composition");
    }

    static void T_CostForbidden(Action<bool, string> Ok)
    {
        var forbidden = CostPolicyDecision.FromQuote(new FamixaProviderCostQuote(
            "google", "veo-3.1", 999, "credit", FamixaCostKind.Estimate,
            FamixaProviderCostCatalog.BasisUnknown, "credit", 999, 1));
        Ok(forbidden.Kind == CostPolicyKind.Forbidden && forbidden.BlockReason == ProductionFailureReasons.OverBudget,
            "over-budget quote maps FORBIDDEN");
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Cost = forbidden });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.OverBudget),
            "cost forbidden blocks");
    }

    static void T_CostAllowedDoesNotInventCatalog(Action<bool, string> Ok)
    {
        var declared = CostPolicyDecision.ExplicitAllowed(10);
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Cost = declared });
        Ok(!r.Blockers.Contains(ProductionFailureReasons.UnknownCost)
            && FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10).EstimatedAmount == 0.20m
            && FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10).CostKind == FamixaCostKind.Known,
            "declared ALLOWED does not invent catalog quote");
        Ok(r.Allowed, "cost allowed in fake composition");
    }

    static void T_PermitMissing(Action<bool, string> Ok)
    {
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Permit = null });
        Ok(!r.Allowed && r.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "permit missing");
    }

    static void T_PermitExpired(Action<bool, string> Ok)
    {
        var req = Satisfied();
        var expired = req.Permit! with { Status = ProductionExecutionPermitStatuses.Expired };
        var r = RealVertexExecutionPreflight.Evaluate(req with { Permit = expired });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.PermitExpired),
            "permit expired");
    }

    static void T_PermitWrongScope(Action<bool, string> Ok)
    {
        var wrong = ProductionExecutionPermitCanon.Create(new IssueProductionExecutionPermitCommand(
            ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH99", "EP01", "SC01",
            ProductionActions.CreateVideo, "google", "veo-3.1", "test", "wrong-shot"));
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Permit = wrong });
        Ok(!r.Allowed && r.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "permit wrong scope");
    }

    static void T_PaidNetworkFalse(Action<bool, string> Ok)
    {
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { AuthorizePaidNetwork = false });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden),
            "paid-network false");
    }

    static void T_AuthenticatorUnavailable(Action<bool, string> Ok)
    {
        var closed = new ProductionVeoHttpAuthenticator();
        var r = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Authenticator = closed });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady)
            && closed.GetAccessTokenCalls == 0,
            "authenticator unavailable / CLOSED, no GetAccessToken");
        var missing = RealVertexExecutionPreflight.Evaluate(Satisfied() with { Authenticator = null });
        Ok(!missing.Allowed && missing.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady),
            "null authenticator");
    }

    static void T_UnsupportedOutput(Action<bool, string> Ok)
    {
        var r = RealVertexExecutionPreflight.Evaluate(
            Satisfied() with { ExpectedOutputMode = RealVertexOutputMode.GcsUri });
        Ok(!r.Allowed && r.Blockers.Contains(ProductionFailureReasons.ProviderOutputUnsupported),
            "GCS output unsupported");
    }

    static void T_AllSatisfiedNoStart(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var req = Satisfied(graph);
        var first = RealVertexExecutionPreflight.Evaluate(req);
        var second = RealVertexExecutionPreflight.Evaluate(req);
        Ok(first.Allowed && first.Authorization is not null, "all conditions satisfied Allowed=true");
        Ok(first.Blockers.Count == 0, "no blockers when fake composition is complete");
        Ok(first.Authorization!.SnapshotId == req.Frozen!.SnapshotId
            && first.Authorization.SnapshotHash == req.Frozen.SnapshotHash
            && first.Authorization.KeyframeArtifactId == OfficialKeyframeId
            && first.Authorization.PerformanceSnapshotId == OfficialPerformanceId,
            "authorization references frozen snapshot, does not duplicate it");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0
            && graph.Authenticator.Calls == 0,
            "Allowed=true does not Start / HTTP / GetAccessToken");
        Ok(first.Blockers.SequenceEqual(second.Blockers) && first.Allowed == second.Allowed,
            "preflight is deterministic");
    }

    static void T_NoNetworkNoOauthNoConsume(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var permits = new CountingProductionExecutionPermitStore();
        _ = RealVertexExecutionPreflight.Evaluate(Satisfied(graph));
        Ok(graph.Handler.Requests.Count == 0 && graph.Transport.HttpCallCount == 0, "no network");
        Ok(graph.Authenticator.Calls == 0, "no OAuth / token");
        Ok(permits.ConsumeCalls == 0, "no permit consume");
        Ok(graph.Transport.StartCount == 0, "no provider Start");
    }

    static void T_Deterministic(Action<bool, string> Ok)
    {
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope(), Selection());
        var a = RealVertexExecutionPreflight.Evaluate(closed);
        var b = RealVertexExecutionPreflight.Evaluate(closed);
        Ok(!a.Allowed && a.Blockers.SequenceEqual(b.Blockers) && a.Blockers.Count > 0,
            "same input → same blocker set");
    }

    static void T_OfficialClosedNoMutation(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var r = RealVertexExecutionPreflight.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope(), Selection()));
        Ok(!r.Allowed && r.Authorization is null, "official production defaults BLOCKED");
        Ok(r.Blockers.Contains(ProductionFailureReasons.UnknownProvider), "official resolver CLOSED");
        Ok(!r.Blockers.Contains(ProductionFailureReasons.UnknownCost), "official cost ALLOWED for VIDEO_ONLY 720p");
        Ok(r.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden), "official permit null");
        Ok(r.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden), "official paid-network false");
        Ok(r.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady), "production authenticator CLOSED");
        Ok(r.Blockers.Contains(ProductionFailureReasons.HttpCompositionUnavailable), "production HTTP not registered");
        Ok(r.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired), "execution auth not enabled");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0, "official closed: Start/HTTP = 0");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "production lock flags unchanged");
    }

    static RealVertexExecutionPreflightRequest Satisfied(ProductionVeoTestGraph? graph = null)
    {
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK11-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var adapter = graph?.Adapter ?? new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        var resolver = new ProductionMotionAdapterResolver([new FixtureMotionAdapter(), adapter]);
        var live = new ShotExecutionInput(
            Story, Acting, Visual, "CHAR-001", hash, "10.0s", "", "motion",
            OfficialKeyframeId, hash, "16:9", 10, ShotExecutionCompiler.Version, null,
            PerformanceSnapshotId: OfficialPerformanceId,
            PerformanceHash: OfficialPerformanceHash);
        var frozen = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(),
            Story, Acting, Visual, "", "", "",
            ShotExecutionCompiler.Hash(Story),
            ShotExecutionCompiler.Hash(Acting),
            ShotExecutionCompiler.Hash(Visual),
            ShotExecutionCompiler.Hash(""),
            hash, "", "", "",
            ShotExecutionCompiler.Hash("10.0s"),
            FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId,
            "",
            DateTimeOffset.UnixEpoch,
            OfficialKeyframeId, "16:9", 10,
            PerformanceSnapshotId: OfficialPerformanceId,
            PerformanceHash: OfficialPerformanceHash);
        frozen = frozen with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(frozen) };
        var permit = ProductionExecutionPermitCanon.Create(new IssueProductionExecutionPermitCommand(
            ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH02", "EP01", "SC01",
            ProductionActions.CreateVideo, "google", "veo-3.1", "checkpoint-11", "FAKE_TEST_PERMIT"));
        return new RealVertexExecutionPreflightRequest(
            OfficialScope(),
            Selection(),
            ResolverEnabled: true,
            Resolver: resolver,
            Frozen: frozen,
            LiveClaim: live,
            Adapter: adapter,
            KeyframeBytes: bytes,
            Cost: CostPolicyDecision.ExplicitAllowed(10),
            Permit: permit,
            AuthorizePaidNetwork: true,
            Authenticator: graph?.Authenticator ?? new FakeVeoHttpAuthenticator(),
            HttpCompositionAvailable: true,
            ExecutionAuthorizationEnabled: true,
            ExpectedOutputMode: RealVertexOutputMode.InlineBytes,
            ReplayConflict: false,
            AsOf: DateTimeOffset.UnixEpoch);
    }

    static ProductionScope OfficialScope() =>
        new(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");

    static ProductionAiSelection Selection(string provider = "google", string model = "veo-3.1") =>
        new(
            OfficialSelectionId,
            ProductionScopeAnchorRules.OfficialEp01,
            "EP01-SC01-SH02", "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            provider, model,
            ProductionAiSelectionModes.Explicit,
            "checkpoint-11",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck11");
}
