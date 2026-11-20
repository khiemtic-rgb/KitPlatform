using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// One shot outcome read from the existing Production OS store.
/// Technical file checks and a human review stay separate. This reader does not
/// generate media, issue a permit, or invent a dialogue window from durations.
/// </summary>
public static class ShotProductionOutcomeV1
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OUTCOME_V1";
    public const string PlaybackBytesMismatch = "PLAYBACK_BYTES_MISMATCH";
    public const string PlaybackMissing = "PLAYBACK_MISSING";
    public const string PlaybackFailClosed = "PLAYBACK_FAIL_CLOSED";
    public const string CurrentFileNotReviewed = "CURRENT_FILE_NOT_REVIEWED";
    public const string PermitNotIssued = "PERMIT_NOT_ISSUED";
    public const string NoApprovedDialogueWindow = "NO_APPROVED_DIALOGUE_WINDOW";
    public const string CheckPass = "PASS";
    public const string CheckFail = "FAIL";
    public const string CheckHuman = "HUMAN_REQUIRED";
    public const string CheckNa = "NOT_APPLICABLE";
    public const string KindAutomated = "AUTOMATED";
    public const string KindHuman = "HUMAN";
    public const string ReviewNotRecorded = "NOT_RECORDED";
    public const string ReviewOtherFile = "OTHER_FILE";
    public const string ReviewRecorded = "RECORDED";

    public static async Task<ShotProductionOutcome> ReadAsync(
        IProductionWorkStore store,
        IProductionArtifactFileStore files,
        Guid buildId,
        string shotCode,
        ShotHumanReview? review,
        CancellationToken ct = default,
        PerformanceSourceRef? directorTiming = null)
    {
        var shot = (shotCode ?? "").Trim();
        var contract = ShortProductionQualityPipelineV1.Ep01Contract(shot);
        var tasks = await store.ListTasksByScopeAsync(buildId, shot, ct);
        var videoTask = tasks.FirstOrDefault(t => t.Action == ProductionActions.CreateVideo);
        var voiceTask = tasks.FirstOrDefault(t => t.Action == ProductionActions.CreateVoice);
        var videoSnap = videoTask is null ? null : await store.GetSnapshotByTaskAsync(videoTask.TaskId, ct);
        var voiceSnap = voiceTask is null ? null : await store.GetSnapshotByTaskAsync(voiceTask.TaskId, ct);
        var stages = await StagesAsync(store, buildId, shot, ct);
        var playback = await OfficialVideoAuthorityBridgeV1.ResolveAsync(store, buildId, shot, ct);
        var bytesMatch = false;
        if (playback.Hit is not null && !playback.FailClosed)
        {
            var bytes = files.Read(playback.Hit.StorageKey);
            bytesMatch = bytes is not null
                && bytes.Length == playback.Hit.SizeBytes
                && string.Equals(
                    KitVideoIntegrityRules.Sha256Hex(bytes),
                    playback.Hit.ByteSha256,
                    StringComparison.OrdinalIgnoreCase);
        }

        var picture = Stage(stages, ProductionArtifactRoles.VideoTake)?.DurationSec ?? playback.Hit?.DurationSec ?? 0;
        var played = playback.Hit?.DurationSec ?? picture;
        var speech = Stage(stages, ProductionArtifactRoles.Voice)?.DurationSec ?? 0;
        var lipsync = Stage(stages, ProductionArtifactRoles.Lipsync)?.DurationSec ?? 0;
        var dialogue = DirectorDialogue(directorTiming);
        var mixTimeline = await MixTimelineAsync(store, stages, dialogue, ct);
        var spoken = voiceSnap?.Story ?? "";
        if (string.IsNullOrWhiteSpace(spoken))
            spoken = contract?.SpokenLine ?? "";
        var playTask = playback.Hit is null ? videoTask : await store.GetTaskAsync(playback.Hit.TaskId, ct);
        var bindings = await store.ListActiveBindingsAsync(buildId, shot, ct);
        var identityLocked = bindings.Any(b => b.Role.StartsWith(ProductionBindingRoles.Accepted, StringComparison.Ordinal));
        var evidence = new ShortQualityEvidence(
            shot,
            playTask?.Status,
            playback.Hit?.VerificationStatus,
            playback.Hit?.MimeType,
            playback.Hit?.DurationSec ?? 0,
            playTask?.Scope.ShotCode,
            shot,
            identityLocked,
            review?.Decision,
            review?.ArtifactId,
            playback.Hit?.ArtifactId.ToString("D"),
            videoSnap?.Story ?? contract?.Story,
            videoSnap?.ActingBeat ?? contract?.ActingBeat,
            contract?.DirectDialogue ?? speech > 0,
            speech,
            played,
            spoken,
            contract?.LineComplete,
            review?.MouthAlignedDuringSpeech,
            review?.Attestations ?? [],
            review is null ? "" : "REVIEW_RECORD",
            contract?.RequiredProps,
            review?.ObservedProps,
            lipsync,
            picture,
            review?.ContentHash,
            playback.Hit?.ByteSha256,
            review?.MouthMovesDuringSilence,
            dialogue,
            mixTimeline);
        var verdict = ShortProductionQualityPipelineV1.Classify(evidence);
        var blockers = verdict.Blockers.ToList();
        if (playback.FailClosed)
            blockers.Add(PlaybackFailClosed);
        else if (playback.Hit is null)
            blockers.Add(PlaybackMissing);
        else if (!bytesMatch)
            blockers.Add(PlaybackBytesMismatch);
        var ready = verdict.ProductionReady && playback.Hit is not null && !playback.FailClosed && bytesMatch;
        var clocks = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            evidence.DirectDialogue, picture, speech, lipsync, dialogue, review?.MouthMovesDuringSilence, review?.MouthAlignedDuringSpeech, mixTimeline));
        var next = videoSnap is null
            ? null
            : ShortProductionQualityPipelineV1.PrepareFutureVideo(shot, videoSnap.Story, videoSnap.ActingBeat, videoSnap.VisualContract);
        var prompt = videoSnap is null
            ? ""
            : ShortProductionQualityPipelineV1.VideoPromptOf(videoSnap.Story, videoSnap.ActingBeat, videoSnap.VisualContract);
        var checks = BuildChecks(evidence, verdict, playback, bytesMatch, clocks, contract, videoSnap);
        return new ShotProductionOutcome(
            shot,
            buildId,
            contract?.ActingBeat,
            contract?.SpokenLine,
            contract?.Continuity,
            contract?.RequiredProps ?? [],
            dialogue is { Count: > 0 },
            videoSnap is null ? null : new ShotRequestTrace(
                videoTask!.TaskId,
                videoSnap.SnapshotId,
                videoSnap.Story,
                videoSnap.ActingBeat,
                videoSnap.VisualContract,
                prompt,
                videoSnap.ProviderId,
                videoSnap.ModelId,
                videoSnap.DurationSec,
                videoSnap.SnapshotHash,
                next?.InputGeneration ?? ""),
            stages,
            playback.Hit,
            playback.FailClosed,
            bytesMatch,
            picture,
            speech,
            lipsync,
            Stage(stages, ProductionArtifactRoles.Mix)?.DurationSec ?? 0,
            clocks,
            verdict.Stage,
            ready,
            blockers,
            checks,
            false);
    }

    /// <summary>
    /// Readiness of a later SH02 provider attempt. Does not issue a permit or submit a job.
    /// Veo 4, 6, and 8 are the durations the desk can send. None is an approved dialogue window.
    /// </summary>
    public static Sh02ControlledAttemptGate ControlledSh02Attempt(bool permitIssued, bool currentFileReviewed)
    {
        var historicalStory = ProductionEp01Sh02IngressCanon.Story;
        var next = ShortProductionQualityPipelineV1.PrepareFuturePackage(
            ShortProductionQualityPipelineV1.PilotShot,
            historicalStory,
            "Minh đứng yên và nhìn tờ giấy trắng.",
            "",
            "");
        var prompt = ShortProductionQualityPipelineV1.VideoPromptOf(next.Story, next.ActingBeat, next.VisualContract);
        var quote = FamixaProviderCostCatalog.Quote("google", "veo-3.1");
        var blockers = new List<string>();
        if (!currentFileReviewed)
            blockers.Add(CurrentFileNotReviewed);
        if (!permitIssued)
            blockers.Add(PermitNotIssued);
        blockers.Add(NoApprovedDialogueWindow);
        foreach (var picture in new decimal[] { 4m, 6m, 8m })
        {
            var clocks = ShortProductionQualityPipelineV1.FutureClockBlockers(true, 2.534m, picture, 0);
            if (clocks.Count > 0)
                blockers.Add("VEO_" + decimal.Truncate(picture).ToString("0") + ":" + string.Join(",", clocks));
        }
        return new Sh02ControlledAttemptGate(
            false,
            next.ActingBeat,
            next.SpokenLine,
            prompt,
            quote.ProviderId,
            quote.ModelId,
            quote.EstimatedAmount,
            quote.Currency,
            blockers);
    }

    /// <summary>
    /// One readiness record. Technical validity, a review record, and visual acceptance are separate fields.
    /// This reader never sets visual acceptance. A review record is not a measurement of the pixels.
    /// </summary>
    public static ShotReadinessDecision Readiness(ShotProductionOutcome outcome, Sh02ControlledAttemptGate attempt)
    {
        var technicalFile = Status(outcome, "TECHNICAL_FILE") == CheckPass;
        var request = Status(outcome, "REQUEST_VS_CONTRACT") == CheckPass;
        var dialogue = Status(outcome, "DIALOGUE_TEXT");
        var dialogueOk = dialogue is CheckPass or CheckNa;
        var clocksOk = outcome.ClockBlockers.Count == 0;
        var hashOk = outcome.PlaybackBytesMatch && outcome.Playback is not null && !outcome.PlaybackFailClosed;
        var review = Status(outcome, "REVIEW_PROVENANCE");
        var reviewStatus = review == CheckFail
            ? ReviewOtherFile
            : review == CheckPass ? ReviewRecorded : ReviewNotRecorded;
        return new ShotReadinessDecision(
            !string.IsNullOrWhiteSpace(outcome.ApprovedActing),
            outcome.Request is not null,
            outcome.Stages.Count > 0,
            clocksOk,
            hashOk,
            reviewStatus,
            technicalFile && request && dialogueOk && clocksOk && hashOk,
            false,
            attempt.Ready,
            outcome.Playback?.ArtifactId,
            outcome.Playback?.ByteSha256);
    }

    /// <summary>Local catalog only. Does not read an account balance or call a provider.</summary>
    public static CatalogCostLine QuoteLocal(string providerId, string modelId, double? quantity = null)
    {
        var quote = FamixaProviderCostCatalog.Quote(providerId, modelId, quantity);
        return new CatalogCostLine(
            quote.ProviderId,
            quote.ModelId,
            quote.CostKind.ToString(),
            quote.EstimatedAmount,
            quote.Currency,
            quote.BillingBasis);
    }

    static string Status(ShotProductionOutcome outcome, string code) =>
        outcome.Checks.First(c => c.Code == code).Status;

    static async Task<IReadOnlyList<ShotStageArtifact>> StagesAsync(
        IProductionWorkStore store, Guid buildId, string shot, CancellationToken ct)
    {
        var rows = new List<ShotStageArtifact>();
        foreach (var role in new[]
        {
            ProductionArtifactRoles.Keyframe,
            ProductionArtifactRoles.VideoTake,
            ProductionArtifactRoles.Voice,
            ProductionArtifactRoles.Lipsync,
            ProductionArtifactRoles.Mix,
        })
        {
            var bind = await store.GetActiveBindingAsync(buildId, shot, ProductionBindingRoles.CurrentFor(role), ct);
            if (bind is null) continue;
            var art = await store.GetArtifactAsync(bind.ArtifactId, ct);
            if (art is null || art.Role != role) continue;
            var task = await store.GetTaskAsync(art.TaskId, ct);
            var attempt = await store.GetAttemptAsync(art.AttemptId, ct);
            rows.Add(new ShotStageArtifact(
                role,
                art.ArtifactId,
                art.ByteSha256,
                art.DurationSec,
                art.VerificationStatus,
                art.StorageKey,
                art.TaskId,
                attempt?.ProviderId ?? task?.ProviderId,
                attempt?.ModelId ?? task?.ModelId,
                art.ParentArtifactIds ?? []));
        }
        return rows;
    }

    static ShotStageArtifact? Stage(IReadOnlyList<ShotStageArtifact> stages, string role) =>
        stages.FirstOrDefault(s => s.Role == role);

    /// <summary>
    /// A window exists only on an explicit director decision. A picture duration and a computed lead-in are not one.
    /// </summary>
    static IReadOnlyList<DialogueInterval>? DirectorDialogue(PerformanceSourceRef? source)
    {
        var speech = ProductionDirectorTiming.ApprovedSpeech(source);
        if (speech is not { } hit) return null;
        return [new DialogueInterval(hit.StartSec, hit.EndSec)];
    }

    /// <summary>
    /// Spans come from the frozen mix contract. They are not read when no director window exists,
    /// so a placement cannot invent the window.
    /// </summary>
    static async Task<IReadOnlyList<MixSpeechSpan>?> MixTimelineAsync(
        IProductionWorkStore store,
        IReadOnlyList<ShotStageArtifact> stages,
        IReadOnlyList<DialogueInterval>? dialogue,
        CancellationToken ct)
    {
        if (dialogue is null || dialogue.Count == 0) return null;
        var mix = Stage(stages, ProductionArtifactRoles.Mix);
        if (mix is null) return [];
        var snap = await store.GetSnapshotByTaskAsync(mix.TaskId, ct);
        if (ProductionMixFreeze.TryParseV1(snap?.ShotContract, out var contract))
            return ProductionMixContract.SpeechSpans(contract);
        return [];
    }

    static IReadOnlyList<ShotQualityCheck> BuildChecks(
        ShortQualityEvidence evidence,
        ShortQualityVerdict verdict,
        OfficialVideoAuthorityResolution playback,
        bool bytesMatch,
        IReadOnlyList<string> clocks,
        ShotAuthoritativeContract? contract,
        ProductionTaskInputSnapshot? videoSnap)
    {
        var blockers = verdict.Blockers;
        var technical = verdict.Stage is ShortProductionAcceptanceV1.TechnicallyValid
            or ShortProductionAcceptanceV1.QualityReviewed
            or ShortProductionAcceptanceV1.Accepted
            or ShortProductionAcceptanceV1.Rejected;
        var requestMatches = contract is null || videoSnap is null
            || string.Equals(videoSnap.ActingBeat, contract.ActingBeat, StringComparison.Ordinal);
        var reviewed = !string.IsNullOrWhiteSpace(evidence.QualityDecision);
        return
        [
            Check(KindAutomated, "PLAYBACK_IDENTITY",
                playback.Hit is not null && !playback.FailClosed && bytesMatch ? CheckPass : CheckFail,
                "The file that plays is the selected artifact, and its bytes match the stored hash."),
            Check(KindAutomated, "TECHNICAL_FILE", technical ? CheckPass : CheckFail,
                "Task succeeded, the file is verified, duration is positive, and the artifact belongs to this shot."),
            Check(KindAutomated, "REQUEST_VS_CONTRACT",
                videoSnap is null ? CheckFail : requestMatches ? CheckPass : CheckFail,
                "The frozen acting is the approved contract. A historical snapshot stays the record of the job that already ran."),
            Check(KindAutomated, "DIALOGUE_TEXT",
                blockers.Contains(ShortProductionQualityPipelineV1.SpokenLineIncomplete)
                || blockers.Contains(ShortProductionQualityPipelineV1.StageDirectionInSpokenLine)
                    ? CheckFail
                    : evidence.DirectDialogue ? CheckPass : CheckNa,
                "The spoken line is complete and has no leading stage cue."),
            Check(KindAutomated, "CLOCKS", clocks.Count == 0 ? CheckPass : CheckFail,
                "Picture, speech, and lip-sync durations agree. No dialogue window is added when the contract does not name one."),
            Check(KindAutomated, "REVIEW_PROVENANCE",
                !reviewed ? CheckHuman
                    : blockers.Contains(ShortProductionQualityPipelineV1.ReviewHashMismatch)
                      || blockers.Contains(ShortProductionQualityPipelineV1.ReviewHashMissing)
                      || blockers.Contains(ShortProductionAcceptanceV1.ReviewArtifactMismatch)
                        ? CheckFail
                        : CheckPass,
                "A review names this artifact id and this hash. A different file drops the earlier decision."),
            Human("ACTION_PROP", evidence, ShortProductionQualityPipelineV1.AttestAction,
                blockers.Any(b => b.StartsWith(ShortProductionQualityPipelineV1.RequiredPropMissing, StringComparison.Ordinal)
                    || b == ShortProductionQualityPipelineV1.PropNotAttested),
                "Action and required props are a person's observation of the frames."),
            Human("CHARACTER_CONTINUITY", evidence, ShortProductionQualityPipelineV1.AttestContinuity, false,
                "Character identity and continuity with the neighboring shot are a person's observation."),
            Human("MOTION", evidence, ShortProductionQualityPipelineV1.AttestMotion, false,
                "Held poses and repeated or duplicated motion are a person's observation."),
            Human("MOUTH_AND_SYNC", evidence, ShortProductionQualityPipelineV1.AttestMouth,
                blockers.Contains(ShortProductionQualityPipelineV1.MouthDuringIntendedSilence)
                || blockers.Contains(ShortProductionQualityPipelineV1.MouthClockMismatch),
                "Mouth movement during speech and during intended silence is a person's observation. Duration math is not that observation."),
        ];
    }

    static ShotQualityCheck Human(string code, ShortQualityEvidence evidence, string attestation, bool failed, string detail)
    {
        if (failed) return Check(KindHuman, code, CheckFail, detail);
        if (string.IsNullOrWhiteSpace(evidence.QualityDecision))
            return Check(KindHuman, code, CheckHuman, detail);
        var have = evidence.VisualAttestations ?? [];
        if (!have.Contains(attestation))
            return Check(KindHuman, code, CheckFail, detail);
        if (code == "MOUTH_AND_SYNC" && evidence.DirectDialogue
            && (evidence.MouthMovesDuringSilence is null || evidence.MouthAligned is null))
            return Check(KindHuman, code, CheckHuman, detail);
        return Check(KindHuman, code, CheckPass, detail + " Attested on the review record. Not measured from pixels.");
    }

    static ShotQualityCheck Check(string kind, string code, string status, string detail) =>
        new(kind, code, status, detail);
}

