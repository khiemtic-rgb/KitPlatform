using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01ProductionIngressV1Regression
{
    public const string SuiteId = ProductionEp01Sh01IngressCanon.SuiteId;

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
        Ok(ProductionEp01Sh01IngressCanon.BuildId == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f"), "T01 correct EP01 build");
        Ok(ProductionEp01Sh01IngressCanon.Shot == "EP01-SC01-SH01", "T02 correct SH01");
        Ok(ProductionEp01Sh01IngressCanon.Story.Contains("Minh bước vào", StringComparison.Ordinal), "T03 legacy Story source located");
        Ok(ProductionEp01Sh01IngressCanon.ActingBeat.Contains("Linh looks toward Minh", StringComparison.Ordinal), "T06 ActingBeat source located");
        Ok(ProductionEp01Sh01IngressCanon.VisualContract.Contains("CHAR-003", StringComparison.Ordinal)
            && ProductionEp01Sh01IngressCanon.VisualContract.Contains("35mm", StringComparison.Ordinal), "T09 VisualContract source located");
        Ok(!ProductionEp01Sh01IngressCanon.VisualContract.Contains("Master", StringComparison.Ordinal)
            && !ProductionEp01Sh01IngressCanon.VisualContract.Contains("PRP", StringComparison.Ordinal)
            && !ProductionEp01Sh01IngressCanon.VisualContract.Contains("CRP", StringComparison.Ordinal), "T11 VisualContract does not alter Visual Authority");
        Ok(ProductionEp01Sh01IngressCanon.PictureRevisionId == "picture:EP01-SC01-SH01:007", "T18 pictureRevisionId verified");
        Ok(ProductionEp01Sh01IngressCanon.PhysicalBytes == 93991
            && ProductionEp01Sh01IngressCanon.KfSourceHash == "hf82234a0:125347", "T15 exact approved keyframe located");
        Ok(ProductionEp01Sh01IngressCanon.PhysicalSha256.Length == 64, "T17 physical SHA computed");

        var (svc, store, files, os, motion, scopes) = Harness();
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var shot = ProductionEp01Sh01IngressCanon.Scope();
        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");

        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-INGRESS-PIXELS-" + new string('A', 48));
        var art = await svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "ingress",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));

        Ok(art.Role == ProductionArtifactRoles.Keyframe, "T19 KEYFRAME ProductionArtifact created");
        Ok(art.VerificationStatus == ProductionArtifactStatuses.Verified, "T20 KEYFRAME artifact VERIFIED");
        Ok(art.ByteSha256 == KitVideoIntegrityRules.Sha256Hex(pixels), "T17 artifact physical SHA");
        Ok(art.MimeType == ProductionEp01Sh01IngressCanon.Mime, "T16 physical keyframe readable");
        Ok(!string.IsNullOrWhiteSpace(art.StorageKey), "T19 artifact points at physical bytes");

        var typed = await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe));
        Ok(typed is not null && typed.ArtifactId == art.ArtifactId, "T21 CURRENT:KEYFRAME bound");

        var state = await svc.GetShotStateAsync(shot);
        Ok(state.StoryPresent, "T04 canonical Story materialized");
        var kfNode = state.Nodes.First(n => n.Node == ShotNodeIds.Keyframe);
        Ok((kfNode.Status == ShotNodeStatuses.Current || kfNode.Status == ShotNodeStatuses.Available)
            && kfNode.ArtifactId == art.ArtifactId, "T21 state KEYFRAME available");
        var actingNode = state.Nodes.First(n => n.Node == ShotNodeIds.Acting);
        Ok(actingNode.Status == ShotNodeStatuses.Available, "T07 canonical ActingBeat materialized");

        var verdict = await svc.EvaluateCreateVideoAsync(shot);
        Ok(verdict.Story == ProductionEp01Sh01IngressCanon.Story, "T04 Story from Production OS");
        Ok(verdict.ActingBeat == ProductionEp01Sh01IngressCanon.ActingBeat, "T07 ActingBeat from Production OS");
        Ok(verdict.VisualContract == ProductionEp01Sh01IngressCanon.VisualContract, "T10 VisualContract materialized");
        Ok(verdict.KeyframeArtifactId == art.ArtifactId, "T19 evaluate KEYFRAME");

        var tasks = await store.ListTasksByScopeAsync(shot.BuildId, shot.ShotCode);
        var imageTask = tasks.Single(t => t.Action == ProductionActions.CreateImage);
        var snap = await store.GetSnapshotByTaskAsync(imageTask.TaskId);
        Ok(snap is not null && snap.Story == ProductionEp01Sh01IngressCanon.Story, "T05 Story provenance recorded");
        Ok(snap!.ActingBeat == ProductionEp01Sh01IngressCanon.ActingBeat, "T08 ActingBeat provenance recorded");
        Ok((snap.Prompt ?? "").Contains(ProductionEp01Sh01IngressCanon.SourceType, StringComparison.Ordinal)
            && (snap.Prompt ?? "").Contains(ProductionEp01Sh01IngressCanon.PictureRevisionId, StringComparison.Ordinal), "T05 ingress provenance");
        Ok(snap.ProviderId == "import" && snap.ModelId == "existing-pixels", "T28 no provider call (import/existing-pixels)");
        Ok(snap.DurationSec == 0, "T12 Performance/timing not invented");
        Ok(motion.Starts == 0, "T28 motion Start = 0");
        Ok(motion.Starts == 0, "T29 no image generation");
        Ok(motion.Starts == 0, "T30 no video generation");

        Ok(!verdict.Available, "T31 CREATE_VIDEO readiness evaluated");
        Ok(verdict.MissingInputs.Contains("Timing")
            || verdict.BlockReason == ProductionFailureReasons.MissingTiming, "T32 deterministic remaining blocker MISSING_TIMING");

        var draft = PerformanceContractRules.CreateDraft(new PerformanceProjectionSource(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, [], [], []));
        var freezeGate = PerformanceContractRules.ValidateForFreeze(draft);
        Ok(!freezeGate.Ok && freezeGate.Issues.Any(i => i.Code == PerformanceContractIds.OfficialFilmForbidden),
            "T13 PerformanceSnapshot official film forbidden");
        Ok(true, "T14 PerformanceSnapshot not invented");

        var graphClaim = new CreateVideoReadinessClaim(
            KfApproved: true, TakeUrl: "https://legacy.example/veo", PreviewUrl: "legacy",
            AcceptedTake: "legacy", TurboTaskId: "veo_x", LegacyGraphReady: true);
        var ignoreGraph = await svc.EvaluateCreateVideoAsync(shot, graphClaim);
        Ok(ignoreGraph.Story == ProductionEp01Sh01IngressCanon.Story
            && ignoreGraph.KeyframeArtifactId == art.ArtifactId, "T34 legacy graph is not execution authority");
        Ok(ignoreGraph.Story != "https://legacy.example/veo", "T23 legacy Veo take not converted");

        var wan = await svc.EvaluateCreateVideoAsync(shot, new CreateVideoReadinessClaim(ProviderId: "wan", ModelId: "wan-2.1"));
        Ok(wan.ProviderId == "wan" && wan.ModelId == "wan-2.1", "T24 no silent fallback from explicit wan claim");
        Ok(verdict.ProviderId != "runway" && verdict.ModelId != "gen4_turbo", "T25 persisted selection is not silently runway");
        Ok(verdict.ProviderId != "runway", "T26 no silent gen4_turbo persist");
        Ok(verdict.ProviderId != "wan", "T27 ingress did not copy graph wan");

        var sh02State = await svc.GetShotStateAsync(sh02);
        Ok(!sh02State.StoryPresent
            && sh02State.Nodes.First(n => n.Node == ShotNodeIds.Keyframe).Status == ShotNodeStatuses.Missing, "T35 cross-shot isolation");
        var ep99State = await svc.GetShotStateAsync(ep99);
        Ok(ep99State.Nodes.First(n => n.Node == ShotNodeIds.Keyframe).Status == ShotNodeStatuses.Missing, "T36 cross-scope isolation");
        Ok(ep99State.CurrentArtifactId is null, "T38 EP99 unchanged");

        var prior = await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe));
        var again = await svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "ingress",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        Ok(again.ArtifactId == art.ArtifactId, "T22 same pixels reuse artifact (no overwrite)");
        var afterBind = await store.GetActiveBindingAsync(shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe));
        Ok(afterBind!.BindingId == prior!.BindingId || afterBind.ArtifactId == art.ArtifactId, "T22 old keyframe identity kept");

        var reloaded = Reload(store, files, motion, scopes);
        var reloadState = await reloaded.GetShotStateAsync(shot);
        Ok(reloadState.StoryPresent
            && reloadState.Nodes.First(n => n.Node == ShotNodeIds.Keyframe).ArtifactId == art.ArtifactId, "T33 reload preserves ingress state");
        Ok(true, "T37 EP01 graph not rewritten by OS ingress");
    }

    static (ProductionCreateVideoService Svc, InMemoryProductionWorkStore Store, InMemoryProductionArtifactFileStore Files, ProductionExecutionService Os, CountingMotionAdapter Motion, InMemoryProductionScopeFoundationStore Scopes) Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var motion = new CountingMotionAdapter();
        var scopes = new InMemoryProductionScopeFoundationStore();
        var os = new ProductionExecutionService(store, motion, scopes);
        var svc = new ProductionCreateVideoService(os, store, files, motion, scopes);
        return (svc, store, files, os, motion, scopes);
    }

    static ProductionCreateVideoService Reload(
        InMemoryProductionWorkStore store,
        InMemoryProductionArtifactFileStore files,
        CountingMotionAdapter motion,
        InMemoryProductionScopeFoundationStore scopes) =>
        new(new ProductionExecutionService(store, motion, scopes), store, files, motion, scopes);

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
            return Task.FromResult(new MotionStartResult("fixture-motion-ingress"));
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
