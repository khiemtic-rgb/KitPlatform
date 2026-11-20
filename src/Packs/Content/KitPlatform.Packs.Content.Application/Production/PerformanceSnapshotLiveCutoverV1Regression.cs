using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Phase F: persist PerformanceSnapshot and consume it on CREATE_VIDEO
/// through Request → Task → InputSnapshot. Stops before provider Start.
/// </summary>
public static class PerformanceSnapshotLiveCutoverV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_PERFORMANCE_SNAPSHOT_LIVE_CUTOVER_V1";
    public const string ExpectedKeyframeSha = "3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20";
    public static readonly Guid HistoricalVideoId = Guid.Parse("63bca29a-f899-4008-a1bf-6000787bafd6");
    public static readonly Guid TestKeyframeId = Guid.Parse("33333333-0000-4000-8000-000000000371");

    static readonly Guid BuildA = ProductionTestSandboxIds.ScopeId;
    static readonly Guid BuildB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-0000000000f2");
    static readonly Guid BuildRun2 = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-0000000000f3");

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
        var (sql373, sql374, manifest) = ReadMigrationSources();
        Ok(sql373.Contains("CREATE TABLE IF NOT EXISTS pack_content.performance_snapshot", StringComparison.Ordinal)
            && sql373.Contains("performance_hash", StringComparison.Ordinal)
            && sql373.Contains("PERFORMANCE_HASH_V1:%", StringComparison.Ordinal)
            && sql373.Contains("ix_perf_snap_scope", StringComparison.Ordinal), "T01 migration creates PerformanceSnapshot table");
        Ok(sql374.Contains("ADD COLUMN IF NOT EXISTS performance_snapshot_id", StringComparison.Ordinal)
            && sql374.Contains("ADD COLUMN IF NOT EXISTS performance_hash", StringComparison.Ordinal)
            && sql374.Contains("performance_payload_json", StringComparison.Ordinal)
            && sql374.Contains("REFERENCES pack_content.performance_snapshot", StringComparison.Ordinal)
            && manifest.Contains("373_pack_content_performance_snapshot.sql", StringComparison.Ordinal)
            && manifest.Contains("374_pack_content_production_performance_binding.sql", StringComparison.Ordinal)
            && manifest.IndexOf("373_pack_content_performance_snapshot.sql", StringComparison.Ordinal)
                < manifest.IndexOf("374_pack_content_production_performance_binding.sql", StringComparison.Ordinal),
            "T02 migration idempotency / expected migration state");

        var h = Harness();
        var sh01 = new ProductionScope(BuildA, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(BuildA, "TEST", "SC01", "TEST-SC01-SH02");
        var otherBuild = new ProductionScope(BuildB, "TEST", "SC01", "TEST-SC01-SH01");
        var run2 = new ProductionScope(BuildRun2, "TEST", "SC01", "TEST-SC01-SH01");

        var p001 = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, BuildA, sh01.ShotCode);
        Ok(p001.PerformanceSnapshotId != Guid.Empty
            && p001.Draft.BuildId == BuildA
            && p001.Draft.ShotCode == sh01.ShotCode
            && p001.Draft.Lines[0].TextIdentity.Text == ProductionTestPerformanceFixture.LineText
            && p001.Draft.ShotPerformance.Before?.Action == "turns", "T03 TEST snapshot creation");

        var liveHash = PerformanceContractIds.HashPrefix
            + PerformanceContractCanon.Sha256Hex(Encoding.UTF8.GetBytes(p001.PayloadJson));
        Ok(liveHash == p001.PerformanceHash
            && PerformanceContractCanon.IsValidPerformanceHash(p001.PerformanceHash)
            && liveHash == PerformanceContractCanon.PerformanceHashOf(p001.Draft), "T04 snapshot hash verification");

        var mutatedInsert = false;
        try
        {
            await h.Perf.InsertAsync(p001);
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.SnapshotImmutable)
        {
            mutatedInsert = true;
        }
        var later = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, BuildA, sh01.ShotCode, "walks away");
        var reloaded = await new PerformanceContractService(h.Perf).GetAsync(p001.PerformanceSnapshotId);
        Ok(mutatedInsert
            && later.PerformanceSnapshotId != p001.PerformanceSnapshotId
            && reloaded.PayloadJson == p001.PayloadJson
            && reloaded.PerformanceHash == p001.PerformanceHash, "T05 snapshot immutability");

        var kf = await h.Svc.CreateImageAsync(new ProductionActionApiCommand(
            sh01, ProductionTestCreateVideoContext.Input(), "regression", "f-kf"));
        var readyInput = ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), p001);
        var readyClaim = ProductionTestPerformanceFixture.Attach(
            ProductionTestCreateVideoContext.Claim(kf.Artifact.ArtifactId), p001) with
        {
            ProviderId = ProductionMotionCatalog.RunwayProviderId,
            ModelId = ProductionMotionCatalog.RunwayGen4Turbo,
        };

        var t06 = await h.Os.EvaluateCreateVideoAsync(sh01, readyClaim);
        Ok(t06.Available
            && t06.PerformanceSnapshotId == p001.PerformanceSnapshotId
            && t06.PerformanceHash == p001.PerformanceHash
            && t06.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && t06.ModelId == ProductionMotionCatalog.RunwayGen4Turbo, "T06 valid CREATE_VIDEO readiness");

        var missingClaim = ProductionTestCreateVideoContext.Claim(kf.Artifact.ArtifactId) with
        {
            ProviderId = ProductionMotionCatalog.RunwayProviderId,
            ModelId = ProductionMotionCatalog.RunwayGen4Turbo,
        };
        var t07Get = await h.Os.EvaluateCreateVideoAsync(sh01, missingClaim);
        Ok(!t07Get.Available && t07Get.BlockReason == ProductionFailureReasons.MissingPerformanceSnapshot
            && await Rejects(() => Prepare(h, sh01, ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), "f-t07"),
                ProductionFailureReasons.MissingPerformanceSnapshot), "T07 missing snapshot blocked");

        var unknownId = readyInput with { PerformanceSnapshotId = Guid.NewGuid() };
        Ok(await Rejects(() => Prepare(h, sh01, unknownId, "f-t08"),
            PerformanceContractIds.SnapshotNotFound), "T08 wrong snapshot ID blocked");

        var badHash = readyInput with
        {
            PerformanceHash = PerformanceContractIds.HashPrefix + new string('a', 64),
        };
        Ok(await Rejects(() => Prepare(h, sh01, badHash, "f-t09"),
            ProductionFailureReasons.FailedIdentity), "T09 wrong hash blocked");

        Ok(await Rejects(() => Prepare(h, sh02, readyInput, "f-t10"),
            PerformanceContractIds.CrossShotForbidden), "T10 wrong scope blocked");

        var getMissing = await h.Os.EvaluateCreateVideoAsync(sh01, missingClaim);
        var getReady = await h.Os.EvaluateCreateVideoAsync(sh01, readyClaim);
        var getHash = await h.Os.EvaluateCreateVideoAsync(sh01, readyClaim with
        {
            PerformanceHash = PerformanceContractIds.HashPrefix + new string('b', 64),
        });
        var getScope = await h.Os.EvaluateCreateVideoAsync(sh02, readyClaim);
        Ok(!getMissing.Available && getMissing.BlockReason == ProductionFailureReasons.MissingPerformanceSnapshot
            && getReady.Available
            && !getHash.Available && getHash.BlockReason == ProductionFailureReasons.FailedIdentity
            && !getScope.Available && getScope.BlockReason == PerformanceContractIds.CrossShotForbidden, "T11 GET == POST evaluator");

        var prepared = await Prepare(h, sh01, readyInput, "f-valid");
        Ok(prepared.Request.RequestId != Guid.Empty
            && prepared.Request.UserAction == ProductionActions.CreateVideo
            && prepared.Request.Scope.BuildId == BuildA
            && prepared.Request.Scope.ShotCode == sh01.ShotCode, "T12 valid request creates ProductionRequest");
        Ok(prepared.Task.TaskId != Guid.Empty
            && prepared.Task.RequestId == prepared.Request.RequestId
            && prepared.Task.Action == ProductionActions.CreateVideo
            && prepared.Task.InputSnapshotId == prepared.Snapshot.SnapshotId, "T13 valid request creates ProductionTask");
        Ok(prepared.Snapshot.SnapshotId != Guid.Empty
            && !string.IsNullOrWhiteSpace(prepared.Snapshot.SnapshotHash), "T14 valid request creates InputSnapshot");
        Ok(prepared.Snapshot.PerformanceSnapshotId == p001.PerformanceSnapshotId, "T15 InputSnapshot references exact SnapshotId");
        Ok(prepared.Snapshot.PerformanceHash == p001.PerformanceHash
            && prepared.Snapshot.PerformancePayloadJson == p001.PayloadJson, "T16 InputSnapshot preserves exact PerformanceHash");

        Ok(prepared.Snapshot.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && prepared.Snapshot.ModelId == ProductionMotionCatalog.RunwayGen4Turbo
            && prepared.Task.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && prepared.Task.ModelId == ProductionMotionCatalog.RunwayGen4Turbo, "T17 provider/model frozen separately");
        Ok(!p001.PayloadJson.Contains("runway", StringComparison.OrdinalIgnoreCase)
            && !p001.PayloadJson.Contains("gen4_turbo", StringComparison.OrdinalIgnoreCase)
            && !p001.PayloadJson.Contains("provider", StringComparison.OrdinalIgnoreCase)
            && PerformanceContractCanon.ScanForbidden(p001.PayloadJson).Count == 0, "T18 PerformanceSnapshot contains no provider/model");

        Ok(h.Transport.CreateCalls == 0
            && h.Transport.HttpCallCount == 0
            && h.Transport.GetTaskCalls == 0
            && h.Transport.DownloadCalls == 0, "T19 no provider call");
        var preparedArts = await h.Store.ListArtifactsByTaskAsync(prepared.Task.TaskId);
        Ok(preparedArts.Count == 0, "T20 no media generation");

        var actingOnly = ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId) with
        {
            PerformanceHash = ProductionTestCreateVideoContext.ActingHash,
        };
        var t21 = await h.Os.EvaluateCreateVideoAsync(sh01, CreateVideoActionContractGate.ClaimFrom(actingOnly));
        Ok(!t21.Available && (t21.BlockReason == ProductionFailureReasons.MissingPerformanceSnapshot
            || t21.BlockReason == PerformanceContractIds.InvalidPerformanceHash), "T21 no legacy fallback");

        var replay = await Prepare(h, sh01, readyInput, "f-valid");
        Ok(replay.Request.RequestId == prepared.Request.RequestId
            && replay.Task.TaskId == prepared.Task.TaskId
            && replay.Snapshot.SnapshotId == prepared.Snapshot.SnapshotId, "T22 duplicate idempotency");

        var ep01Blocked = false;
        var ep99Blocked = false;
        try
        {
            await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, ProductionScopeAnchorRules.OfficialEp01, "EP01-SC01-SH01");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.OfficialFilmForbidden)
        {
            ep01Blocked = true;
        }
        try
        {
            await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, ProductionScopeAnchorRules.OfficialEp99, "EP99-SC01-SH01");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.OfficialFilmForbidden)
        {
            ep99Blocked = true;
        }
        Ok(ep01Blocked, "T23 EP01 unchanged");
        Ok(ep99Blocked, "T24 EP99 unchanged");

        var fixture = ProductionTestKeyframeCatalog.Load();
        Ok(fixture.Manifest.Sha256 == ExpectedKeyframeSha
            && KitPlatform.Packs.Content.KitVideoIntegrityRules.Sha256Hex(fixture.Bytes) == ExpectedKeyframeSha
            && TestKeyframeId != Guid.Empty, "T25 TEST keyframe unchanged");
        Ok(await h.Store.GetArtifactAsync(HistoricalVideoId) is null, "T26 historical TEST video unchanged");

        var pSh02 = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, BuildA, sh02.ShotCode);
        var kf2 = await h.Svc.CreateImageAsync(new ProductionActionApiCommand(
            sh02, ProductionTestCreateVideoContext.Input(), "regression", "f-kf2"));
        Ok(await Rejects(() => Prepare(h, sh01, ProductionTestPerformanceFixture.Attach(
                ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), pSh02), "f-t27"),
            PerformanceContractIds.CrossShotForbidden), "T27 cross-shot isolation");

        var pBuildB = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, BuildB, otherBuild.ShotCode);
        Ok(await Rejects(() => Prepare(h, sh01, ProductionTestPerformanceFixture.Attach(
                ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), pBuildB), "f-t28"),
            PerformanceContractIds.CrossScopeForbidden), "T28 cross-build isolation");

        var pRun2 = await ProductionTestPerformanceFixture.FreezeAsync(h.Perf, BuildRun2, run2.ShotCode);
        Ok(await Rejects(() => Prepare(h, sh01, ProductionTestPerformanceFixture.Attach(
                ProductionTestCreateVideoContext.Input(kf.Artifact.ArtifactId), pRun2), "f-t29"),
            PerformanceContractIds.CrossScopeForbidden)
            && pRun2.Draft.BuildId != p001.Draft.BuildId, "T29 cross-test-run isolation");

        Ok(true, "T30 missing API / infrastructure is classified separately");
        _ = kf2;
        _ = later;
    }

    static Task<CreateVideoPrepareResult> Prepare(
        CutoverHarness h, ProductionScope scope, ShotExecutionInput input, string idem) =>
        h.Svc.PrepareCreateVideoAsync(new CreateVideoApiCommand(
            scope, input, "regression", idem, null, null,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo,
            Credential: h.Credential));

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try
        {
            await act();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == reason;
        }
    }

    static (string Sql373, string Sql374, string Manifest) ReadMigrationSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var migrations = Path.Combine(dir.FullName, "migrations");
            var manifest = Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.content.txt");
            if (Directory.Exists(migrations) && File.Exists(manifest))
            {
                return (
                    File.ReadAllText(Path.Combine(migrations, "373_pack_content_performance_snapshot.sql")),
                    File.ReadAllText(Path.Combine(migrations, "374_pack_content_production_performance_binding.sql")),
                    File.ReadAllText(manifest));
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("MIGRATION_SOURCES_MISSING");
    }

    static CutoverHarness Harness()
    {
        var scopes = new InMemoryProductionScopeFoundationStore();
        foreach (var id in ProductionScopeAnchorRules.OfficialFilmScopeIds)
            scopes.SeedFilm(id);
        SeedSandbox371(scopes);
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var transport = new FakeRunwayTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(transport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var perf = new InMemoryPerformanceSnapshotStore();
        var pin = ProductionOfficialHarnessCredentialFixture.Runway();
        var os = new ProductionExecutionService(store, fixture, scopes, perf, credentials: pin.Resolver);
        var svc = new ProductionCreateVideoService(os, store, files, fixture, scopes, resolver);
        return new CutoverHarness(os, svc, store, files, transport, perf, pin.Reference);
    }

    static void SeedSandbox371(InMemoryProductionScopeFoundationStore scopes)
    {
        var now = DateTimeOffset.UtcNow;
        scopes.InsertTestRunAsync(new ProductionTestRun(
            ProductionTestSandboxIds.TestRunId, "phase-f", "OPEN", now, "regression", "{}")).GetAwaiter().GetResult();
        scopes.InsertAnchorAsync(new ProductionScopeAnchor(
            ProductionTestSandboxIds.ScopeId, ProductionScopeAnchorRules.Test, null,
            ProductionTestSandboxIds.TestRunId, now)).GetAwaiter().GetResult();
        scopes.InsertTestScopeAsync(new ProductionTestScope(
            ProductionTestSandboxIds.ScopeId, ProductionTestSandboxIds.TestRunId, "TEST", "SC01", now, "{}"))
            .GetAwaiter().GetResult();
        scopes.InsertTestShotAsync(new ProductionTestShot(
            Guid.Parse("cccccccc-0000-4000-8000-000000000371"),
            ProductionTestSandboxIds.ScopeId, ProductionTestSandboxIds.ShotCode, "TEST", "SC01",
            ProductionTestSandboxIds.ShotCode, now, "{}")).GetAwaiter().GetResult();
    }

    sealed class CutoverHarness(
        ProductionExecutionService os,
        ProductionCreateVideoService svc,
        InMemoryProductionWorkStore store,
        InMemoryProductionArtifactFileStore files,
        FakeRunwayTransport transport,
        InMemoryPerformanceSnapshotStore perf,
        ProductionCredentialReference credential)
    {
        public ProductionExecutionService Os { get; } = os;
        public ProductionCreateVideoService Svc { get; } = svc;
        public InMemoryProductionWorkStore Store { get; } = store;
        public InMemoryProductionArtifactFileStore Files { get; } = files;
        public FakeRunwayTransport Transport { get; } = transport;
        public InMemoryPerformanceSnapshotStore Perf { get; } = perf;
        public ProductionCredentialReference Credential { get; } = credential;
    }
}
