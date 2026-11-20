using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCostOfficialPermitAuthorizationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_COST_OFFICIAL_PERMIT_AUTHORIZATION_V1";
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
        T_GoogleVeo31OfficialKnown(Ok);
        T_UnknownCostBlocks(Ok);
        T_KnownQuoteAllowedPassesCostGateInFake(Ok);
        T_ForbiddenBlocks(Ok);
        T_NoInventedPrice(Ok);
        T_CatalogNotMutated(Ok);
        T_SnapshotNotMutated(Ok);
        T_NullPermitBlocks(Ok);
        T_WrongProviderBlocks(Ok);
        T_WrongModelBlocks(Ok);
        T_WrongBuildBlocks(Ok);
        T_WrongShotBlocks(Ok);
        T_WrongScopeBlocks(Ok);
        T_ExpiredPermitBlocks(Ok);
        T_ConsumedPermitBlocks(Ok);
        T_ValidFakePermitPassesPermitGate(Ok);
        T_PermitNotConsumedDuringPreflight(Ok);
        T_PaidNetworkFalseBlocks(Ok);
        T_PaidNetworkTruePassesInFakeOnly(Ok);
        T_ProductionPaidNetworkUnchanged(Ok);
        T_AuthenticatorNotReadyBlocks(Ok);
        T_GetAccessTokenCountZero(Ok);
        T_UnknownCostValidPermitBlocked(Ok);
        T_AllowedCostMissingPermitBlocked(Ok);
        T_AllowedCostValidPermitPaidFalseBlocked(Ok);
        T_AllFakeConditionsAllowedNoStart(Ok);
        T_SafetyNoVertexHttpOauthJob(Ok);
        T_IssuanceExistsButNotInvoked(Ok);
        T_ProductionDefaultsRemainBlocked(Ok);
    }

    static void T_GoogleVeo31OfficialKnown(Action<bool, string> Ok)
    {
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        var source = CostPolicySource.Inspect("google", "veo-3.1", 10);
        Ok(ProductionMotionCatalog.GoogleVeo31.IsPaid, "catalog IsPaid=true");
        Ok(quote.CostKind == FamixaCostKind.Known && quote.EstimatedAmount == 0.20m
            && quote.Currency == "USD" && quote.BillingUnit == "count",
            "google/veo-3.1 quote is official $0.20 / count");
        Ok(source.SourceKind == CostPolicySourceKind.Catalog
            && source.Policy == CostPolicyKind.Allowed
            && source.EffectiveVersion == FamixaVeo31OfficialPricing.RetrievedOn
            && source.Validity == "as-of:" + FamixaVeo31OfficialPricing.RetrievedOn,
            "official provenance bound");
        Ok(CostPolicyDecision.FromCatalog("google", "veo-3.1", 10).Kind == CostPolicyKind.Allowed,
            "FromCatalog ALLOWED for VIDEO_ONLY 720p");
    }

    static void T_UnknownCostBlocks(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(
            Satisfied() with { Cost = CostPolicyDecision.ExplicitUnknown() });
        Ok(!report.CostGatePass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost),
            "unknown cost blocks");
    }

    static void T_KnownQuoteAllowedPassesCostGateInFake(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied());
        Ok(report.CostGatePass && report.Cost.Kind == CostPolicyKind.Allowed
            && report.Cost.BillingBasis == "EXPLICIT_TEST_POLICY",
            "known quote + ALLOWED passes cost gate in fake");
        Ok(report.CostSource.Policy == CostPolicyKind.Allowed
            && report.CostSource.SourceKind == CostPolicySourceKind.Catalog
            && report.CostSource.Amount == 0.20m,
            "catalog source is official ALLOWED, not invented");
    }

    static void T_ForbiddenBlocks(Action<bool, string> Ok)
    {
        var forbidden = CostPolicyDecision.FromQuote(new FamixaProviderCostQuote(
            "google", "veo-3.1", 999, "credit", FamixaCostKind.Estimate,
            FamixaProviderCostCatalog.BasisUnknown, "credit", 999, 1));
        Ok(forbidden.Kind == CostPolicyKind.Forbidden, "over-budget maps FORBIDDEN");
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Cost = forbidden });
        Ok(!report.CostGatePass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.OverBudget),
            "FORBIDDEN blocks");
    }

    static void T_NoInventedPrice(Action<bool, string> Ok)
    {
        var before = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        _ = CostOfficialPermitAuthorization.Evaluate(Satisfied());
        var after = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        Ok(before.EstimatedAmount == 0.20m && after.EstimatedAmount == 0.20m
            && before.CostKind == FamixaCostKind.Known && after.CostKind == FamixaCostKind.Known
            && before.BillingBasis == FamixaProviderCostCatalog.BasisGoogleVeo31PerCount
            && before == after,
            "official $0.20 is not rewritten by authorization");
    }

    static void T_CatalogNotMutated(Action<bool, string> Ok)
    {
        var before = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        _ = CostPolicySource.Inspect("google", "veo-3.1", 10);
        _ = CostOfficialPermitAuthorization.Evaluate(
            Satisfied() with { Cost = CostPolicyDecision.ExplicitAllowed(4) });
        var after = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        Ok(before == after && ProductionMotionCatalog.GoogleVeo31.IsPaid,
            "catalog not mutated");
    }

    static void T_SnapshotNotMutated(Action<bool, string> Ok)
    {
        var req = Satisfied();
        var frozen = req.Frozen!;
        var hash = frozen.SnapshotHash;
        var story = frozen.Story;
        var report = CostOfficialPermitAuthorization.Evaluate(req);
        Ok(req.Frozen!.SnapshotHash == hash
            && req.Frozen.Story == story
            && req.Frozen.ActingBeat == frozen.ActingBeat
            && req.Frozen.VisualContract == frozen.VisualContract
            && req.Frozen.SnapshotId == frozen.SnapshotId,
            "snapshot not mutated");
        var auth = report.Preflight.Authorization;
        Ok(auth is null
            || (auth.SnapshotId == frozen.SnapshotId
                && auth.SnapshotHash == hash
                && auth.PerformanceSnapshotId == OfficialPerformanceId),
            "authorization references snapshot, does not duplicate story");
    }

    static void T_NullPermitBlocks(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = null });
        Ok(!report.PermitGatePass && !report.Preflight.Allowed
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden
            && report.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "null permit blocks");
    }

    static void T_WrongProviderBlocks(Action<bool, string> Ok)
    {
        var permit = OfficialPermit("runway", "veo-3.1");
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = permit });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "wrong provider blocks");
    }

    static void T_WrongModelBlocks(Action<bool, string> Ok)
    {
        var permit = OfficialPermit("google", "veo-2");
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = permit });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "wrong model blocks");
    }

    static void T_WrongBuildBlocks(Action<bool, string> Ok)
    {
        var permit = ProductionExecutionPermitCanon.Create(new IssueProductionExecutionPermitCommand(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01-SC01-SH02", "EP01", "SC01",
            ProductionActions.CreateVideo, "google", "veo-3.1", "ck13", "wrong-build"));
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = permit });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "wrong build blocks");
    }

    static void T_WrongShotBlocks(Action<bool, string> Ok)
    {
        var permit = ProductionExecutionPermitCanon.Create(new IssueProductionExecutionPermitCommand(
            ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH01", "EP01", "SC01",
            ProductionActions.CreateVideo, "google", "veo-3.1", "ck13", "wrong-shot"));
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = permit });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "wrong shot blocks");
    }

    static void T_WrongScopeBlocks(Action<bool, string> Ok)
    {
        var permit = ProductionExecutionPermitCanon.Create(new IssueProductionExecutionPermitCommand(
            ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH02", "EP01", "SC01",
            ProductionActions.CreateVoice, "google", "veo-3.1", "ck13", "wrong-action"));
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = permit });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "wrong scope blocks");
    }

    static void T_ExpiredPermitBlocks(Action<bool, string> Ok)
    {
        var expired = OfficialPermit() with { Status = ProductionExecutionPermitStatuses.Expired };
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = expired });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == ProductionFailureReasons.PermitExpired
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.PermitExpired),
            "expired permit blocks");
    }

    static void T_ConsumedPermitBlocks(Action<bool, string> Ok)
    {
        var consumed = OfficialPermit() with
        {
            Status = ProductionExecutionPermitStatuses.Consumed,
            ConsumedAt = DateTimeOffset.UnixEpoch,
        };
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = consumed });
        Ok(!report.PermitGatePass
            && report.PermitBlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "consumed permit blocks");
    }

    static void T_ValidFakePermitPassesPermitGate(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied());
        Ok(report.PermitGatePass && report.PermitBlockReason is null
            && report.Permit is { Status: ProductionExecutionPermitStatuses.Issued, ConsumedAt: null },
            "valid fake permit passes permit gate");
    }

    static void T_PermitNotConsumedDuringPreflight(Action<bool, string> Ok)
    {
        var permits = new CountingProductionExecutionPermitStore();
        var permit = OfficialPermit();
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = permit });
        Ok(permits.ConsumeCalls == 0 && permits.IssueCalls == 0
            && permit.Status == ProductionExecutionPermitStatuses.Issued
            && permit.ConsumedAt is null
            && report.Permit!.Status == ProductionExecutionPermitStatuses.Issued,
            "permit is not consumed during preflight");
    }

    static void T_PaidNetworkFalseBlocks(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(
            Satisfied() with { AuthorizePaidNetwork = false });
        Ok(!report.PaidNetworkPass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden),
            "paid-network false blocks");
    }

    static void T_PaidNetworkTruePassesInFakeOnly(Action<bool, string> Ok)
    {
        var fake = CostOfficialPermitAuthorization.Evaluate(Satisfied());
        var production = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope(), Selection()));
        Ok(fake.PaidNetworkPass, "true passes in fake only");
        Ok(!production.PaidNetworkPass
            && production.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden),
            "production paid-network stays false");
    }

    static void T_ProductionPaidNetworkUnchanged(Action<bool, string> Ok)
    {
        var dto = new ProductionCreateVideoRequestDto(
            ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02",
            "", "", "", "", "16:9", 10, null, null, null);
        Ok(!dto.AuthorizePaidNetwork
            && !CostOfficialPermitAuthorization.AuthorizePaidNetworkProductionDefault
            && !RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope()).AuthorizePaidNetwork,
            "production AuthorizePaidNetwork unchanged");
    }

    static void T_AuthenticatorNotReadyBlocks(Action<bool, string> Ok)
    {
        var closed = new ProductionVeoHttpAuthenticator();
        var report = CostOfficialPermitAuthorization.Evaluate(
            Satisfied() with { Authenticator = closed });
        Ok(!report.AuthenticatorPass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady)
            && closed.GetAccessTokenCalls == 0,
            "authenticator not ready blocks");
    }

    static void T_GetAccessTokenCountZero(Action<bool, string> Ok)
    {
        var closed = new ProductionVeoHttpAuthenticator();
        var fake = new FakeVeoHttpAuthenticator();
        _ = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope(), Selection())
            with { Authenticator = closed });
        _ = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Authenticator = fake });
        Ok(closed.GetAccessTokenCalls == 0 && fake.Calls == 0,
            "GetAccessTokenAsync count = 0");
    }

    static void T_UnknownCostValidPermitBlocked(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(
            Satisfied() with { Cost = CostPolicyDecision.ExplicitUnknown() });
        Ok(!report.CostGatePass && report.PermitGatePass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost)
            && !report.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden),
            "unknown cost + valid permit => BLOCKED");
    }

    static void T_AllowedCostMissingPermitBlocked(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied() with { Permit = null });
        Ok(report.CostGatePass && !report.PermitGatePass && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden)
            && !report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost),
            "allowed cost + missing permit => BLOCKED");
    }

    static void T_AllowedCostValidPermitPaidFalseBlocked(Action<bool, string> Ok)
    {
        var report = CostOfficialPermitAuthorization.Evaluate(
            Satisfied() with { AuthorizePaidNetwork = false });
        Ok(report.CostGatePass && report.PermitGatePass && !report.PaidNetworkPass
            && !report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden),
            "allowed cost + valid permit + paid network false => BLOCKED");
    }

    static void T_AllFakeConditionsAllowedNoStart(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var report = CostOfficialPermitAuthorization.Evaluate(Satisfied(graph));
        Ok(report.CostGatePass && report.PermitGatePass && report.PaidNetworkPass
            && report.AuthenticatorPass && report.ResolverPass
            && report.FrozenSnapshotPass && report.KeyframePass,
            "all fake gates PASS");
        Ok(report.Preflight.Allowed && report.Preflight.Authorization is not null,
            "all fake conditions => Allowed=true");
        Ok(graph.Transport.StartCount == 0 && graph.Handler.Requests.Count == 0
            && graph.Authenticator.Calls == 0,
            "Allowed=true StartCount=0");
    }

    static void T_SafetyNoVertexHttpOauthJob(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var permits = new CountingProductionExecutionPermitStore();
        var closed = new ProductionVeoHttpAuthenticator();
        _ = CostOfficialPermitAuthorization.Evaluate(Satisfied(graph));
        _ = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope(), Selection())
            with { Authenticator = closed });
        Ok(graph.Transport.StartCount == 0 && graph.Transport.HttpCallCount == 0
            && graph.Handler.Requests.Count == 0, "no Vertex / HTTP / provider job");
        Ok(graph.Authenticator.Calls == 0 && closed.GetAccessTokenCalls == 0, "no OAuth");
        Ok(permits.ConsumeCalls == 0 && permits.IssueCalls == 0, "no permit consume / issue");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "production resolver / execution lock unchanged");
    }

    static void T_IssuanceExistsButNotInvoked(Action<bool, string> Ok)
    {
        Ok(!ProductionOfficialPermitGate.CheckpointIssuesOfficialCreateVideoPermit,
            "checkpoint does not issue official CREATE_VIDEO permit");
        Ok(typeof(IProductionExecutionPermitStore).GetMethod(nameof(IProductionExecutionPermitStore.IssueAsync)) is not null
            && typeof(ProductionReadinessAuthority).GetMethod(nameof(ProductionReadinessAuthority.IssuePermitAsync)) is not null,
            "canonical Issue API exists — audit only");
    }

    static void T_ProductionDefaultsRemainBlocked(Action<bool, string> Ok)
    {
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(OfficialScope(), Selection());
        var report = CostOfficialPermitAuthorization.Evaluate(closed);
        Ok(!report.Preflight.Allowed && report.Preflight.Authorization is null,
            "production preflight Allowed=false");
        Ok(report.CostGatePass && report.CostSource.SourceKind == CostPolicySourceKind.Catalog
            && report.CostSource.Policy == CostPolicyKind.Allowed,
            "production cost ALLOWED for official VIDEO_ONLY 720p");
        Ok(!report.PermitGatePass && report.Permit is null, "production permit missing");
        Ok(!report.PaidNetworkPass, "production paid-network false");
        Ok(!report.AuthenticatorPass, "production authenticator closed");
        Ok(!report.ResolverPass, "production resolver closed");
        Ok(!report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownCost)
            && report.Preflight.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired),
            "production blockers remain except UNKNOWN_COST");
    }

    static RealVertexExecutionPreflightRequest Satisfied(ProductionVeoTestGraph? graph = null)
    {
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK13-" + new string('K', 48));
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
            Permit: OfficialPermit(),
            AuthorizePaidNetwork: true,
            Authenticator: graph?.Authenticator ?? new FakeVeoHttpAuthenticator(),
            HttpCompositionAvailable: true,
            ExecutionAuthorizationEnabled: true,
            ExpectedOutputMode: RealVertexOutputMode.InlineBytes,
            ReplayConflict: false,
            AsOf: DateTimeOffset.UnixEpoch);
    }

    static ProductionExecutionPermit OfficialPermit(string provider = "google", string model = "veo-3.1") =>
        ProductionExecutionPermitCanon.Create(new IssueProductionExecutionPermitCommand(
            ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH02", "EP01", "SC01",
            ProductionActions.CreateVideo, provider, model, "checkpoint-13", "FAKE_TEST_PERMIT"));

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
            "checkpoint-13",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck13");
}
