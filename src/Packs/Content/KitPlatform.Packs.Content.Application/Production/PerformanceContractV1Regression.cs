using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

public static class PerformanceContractV1Regression
{
    public const string SuiteId = PerformanceContractIds.SuiteId;

    static readonly Guid TestBuild = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-000000000001");
    static readonly Guid TestBuildB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-000000000002");

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
        var svc = new PerformanceContractService(new InMemoryPerformanceSnapshotStore());
        var src = SpokenSource();
        var draft = svc.CreateDraft(src);
        var again = svc.CreateDraft(src);
        Ok(PerformanceContractCanon.CanonicalJson(draft) == PerformanceContractCanon.CanonicalJson(again), "T01 projection deterministic");
        Ok(draft.ShotPerformance.Before?.Action == "turns"
            && draft.ShotPerformance.During?.Emotion == "uneasy"
            && draft.Lines[0].Delivery.Emotion == "tense"
            && draft.Lines[0].Delivery.Locked, "T01 acting + locked line projected");

        var first = await svc.FreezeAsync(draft, "regression", PerformanceContractRules.SourceOf(src, draft));
        Ok(first.PerformanceSnapshotId != Guid.Empty, "T02 snapshot id");
        Ok(first.PerformanceHash.StartsWith(PerformanceContractIds.HashPrefix, StringComparison.Ordinal)
            && first.PerformanceHash.Length == PerformanceContractIds.HashPrefix.Length + 64, "T03 PERFORMANCE_HASH_V1");

        var second = await svc.FreezeAsync(svc.CreateDraft(src), "regression");
        Ok(first.PerformanceHash == second.PerformanceHash, "T04 same draft → same hash");
        Ok(first.PerformanceSnapshotId == second.PerformanceSnapshotId, "T05 same draft → same SnapshotId");

        var mutated = src with { ActingBeat = src.ActingBeat! with { BeforeAction = "walks away" } };
        var afterMutate = svc.CreateDraft(mutated);
        Ok(afterMutate.ShotPerformance.Before?.Action == "walks away", "T06 draft can mutate");
        var reloaded = await svc.GetAsync(first.PerformanceSnapshotId);
        Ok(reloaded.PayloadJson == first.PayloadJson
            && reloaded.PerformanceHash == first.PerformanceHash
            && reloaded.PerformanceSnapshotId == first.PerformanceSnapshotId
            && reloaded.Draft.ShotPerformance.Before?.Action == "turns", "T06 freeze not mutated by later draft");

        var changedAction = await svc.FreezeAsync(svc.CreateDraft(mutated), "regression");
        Ok(changedAction.PerformanceHash != first.PerformanceHash, "T07 different action → different hash");

        var changedDelivery = src with
        {
            Lines = [src.Lines[0] with { Emotion = "hurt", Intensity = 2, Label = PerformanceEmotions.LabelOf("hurt", 2) }],
        };
        var deliverySnap = await svc.FreezeAsync(svc.CreateDraft(changedDelivery), "regression");
        Ok(deliverySnap.PerformanceHash != first.PerformanceHash, "T08 different line delivery → different hash");

        Ok(draft.ShotPerformance.During?.Emotion == "uneasy" && draft.Lines[0].Delivery.Emotion == "tense", "T09 L1 != L3 valid");
        Ok(svc.ValidateDraft(draft).Ok && svc.ValidateDraft(draft).VoiceReady, "T09 freeze-ready with divergence");

        var noL1 = SpokenSource() with { ActingBeat = new ActingBeatSource(BeforeAction: "turns", BeforeBody: "stands") };
        var noL1Draft = svc.CreateDraft(noL1);
        Ok(noL1Draft.ShotPerformance.During?.Emotion is null
            && noL1Draft.Lines[0].Delivery.Emotion == "tense", "T10 L1 does not inherit from first line");

