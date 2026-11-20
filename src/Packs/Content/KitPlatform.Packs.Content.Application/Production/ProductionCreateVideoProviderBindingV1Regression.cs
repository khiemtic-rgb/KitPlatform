using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionCreateVideoProviderBindingV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_CREATE_VIDEO_PROVIDER_BINDING_V1";
    const string VeoModel = "veo-3.1";
    const string AltProvider = "acme-bind";
    const string AltModel = "acme-motion-v1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        await T_OfficialGoogleVeoFailsClosedNoFixture(Ok);
        await T_OfficialSelectionXCarriesIntoPlan(Ok);
        await T_FrozenSnapshotMatchesAuthoritativeSelection(Ok);
        await T_TestPathFixtureUnchanged(Ok);
        await T_OfficialNoSelectionPreserved(Ok);
        await T_CommandFixtureCannotOverrideSelection(Ok);
    }

    static async Task T_OfficialGoogleVeoFailsClosedNoFixture(Action<bool, string> Ok)
    {
        var h = await SeedOfficial(FamixaProviderIds.Google, VeoModel);
        var live = LiveOf(h.Input);
        var failed = false;
        try
        {
            await h.Svc.CreateVideoAsync(new CreateVideoApiCommand(
                h.Scope, live, "test", "ck6-google", null, null,
                ProviderId: ProductionMotionCatalog.FixtureProviderId,
                ModelId: ProductionMotionCatalog.FixtureModelId));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionFailureReasons.UnknownProvider, StringComparison.Ordinal))
        {
            failed = true;
        }

        var tasks = await h.Store.ListTasksByScopeAsync(h.Scope.BuildId, h.Scope.ShotCode);
        var prior = await h.Store.GetRequestByIdempotencyAsync("ck6-google");
        Ok(failed, "official google/veo-3.1 + no adapter FAIL CLOSED UNKNOWN_PROVIDER");
        Ok(prior is null && tasks.All(t => t.Action != ProductionActions.CreateVideo),
            "official google/veo-3.1 does not create CREATE_VIDEO request");
        Ok(ProductionMotionCatalog.TryGet(FamixaProviderIds.Google, VeoModel, out var catalog)
            && catalog.ProviderId == FamixaProviderIds.Google
            && catalog.ModelId == VeoModel
            && catalog.IsPaid
            && catalog.ProviderId != ProductionMotionCatalog.FixtureProviderId
            && catalog.ModelId != ProductionMotionCatalog.FixtureModelId,
            "google/veo-3.1 is not a catalog alias for Fixture");
        var bound = ProductionCreateVideoProviderBinding.Authoritative(
            true, h.Selection, ProductionMotionCatalog.FixtureProviderId, ProductionMotionCatalog.FixtureModelId, h.Motion);
        Ok(bound.ProviderId == FamixaProviderIds.Google && bound.ModelId == VeoModel,
            "authoritative bind stays google/veo-3.1");
    }

    static async Task T_OfficialSelectionXCarriesIntoPlan(Action<bool, string> Ok)
    {
        var google = await SeedOfficial(FamixaProviderIds.Google, VeoModel);
        var googleVerdict = await google.Os.EvaluateCreateVideoAsync(google.Scope, Claim(google.Input));
        Ok(googleVerdict.ProviderId == FamixaProviderIds.Google && googleVerdict.ModelId == VeoModel,
            "plan carries official google/veo-3.1");

        var x = await SeedOfficial(AltProvider, AltModel);
        var plan = ProductionCreateVideoProviderBinding.Authoritative(
            true, x.Selection, "fixture", "fixture-motion-v1", x.Motion);
        var verdict = await x.Os.EvaluateCreateVideoAsync(x.Scope, Claim(x.Input) with
        {
            ProviderId = "fixture",
            ModelId = "fixture-motion-v1",
        });
        Ok(plan.ProviderId == AltProvider && plan.ModelId == AltModel, "plan bind carries X/X-model");
        Ok(verdict.ProviderId == AltProvider && verdict.ModelId == AltModel, "Evaluate plan carries X/X-model");
    }

    static async Task T_FrozenSnapshotMatchesAuthoritativeSelection(Action<bool, string> Ok)
    {
        var h = await SeedOfficial(FamixaProviderIds.Google, VeoModel);
        var bound = ProductionCreateVideoProviderBinding.Authoritative(
            true, h.Selection, "fixture", "fixture-motion-v1", h.Motion);
        var req = await h.Os.CreateRequestAsync(h.Scope, ProductionActions.CreateVideo, "test", "ck6-freeze");
        var task = await h.Os.CreateTaskAsync(req.RequestId);
        var frozen = await new ProductionWorkExecutionEngine(h.Store)
            .FreezeInputAsync(task.TaskId, h.Input, bound.ProviderId, bound.ModelId);
        Ok(frozen.ProviderId == FamixaProviderIds.Google && frozen.ModelId == VeoModel,
            "frozen ProviderId/ModelId == ACTIVE google/veo-3.1");
        Ok(bound.ProviderId == h.Selection.ProviderId && bound.ModelId == h.Selection.ModelId,
            "freeze IDs come from ACTIVE selection");
        var attempts = await h.Store.ListAttemptsAsync(task.TaskId);
        Ok(attempts.Count == 0, "freeze-only: no attempt / provider job");
    }

    static async Task T_TestPathFixtureUnchanged(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, motion);
        var scope = new ProductionScope(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "EP99", "SC01", "EP99-SC01-SH99");
        var snap = await ProductionTestPerformanceFixture.FreezeAsync(perf, scope.BuildId, scope.ShotCode);
        var kf = await SeedKeyframe(os, scope, Attach(Input(10), snap));
        var input = Attach(Input(10) with
        {
            KeyframeArtifactId = kf.ArtifactId,
            KeyframeByteSha256 = kf.ByteSha256,
            KeyframeHash = kf.ByteSha256,
        }, snap);
        var bound = ProductionCreateVideoProviderBinding.Authoritative(false, null, null, null, motion);
        Ok(bound.ProviderId == ProductionMotionCatalog.FixtureProviderId
            && bound.ModelId == ProductionMotionCatalog.FixtureModelId,
            "TEST no selection still binds Fixture");
        var verdict = await os.EvaluateCreateVideoAsync(scope, Claim(input));
        Ok(verdict.Available
            && verdict.ProviderId == ProductionMotionCatalog.FixtureProviderId
            && verdict.ModelId == ProductionMotionCatalog.FixtureModelId,
            "TEST Evaluate still Fixture");
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateVideo, "test", "ck6-test-fixture");
        var task = await os.CreateTaskAsync(req.RequestId);
        var frozen = await os.FreezeInputAsync(task.TaskId, input, bound.ProviderId, bound.ModelId);
        Ok(frozen.ProviderId == ProductionMotionCatalog.FixtureProviderId
            && frozen.ModelId == ProductionMotionCatalog.FixtureModelId,
            "TEST freeze still Fixture");
        _ = svc;
    }

    static async Task T_OfficialNoSelectionPreserved(Action<bool, string> Ok)
    {
        var h = await SeedOfficial(null, null);
        var bound = ProductionCreateVideoProviderBinding.Authoritative(true, null, null, null, h.Motion);
        Ok(bound.ProviderId == "" && bound.ModelId == "", "official no selection does not fallback Fixture");
        var verdict = await h.Os.EvaluateCreateVideoAsync(h.Scope, Claim(h.Input));
        Ok(verdict.BlockReason == ProductionFailureReasons.MissingProductionAiSelection,
            "official no selection stays MISSING_PRODUCTION_AI_SELECTION");
        var failed = false;
        try
        {
            await h.Svc.CreateVideoAsync(new CreateVideoApiCommand(h.Scope, h.Input, "test", "ck6-noselect", null, null));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.MissingProductionAiSelection)
        {
            failed = true;
        }
        Ok(failed, "official no selection CreateVideo fail-closed unchanged");
        var tasks = await h.Store.ListTasksByScopeAsync(h.Scope.BuildId, h.Scope.ShotCode);
        var prior = await h.Store.GetRequestByIdempotencyAsync("ck6-noselect");
        Ok(prior is null && tasks.All(t => t.Action != ProductionActions.CreateVideo),
            "official no selection does not create CREATE_VIDEO");
    }

    static async Task T_CommandFixtureCannotOverrideSelection(Action<bool, string> Ok)
    {
        var h = await SeedOfficial(FamixaProviderIds.Google, VeoModel);
        var bound = ProductionCreateVideoProviderBinding.Authoritative(
            true, h.Selection, ProductionMotionCatalog.FixtureProviderId, ProductionMotionCatalog.FixtureModelId, h.Motion);
        Ok(bound.ProviderId == FamixaProviderIds.Google && bound.ModelId == VeoModel,
            "body Fixture cannot override ACTIVE google/veo-3.1");
        var verdict = await h.Os.EvaluateCreateVideoAsync(h.Scope, Claim(h.Input) with
        {
            ProviderId = ProductionMotionCatalog.FixtureProviderId,
            ModelId = ProductionMotionCatalog.FixtureModelId,
        });
        Ok(verdict.ProviderId == FamixaProviderIds.Google && verdict.ModelId == VeoModel,
            "Evaluate ignores body Fixture when ACTIVE selection exists");
        Ok(verdict.ProviderId != ProductionMotionCatalog.FixtureProviderId
            && verdict.ModelId != ProductionMotionCatalog.FixtureModelId,
            "no silent ACTIVE→Fixture remap");
    }

    static async Task<Harness> SeedOfficial(string? provider, string? model)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, selections: selections);
        var svc = new ProductionCreateVideoService(os, store, files, motion);
        var scope = new ProductionScope(
            ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        var seed = Input(10);
        var kf = await SeedKeyframe(os, scope, seed);
        var input = seed with
        {
            KeyframeArtifactId = kf.ArtifactId,
            KeyframeByteSha256 = kf.ByteSha256,
            KeyframeHash = kf.ByteSha256,
        };
        ProductionAiSelection? selection = null;
        if (provider is not null && model is not null)
        {
            selection = await selections.UpsertActiveAsync(new UpsertProductionAiSelectionCommand(
                scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
                ProductionAiSelectionStages.Video, provider, model, "checkpoint-6", "binding"));
        }
        return new Harness(store, svc, os, scope, input, selection!, motion);
    }

    sealed record Harness(
        InMemoryProductionWorkStore Store,
        ProductionCreateVideoService Svc,
        IProductionExecutionService Os,
        ProductionScope Scope,
        ShotExecutionInput Input,
        ProductionAiSelection Selection,
        FixtureMotionAdapter Motion);

    static CreateVideoReadinessClaim Claim(ShotExecutionInput input) =>
        CreateVideoActionContractGate.ClaimFrom(input);

    static ShotExecutionInput LiveOf(ShotExecutionInput seed) => seed;

    static ShotExecutionInput Attach(ShotExecutionInput input, PerformanceSnapshot snap) =>
        ProductionTestPerformanceFixture.Attach(input, snap);

    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
    const string Acting = "He stays in place.";
    const string Visual = "visual 16:9";

    static ShotExecutionInput Input(decimal duration) =>
        new(Story, Acting, Visual, "CHAR-001", "",
            duration.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s",
            "", "motion",
            null, "", "16:9", duration, ShotExecutionCompiler.Version, null);

    static async Task<ProductionArtifact> SeedKeyframe(
        IProductionExecutionService os,
        ProductionScope scope,
        ShotExecutionInput input)
    {
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        var att = await os.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK6-" + scope.ShotCode + new string('K', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-ck6");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "kf/" + scope.ShotCode + ".png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }
}
