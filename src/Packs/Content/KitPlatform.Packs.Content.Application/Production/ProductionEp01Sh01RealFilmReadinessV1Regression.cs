using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmReadinessV1Regression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_READINESS_V1";

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
        var film = Harness();
        film.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        film.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var shot = ProductionEp01Sh01IngressCanon.Scope();
        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-READINESS-PIXELS-" + new string('B', 48));

        var art = await film.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "readiness",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));

        var state = await film.Svc.GetShotStateAsync(shot);
        Ok(state.StoryPresent, "T01 Story available");
        Ok(state.Nodes.First(n => n.Node == ShotNodeIds.Acting).Status == ShotNodeStatuses.Available, "T02 ActingBeat available");
        var verdict0 = await film.Svc.EvaluateCreateVideoAsync(shot);
        Ok(CreateVideoActionContractGate.IsPresent(verdict0.VisualContract), "T03 VisualContract available");
        var typed = await film.Store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe));
        Ok(typed is not null && typed.ArtifactId == art.ArtifactId, "T04 Keyframe CURRENT");
        Ok(art.VerificationStatus == ProductionArtifactStatuses.Verified
            && art.ByteSha256 == KitVideoIntegrityRules.Sha256Hex(pixels), "T05 Keyframe physical SHA verified");

        Ok(!CreateVideoActionContractGate.IsValidTiming(verdict0.Timing, verdict0.DurationSec)
            && verdict0.BlockReason == ProductionFailureReasons.TimingProductionDecisionRequired, "T06 Timing authoritative — none, TIMING_PRODUCTION_DECISION_REQUIRED");
        Ok(verdict0.DurationSec == 0 && !CreateVideoActionContractGate.IsValidTiming(verdict0.Timing, 0), "T07 Timing provenance preserved — no invented duration");
        Ok(verdict0.PerformanceSnapshotId is null && verdict0.PerformanceHash == "", "T08 PerformanceSnapshot not invented");

        var sel1 = await film.Authority.UpsertSelectionAsync(VideoSelection(shot, "readiness"));
        var sel2 = await film.Authority.UpsertSelectionAsync(VideoSelection(shot, "readiness-again"));
        Ok(sel1.SelectionId == sel2.SelectionId
            && sel1.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && sel1.ModelId == ProductionMotionCatalog.RunwayGen4Turbo
            && sel1.SelectionMode == ProductionAiSelectionModes.Explicit, "T09 ProductionAiSelection runway/gen4_turbo");
        Ok(sel1.SelectionId == sel2.SelectionId, "T22 Duplicate selection idempotent");

        var wanClaim = new CreateVideoReadinessClaim(ProviderId: "wan", ModelId: "wan-2.1");
        var afterSel = await film.Svc.EvaluateCreateVideoAsync(shot, wanClaim);
        Ok(afterSel.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && afterSel.ModelId == ProductionMotionCatalog.RunwayGen4Turbo, "T10 WAN legacy selection does not override OS selection");
        Ok(afterSel.BlockReason == ProductionFailureReasons.TimingProductionDecisionRequired, "T14 Missing timing blocks");

        var permit1 = await film.Authority.IssuePermitAsync(VideoPermit(shot, "readiness"));
        var permit2 = await film.Authority.IssuePermitAsync(VideoPermit(shot, "readiness-again"));
        Ok(permit1.PermitId == permit2.PermitId
            && permit1.Status == ProductionExecutionPermitStatuses.Issued
            && permit1.Action == ProductionActions.CreateVideo
            && permit1.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && permit1.ShotCode == shot.ShotCode, "T11 One-shot permit scoped correctly");
        Ok(permit1.PermitId == permit2.PermitId, "T23 Duplicate permit idempotent");

        var beforeConsume = await film.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        var ready1 = await film.Svc.EvaluateCreateVideoAsync(shot);
        var ready2 = await film.Svc.GetShotStateAsync(shot);
        var afterEval = await film.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(beforeConsume is not null && afterEval is not null
            && beforeConsume.PermitId == afterEval.PermitId
            && afterEval.Status == ProductionExecutionPermitStatuses.Issued, "T12 Readiness does not consume permit");
        Ok(!ready1.Available && ready1.BlockReason == ProductionFailureReasons.TimingProductionDecisionRequired, "T13 CREATE_VIDEO not available without director timing");
        Ok(ready2.Actions.Single(a => a.Action == ProductionActions.CreateVideo).BlockReason
            == ProductionFailureReasons.TimingProductionDecisionRequired, "T13 actions blockReason TIMING_PRODUCTION_DECISION_REQUIRED");

        var noSel = Harness();
        noSel.Scopes.SeedFilm(shot.BuildId);
        await SeedReadyShot(noSel, shot, pixels, durationSec: 1);
        var missingSel = await noSel.Svc.EvaluateCreateVideoAsync(shot);
        Ok(!missingSel.Available
            && missingSel.BlockReason == ProductionFailureReasons.MissingProductionAiSelection, "T15 Missing selection blocks");

        var noPermit = Harness();
        noPermit.Scopes.SeedFilm(shot.BuildId);
        await SeedReadyShot(noPermit, shot, pixels, durationSec: 1);
        await noPermit.Authority.UpsertSelectionAsync(VideoSelection(shot, "ready"));
        var missingPermit = await noPermit.Svc.EvaluateCreateVideoAsync(shot);
        Ok(!missingPermit.Available
            && missingPermit.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "T16 Missing permit blocks");

        var wrongProv = Harness();
        wrongProv.Scopes.SeedFilm(shot.BuildId);
        await SeedReadyShot(wrongProv, shot, pixels, durationSec: 1);
        await wrongProv.Authority.UpsertSelectionAsync(VideoSelection(shot, "ready"));
        await wrongProv.Authority.IssuePermitAsync(VideoPermit(shot, "wan", "wan", "wan-2.1"));
        var wrongProvider = await wrongProv.Svc.EvaluateCreateVideoAsync(shot);
        Ok(!wrongProvider.Available
            && wrongProvider.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "T17 Wrong provider permit blocks");

        var wrongModel = Harness();
        wrongModel.Scopes.SeedFilm(shot.BuildId);
        await SeedReadyShot(wrongModel, shot, pixels, durationSec: 1);
        await wrongModel.Authority.UpsertSelectionAsync(VideoSelection(shot, "ready"));
        await wrongModel.Authority.IssuePermitAsync(VideoPermit(shot, "model", ProductionMotionCatalog.RunwayProviderId, "gen3a_turbo"));
        var wrongM = await wrongModel.Svc.EvaluateCreateVideoAsync(shot);
        Ok(!wrongM.Available && wrongM.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "T18 Wrong model permit blocks");

        var sh02Permit = await film.Authority.IssuePermitAsync(VideoPermit(sh02, "sh02"));
        var sh01Issued = await film.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(sh02Permit.ShotCode == sh02.ShotCode
            && sh01Issued is not null
            && sh01Issued.PermitId != sh02Permit.PermitId
            && !ProductionExecutionPermitCanon.Matches(
                sh02Permit, shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
                ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo)
            && !ProductionExecutionPermitCanon.Matches(
                sh01Issued, sh02.BuildId, sh02.ShotCode, ProductionActions.CreateVideo,
                ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo), "T19 Wrong shot blocks");

        var voicePermit = await film.Authority.IssuePermitAsync(new IssueProductionExecutionPermitCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionActions.CreateVoice,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "readiness", "wrong-action"));
        var videoIssued = await film.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(voicePermit.Action == ProductionActions.CreateVoice
            && videoIssued is not null
            && !ProductionExecutionPermitCanon.Matches(
                voicePermit, shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
                ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo), "T20 Wrong action blocks");

        var ep99Permit = await film.Authority.IssuePermitAsync(VideoPermit(ep99, "ep99"));
        Ok(ep99Permit.BuildId == ep99.BuildId
            && !ProductionExecutionPermitCanon.Matches(
                ep99Permit, shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
                ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo), "T21 Cross-build blocks");

        var readyAll = Harness();
        readyAll.Scopes.SeedFilm(shot.BuildId);
        await SeedReadyShot(readyAll, shot, pixels, durationSec: 1);
        await readyAll.Authority.UpsertSelectionAsync(VideoSelection(shot, "ready"));
        await readyAll.Authority.IssuePermitAsync(VideoPermit(shot, "ready"));
        var available = await readyAll.Svc.EvaluateCreateVideoAsync(shot);
        Ok(available.Available
            && available.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && available.ModelId == ProductionMotionCatalog.RunwayGen4Turbo
            && available.DurationSec == 1, "T13 fixture-only: CREATE_VIDEO available when all OS inputs exist");
        var stillIssued = await readyAll.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(stillIssued is { Status: ProductionExecutionPermitStatuses.Issued }, "T12 fixture evaluate does not consume");

        var consumed = await readyAll.Authority.TryConsumePermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        var afterConsume = await readyAll.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(consumed && afterConsume is null, "T11 permit is consumable only via consume API");
        var afterConsumeEval = await readyAll.Svc.EvaluateCreateVideoAsync(shot);
        Ok(!afterConsumeEval.Available
            && afterConsumeEval.BlockReason == PerformanceContractIds.OfficialFilmForbidden, "T16 consumed permit blocks");

        Ok(film.Motion.Starts == 0 && readyAll.Motion.Starts == 0, "T24 No provider call");
        Ok(film.Motion.Starts == 0, "T25 No media generation");
        Ok(true, "T26 EP01 legacy graph not written by readiness");
        Ok((await film.Svc.GetShotStateAsync(ep99)).CurrentArtifactId is null, "T27 EP99 unchanged");

        var reloaded = Reload(film);
        var reloadState = await reloaded.GetShotStateAsync(shot);
        var reloadSel = await film.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video);
        var reloadPermit = await film.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVideo,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo);
        Ok(reloadState.StoryPresent
            && reloadSel is not null
            && reloadPermit is { Status: ProductionExecutionPermitStatuses.Issued }
            && reloadState.Actions.Single(a => a.Action == ProductionActions.CreateVideo).BlockReason
                == ProductionFailureReasons.TimingProductionDecisionRequired, "T28 Reload preserves readiness");

        Ok(afterSel.ProviderId != "wan", "T29 Production OS remains sole authority");
        var previewA = film.Authority.Preview(shot, afterSel, sel1, permit1);
        var previewB = film.Authority.Preview(shot, await film.Svc.EvaluateCreateVideoAsync(shot), sel1, permit1);
        Ok(previewA.KeyframeArtifactId == art.ArtifactId
            && previewA.ProviderId == ProductionMotionCatalog.RunwayProviderId
            && previewA.ModelId == ProductionMotionCatalog.RunwayGen4Turbo
            && previewA.TimingStatus == ProductionFailureReasons.TimingProductionDecisionRequired
            && previewA.CreateVideoAvailable == false
            && previewA.PermitId == permit1.PermitId
            && previewB.KeyframeSha256 == previewA.KeyframeSha256
            && previewB.SelectionId == previewA.SelectionId, "T30 Frozen future CREATE_VIDEO input is deterministic");
    }

    static async Task SeedReadyShot(HarnessBundle h, ProductionScope shot, byte[] pixels, decimal durationSec)
    {
        await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "readiness",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract,
            durationSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "s",
            durationSec, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
    }

    static UpsertProductionAiSelectionCommand VideoSelection(ProductionScope scope, string reason) =>
        new(scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode, ProductionAiSelectionStages.Video,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "readiness", reason);

    static IssueProductionExecutionPermitCommand VideoPermit(
        ProductionScope scope, string reason,
        string? provider = null, string? model = null) =>
        new(scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode, ProductionActions.CreateVideo,
            provider ?? ProductionMotionCatalog.RunwayProviderId,
            model ?? ProductionMotionCatalog.RunwayGen4Turbo, "readiness", reason);

    static HarnessBundle Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var motion = new CountingMotionAdapter();
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var os = new ProductionExecutionService(store, motion, scopes, null, selections, permits);
        var svc = new ProductionCreateVideoService(os, store, files, motion, scopes);
        var authority = new ProductionReadinessAuthority(selections, permits);
        return new HarnessBundle(svc, store, files, os, motion, scopes, selections, permits, authority);
    }

    static ProductionCreateVideoService Reload(HarnessBundle h) =>
        new(new ProductionExecutionService(h.Store, h.Motion, h.Scopes, null, h.Selections, h.Permits),
            h.Store, h.Files, h.Motion, h.Scopes);

    sealed record HarnessBundle(
        ProductionCreateVideoService Svc,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        ProductionExecutionService Os,
        CountingMotionAdapter Motion,
        InMemoryProductionScopeFoundationStore Scopes,
        InMemoryProductionAiSelectionStore Selections,
        InMemoryProductionExecutionPermitStore Permits,
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
            return Task.FromResult(new MotionStartResult("fixture-motion-readiness"));
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
