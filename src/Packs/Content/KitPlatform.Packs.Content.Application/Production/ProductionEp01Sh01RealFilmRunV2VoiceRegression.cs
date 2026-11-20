using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmRunV2VoiceRegression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_RUN_V2_VOICE";

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
        var shot = ProductionEp01Sh01IngressCanon.Scope();
        await SeedReadyOfficialAsync(blocked, shot);
        Ok(await Rejects(() => blocked.Svc.CreateVoiceAsync(VoiceCommand(shot, blocked.Snap, "CHAR-TEST", "VOICE-TEST", "line-test", blocked.Credential)),
            ProductionFailureReasons.CanonicalVoiceInputNotBound), "T09 TEST identity without OS bind stops before Start");
        Ok(blocked.VoiceTransport.SynthesizeCount == 0, "T12 blocked path does not call ElevenLabs");

        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        await SeedReadyOfficialAsync(h, shot);
        var ingress = await h.Authority.MaterializeVoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "voice-v2"));
        h.Snap = ingress.Snapshot;
        Ok(ingress.Snapshot.Draft.Lines[0].LineId == ProductionEp01Sh01VoiceIngressCanon.LineId, "T01 canonical line resolved");
        Ok(ingress.Snapshot.Draft.Lines[0].TextIdentity.Text == ProductionEp01Sh01VoiceIngressCanon.LineText, "T02 exact text frozen");
        Ok(ingress.Snapshot.Draft.Lines[0].CharacterId == ProductionEp01Sh01VoiceIngressCanon.SpeakerCharacterId, "T03 speaker CHAR-003");
        Ok(ProductionVoiceSelectionCanon.VoiceIdOf(ingress.Selection) == ProductionEp01Sh01VoiceIngressCanon.VoiceId, "T04 VoiceId");
        Ok(ingress.Selection.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId, "T05 provider elevenlabs");
        Ok(ingress.Selection.ModelId == ProductionVoiceCatalog.ElevenV3, "T06 model eleven_v3");
        Ok(ingress.Snapshot.PerformanceSnapshotId != Guid.Empty, "T07 PerformanceSnapshot frozen");
        Ok(ProductionDirectorTiming.HasDirectorTiming(ingress.Snapshot.Source), "ingress snapshot has director timing");
        Ok(ingress.Permit.PermitId != Guid.Empty
            && ingress.Permit.Action == ProductionActions.CreateVoice
            && ingress.Permit.Status == ProductionExecutionPermitStatuses.Issued
            && ingress.Permit.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId
            && ingress.Permit.ModelId == ProductionVoiceCatalog.ElevenV3, "T10 permit scope correct");
        Ok((await h.Authority.GetIssuedPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVoice,
            ingress.Permit.ProviderId, ingress.Permit.ModelId))!.PermitId == ingress.Permit.PermitId,
            "evaluate/ingress does not consume permit");
        var ready = await h.Os.EvaluateCreateVoiceAsync(shot);
        Ok(ready.Available
            && ready.LineId == ProductionEp01Sh01VoiceIngressCanon.LineId
            && ready.VoiceId == ProductionEp01Sh01VoiceIngressCanon.VoiceId
            && ready.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId,
            "CREATE_VOICE ready: " + (ready.BlockReason ?? ready.LineId ?? "empty"));

        var first = await h.Svc.CreateVoiceAsync(VoiceCommand(shot, h.Snap, "CHAR-TEST", "VOICE-TEST", "line-test", h.Credential));
        Ok(first.Request.UserAction == ProductionActions.CreateVoice, "Request CREATE_VOICE");
        var snap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap is not null
            && snap.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId
            && snap.ModelId == ProductionVoiceCatalog.ElevenV3
            && snap.Story == ProductionEp01Sh01VoiceIngressCanon.LineText
            && ProductionVoiceLineIdentity.Matches(snap, ProductionEp01Sh01VoiceIngressCanon.LineId)
            && ProductionVoiceFreeze.VoiceIdFromSnapshot(snap) == ProductionEp01Sh01VoiceIngressCanon.VoiceId
            && !snap.ShotContract.Contains("CHAR-TEST", StringComparison.Ordinal)
            && !snap.ShotContract.Contains("VOICE-TEST", StringComparison.Ordinal)
            && snap.PerformanceSnapshotId == ingress.Snapshot.PerformanceSnapshotId, "T08 InputSnapshot immutable canonical");
        Ok(h.VoiceTransport.SynthesizeCount == 1
            && !string.IsNullOrWhiteSpace(first.Attempt.ProviderJobId)
            && first.Attempt.ProviderJobId == h.VoiceTransport.LastProviderJobId, "T12 exactly one ElevenLabs generation");
        Ok(first.Artifact.Role == ProductionArtifactRoles.Voice
            && first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && first.Artifact.MimeType == ProductionTestWav.Mime
            && first.Artifact.DurationSec > 0
            && first.Artifact.SizeBytes >= 32
            && first.Artifact.ByteSha256.Length == 64, "T14-T18 physical VOICE VERIFIED");
        var current = await h.Store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        Ok(current is not null && current.ArtifactId == first.Artifact.ArtifactId, "T19 CURRENT:VOICE bound");
        var resolved = await h.Svc.GetCurrentVoiceAsync(shot, ProductionEp01Sh01VoiceIngressCanon.LineId);
        Ok(!resolved.IsEmpty
            && resolved.ArtifactId == first.Artifact.ArtifactId
            && resolved.StorageKey == first.Artifact.StorageKey
            && resolved.ByteSha256 == first.Artifact.ByteSha256, "T15 SHA + storageKey recorded");
        Ok(resolved.LineId == ProductionEp01Sh01VoiceIngressCanon.LineId
            && resolved.ProviderId == ProductionVoiceCatalog.ElevenLabsProviderId
            && resolved.ModelId == ProductionVoiceCatalog.ElevenV3, "canonical identity on CURRENT:VOICE");

        var after = await h.Authority.GetLatestPermitAsync(
            shot.BuildId, shot.ShotCode, ProductionActions.CreateVoice,
            ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3);
        Ok(after is { Status: ProductionExecutionPermitStatuses.Consumed, PermitId: var pid }
            && pid == ingress.Permit.PermitId, "T11 permit consumed exactly once");

        var replay = await h.Svc.CreateVoiceAsync(VoiceCommand(shot, h.Snap, "CHAR-TEST", "VOICE-TEST", "line-test", h.Credential));
        Ok(replay.Request.RequestId == first.Request.RequestId
            && replay.Task.TaskId == first.Task.TaskId
            && replay.Attempt.AttemptId == first.Attempt.AttemptId
            && replay.Artifact.ArtifactId == first.Artifact.ArtifactId
            && h.VoiceTransport.SynthesizeCount == 1, "T13/T21 replay does not call provider again");

        Ok(current!.ArtifactId != Guid.Empty
            && first.Artifact.ArtifactId.ToString("D") != ProductionEp01Sh01VoiceIngressCanon.HistoricalVoiceArtifactId,
            "T22 historical audio does not become CURRENT");

        var video = await h.Store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        Ok(video is not null && video.ArtifactId == h.VideoId, "T25 VIDEO_TAKE unchanged");
        Ok(h.RunwayTransport.CreateCalls == 0, "T28 no Runway call");

        var sh02 = new ProductionScope(shot.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");
        Ok((await h.Svc.GetShotStateAsync(sh02)).VoiceArtifactId is null, "T23 cross-shot blocked");
        Ok((await h.Svc.GetShotStateAsync(ep99)).VoiceArtifactId is null, "T24/T27 EP99 unchanged");
        Ok((await h.Svc.GetShotStateAsync(shot)).VoiceArtifactId == first.Artifact.ArtifactId, "T26 EP01 SH01 voice bound only");

        var net = Harness(paidNetwork: true);
        net.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedReadyOfficialAsync(net, shot);
        var netIngress = await net.Authority.MaterializeVoiceProductionIngressAsync(
            new MaterializeVoiceProductionIngressCommand(shot, "voice-v2-net"));
        net.Snap = netIngress.Snapshot;
        Ok(await Rejects(() => net.Svc.CreateVoiceAsync(VoiceCommand(shot, net.Snap, "CHAR-003",
            ProductionEp01Sh01VoiceIngressCanon.VoiceId, ProductionEp01Sh01VoiceIngressCanon.LineId, net.Credential)),
            ProductionFailureReasons.PaidNetworkForbidden), "paid official film requires AuthorizePaidNetwork");
        Ok(net.VoiceTransport.SynthesizeCount == 0, "unauthorized paid path does not Start");
        var executed = await net.Svc.CreateVoiceAsync(VoiceCommand(shot, net.Snap, "CHAR-003",
            ProductionEp01Sh01VoiceIngressCanon.VoiceId, ProductionEp01Sh01VoiceIngressCanon.LineId, net.Credential, authorize: true));
        Ok(executed.Artifact.Role == ProductionArtifactRoles.Voice
            && net.VoiceTransport.SynthesizeCount == 1
            && (await net.Authority.GetLatestPermitAsync(
                shot.BuildId, shot.ShotCode, ProductionActions.CreateVoice,
                ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3))
                !.Status == ProductionExecutionPermitStatuses.Consumed, "authorized official Start consumes once");
        var replayNet = await net.Svc.CreateVoiceAsync(VoiceCommand(shot, net.Snap, "CHAR-003",
            ProductionEp01Sh01VoiceIngressCanon.VoiceId, ProductionEp01Sh01VoiceIngressCanon.LineId, net.Credential, authorize: true));
        Ok(replayNet.Artifact.ArtifactId == executed.Artifact.ArtifactId
            && net.VoiceTransport.SynthesizeCount == 1, "authorized replay does not Start again");
        Ok(net.RunwayTransport.CreateCalls == 0, "T30 no other AI provider");
    }

    static async Task SeedReadyOfficialAsync(HarnessBundle h, ProductionScope shot)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-SH01-VOICE-V2-PIXELS-" + new string('R', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "voice-v2",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        await h.Authority.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
            shot.BuildId, shot.ShotCode, shot.EpisodeCode, shot.SceneCode, ProductionAiSelectionStages.Video,
            ProductionMotionCatalog.RunwayProviderId, ProductionMotionCatalog.RunwayGen4Turbo, "voice-v2", "locked"));
        var facts = await h.Svc.EvaluateCreateVideoAsync(shot);
        var snap = await h.Authority.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
            shot, ProductionDirectorTiming.Ep01Sh01DurationSec, facts.Story, facts.ActingBeat, facts.VisualContract,
            art.ArtifactId, art.ByteSha256, "director"));
        var videoBytes = ProductionTestMp4.Encode("EP01-SH01-LOCKED-VIDEO", 5);
        var videoId = Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae");
        var sha = KitVideoIntegrityRules.Sha256Hex(videoBytes);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            videoId, art.TaskId, art.AttemptId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
            videoBytes.Length, 5.042m, sha, "os/locked-video-v2", "locked-job", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "locked-video-hash"));
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
            videoId, art.TaskId, DateTimeOffset.UtcNow, null));
        h.KfId = art.ArtifactId;
        h.VideoId = videoId;
        h.Snap = snap;
    }

    static ProductionActionApiCommand VoiceCommand(
        ProductionScope scope, PerformanceSnapshot? snap, string characterId, string voiceId, string lineId,
        ProductionCredentialReference credential, bool authorize = false) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh01VoiceIngressCanon.LineText,
                ProductionEp01Sh01IngressCanon.ActingBeat,
                ProductionEp01Sh01IngressCanon.VisualContract,
                "", "", "5.0s", "", "",
                LineId: lineId,
                SpeakerCharacterId: characterId,
                VoiceId: voiceId,
                PerformanceSnapshotId: snap?.PerformanceSnapshotId,
                PerformanceHash: snap?.PerformanceHash),
            "regression",
            "voice-v2-" + scope.ShotCode,
            AuthorizePaidNetwork: authorize,
            Credential: credential);

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
        var officialVoice = new ElevenLabsVoiceExecutionAdapter(voiceTransport);
        var pin = ProductionOfficialHarnessCredentialFixture.ElevenLabs();
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits, pin.Resolver);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits, officialVoice: officialVoice);
        var authority = new ProductionReadinessAuthority(selections, permits, perf, snaps);
        return new HarnessBundle(svc, os, store, runwayTransport, voiceTransport, scopes, authority, pin.Reference);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        FakeRunwayTransport RunwayTransport,
        FakeElevenLabsVoiceTransport VoiceTransport,
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionReadinessAuthority Authority,
        ProductionCredentialReference Credential)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public FakeRunwayTransport RunwayTransport { get; } = RunwayTransport;
        public FakeElevenLabsVoiceTransport VoiceTransport { get; } = VoiceTransport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public ProductionCredentialReference Credential { get; } = Credential;
        public Guid KfId { get; set; }
        public Guid VideoId { get; set; }
        public PerformanceSnapshot Snap { get; set; } = null!;
    }

    sealed class NetworkFakeElevenLabsVoiceTransport : FakeElevenLabsVoiceTransport
    {
        public override bool RequiresNetwork => true;
        protected override string ProviderJobPrefix => "elevenlabs-net-";
    }
}
