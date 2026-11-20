using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Isolated tenant proof of one shot outcome. Does not read or update the
/// unattributed official SH02 rows, and does not call a provider.
/// </summary>
public static class ShotProductionOutcomeV1Regression
{
    public const string SuiteId = ShotProductionOutcomeV1.SuiteId;
    static readonly Guid Tenant = Guid.Parse("aaaaaaaa-1111-4111-8111-0000000000a1");
    static readonly Guid Other = Guid.Parse("bbbbbbbb-2222-4222-8222-0000000000b2");
    static readonly Guid Build = Guid.Parse("cccccccc-3333-4333-8333-0000000000c3");

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
        Ok(Build != ProductionEp01Sh02IngressCanon.BuildId, "T01 isolated build is not the official EP01 build");
        var caller = new Caller(Tenant);
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        store.RegisterBuild(Build, Tenant);

        var shaped = await PlantShot(store, files, "EP01-SC01-SH02", historical: true, picture: 8m, speech: 2.534m, lipsync: 2.5m, mix: 8m);
        var unread = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "EP01-SC01-SH02", null);
        Ok(unread.ApprovedDialogueWindow == false, "T02 no dialogue window is invented");
        Ok(unread.Request?.InputGeneration == ShortProductionQualityPipelineV1.HistoricalGeneration, "T03 exact historical request stays historical");
        Ok(unread.ApprovedActing?.Contains("red score", StringComparison.Ordinal) == true, "T04 approved acting is the contract");
        Ok(unread.Request?.ActingBeat.Contains("đưa hai", StringComparison.Ordinal) == true, "T05 frozen request keeps the truncated acting");
        Ok(unread.Playback?.ArtifactId == shaped.MixId && unread.Playback.ArtifactRole == ProductionArtifactRoles.Mix, "T06 playback is the current mix");
        Ok(unread.PlaybackBytesMatch, "T07 playback bytes match the mix hash");
        Ok(unread.Stages.Any(s => s.Role == ProductionArtifactRoles.VideoTake && s.ArtifactId == shaped.VideoId), "T08 video take is selected");
        Ok(unread.Stages.First(s => s.Role == ProductionArtifactRoles.Lipsync).ParentArtifactIds.Contains(shaped.VideoId)
            && unread.Stages.First(s => s.Role == ProductionArtifactRoles.Lipsync).ParentArtifactIds.Contains(shaped.VoiceId), "T09 lipsync parents are the take and the voice");
        Ok(unread.Stages.First(s => s.Role == ProductionArtifactRoles.Mix).ParentArtifactIds.Contains(shaped.LipsyncId), "T10 mix parent is the lipsync");
        Ok(unread.PictureDurationSec == 8m && unread.SpeechDurationSec == 2.534m && unread.LipsyncDurationSec == 2.5m && unread.MixDurationSec == 8m, "T11 four clocks stay separate");
        Ok(unread.ClockBlockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            && unread.ClockBlockers.Contains(ShortProductionQualityPipelineV1.MixClockSplit), "T12 short speech on an 8s picture fails");
        Ok(!unread.ProductionReady && !unread.AutomatedVisualProof, "T13 the shaped shot is not accepted and pixels were not judged");
        Ok(unread.Checks.First(c => c.Code == "MOUTH_AND_SYNC").Status == ShotProductionOutcomeV1.CheckHuman, "T14 mouth sync stays a human check");
        Ok(unread.Checks.First(c => c.Code == "TECHNICAL_FILE").Status == ShotProductionOutcomeV1.CheckPass, "T15 technical validity is separate from the visual decision");

        var stale = new ShotHumanReview(
            ShortProductionAcceptanceV1.DecisionAccept,
            shaped.MixId.ToString("D"),
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            [ShortProductionQualityPipelineV1.AttestAction, ShortProductionQualityPipelineV1.AttestContinuity, ShortProductionQualityPipelineV1.AttestMotion, ShortProductionQualityPipelineV1.AttestMouth],
            ["bag"],
            true,
            false);
        var rejectedHash = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "EP01-SC01-SH02", stale);
        Ok(rejectedHash.Blockers.Contains(ShortProductionQualityPipelineV1.ReviewHashMismatch) && !rejectedHash.ProductionReady, "T16 a different hash does not keep ACCEPT");

        var witnessed = stale with
        {
            ContentHash = shaped.MixHash,
            ObservedProps = ["bag"],
            MouthMovesDuringSilence = true,
        };
        var stillBad = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "EP01-SC01-SH02", witnessed);
        Ok(stillBad.Blockers.Contains(ShortProductionQualityPipelineV1.MouthDuringIntendedSilence)
            && stillBad.Blockers.Any(b => b.StartsWith(ShortProductionQualityPipelineV1.RequiredPropMissing, StringComparison.Ordinal))
            && !stillBad.ProductionReady, "T17 a matching review still fails the clocks, the mouth, and the missing prop");

        files.PlantExternalBytes(shaped.MixKey, Encoding.UTF8.GetBytes("TAMPER-" + new string('Z', 48)));
        var tampered = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "EP01-SC01-SH02", null);
        Ok(!tampered.PlaybackBytesMatch && tampered.Blockers.Contains(ShotProductionOutcomeV1.PlaybackBytesMismatch) && !tampered.ProductionReady, "T18 bytes that do not match the hash are not the artifact");

        caller.Id = Other;
        var hidden = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "EP01-SC01-SH02", null);
        Ok(hidden.Playback is null && hidden.Stages.Count == 0 && !hidden.ProductionReady, "T19 another tenant does not receive the shot");
        Ok(files.Read(shaped.MixKey) is null, "T20 another tenant does not read the storage key");
        caller.Id = Tenant;

        var capable = await PlantShot(store, files, "ISO-SC01-SH01", historical: false, picture: 4.2m, speech: 4.0m, lipsync: 4.2m, mix: 4.2m);
        var accepted = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "ISO-SC01-SH01", new ShotHumanReview(
            ShortProductionAcceptanceV1.DecisionAccept,
            capable.MixId.ToString("D"),
            capable.MixHash,
            [ShortProductionQualityPipelineV1.AttestAction, ShortProductionQualityPipelineV1.AttestContinuity, ShortProductionQualityPipelineV1.AttestMotion, ShortProductionQualityPipelineV1.AttestMouth],
            null,
            false,
            true));
        Ok(accepted.PlaybackBytesMatch && accepted.ClockBlockers.Count == 0 && accepted.ProductionReady, "T21 a tenant shot with matching clocks and a bound review can be ready");
        Ok(!accepted.AutomatedVisualProof, "T22 readiness does not claim the pixels were measured");
        Ok(accepted.Checks.First(c => c.Code == "MOTION").Status == ShotProductionOutcomeV1.CheckPass
            && accepted.Checks.First(c => c.Code == "MOTION").Detail.Contains("Not measured from pixels", StringComparison.Ordinal), "T23 motion pass is an attestation");
        Ok(!ShortProductionQualityPipelineV1.ObserveEp01Episode().ProductionReady, "T24 the isolated shot does not accept EP01");

        var gate = ShotProductionOutcomeV1.ControlledSh02Attempt(permitIssued: false, currentFileReviewed: false);
        Ok(!gate.Ready, "T25 SH02 generation stays closed");
        Ok(gate.ProposedActing.Contains("red score", StringComparison.Ordinal) && !gate.ProposedActing.Contains("đưa hai", StringComparison.Ordinal), "T26 the proposed acting is the approved contract");
        Ok(gate.ProposedSpokenLine == "Dạ… nay con làm bài không tốt." && !gate.ProposedVideoPrompt.Contains("Dạ", StringComparison.Ordinal), "T27 the spoken line stays off the video prompt");
        Ok(gate.ProviderId == "google" && gate.ModelId == "veo-3.1" && gate.EstimatedAmountUsd == 0.20m, "T28 veo-3.1 video-only quote is 0.20 USD");
        Ok(gate.Blockers.Contains(ShotProductionOutcomeV1.NoApprovedDialogueWindow)
            && gate.Blockers.Contains(ShotProductionOutcomeV1.PermitNotIssued)
            && gate.Blockers.Any(b => b.StartsWith("VEO_4:", StringComparison.Ordinal))
            && gate.Blockers.Any(b => b.StartsWith("VEO_6:", StringComparison.Ordinal))
            && gate.Blockers.Any(b => b.StartsWith("VEO_8:", StringComparison.Ordinal)), "T29 Veo 4, 6, and 8 fail the undeclared speech clock");

        var shapedDecision = ShotProductionOutcomeV1.Readiness(unread, gate);
        Ok(shapedDecision.ContractPresent && shapedDecision.FrozenRequestPresent && shapedDecision.CurrentArtifactsPresent, "T30 the shaped shot connects the contract, the frozen request, and the current artifacts");
        Ok(shapedDecision.PlaybackHashMatch && !shapedDecision.ClocksPass && !shapedDecision.TechnicalValid, "T31 a matching hash is not technical validity");
        Ok(shapedDecision.HumanReviewStatus == ShotProductionOutcomeV1.ReviewNotRecorded && !shapedDecision.VisualAcceptance && !shapedDecision.ControlledAttemptReady, "T32 no review is not visual acceptance and does not open a provider attempt");
        Ok(shapedDecision.PlaybackArtifactId == shaped.MixId, "T33 the readiness names the current mix");

        var recorded = ShotProductionOutcomeV1.Readiness(stillBad, gate);
        Ok(recorded.HumanReviewStatus == ShotProductionOutcomeV1.ReviewRecorded && !recorded.VisualAcceptance && !recorded.TechnicalValid, "T34 a review record is not visual acceptance and does not clear a failed clock");

        var otherFile = ShotProductionOutcomeV1.Readiness(rejectedHash, gate);
        Ok(otherFile.HumanReviewStatus == ShotProductionOutcomeV1.ReviewOtherFile && !otherFile.VisualAcceptance, "T35 a different hash is not acceptance of this file");

        var capableDecision = ShotProductionOutcomeV1.Readiness(accepted, gate);
        Ok(capableDecision.TechnicalValid && capableDecision.HumanReviewStatus == ShotProductionOutcomeV1.ReviewRecorded && !capableDecision.VisualAcceptance && !capableDecision.ControlledAttemptReady, "T36 technical validity does not accept the pixels or open SH02");

        var video = ShotProductionOutcomeV1.QuoteLocal("google", "veo-3.1");
        var voice = ShotProductionOutcomeV1.QuoteLocal("elevenlabs", "eleven_v3");
        var lip = ShotProductionOutcomeV1.QuoteLocal("fal", "sync-lipsync-v3", 2.5);
        Ok(video.CostKind == "Known" && video.Amount == 0.20m && video.Currency == "USD", "T37 veo-3.1 video-only quote stays 0.20 USD");
        Ok(voice.CostKind == "Unknown" && voice.Amount is null, "T38 elevenlabs eleven_v3 has no local amount");
        Ok(lip.CostKind == "Estimate" && lip.Amount == 0.67m && lip.Basis == FamixaProviderCostCatalog.BasisFalUsdPerMin, "T39 fal v3 is a local estimate, billed from a 5 second minimum");
        Ok(ProductionDirectorTiming.ApprovedSpeech(new DirectorTimingDecision(
                5m, ProductionDirectorTiming.Source, ProductionDirectorTiming.DecisionType, ProductionDirectorTiming.Stage)) is null
            && ProductionDirectorTiming.ApprovedSpeech(new DirectorTimingDecision(
                10m, ProductionDirectorTiming.Source, ProductionDirectorTiming.DecisionType, ProductionDirectorTiming.Stage)) is null
            && ProductionDirectorTiming.ApprovedSpeech(new DirectorTimingDecision(
                8m, "DEFAULT_TAKE", "COMPUTED", "VIDEO", 0.2m, 2.7m)) is null,
            "T40 picture duration and a computed lead-in do not open a dialogue window");
        Ok(!gate.Ready && !shapedDecision.VisualAcceptance, "T41 this readiness record still does not accept the shot");

        var window = new DirectorSpeechInterval(2m, 4.5m);
        var placed = await PlantShot(
            store, files, "ISO-SC01-SH08", historical: false,
            picture: 8m, speech: 2.5m, lipsync: 2.5m, mix: 8m, speechWindow: window);
        var director = new PerformanceSourceRef(
            true, [], false,
            ProductionDurationSec: 8m,
            TimingSource: ProductionDirectorTiming.Source,
            DecisionType: ProductionDirectorTiming.DecisionType,
            SpeechStartSec: window.StartSec,
            SpeechEndSec: window.EndSec);
        var placedRead = await ShotProductionOutcomeV1.ReadAsync(
            store, files, Build, "ISO-SC01-SH08", null, directorTiming: director);
        Ok(placedRead.ApprovedDialogueWindow && placedRead.Playback?.ArtifactId == placed.MixId, "T42 a director interval is the dialogue window");
        Ok(!placedRead.ClockBlockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            && !placedRead.ClockBlockers.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch)
            && !placedRead.ClockBlockers.Contains(ShortProductionQualityPipelineV1.MixClockSplit),
            "T43 the frozen mix span that covers the director interval clears the clock");
        Ok(!placedRead.ProductionReady && !placedRead.AutomatedVisualProof, "T44 a cleared clock is not visual acceptance");

        var undeclaredRead = await ShotProductionOutcomeV1.ReadAsync(store, files, Build, "ISO-SC01-SH08", null);
        Ok(!undeclaredRead.ApprovedDialogueWindow
            && undeclaredRead.ClockBlockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback),
            "T45 mix placement does not invent a dialogue window");

        var computed = new PerformanceSourceRef(
            true, [], false,
            ProductionDurationSec: 8m,
            TimingSource: "DEFAULT_TAKE",
            DecisionType: "COMPUTED",
            SpeechStartSec: 0.2m,
            SpeechEndSec: 2.7m);
        var computedRead = await ShotProductionOutcomeV1.ReadAsync(
            store, files, Build, "ISO-SC01-SH08", null, directorTiming: computed);
        Ok(!computedRead.ApprovedDialogueWindow
            && computedRead.ClockBlockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback),
            "T46 a computed lead-in is not a director interval");

        await PlantShot(
            store, files, "ISO-SC01-SH09", historical: false,
            picture: 8m, speech: 2.5m, lipsync: 2.5m, mix: 8m);
        var missingSpan = await ShotProductionOutcomeV1.ReadAsync(
            store, files, Build, "ISO-SC01-SH09", null, directorTiming: director);
        Ok(missingSpan.ApprovedDialogueWindow
            && missingSpan.ClockBlockers.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch)
            && !missingSpan.ClockBlockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback),
            "T47 a director window without mix spans stays blocked");
    }

    static async Task<Planted> PlantShot(
        InMemoryProductionWorkStore store,
        InMemoryProductionArtifactFileStore files,
        string shot,
        bool historical,
        decimal picture,
        decimal speech,
        decimal lipsync,
        decimal mix,
        DirectorSpeechInterval? speechWindow = null)
    {
        var scope = new ProductionScope(Build, "EP01", "SC01", shot);
        var story = historical ? ProductionEp01Sh02IngressCanon.Story : "A person hands a folded paper across the table.";
        var acting = historical
            ? ProductionEp01Sh02IngressCanon.ActingBeat
            : "The person hands the folded paper across the table. Face visible.";
        var visual = historical ? ProductionEp01Sh02IngressCanon.VisualContract : "medium two-shot";
        var line = historical ? "Dạ… nay con làm bài không tốt." : "Con đưa mẹ tờ giấy.";
        var video = await Plant(store, files, scope, ProductionActions.CreateVideo, ProductionArtifactRoles.VideoTake, "google", "veo-3.1", picture, "video/mp4", story, acting, visual, null);
        var voice = await Plant(store, files, scope, ProductionActions.CreateVoice, ProductionArtifactRoles.Voice, "elevenlabs", "eleven_v3", speech, "audio/mpeg", line, line, "", null);
        var lip = await Plant(store, files, scope, ProductionActions.CreateLipsync, ProductionArtifactRoles.Lipsync, "fal", "sync-lipsync-v3", lipsync, "video/mp4", story, acting, visual, [video.ArtifactId, voice.ArtifactId]);
        string? mixToken = null;
        if (speechWindow is { } window)
        {
            var contract = ProductionMixContract.Build(video, lip, "VIDEO", "LIPSYNC", "director-speech", window);
            mixToken = ProductionMixFreeze.TokenV1(contract);
        }
        var mixed = await Plant(store, files, scope, ProductionActions.MixVideo, ProductionArtifactRoles.Mix, "local", "mix", mix, "video/mp4", story, acting, visual, [lip.ArtifactId], mixToken);
        return new Planted(video.ArtifactId, voice.ArtifactId, lip.ArtifactId, mixed.ArtifactId, mixed.ByteSha256, mixed.StorageKey);
    }

    static async Task<ProductionArtifact> Plant(
        InMemoryProductionWorkStore store,
        InMemoryProductionArtifactFileStore files,
        ProductionScope scope,
        string action,
        string role,
        string provider,
        string model,
        decimal duration,
        string mime,
        string story,
        string acting,
        string visual,
        IReadOnlyList<Guid>? parents,
        string? shotContract = null)
    {
        var now = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        await store.InsertRequestAsync(new ProductionRequest(
            requestId, scope, action, role, now, "outcome-test", ProductionRequestStatuses.Completed, null,
            action + ":" + artifactId.ToString("N"), []));
        await store.InsertTaskAsync(new ProductionTask(
            taskId, requestId, null, action, role, scope.ShotCode, scope, snapshotId, provider, model,
            ProductionTaskStatuses.Succeeded, now, now, now, null, null));
        var bytes = Encoding.UTF8.GetBytes(role + ":" + artifactId.ToString("N") + new string('Q', 40));
        var key = files.Write("os", attemptId.ToString("N"), bytes);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        await store.InsertSnapshotAsync(new ProductionTaskInputSnapshot(
            snapshotId, taskId, story, acting, visual,
            shotContract ?? "", story + "\n" + acting, "",
            "s", "a", "v", "c", "k", "vo", "m", "l", "t",
            provider, model, sha, now,
            DurationSec: duration));
        await store.InsertAttemptAsync(new ProductionAttempt(
            attemptId, taskId, 1, provider, model, null, ProductionAttemptStatuses.ProviderSucceeded, now, now, sha, sha, null));
        var art = new ProductionArtifact(
            artifactId, taskId, attemptId, role, mime, bytes.Length, duration, sha, key, null, now,
            ProductionArtifactStatuses.Verified, sha, parents);
        await store.InsertArtifactAsync(art);
        await store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(role), artifactId, taskId, now, null));
        return art;
    }

    sealed record Planted(Guid VideoId, Guid VoiceId, Guid LipsyncId, Guid MixId, string MixHash, string MixKey);

    sealed class Caller(Guid id) : IProductionCallerTenant
    {
        public Guid Id { get; set; } = id;
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
