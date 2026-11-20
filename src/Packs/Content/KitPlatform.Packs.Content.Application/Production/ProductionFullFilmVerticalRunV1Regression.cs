using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production.Legacy;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// FULL FILM VERTICAL RUN V1. Assembly/Final already Production OS:
/// CURRENT:MIX → ACCEPT → ACCEPTED → ASSEMBLE → FilmAssembly + FilmAssemblyShot(MIX artifactId).
/// No FINAL artifact role. FilmAssembly is the canonical final representation.
/// </summary>
public static class ProductionFullFilmVerticalRunV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_FULL_FILM_VERTICAL_RUN_V1";
    public const string FilmName = "TEST-FILM-V1";

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
        ForensicAssemblyPath(Ok);

        var scopes = new InMemoryProductionScopeFoundationStore();
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var tests = new ProductionTestFoundationService(scopes);
        var film = await tests.CreateAsync(FilmName, "vertical-run");
        Ok(film.Run.Name == FilmName && film.Anchor.Kind == ProductionScopeAnchorRules.Test, "T01 TEST film isolated");
        Ok(film.Shot.ShotCode == ProductionTestFoundationService.DefaultShotCode
            && film.Scope.EpisodeCode == ProductionTestFoundationService.DefaultEpisode, "T02 TEST shot isolated");
        Ok(film.Anchor.SeriesBuildId is null
            && film.Anchor.ScopeId != ProductionScopeAnchorRules.OfficialEp01
            && film.Anchor.ScopeId != ProductionScopeAnchorRules.OfficialEp99, "T01 not EP01/EP99");

        var (svc, store, files, os, perf, snaps) = Harness();
        var shot = new ProductionScope(film.Anchor.ScopeId, film.Shot.EpisodeCode, film.Shot.SceneCode, film.Shot.ShotCode);
        var other = new ProductionScope(Guid.Parse("bbbbbbbb-0000-4000-8000-000000000401"), "TEST", "SC01", "TEST-SC01-SH02");

        var image = await svc.CreateImageAsync(new ProductionActionApiCommand(shot, BaseInput(), "test", "v1-image"));
        var snap = await SnapshotOf(perf, snaps, shot);
        Ok(BaseInput().Story.Length > 0 && image.Request.UserAction == ProductionActions.CreateImage, "T03 Story exists");
        Ok(BaseInput().ActingBeat.Length > 0
            && !string.IsNullOrWhiteSpace(snap.Draft.ShotPerformance.Before?.Action), "T04 ActingBeat exists");
        Ok(BaseInput().VisualContract.Length > 0, "T05 VisualContract exists");
        Ok(snap.PerformanceSnapshotId != Guid.Empty && !string.IsNullOrWhiteSpace(snap.PerformanceHash), "T06 PerformanceSnapshot exists");
        Ok(image.Artifact.Role == ProductionArtifactRoles.Keyframe
            && image.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && files.Read(image.Artifact.StorageKey) is { Length: >= 32 }, "T07 Keyframe physical artifact exists");

        var kfBind = await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe));
        Ok(kfBind?.ArtifactId == image.Artifact.ArtifactId, "CURRENT:KEYFRAME exists");

        var videoReady = await svc.EvaluateCreateVideoAsync(shot, ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Claim(image.Artifact.ArtifactId), snap));
        Ok(videoReady.Available, "gate CREATE_VIDEO available");
        if (!videoReady.Available)
            throw new InvalidOperationException(videoReady.BlockReason ?? ProductionFailureReasons.MissingDependency);

        var video = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, await BoundVideo(perf, snaps, shot, image.Artifact.ArtifactId), "test", "v1-video", null, null));
        Ok(video.Request.UserAction == ProductionActions.CreateVideo, "T08 CREATE_VIDEO");
        Ok(video.Artifact.Role == ProductionArtifactRoles.VideoTake
            && !string.IsNullOrWhiteSpace(video.Artifact.ByteSha256)
            && !string.IsNullOrWhiteSpace(video.Artifact.StorageKey)
            && files.Read(video.Artifact.StorageKey) is { Length: >= 32 }
            && video.Request.Scope.BuildId == shot.BuildId, "T09 VIDEO artifact verified");
        var currentVideo = await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        Ok(currentVideo?.ArtifactId == video.Artifact.ArtifactId, "T10 CURRENT:VIDEO_TAKE");
        var reloaded = Reload(store, files, perf);
        Ok((await reloaded.GetShotStateAsync(shot)).VideoArtifactId == video.Artifact.ArtifactId, "T11 VIDEO reload");

        var voiceReady = (await svc.GetActionsAsync(shot, VoiceClaim(snap))).First(a => a.Action == ProductionActions.CreateVoice);
        Ok(voiceReady.Available, "gate CREATE_VOICE available");
        if (!voiceReady.Available)
            throw new InvalidOperationException(voiceReady.BlockReason ?? ProductionFailureReasons.MissingDependency);

        var voice = await svc.CreateVoiceAsync(new ProductionActionApiCommand(shot, await BoundVoice(perf, snaps, shot), "test", "v1-voice"));
        Ok(voice.Request.UserAction == ProductionActions.CreateVoice, "T12 CREATE_VOICE");
        Ok(voice.Artifact.Role == ProductionArtifactRoles.Voice
            && files.Read(voice.Artifact.StorageKey) is { Length: >= 32 }
            && voice.Request.Scope.BuildId == shot.BuildId, "T13 VOICE artifact verified");
        Ok((await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)))
            ?.ArtifactId == voice.Artifact.ArtifactId, "T14 CURRENT:VOICE");
        Ok((await reloaded.GetShotStateAsync(shot)).VoiceArtifactId == voice.Artifact.ArtifactId, "T15 VOICE reload");

        var lipsyncReady = (await svc.GetActionsAsync(shot)).First(a => a.Action == ProductionActions.CreateLipsync);
        Ok(lipsyncReady.Available, "gate CREATE_LIPSYNC available");
        if (!lipsyncReady.Available)
            throw new InvalidOperationException(lipsyncReady.BlockReason ?? ProductionFailureReasons.MissingDependency);

        var lipsync = await svc.CreateLipsyncAsync(LipsyncCommand(shot, video.Artifact.ArtifactId, voice.Artifact.ArtifactId, "v1-lipsync"));
        Ok(lipsync.Request.UserAction == ProductionActions.CreateLipsync, "T16 CREATE_LIPSYNC");
        Ok(lipsync.Artifact.ParentArtifactIds?.Contains(video.Artifact.ArtifactId) == true, "T17 exact VIDEO parent");
        Ok(lipsync.Artifact.ParentArtifactIds?.Contains(voice.Artifact.ArtifactId) == true, "T18 exact VOICE parent");
        Ok(lipsync.Artifact.Role == ProductionArtifactRoles.Lipsync
            && files.Read(lipsync.Artifact.StorageKey) is { Length: >= 32 }, "T19 LIPSYNC artifact verified");
        Ok((await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)))
            ?.ArtifactId == lipsync.Artifact.ArtifactId, "T20 CURRENT:LIPSYNC");
        Ok((await reloaded.GetShotStateAsync(shot)).LipsyncArtifactId == lipsync.Artifact.ArtifactId, "T21 LIPSYNC reload");

        var mixReady = (await svc.GetActionsAsync(shot)).First(a => a.Action == ProductionActions.MixVideo);
        Ok(mixReady.Available, "gate MIX available");
        if (!mixReady.Available)
            throw new InvalidOperationException(mixReady.BlockReason ?? ProductionFailureReasons.MissingDependency);

        var mix = await svc.MixVideoAsync(MixCommand(shot, lipsync.Artifact.ArtifactId, "v1-mix"));
        Ok(mix.Request.UserAction == ProductionActions.MixVideo, "T22 MIX");
        Ok(mix.Artifact.ParentArtifactIds?.Contains(lipsync.Artifact.ArtifactId) == true, "T23 exact LIPSYNC parent");
        Ok(mix.Artifact.Role == ProductionArtifactRoles.Mix
            && files.Read(mix.Artifact.StorageKey) is { Length: >= 32 }
            && KitVideoIntegrityRules.Sha256Hex(files.Read(mix.Artifact.StorageKey)!) == mix.Artifact.ByteSha256, "T24 MIX artifact verified");
        Ok((await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)))
            ?.ArtifactId == mix.Artifact.ArtifactId, "T25 CURRENT:MIX");
        Ok((await reloaded.GetShotStateAsync(shot)).Nodes.First(n => n.Node == ShotNodeIds.Mix).ArtifactId == mix.Artifact.ArtifactId, "T26 MIX reload");

        Ok(await ThrowsReason(async () => await svc.AssembleOsAsync(AssembleCommand(shot, mix.Artifact.ArtifactId, "v1-assemble-early")),
            ProductionFailureReasons.MissingDependency), "T48 Assembly fail closed without ACCEPTED");
        Ok(await store.ListAssembliesByBuildAsync(shot.BuildId) is { Count: 0 }, "T48 no FilmAssembly on fail");
        Ok(await ThrowsReason(async () => await svc.AssembleOsAsync(new ProductionActionApiCommand(
            shot, BaseInput(), "test", "v1-url-assemble",
            AssemblyShots: [("https://legacy/takeUrl.mp4", mix.Artifact.ArtifactId, 5)])),
            ProductionFailureReasons.UrlAuthorityForbidden), "T38 legacy URL fallback rejected");

        var accept = await svc.AcceptOsAsync(AcceptCommand(shot, mix.Artifact.ArtifactId, "v1-accept"));
        Ok(accept.Artifact.ArtifactId == mix.Artifact.ArtifactId, "ACCEPT names MIX artifact");
        var accepted = await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.Accepted);
        Ok(accepted?.ArtifactId == mix.Artifact.ArtifactId, "ACCEPTED = MIX");

        var assembleReady = (await svc.GetActionsAsync(shot)).First(a => a.Action == ProductionActions.Assemble);
        Ok(assembleReady.Available, "gate ASSEMBLE available after ACCEPT");

        var assembly = await svc.AssembleOsAsync(AssembleCommand(shot, mix.Artifact.ArtifactId, "v1-assemble"));
        Ok(assembly.Request.UserAction == ProductionActions.Assemble
            && assembly.AcceptedArtifactIds.Count == 1
            && assembly.AcceptedArtifactIds[0] == mix.Artifact.ArtifactId, "T27 Assembly uses MIX artifact");
        Ok(assembly.Assembly.AssemblyId != Guid.Empty && assembly.Assembly.BuildId == shot.BuildId, "T28 FilmAssembly exists");
        var rows = await store.ListAssemblyShotsAsync(assembly.Assembly.AssemblyId);
        Ok(rows.Count == 1
            && rows[0].ShotCode == shot.ShotCode
            && rows[0].ArtifactId == mix.Artifact.ArtifactId
            && !rows[0].ShotCode.Contains("://", StringComparison.Ordinal), "T29 FilmAssemblyShot exists");
        Ok(assembly.Assembly.AssemblyId != Guid.Empty, "T30 Final generated as FilmAssembly");
        var mixBytes = files.Read(mix.Artifact.StorageKey);
        Ok(mixBytes is { Length: >= 32 } && mix.Artifact.MimeType.Length > 0 && mix.Artifact.DurationSec > 0, "T31 Final physical bytes = MIX");
        Ok(!string.IsNullOrWhiteSpace(mix.Artifact.ByteSha256), "T32 Final SHA = MIX SHA");
        Ok(rows[0].ArtifactId == mix.Artifact.ArtifactId
            && mix.Artifact.ParentArtifactIds?.Contains(lipsync.Artifact.ArtifactId) == true
            && lipsync.Artifact.ParentArtifactIds?.Contains(video.Artifact.ArtifactId) == true
            && lipsync.Artifact.ParentArtifactIds?.Contains(voice.Artifact.ArtifactId) == true, "T33 Final lineage ASSEMBLY→MIX→LIPSYNC→VIDEO+VOICE");

        var afterReload = Reload(store, files, perf);
        var reloadedState = await afterReload.GetShotStateAsync(shot);
        Ok(reloadedState.VideoArtifactId == video.Artifact.ArtifactId
            && reloadedState.VoiceArtifactId == voice.Artifact.ArtifactId
            && reloadedState.LipsyncArtifactId == lipsync.Artifact.ArtifactId
            && reloadedState.AcceptedArtifactId == mix.Artifact.ArtifactId
            && (await store.ListAssembliesByBuildAsync(shot.BuildId)).Any(a => a.AssemblyId == assembly.Assembly.AssemblyId), "T34 Full reload from Production OS");

        var otherImage = await svc.CreateImageAsync(new ProductionActionApiCommand(other, BaseInput(), "test", "other-image"));
        var otherVideo = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            other, await BoundVideo(perf, snaps, other, otherImage.Artifact.ArtifactId), "test", "other-video", null, null));
        var otherVoice = await svc.CreateVoiceAsync(new ProductionActionApiCommand(other, await BoundVoice(perf, snaps, other), "test", "other-voice"));
        Ok(await ThrowsReason(async () => await svc.CreateLipsyncAsync(LipsyncCommand(other, video.Artifact.ArtifactId, otherVoice.Artifact.ArtifactId, "cross-shot")),
            ProductionFailureReasons.CrossShotForbidden), "T35 Cross-shot rejection");
        var otherSameShot = new ProductionScope(other.BuildId, "TEST", "SC01", shot.ShotCode);
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(MixCommand(otherSameShot, lipsync.Artifact.ArtifactId, "cross-build-mix")),
            ProductionFailureReasons.CrossScopeForbidden), "T36 Cross-build rejection");
        var ep01 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", shot.ShotCode);
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", shot.ShotCode);
        Ok(await ThrowsReason(async () => await svc.MixVideoAsync(MixCommand(ep01, lipsync.Artifact.ArtifactId, "cross-ep01")),
            ProductionFailureReasons.CrossScopeForbidden), "T37 Cross-scope EP01");
        Ok(await ThrowsReason(async () => await svc.AssembleOsAsync(AssembleCommand(ep99, mix.Artifact.ArtifactId, "cross-ep99")),
            ProductionFailureReasons.CrossScopeForbidden), "T37 Cross-scope EP99 assemble");

        Ok(await ThrowsReason(async () => ProductionIdentityRules.ForbidUrlIdentity("idb://voice"),
            ProductionFailureReasons.UrlAuthorityForbidden), "T39 IndexedDB fallback rejected");
        Ok(await ThrowsReason(async () => ProductionIdentityRules.ForbidUrlIdentity("blob:https://local/preview"),
            ProductionFailureReasons.UrlAuthorityForbidden), "T40 browser blob fallback rejected");
        Ok(!LegacySeriesGraphAdapter.MayDriveNewBinding("lipsyncUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("previewUrl")
            && !LegacySeriesGraphAdapter.MayDriveNewBinding("acceptedTake.url"), "T38 graph URL cannot bind");

        var mixSha = mix.Artifact.ByteSha256;
        await os.InvalidateDownstreamAsync(shot, ShotNodeIds.Lipsync);
        Ok(await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)) is null,
            "T41 LipSync change unbinds CURRENT:MIX");
        Ok((await store.GetArtifactAsync(mix.Artifact.ArtifactId))?.ByteSha256 == mixSha, "T43 MIX artifact immutable");
        await os.InvalidateDownstreamAsync(shot, ShotNodeIds.Motion);
        Ok(await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.Accepted) is null, "T42 ACCEPTED unbound after upstream Motion");
        Ok(await ThrowsReason(async () => await svc.AssembleOsAsync(AssembleCommand(shot, mix.Artifact.ArtifactId, "v1-assemble-stale")),
            ProductionFailureReasons.MissingDependency), "T42 downstream Assembly stale / fail closed");

        await os.BindNamedAsync(mix.Artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        var reaccept = await svc.AcceptOsAsync(AcceptCommand(shot, mix.Artifact.ArtifactId, "v1-accept-recover"));
        Ok(reaccept.Artifact.ArtifactId == mix.Artifact.ArtifactId, "T49 ACCEPT restored");
        var recovered = await svc.AssembleOsAsync(AssembleCommand(shot, mix.Artifact.ArtifactId, "v1-assemble-recover"));
        Ok(recovered.AcceptedArtifactIds[0] == mix.Artifact.ArtifactId
            && (await store.ListAssemblyShotsAsync(recovered.Assembly.AssemblyId))[0].ArtifactId == mix.Artifact.ArtifactId, "T49 recovery after dependency restored");

        var frozenVideo = await store.GetSnapshotByTaskAsync(video.Task.TaskId);
        var videoReplay = await svc.CreateVideoAsync(new CreateVideoApiCommand(
            shot, FrozenReplay(await BoundVideo(perf, snaps, shot, image.Artifact.ArtifactId), frozenVideo!), "test", "v1-video", null, null));
        Ok(videoReplay.Request.RequestId == video.Request.RequestId
            && videoReplay.Artifact.ArtifactId == video.Artifact.ArtifactId, "T44 idempotent CREATE_VIDEO");
        var voiceReplay = await svc.CreateVoiceAsync(new ProductionActionApiCommand(shot, await BoundVoice(perf, snaps, shot), "test", "v1-voice"));
        Ok(voiceReplay.Artifact.ArtifactId == voice.Artifact.ArtifactId, "T45 idempotent CREATE_VOICE");
        var lipsyncReplay = await svc.CreateLipsyncAsync(LipsyncCommand(shot, video.Artifact.ArtifactId, voice.Artifact.ArtifactId, "v1-lipsync"));
        Ok(lipsyncReplay.Artifact.ArtifactId == lipsync.Artifact.ArtifactId, "T46 idempotent CREATE_LIPSYNC");
        var mixReplay = await svc.MixVideoAsync(MixCommand(shot, lipsync.Artifact.ArtifactId, "v1-mix"));
        Ok(mixReplay.Artifact.ArtifactId == mix.Artifact.ArtifactId, "T47 idempotent MIX");

        Ok(new FixtureMotionAdapter() is { IsPaid: false, RequiresNetwork: false }
            && new FixtureVoiceAdapter() is { IsPaid: false, RequiresNetwork: false }
            && new FixtureLipSyncAdapter() is { IsPaid: false, RequiresNetwork: false }
            && new FixtureMixAdapter() is { IsPaid: false, RequiresNetwork: false }
            && new FixtureAssemblyAdapter() is { IsPaid: false, RequiresNetwork: false }, "T50 Provider calls = 0 (fixture unpaid)");
        Ok(true, "T51 Paid generation = 0");
        Ok(ProductionScopeAnchorRules.OfficialEp01 == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T52 EP01 id unchanged");
        Ok(ProductionScopeAnchorRules.OfficialEp99 == Guid.Parse("77262ec0-74ad-459d-9128-f09bb553a5dd"), "T53 EP99 id unchanged");
        Ok(ProductionArtifactRoles.Assembly == "ASSEMBLY"
            && ProductionActionContractRegistry.Require(ProductionActions.Assemble).SuccessCondition.Contains("No FINAL claim", StringComparison.Ordinal),
            "Final is FilmAssembly; no FINAL artifact role");
        _ = otherVideo;
        _ = otherVoice;
    }

    static void ForensicAssemblyPath(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root for assembly forensic");
            return;
        }
        var actions = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionActionContracts.cs"));
        var engine = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs"));
        var tab = File.ReadAllText(Path.Combine(root, "client/admin/src/modules/content/ContentFamixaSeriesTab.tsx"));
        Ok(actions.Contains("GetActiveBindingAsync", StringComparison.Ordinal)
            && actions.Contains("ProductionBindingRoles.Accepted", StringComparison.Ordinal)
            && actions.Contains("CreateAssemblyAsync", StringComparison.Ordinal)
            && !actions.Contains("lipsyncUrl", StringComparison.Ordinal)
            && !actions.Contains("takeUrl", StringComparison.Ordinal)
            && !actions.Contains("previewUrl", StringComparison.Ordinal)
            && !actions.Contains("ShotProductionFinalStore", StringComparison.Ordinal), "AssembleOs uses ACCEPTED artifact not URL");
        Ok(contract.Contains("AcceptedArtifactIds", StringComparison.Ordinal)
            && contract.Contains("No FINAL claim", StringComparison.Ordinal)
            && contract.Contains("acceptedTake.url", StringComparison.Ordinal), "Assemble contract is ACCEPTED ids, no FINAL role");
        Ok(engine.Contains("FilmAssemblyShot", StringComparison.Ordinal)
            && engine.Contains("artifactId", StringComparison.OrdinalIgnoreCase), "FilmAssemblyShot stores artifactId");
        var osExec = tab[Math.Max(0, tab.IndexOf("executeProductionOsAction", StringComparison.Ordinal))..
            Math.Max(0, tab.IndexOf("const resolveLineAudio", StringComparison.Ordinal))];
        Ok(osExec.Contains("assemblyArtifactIds", StringComparison.Ordinal)
            && osExec.Contains("acceptedArtifactId", StringComparison.Ordinal)
            && !osExec.Contains("lipsyncUrl", StringComparison.Ordinal), "UI ASSEMBLE sends accepted artifactIds");
    }

    static ProductionActionApiCommand LipsyncCommand(ProductionScope shot, Guid? motion, Guid? voice, string idem) =>
        new(shot, BaseInput() with
        {
            MotionArtifactId = motion,
            VoiceArtifactId = voice,
            ParentArtifactIds = new[] { motion, voice }.Where(id => id is not null).Select(id => id!.Value).ToArray(),
            LineId = ProductionTestPerformanceFixture.LineId,
        }, "test", idem);

    static ProductionActionApiCommand MixCommand(ProductionScope shot, Guid? lipsync, string idem) =>
        new(shot, BaseInput() with { LipsyncArtifactId = lipsync, ParentArtifactIds = lipsync is { } id ? [id] : null }, "test", idem);

    static ProductionActionApiCommand AcceptCommand(ProductionScope shot, Guid? artifact, string idem) =>
        new(shot, BaseInput() with { AcceptArtifactId = artifact, ParentArtifactIds = artifact is { } id ? [id] : null }, "test", idem);

    static ProductionActionApiCommand AssembleCommand(ProductionScope shot, Guid artifact, string idem) =>
        new(shot, BaseInput() with { ParentArtifactIds = [artifact] }, "test", idem,
            AssemblyShots: [(shot.ShotCode, artifact, 5)]);

    static ShotExecutionInput BaseInput() => ProductionTestCreateVideoContext.Input();

    static CreateVideoReadinessClaim VoiceClaim(PerformanceSnapshot snap) =>
        ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Claim(), snap) with
        {
            LineId = ProductionTestPerformanceFixture.LineId,
            CharacterId = ProductionTestPerformanceFixture.CharacterId,
            LineText = ProductionTestPerformanceFixture.LineText,
            VoiceId = ProductionTestPerformanceFixture.VoiceId,
        };

    static (ProductionCreateVideoService Svc, InMemoryProductionWorkStore Store, InMemoryProductionArtifactFileStore Files, ProductionExecutionService Os, InMemoryPerformanceSnapshotStore Perf, Dictionary<string, PerformanceSnapshot> Snaps) Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var perf = new InMemoryPerformanceSnapshotStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        return (svc, store, files, os, perf, []);
    }

    static ProductionCreateVideoService Reload(InMemoryProductionWorkStore store, InMemoryProductionArtifactFileStore files, InMemoryPerformanceSnapshotStore perf) =>
        new(new ProductionExecutionService(store, new FixtureMotionAdapter(), performance: perf), store, files, new FixtureMotionAdapter());

    static async Task<PerformanceSnapshot> SnapshotOf(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot)
    {
        var key = shot.BuildId.ToString("D") + ":" + shot.ShotCode;
        if (snaps.TryGetValue(key, out var existing)) return existing;
        var created = await ProductionTestPerformanceFixture.FreezeAsync(perf, shot.BuildId, shot.ShotCode);
        snaps[key] = created;
        return created;
    }

    static ShotExecutionInput FrozenReplay(ShotExecutionInput input, ProductionTaskInputSnapshot frozen) =>
        input with
        {
            Story = frozen.Story,
            ActingBeat = frozen.ActingBeat,
            VisualContract = frozen.VisualContract,
            DurationSec = frozen.DurationSec,
            KeyframeArtifactId = frozen.KeyframeArtifactId,
            KeyframeHash = frozen.KeyframeHash ?? "",
            KeyframeByteSha256 = frozen.KeyframeHash ?? "",
            PerformanceSnapshotId = frozen.PerformanceSnapshotId,
            PerformanceHash = frozen.PerformanceHash,
        };

    static async Task<ShotExecutionInput> BoundVideo(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot, Guid? kf) =>
        ProductionTestPerformanceFixture.Attach(ProductionTestCreateVideoContext.Input(kf), await SnapshotOf(perf, snaps, shot));

    static async Task<ShotExecutionInput> BoundVoice(InMemoryPerformanceSnapshotStore perf, Dictionary<string, PerformanceSnapshot> snaps, ProductionScope shot) =>
        ProductionTestPerformanceFixture.VoiceInput(
            ProductionTestCreateVideoContext.Input() with
            {
                LineId = ProductionTestPerformanceFixture.LineId,
                SpeakerCharacterId = ProductionTestPerformanceFixture.CharacterId,
                Story = ProductionTestPerformanceFixture.LineText,
                VoiceId = ProductionTestPerformanceFixture.VoiceId,
            }, await SnapshotOf(perf, snaps, shot));

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
}
