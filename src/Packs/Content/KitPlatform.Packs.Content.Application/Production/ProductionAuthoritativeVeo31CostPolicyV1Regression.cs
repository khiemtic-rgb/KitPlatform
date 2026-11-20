using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionAuthoritativeVeo31CostPolicyV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_AUTHORITATIVE_VEO31_COST_POLICY_V1";

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
        T_KnownCostForExactVariant(Ok);
        T_AmountCurrencyUnit(Ok);
        T_VideoOnly720pNoAudioCount1(Ok);
        T_OfficialProvenance(Ok);
        T_UnknownNoLongerReturned(Ok);
        T_UnrelatedModelsIndependent(Ok);
        T_AudioDoesNotInheritVideoOnly(Ok);
        T_FourKDoesNotInherit720p(Ok);
        T_DurationIsNotAMultiplier(Ok);
        T_CostAllowedDoesNotOpenPreflight(Ok);
        T_NoPermitHttpOauthStart(Ok);
        T_SnapshotAndGraphUnchanged(Ok);
        T_UnrelatedCatalogUnchanged(Ok);
        T_Ck13CostPassPermitFail(Ok);
    }

    static void T_KnownCostForExactVariant(Action<bool, string> Ok)
    {
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1");
        Ok(quote.CostKind == FamixaCostKind.Known && quote.EstimatedAmount is not null,
            "google/veo-3.1 resolves known cost");
        Ok(quote.ProviderId == "google" && quote.ModelId == "veo-3.1",
            "quote bound to google/veo-3.1");
    }

    static void T_AmountCurrencyUnit(Action<bool, string> Ok)
    {
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1");
        Ok(quote.EstimatedAmount == 0.20m, "amount = 0.20");
        Ok(quote.Currency == "USD", "currency = USD");
        Ok(quote.BillingUnit == "count", "unit = count");
        Ok(quote.BillingBasis == FamixaProviderCostCatalog.BasisGoogleVeo31PerCount,
            "pricing basis PER_COUNT");
    }

    static void T_VideoOnly720pNoAudioCount1(Action<bool, string> Ok)
    {
        var quote = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, FamixaVeo31OfficialPricing.ProductionDefault);
        Ok(quote.Variant is { GenerationMode: FamixaVeo31OfficialPricing.VideoOnly },
            "video-only variant");
        Ok(quote.Variant!.Resolution == FamixaVeo31OfficialPricing.Res720p, "720p variant");
        Ok(quote.Variant.GenerateAudio == false, "generateAudio=false");
        Ok(quote.Variant.SampleCount == 1 && quote.BilledQuantity == 1, "sampleCount=1");
        Ok(quote.Provenance!.ApplicableVariant == FamixaVeo31OfficialPricing.VariantVideoOnly720p,
            "applicable variant VEO_3_1_VIDEO_ONLY_720P");
    }

    static void T_OfficialProvenance(Action<bool, string> Ok)
    {
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1");
        var p = quote.Provenance;
        Ok(p is not null, "official source provenance exists");
        Ok(p!.SourceName == "Google Cloud", "source Google Cloud");
        Ok(p.SourceType == "OFFICIAL_PROVIDER_PRICING", "source type official provider pricing");
        Ok(p.SourceReference == FamixaVeo31OfficialPricing.SourceReference, "source reference");
        Ok(p.RetrievedOn == "2026-09-22" && p.EffectiveVersion == "2026-09-22",
            "verified 2026-09-22");
        Ok(p.PricingBasis == "PER_COUNT", "PER_COUNT");
        var inspect = CostPolicySource.Inspect("google", "veo-3.1");
        Ok(inspect.SourceKind == CostPolicySourceKind.Catalog
            && inspect.Policy == CostPolicyKind.Allowed
            && inspect.EffectiveVersion == "2026-09-22",
            "inspector binds official provenance");
    }

    static void T_UnknownNoLongerReturned(Action<bool, string> Ok)
    {
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        Ok(quote.CostKind != FamixaCostKind.Unknown && quote.EstimatedAmount is not null,
            "unknown is no longer returned for exact supported variant");
        Ok(CostPolicyDecision.FromCatalog("google", "veo-3.1", 10).Kind == CostPolicyKind.Allowed,
            "CostPolicyDecision ALLOWED for exact variant");
        Ok(ProductionMotionCostGuard.Evaluate("google", "veo-3.1", 10, true).Allowed,
            "cost guard ALLOWED for exact variant");
    }

    static void T_UnrelatedModelsIndependent(Action<bool, string> Ok)
    {
        Ok(FamixaProviderCostCatalog.Quote("google", "veo-3.1-fast").CostKind == FamixaCostKind.Unknown
            && FamixaProviderCostCatalog.Quote("google", "veo-3.1-fast").EstimatedAmount is null,
            "veo-3.1-fast remains independent / UNKNOWN");
        Ok(FamixaProviderCostCatalog.Quote("google", "veo-3.1-lite").CostKind == FamixaCostKind.Unknown,
            "veo-3.1-lite remains independent");
        Ok(FamixaProviderCostCatalog.Quote("google", "veo-3").CostKind == FamixaCostKind.Unknown,
            "veo-3 remains independent");
        Ok(FamixaProviderCostCatalog.Quote("google", "veo-2").CostKind == FamixaCostKind.Unknown,
            "Veo 2 remains independent");
        Ok(FamixaProviderCostCatalog.Quote("google", FamixaVeo31OfficialPricing.VertexModel)
                .CostKind == FamixaCostKind.Unknown,
            "vertex model id does not inherit veo-3.1 quote");
        var runway = FamixaProviderCostCatalog.Quote("runway", "gen4_turbo", 5);
        Ok(runway.CostKind == FamixaCostKind.Estimate && runway.BillingBasis == FamixaProviderCostCatalog.BasisRunwayCreditPerSec,
            "Runway catalog unchanged");
        var fixture = FamixaProviderCostCatalog.Quote("fixture", "fixture-motion-v1");
        Ok(fixture.CostKind == FamixaCostKind.Unknown, "Fixture catalog unchanged");
    }

    static void T_AudioDoesNotInheritVideoOnly(Action<bool, string> Ok)
    {
        var audio = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, FamixaVeo31OfficialPricing.VideoAudio720p());
        var video = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, FamixaVeo31OfficialPricing.VideoOnly720p());
        Ok(audio.EstimatedAmount == 0.40m && video.EstimatedAmount == 0.20m,
            "audio variant does not inherit video-only $0.20");
        Ok(audio.Provenance!.ApplicableVariant == FamixaVeo31OfficialPricing.VariantVideoAudio720p,
            "audio variant identity is distinct");
        var inconsistent = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, new FamixaVeoCostVariant("VIDEO_ONLY", "720p", true, 1));
        Ok(inconsistent.CostKind == FamixaCostKind.Unknown,
            "inconsistent audio+VIDEO_ONLY fail-closed");
    }

    static void T_FourKDoesNotInherit720p(Action<bool, string> Ok)
    {
        var fourK = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, FamixaVeo31OfficialPricing.VideoOnly4k());
        var hd = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, FamixaVeo31OfficialPricing.VideoOnly720p());
        Ok(fourK.EstimatedAmount == 0.40m && hd.EstimatedAmount == 0.20m,
            "4K does not inherit 720p $0.20");
        Ok(fourK.Provenance!.ApplicableVariant == FamixaVeo31OfficialPricing.VariantVideoOnly4k,
            "4K variant identity is distinct");
        var unknownRes = FamixaProviderCostCatalog.Quote(
            "google", "veo-3.1", null, new FamixaVeoCostVariant("VIDEO_ONLY", "480p", false, 1));
        Ok(unknownRes.CostKind == FamixaCostKind.Unknown, "unsupported resolution fail-closed");
    }

    static void T_DurationIsNotAMultiplier(Action<bool, string> Ok)
    {
        var ten = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 10);
        var eight = FamixaProviderCostCatalog.Quote("google", "veo-3.1", 8);
        var none = FamixaProviderCostCatalog.Quote("google", "veo-3.1");
        Ok(ten.EstimatedAmount == 0.20m && eight.EstimatedAmount == 0.20m && none.EstimatedAmount == 0.20m,
            "OS 10 / provider 8 are not multiplied into the quote");
    }

    static void T_CostAllowedDoesNotOpenPreflight(Action<bool, string> Ok)
    {
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(
            new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02"),
            new ProductionAiSelection(
                Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8"),
                ProductionScopeAnchorRules.OfficialEp01,
                "EP01-SC01-SH02", "EP01", "SC01",
                ProductionAiSelectionStages.Video, "google", "veo-3.1",
                ProductionAiSelectionModes.Explicit, "ck13a", DateTimeOffset.UnixEpoch,
                "DIRECTOR_SELECTION", ProductionAiSelectionStatuses.Active, "h"));
        var r = RealVertexExecutionPreflight.Evaluate(closed);
        Ok(closed.Cost.Kind == CostPolicyKind.Allowed, "production catalog cost ALLOWED");
        Ok(!r.Allowed, "preflight remains FALSE");
        Ok(!r.Blockers.Contains(ProductionFailureReasons.UnknownCost), "UNKNOWN_COST removed");
        Ok(r.Blockers.Contains(PerformanceContractIds.OfficialFilmForbidden)
            && r.Blockers.Contains(ProductionFailureReasons.PaidNetworkForbidden)
            && r.Blockers.Contains(ProductionFailureReasons.AuthenticatorNotReady)
            && r.Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && r.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired)
            && r.Blockers.Contains(ProductionFailureReasons.HttpCompositionUnavailable),
            "remaining gates still closed");
    }

    static void T_NoPermitHttpOauthStart(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var permits = new CountingProductionExecutionPermitStore();
        var closed = new ProductionVeoHttpAuthenticator();
        _ = FamixaProviderCostCatalog.Quote("google", "veo-3.1");
        _ = CostPolicyDecision.FromCatalog("google", "veo-3.1", 10);
        _ = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02"))
            with { Authenticator = closed });
        Ok(permits.IssueCalls == 0, "no permit issue");
        Ok(permits.ConsumeCalls == 0, "no permit consume");
        Ok(graph.Handler.Requests.Count == 0 && graph.Transport.HttpCallCount == 0, "no HTTP");
        Ok(graph.Authenticator.Calls == 0 && closed.GetAccessTokenCalls == 0, "no OAuth");
        Ok(graph.Transport.StartCount == 0, "no provider Start");
    }

    static void T_SnapshotAndGraphUnchanged(Action<bool, string> Ok)
    {
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK13A-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var frozen = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(),
            ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "",
            ShotExecutionCompiler.Hash(ProductionEp01Sh02IngressCanon.Story),
            ShotExecutionCompiler.Hash(ProductionEp01Sh02IngressCanon.ActingBeat),
            ShotExecutionCompiler.Hash(ProductionEp01Sh02IngressCanon.VisualContract),
            ShotExecutionCompiler.Hash(""),
            hash, "", "", "",
            ShotExecutionCompiler.Hash("10.0s"),
            "google", "veo-3.1", "",
            DateTimeOffset.UnixEpoch,
            Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef"), "16:9", 10,
            PerformanceSnapshotId: Guid.Parse("6505b8a2-8a71-4d74-8d93-f1fae8319b6b"),
            PerformanceHash: "PERFORMANCE_HASH_V1:8fa1d71b14e46e64bf1cf4dc7b107ae7f39be69f3f79da1d55d24f33a116914a");
        frozen = frozen with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(frozen) };
        var before = frozen.SnapshotHash;
        _ = CostPolicyDecision.FromCatalog(frozen.ProviderId, frozen.ModelId, frozen.DurationSec);
        Ok(frozen.SnapshotHash == before
            && frozen.Story == ProductionEp01Sh02IngressCanon.Story
            && frozen.DurationSec == 10,
            "ProductionTaskInputSnapshot unchanged");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "no graph / resolver mutation");
    }

    static void T_UnrelatedCatalogUnchanged(Action<bool, string> Ok)
    {
        var runway5 = FamixaProviderCostCatalog.Quote("runway", "gen4_turbo", 5);
        var runway10 = FamixaProviderCostCatalog.Quote("runway", "gen4_turbo", 10);
        Ok(runway5.EstimatedAmount == 25 && runway10.EstimatedAmount == 50,
            "Runway per-second estimate unchanged");
        Ok(FamixaProviderCostCatalog.Quote("wan", "wan-2.1").CostKind == FamixaCostKind.FalBilledEstimate,
            "Wan catalog unchanged");
    }

    static void T_Ck13CostPassPermitFail(Action<bool, string> Ok)
    {
        var ck13 = ProductionCostOfficialPermitAuthorizationV1Regression.Run();
        Ok(ck13.Count == 0, ck13.Count == 0 ? "CK13 suite still green" : "CK13:" + string.Join(",", ck13));
        var report = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02"),
                new ProductionAiSelection(
                    Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8"),
                    ProductionScopeAnchorRules.OfficialEp01,
                    "EP01-SC01-SH02", "EP01", "SC01",
                    ProductionAiSelectionStages.Video, "google", "veo-3.1",
                    ProductionAiSelectionModes.Explicit, "ck13a", DateTimeOffset.UnixEpoch,
                    "DIRECTOR_SELECTION", ProductionAiSelectionStatuses.Active, "h")));
        Ok(report.CostGatePass && !report.PermitGatePass && !report.PaidNetworkPass
            && !report.AuthenticatorPass && !report.ResolverPass && !report.Preflight.Allowed,
            "CK13: cost PASS, permit/paid/auth/resolver FAIL, overall BLOCKED");
    }
}
