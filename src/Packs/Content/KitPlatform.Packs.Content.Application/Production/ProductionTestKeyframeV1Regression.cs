using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTestKeyframeV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_TEST_FIXTURE_KEYFRAME_CHECKPOINT_3A";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var fixture = ProductionTestKeyframeCatalog.Load();
        T01_ManifestExists(Ok, fixture);
        T02_ShaMatches(Ok, fixture);
        T03_MimePng(Ok, fixture);
        T04_Dimensions(Ok, fixture);

        var (os, svc, scopes, store, files, tests) = Harness();
        var created = await tests.CreateAsync("checkpoint-3a", "regression");
        var before = await os.EvaluateCreateVideoAsync(TestScope(created));
        Ok(before.BlockReason == ProductionFailureReasons.MissingKeyframe, "before register MISSING_KEYFRAME");

        await T05_TestScopeRequired(Ok, svc, created);
        await T06_FilmRejected(Ok, svc);
        await T07_UnknownRejected(Ok, svc);
        await T08_RunMismatch(Ok, svc, created);
        await T09_ShotRequired(Ok, svc, created);

        var first = await svc.RegisterAsync(Cmd(created));
        await T10_StorageNamespace(Ok, first, created, files);
        await T11_ArtifactCreated(Ok, first, created);
        await T12_Verified(Ok, first);
        await T13_HashEqualsBytes(Ok, first, files);
        await T14_StorageKeyTest(Ok, first, created);
        await T15_CurrentBinding(Ok, first, store);
        var second = await svc.RegisterAsync(Cmd(created));
        await T16_Idempotent(Ok, first, second, store);
        var filmKf = await SeedFilmKeyframe(os);
        await T17_TestCannotCrossFilm(Ok, os, tests, created, first);
        await T18_FilmCannotCrossTest(Ok, os, tests, created, filmKf);
        await T19_NoLongerMissingKeyframe(Ok, os, created, first);
        await T20_ExistingSuites(Ok);
        await T21_Ep01Ep99Unchanged(Ok, scopes);
        T22_ActiveBuildUnchanged(Ok);
        T23_SeriesBuildCount(Ok, created);
        T24_NoProviderCalls(Ok, first);
    }

    static void T01_ManifestExists(Action<bool, string> Ok, ProductionTestKeyframeFixture fixture)
    {
        Ok(File.Exists(fixture.Path), "T01 fixture file exists");
        Ok(fixture.Manifest.FixtureId == ProductionTestKeyframeCatalog.FixtureId, "T01 fixtureId");
        Ok(fixture.Manifest.TestOnly && fixture.Manifest.ScopeKind == "TEST", "T01 TEST only");
        Ok(fixture.Manifest.Purpose == "CREATE_VIDEO_E2E", "T01 purpose");
    }

    static void T02_ShaMatches(Action<bool, string> Ok, ProductionTestKeyframeFixture fixture)
    {
        Ok(KitVideoIntegrityRules.Sha256Hex(fixture.Bytes) == fixture.Manifest.Sha256, "T02 sha256 == manifest");
        Ok(fixture.Bytes.Length == fixture.Manifest.ByteLength, "T02 byteLength");
    }

    static void T03_MimePng(Action<bool, string> Ok, ProductionTestKeyframeFixture fixture)
    {
        Ok(fixture.Manifest.Mime == "image/png", "T03 mime");
        Ok(ProductionTestKeyframePng.HasPngSignature(fixture.Bytes), "T03 PNG signature");
    }

    static void T04_Dimensions(Action<bool, string> Ok, ProductionTestKeyframeFixture fixture)
    {
        Ok(ProductionTestKeyframePng.TryReadPngSize(fixture.Bytes, out var w, out var h)
            && w == 160 && h == 90 && w == fixture.Manifest.Width && h == fixture.Manifest.Height, "T04 160x90");
    }

    static async Task T05_TestScopeRequired(Action<bool, string> Ok, ProductionTestKeyframeService svc, ProductionTestFoundation created)
    {
        Ok(created.Anchor.Kind == ProductionScopeAnchorRules.Test, "T05 created scope is TEST");
        Ok(await Rejects(() => svc.RegisterAsync(new RegisterTestKeyframeCommand(
            created.Run.TestRunId, created.Scope.ScopeId, created.Shot.ShotCode, "NO_SUCH", "t")),
            ProductionFailureReasons.FixtureNotFound), "T05 unknown fixture rejected");
    }

    static async Task T06_FilmRejected(Action<bool, string> Ok, ProductionTestKeyframeService svc)
    {
        Ok(await Rejects(() => svc.RegisterAsync(new RegisterTestKeyframeCommand(
            Guid.NewGuid(), ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH02",
            ProductionTestKeyframeCatalog.FixtureId, "t")),
            ProductionFailureReasons.TestScopeRequired), "T06 FILM rejected");
    }

    static async Task T07_UnknownRejected(Action<bool, string> Ok, ProductionTestKeyframeService svc)
    {
        Ok(await Rejects(() => svc.RegisterAsync(new RegisterTestKeyframeCommand(
            Guid.NewGuid(), Guid.Parse("99999999-9999-4999-8999-999999999999"),
            ProductionTestFoundationService.DefaultShotCode, ProductionTestKeyframeCatalog.FixtureId, "t")),
            ProductionFailureReasons.UnknownScope), "T07 unknown rejected");
    }

    static async Task T08_RunMismatch(Action<bool, string> Ok, ProductionTestKeyframeService svc, ProductionTestFoundation created)
    {
        Ok(await Rejects(() => svc.RegisterAsync(new RegisterTestKeyframeCommand(
            Guid.NewGuid(), created.Scope.ScopeId, created.Shot.ShotCode,
            ProductionTestKeyframeCatalog.FixtureId, "t")),
            ProductionFailureReasons.TestRunMismatch), "T08 testRunId mismatch");
    }

    static async Task T09_ShotRequired(Action<bool, string> Ok, ProductionTestKeyframeService svc, ProductionTestFoundation created)
    {
        Ok(await Rejects(() => svc.RegisterAsync(new RegisterTestKeyframeCommand(
            created.Run.TestRunId, created.Scope.ScopeId, "EP01-SC01-SH02",
            ProductionTestKeyframeCatalog.FixtureId, "t")),
            ProductionFailureReasons.TestShotMissing), "T09 TEST shot required");
    }

    static Task T10_StorageNamespace(
        Action<bool, string> Ok, ProductionTestKeyframeRegistration row, ProductionTestFoundation created,
        InMemoryProductionArtifactFileStore files)
    {
        var prefix = $"os-test/{created.Run.TestRunId:D}/kf/";
        Ok(row.StorageKey.StartsWith(prefix, StringComparison.Ordinal), "T10 storage prefix");
        Ok(files.Read(row.StorageKey) is { Length: > 0 }, "T10 physical bytes in store");
        return Task.CompletedTask;
    }

    static Task T11_ArtifactCreated(Action<bool, string> Ok, ProductionTestKeyframeRegistration row, ProductionTestFoundation created)
    {
        Ok(row.ArtifactId != Guid.Empty, "T11 artifactId");
        Ok(row.ScopeId == created.Scope.ScopeId, "T11 scopeId");
        Ok(row.ShotCode == created.Shot.ShotCode, "T11 shotCode");
        Ok(row.Role == ProductionArtifactRoles.Keyframe, "T11 role KEYFRAME");
        Ok(row.Source == ProductionTestKeyframeService.Source, "T11 source TEST_FIXTURE");
        Ok(row.FixtureId == ProductionTestKeyframeCatalog.FixtureId, "T11 fixtureId != artifactId");
        Ok(row.FixtureId != row.ArtifactId.ToString("D"), "T11 fixtureId separate");
        return Task.CompletedTask;
    }

    static Task T12_Verified(Action<bool, string> Ok, ProductionTestKeyframeRegistration row)
    {
        Ok(row.VerificationStatus == ProductionArtifactStatuses.Verified, "T12 VERIFIED");
        return Task.CompletedTask;
    }

    static Task T13_HashEqualsBytes(Action<bool, string> Ok, ProductionTestKeyframeRegistration row, InMemoryProductionArtifactFileStore files)
    {
        var live = files.Read(row.StorageKey);
        Ok(live is not null && KitVideoIntegrityRules.Sha256Hex(live) == row.ByteSha256, "T13 hash == bytes");
        Ok(row.ByteSha256 == ProductionTestKeyframeCatalog.Load().Manifest.Sha256, "T13 hash == manifest");
        return Task.CompletedTask;
    }

    static Task T14_StorageKeyTest(Action<bool, string> Ok, ProductionTestKeyframeRegistration row, ProductionTestFoundation created)
    {
        Ok(row.StorageKey.StartsWith($"os-test/{created.Run.TestRunId:D}/kf/", StringComparison.Ordinal), "T14 TEST namespace");
        Ok(!row.StorageKey.StartsWith("os-kf/", StringComparison.Ordinal), "T14 not FILM os-kf");
        return Task.CompletedTask;
    }

    static async Task T15_CurrentBinding(Action<bool, string> Ok, ProductionTestKeyframeRegistration row, InMemoryProductionWorkStore store)
    {
        var typed = await store.GetActiveBindingAsync(row.ScopeId, row.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe));
        Ok(typed is not null && typed.ArtifactId == row.ArtifactId, "T15 CURRENT:KEYFRAME → artifactId");
        Ok(typed!.Role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), "T15 role CURRENT:KEYFRAME");
    }

    static async Task T16_Idempotent(
        Action<bool, string> Ok, ProductionTestKeyframeRegistration first, ProductionTestKeyframeRegistration second,
        InMemoryProductionWorkStore store)
    {
        Ok(second.Reused, "T16 reused");
        Ok(second.ArtifactId == first.ArtifactId, "T16 same artifactId");
        Ok(second.ByteSha256 == first.ByteSha256, "T16 same sha");
        var bindings = await store.ListActiveBindingsAsync(first.ScopeId, first.ShotCode);
        Ok(bindings.Count(b => b.Role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe)) == 1,
            "T16 one active CURRENT:KEYFRAME");
    }

    static async Task T17_TestCannotCrossFilm(
        Action<bool, string> Ok, ProductionExecutionService os, ProductionTestFoundationService tests,
        ProductionTestFoundation created, ProductionTestKeyframeRegistration row)
    {
        var access = false;
        try { await tests.RequireArtifactAccessAsync(ProductionScopeAnchorRules.OfficialEp01, created.Scope.ScopeId); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        { access = true; }
        Ok(access, "T17 FILM cannot use TEST scope");
        var claim = false;
        try
        {
            await os.EvaluateCreateVideoAsync(new ProductionScope(
                ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02"),
                new CreateVideoReadinessClaim(KeyframeArtifactId: row.ArtifactId));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        { claim = true; }
        Ok(claim, "T17 FILM claim TEST artifact CROSS_SCOPE_FORBIDDEN");
    }

    static async Task T18_FilmCannotCrossTest(
        Action<bool, string> Ok, ProductionExecutionService os, ProductionTestFoundationService tests,
        ProductionTestFoundation created, ProductionArtifact filmKf)
    {
        var access = false;
        try { await tests.RequireArtifactAccessAsync(created.Scope.ScopeId, ProductionScopeAnchorRules.OfficialEp01); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        { access = true; }
        Ok(access, "T18 TEST cannot use FILM scope");
        var claim = false;
        try
        {
            await os.EvaluateCreateVideoAsync(TestScope(created), new CreateVideoReadinessClaim(KeyframeArtifactId: filmKf.ArtifactId));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        { claim = true; }
        Ok(claim, "T18 TEST claim FILM artifact CROSS_SCOPE_FORBIDDEN");
    }

    static async Task T19_NoLongerMissingKeyframe(
        Action<bool, string> Ok, ProductionExecutionService os, ProductionTestFoundation created,
        ProductionTestKeyframeRegistration row)
    {
        var verdict = await os.EvaluateCreateVideoAsync(TestScope(created));
        Ok(verdict.BlockReason != ProductionFailureReasons.MissingKeyframe, "T19 not MISSING_KEYFRAME");
        Ok(verdict.KeyframeArtifactId == row.ArtifactId, "T19 ApprovedKeyframe = fixture artifact");
        Ok(verdict.MissingInputs is null || !verdict.MissingInputs.Contains("ApprovedKeyframe"), "T19 ApprovedKeyframe satisfied");
        Ok(!verdict.Available && verdict.BlockReason == ProductionFailureReasons.MissingStory, "T19 remaining MISSING_STORY");
    }

    static Task T20_ExistingSuites(Action<bool, string> Ok)
    {
        var work = ProductionWorkExecutionV1Regression.Run();
        Ok(work.Count == 0, "T20 work: " + string.Join("; ", work));
        var pipeline = ProductionPipelineV1Regression.Run();
        Ok(pipeline.Count == 0, "T20 pipeline: " + string.Join("; ", pipeline));
        var cutover = ProductionCreateVideoV1Regression.Run();
        Ok(cutover.Count == 0, "T20 cutover: " + string.Join("; ", cutover));
        var contract = ProductionCreateVideoActionContractV1Regression.Run();
        Ok(contract.Count == 0, "T20 contract: " + string.Join("; ", contract));
        var film = ProductionScopeAnchorV1Regression.Run();
        Ok(film.Count == 0, "T20 checkpoint 1: " + string.Join("; ", film));
        var test = ProductionTestScopeFoundationV1Regression.Run();
        Ok(test.Count == 0, "T20 checkpoint 2: " + string.Join("; ", test));
        return Task.CompletedTask;
    }

    static async Task T21_Ep01Ep99Unchanged(Action<bool, string> Ok, InMemoryProductionScopeFoundationStore scopes)
    {
        var ep01 = await scopes.GetAnchorAsync(ProductionScopeAnchorRules.OfficialEp01);
        var ep99 = await scopes.GetAnchorAsync(ProductionScopeAnchorRules.OfficialEp99);
        Ok(ep01 is { Kind: ProductionScopeAnchorRules.Film, TestRunId: null }, "T21 EP01 FILM");
        Ok(ep99 is { Kind: ProductionScopeAnchorRules.Film, TestRunId: null }, "T21 EP99 FILM");
    }

    static void T22_ActiveBuildUnchanged(Action<bool, string> Ok)
    {
        Ok(ProductionScopeAnchorRules.OfficialEp01 == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T22 official EP01");
        Ok(ProductionScopeAnchorRules.FilmScopeId(ProductionScopeAnchorRules.OfficialEp01)
            == ProductionScopeAnchorRules.OfficialEp01, "T22 active film identity");
    }

    static void T23_SeriesBuildCount(Action<bool, string> Ok, ProductionTestFoundation created)
    {
        Ok(ProductionScopeAnchorRules.OfficialFilmScopeIds.Count == 4, "T23 four FILM builds");
        Ok(created.Anchor.SeriesBuildId is null, "T23 TEST has no series_build");
        Ok(!ProductionScopeAnchorRules.OfficialFilmScopeIds.Contains(created.Scope.ScopeId), "T23 TEST not in film list");
    }

    static void T24_NoProviderCalls(Action<bool, string> Ok, ProductionTestKeyframeRegistration row)
    {
        Ok(row.Source == ProductionTestKeyframeService.Source, "T24 TEST_FIXTURE only");
        Ok(row.Mime == "image/png", "T24 no generated video");
        Ok(row.StorageKey.Contains("/kf/", StringComparison.Ordinal), "T24 keyframe folder not video");
    }

    static (ProductionExecutionService os, ProductionTestKeyframeService svc, InMemoryProductionScopeFoundationStore scopes,
        InMemoryProductionWorkStore store, InMemoryProductionArtifactFileStore files, ProductionTestFoundationService tests) Harness()
    {
        var scopes = new InMemoryProductionScopeFoundationStore();
        foreach (var id in ProductionScopeAnchorRules.OfficialFilmScopeIds)
            scopes.SeedFilm(id);
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, scopes);
        var svc = new ProductionTestKeyframeService(os, store, files, scopes);
        return (os, svc, scopes, store, files, new ProductionTestFoundationService(scopes));
    }

    static RegisterTestKeyframeCommand Cmd(ProductionTestFoundation created) =>
        new(created.Run.TestRunId, created.Scope.ScopeId, created.Shot.ShotCode,
            ProductionTestKeyframeCatalog.FixtureId, "regression");

    static ProductionScope TestScope(ProductionTestFoundation created) =>
        new(created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, created.Shot.ShotCode);

    static async Task<ProductionArtifact> SeedFilmKeyframe(IProductionExecutionService os)
    {
        var scope = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "film-kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        var input = new ShotExecutionInput("FILM story", "acting", "visual", "CHAR-001", "", "5s", "", "motion",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null);
        await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        var att = await os.StartAttemptAsync(task.TaskId, "kf-film");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-FILM-KF-" + new string('F', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-film");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "os-kf/film.png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }
}