public sealed record ShotHumanReview(
    string? Decision,
    string? ArtifactId,
    string? ContentHash,
    IReadOnlyList<string>? Attestations = null,
    IReadOnlyList<string>? ObservedProps = null,
    bool? MouthMovesDuringSilence = null,
    bool? MouthAlignedDuringSpeech = null);

public sealed record ShotRequestTrace(
    Guid TaskId,
    Guid SnapshotId,
    string Story,
    string ActingBeat,
    string VisualContract,
    string ProviderPrompt,
    string ProviderId,
    string ModelId,
    decimal DurationSec,
    string SnapshotHash,
    string InputGeneration);

public sealed record ShotStageArtifact(
    string Role,
    Guid ArtifactId,
    string ByteSha256,
    decimal DurationSec,
    string VerificationStatus,
    string StorageKey,
    Guid TaskId,
    string? ProviderId,
    string? ModelId,
    IReadOnlyList<Guid> ParentArtifactIds);

public sealed record ShotQualityCheck(string Kind, string Code, string Status, string Detail);

public sealed record ShotProductionOutcome(
    string ShotCode,
    Guid BuildId,
    string? ApprovedActing,
    string? ApprovedSpokenLine,
    string? ApprovedContinuity,
    IReadOnlyList<string> RequiredProps,
    bool ApprovedDialogueWindow,
    ShotRequestTrace? Request,
    IReadOnlyList<ShotStageArtifact> Stages,
    OfficialVideoAuthorityHit? Playback,
    bool PlaybackFailClosed,
    bool PlaybackBytesMatch,
    decimal PictureDurationSec,
    decimal SpeechDurationSec,
    decimal LipsyncDurationSec,
    decimal MixDurationSec,
    IReadOnlyList<string> ClockBlockers,
    string Stage,
    bool ProductionReady,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<ShotQualityCheck> Checks,
    bool AutomatedVisualProof);

public sealed record Sh02ControlledAttemptGate(
    bool Ready,
    string ProposedActing,
    string ProposedSpokenLine,
    string ProposedVideoPrompt,
    string? ProviderId,
    string? ModelId,
    decimal? EstimatedAmountUsd,
    string? Currency,
    IReadOnlyList<string> Blockers);

public sealed record ShotReadinessDecision(
    bool ContractPresent,
    bool FrozenRequestPresent,
    bool CurrentArtifactsPresent,
    bool ClocksPass,
    bool PlaybackHashMatch,
    string HumanReviewStatus,
    bool TechnicalValid,
    bool VisualAcceptance,
    bool ControlledAttemptReady,
    Guid? PlaybackArtifactId,
    string? PlaybackHash);

public sealed record CatalogCostLine(
    string ProviderId,
    string? ModelId,
    string CostKind,
    decimal? Amount,
    string? Currency,
    string Basis);
