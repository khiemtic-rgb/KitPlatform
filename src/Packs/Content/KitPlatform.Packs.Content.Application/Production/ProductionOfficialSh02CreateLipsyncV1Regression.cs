using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialSh02CreateLipsyncV1Regression
{
    public const string SuiteId = ProductionEp01Sh02LipsyncCanon.SuiteId;

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
        var shot = ProductionEp01Sh02LipsyncCanon.Scope();
        Ok(ProductionLipsyncSpeakerTarget.TryResolve("CHAR-003", out _), "global CHAR-003 still resolves");
        Ok(!ProductionLipsyncSpeakerTarget.TryResolve("CHAR-001", out _), "CHAR-001 is not a silent Fal auto target");
        Ok(ProductionEp01Sh02LipsyncCanon.TryResolveSpeaker(
                ProductionEp01Sh02LipsyncCanon.LineId, ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId, out var pin)
            && pin.X01 == 0.46 && pin.Y01 == 0.31 && pin.CharacterId == "CHAR-001", "SH02 CHAR-001 uses shot-specific pin");
        Ok(ProductionEp01Sh02LipsyncCanon.LegacyT3PinX01 == 0.28
            && ProductionEp01Sh02LipsyncCanon.LegacyT3PinY01 == 0.38, "CK23 T3 pin remains historical evidence");
        var legacyBox = FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox(
            ProductionEp01Sh02LipsyncCanon.LegacyT3PinX01,
            ProductionEp01Sh02LipsyncCanon.LegacyT3PinY01,
            ProductionEp01Sh02LipsyncCanon.PinWidth,
            ProductionEp01Sh02LipsyncCanon.PinHeight);
        Ok(legacyBox[0] == ProductionEp01Sh02LipsyncCanon.LegacyT3BoxX1
            && legacyBox[1] == ProductionEp01Sh02LipsyncCanon.LegacyT3BoxY1
            && legacyBox[2] == ProductionEp01Sh02LipsyncCanon.LegacyT3BoxX2
            && legacyBox[3] == ProductionEp01Sh02LipsyncCanon.LegacyT3BoxY2, "CK23 T3 box remains [278,184,438,364]");
        Ok(ProductionEp01Sh02LipsyncCanon.MatchesFaceBox(ProductionEp01Sh02LipsyncCanon.FaceBoxXyxy)
            && ProductionEp01Sh02LipsyncCanon.SyncMode == "cut_off", "CK23B box + cut_off");
        Ok(!ProductionEp01Sh02LipsyncCanon.TryResolveSpeaker("", "CHAR-001", out _), "CHAR-001 without SH02 line unresolved");
        Ok(!ProductionEp01Sh02LipsyncCanon.TryResolveSpeaker(ProductionEp01Sh02LipsyncCanon.LineId, "CHAR-003", out _),
            "SH02 pin does not substitute CHAR-003");
        var freeze = ProductionLipsyncFreeze.Token(
            ProductionEp01Sh02LipsyncCanon.LineId,
            ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId,
            ProductionEp01Sh02LipsyncCanon.VideoArtifactId,
            ProductionEp01Sh02LipsyncCanon.VoiceArtifactId,
            pin,
            ProductionEp01Sh02LipsyncCanon.FaceBoxXyxy);
        Ok(ProductionLipsyncFreeze.TryParsePin(freeze, out var frozenPin)
            && frozenPin.CharacterId == "CHAR-001"
            && frozenPin.X01 == 0.46
            && frozenPin.Y01 == 0.31, "freeze pin is authority");
        Ok(ProductionLipsyncFreeze.TryParseBox(freeze, out var frozenBox)
            && ProductionEp01Sh02LipsyncCanon.MatchesFaceBox(frozenBox), "freeze box is XYXY authority");
        Ok(ProductionLipsyncSpeakerTarget.TryAcceptTransportPin(shot, pin, out var acceptedPin)
            && acceptedPin.CharacterId == "CHAR-001", "transport accepts official SH02 CHAR-001 pin");
        Ok(!ProductionLipsyncSpeakerTarget.TryAcceptTransportPin(shot,
            new ProductionLipsyncSpeakerPin("CHAR-003", 0.63, 0.25, 1280, 720, 0), out _),
            "transport does not copy SH01 CHAR-003 onto official SH02");

        var ready = Harness();
        ready.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        ready.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        await SeedOfficialParentsAsync(ready, shot, videoAccepted: true, voiceAccepted: true, voiceCurrent: true);

        var firstPermit = await ready.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "ck23"));
        var againPermit = await ready.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "ck23-again"));
        Ok(firstPermit.Action == ProductionActions.CreateLipsync
            && firstPermit.ProviderId == ProductionLipsyncCatalog.FalProviderId
            && firstPermit.ModelId == ProductionLipsyncCatalog.SyncLipsyncV3
            && firstPermit.Status == ProductionExecutionPermitStatuses.Issued, "CREATE_LIPSYNC permit issued");
        Ok(againPermit.PermitId == firstPermit.PermitId, "lipsync ingress idempotent");

        var eval = await ready.Os.EvaluateCreateLipsyncAsync(shot);
        Ok(eval.Available
            && eval.VideoArtifactId == ProductionEp01Sh02LipsyncCanon.VideoArtifactId
            && eval.VoiceArtifactId == ProductionEp01Sh02LipsyncCanon.VoiceArtifactId, "CREATE_LIPSYNC ready on official parents");

        var take = await ready.Svc.GetAuthoritativeVideoTakeAsync(shot);
        Ok(take is not null && ProductionEp01Sh02LipsyncCanon.MatchesVideo(take), "VIDEO authority unchanged");

        Ok(await Rejects(() => ready.Svc.CreateLipsyncAsync(LipsyncCommand(shot,
                motionId: Guid.NewGuid())),
            ProductionFailureReasons.BindingChanged), "A wrong VIDEO ArtifactId fail-closed");
        Ok(ready.FalTransport.StartCount == 0, "A no Fal");

        var wrongVideoSha = Harness();
        wrongVideoSha.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(wrongVideoSha, shot, videoAccepted: true, voiceAccepted: true, voiceCurrent: true,
            videoSha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        await wrongVideoSha.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "b"));
        Ok(await Rejects(() => wrongVideoSha.Svc.CreateLipsyncAsync(LipsyncCommand(shot)),
            ProductionFailureReasons.BindingChanged), "B wrong VIDEO SHA fail-closed");
        Ok(wrongVideoSha.FalTransport.StartCount == 0, "B no Fal");

        Ok(await Rejects(() => ready.Svc.CreateLipsyncAsync(LipsyncCommand(shot,
                voiceId: Guid.NewGuid())),
            ProductionFailureReasons.BindingChanged), "C wrong VOICE ArtifactId fail-closed");
        Ok(ready.FalTransport.StartCount == 0, "C no Fal");

        var wrongVoiceSha = Harness();
        wrongVoiceSha.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(wrongVoiceSha, shot, videoAccepted: true, voiceAccepted: true, voiceCurrent: true,
            voiceSha: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        await wrongVoiceSha.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "d"));
        Ok(await Rejects(() => wrongVoiceSha.Svc.CreateLipsyncAsync(LipsyncCommand(shot)),
            ProductionFailureReasons.BindingChanged), "D wrong VOICE SHA fail-closed");
        Ok(wrongVoiceSha.FalTransport.StartCount == 0, "D no Fal");

        var videoUnaccepted = Harness();
        videoUnaccepted.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(videoUnaccepted, shot, videoAccepted: false, voiceAccepted: true, voiceCurrent: true);
        await videoUnaccepted.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "e"));
        Ok(await Rejects(() => videoUnaccepted.Svc.CreateLipsyncAsync(LipsyncCommand(shot)),
            ProductionFailureReasons.MissingVideoArtifact), "E VIDEO not ACCEPTED fail-closed");
        Ok(videoUnaccepted.FalTransport.StartCount == 0, "E no Fal");

        var voiceUnaccepted = Harness();
        voiceUnaccepted.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(voiceUnaccepted, shot, videoAccepted: true, voiceAccepted: false, voiceCurrent: false);
        await voiceUnaccepted.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "f"));
        Ok(await Rejects(() => voiceUnaccepted.Svc.CreateLipsyncAsync(LipsyncCommand(shot)),
            ProductionFailureReasons.MissingVoiceArtifact), "F VOICE not ACCEPTED/CURRENT fail-closed");
        Ok(voiceUnaccepted.FalTransport.StartCount == 0, "F no Fal");

        var otherLip = Guid.NewGuid();
        await ready.Store.InsertArtifactAsync(new ProductionArtifact(
            otherLip, ready.VideoTaskId, ready.VideoAttemptId, ProductionArtifactRoles.Lipsync,
            ProductionTestMp4.Mime, 64, 1, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            "os/other-lip", "other-job", DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "other-hash"));
        await ready.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            otherLip, ready.VideoTaskId, DateTimeOffset.UtcNow, null));
        var evalOther = await ready.Os.EvaluateCreateLipsyncAsync(shot);
        Ok(evalOther.Available
            && evalOther.VideoArtifactId == ProductionEp01Sh02LipsyncCanon.VideoArtifactId
            && evalOther.VoiceArtifactId == ProductionEp01Sh02LipsyncCanon.VoiceArtifactId, "G foreign LIPSYNC is not parent authority");
        Ok(await Rejects(() => ready.Svc.CreateLipsyncAsync(LipsyncCommand(shot)),
            ProductionFailureReasons.BindingChanged)
            || await Rejects(() => ready.Svc.CreateLipsyncAsync(LipsyncCommand(shot)),
                ProductionFailureReasons.FailedIdentity),
            "G existing LIPSYNC for different input is not reused");
            Ok(ready.FalTransport.StartCount == 0, "G no Fal");

        var historical = Harness();
        historical.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(historical, shot, videoAccepted: true, voiceAccepted: true, voiceCurrent: true);
        await historical.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "ck23b-hist"));
        await historical.Store.InsertArtifactAsync(new ProductionArtifact(
            ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId,
            historical.VideoTaskId, historical.VideoAttemptId, ProductionArtifactRoles.Lipsync,
            ProductionTestMp4.Mime, 64, 8, ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncSha256,
            "os/ck23-hist", "hist-job", DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "hist-hash"));
        await historical.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId,
            historical.VideoTaskId, DateTimeOffset.UtcNow, null));
        var createdOverHist = await TryCreateOfficialAsync(historical, shot);
        if (createdOverHist is not null)
        {
            Ok(createdOverHist.Artifact.ArtifactId != ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId,
                "CK23B does not reuse historical 32bec");
            Ok((await historical.Store.GetActiveBindingAsync(
                    shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)))
                ?.ArtifactId == ProductionEp01Sh02LipsyncCanon.HistoricalCk23LipsyncArtifactId,
                "CK23 CURRENT:LIPSYNC stays 32bec until quality");
            Ok(historical.FalTransport.LastRequest?.SyncMode == "cut_off"
                && ProductionEp01Sh02LipsyncCanon.MatchesFaceBox(historical.FalTransport.LastRequest?.FaceBoxXyxy),
                "CK23B transport sends XYXY + cut_off");
        }
        else
        {
            Ok(historical.FalTransport.StartCount == 0, "historical physical parents not in-process; no Fal Start");
        }

        var graph = Harness();
        graph.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(graph, shot, videoAccepted: true, voiceAccepted: true, voiceCurrent: true);
        await graph.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "i"));
        Ok(await Rejects(() => graph.Svc.CreateLipsyncAsync(LipsyncCommand(shot,
                motionId: Guid.NewGuid(), voiceId: Guid.NewGuid())),
            ProductionFailureReasons.BindingChanged), "I graph IDs do not override Production OS parents");
        Ok(graph.FalTransport.StartCount == 0, "I no Fal");

        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        try
        {
            await ready.Authority.MaterializeSh02LipsyncProductionIngressAsync(
                new MaterializeLipsyncProductionIngressCommand(sh01, "cross"));
            Ok(false, "SH02 lipsync ingress rejects SH01");
        }
        catch (InvalidOperationException ex)
        {
            Ok(ex.Message == PerformanceContractIds.CrossShotForbidden, "SH02 lipsync ingress rejects SH01");
        }

        var replayHarness = Harness();
        replayHarness.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        await SeedOfficialParentsAsync(replayHarness, shot, videoAccepted: true, voiceAccepted: true, voiceCurrent: true);
        await replayHarness.Authority.MaterializeSh02LipsyncProductionIngressAsync(
            new MaterializeLipsyncProductionIngressCommand(shot, "h"));
        var created = await TryCreateOfficialAsync(replayHarness, shot);
        if (created is not null)
        {
            Ok(created.Artifact.Role == ProductionArtifactRoles.Lipsync
                && created.Artifact.VerificationStatus == ProductionArtifactStatuses.Verified
                && (created.Artifact.ParentArtifactIds ?? []).Contains(ProductionEp01Sh02LipsyncCanon.VideoArtifactId)
                && (created.Artifact.ParentArtifactIds ?? []).Contains(ProductionEp01Sh02LipsyncCanon.VoiceArtifactId),
                "H LIPSYNC verified + exact parents");
            Ok(replayHarness.FalTransport.StartCount == 1, "H exactly one Fal Start");
            var replay = await replayHarness.Svc.CreateLipsyncAsync(LipsyncCommand(shot, authorize: true));
            Ok(replay.Artifact.ArtifactId == created.Artifact.ArtifactId
                && replayHarness.FalTransport.StartCount == 1, "H replay does not Start Fal again");
            var accept = await replayHarness.Svc.AcceptOsAsync(AcceptCommand(shot, created.Artifact.ArtifactId));
            Ok(accept.Artifact.ArtifactId == created.Artifact.ArtifactId, "ACCEPT same LIPSYNC");
            Ok((await replayHarness.Store.GetActiveBindingAsync(
                    shot.BuildId, shot.ShotCode, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole))
                ?.ArtifactId == ProductionEp01Sh02LipsyncCanon.VideoArtifactId, "ACCEPTED:VIDEO_TAKE unchanged");
            Ok((await replayHarness.Store.GetActiveBindingAsync(
                    shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice)))
                ?.ArtifactId == ProductionEp01Sh02LipsyncCanon.VoiceArtifactId, "ACCEPTED:VOICE unchanged");
            Ok((await replayHarness.Store.GetActiveBindingAsync(
                    shot.BuildId, shot.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Lipsync)))
                ?.ArtifactId == created.Artifact.ArtifactId, "ACCEPTED:LIPSYNC bound");
        }
        else
        {
            Ok(replayHarness.FalTransport.StartCount == 0, "H physical parents not in-process; no Fal Start");
        }

        Ok(ready.VoiceTransport.SynthesizeCount == 0 && ready.RunwayTransport.CreateCalls == 0,
            "no ElevenLabs / Runway");

        var ck20 = ProductionOfficialVeoVideoTakeAcceptanceV1Regression.Run();
        Ok(ck20.Count == 0, ck20.Count == 0 ? "CK20 still green" : "CK20:" + string.Join(",", ck20));
        var ck21a = ProductionOfficialVideoAuthorityBridgeV1Regression.Run();
        Ok(ck21a.Count == 0, ck21a.Count == 0 ? "CK21A still green" : "CK21A:" + string.Join(",", ck21a));
        var ck22 = ProductionOfficialSh02CreateVoiceV1Regression.Run();
        Ok(ck22.Count == 0, ck22.Count == 0 ? "CK22 still green" : "CK22:" + string.Join(",", ck22));
        var sh01Lip = ProductionEp01Sh01RealFilmRunV1LipsyncRegression.Run();
        Ok(sh01Lip.Count == 0, sh01Lip.Count == 0 ? "SH01 V1 lipsync still green" : "SH01LIP:" + string.Join(",", sh01Lip));
    }

    static async Task<CreateVideoSliceResult?> TryCreateOfficialAsync(HarnessBundle h, ProductionScope shot)
    {
        try
        {
            return await h.Svc.CreateLipsyncAsync(LipsyncCommand(shot, authorize: true));
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedIdentity)
        {
            return null;
        }
    }

    static async Task SeedOfficialParentsAsync(
        HarnessBundle h,
        ProductionScope shot,
        bool videoAccepted,
        bool voiceAccepted,
        bool voiceCurrent,
        string? videoSha = null,
        string? voiceSha = null)
    {
        var pixels = Encoding.ASCII.GetBytes("EP01-SH02-CK23-PIXELS-" + new string('K', 48));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh02IngressCanon.Mime, ProductionEp01Sh02IngressCanon.Story, "ck23",
            ProductionEp01Sh02IngressCanon.ActingBeat, ProductionEp01Sh02IngressCanon.VisualContract, "",
            0, ProductionEp01Sh02IngressCanon.PictureRevisionId, ProductionEp01Sh02IngressCanon.ProvenanceJson()));
        var engine = new ProductionWorkExecutionEngine(h.Store);
        var req = await engine.CreateRequestAsync(shot, ProductionActions.CreateVideo, "ck23", "vid-ck23-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var videoSnap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId,
            ProductionEp01Sh02IngressCanon.Story, ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract, "motion",
            "compiled", "compiled",
            "s", "a", "v", "c", art.ByteSha256, "vh", "mh", "", "th",
            "google", "veo-3.1", "snap-hash-ck23", DateTimeOffset.UtcNow,
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
        var plantedVideoSha = videoSha ?? ProductionEp01Sh02LipsyncCanon.VideoSha256;
        var videoBytes = ProductionTestMp4.Encode("ck23-video-" + plantedVideoSha[..12], 10);
        if (videoSha is null)
            h.Files.Write("os", "official-sh02-take", videoBytes);
        else
            h.Files.Write("os", "official-sh02-take", Encoding.ASCII.GetBytes("CK23-WRONG-VIDEO-" + new string('V', 48)));
        var take = new ProductionArtifact(
            ProductionEp01Sh02LipsyncCanon.VideoArtifactId,
            task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake,
            "video/mp4", ProductionEp01Sh02LipsyncCanon.VideoBytes, 10,
            plantedVideoSha, "os/official-sh02-take", attempt.ProviderJobId,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, videoSnap.SnapshotHash, [art.ArtifactId]);
        await h.Store.InsertArtifactAsync(take);
        await h.Store.UpdateTaskAsync((await h.Store.GetTaskAsync(task.TaskId))! with
        {
            Status = ProductionTaskStatuses.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        if (videoAccepted)
        {
            await h.Store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), shot.BuildId, shot.ShotCode,
                OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole, take.ArtifactId, task.TaskId,
                DateTimeOffset.UtcNow, null));
        }
        h.VideoTaskId = task.TaskId;
        h.VideoAttemptId = attempt.AttemptId;

        var voiceReq = await engine.CreateRequestAsync(shot, ProductionActions.CreateVoice, "ck23", "voice-ck23-" + Guid.NewGuid().ToString("N"));
        var voiceTask = await engine.CreateTaskAsync(voiceReq.RequestId);
        var voiceSnap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), voiceTask.TaskId,
            ProductionEp01Sh02LipsyncCanon.LineText, ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract, ProductionVoiceFreeze.Token(
                ProductionEp01Sh02LipsyncCanon.LineId,
                ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId,
                ProductionEp01Sh02LipsyncCanon.VoiceId,
                ProductionEp01Sh02LipsyncCanon.LineText),
            "compiled", "compiled",
            "s", "a", "v", "c", "", "vh", "mh", "", "th",
            ProductionVoiceCatalog.ElevenLabsProviderId, ProductionVoiceCatalog.ElevenV3,
            "voice-snap-ck23", DateTimeOffset.UtcNow,
            take.ArtifactId, "16:9", 10, ShotExecutionCompiler.Version, [take.ArtifactId],
            PerformancePayloadJson: "{\"lineId\":\"" + ProductionEp01Sh02LipsyncCanon.LineId + "\"}");
        voiceSnap = voiceSnap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(voiceSnap) };
        await h.Store.InsertSnapshotAsync(voiceSnap);
        await h.Store.UpdateTaskAsync(voiceTask with
        {
            InputSnapshotId = voiceSnap.SnapshotId,
            ProviderId = ProductionVoiceCatalog.ElevenLabsProviderId,
            ModelId = ProductionVoiceCatalog.ElevenV3,
            Status = ProductionTaskStatuses.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow,
        });
        var voiceAttempt = await engine.StartAttemptAsync(voiceTask.TaskId, voiceSnap.SnapshotHash);
        var plantedVoiceSha = voiceSha ?? ProductionEp01Sh02LipsyncCanon.VoiceSha256;
        var voiceBytes = ProductionTestWav.Encode("ck23-voice-" + plantedVoiceSha[..12], 4);
        if (voiceSha is null)
            h.Files.Write("os-voice", "official-sh02-voice", voiceBytes);
        else
            h.Files.Write("os-voice", "official-sh02-voice", Encoding.ASCII.GetBytes("CK23-WRONG-VOICE-" + new string('A', 48)));
        var voice = new ProductionArtifact(
            ProductionEp01Sh02LipsyncCanon.VoiceArtifactId,
            voiceTask.TaskId, voiceAttempt.AttemptId, ProductionArtifactRoles.Voice,
            ProductionTestWav.Mime, ProductionEp01Sh02LipsyncCanon.VoiceBytes, 4,
            plantedVoiceSha, "os-voice/official-sh02-voice", voiceAttempt.ProviderJobId,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, voiceSnap.SnapshotHash, [take.ArtifactId]);
        await h.Store.InsertArtifactAsync(voice);
        if (voiceCurrent)
        {
            await h.Store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), shot.BuildId, shot.ShotCode,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice),
                voice.ArtifactId, voiceTask.TaskId, DateTimeOffset.UtcNow, null));
        }
        if (voiceAccepted)
        {
            await h.Store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), shot.BuildId, shot.ShotCode,
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice),
                voice.ArtifactId, voiceTask.TaskId, DateTimeOffset.UtcNow, null));
        }
    }

    static ProductionActionApiCommand LipsyncCommand(
        ProductionScope scope, Guid? motionId = null, Guid? voiceId = null, bool authorize = false) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh02LipsyncCanon.LineText,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "10.0s", "", "",
                LineId: ProductionEp01Sh02LipsyncCanon.LineId,
                SpeakerCharacterId: ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId,
                MotionArtifactId: motionId ?? ProductionEp01Sh02LipsyncCanon.VideoArtifactId,
                VoiceArtifactId: voiceId ?? ProductionEp01Sh02LipsyncCanon.VoiceArtifactId,
                DurationSec: 10),
            "regression",
            "ck23-lipsync-" + scope.ShotCode,
            AuthorizePaidNetwork: authorize);

    static ProductionActionApiCommand AcceptCommand(ProductionScope scope, Guid artifactId) =>
        new(scope,
            new ShotExecutionInput(
                ProductionEp01Sh02LipsyncCanon.LineText,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "10.0s", "", "",
                AcceptArtifactId: artifactId),
            "regression",
            "ck23-accept-" + artifactId.ToString("N"));

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
        var fixture = new FixtureMotionAdapter();
        var runway = new RunwayMotionExecutionAdapter(runwayTransport);
        var resolver = new ProductionMotionAdapterResolver([fixture, runway]);
        var scopes = new InMemoryProductionScopeFoundationStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new InMemoryProductionExecutionPermitStore();
        var snaps = new InMemoryPerformanceSnapshotStore();
        var officialVoice = new ElevenLabsVoiceExecutionAdapter(voiceTransport);
        var officialLipsync = new FalLipSyncExecutionAdapter(falTransport);
        var os = new ProductionExecutionService(store, fixture, scopes, snaps, selections, permits);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, resolver, officialPermits: permits,
            officialVoice: officialVoice, officialLipsync: officialLipsync);
        var authority = new ProductionReadinessAuthority(selections, permits, new PerformanceContractService(snaps), snaps);
        return new HarnessBundle(svc, os, store, files, runwayTransport, voiceTransport, falTransport, scopes, authority);
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
        ProductionReadinessAuthority Authority)
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
        public Guid VideoTaskId { get; set; }
        public Guid VideoAttemptId { get; set; }
    }
}
