using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialSh02CreateMixV1Regression
{
    public const string SuiteId = ProductionEp01Sh02MixCanon.SuiteId;

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
        var sh02 = ProductionEp01Sh02MixCanon.Scope();
        var locked = Harness(sh02);
        await SeedVideoAndLipsync(locked, Guid.NewGuid(), 8, Guid.NewGuid(), 2.5m);
        Ok(await Rejects(() => locked.Svc.MixVideoAsync(MixCmd(sh02)),
            ProductionFailureReasons.BindingChanged), "SH02 MixVideo rejects unlocked parents");
        Ok(locked.MixTransport.ComposeExecutionCount == 0 && locked.MixTransport.ExecutionCount == 0,
            "SH02 lock does not start FFmpeg");

        var hist = Harness(sh02);
        await SeedVideoAndLipsync(hist, Guid.NewGuid(), 8, ProductionEp01Sh02MixCanon.HistoricalLipsyncArtifactId, 2.5m);
        Ok(await Rejects(() => hist.Svc.MixVideoAsync(MixCmd(sh02)),
            ProductionFailureReasons.BindingChanged), "historical LIPSYNC rejected");

        var sh03 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH03");
        var noPermit = Harness(sh03);
        var v0 = Guid.NewGuid();
        var l0 = Guid.NewGuid();
        await SeedVideoAndLipsync(noPermit, v0, 8, l0, 2.5m);
        Ok(await Rejects(() => noPermit.Svc.MixVideoAsync(MixCmd(sh03, l0)),
            PerformanceContractIds.OfficialFilmForbidden), "compose requires issued MIX permit");
        Ok(noPermit.MixTransport.ComposeExecutionCount == 0, "no permit → no compose");

        var h = Harness(sh03);
        var videoId = Guid.NewGuid();
        var lipId = Guid.NewGuid();
        await SeedVideoAndLipsync(h, videoId, 8, lipId, 2.5m);
        await IssueMixPermit(h, sh03);
        var first = await h.Svc.MixVideoAsync(MixCmd(sh03, lipId));
        Ok(first.Request.UserAction == ProductionActions.MixVideo, "MIX_VIDEO request");
        Ok(first.Artifact.Role == ProductionArtifactRoles.Mix
            && first.Artifact.DurationSec == 8
            && first.Artifact.MimeType == ProductionTestMp4.Mime, "MIX artifact 8s");
        Ok(h.MixTransport.ComposeExecutionCount == 1 && h.MixTransport.ExecutionCount == 0,
            "compose exactly 1; remux 0");
        var current = await h.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        Ok(current?.ArtifactId == first.Artifact.ArtifactId, "CURRENT:MIX");
        var permit = await h.Permits.GetLatestAsync(
            sh03.BuildId, sh03.ShotCode, ProductionActions.MixVideo,
            ProductionMixCatalog.ServerProviderId, ProductionMixCatalog.FfmpegModelId);
        Ok(permit is not null && permit.Status == ProductionExecutionPermitStatuses.Consumed
            && permit.ConsumedAt is not null, "CREATE_MIX permit consumed 1");

        var replay = await h.Svc.MixVideoAsync(MixCmd(sh03, lipId));
        Ok(replay.Artifact.ArtifactId == first.Artifact.ArtifactId
            && h.MixTransport.ComposeExecutionCount == 1
            && h.MixTransport.ExecutionCount == 0, "replay returns same MIX; compose stays 1");
        var afterReplay = await h.Permits.ListByTargetAsync(
            sh03.BuildId, sh03.ShotCode, ProductionActions.MixVideo,
            ProductionMixCatalog.ServerProviderId, ProductionMixCatalog.FfmpegModelId);
        Ok(afterReplay.Count == 1 && afterReplay[0].Status == ProductionExecutionPermitStatuses.Consumed,
            "replay does not consume another permit");

        var video = await h.Store.GetArtifactAsync(videoId);
        var lipsync = await h.Store.GetArtifactAsync(lipId);
        var contract = ProductionMixContract.Build(
            video!, lipsync!,
            OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            ShotExecutionCompiler.Hash("8.0s"));
        Ok(contract.OutputDurationSec == 8
            && contract.PictureSegments[0].DestEndSec == 2.5m
            && contract.PictureSegments[1].SourceRole == ProductionArtifactRoles.VideoTake
            && contract.AudioSegments[1].SourceRole == ProductionMixContract.Silence, "SH03 contract is COMPOSE_SEGMENTS 8s");
        var mutated = Art(videoId, ProductionArtifactRoles.VideoTake, 8, Sha("mutated-video"));
        Ok(ProductionMixContract.MatchesFrozen(contract, mutated, lipsync!)
            == ProductionFailureReasons.HashChanged, "negative replay: VIDEO SHA change is HASH_CHANGED");

        var graph = FfmpegMixComposeGraph.FilterComplex(contract);
        Ok(graph.Contains("trim=start=0:end=2.5", StringComparison.Ordinal)
            && graph.Contains("trim=start=2.5:end=8", StringComparison.Ordinal)
            && graph.Contains("anullsrc=", StringComparison.Ordinal)
            && !graph.Contains(ProductionEp01Sh02MixCanon.VoiceArtifactId.ToString("N"), StringComparison.Ordinal),
            "graph uses frozen 0-2.5 / 2.5-8 + silence; no VOICE");

        Ok(h.FalTransport.StartCount == 0 && h.VoiceTransport.SynthesizeCount == 0 && h.RunwayTransport.CreateCalls == 0,
            "no Fal / ElevenLabs / Runway");

        var failed = Harness(sh03);
        var failVideo = Guid.NewGuid();
        var failLip = Guid.NewGuid();
        await SeedVideoAndLipsync(failed, failVideo, 8, failLip, 2.5m);
        await IssueMixPermit(failed, sh03);
        failed.MixTransport.FailComposeProcessRemaining = 1;
        string? composeError = null;
        try { await failed.Svc.MixVideoAsync(MixCmd(sh03, failLip)); }
        catch (InvalidOperationException ex) { composeError = ex.Message; }
        var mixTask = (await failed.Store.ListTasksByScopeAsync(sh03.BuildId, sh03.ShotCode))
            .LastOrDefault(t => t.Action == ProductionActions.MixVideo);
        var mixAttempts = mixTask is null ? [] : await failed.Store.ListAttemptsAsync(mixTask.TaskId);
        var mixArts = mixTask is null ? [] : await failed.Store.ListArtifactsByTaskAsync(mixTask.TaskId);
        var mixCurrent = await failed.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        var mixSnap = mixTask is null ? null : await failed.Store.GetSnapshotByTaskAsync(mixTask.TaskId);
        Ok(composeError is not null
            && composeError.StartsWith(ProductionFailureReasons.FailedIdentity + ":ffmpeg-compose:", StringComparison.Ordinal)
            && failed.MixTransport.ComposeExecutionCount == 1
            && failed.MixTransport.ExecutionCount == 0
            && mixTask is not null
            && mixTask.Status == ProductionTaskStatuses.Failed
            && mixTask.FailureCode == ProductionFailureReasons.ExecutionProcessFailed
            && (await failed.Store.GetRequestAsync(mixTask.RequestId))?.Status == ProductionRequestStatuses.Failed
            && mixAttempts.Count == 1
            && mixAttempts[0].Status == ProductionAttemptStatuses.ProviderFailed
            && string.IsNullOrWhiteSpace(mixAttempts[0].ProviderJobId)
            && mixArts.Count == 0
            && mixCurrent is null
            && mixSnap?.CredentialId is null
            && failed.FalTransport.StartCount == 0
            && failed.VoiceTransport.SynthesizeCount == 0
            && failed.RunwayTransport.CreateCalls == 0, "compose process failure closes without a MIX artifact");

        var sh01 = ProductionEp01Sh01RealFilmRunV1MixRegression.Run();
        Ok(sh01.Count == 0, "SH01 mix suite still PASS");
    }

    static ProductionActionApiCommand MixCmd(ProductionScope shot, Guid? lipsync = null) =>
        new(shot,
            new ShotExecutionInput(
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "8.0s", "", "",
                DurationSec: 8, LipsyncArtifactId: lipsync),
            "regression",
            "mix-ck24d-" + shot.ShotCode);

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static HarnessBundle Harness(ProductionScope shot)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var mixTransport = new FakeFfmpegMixTransport();
        var fixture = new FixtureMotionAdapter();
        var scopes = new InMemoryProductionScopeFoundationStore();
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var permits = new InMemoryProductionExecutionPermitStore();
        var runwayTransport = new FakeRunwayTransport();
        var voiceTransport = new FakeElevenLabsVoiceTransport();
        var falTransport = new FakeFalLipsyncTransport();
        var os = new ProductionExecutionService(store, fixture, scopes);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes,
            officialPermits: permits,
            officialMix: new FfmpegMixExecutionAdapter(mixTransport));
        return new HarnessBundle(svc, os, store, files, mixTransport, permits, shot, runwayTransport, voiceTransport, falTransport);
    }

    static Task IssueMixPermit(HarnessBundle h, ProductionScope shot) =>
        h.Permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode,
            ProductionActions.MixVideo,
            ProductionMixCatalog.ServerProviderId,
            ProductionMixCatalog.FfmpegModelId,
            "regression",
            "ck24d-compose"));

    static async Task SeedVideoAndLipsync(HarnessBundle h, Guid videoId, decimal videoDur, Guid lipId, decimal lipDur)
    {
        var pixels = Encoding.ASCII.GetBytes("MIX-CK24D-" + h.Shot.ShotCode + new string('K', 32));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            h.Shot, pixels, ProductionEp01Sh02IngressCanon.Mime, ProductionEp01Sh02IngressCanon.Story, "mix-ck24d",
            ProductionEp01Sh02IngressCanon.ActingBeat, ProductionEp01Sh02IngressCanon.VisualContract, "",
            0, ProductionEp01Sh02IngressCanon.PictureRevisionId, ProductionEp01Sh02IngressCanon.ProvenanceJson()));
        var videoBytes = ProductionTestMp4.Encode("vid-" + videoId.ToString("N"), videoDur);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            videoId, art.TaskId, art.AttemptId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
            videoBytes.Length, videoDur, KitVideoIntegrityRules.Sha256Hex(videoBytes),
            "os/vid-" + videoId.ToString("N")[..12], "vid", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "vid-hash"));
        h.Files.Write("os", "vid-" + videoId.ToString("N")[..12], videoBytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), h.Shot.BuildId, h.Shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
            videoId, art.TaskId, DateTimeOffset.UtcNow, null));
        var lipBytes = ProductionTestMp4.Encode("lip-" + lipId.ToString("N"), lipDur);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            lipId, art.TaskId, art.AttemptId, ProductionArtifactRoles.Lipsync, ProductionTestMp4.Mime,
            lipBytes.Length, lipDur, KitVideoIntegrityRules.Sha256Hex(lipBytes),
            "os/lip-" + lipId.ToString("N")[..12], "lip", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "lip-hash"));
        h.Files.Write("os", "lip-" + lipId.ToString("N")[..12], lipBytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), h.Shot.BuildId, h.Shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            lipId, art.TaskId, DateTimeOffset.UtcNow, null));
    }

    static ProductionArtifact Art(Guid id, string role, decimal duration, string sha) =>
        new(id, Guid.NewGuid(), Guid.NewGuid(), role, ProductionTestMp4.Mime, 64, duration, sha,
            "os/" + id.ToString("N"), null, DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "in");

    static string Sha(string seed) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeFfmpegMixTransport MixTransport,
        InMemoryProductionExecutionPermitStore Permits,
        ProductionScope Shot,
        FakeRunwayTransport RunwayTransport,
        FakeElevenLabsVoiceTransport VoiceTransport,
        FakeFalLipsyncTransport FalTransport)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeFfmpegMixTransport MixTransport { get; } = MixTransport;
        public InMemoryProductionExecutionPermitStore Permits { get; } = Permits;
        public ProductionScope Shot { get; } = Shot;
        public FakeRunwayTransport RunwayTransport { get; } = RunwayTransport;
        public FakeElevenLabsVoiceTransport VoiceTransport { get; } = VoiceTransport;
        public FakeFalLipsyncTransport FalTransport { get; } = FalTransport;
    }
}
