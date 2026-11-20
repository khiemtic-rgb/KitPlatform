using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialSh02CreateVoiceV1Regression
{
    public const string SuiteId = ProductionEp01Sh02VoiceIngressCanon.SuiteId;

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
        var shot = ProductionEp01Sh02VoiceIngressCanon.Scope();
        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        await SeedReadyOfficialAsync(h, shot, bindAcceptedTake: true);

        Ok(h.Snap.Draft.Lines.Count == 0, "parent snapshot starts silent");
        var parentId = h.Snap.PerformanceSnapshotId;
        var parentHash = h.Snap.PerformanceHash;
        var videoSel = await h.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video);

        try
        {
            await h.Authority.MaterializeVoiceProductionIngressAsync(
                new MaterializeVoiceProductionIngressCommand(shot, "sh01-path"));
            Ok(false, "T20 SH01 ingress still rejects SH02");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == PerformanceContractIds.CrossShotForbidden, "T20 SH01 ingress still rejects SH02");
        }

        var first = await h.Authority.MaterializeSh02VoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "ck22"));
        var second = await h.Authority.MaterializeSh02VoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "ck22-again"));

        Ok(first.Snapshot.Draft.Lines.Count == 1
            && first.Snapshot.Draft.Lines[0].LineId == ProductionEp01Sh02VoiceIngressCanon.LineId, "canonical lineId");
        Ok(first.Snapshot.Draft.Lines[0].TextIdentity.Text == PerformanceContractRules.SpokenText(ProductionEp01Sh02VoiceIngressCanon.LineText), "spoken SH02 dialogue drops the stage direction");
        Ok(first.Snapshot.Draft.Lines[0].CharacterId == ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId, "speaker CHAR-001");
        Ok(ProductionVoiceSelectionCanon.VoiceIdOf(first.Selection) == ProductionEp01Sh02VoiceIngressCanon.VoiceId, "CHAR-001 voiceId");
        Ok(!ProductionEp01Sh02VoiceIngressCanon.IsSh01VoiceId(ProductionVoiceSelectionCanon.VoiceIdOf(first.Selection)), "not SH01 voiceId");
        Ok(first.Selection.ProviderId == ProductionEp01Sh02VoiceIngressCanon.ProviderId
            && first.Selection.ModelId == ProductionEp01Sh02VoiceIngressCanon.ModelId, "elevenlabs/eleven_v3");
        Ok(first.Snapshot.PerformanceSnapshotId != parentId
            && first.Snapshot.PerformanceHash != parentHash, "new voice snapshot");
        var parent = await h.Perf.GetAsync(parentId);
        Ok(parent.Draft.Lines.Count == 0 && parent.PerformanceHash == parentHash, "parent snapshot immutable");
        Ok(first.Snapshot.Source.ProductionDurationSec == ProductionEp01Sh02VoiceIngressCanon.DurationSec, "10s timing preserved");
        Ok(first.Permit.Action == ProductionActions.CreateVoice
            && first.Permit.Status == ProductionExecutionPermitStatuses.Issued, "CREATE_VOICE permit issued");
        Ok(second.Snapshot.PerformanceSnapshotId == first.Snapshot.PerformanceSnapshotId
            && second.Selection.SelectionId == first.Selection.SelectionId
            && second.Permit.PermitId == first.Permit.PermitId, "ingress idempotent");

        var ready = await h.Os.EvaluateCreateVoiceAsync(shot);
        Ok(ready.Available
            && ready.LineId == ProductionEp01Sh02VoiceIngressCanon.LineId
            && ready.VoiceId == ProductionEp01Sh02VoiceIngressCanon.VoiceId, "CREATE_VOICE ready");

        var created = await h.Svc.CreateVoiceAsync(VoiceCommand(shot, first.Snapshot, h.Credential));
        Ok(created.Request.UserAction == ProductionActions.CreateVoice, "Request CREATE_VOICE");
        var snap = await h.Store.GetSnapshotByTaskAsync(created.Task.TaskId);
        Ok(snap is not null
            && snap.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId
            && snap.ModelId == ProductionVoiceCatalog.ElevenV3
            && snap.Story == PerformanceContractRules.SpokenText(ProductionEp01Sh02VoiceIngressCanon.LineText)
            && ProductionVoiceLineIdentity.Matches(snap, ProductionEp01Sh02VoiceIngressCanon.LineId)
            && ProductionVoiceFreeze.VoiceIdFromSnapshot(snap) == ProductionEp01Sh02VoiceIngressCanon.VoiceId
            && snap.DurationSec == ProductionEp01Sh02VoiceIngressCanon.DurationSec
            && (snap.ParentArtifactIds ?? []).Contains(ProductionEp01Sh02VoiceIngressCanon.VideoParentArtifactId),
            "frozen input binds VIDEO_TAKE parent");
        Ok(h.VoiceTransport.SynthesizeCount == 1, "exactly one ElevenLabs Start");
        Ok(created.Artifact.Role == ProductionArtifactRoles.Voice
            && created.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && created.Artifact.SizeBytes >= 32
            && created.Artifact.ByteSha256.Length == 64
            && (created.Artifact.ParentArtifactIds ?? []).Contains(ProductionEp01Sh02VoiceIngressCanon.VideoParentArtifactId),
            "VOICE artifact verified + parent bound");

        var current = await h.Store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        Ok(current is not null && current.ArtifactId == created.Artifact.ArtifactId, "CURRENT:VOICE bound");
        var resolved = await h.Svc.GetCurrentVoiceAsync(shot, ProductionEp01Sh02VoiceIngressCanon.LineId);
        Ok(!resolved.IsEmpty
            && resolved.ArtifactId == created.Artifact.ArtifactId
            && resolved.ByteSha256 == created.Artifact.ByteSha256, "CURRENT:VOICE SHA");

        var accept = await h.Svc.AcceptOsAsync(AcceptCommand(shot, created.Artifact.ArtifactId));
        Ok(accept.Artifact.ArtifactId == created.Artifact.ArtifactId, "ACCEPT same VOICE");
        var accepted = await h.Store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice));
        Ok(accepted is not null && accepted.ArtifactId == created.Artifact.ArtifactId, "ACCEPTED:VOICE bound");

        var replay = await h.Svc.CreateVoiceAsync(VoiceCommand(shot, first.Snapshot, h.Credential));
        Ok(replay.Artifact.ArtifactId == created.Artifact.ArtifactId
            && h.VoiceTransport.SynthesizeCount == 1, "replay does not Start again");

        var take = await h.Svc.GetAuthoritativeVideoTakeAsync(shot);
        Ok(take is not null
            && take.ArtifactId == ProductionEp01Sh02VoiceIngressCanon.VideoParentArtifactId
            && take.ByteSha256 == ProductionEp01Sh02VoiceIngressCanon.VideoParentSha
            && take.ByteLength == ProductionEp01Sh02VoiceIngressCanon.VideoParentBytes
            && take.AcceptanceStatus == OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus, "VIDEO parent unchanged");
        Ok(h.RunwayTransport.CreateCalls == 0, "no motion Start");

        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        Ok((await h.Svc.GetShotStateAsync(sh01)).VoiceArtifactId is null, "SH01 voice unchanged");
        try
        {
            await h.Authority.MaterializeSh02VoiceProductionIngressAsync(
                new MaterializeVoiceProductionIngressCommand(sh01, "cross"));
            Ok(false, "SH02 ingress rejects SH01");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == PerformanceContractIds.CrossShotForbidden, "SH02 ingress rejects SH01");
        }

        var missing = Harness();
        missing.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedReadyOfficialAsync(missing, shot, bindAcceptedTake: false);
        var missIngress = await missing.Authority.MaterializeSh02VoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "missing-take"));
        Ok(await Rejects(() => missing.Svc.CreateVoiceAsync(VoiceCommand(shot, missIngress.Snapshot, missing.Credential)),
            ProductionFailureReasons.OutputArtifactNotFound), "missing ACCEPTED:VIDEO_TAKE fail-closed");
        Ok(missing.VoiceTransport.SynthesizeCount == 0, "missing take does not Start");

        var net = Harness(paidNetwork: true);
        net.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedReadyOfficialAsync(net, shot, bindAcceptedTake: true);
        var netIngress = await net.Authority.MaterializeSh02VoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "paid"));
        Ok(await Rejects(() => net.Svc.CreateVoiceAsync(VoiceCommand(shot, netIngress.Snapshot, net.Credential)),
            ProductionFailureReasons.PaidNetworkForbidden), "paid official requires AuthorizePaidNetwork");
        Ok(net.VoiceTransport.SynthesizeCount == 0, "unauthorized paid path does not Start");
        var paid = await net.Svc.CreateVoiceAsync(VoiceCommand(shot, netIngress.Snapshot, net.Credential, authorize: true));
        Ok(paid.Artifact.Role == ProductionArtifactRoles.Voice
            && net.VoiceTransport.SynthesizeCount == 1, "authorized official Start once");

        var ck20 = ProductionOfficialVeoVideoTakeAcceptanceV1Regression.Run();
        Ok(ck20.Count == 0, ck20.Count == 0 ? "CK20 still green" : "CK20:" + string.Join(",", ck20));
        var sh01Ingress = ProductionEp01Sh01VoiceProductionIngressV1Regression.Run();
        Ok(sh01Ingress.Count == 0, sh01Ingress.Count == 0 ? "SH01 T20 still green" : "SH01:" + string.Join(",", sh01Ingress));

        var videoAfter = await h.Authority.GetSelectionAsync(shot.BuildId, shot.ShotCode, ProductionAiSelectionStages.Video);
        Ok(videoAfter is not null && videoSel is not null
            && videoAfter.SelectionId == videoSel.SelectionId, "VIDEO selection unchanged");
    }

    static async Task SeedReadyOfficialAsync(HarnessBundle h, ProductionScope shot, bool bindAcceptedTake)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-SH02-CK22-PIXELS-" + new string('K', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh02IngressCanon.Mime, ProductionEp01Sh02IngressCanon.Story, "ck22",
            ProductionEp01Sh02IngressCanon.ActingBeat, ProductionEp01Sh02IngressCanon.VisualContract, "",
            0, ProductionEp01Sh02IngressCanon.PictureRevisionId, ProductionEp01Sh02IngressCanon.ProvenanceJson()));
        await h.Authority.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionAiSelectionStages.Video,
            "google", "veo-3.1", "ck22", "locked"));
        var facts = await h.Svc.EvaluateCreateVideoAsync(shot);
        var snap = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionEp01Sh02VoiceIngressCanon.DurationSec, facts.Story, facts.ActingBeat, facts.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));
        var engine = new ProductionWorkExecutionEngine(h.Store);
        var req = await engine.CreateRequestAsync(shot, ProductionActions.CreateVideo, "ck22", "vid-ck22-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var videoSnap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId,
            ProductionEp01Sh02IngressCanon.Story, ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract, "motion",
            "compiled", "compiled",
            "s", "a", "v", "c", art.ByteSha256, "vh", "mh", "", "th",
            "google", "veo-3.1", "snap-hash-ck22", DateTimeOffset.UtcNow,
            art.ArtifactId, "16:9", 10, ShotExecutionCompiler.Version, [art.ArtifactId]);
        videoSnap = videoSnap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(videoSnap) };
        await h.Store.InsertSnapshotAsync(videoSnap);
        await h.Store.UpdateTaskAsync(task with
        {
            InputSnapshotId = videoSnap.SnapshotId,
            ProviderId = "google",
            ModelId = "veo-3.1",
            Status = ProductionTaskStatuses.ProviderFrozen,
        });
        var attempt = await engine.StartAttemptAsync(task.TaskId, videoSnap.SnapshotHash);
        var take = new ProductionArtifact(
            ProductionEp01Sh02VoiceIngressCanon.VideoParentArtifactId,
            task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake,
            "video/mp4", ProductionEp01Sh02VoiceIngressCanon.VideoParentBytes, 10,
            ProductionEp01Sh02VoiceIngressCanon.VideoParentSha, "os/official-sh02-take", attempt.ProviderJobId,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, videoSnap.SnapshotHash, [art.ArtifactId]);
        await h.Store.InsertArtifactAsync(take);
        await h.Store.UpdateTaskAsync((await h.Store.GetTaskAsync(task.TaskId))! with
        {
            Status = ProductionTaskStatuses.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        if (bindAcceptedTake)
        {
            await h.Store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), shot.BuildId, shot.ShotCode,
                OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, take.ArtifactId, task.TaskId,
                DateTimeOffset.UtcNow, null));
        }
        h.KfId = art.ArtifactId;
        h.Snap = snap;
    }

    static ProductionActionApiCommand VoiceCommand(
        ProductionScope scope, PerformanceSnapshot? snap, ProductionCredentialReference credential, bool authorize = false) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh02VoiceIngressCanon.LineText,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "10.0s", "", "",
                LineId: ProductionEp01Sh02VoiceIngressCanon.LineId,
                SpeakerCharacterId: ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId,
                VoiceId: ProductionEp01Sh02VoiceIngressCanon.VoiceId,
                DurationSec: ProductionEp01Sh02VoiceIngressCanon.DurationSec,
                PerformanceSnapshotId: snap?.PerformanceSnapshotId,
                PerformanceHash: snap?.PerformanceHash),
            "regression",
            "ck22-voice-" + scope.ShotCode,
            AuthorizePaidNetwork: authorize,
            Credential: credential);

    static ProductionActionApiCommand AcceptCommand(ProductionScope scope, Guid artifactId) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh02VoiceIngressCanon.LineText,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "10.0s", "", "",
                AcceptArtifactId: artifactId),
            "regression",
            "ck22-accept-" + artifactId.ToString("N"));

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static HarnessBundle Harness(bool paidNetwork = false)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var runwayTransport = new FakeRunwayTransport();
        var voiceTransport = paidNetwork ? new NetworkFakeElevenLabsVoiceTransport() : new FakeElevenLabsVoiceTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(runwayTransport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var perf = new PerformanceContractService(snaps);
        var pin = ProductionOfficialHarnessCredentialFixture.ElevenLabs();
        var officialVoice = new ElevenLabsVoiceExecutionAdapter(voiceTransport);
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits, pin.Resolver);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits, officialVoice: officialVoice);
        var authority = new ProductionReadinessAuthority(selections, permits, perf, snaps);
        return new HarnessBundle(svc, os, store, runwayTransport, voiceTransport, scopes, authority, perf, pin.Reference);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        FakeRunwayTransport RunwayTransport,
        FakeElevenLabsVoiceTransport VoiceTransport,
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionReadinessAuthority Authority,
        PerformanceContractService Perf,
        ProductionCredentialReference Credential)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public FakeRunwayTransport RunwayTransport { get; } = RunwayTransport;
        public FakeElevenLabsVoiceTransport VoiceTransport { get; } = VoiceTransport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public PerformanceContractService Perf { get; } = Perf;
        public ProductionCredentialReference Credential { get; } = Credential;
        public Guid KfId { get; set; }
        public PerformanceSnapshot Snap { get; set; } = null!;
    }

    sealed class NetworkFakeElevenLabsVoiceTransport : FakeElevenLabsVoiceTransport
    {
        public override bool RequiresNetwork => true;
        protected override string ProviderJobPrefix => "elevenlabs-net-";
    }
}
