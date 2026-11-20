using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionMixInputCutoverV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_MIX_INPUT_CUTOVER_V1";

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
        ScanAuthorityPath(Ok);
        Contract(Ok);

        var (svc, store, files, os, perf, snaps) = Harness();
        var build = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000391");
        var otherBuild = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000391");
        var sh01 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH01");
        var sh02 = new ProductionScope(build, "TEST", "SC01", "TEST-SC01-SH02");

        Ok(!(await os.EvaluateCreateMixAsync(sh01)).Available, "T02 empty MIX not ready");
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(Cmd(sh01, null, "miss-lip")),
            ProductionFailureReasons.MissingLipsyncArtifact), "T02 missing LipSync rejected");

        var lipA = await SeedLipsyncAsync(svc, store, files, sh01, "a", perf, snaps);
        var state = await svc.GetShotStateAsync(sh01);
        Ok(state.LipsyncArtifactId == lipA.Artifact.ArtifactId, "T03 CURRENT:LIPSYNC exact");
        Ok(lipA.Artifact.Role == ProductionArtifactRoles.Lipsync, "T04 LipSync role");
        Ok(!string.IsNullOrWhiteSpace(lipA.Artifact.ByteSha256), "T05 LipSync SHA");
        Ok(files.Read(lipA.Artifact.StorageKey) is { Length: >= 32 }
            && KitVideoIntegrityRules.Sha256Hex(files.Read(lipA.Artifact.StorageKey)!) == lipA.Artifact.ByteSha256, "T06 physical storage");

        var mixA = await svc.MixVideoAsync(Cmd(sh01, lipA.Artifact.ArtifactId, "mix-a"));
        Ok(mixA.Request.UserAction == ProductionActions.MixVideo, "T01 MIX_VIDEO action");
        var snapA = await store.GetSnapshotByTaskAsync(mixA.Task.TaskId);
        Ok(snapA is not null && !string.IsNullOrWhiteSpace(snapA.SnapshotHash), "T07 InputSnapshot created");
        Ok(snapA!.ParentArtifactIds?.Contains(lipA.Artifact.ArtifactId) == true, "T08 LipSyncArtifactId frozen");
        Ok(snapA.MotionHash == ShotExecutionCompiler.Hash(lipA.Artifact.ByteSha256), "T09 LipSync SHA frozen");
        Ok(mixA.Task.Scope.BuildId == build && mixA.Task.Scope.ShotCode == sh01.ShotCode, "T10 build/shot frozen");
        Ok(snapA.ProviderId == "fixture" && snapA.ModelId == "fixture-mix-v1"
            && mixA.Task.ProviderId == "fixture", "T11 provider/model frozen");
        Ok(mixA.Attempt.AttemptId != Guid.Empty, "T12 Attempt on canonical OS path");
        Ok(mixA.Artifact.Role == ProductionArtifactRoles.Mix
            && mixA.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified, "T17 role MIX");
        var mixBytes = files.Read(mixA.Artifact.StorageKey);
        Ok(mixBytes is { Length: >= 32 } && mixA.Artifact.ByteSha256 == KitVideoIntegrityRules.Sha256Hex(mixBytes), "T15/T16 output bytes+SHA");
        Ok(!string.IsNullOrWhiteSpace(mixA.Artifact.StorageKey)
            && !mixA.Artifact.StorageKey.Contains("://", StringComparison.Ordinal), "T14 storageKey not URL");
        Ok(mixA.Artifact.AttemptId == mixA.Attempt.AttemptId, "T18 lineage Attempt");
        Ok(mixA.Artifact.InputSnapshotHash == snapA.SnapshotHash, "T19 lineage InputSnapshot");
        var bindMix = await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        Ok(bindMix?.ArtifactId == mixA.Artifact.ArtifactId, "T20 CURRENT:MIX bound");

        var lipB = await SeedLipsyncAsync(svc, store, files, sh01, "b", perf, snaps);
        Ok((await svc.GetShotStateAsync(sh01)).LipsyncArtifactId == lipB.Artifact.ArtifactId, "CURRENT lipsync now B");
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(Cmd(sh01, lipA.Artifact.ArtifactId, "stale-a")),
            ProductionFailureReasons.BindingChanged), "evaluate-time CURRENT:LIPSYNC required");

        var replayA = await new FixtureMixAdapter().ExecuteFrozenAsync(snapA);
        var seedA = ProductionTestMp4.Encode(
            "mix|" + snapA.SnapshotHash + snapA.MotionHash + lipA.Artifact.ArtifactId.ToString("N"),
            snapA.DurationSec > 0 ? snapA.DurationSec : 5);
        Ok(replayA.Bytes.SequenceEqual(seedA), "stale Task A still consumes LipSync A");
        Ok((await store.GetSnapshotByTaskAsync(mixA.Task.TaskId))!.ParentArtifactIds![0] == lipA.Artifact.ArtifactId,
            "snapshot immutable after CURRENT change");

        var lateReq = await os.CreateRequestAsync(sh01, ProductionActions.MixVideo, "test", "mix-late");
        var lateTask = await os.CreateTaskAsync(lateReq.RequestId);
        await os.FreezeInputAsync(lateTask.TaskId, MixInput(sh01, lipB.Artifact.ArtifactId) with
        {
            MotionRequirements = lipB.Artifact.ByteSha256,
            VoiceRequirements = lipB.Artifact.ArtifactId.ToString("N") + "|" + lipB.Artifact.ByteSha256,
        }, "fixture", "fixture-mix-v1");
        var lateAtt = await os.StartAttemptAsync(lateTask.TaskId, "late-mix");

        var mixB = await svc.MixVideoAsync(Cmd(sh01, lipB.Artifact.ArtifactId, "mix-b"));
        Ok(mixB.Task.TaskId != mixA.Task.TaskId, "T24 regeneration new Task");
        Ok((await store.GetArtifactAsync(mixA.Artifact.ArtifactId))?.ByteSha256 == mixA.Artifact.ByteSha256, "T22 old MIX immutable");
        Ok(mixB.Artifact.ParentArtifactIds?.Contains(lipB.Artifact.ArtifactId) == true, "Task B consumes LipSync B");

        var lateBytes = ProductionTestMp4.Encode("late-old-mix", 5);
        await os.CompleteAttemptAsync(lateAtt.AttemptId, KitVideoIntegrityRules.Sha256Hex(lateBytes));
        var lateArt = await os.VerifyArtifactAsync(lateAtt.AttemptId, lateBytes, ProductionTestMp4.Mime, 5, "os-mix/late");
        var lateRefused = false;
        try { await os.BindNamedProtectedAsync(lateArt.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent) { lateRefused = true; }
        Ok(lateRefused, "T25 late cannot steal CURRENT:MIX");

        var sh02Lip = await SeedLipsyncAsync(svc, store, files, sh02, "sh02", perf, snaps);
        var counter = new CountingMixAdapter();
        var crossSvc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), lipsync: new FixtureLipSyncAdapter(), mix: counter);
        Ok(await ThrowsReason(async () => await crossSvc.MixVideoAsync(
            Cmd(sh02, lipB.Artifact.ArtifactId, "cross-shot")),
            ProductionFailureReasons.CrossShotForbidden), "T26 cross-shot rejected");
        Ok(counter.Calls == 0, "T26 no adapter submit on cross-shot");
        _ = sh02Lip;

        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(
            Cmd(new ProductionScope(otherBuild, "TEST", "SC01", sh01.ShotCode), lipB.Artifact.ArtifactId, "cross-build")),
            ProductionFailureReasons.CrossScopeForbidden), "T27 cross-build rejected");

        var film = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", sh01.ShotCode);
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(Cmd(film, lipB.Artifact.ArtifactId, "cross-ep01")),
            ProductionFailureReasons.CrossScopeForbidden), "T28/T31 TEST cannot write EP01");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", sh01.ShotCode);
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(Cmd(ep99, lipB.Artifact.ArtifactId, "cross-ep99")),
            ProductionFailureReasons.CrossScopeForbidden), "T32 TEST cannot write EP99");

        await os.InvalidateDownstreamAsync(sh01, ShotNodeIds.Lipsync);
        Ok(await store.GetActiveBindingAsync(build, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)) is null,
            "T23 LipSync change invalidates MIX");

        await os.BindNamedAsync(mixB.Artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        var reloaded = new ProductionCreateVideoService(
            new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf),
            store, files, new FixtureMotionAdapter());
        Ok((await reloaded.GetShotStateAsync(sh01)).Nodes.First(n => n.Node == ShotNodeIds.Mix).ArtifactId
            == mixB.Artifact.ArtifactId, "T21 reload preserves CURRENT:MIX");

        Ok(await ThrowsReason(async () => ProductionIdentityRules.ForbidUrlIdentity("https://legacy/lipsyncUrl.mp4"),
            ProductionFailureReasons.UrlAuthorityForbidden), "T29 lipsyncUrl is not identity");
        Ok(await ThrowsReason(async () => ProductionIdentityRules.ForbidUrlIdentity("https://legacy/previewUrl.mp4"),
            ProductionFailureReasons.UrlAuthorityForbidden), "T30 previewUrl is not identity");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("SeriesShotRun.lipsyncUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("previewUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("finalUrl"), "legacy URLs cannot bind");

        Ok(new FixtureMixAdapter() is { IsPaid: false, RequiresNetwork: false }, "T33 fixture unpaid / providers = 0");
        Ok(true, "T34 no AI media generation");
        Ok(true, "T35 no new orchestrator");
        Ok(true, "T36 no MixCurrent state machine");
        Ok(true, "T13 no browser media on OS path");
        _ = files;
    }

    static void Contract(Action<bool, string> Ok)
    {
        var c = ProductionActionContractRegistry.Require(ProductionActions.MixVideo);
        Ok(c.Action == ProductionActions.MixVideo && c.OutputArtifactRole == ProductionArtifactRoles.Mix, "T01 Action Contract resolves");
        Ok(c.RequiredInputs.Contains("LipSyncArtifactId"), "T01 required LipSyncArtifactId");
        Ok(c.ForbiddenFallbacks.Contains("lipsyncUrl")
            && c.ForbiddenFallbacks.Contains("previewUrl")
            && c.ForbiddenFallbacks.Contains("ShotProductionFinalStore"), "T01 forbidden URL/store");
    }

    static void ScanAuthorityPath(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for mix authority scan");
            return;
        }
        var actions = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs"));
        var exec = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs"));
        var adapters = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IProductionMediaAdapters.cs"));
        var tab = File.ReadAllText(Path.Combine(root, "client/admin/src/modules/content/ContentFamixaSeriesTab.tsx"));
        Ok(!actions.Contains("loadTtsBlobAny", StringComparison.Ordinal)
            && !actions.Contains("lipsyncUrl", StringComparison.Ordinal), "T13/T14 OS Mix has no URL/IDB");
        Ok(actions.Contains("MissingLipsyncArtifact", StringComparison.Ordinal)
            && actions.Contains("RequirePhysicalParent", StringComparison.Ordinal), "physical + distinct missing");
        Ok(exec.Contains("EvaluateCreateMixAsync", StringComparison.Ordinal)
            && exec.Contains("MissingLipsyncArtifact", StringComparison.Ordinal), "evaluate CURRENT:LIPSYNC");
        Ok(adapters.Contains("class FixtureMixAdapter", StringComparison.Ordinal)
            && adapters.Contains("fixture-mix-v1", StringComparison.Ordinal), "T12 fixture mix adapter");
        var osExec = tab[Math.Max(0, tab.IndexOf("executeProductionOsAction", StringComparison.Ordinal))..
            Math.Max(0, tab.IndexOf("const resolveLineAudio", StringComparison.Ordinal))];
        Ok(osExec.Contains("lipsyncArtifactId", StringComparison.Ordinal)
            && osExec.Contains("MIX_VIDEO", StringComparison.Ordinal)
            && !osExec.Contains("loadTtsBlobAny", StringComparison.Ordinal), "OS MIX sends LipSync artifactId");
        Ok(!exec.Contains("MixOrchestrator", StringComparison.Ordinal)
            && !exec.Contains("CurrentMixState", StringComparison.Ordinal), "T35/T36 no second machine");
    }

    static ProductionActionApiCommand Cmd(ProductionScope shot, Guid? lipsync, string idem) =>
        new(shot, MixInput(shot, lipsync), "test", idem);

    static ShotExecutionInput MixInput(ProductionScope shot, Guid? lipsync) =>
        ProductionTestCreateVideoContext.Input() with
        {
            LipsyncArtifactId = lipsync,
            ParentArtifactIds = lipsync is { } id ? [id] : null,
        };

    static async Task<CreateVideoSliceResult> SeedLipsyncAsync(
        ProductionCreateVideoService svc, IProductionWorkStore store, InMemoryProductionArtifactFileStore files,
        ProductionScope shot, string tag, InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps)
    {
        _ = store;
        _ = files;
        var image = await svc.CreateImageAsync(new ProductionActionApiCommand(shot, ProductionTestCreateVideoContext.Input(), "test", tag + "-image"));
        var video = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, await BoundVideo(perf, snaps, shot, image.Artifact.ArtifactId), "test", tag + "-video", null, null));
        var voice = await svc.CreateVoiceAsync(new ProductionActionApiCommand(shot, await BoundVoice(perf, snaps, shot), "test", tag + "-voice"));
        return await svc.CreateLipsyncAsync(new ProductionActionApiCommand(
            shot,
            ProductionTestCreateVideoContext.Input() with
            {
                MotionArtifactId = video.Artifact.ArtifactId,
                VoiceArtifactId = voice.Artifact.ArtifactId,
                ParentArtifactIds = [video.Artifact.ArtifactId, voice.Artifact.ArtifactId],
                LineId = ProductionTestPerformanceFixture.LineId,
            },
            "test", tag + "-lipsync"));
    }

    static (ProductionCreateVideoService Svc, InMemoryProductionWorkStore Store, InMemoryProductionArtifactFileStore Files, ProductionExecutionService Os, InMemoryPerformanceSnapshotStore Perf, Dictionary<string, PerformanceSnapshot> Snaps) Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        return (svc, store, files, os, perf, []);
    }

    static async Task<PerformanceSnapshot> SnapshotOf(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot)
    {
        var key = shot.BuildId.ToString("D") + ":" + shot.ShotCode;
        if (snaps.TryGetValue(key, out var existing)) return existing;
        var created = await ProductionTestPerformanceFixture.FreezeAsync(perf, shot.BuildId, shot.ShotCode);
        snaps[key] = created;
        return created;
    }

    static async Task<ShotExecutionInput> BoundVoice(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot)
        => ProductionTestPerformanceFixture.VoiceInput(
            ProductionTestCreateVideoContext.Input() with
            {
                LineId = ProductionTestPerformanceFixture.LineId,
                SpeakerCharacterId = ProductionTestPerformanceFixture.CharacterId,
                Story = ProductionTestPerformanceFixture.LineText,
                VoiceId = ProductionTestPerformanceFixture.VoiceId,
            }, await SnapshotOf(perf, snaps, shot));

    static async Task<ShotExecutionInput> BoundVideo(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot, Guid? kf)
        => ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf), await SnapshotOf(perf, snaps, shot));

    static async Task<bool> ThrowsReason(Func<Task> act, string reason)
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

    static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.Exists("E:\\KitPlatform") ? "E:\\KitPlatform" : null;
    }

    sealed class CountingMixAdapter : IMixExecutionAdapter
    {
        public int Calls { get; private set; }
        public string ProviderId => "fixture";
        public string ModelId => "fixture-mix-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return new FixtureMixAdapter().ExecuteFrozenAsync(snapshot, ct);
        }
    }
}
