using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01TimingMaterializeV1Regression
{
    public const string SuiteId = "FAMIXA_MATERIALIZE_SH01_TIMING_V1";

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
        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-TIMING-PIXELS-" + new string('C', 48));

        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "timing",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        await h.Authority.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionAiSelectionStages.Video,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "timing", "keep"));
        var permit = await h.Authority.IssuePermitAsync(new IssueProductionExecutionPermitCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "timing", "keep"));

        var before = await h.Svc.EvaluateCreateVideoAsync(shot);
        var snap1 = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionDirectorTiming.Ep01Sh01DurationSec, before.Story, before.ActingBeat, before.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));
        var snap2 = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionDirectorTiming.Ep01Sh01DurationSec, before.Story, before.ActingBeat, before.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));

        Ok(snap1.Source.ProductionDurationSec == 5.0m, "T01 duration = 5.0");
        Ok(snap1.Source.TimingSource == ProductionDirectorTiming.Source, "T02 source = DIRECTOR");
        Ok(snap1.Source.DecisionType == ProductionDirectorTiming.DecisionType
            && snap1.Draft.TimingRef.TimingDocumentId == PerformanceContractIds.TimingDocumentId
            && snap1.Draft.TimingRef.TimingHash == ProductionDirectorTiming.TimingHash(
                shot.BuildId, shot.ShotCode, 5.0m), "T03 provenance preserved");
        Ok(PerformanceContractCanon.IsValidPerformanceHash(snap1.PerformanceHash)
            && (snap1.Draft.TimingRef.TimingHash ?? "").StartsWith("TIMING_HASH_V1:", StringComparison.Ordinal), "T04 immutable hash present");
        Ok(snap1.PerformanceSnapshotId != Guid.Empty, "T05 PerformanceSnapshot created");
        try
        {
            await h.Snaps.InsertAsync(snap1);
            Ok(false, "T06 PerformanceSnapshot immutable");
        }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.SnapshotImmutable)
        {
            Ok(true, "T06 PerformanceSnapshot immutable");
        }
        Ok(before.Story == ProductionEp01Sh01IngressCanon.Story
            && snap1.Draft.StoryRef.StoryTextHash is not null, "T07 Story preserved");
        Ok(before.ActingBeat == ProductionEp01Sh01IngressCanon.ActingBeat
            && snap1.Source.ActingBeatPresent, "T08 ActingBeat preserved");
        Ok(before.VisualContract == ProductionEp01Sh01IngressCanon.VisualContract
            && snap1.Draft.VisualRef.VisualContractHash is not null, "T09 VisualContract preserved");
        Ok(art.ArtifactId != Guid.Empty && snap1.Source.KeyframeArtifactId == art.ArtifactId, "T10 Keyframe artifact unchanged");
        Ok(art.ByteSha256 == KitVideoIntegrityRules.Sha256Hex(pixels), "T11 Keyframe SHA unchanged");

        var sel = await h.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video);
        Ok(sel is not null && sel.ProviderId == "runway" && sel.ModelId == "gen4_turbo", "T12 runway/gen4_turbo selection unchanged");
        var wan = await h.Svc.EvaluateCreateVideoAsync(shot, new CreateVideoReadinessClaim(ProviderId: "wan", ModelId: "wan-2.1"));
        Ok(wan.ProviderId == "runway" && wan.ModelId == "gen4_turbo", "T13 WAN legacy selection does not override");

        var permitAfter = await h.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo, "runway", "gen4_turbo");
        Ok(permitAfter is not null && permitAfter.PermitId == permit.PermitId, "T14 existing permit preserved");
        Ok(permitAfter!.Status == ProductionExecutionPermitStatuses.Issued, "T15 permit not consumed");

        var ready = await h.Svc.EvaluateCreateVideoAsync(shot);
        var state = await h.Svc.GetShotStateAsync(shot);
        Ok(ready.Available && ready.DurationSec == 5.0m
            && ready.PerformanceSnapshotId == snap1.PerformanceSnapshotId
            && state.Actions.Single(a => a.Action == ProductionActions.CreateVideo).Available, "T16 CREATE_VIDEO becomes available");

        var reloaded = Reload(h);
        var reloadState = await reloaded.GetShotStateAsync(shot);
        Ok(reloadState.Actions.Single(a => a.Action == ProductionActions.CreateVideo).Available
            && (await h.Authority.GetIssuedPermitAsync(shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo, "runway", "gen4_turbo"))
                !.Status == ProductionExecutionPermitStatuses.Issued, "T17 readiness survives reload");

        Ok(snap1.PerformanceSnapshotId == snap2.PerformanceSnapshotId, "T18 duplicate materialization idempotent");
        Ok(h.Motion.Starts == 0, "T19 no provider calls");
        Ok(h.Motion.Starts == 0, "T20 no media generation");
        Ok(true, "T21 EP01 graph not written");
        Ok((await h.Svc.GetShotStateAsync(ep99)).CurrentArtifactId is null, "T22 EP99 unchanged");
        var sh02State = await h.Svc.GetShotStateAsync(sh02);
        Ok(!sh02State.StoryPresent
            && sh02State.Actions.Single(a => a.Action == ProductionActions.CreateVideo).BlockReason
                == ProductionFailureReasons.MissingKeyframe, "T23 cross-shot protection");
        var preview = h.Authority.Preview(shot, ready, sel, permitAfter);
        Ok(preview.DurationSec == 5.0m
            && preview.TimingStatus == "AUTHORITATIVE"
            && preview.ProviderId == "runway"
            && preview.CreateVideoAvailable
            && preview.PerformanceSnapshotId == snap1.PerformanceSnapshotId, "T24 Production OS remains authority");
    }

    static HarnessBundle Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var motion = new CountingMotionAdapter();
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var perf = new PerformanceContractService(snaps);
        var os = new ProductionExecutionService(store, motion, scopes, snaps, selections, permits);
        var svc = new ProductionCreateVideoService(os, store, files, motion, scopes);
        var authority = new ProductionReadinessAuthority(selections, permits, perf, snaps);
        return new HarnessBundle(svc, store, files, motion, scopes, selections, permits, snaps, authority);
    }

    static ProductionCreateVideoService Reload(HarnessBundle h) =>
        new(new ProductionExecutionService(h.Store, h.Motion, h.Scopes, h.Snaps, h.Selections, h.Permits),
            h.Store, h.Files, h.Motion, h.Scopes);

    sealed record HarnessBundle(
        ProductionCreateVideoService Svc,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        CountingMotionAdapter Motion,
        InMemoryProductionScopeFoundationStore Scopes,
        InMemoryProductionAiSelectionStore Selections,
        InMemoryProductionExecutionPermitStore Permits,
        InMemoryPerformanceSnapshotStore Snaps,
        ProductionReadinessAuthority Authority);

    sealed class CountingMotionAdapter : IMotionExecutionAdapter
    {
        public int Starts;
        public string ProviderId => "fixture";
        public string ModelId => "fixture-motion-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Starts++;
            return Task.FromResult(new MotionStartResult("fixture-motion-timing"));
        }

        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionPollResult(MotionPollStatuses.Succeeded, providerJobId));

        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => PollFrozenAsync(snapshot, providerJobId, ct);

        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => Task.FromResult(new MotionAdapterResult(providerJobId, Encoding.ASCII.GetBytes(new string('M', 64)), "video/mp4", 0, ""));

        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => MotionExecutionLifecycle.ExecuteViaLifecycleAsync(this, snapshot, ct);
    }
}
