using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmRunV1AssemblyRegression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_RUN_V1_ASSEMBLY";

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
        var missing = Harness();
        missing.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        var sh03 = new ProductionScope(sh01.BuildId, "EP01", "SC01", "EP01-SC01-SH03");
        var mixId = Guid.NewGuid();
        await SeedMixAsync(missing, sh03, mixId);
        Ok(await Rejects(() => missing.Svc.AssembleOsAsync(AssembleCommand(sh03, mixId)),
            ProductionFailureReasons.MissingDependency), "no ACCEPTED → no ASSEMBLE task");
        Ok(missing.MixTransport.ExecutionCount == 0, "missing ACCEPTED does not run FFmpeg");

        var locked = Harness();
        locked.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var wrongMix = Guid.NewGuid();
        await SeedMixAsync(locked, sh01, wrongMix);
        await locked.Svc.AcceptOsAsync(AcceptCommand(sh01, wrongMix));
        Ok(await Rejects(() => locked.Svc.AssembleOsAsync(AssembleCommand(sh01, wrongMix)),
            ProductionFailureReasons.BindingChanged), "SH01 MIX canon mismatch stops before FFmpeg");
        Ok(locked.MixTransport.ExecutionCount == 0, "canon mismatch does not run FFmpeg");

        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var liveMix = Guid.NewGuid();
        await SeedMixAsync(h, sh03, liveMix);
        var accept = await h.Svc.AcceptOsAsync(AcceptCommand(sh03, liveMix));
        Ok(accept.Artifact.ArtifactId == liveMix, "ACCEPT names MIX artifact");
        Ok((await h.Store.GetActiveBindingAsync(sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.Accepted))
            ?.ArtifactId == liveMix, "ACCEPTED = MIX");
        Ok((await h.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Mix)))
            ?.ArtifactId == liveMix, "ACCEPTED:MIX");

        var first = await h.Svc.AssembleOsAsync(AssembleCommand(sh03, liveMix));
        Ok(first.Request.UserAction == ProductionActions.Assemble
            && first.Output is not null
            && first.Output.Role == ProductionArtifactRoles.Assembly, "Request ASSEMBLE + ASSEMBLY artifact");
        var snap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap is not null
            && snap.ProviderId == ProductionAssemblyCatalog.ServerProviderId
            && snap.ModelId == ProductionAssemblyCatalog.FfmpegModelId
            && ProductionAssemblyFreeze.TryParse(snap.ShotContract, out var frozenAssembly, out var frozenBuild, out var frozenShots)
            && frozenAssembly == first.Assembly.AssemblyId
            && frozenBuild == sh03.BuildId
            && frozenShots.Count == 1
            && frozenShots[0].ArtifactId == liveMix
            && snap.ParentArtifactIds?.Contains(liveMix) == true, "InputSnapshot freezes server/ffmpeg + assembly membership");
        Ok(h.MixTransport.ExecutionCount == 1
            && h.MixTransport.LastCommand == FakeFfmpegMixTransport.Command, "exactly one FFmpeg remux");
        Ok(first.Output!.VerificationStatus == ProductionArtifactStatuses.Verified
            && first.Output.MimeType == ProductionTestMp4.Mime
            && first.Output.ByteSha256.Length == 64
            && first.Output.ParentArtifactIds?.Contains(liveMix) == true, "ASSEMBLY VERIFIED physical");
        Ok((await h.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly)))
            ?.ArtifactId == first.Output.ArtifactId, "CURRENT:ASSEMBLY bound");
        Ok((await h.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly)))
            ?.ArtifactId == first.Output.ArtifactId, "VISIBLE:ASSEMBLY bound");
        Ok((await h.Store.ListAssemblyShotsAsync(first.Assembly.AssemblyId))[0].ArtifactId == liveMix, "FilmAssemblyShot = MIX");
        Ok(first.Assembly.Title != "TEST-ASSEMBLY", "official FilmAssembly is not TEST fixture title");
        Ok((await h.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)))
            ?.ArtifactId == liveMix, "CURRENT:MIX unchanged");

        var replay = await h.Svc.AssembleOsAsync(AssembleCommand(sh03, liveMix));
        Ok(replay.Output?.ArtifactId == first.Output.ArtifactId
            && replay.Attempt.AttemptId == first.Attempt.AttemptId
            && replay.Assembly.AssemblyId == first.Assembly.AssemblyId
            && h.MixTransport.ExecutionCount == 1, "replay does not run FFmpeg again");
        Ok((await h.Svc.GetShotStateAsync(sh01)).AcceptedArtifactId is null, "SH01 not stolen");
        Ok((await h.Svc.GetShotStateAsync(new ProductionScope(
            ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01"))).AcceptedArtifactId is null,
            "EP99 unchanged");
        Ok(h.FalTransport.StartCount == 0 && h.VoiceTransport.SynthesizeCount == 0 && h.RunwayTransport.CreateCalls == 0,
            "no Fal / ElevenLabs / Runway");
    }

    static async Task SeedMixAsync(HarnessBundle h, ProductionScope shot, Guid mixId)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-ASM-PIXELS-" + new string('R', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "asm-v1",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        var mixBytes = ProductionTestMp4.Encode("EP01-ASM-MIX-" + shot.ShotCode, 5.042m);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            mixId, art.TaskId, art.AttemptId, ProductionArtifactRoles.Mix, ProductionTestMp4.Mime,
            mixBytes.Length, 5.042m, KitVideoIntegrityRules.Sha256Hex(mixBytes), "os/locked-mix-asm", "locked-mix-job",
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "locked-mix-hash"));
        h.Files.Write("os", "locked-mix-asm", mixBytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix),
            mixId, art.TaskId, DateTimeOffset.UtcNow, null));
    }

    static ProductionActionApiCommand AcceptCommand(ProductionScope scope, Guid artifact) =>
        new(scope, Input() with { AcceptArtifactId = artifact, ParentArtifactIds = [artifact] },
            "regression", "asm-accept-" + scope.ShotCode);

    static ProductionActionApiCommand AssembleCommand(ProductionScope scope, Guid artifact) =>
        new(scope, Input() with { ParentArtifactIds = [artifact] },
            "regression", "asm-v1-" + scope.ShotCode,
            AssemblyShots: [(scope.ShotCode, artifact, 5.042m)]);

    static ShotExecutionInput Input() =>
        new(
            ProductionEp01Sh01IngressCanon.Story,
            ProductionEp01Sh01IngressCanon.ActingBeat,
            ProductionEp01Sh01IngressCanon.VisualContract,
            "", "", "5.0s", "", "");

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static HarnessBundle Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var runwayTransport = new FakeRunwayTransport();
        var voiceTransport = new FakeElevenLabsVoiceTransport();
        var falTransport = new FakeFalLipsyncTransport();
        var mixTransport = new FakeFfmpegMixTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(runwayTransport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var officialVoice = new ElevenLabsVoiceExecutionAdapter(voiceTransport);
        var officialLipsync = new FalLipSyncExecutionAdapter(falTransport);
        var officialMix = new FfmpegMixExecutionAdapter(mixTransport);
        var officialAssembly = new FfmpegAssemblyExecutionAdapter(mixTransport);
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits,
            officialVoice: officialVoice, officialLipsync: officialLipsync, officialMix: officialMix,
            officialAssembly: officialAssembly);
        return new HarnessBundle(svc, os, store, files, runwayTransport, voiceTransport, falTransport, mixTransport, scopes);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeRunwayTransport RunwayTransport,
        FakeElevenLabsVoiceTransport VoiceTransport,
        FakeFalLipsyncTransport FalTransport,
        FakeFfmpegMixTransport MixTransport,
        InMemoryProductionScopeFoundationStore Scopes)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeRunwayTransport RunwayTransport { get; } = RunwayTransport;
        public FakeElevenLabsVoiceTransport VoiceTransport { get; } = VoiceTransport;
        public FakeFalLipsyncTransport FalTransport { get; } = FalTransport;
        public FakeFfmpegMixTransport MixTransport { get; } = MixTransport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
    }
}
