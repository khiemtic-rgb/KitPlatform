using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmVoiceV1Regression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_RUN_V1_VOICE";

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
        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var shot = ProductionEp01Sh01IngressCanon.Scope();
        await SeedReadyOfficialAsync(h, shot);

        Ok(h.Snap.Draft.Lines.Count == 0, "T01 PerformanceSnapshot has no spoken line");
        Ok(h.Snap.Draft.Characters.All(c => (c.LineIds?.Count ?? 0) == 0), "T02 no speaker lineIds on snapshot");
        Ok((await h.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video)) is { ProviderId: "runway" },
            "T04 VIDEO selection is not a Voice selection");

        var noSnap = await h.Os.EvaluateCreateVoiceAsync(shot);
        Ok(!noSnap.Available && noSnap.BlockReason == ProductionFailureReasons.MissingDependency,
            "official film empty-line snapshot cannot resolve CREATE_VOICE");

        Ok(await Rejects(() => h.Svc.CreateVoiceAsync(VoiceCommand(shot, h.Snap, "CHAR-TEST", "VOICE-TEST", "line-test")),
            ProductionFailureReasons.CanonicalVoiceInputNotBound), "T03 TEST VoiceId/speaker not bound to OS");

        Ok(await Rejects(() => h.Svc.CreateVoiceAsync(VoiceCommand(shot, null, "CHAR-003", "some-voice", "L1")),
            ProductionFailureReasons.PerformanceLineMismatch), "empty snapshot lines + claimed line blocked");

        Ok(await Rejects(() => h.Svc.CreateVoiceAsync(VoiceCommand(shot, h.Snap, "CHAR-003", "some-voice", "L1")),
            ProductionFailureReasons.PerformanceLineMismatch), "T01 empty snapshot lines → PERFORMANCE_LINE_MISMATCH");

        Ok(h.Voice.ExecuteCount == 0, "T08/T09 no voice adapter Start");
        Ok(h.Transport.CreateCalls == 0, "T21 no Runway call");

        var video = await h.Store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        Ok(video is null || video.ArtifactId == h.VideoId, "T18 VIDEO_TAKE not replaced");

        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
        Ok((await h.Svc.GetShotStateAsync(sh02)).VoiceArtifactId is null, "T19 cross-shot protection");
        Ok((await h.Svc.GetShotStateAsync(ep99)).VoiceArtifactId is null, "T20/T25 EP99 unchanged");
        Ok((await h.Svc.GetShotStateAsync(shot)).VoiceArtifactId is null, "no CURRENT:VOICE created");
    }

    static async Task SeedReadyOfficialAsync(HarnessBundle h, ProductionScope shot)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-VOICE-BLOCK-PIXELS-" + new string('V', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "voice-v1",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        await h.Authority.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionAiSelectionStages.Video,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "voice-v1", "locked"));
        var facts = await h.Svc.EvaluateCreateVideoAsync(shot);
        var snap = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionDirectorTiming.Ep01Sh01DurationSec, facts.Story, facts.ActingBeat, facts.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));
        h.KfId = art.ArtifactId;
        h.Snap = snap;
    }

    static ProductionActionApiCommand VoiceCommand(
        ProductionScope scope, PerformanceSnapshot? snap, string characterId, string voiceId, string lineId) =>
        new(scope,
            new ShotExecutionInput(
                "Về rồi hả con?",
                ProductionEp01Sh01IngressCanon.ActingBeat,
                ProductionEp01Sh01IngressCanon.VisualContract,
                "", "", "5.0s", "", "",
                LineId: lineId,
                SpeakerCharacterId: characterId,
                VoiceId: voiceId,
                PerformanceSnapshotId: snap?.PerformanceSnapshotId,
                PerformanceHash: snap?.PerformanceHash),
            "regression", "voice-v1-" + lineId + "-" + voiceId);

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static HarnessBundle Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var transport = new FakeRunwayTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(transport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var perf = new PerformanceContractService(snaps);
        var voice = new CountingVoiceAdapter();
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits, voice: voice);
        var authority = new ProductionReadinessAuthority(selections, permits, perf, snaps);
        return new HarnessBundle(svc, os, store, transport, scopes, authority, voice);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        FakeRunwayTransport Transport,
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionReadinessAuthority Authority,
        CountingVoiceAdapter Voice)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public FakeRunwayTransport Transport { get; } = Transport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public CountingVoiceAdapter Voice { get; } = Voice;
        public Guid KfId { get; set; }
        public Guid? VideoId { get; set; }
        public PerformanceSnapshot Snap { get; set; } = null!;
    }

    sealed class CountingVoiceAdapter : IVoiceExecutionAdapter
    {
        public int ExecuteCount { get; private set; }
        public string ProviderId => "fixture";
        public string ModelId => "fixture-voice-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            ExecuteCount++;
            return new FixtureVoiceAdapter().ExecuteFrozenAsync(snapshot, ct);
        }
    }
}
