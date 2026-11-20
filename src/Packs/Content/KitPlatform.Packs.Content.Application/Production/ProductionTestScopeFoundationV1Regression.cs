using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTestScopeFoundationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_TEST_SCOPE_FOUNDATION_CHECKPOINT_2";

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
        Ok(ProductionWorkIds.SchemaVersion == "372", "schema version 372");
        Ok(ProductionScopeAnchorRules.Test == "TEST", "kind TEST");
        Ok(ProductionFailureReasons.CrossScopeForbidden == "CROSS_SCOPE_FORBIDDEN", "CROSS_SCOPE_FORBIDDEN");
        Ok(ProductionFailureReasons.UnknownScope == "UNKNOWN_SCOPE", "UNKNOWN_SCOPE");

        var scopes = new InMemoryProductionScopeFoundationStore();
        foreach (var id in ProductionScopeAnchorRules.OfficialFilmScopeIds)
            scopes.SeedFilm(id);
        var tests = new ProductionTestFoundationService(scopes);
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var motion = new FixtureMotionAdapter();
        var os = new ProductionExecutionService(store, motion, scopes);
        var svc = new ProductionCreateVideoService(os, store, files, motion, scopes);

        var created = await tests.CreateAsync("checkpoint-2-sandbox", "regression");
        await T01_CreateTestRun(Ok, created);
        await T02_CreateTestScope(Ok, created);
        await T03_CreateTestAnchor(Ok, created, scopes);
        await T04_CreateTestShot(Ok, created);
        await T05_TestResolvesAsTest(Ok, tests, created);
        await T06_FilmResolvesAsFilm(Ok, tests, scopes);
        await T07_UnknownRejected(Ok, os);
        var filmKf = await SeedKeyframe(os, FilmSh02(), FullInput());
        await T08_TestCannotAccessFilm(Ok, os, tests, created, filmKf, store);
        await T10_TestEvaluateNeverReadsFilm(Ok, os, created, filmKf);
        var testKf = await SeedKeyframe(os, TestScope(created), FullInput());
        await T09_FilmCannotAccessTest(Ok, os, tests, created, testKf, store);
        await T11_TestStorageNamespace(Ok, svc, created);
        await T12_FilmStorageUnchanged(Ok, svc);
        await T13_TestAbsentFromSeriesBuilds(Ok, created);
        await T14_ActiveBuildIdentityUnchanged(Ok);
        await T15_Ep01Ep99Unchanged(Ok, scopes);
        await T16_ExistingOsSuites(Ok);
    }

    static Task T01_CreateTestRun(Action<bool, string> Ok, ProductionTestFoundation created)
    {
        Ok(created.Run.TestRunId != Guid.Empty, "T01 test_run_id");
        Ok(created.Run.Name == "checkpoint-2-sandbox", "T01 name");
        Ok(created.Run.Status == "OPEN", "T01 status OPEN");
        Ok(created.Run.CreatedAt != default, "T01 created_at");
        Ok(created.Run.MetadataJson == "{}", "T01 metadata");
        Ok(!ProductionScopeAnchorRules.IsOfficialFilm(created.Run.TestRunId), "T01 run != official FILM");
        return Task.CompletedTask;
    }

    static Task T02_CreateTestScope(Action<bool, string> Ok, ProductionTestFoundation created)
    {
        Ok(created.Scope.ScopeId == created.Anchor.ScopeId, "T02 scope_id == anchor");
        Ok(created.Scope.TestRunId == created.Run.TestRunId, "T02 test_run_id");
        Ok(created.Scope.EpisodeCode == ProductionTestFoundationService.DefaultEpisode, "T02 episode TEST");
        Ok(created.Scope.CreatedAt != default, "T02 created_at");
        return Task.CompletedTask;
    }

    static async Task T03_CreateTestAnchor(Action<bool, string> Ok, ProductionTestFoundation created, InMemoryProductionScopeFoundationStore scopes)
    {
        Ok(created.Anchor.Kind == ProductionScopeAnchorRules.Test, "T03 kind TEST");
        Ok(created.Anchor.SeriesBuildId is null, "T03 series_build_id NULL");
        Ok(created.Anchor.TestRunId == created.Run.TestRunId, "T03 test_run_id set");
        Ok(!ProductionScopeAnchorRules.IsOfficialFilm(created.Anchor.ScopeId), "T03 scope != official FILM");
        foreach (var film in ProductionScopeAnchorRules.OfficialFilmScopeIds)
            Ok(created.Anchor.ScopeId != film, "T03 scope != " + film.ToString("D"));
        var stored = await scopes.GetAnchorAsync(created.Anchor.ScopeId);
        Ok(stored is { Kind: ProductionScopeAnchorRules.Test, SeriesBuildId: null }, "T03 store anchor TEST");
    }

    static Task T04_CreateTestShot(Action<bool, string> Ok, ProductionTestFoundation created)
    {
        Ok(created.Shot.TestShotId != Guid.Empty, "T04 test_shot_id");
        Ok(created.Shot.ScopeId == created.Scope.ScopeId, "T04 shot scope_id");
        Ok(created.Shot.ShotCode == ProductionTestFoundationService.DefaultShotCode, "T04 shot_code");
        Ok(created.Shot.EpisodeCode == ProductionTestFoundationService.DefaultEpisode, "T04 episode");
        Ok(created.Shot.SceneCode == ProductionTestFoundationService.DefaultScene, "T04 scene");
        Ok(created.Shot.ShotId == created.Shot.ShotCode, "T04 shot_id addressable");
        return Task.CompletedTask;
    }

    static async Task T05_TestResolvesAsTest(Action<bool, string> Ok, ProductionTestFoundationService tests, ProductionTestFoundation created)
    {
        var kind = await tests.ResolveKindAsync(created.Scope.ScopeId);
        Ok(kind == ProductionScopeKind.Test, "T05 resolve TEST");
        Ok(ProductionScopeGuard.Resolve(created.Anchor, created.Scope.ScopeId) == ProductionScopeKind.Test, "T05 guard TEST");
    }

    static async Task T06_FilmResolvesAsFilm(Action<bool, string> Ok, ProductionTestFoundationService tests, InMemoryProductionScopeFoundationStore scopes)
    {
        var ep01 = await tests.ResolveKindAsync(ProductionScopeAnchorRules.OfficialEp01);
        var ep99 = await tests.ResolveKindAsync(ProductionScopeAnchorRules.OfficialEp99);
        Ok(ep01 == ProductionScopeKind.Film, "T06 EP01 FILM");
        Ok(ep99 == ProductionScopeKind.Film, "T06 EP99 FILM");
        var stored = await scopes.GetAnchorAsync(ProductionScopeAnchorRules.OfficialEp01);
        Ok(stored is { Kind: ProductionScopeAnchorRules.Film, SeriesBuildId: not null, TestRunId: null }, "T06 EP01 anchor FILM");
        Ok(ProductionScopeGuard.Resolve(null, ProductionScopeAnchorRules.OfficialEp01) == ProductionScopeKind.Film,
            "T06 official id without row still FILM");
    }

    static async Task T07_UnknownRejected(Action<bool, string> Ok, ProductionExecutionService os)
    {
        var unknown = Guid.Parse("99999999-9999-4999-8999-999999999999");
        Ok(!ProductionScopeAnchorRules.IsOfficialFilm(unknown), "T07 not official");
        Ok(ProductionScopeGuard.Resolve(null, unknown) == ProductionScopeKind.Unknown, "T07 resolve UNKNOWN");
        var rejected = false;
        try { ProductionScopeGuard.RequireKnown(ProductionScopeKind.Unknown); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UnknownScope)
        { rejected = true; }
        Ok(rejected, "T07 RequireKnown UNKNOWN_SCOPE");
        var evalRejected = false;
        try
        {
            await os.EvaluateCreateVideoAsync(new ProductionScope(unknown, "X", "SC01", "X-SC01-SH01"));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UnknownScope)
        {
            evalRejected = true;
        }
        Ok(evalRejected, "T07 evaluate UNKNOWN_SCOPE");
    }

    static async Task T08_TestCannotAccessFilm(
        Action<bool, string> Ok,
        ProductionExecutionService os,
        ProductionTestFoundationService tests,
        ProductionTestFoundation created,
        ProductionArtifact filmKf,
        InMemoryProductionWorkStore store)
    {
        var access = false;
        try { await tests.RequireArtifactAccessAsync(created.Scope.ScopeId, ProductionScopeAnchorRules.OfficialEp01); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        { access = true; }
        Ok(access, "T08 TEST→FILM access CROSS_SCOPE_FORBIDDEN");

        var claim = false;
        try
        {
            await os.EvaluateCreateVideoAsync(TestScope(created), new CreateVideoReadinessClaim(KeyframeArtifactId: filmKf.ArtifactId));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        {
            claim = true;
        }
        Ok(claim, "T08 TEST claim FILM keyframe CROSS_SCOPE_FORBIDDEN");

        await store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), created.Scope.ScopeId, created.Shot.ShotCode, ProductionBindingRoles.Visible,
            filmKf.ArtifactId, filmKf.TaskId, DateTimeOffset.UtcNow, null));
        var preview = await os.GetPreviewAsync(TestScope(created));
        Ok(preview.IsEmpty, "T08 TEST preview ignores foreign keyframe");
    }

    static async Task T09_FilmCannotAccessTest(
        Action<bool, string> Ok,
        ProductionExecutionService os,
        ProductionTestFoundationService tests,
        ProductionTestFoundation created,
        ProductionArtifact testKf,
        InMemoryProductionWorkStore store)
    {
        var access = false;
        try { await tests.RequireArtifactAccessAsync(ProductionScopeAnchorRules.OfficialEp01, created.Scope.ScopeId); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        { access = true; }
        Ok(access, "T09 FILM→TEST access CROSS_SCOPE_FORBIDDEN");

        var claim = false;
        try
        {
            await os.EvaluateCreateVideoAsync(FilmSh02(), new CreateVideoReadinessClaim(KeyframeArtifactId: testKf.ArtifactId));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.CrossScopeForbidden)
        {
            claim = true;
        }
        Ok(claim, "T09 FILM claim TEST keyframe CROSS_SCOPE_FORBIDDEN");

        await store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH99", ProductionBindingRoles.Visible,
            testKf.ArtifactId, testKf.TaskId, DateTimeOffset.UtcNow, null));
        var preview = await os.GetPreviewAsync(new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH99"));
        Ok(preview.IsEmpty, "T09 FILM preview ignores foreign keyframe");
    }

    static async Task T10_TestEvaluateNeverReadsFilm(
        Action<bool, string> Ok,
        ProductionExecutionService os,
        ProductionTestFoundation created,
        ProductionArtifact filmKf)
    {
        var filmReady = await os.EvaluateCreateVideoAsync(FilmSh02());
        Ok(filmReady.KeyframeArtifactId == filmKf.ArtifactId, "T10 FILM still sees own keyframe");

        var sameShotOnTest = new ProductionScope(
            created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, FilmSh02().ShotCode);
        var leaked = await os.EvaluateCreateVideoAsync(sameShotOnTest);
        Ok(!leaked.Available && leaked.BlockReason == ProductionFailureReasons.MissingKeyframe, "T10 TEST same shot_code MISSING_KEYFRAME");
        Ok(leaked.KeyframeArtifactId is null, "T10 TEST did not inherit FILM artifact id");
        Ok(!string.Equals(leaked.Story, filmReady.Story, StringComparison.Ordinal)
            || !CreateVideoActionContractGate.IsPresent(leaked.Story), "T10 TEST did not inherit FILM story");

        var native = await os.EvaluateCreateVideoAsync(TestScope(created), FullClaim());
        Ok(!native.Available && native.BlockReason == ProductionFailureReasons.MissingKeyframe, "T10 TEST native MISSING_KEYFRAME");
        Ok(native.KeyframeArtifactId is null, "T10 TEST native no FILM kf");
    }

    static async Task T11_TestStorageNamespace(Action<bool, string> Ok, ProductionCreateVideoService svc, ProductionTestFoundation created)
    {
        var expectedKf = $"os-test/{created.Run.TestRunId:D}/kf";
        var expectedVideo = $"os-test/{created.Run.TestRunId:D}/video";
        Ok(ProductionStoragePolicy.KeyframeFolder(ProductionScopeKind.Test, created.Run.TestRunId) == expectedKf, "T11 policy kf");
        Ok(ProductionStoragePolicy.VideoFolder(ProductionScopeKind.Test, created.Run.TestRunId) == expectedVideo, "T11 policy video");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-TEST-EXISTING-PIXELS" + new string('K', 40));
        var art = await svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            TestScope(created), bytes, "image/png", "existing pixels", "regression"));
        Ok(art.StorageKey.StartsWith(expectedKf + "/", StringComparison.Ordinal), "T11 write os-test/{test_run_id}/kf");
        Ok(!art.StorageKey.StartsWith("os-kf/", StringComparison.Ordinal), "T11 not FILM os-kf");
    }

    static async Task T12_FilmStorageUnchanged(Action<bool, string> Ok, ProductionCreateVideoService svc)
    {
        Ok(ProductionStoragePolicy.KeyframeFolder(ProductionScopeKind.Film, null) == ProductionStoragePolicy.FilmKeyframeFolder, "T12 policy os-kf");
        Ok(ProductionStoragePolicy.VideoFolder(ProductionScopeKind.Film, null) == ProductionStoragePolicy.FilmVideoFolder, "T12 policy os");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-FILM-EXISTING-PIXELS" + new string('F', 40));
        var art = await svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH06"),
            bytes, "image/png", "existing pixels", "regression"));
        Ok(art.StorageKey.StartsWith(ProductionStoragePolicy.FilmKeyframeFolder + "/", StringComparison.Ordinal), "T12 FILM write os-kf");
        Ok(!art.StorageKey.Contains("os-test/", StringComparison.Ordinal), "T12 FILM not os-test");
    }

    static Task T13_TestAbsentFromSeriesBuilds(Action<bool, string> Ok, ProductionTestFoundation created)
    {
        Ok(created.Anchor.SeriesBuildId is null, "T13 no series_build_id");
        Ok(!ProductionScopeAnchorRules.OfficialFilmScopeIds.Contains(created.Scope.ScopeId), "T13 TEST not a film build id");
        var src = ReadRepoSource("ContentRepository.cs");
        Ok(src is not null, "T13 ContentRepository readable");
        if (src is not null)
        {
            Ok(src.Contains("FROM pack_content.series_build", StringComparison.Ordinal), "T13 list reads series_build");
            Ok(!src.Contains("production_test_run", StringComparison.Ordinal)
                && !src.Contains("production_test_scope", StringComparison.Ordinal)
                && !src.Contains("production_test_shot", StringComparison.Ordinal), "T13 series queries omit TEST tables");
        }
        return Task.CompletedTask;
    }

    static Task T14_ActiveBuildIdentityUnchanged(Action<bool, string> Ok)
    {
        Ok(ProductionScopeAnchorRules.FilmScopeId(ProductionScopeAnchorRules.OfficialEp01)
            == ProductionScopeAnchorRules.OfficialEp01, "T14 active film identity EP01");
        Ok(ProductionScopeAnchorRules.OfficialEp01 == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T14 official EP01 constant");
        return Task.CompletedTask;
    }

    static async Task T15_Ep01Ep99Unchanged(Action<bool, string> Ok, InMemoryProductionScopeFoundationStore scopes)
    {
        var ep01 = await scopes.GetAnchorAsync(ProductionScopeAnchorRules.OfficialEp01);
        var ep99 = await scopes.GetAnchorAsync(ProductionScopeAnchorRules.OfficialEp99);
        Ok(ep01 is { Kind: ProductionScopeAnchorRules.Film, SeriesBuildId: not null, TestRunId: null }, "T15 EP01 FILM untouched");
        Ok(ep99 is { Kind: ProductionScopeAnchorRules.Film, SeriesBuildId: not null, TestRunId: null }, "T15 EP99 FILM untouched");
        Ok(ProductionScopeAnchorRules.OfficialEp99 == Guid.Parse("77262ec0-74ad-459d-9128-f09bb553a5dd"), "T15 official EP99 constant");
    }

    static Task T16_ExistingOsSuites(Action<bool, string> Ok)
    {
        var work = ProductionWorkExecutionV1Regression.Run();
        Ok(work.Count == 0, "T16 work execution: " + string.Join("; ", work));
        var pipeline = ProductionPipelineV1Regression.Run();
        Ok(pipeline.Count == 0, "T16 pipeline: " + string.Join("; ", pipeline));
        var cutover = ProductionCreateVideoV1Regression.Run();
        Ok(cutover.Count == 0, "T16 create-video cutover: " + string.Join("; ", cutover));
        var contract = ProductionCreateVideoActionContractV1Regression.Run();
        Ok(contract.Count == 0, "T16 action contract: " + string.Join("; ", contract));
        var film = ProductionScopeAnchorV1Regression.Run();
        Ok(film.Count == 0, "T16 checkpoint 1: " + string.Join("; ", film));
        return Task.CompletedTask;
    }

    static ProductionScope TestScope(ProductionTestFoundation created) =>
        new(created.Scope.ScopeId, created.Shot.EpisodeCode, created.Shot.SceneCode, created.Shot.ShotCode);

    static ProductionScope FilmSh02() =>
        new(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");

    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";

    static CreateVideoReadinessClaim FullClaim() =>
        new(Story, "He stays in place.", "visual 16:9", "5s", 5);

    static ShotExecutionInput FullInput() =>
        new(Story, "He stays in place.", "visual 16:9", "CHAR-001", "", "5s", "", "motion",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null);

    static async Task<ProductionArtifact> SeedKeyframe(
        IProductionExecutionService os,
        ProductionScope scope,
        ShotExecutionInput input)
    {
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        var att = await os.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-TEST-SCOPE-" + scope.ShotCode + new string('K', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-test-scope");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "import/" + scope.ShotCode + ".png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }

    static string? ReadRepoSource(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName,
                "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);
            if (Directory.Exists(Path.Combine(dir.FullName, "migrations"))
                && File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.content.txt")))
            {
                var rooted = Path.Combine(dir.FullName,
                    "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", fileName);
                return File.Exists(rooted) ? File.ReadAllText(rooted) : null;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
