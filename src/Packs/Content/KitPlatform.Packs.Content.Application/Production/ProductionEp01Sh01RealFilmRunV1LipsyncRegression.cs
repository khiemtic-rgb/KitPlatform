using System.Security.Cryptography;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEp01Sh01RealFilmRunV1LipsyncRegression
{
    public const string SuiteId = "FAMIXA_REAL_FILM_RUN_V1_LIPSYNC";
    const string FixtureSecret = "ck23b-in-memory-fixture-secret";
    static readonly Guid HarnessTenantId = Guid.Parse("c23bc23b-c23b-4c23-8c23-c23bc23bc23b");
    static readonly Guid FixtureCredentialId = Guid.Parse("c23b0002-c23b-4c23-8c23-c23b00000002");
    static readonly Guid FixtureCredentialVersionId = Guid.Parse("c23b0003-c23b-4c23-8c23-c23b00000003");

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
        Ok(ProductionLipsyncSpeakerTarget.TryResolve("CHAR-003", out var pin)
            && pin.X01 == 0.63 && pin.Y01 == 0.25 && pin.FrameNumber == 0, "CHAR-003 pin resolved");
        Ok(!ProductionLipsyncSpeakerTarget.TryResolve("CHAR-001", out _), "CHAR-001 is not a silent Fal auto target");
        Ok(!ProductionLipsyncSpeakerTarget.TryResolve("", out _), "empty speaker unresolved");

        var blocked = Harness();
        blocked.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        await SeedParentsAsync(blocked, sh01, Guid.NewGuid(), Guid.NewGuid());
        Ok(await Rejects(() => blocked.Svc.CreateLipsyncAsync(LipsyncCommand(blocked, sh01)),
            ProductionFailureReasons.BindingChanged), "SH01 CURRENT mismatch stops before Fal");
        Ok(blocked.FalTransport.StartCount == 0, "mismatch does not Start Fal");

        var noPermit = Harness();
        noPermit.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var sh02 = new ProductionScope(sh01.BuildId, "EP01", "SC01", "EP01-SC01-SH02");
        var sh99 = new ProductionScope(sh01.BuildId, "EP01", "SC01", "EP01-SC01-SH99");
        await SeedParentsAsync(noPermit, sh02, Guid.NewGuid(), Guid.NewGuid());
        Ok(await Rejects(() => noPermit.Svc.CreateLipsyncAsync(LipsyncCommand(noPermit, sh02, speaker: "CHAR-003")),
            ProductionFailureReasons.MissingVideoArtifact), "official SH02 without ACCEPTED:VIDEO_TAKE fail-closed");
        Ok(noPermit.FalTransport.StartCount == 0, "locked SH02 does not Start Fal");
        await SeedParentsAsync(noPermit, sh99, Guid.NewGuid(), Guid.NewGuid());
        Ok(await Rejects(() => noPermit.Svc.CreateLipsyncAsync(LipsyncCommand(noPermit, sh99, speaker: "CHAR-003")),
            PerformanceContractIds.OfficialFilmForbidden), "official CREATE_LIPSYNC requires issued permit");
        Ok(noPermit.FalTransport.StartCount == 0, "no permit → no Fal");

        var target = Harness();
        target.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedParentsAsync(target, sh99, Guid.NewGuid(), Guid.NewGuid());
        await target.Authority.IssuePermitAsync(LipsyncPermit(sh99, "target"));
        Ok(await Rejects(() => target.Svc.CreateLipsyncAsync(LipsyncCommand(target, sh99, speaker: "CHAR-001")),
            ProductionFailureReasons.LipsyncTargetUnresolved), "multi-face CHAR-001 is LIPSYNC_TARGET_UNRESOLVED");
        Ok(target.FalTransport.StartCount == 0, "unresolved target does not spend Fal");

        var h = Harness();
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        h.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        await SeedParentsAsync(h, sh99, Guid.NewGuid(), Guid.NewGuid());
        var permit = await h.Authority.IssuePermitAsync(LipsyncPermit(sh99, "ok"));
        AssertOwnedFalPin(h, Ok);
        var ready = LipsyncCommand(h, sh99, speaker: "CHAR-003");
        Ok(ready.Credential?.CredentialId == FixtureCredentialId
            && ready.Credential.CredentialVersionId == FixtureCredentialVersionId,
            "official lipsync command carries the fixture pair");
        var first = await h.Svc.CreateLipsyncAsync(ready);
        Ok(first.Request.UserAction == ProductionActions.CreateLipsync, "Request CREATE_LIPSYNC");
        var snap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(snap is not null
            && snap.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && snap.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3
            && ProductionLipsyncFreeze.TryParse(snap.ShotContract, out _, out var frozenSpeaker, out var frozenVideo, out var frozenVoice)
            && frozenSpeaker == "CHAR-003"
            && frozenVideo == h.VideoId
            && frozenVoice == h.VoiceId, "InputSnapshot freezes Fal + CHAR-003 + exact parents");
        Ok(snap is { } frozenSnap
            && frozenSnap.CredentialId == FixtureCredentialId
            && frozenSnap.CredentialVersionId == FixtureCredentialVersionId
            && frozenSnap.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && frozenSnap.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3
            && !frozenSnap.ToString().Contains(FixtureSecret, StringComparison.Ordinal),
            "InputSnapshot freezes the fal credential pair and not the secret");
        Ok(h.FalTransport.StartCount == 1
            && first.Attempt.ProviderJobId == h.FalTransport.LastProviderJobId, "exactly one Fal Start");
        Ok(first.Artifact.Role == ProductionArtifactRoles.Lipsync
            && first.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && first.Artifact.MimeType == ProductionTestMp4.Mime
            && first.Artifact.ByteSha256.Length == 64, "LIPSYNC VERIFIED physical");
        var current = await h.Store.GetActiveBindingAsync(
            sh99.BuildId, sh99.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync));
        var visible = await h.Store.GetActiveBindingAsync(
            sh99.BuildId, sh99.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Lipsync));
        Ok(current?.ArtifactId == first.Artifact.ArtifactId, "CURRENT:LIPSYNC bound");
        Ok(visible?.ArtifactId == first.Artifact.ArtifactId, "VISIBLE:LIPSYNC bound");
        var after = await h.Authority.GetLatestPermitAsync(
            sh99.BuildId, sh99.ShotCode, ProductionActions.CreateLipsync,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3);
        Ok(after is { Status: ProductionExecutionPermitStatuses.Consumed, PermitId: var pid }
            && pid == permit.PermitId, "permit consumed once");

        var replay = await h.Svc.CreateLipsyncAsync(LipsyncCommand(h, sh99, speaker: "CHAR-003"));
        var replaySnap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        Ok(replay.Artifact.ArtifactId == first.Artifact.ArtifactId
            && replay.Attempt.AttemptId == first.Attempt.AttemptId
            && h.FalTransport.StartCount == 1, "replay does not Start Fal again");
        Ok(snap is { } firstSnap
            && replaySnap is { } kept
            && kept.SnapshotId == firstSnap.SnapshotId
            && kept.CredentialId == FixtureCredentialId
            && kept.CredentialVersionId == FixtureCredentialVersionId,
            "replay keeps the same frozen credential pair");

        Ok((await h.Store.GetActiveBindingAsync(
            sh99.BuildId, sh99.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake)))
            ?.ArtifactId == h.VideoId, "CURRENT:VIDEO_TAKE unchanged");
        Ok((await h.Store.GetActiveBindingAsync(
            sh99.BuildId, sh99.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)))
            ?.ArtifactId == h.VoiceId, "CURRENT:VOICE unchanged");
        Ok(h.VoiceTransport.SynthesizeCount == 0 && h.RunwayTransport.CreateCalls == 0, "no ElevenLabs / Runway");
        Ok((await h.Svc.GetShotStateAsync(sh01)).LipsyncArtifactId is null, "SH01 not stolen");
        Ok((await h.Svc.GetShotStateAsync(new ProductionScope(
            ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01"))).LipsyncArtifactId is null,
            "EP99 unchanged");

        var net = Harness(paidNetwork: true);
        net.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedParentsAsync(net, sh99, Guid.NewGuid(), Guid.NewGuid());
        await net.Authority.IssuePermitAsync(LipsyncPermit(sh99, "net"));
        Ok(await Rejects(() => net.Svc.CreateLipsyncAsync(LipsyncCommand(net, sh99, speaker: "CHAR-003")),
            ProductionFailureReasons.PaidNetworkForbidden), "paid official requires AuthorizePaidNetwork");
        Ok(net.FalTransport.StartCount == 0, "unauthorized paid path does not Start");
        var executed = await net.Svc.CreateLipsyncAsync(LipsyncCommand(net, sh99, speaker: "CHAR-003", authorize: true));
        Ok(executed.Artifact.Role == ProductionArtifactRoles.Lipsync
            && net.FalTransport.StartCount == 1, "authorized official Start once");
        var replayNet = await net.Svc.CreateLipsyncAsync(LipsyncCommand(net, sh99, speaker: "CHAR-003", authorize: true));
        Ok(replayNet.Artifact.ArtifactId == executed.Artifact.ArtifactId
            && net.FalTransport.StartCount == 1, "authorized replay does not Start again");

        var recover = RecoverHarness();
        recover.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedParentsAsync(recover, sh99, Guid.NewGuid(), Guid.NewGuid());
        await recover.Authority.IssuePermitAsync(LipsyncPermit(sh99, "recover"));
        Ok(await Rejects(() => recover.Svc.CreateLipsyncAsync(LipsyncCommand(recover, sh99, speaker: "CHAR-003")),
            ProductionFailureReasons.FailedIdentity), "start-then-identity-fail stays FAILED_IDENTITY");
        Ok(recover.FalTransport.StartCount == 1
            && recover.FalTransport.LastProviderJobId == StartThenIdentityFailFalTransport.KnownJobId,
            "failed download still counts as one Fal Start");
        var inflight = await recover.Store.GetRequestByIdempotencyAsync("lipsync-v1-" + sh99.ShotCode);
        var inflightTask = inflight is null ? null : (await recover.Store.ListTasksByRequestAsync(inflight.RequestId)).LastOrDefault();
        var inflightAttempt = inflightTask is null ? null : (await recover.Store.ListAttemptsAsync(inflightTask.TaskId)).LastOrDefault();
        Ok(inflightAttempt is { ProviderJobId: null or "" }, "failed Start left Attempt without providerJobId");
        var frozenRecover = inflightTask is null ? null : await recover.Store.GetSnapshotByTaskAsync(inflightTask.TaskId);
        Ok(frozenRecover is not null
            && frozenRecover.CredentialId == FixtureCredentialId
            && frozenRecover.CredentialVersionId == FixtureCredentialVersionId
            && frozenRecover.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && frozenRecover.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3,
            "failed start keeps the frozen fal credential pair");
        var recovered = await recover.Svc.RecoverOfficialExistingLipsyncAsync(
            inflightAttempt!.AttemptId, StartThenIdentityFailFalTransport.KnownJobId);
        Ok(recovered.Attempt.AttemptId == inflightAttempt.AttemptId
            && recovered.Attempt.ProviderJobId == StartThenIdentityFailFalTransport.KnownJobId
            && recover.FalTransport.StartCount == 1, "recover persists existing job and does not Start");
        Ok(recovered.Artifact.Role == ProductionArtifactRoles.Lipsync
            && recovered.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
            && recovered.Artifact.ProviderJobId == StartThenIdentityFailFalTransport.KnownJobId, "recover verifies LIPSYNC");
        Ok((await recover.Store.GetActiveBindingAsync(
            sh99.BuildId, sh99.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)))
            ?.ArtifactId == recovered.Artifact.ArtifactId, "recover binds CURRENT:LIPSYNC");
        Ok((await recover.Store.GetActiveBindingAsync(
            sh99.BuildId, sh99.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Lipsync)))
            ?.ArtifactId == recovered.Artifact.ArtifactId, "recover binds VISIBLE:LIPSYNC");
        var recoverAgain = await recover.Svc.RecoverOfficialExistingLipsyncAsync(
            inflightAttempt.AttemptId, StartThenIdentityFailFalTransport.KnownJobId);
        var recoverSnap = await recover.Store.GetSnapshotByTaskAsync(inflightTask!.TaskId);
        Ok(recoverAgain.Artifact.ArtifactId == recovered.Artifact.ArtifactId
            && recover.FalTransport.StartCount == 1, "recover replay returns existing Artifact");
        Ok(frozenRecover is { } beforeRecover
            && recoverSnap is { } afterRecover
            && afterRecover.SnapshotId == beforeRecover.SnapshotId
            && afterRecover.CredentialId == FixtureCredentialId
            && afterRecover.CredentialVersionId == FixtureCredentialVersionId,
            "recover does not replace the frozen credential version");

        var empty = Harness();
        empty.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var emptyReq = await empty.Os.CreateRequestAsync(
            sh99, ProductionActions.CreateLipsync, "regression", "empty-lipsync-no-attempt");
        var emptyTask = await empty.Os.CreateTaskAsync(emptyReq.RequestId);
        var closed = await empty.Svc.CloseEmptyOfficialLipsyncRequestAsync("empty-lipsync-no-attempt");
        Ok(closed.Status == ProductionRequestStatuses.Failed
            && (await empty.Store.GetTaskAsync(emptyTask.TaskId))!.Status == ProductionTaskStatuses.Superseded
            && empty.FalTransport.StartCount == 0, "empty-story request closes without Fal");
        var closedAgain = await empty.Svc.CloseEmptyOfficialLipsyncRequestAsync("empty-lipsync-no-attempt");
        Ok(closedAgain.RequestId == closed.RequestId
            && closedAgain.Status == ProductionRequestStatuses.Failed, "empty-story close is idempotent");
    }

    static async Task SeedParentsAsync(HarnessBundle h, ProductionScope shot, Guid videoId, Guid voiceId)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-LIPSYNC-PIXELS-" + new string('R', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh01IngressCanon.Mime, ProductionEp01Sh01IngressCanon.Story, "lipsync-v1",
            ProductionEp01Sh01IngressCanon.ActingBeat, ProductionEp01Sh01IngressCanon.VisualContract, "",
            0, ProductionEp01Sh01IngressCanon.PictureRevisionId, ProductionEp01Sh01IngressCanon.ProvenanceJson()));
        var videoBytes = ProductionTestMp4.Encode("EP01-LIPSYNC-VIDEO-" + shot.ShotCode, 5.042m);
        var voiceBytes = ProductionTestWav.Encode("EP01-LIPSYNC-VOICE-" + shot.ShotCode, 4.232m);
        var videoFile = "locked-video-" + videoId.ToString("N");
        var voiceFile = "locked-voice-" + voiceId.ToString("N");
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            videoId, art.TaskId, art.AttemptId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
            videoBytes.Length, 5.042m, KitVideoIntegrityRules.Sha256Hex(videoBytes), "os/" + videoFile, "locked-video-job",
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "locked-video-hash"));
        h.Files.Write("os", videoFile, videoBytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
            videoId, art.TaskId, DateTimeOffset.UtcNow, null));
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            voiceId, art.TaskId, art.AttemptId, ProductionArtifactRoles.Voice, ProductionTestWav.Mime,
            voiceBytes.Length, 4.232m, KitVideoIntegrityRules.Sha256Hex(voiceBytes), "os-voice/" + voiceFile, "locked-voice-job",
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "locked-voice-hash"));
        h.Files.Write("os-voice", voiceFile, voiceBytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice),
            voiceId, art.TaskId, DateTimeOffset.UtcNow, null));
        h.VideoId = videoId;
        h.VoiceId = voiceId;
    }

    static IssueProductionExecutionPermitCommand LipsyncPermit(ProductionScope scope, string reason) =>
        new(scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionActions.CreateLipsync, ProductionLipsyncCatalog.FalProviderId,
            ProductionLipsyncCatalog.SyncLipsyncV3, "regression", reason);

    static ProductionActionApiCommand LipsyncCommand(
        HarnessBundle harness, ProductionScope scope, string speaker = "CHAR-003", bool authorize = false) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh01VoiceIngressCanon.LineText,
                ProductionEp01Sh01IngressCanon.ActingBeat,
                ProductionEp01Sh01IngressCanon.VisualContract,
                "", "", "5.0s", "", "",
                SpeakerCharacterId: speaker),
            "regression",
            "lipsync-v1-" + scope.ShotCode,
            AuthorizePaidNetwork: authorize,
            Credential: harness.Reference);

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static void AssertOwnedFalPin(HarnessBundle harness, Action<bool, string> Ok)
    {
        Ok(harness.Credential.TenantId == HarnessTenantId
            && harness.Credential.Status == ProductionCredentialStatuses.Active
            && harness.Credential.CredentialId == FixtureCredentialId,
            "credential belongs to the harness tenant and is active");
        Ok(harness.Version.CredentialId == harness.Credential.CredentialId
            && harness.Version.TenantId == HarnessTenantId
            && harness.Version.Status == ProductionCredentialStatuses.Active
            && harness.Version.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && harness.Version.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3
            && harness.Version.CredentialVersionId == FixtureCredentialVersionId,
            "credential version belongs to that credential, is active, and matches fal");
    }

    static FalPin CreateFalPin()
    {
        var caller = new FixtureCaller(HarnessTenantId);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("ck23b", new Dictionary<string, byte[]> { ["ck23b"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var credential = ProductionCredentialOwnership.Create(FixtureCredentialId, HarnessTenantId, "ck23b-fal-fixture");
        identities.CreateAsync(credential).GetAwaiter().GetResult();
        var version = ProductionCredentialOwnership.CreateVersion(
            FixtureCredentialVersionId, FixtureCredentialId, HarnessTenantId, 1,
            ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3);
        identities.AddVersionAsync(version).GetAwaiter().GetResult();
        secrets.PutAsync(FixtureCredentialVersionId, FixtureSecret).GetAwaiter().GetResult();
        return new FalPin(
            credential,
            version,
            new ProductionCredentialReference(FixtureCredentialId, FixtureCredentialVersionId),
            new ProductionCredentialResolver(identities, secrets, caller));
    }

    static HarnessBundle RecoverHarness() => Build(new StartThenIdentityFailFalTransport());

    static HarnessBundle Harness(bool paidNetwork = false) =>
        Build(paidNetwork ? new NetworkFakeFalLipsyncTransport() : new FakeFalLipsyncTransport());

    static HarnessBundle Build(FakeFalLipsyncTransport falTransport)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var runwayTransport = new FakeRunwayTransport();
        var voiceTransport = new FakeElevenLabsVoiceTransport();
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(runwayTransport);
        var motionResolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var pin = CreateFalPin();
        var officialVoice = new ElevenLabsVoiceExecutionAdapter(voiceTransport);
        var officialLipsync = new FalLipSyncExecutionAdapter(falTransport);
        var os = new ProductionExecutionService(
            store, fixture, scopes, snaps, selections, permits, pin.Resolver);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, motionResolver, officialPermits: permits,
            officialVoice: officialVoice, officialLipsync: officialLipsync);
        var authority = new ProductionReadinessAuthority(selections, permits, new PerformanceContractService(snaps), snaps);
        return new HarnessBundle(
            svc, os, store, files, runwayTransport, voiceTransport, falTransport, scopes, authority,
            pin.Credential, pin.Version, pin.Reference);
    }

    sealed record FalPin(
        ProductionCredential Credential,
        ProductionCredentialVersion Version,
        ProductionCredentialReference Reference,
        ProductionCredentialResolver Resolver);

    sealed class FixtureCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeRunwayTransport RunwayTransport,
        FakeElevenLabsVoiceTransport VoiceTransport,
        FakeFalLipsyncTransport FalTransport,
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionReadinessAuthority Authority,
        ProductionCredential Credential,
        ProductionCredentialVersion Version,
        ProductionCredentialReference Reference)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeRunwayTransport RunwayTransport { get; } = RunwayTransport;
        public FakeElevenLabsVoiceTransport VoiceTransport { get; } = VoiceTransport;
        public FakeFalLipsyncTransport FalTransport { get; } = FalTransport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public ProductionReadinessAuthority Authority { get; } = Authority;
        public ProductionCredential Credential { get; } = Credential;
        public ProductionCredentialVersion Version { get; } = Version;
        public ProductionCredentialReference Reference { get; } = Reference;
        public Guid VideoId { get; set; }
        public Guid VoiceId { get; set; }
    }

    sealed class StartThenIdentityFailFalTransport : FakeFalLipsyncTransport
    {
        public const string KnownJobId = "01a0c347-d35d-7c71-8cf5-12ba5141f0bc";

        public override Task<FalLipsyncTransportResult> ExecuteOnceAsync(
            FalLipsyncTransportRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            StartCount++;
            LastProviderJobId = KnownJobId;
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        }

        public override Task<FalLipsyncTransportResult> RecoverOnceAsync(
            string providerJobId, CancellationToken ct = default)
        {
            _ = ct;
            if (!string.Equals(providerJobId.Trim(), KnownJobId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.RecoveryFailed);
            var bytes = ProductionTestMp4.Encode("recover-existing-fal-" + KnownJobId, 5.042m);
            return Task.FromResult(new FalLipsyncTransportResult(KnownJobId, bytes, ProductionTestMp4.Mime));
        }
    }
}
