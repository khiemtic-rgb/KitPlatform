using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmRunV1MixRegression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_RUN_V1_MIX";

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
        var blocked = Harness();
        blocked.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        await SeedLipsyncAsync(blocked, sh01, Guid.NewGuid());
        Ok(await Rejects(() => blocked.Svc.MixVideoAsync(MixCommand(sh01)),
            ProductionFailureReasons.BindingChanged), "SH01 CURRENT:LIPSYNC mismatch stops before FFmpeg");
        Ok(blocked.MixTransport.ExecutionCount == 0, "mismatch does not run FFmpeg");

        var missing = Harness();
        missing.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var sh02 = new ProductionScope(sh01.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        Ok(await Rejects(() => missing.Svc.MixVideoAsync(MixCommand(sh02)),
            ProductionFailureReasons.MissingLipsyncArtifact), "no CURRENT:LIPSYNC → no MIX task");
        Ok(missing.MixTransport.ExecutionCount == 0, "missing lipsync does not run FFmpeg");

        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var lipId = Guid.NewGuid();
        await SeedLipsyncAsync(h, sh02, lipId);
        Ok(await Rejects(() => h.Svc.MixVideoAsync(MixCommand(sh02)),
            ProductionFailureReasons.MissingVideoArtifact), "official SH02 MIX_CONTRACT_V1 requires VIDEO");
        Ok(h.MixTransport.ExecutionCount == 0, "SH02 compose does not remux without VIDEO");
        Ok((await h.Store.GetActiveBindingAsync(
            sh02.BuildId, sh02.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix))) is null,
            "no CURRENT:MIX created");
        Ok((await h.Store.GetActiveBindingAsync(
            sh02.BuildId, sh02.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)))
            ?.ArtifactId == lipId, "CURRENT:LIPSYNC unchanged");
        Ok((await h.Svc.GetShotStateAsync(sh01)).LipsyncArtifactId is null, "SH01 not stolen");
        Ok((await h.Svc.GetShotStateAsync(new ProductionScope(
            ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01"))).LipsyncArtifactId is null,
            "EP99 unchanged");
        Ok(h.FalTransport.StartCount == 0 && h.VoiceTransport.SynthesizeCount == 0 && h.RunwayTransport.CreateCalls == 0,
            "no Fal / ElevenLabs / Runway");
    }

    static async Task SeedLipsyncAsync(HarnessBundle h, ProductionScope shot, Guid lipsyncId)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-MIX-PIXELS-" + new string('R', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "mix-v1",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        var lipBytes = ProductionTestMp4.Encode("EP01-MIX-LIPSYNC-" + shot.ShotCode, 5.042m);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            lipsyncId, art.TaskId, art.AttemptId, ProductionArtifactRoles.Lipsync, ProductionTestMp4.Mime,
            lipBytes.Length, 5.042m, KitVideoIntegrityRules.Sha256Hex(lipBytes), "os/locked-lipsync-mix", "locked-lip-job",
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "locked-lip-hash"));
        h.Files.Write("os", "locked-lipsync-mix", lipBytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            lipsyncId, art.TaskId, DateTimeOffset.UtcNow, null));
        h.LipsyncId = lipsyncId;
    }

    static ProductionActionApiCommand MixCommand(ProductionScope scope) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh01IngressCanon.Story,
                ProductionEp01Sh01IngressCanon.ActingBeat,
                ProductionEp01Sh01IngressCanon.VisualContract,
                "", "", "5.0s", "", ""),
            "regression",
            "mix-v1-" + scope.ShotCode);

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
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits,
            officialVoice: officialVoice, officialLipsync: officialLipsync, officialMix: officialMix);
        var authority = new ProductionReadinessAuthority(selections, permits, new PerformanceContractService(snaps), snaps);
        return new HarnessBundle(svc, os, store, files, runwayTransport, voiceTransport, falTransport, mixTransport, scopes, authority);
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
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionReadinessAuthority Authority)
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
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public Guid LipsyncId { get; set; }
    }
}