        var unlocked = SpokenSource() with { Lines = [src.Lines[0] with { Locked = false }] };
        var unlockedDraft = svc.CreateDraft(unlocked);
        var unlockedVerdict = svc.ValidateDraft(unlockedDraft);
        Ok(unlockedVerdict.Ok && !unlockedVerdict.VoiceReady, "T11 unlocked is not voice-ready");
        var unlockedFreeze = false;
        try { await svc.FreezeAsync(unlockedDraft, "regression"); }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.SpokenLineUnlocked) { unlockedFreeze = true; }
        Ok(unlockedFreeze, "T11 unlocked spoken line cannot freeze");

        var badChar = false;
        try
        {
            svc.CreateDraft(SpokenSource() with
            {
                Lines = [src.Lines[0] with { CharacterId = "CHAR-MISSING" }],
            });
        }
        catch (InvalidOperationException)
        {
            badChar = true;
        }
        if (!badChar)
        {
            var orphan = SpokenSource() with
            {
                CharacterIds = ["CHAR-001"],
                Lines = [src.Lines[0] with { CharacterId = "CHAR-099" }],
            };
            try
            {
                var orphanDraft = svc.CreateDraft(orphan);
                await svc.FreezeAsync(orphanDraft, "regression");
            }
            catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.InvalidCharacter)
            {
                badChar = true;
            }
        }
        Ok(badChar, "T12 invalid characterId rejected");

        var dup = SpokenSource() with
        {
            CharacterIds = ["CHAR-001", "CHAR-003"],
            PrimaryCharacterIds = ["CHAR-001", "CHAR-003"],
            Lines = [src.Lines[0]],
        };
        var dupFreeze = false;
        try { await svc.FreezeAsync(svc.CreateDraft(dup), "regression"); }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.DuplicatePrimary) { dupFreeze = true; }
        Ok(dupFreeze, "T13 duplicate PRIMARY rejected");

        var crossShot = false;
        try { svc.BindToScope(first, TestBuild, "TEST-SC01-SH02"); }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossShotForbidden) { crossShot = true; }
        Ok(crossShot, "T14 cross-shot reuse rejected");

        var crossBuild = false;
        try { svc.BindToScope(first, TestBuildB, first.Draft.ShotCode); }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.CrossScopeForbidden) { crossBuild = true; }
        var official = false;
        try { await svc.FreezeAsync(svc.CreateDraft(SpokenSource() with { BuildId = ProductionScopeAnchorRules.OfficialEp01 }), "regression"); }
        catch (InvalidOperationException ex) when (ex.Message == PerformanceContractIds.OfficialFilmForbidden) { official = true; }
        Ok(crossBuild && official, "T15 cross-build / official film rejected");

        var provider = svc.ValidateDraft(draft, """{"runway":"gen4_turbo"}""");
        Ok(provider.Issues.Any(i => i.Code == PerformanceContractIds.ProviderFieldForbidden), "T16 provider field rejected");

        var voiceId = svc.ValidateDraft(draft, """{"voiceId":"eleven-xyz"}""");
        Ok(voiceId.Issues.Any(i => i.Code == PerformanceContractIds.VoiceIdForbidden), "T17 VoiceId rejected");

        var duration = svc.ValidateDraft(draft, """{"providerDurationSec":5}""");
        Ok(duration.Issues.Any(i => i.Code == PerformanceContractIds.ProviderDurationForbidden), "T18 provider duration rejected");

        Ok(PerformanceContractCanon.CanonicalJson(draft) == PerformanceContractCanon.CanonicalJson(svc.CreateDraft(src)), "T19 canon deterministic");
        Ok(!PerformanceContractCanon.CanonicalJson(draft).Contains("holdSec", StringComparison.Ordinal)
            && !PerformanceContractCanon.CanonicalJson(draft).Contains("voiceId", StringComparison.Ordinal)
            && !PerformanceContractCanon.CanonicalJson(draft).Contains("providerDurationSec", StringComparison.Ordinal), "T19 no timing leak / VoiceId / duration");

        Ok(PerformanceContractCanon.RejectLegacyHash(ProductionTestCreateVideoContext.ActingHash).Count == 1, "T20 ActingHash rejected");
        Ok(PerformanceContractCanon.RejectLegacyHash("a1b2c3d4").Count == 1
            && !PerformanceContractCanon.IsValidPerformanceHash("a1b2c3d4"), "T21 actingBeatFingerprint rejected");
        Ok(PerformanceContractCanon.IsValidPerformanceHash(first.PerformanceHash), "T21 real hash accepted");

        var reload = await svc.GetAsync(first.PerformanceSnapshotId);
        Ok(reload.PayloadJson == first.PayloadJson && reload.PerformanceHash == first.PerformanceHash, "T22 reload identical");

        var graph = src with { ActingBeat = src.ActingBeat! with { BeforeGaze = "camera" } };
        _ = svc.CreateDraft(graph);
        var afterGraph = await svc.GetAsync(first.PerformanceSnapshotId);
        Ok(afterGraph.Draft.ShotPerformance.Before?.Gaze == "window"
            && afterGraph.PerformanceHash == first.PerformanceHash
            && !afterGraph.PayloadJson.Contains("camera", StringComparison.Ordinal), "T23 snapshot survives draft mutation");

        var twoFace = SpokenSource() with
        {
            CharacterIds = ["CHAR-001", "CHAR-003"],
            PrimaryCharacterIds = ["CHAR-001"],
            SecondaryCharacterIds = ["CHAR-003"],
        };
        var twoFaceSnap = await svc.FreezeAsync(svc.CreateDraft(twoFace), "regression");
        Ok(twoFaceSnap.Draft.Characters.Count == 2
            && twoFaceSnap.Draft.Characters.Count(c => c.SpeechRole == PerformanceContractIds.Speaker) == 1
            && twoFaceSnap.Draft.Characters.Count(c => c.SpeechRole == PerformanceContractIds.Listener) == 1, "T24 multi-character one speaker valid");

        var multi = SpokenSource() with
        {
            CharacterIds = ["CHAR-001", "CHAR-003"],
            PrimaryCharacterIds = ["CHAR-001"],
            SecondaryCharacterIds = ["CHAR-003"],
            DialogueSegmentIds = ["L1", "L2"],
            Lines =
            [
                src.Lines[0],
                new LineProjectionSource("L2", "CHAR-003", "Con biết rồi.", "hurt", 2, "slow", "low", 0.4, PerformanceEmotions.LabelOf("hurt", 2), Locked: true),
            ],
        };
        var multiSnap = await svc.FreezeAsync(svc.CreateDraft(multi), "regression");
        Ok(multiSnap.Draft.Characters.Count(c => c.SpeechRole == PerformanceContractIds.Speaker) == 2, "T25 multi-speaker snapshot may exist");
        Ok(ProductionActionContractRegistry.CreateLipsync.RequiredInputs.Contains("MotionArtifactId")
            && ProductionActionContractRegistry.CreateLipsync.RequiredInputs.Contains("PerformanceSnapshot")
            && ProductionActionContractRegistry.CreateVideo.RequiredInputs.Contains("ActingBeat")
            && ProductionActionContractRegistry.CreateVideo.RequiredInputs.Contains("PerformanceSnapshot")
            && ProductionActionContractRegistry.CreateVoice.RequiredInputs.Contains("LineText")
            && ProductionActionContractRegistry.CreateVoice.RequiredInputs.Contains("PerformanceSnapshot"), "T25 Phase C VIDEO/VOICE/LIPSYNC require PerformanceSnapshot");

        Ok(!first.PayloadJson.Contains("performanceSnapshotId", StringComparison.Ordinal)
            && !first.PayloadJson.Contains("frozenAt", StringComparison.Ordinal)
            && !first.PayloadJson.Contains("createdBy", StringComparison.Ordinal), "hash payload excludes instance metadata");

        await RegressionSuites(Ok);
    }

    static async Task RegressionSuites(Action<bool, string> Ok)
    {
        var video = ProductionTestCreateVideoV1Regression.Run();
        Ok(video.Count == 0, "REGRESSION CREATE_VIDEO TEST FAIL=" + video.Count + (video.Count == 0 ? "" : ":" + string.Join(",", video)));
        var authority = ProductionCompleteActionAuthorityV1Regression.Run();
        Ok(authority.Count == 0, "REGRESSION Complete Action Authority FAIL=" + authority.Count + (authority.Count == 0 ? "" : ":" + string.Join(",", authority.Take(3))));
        var cutoverMap = ProductionActionContractRegistry.CreateImage.UiCta == "Tạo hình"
            && ProductionActionContractRegistry.CreateVideo.UiCta == "Tạo video"
            && ProductionActionContractRegistry.CreateVoice.UiCta == "Tạo giọng nói"
            && ProductionActionContractRegistry.CreateLipsync.UiCta == "Khớp môi";
        Ok(cutoverMap, "REGRESSION UI cutover CTA map unchanged");
        await Task.CompletedTask;
    }

    static PerformanceProjectionSource SpokenSource() => new(
        TestBuild,
        "TEST-SC01-SH01",
        "TEST",
        "SC01",
        ["CHAR-001"],
        ["L1"],
        [
            new LineProjectionSource("L1", "CHAR-001", "Thì sao?", "tense", 3, "fast", "mid", 0.12, PerformanceEmotions.LabelOf("tense", 3), Locked: true),
        ],
        new ActingBeatSource("turns", "stands", null, "window", "kitchen", "uneasy", 2, null, "down"));
}
