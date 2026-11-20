using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01VoiceProductionIngressV1Regression
{
    public const string SuiteId = ProductionEp01Sh01VoiceIngressCanon.SuiteId;

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

        Ok(h.Snap.Draft.Lines.Count == 0, "parent snapshot starts with empty lines");
        var parentId = h.Snap.PerformanceSnapshotId;
        var parentHash = h.Snap.PerformanceHash;
        var videoSel = await h.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video);

        var first = await h.Authority.MaterializeVoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "ingress"));
        var second = await h.Authority.MaterializeVoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "ingress-again"));

        Ok(first.Snapshot.Draft.Lines.Count == 1
            && first.Snapshot.Draft.Lines[0].LineId == ProductionEp01Sh01VoiceIngressCanon.LineId, "T01/T03 canonical lineId");
        Ok(first.Snapshot.Draft.Lines[0].TextIdentity.Text == ProductionEp01Sh01VoiceIngressCanon.LineText, "T02 exact dialogue preserved");
        Ok(first.Snapshot.Draft.Lines[0].CharacterId == ProductionEp01Sh01VoiceIngressCanon.SpeakerCharacterId
            && first.Snapshot.Draft.Characters.First(c => c.CharacterId == "CHAR-003").SpeechRole == PerformanceContractIds.Speaker, "T04 canonical speaker");
        Ok(ProductionVoiceSelectionCanon.VoiceIdOf(first.Selection) == ProductionEp01Sh01VoiceIngressCanon.VoiceId, "T05 approved VoiceId");
        Ok(first.Selection.ProviderId == ProductionEp01Sh01VoiceIngressCanon.ProviderId, "T06 ElevenLabs selected");
        Ok(first.Selection.ModelId == ProductionEp01Sh01VoiceIngressCanon.ModelId, "T07 approved model");
        Ok(first.Selection.Stage == ProductionAiSelectionStages.Voice
            && first.Selection.SelectionMode == ProductionAiSelectionModes.Explicit
            && first.Selection.Status == ProductionAiSelectionStatuses.Active, "T08 VOICE ProductionAiSelection persisted");
        Ok(first.Snapshot.PerformanceSnapshotId != parentId
            && first.Snapshot.PerformanceHash != parentHash, "T09 new snapshot version");
        var parent = await h.Perf.GetAsync(parentId);
        Ok(parent.Draft.Lines.Count == 0 && parent.PerformanceHash == parentHash, "T09 parent snapshot not mutated");
        Ok(first.Snapshot.Draft.StoryRef.StoryTextHash == parent.Draft.StoryRef.StoryTextHash, "T10 Story unchanged");
        Ok(first.Snapshot.Draft.ShotPerformance.Before?.Action == ProductionEp01Sh01IngressCanon.ActingBeat, "T11 ActingBeat unchanged");
        Ok(first.Snapshot.Draft.VisualRef.VisualContractHash == parent.Draft.VisualRef.VisualContractHash, "T12 VisualContract unchanged");
        Ok(first.Snapshot.Source.KeyframeArtifactId == parent.Source.KeyframeArtifactId, "T13 Keyframe unchanged");
        Ok(first.Snapshot.Source.ProductionDurationSec == ProductionDirectorTiming.Ep01Sh01DurationSec, "T14 5.0s timing unchanged");
        Ok(first.Permit.Action == ProductionActions.CreateVoice
            && first.Permit.Status == ProductionExecutionPermitStatuses.Issued
            && first.Permit.ProviderId == ProductionEp01Sh01VoiceIngressCanon.ProviderId, "T15 one-shot CREATE_VOICE permit issued");
        Ok(first.Permit.ConsumedAt is null
            && (await h.Authority.GetIssuedPermitAsync(
                shot.BuildId, shot.ShotCode, ProductionActions.CreateVoice,
                first.Permit.ProviderId, first.Permit.ModelId))?.PermitId == first.Permit.PermitId, "T16 permit not consumed");

        var ready = await h.Os.EvaluateCreateVoiceAsync(shot);
        Ok(ready.Available && ready.PerformanceSnapshotId == first.Snapshot.PerformanceSnapshotId, "T17 CREATE_VOICE readiness");
        var reloaded = await h.Os.GetShotStateAsync(shot);
        var voiceAction = reloaded.Actions.First(a => a.Action == ProductionActions.CreateVoice);
        Ok(voiceAction.Available && reloaded.VoiceArtifactId is null, "T18 reload preserves readiness without CURRENT:VOICE");

        Ok(second.Snapshot.PerformanceSnapshotId == first.Snapshot.PerformanceSnapshotId
            && second.Selection.SelectionId == first.Selection.SelectionId
            && second.Permit.PermitId == first.Permit.PermitId, "T19 duplicate ingress idempotent");

        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
        Ok((await h.Authority.GetSelectionAsync(sh02.BuildId, sh02.ShotCode, ProductionAiSelectionStages.Voice)) is null
            && (await h.Svc.GetShotStateAsync(sh02)).VoiceArtifactId is null, "T20 cross-shot blocked");
        Ok((await h.Authority.GetSelectionAsync(ep99.BuildId, ep99.ShotCode, ProductionAiSelectionStages.Voice)) is null
            && (await h.Svc.GetShotStateAsync(ep99)).VoiceArtifactId is null, "T21 cross-build blocked");

        Ok(h.Voice.ExecuteCount == 0 && h.Transport.CreateCalls == 0, "T22/T23 no provider / no media");
        var videoAfter = await h.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video);
        Ok(videoAfter is not null && videoSel is not null
            && videoAfter.SelectionId == videoSel.SelectionId
            && videoAfter.ProviderId == "runway", "T24 VIDEO selection unchanged");

        try
        {
            await h.Authority.MaterializeVoiceProductionIngressAsync(
                new MaterializeVoiceProductionIngressCommand(sh02, "sh02"));
            Ok(false, "T20 SH02 ingress rejected");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == PerformanceContractIds.CrossShotForbidden, "T20 SH02 ingress rejected");
        }

        Ok(h.Voice.ExecuteCount == 0, "T22 still no voice Start");
    }

    static async Task SeedReadyOfficialAsync(HarnessBundle h, ProductionScope shot)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-VOICE-INGRESS-PIXELS-" + new string('V', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "voice-ingress",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        await h.Authority.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionAiSelectionStages.Video,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "voice-ingress", "locked"));
        var facts = await h.Svc.EvaluateCreateVideoAsync(shot);
        var snap = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionDirectorTiming.Ep01Sh01DurationSec, facts.Story, facts.ActingBeat, facts.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));
        h.KfId = art.ArtifactId;
        h.Snap = snap;
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
        return new HarnessBundle(svc, os, store, transport, scopes, authority, voice, perf);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        FakeRunwayTransport Transport,
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionReadinessAuthority Authority,
        CountingVoiceAdapter Voice,
        PerformanceContractService Perf)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public FakeRunwayTransport Transport { get; } = Transport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public CountingVoiceAdapter Voice { get; } = Voice;
        public PerformanceContractService Perf { get; } = Perf;
        public Guid KfId { get; set; }
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
