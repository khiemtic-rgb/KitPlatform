namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// One quality contract for a Famixa short. Technical verification, script consistency,
/// and a human review of an exact artifact are separate decisions. A provider success,
/// a file hash, or a completed assembly never accepts the picture.
/// Historical ingress text stays the record of what was already generated.
/// </summary>
public static class ShortProductionQualityPipelineV1
{
    public const string SuiteId = "FAMIXA_SHORT_QUALITY_PIPELINE_V1";
    public const string FutureGeneration = "FUTURE_SPEC_V1";
    public const string HistoricalGeneration = "HISTORICAL_INGRESS";
    public const string NotInProductionOs = "NOT_IN_PRODUCTION_OS";
    public const string ActionSentenceIncomplete = "ACTION_SENTENCE_INCOMPLETE";
    public const string StageDirectionInSpokenLine = "STAGE_DIRECTION_IN_SPOKEN_LINE";
    public const string SpokenLineIncomplete = "SPOKEN_LINE_INCOMPLETE";
    public const string VisualReviewIncomplete = "VISUAL_REVIEW_INCOMPLETE";
    public const string MouthClockMismatch = "MOUTH_CLOCK_MISMATCH";
    public const string ShotNotAccepted = "SHOT_NOT_ACCEPTED";
    public const string AssemblyNotReviewed = "ASSEMBLY_NOT_REVIEWED";
    public const string AssemblyTechnicalIsNotAcceptance = "ASSEMBLY_TECHNICAL_IS_NOT_ACCEPTANCE";
    public const string RequiredPropMissing = "REQUIRED_PROP_MISSING";
    public const string PropNotAttested = "PROP_NOT_ATTESTED";
    public const string MixClockSplit = "MIX_CLOCK_SPLIT";
    public const string MouthDuringIntendedSilence = "MOUTH_DURING_INTENDED_SILENCE";
    public const string DialogueWindowMismatch = "DIALOGUE_WINDOW_MISMATCH";
    public const string ReviewHashMismatch = "REVIEW_HASH_MISMATCH";
    public const string ReviewHashMissing = "REVIEW_HASH_MISSING";
    public const string AssemblyProvenanceGap = "ASSEMBLY_PROVENANCE_GAP";
    public const string LegacyCandidate = "LEGACY_CANDIDATE";
    public const string LegacyIsNotAcceptance = "LEGACY_IS_NOT_ACCEPTANCE";
    public const string BindExistingMedia = "BIND_EXISTING_MEDIA";
    public const string ReviewPlayback = "REVIEW_PLAYBACK";
    public const decimal ClockToleranceSec = 0.75m;

    public const string AttestAction = "ACTION_MATCHES_BEAT";
    public const string AttestContinuity = "CONTINUITY_WITH_NEIGHBOR";
    public const string AttestMotion = "MOTION_PERFORMS_BEAT";
    public const string AttestMouth = "MOUTH_ON_SPEECH_INTERVAL";

    public const string PilotShot = "EP01-SC01-SH02";
    public const string PilotPlaybackArtifactId = "58297402-e252-45e8-8d33-c7414749a297";
    public const string BrokenTakePaperPrefix =
        "(Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.";
    public const string CompleteTakePaperAction =
        "Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con.";

    public const string Sh01FutureActing =
        "Exactly two people. No third person, no ghost, no duplicate body. Both of Linh's eyes stay visible for the whole shot. She does not turn into profile. Her mouth stays slightly open, a visible three-quarter shape toward Minh, not a closed line. Minh's head stays lowered. He never turns his back to the camera and his head does not spin or snap. Linh's face stays the large face in a medium two-shot. Minh stays in the doorway, bag strap on one shoulder, bag hanging. Audience must see Minh entering the room, head lowered, while Linh wipes the dining table and looks toward him. NEVER look into the lens. Exactly two intact people. No extra duplicates, no severe deformation.";

    public const string Sh01FutureVisual =
        """{"gaze":"Linh is close to the camera, wiping the table, and looks up toward Minh. Her mouth is a visible shape in three-quarter view. NEVER look into the lens.","lens":"35mm","focus":"Linh","camera":"Eye level","intent":"Medium two-shot. Linh's face is large. Minh stays in the doorway, head lowered, bag strap on his shoulder.","shotId":"EP01-SC01-SH01","framing":"MEDIUM","primary":{"id":"CHAR-003","name":"Linh","role":"primary"},"secondary":[{"id":"CHAR-001","name":"Minh","role":"secondary"}],"visualUniverse":"FAMIXA","visualMode":"3D_STYLIZED_REALISM"}""";

    public const string Sh02FutureActing =
        "Exactly two people. No third person, no ghost, no duplicate body. Minh's face stays toward Linh for the whole shot. He never turns his back to the camera and his head does not spin or snap. Minh's mouth is a visible shape, slightly open, in three-quarter view toward Linh. It is not a single closed line and his chin is not buried. Audience must see Minh standing, hesitate, set the bag down on the chair, slowly unzip it, take out the folded test paper, hand it to Linh with both hands, then lower his gaze. Face visible. Gaze toward Linh, not the camera. NEVER look into the lens. Never paint the line. Continuity: Minh is already in the room. Prop: bag on the chair, folded test with a red score. Keyframe: Minh is standing, the bag is already on the chair, and the red mark is already visible on the paper. Do not leave the paper blank.";

    public const string Sh03Sh04FutureActing =
        CompleteTakePaperAction + " Continuity: this is the same folded test from the previous shot. Prop: the score on the paper is red.";

    public const string Sh03FutureActing =
        "Exactly two people. No third person, no ghost, no duplicate body. Both of Linh's eyes stay visible for the whole shot. She does not turn into profile. Her mouth stays a visible shape in three-quarter view toward Minh, not a closed line. Linh's face stays the large face. She looks at Minh, not the lens. She holds the folded test with both hands. Minh stays beside her, head lowered. Exactly two intact people. The camera stays still.";

    public const string Sh03RecordedLine = "Điểm thấp thì lần sau gỡ được mà.";
    public const string Sh05FutureActing =
        "Exactly two people. No third person, no ghost, no duplicate body. Both of Minh's eyes stay visible for the whole shot. He does not turn into profile. His mouth stays slightly open. Minh's face stays visible toward Linh. He does not turn his back to the camera and his head does not spin or snap. Minh's mouth is a visible shape in three-quarter view toward Linh, not a single closed line. Audience must see Minh exhale, sit on the sofa edge, and glance at Linh. He is not already seated at the start. Face visible. NEVER look into the lens. Never paint the line. Continuity: Minh moves from standing to the sofa edge. Prop: sofa.";
    public const string Sh05SpokenLine = "Lát mẹ rảnh… chỉ lại chỗ này cho con nhé?";
    public const string Sh06FutureActing =
        "Exactly two people. No third person, no ghost, no duplicate body. Both of Linh's eyes stay visible for the whole shot. She does not turn into profile. Her mouth stays slightly open. Linh's face stays toward Minh. She does not turn her back to the camera and her head does not spin or snap. Linh's face is large. Her mouth is a visible shape in three-quarter view toward Minh, not a single closed line. Audience must see Linh smile and pat the cushion beside Minh. Face visible. NEVER look into the lens. Never paint the line. Continuity: Minh is seated from the previous shot. Prop: cushion and pen.";
    public const string Sh06SpokenLine = "Ngồi xích lại đây, lấy bút ra mẹ xem nào.";

    public const string DirectorSpeechClock = "Thoại ngắn hơn hình đang phát.";
    public const string DirectorMouthClock = "Miệng và thoại không cùng một nhịp.";
    public const string DirectorMixClock = "Khớp môi và hình gốc không cùng một đồng hồ.";
    public const string DirectorLegacy = "Tệp cũ chưa vào Production OS. Chưa được duyệt.";
    public const string DirectorActing = "Diễn đang trái với chuyện đã khóa.";
    public const string DirectorActionCut = "Câu hành động bị cắt.";
    public const string DirectorLineCut = "Câu thoại bị cắt hoặc chưa gắn.";
    public const string DirectorProp = "Đạo cụ bắt buộc không có trên hình.";
    public const string DirectorMouthSilence = "Miệng cử động trong khoảng im lặng.";
    public const string DirectorDialogueWindow = "Khoảng thoại không khớp đồng hồ đã khóa.";
    public const string DirectorReviewHash = "Duyệt không gắn đúng tệp và mã băm đang phát.";

    public static bool PilotExecuted => false;

    public static readonly string[] Ep01ShotOrder =
    [
        "EP01-SC01-SH01",
        "EP01-SC01-SH02",
        "EP01-SC01-SH03",
        "EP01-SC01-SH04",
        "EP01-SC01-SH05",
        "EP01-SC01-SH06",
    ];

    public static readonly ShotAuthoritativeContract[] Ep01ContractRows =
    [
        new("EP01-SC01-SH01", ProductionEp01Sh01IngressCanon.Story, Sh01FutureActing,
            ProductionEp01Sh01VoiceIngressCanon.LineText, true, true, "CHAR-003",
            ["bag-strap"], "Minh is the person who enters. Linh is already wiping the dining table."),
        new("EP01-SC01-SH02", ProductionEp01Sh02IngressCanon.Story, Sh02FutureActing,
            "Dạ… nay con làm bài không tốt.", true, true, "CHAR-001",
            ["red-score", "bag"], "Minh is already in the room. The bag leaves his shoulder onto the chair."),
        new("EP01-SC01-SH03", Sh03Sh04FutureActing, Sh03FutureActing,
            Sh03RecordedLine, true, true, "CHAR-003",
            ["red-score"], "The paper is the same folded test from the previous shot."),
        new("EP01-SC01-SH04", CompleteTakePaperAction, Sh03Sh04FutureActing,
            "", false, true, "",
            ["red-score"], "The paper is the same folded test from the previous shot."),
        new("EP01-SC01-SH05", Sh05FutureActing, Sh05FutureActing,
            Sh05SpokenLine, true, true, "CHAR-001",
            ["sofa"], "Minh moves from standing to the sofa edge."),
        new("EP01-SC01-SH06", Sh06FutureActing, Sh06FutureActing,
            Sh06SpokenLine, true, true, "CHAR-003",
            ["cushion", "pen"], "Minh is seated from the previous shot."),
    ];

    public static bool HistoricalIngressUnchanged() =>
        ProductionEp01Sh01IngressCanon.ActingBeat.Contains("Linh entering", StringComparison.Ordinal)
        && ProductionEp01Sh01IngressCanon.Story.Contains("Minh bước vào", StringComparison.Ordinal)
        && ProductionEp01Sh02VoiceIngressCanon.LineText.Contains("(lý nhí)", StringComparison.Ordinal)
        && ProductionEp01Sh02IngressCanon.ActingBeat.Contains("đưa hai", StringComparison.Ordinal);

    public static FutureVideoInput PrepareFutureVideo(string? shotCode, string? story, string? acting, string? visual)
    {
        var shot = (shotCode ?? "").Trim();
        var storyText = story ?? "";
        var actingText = acting ?? "";
        var visualText = visual ?? "";
        if (IsExactHistoricalSh02Replay(shot, storyText, actingText))
            return new FutureVideoInput(storyText, actingText, visualText, HistoricalGeneration);

        var contract = Ep01Contract(shot);
        if (contract is null)
            return new FutureVideoInput(storyText, actingText, visualText, "REQUEST");

        var storyOut = IsBrokenTakePaper(storyText) || string.IsNullOrWhiteSpace(storyText)
            ? contract.Story
            : storyText;
        var visualOut = shot == ProductionEp01Sh01IngressCanon.Shot ? Sh01FutureVisual : visualText;
        return new FutureVideoInput(storyOut, contract.ActingBeat, visualOut, FutureGeneration);
    }

    /// <summary>
    /// The frozen SH02 ingress triple is the record of the job that already ran.
    /// Any other text for that shot, including a stale phrase that is not the exact
    /// historical acting, is a future request and uses the approved contract.
    /// </summary>
    public static bool IsExactHistoricalSh02Replay(string? shotCode, string? story, string? acting) =>
        (shotCode ?? "").Trim() == ProductionEp01Sh02IngressCanon.Shot
        && (story ?? "") == ProductionEp01Sh02IngressCanon.Story
        && (acting ?? "") == ProductionEp01Sh02IngressCanon.ActingBeat;

    /// <summary>
    /// The next voice draft. A truncated locked line is refused. An empty request
    /// for a complete contract receives that locked line. The historical snapshot is not rewritten.
    /// </summary>
    public static VoiceDraftDecision PrepareVoiceDraft(string? shotCode, string? spoken)
    {
        var stripped = PerformanceContractRules.SpokenText(spoken);
        var contract = Ep01Contract(shotCode);
        if (contract is { DirectDialogue: true, LineComplete: false })
            return new VoiceDraftDecision(false, "", SpokenLineIncomplete);
        if (contract is not null && SpokenLineLooksTruncated(stripped))
            return new VoiceDraftDecision(false, "", SpokenLineIncomplete);
        if (string.IsNullOrWhiteSpace(stripped) && contract is { DirectDialogue: true, LineComplete: true })
            return new VoiceDraftDecision(true, contract.SpokenLine, null);
        if (contract is { DirectDialogue: true, LineComplete: true }
            && !string.Equals(stripped, contract.SpokenLine, StringComparison.Ordinal))
            return new VoiceDraftDecision(false, "", SpokenLineIncomplete);
        return new VoiceDraftDecision(true, stripped, null);
    }

    public static FutureProductionPackage PrepareFuturePackage(
        string? shotCode, string? story, string? acting, string? visual, string? spoken)
    {
        var video = PrepareFutureVideo(shotCode, story, acting, visual);
        var actingOut = FutureActingForDesk(shotCode, video.InputGeneration == HistoricalGeneration ? acting : video.ActingBeat);
        var draft = PrepareVoiceDraft(shotCode, spoken);
        var contract = Ep01Contract(shotCode);
        var generation = video.InputGeneration == HistoricalGeneration ? FutureGeneration : video.InputGeneration;
        return new FutureProductionPackage(
            video.Story,
            actingOut,
            video.VisualContract,
            draft.Permitted ? draft.SpokenLine : "",
            contract?.RequiredProps ?? [],
            contract?.Continuity ?? "",
            generation,
            !draft.Permitted && contract is { DirectDialogue: true });
    }

    public static string FutureActingForDesk(string? shotCode, string? acting)
    {
        var contract = Ep01Contract(shotCode);
        if (contract is not null) return contract.ActingBeat;
        var text = acting ?? "";
        return IsBrokenTakePaper(text) ? Sh03Sh04FutureActing : text;
    }

    public static string FutureStoryForDesk(string? shotCode, string? story)
    {
        _ = shotCode;
        var text = story ?? "";
        return IsBrokenTakePaper(text) ? CompleteTakePaperAction : text;
    }

    public static bool IsBrokenTakePaper(string? text)
    {
        var value = text ?? "";
        return value.Contains(BrokenTakePaperPrefix, StringComparison.Ordinal)
            || (value.Contains("con số đỏ chót", StringComparison.Ordinal) && value.Contains('(') && !value.Contains(')'));
    }

    public static bool IsHistoricalActing(string shotCode, string acting) =>
        (shotCode == ProductionEp01Sh01IngressCanon.Shot && acting == ProductionEp01Sh01IngressCanon.ActingBeat)
        || (shotCode == ProductionEp01Sh02IngressCanon.Shot && acting == ProductionEp01Sh02IngressCanon.ActingBeat);

    public static ShortQualityVerdict Classify(ShortQualityEvidence evidence)
    {
        var scriptBlockers = ScriptBlockers(evidence);
        if (string.IsNullOrWhiteSpace(evidence.TaskStatus))
        {
            var absent = new List<string> { NotInProductionOs };
            absent.AddRange(scriptBlockers);
            return new ShortQualityVerdict(
                evidence.ShotCode,
                NotInProductionOs,
                false,
                absent,
                [],
                evidence.PlaybackArtifactId,
                evidence.VisualObservation);
        }

        var inner = ShortProductionAcceptanceV1.Classify(evidence.ToAcceptance());
        var blockers = inner.Blockers.ToList();
        var notes = inner.Notes.ToList();
        if (evidence.ApprovedDialogue is { Count: > 0 })
            blockers.RemoveAll(b => b == ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback);
        blockers.AddRange(scriptBlockers);
        if (ReviewTargetsPlayback(evidence))
        {
            var reviewedHash = (evidence.ReviewedContentHash ?? "").Trim();
            var playbackHash = (evidence.PlaybackContentHash ?? "").Trim();
            if (reviewedHash.Length == 0 || playbackHash.Length == 0)
                blockers.Add(ReviewHashMissing);
            else if (!string.Equals(reviewedHash, playbackHash, StringComparison.OrdinalIgnoreCase))
                blockers.Add(ReviewHashMismatch);
        }

        var stage = inner.Stage;
        var ready = inner.ProductionReady;
        if (ReviewTargetsPlayback(evidence)
            && string.Equals(evidence.QualityDecision, ShortProductionAcceptanceV1.DecisionAccept, StringComparison.OrdinalIgnoreCase)
            && !VisualAttestationsComplete(evidence))
        {
            blockers.Add(VisualReviewIncomplete);
            if (stage == ShortProductionAcceptanceV1.Accepted)
            {
                stage = ShortProductionAcceptanceV1.QualityReviewed;
                ready = false;
            }
        }
        if (blockers.Count > 0 && stage == ShortProductionAcceptanceV1.Accepted)
        {
            stage = ShortProductionAcceptanceV1.QualityReviewed;
            ready = false;
        }
        return new ShortQualityVerdict(
            evidence.ShotCode, stage, ready, blockers, notes, evidence.PlaybackArtifactId, evidence.VisualObservation);
    }

    public static EpisodeQualityVerdict ClassifyEpisode(
        IReadOnlyList<string> requiredShots,
        IReadOnlyDictionary<string, ShortQualityVerdict> shots,
        string? assemblyArtifactId,
        string? reviewedAssemblyArtifactId,
        string? assemblyDecision,
        bool assemblyFileVerified)
    {
        var blockers = new List<string>();
        var notes = new List<string>();
        foreach (var shot in requiredShots)
        {
            if (!shots.TryGetValue(shot, out var verdict) || !verdict.ProductionReady)
                blockers.Add(ShotNotAccepted + ":" + shot);
        }
        var reviewMatches = !string.IsNullOrWhiteSpace(assemblyArtifactId)
            && string.Equals(assemblyArtifactId, reviewedAssemblyArtifactId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(assemblyDecision, ShortProductionAcceptanceV1.DecisionAccept, StringComparison.OrdinalIgnoreCase);
        if (!reviewMatches)
            blockers.Add(AssemblyNotReviewed);
        if (assemblyFileVerified && !reviewMatches)
            notes.Add(AssemblyTechnicalIsNotAcceptance);
        return new EpisodeQualityVerdict(blockers.Count == 0, blockers, notes, assemblyArtifactId);
    }

    public static IReadOnlyList<ShortQualityVerdict> ObserveEp01()
    {
        var rows = new[]
        {
            Sh01(),
            Sh02(),
            Legacy("EP01-SC01-SH03", "SEEN_LEGACY. Graph checklist is not an OS review. Paper is blank. Held pose."),
            Legacy("EP01-SC01-SH04", "SEEN_LEGACY. Broken action prefix. Held pose. Blank card. No OS task."),
            Legacy("EP01-SC01-SH05", "SEEN_LEGACY. Already seated. Mouth opens on lipsync. Sit-down was not seen."),
            Legacy("EP01-SC01-SH06", "NOT_FETCHED. CloudFront URL returned HTTP 401. Pixels were not judged."),
        };
        return rows.Select(Classify).ToArray();
    }

    public static EpisodeQualityVerdict ObserveEp01Episode()
    {
        var shots = ObserveEp01().ToDictionary(v => v.ShotCode, StringComparer.Ordinal);
        return ClassifyEpisode(Ep01ShotOrder, shots, null, null, null, false);
    }

    public static string? Ep01LineId(string? shotCode) => (shotCode ?? "").Trim() switch
    {
        "EP01-SC01-SH01" => "line-SC01-CHAR-003-1",
        "EP01-SC01-SH02" => "line-SC01-CHAR-001-2",
        "EP01-SC01-SH03" => "line-SC01-CHAR-003-3",
        "EP01-SC01-SH05" => "line-SC01-CHAR-001-5",
        "EP01-SC01-SH06" => "line-SC01-CHAR-003-6",
        _ => null,
    };

    public static ShotAuthoritativeContract? Ep01Contract(string? shotCode)
    {
        var shot = (shotCode ?? "").Trim();
        foreach (var contract in Ep01ContractRows)
        {
            if (contract.ShotCode == shot) return contract;
        }
        return null;
    }

    public static IReadOnlyList<string> FutureClockBlockers(
        bool directDialogue, decimal speechSec, decimal pictureSec, decimal lipsyncSec = 0) =>
        EvaluateClocks(new AudiovisualClock(directDialogue, pictureSec, speechSec, lipsyncSec, [], null, null, null));

    /// <summary>
    /// Picture length, the approved dialogue intervals, the lip-sync window, the audio
    /// length, and the mix timeline are separate clocks. A declared interval may be
    /// shorter than the picture. An undeclared interval is not invented: speech is
    /// then required to cover the picture, and a lip-sync shorter than the picture
    /// by more than the tolerance is a split clock.
    /// </summary>
    public static IReadOnlyList<string> EvaluateClocks(AudiovisualClock clock)
    {
        var blockers = new List<string>();
        if (!clock.DirectDialogue) return blockers;
        var intervals = clock.ApprovedDialogue ?? [];
        if (intervals.Count == 0)
        {
            if (ShortProductionAcceptanceV1.SpeechWindowDoesNotCoverPlayback(clock.AudioDurationSec, clock.PictureDurationSec))
                blockers.Add(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback);
            if (clock.LipsyncDurationSec > 0
                && clock.PictureDurationSec > 0
                && clock.PictureDurationSec - clock.LipsyncDurationSec > ClockToleranceSec)
                blockers.Add(MixClockSplit);
        }
        else
        {
            var span = intervals.Sum(i => i.EndSec - i.StartSec);
            if (span <= 0 || intervals.Any(i => i.EndSec <= i.StartSec || i.StartSec < 0))
                blockers.Add(DialogueWindowMismatch);
            else if (span - clock.AudioDurationSec > ClockToleranceSec)
                blockers.Add(SpokenLineIncomplete);
            else if (clock.AudioDurationSec - span > ClockToleranceSec)
                blockers.Add(DialogueWindowMismatch);
            if (clock.LipsyncDurationSec > 0 && span - clock.LipsyncDurationSec > ClockToleranceSec)
                blockers.Add(MixClockSplit);
            var mix = clock.MixTimeline;
            if (mix is null || mix.Count == 0)
                blockers.Add(DialogueWindowMismatch);
            else
            {
                foreach (var interval in intervals)
                {
                    var covered = mix.Any(s =>
                        s.CarriesSpeech
                        && s.StartSec <= interval.StartSec + ClockToleranceSec
                        && s.EndSec + ClockToleranceSec >= interval.EndSec);
                    if (!covered) blockers.Add(DialogueWindowMismatch);
                }
            }
        }
        if (clock.MouthMovesDuringSilence == true)
            blockers.Add(MouthDuringIntendedSilence);
        if (clock.MouthAlignedDuringSpeech == false)
            blockers.Add(MouthClockMismatch);
        return blockers.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static string VideoPromptOf(string? story, string? acting, string? visual) =>
        VeoMotionPrompt.FromFrozen(new ProductionTaskInputSnapshot(
            Guid.Empty, Guid.Empty,
            story ?? "", acting ?? "", visual ?? "",
            "", "", "",
            "", "", "", "", "", "", "", "", "",
            "", "", "",
            DateTimeOffset.UnixEpoch));

    public static bool ReviewBindsPlayback(
        string? reviewedArtifactId, string? reviewedHash, string? playbackArtifactId, string? playbackHash) =>
        !string.IsNullOrWhiteSpace(reviewedArtifactId)
        && !string.IsNullOrWhiteSpace(playbackArtifactId)
        && !string.IsNullOrWhiteSpace(reviewedHash)
        && !string.IsNullOrWhiteSpace(playbackHash)
        && string.Equals(reviewedArtifactId.Trim(), playbackArtifactId.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(reviewedHash.Trim(), playbackHash.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool ConcatPermittedIsEpisodeAcceptance() => false;

    public static bool FullEpisodeConcatPermitted(
        IReadOnlyList<string> shotCodes,
        IReadOnlyDictionary<string, ShortQualityVerdict>? verdicts)
    {
        if (shotCodes.Count != Ep01ShotOrder.Length || !shotCodes.SequenceEqual(Ep01ShotOrder))
            return false;
        if (verdicts is null) return false;
        foreach (var shot in Ep01ShotOrder)
        {
            if (!verdicts.TryGetValue(shot, out var verdict) || !verdict.ProductionReady)
                return false;
            if (string.IsNullOrWhiteSpace(verdict.PlaybackArtifactId))
                return false;
        }
        return true;
    }

    public static ShortQualityVerdict BindLegacyCandidate(ShortQualityEvidence media)
    {
        var verdict = Classify(media);
        var notes = verdict.Notes.ToList();
        notes.Add(LegacyIsNotAcceptance);
        var stage = verdict.Stage;
        if (stage == NotInProductionOs || stage == ShortProductionAcceptanceV1.Accepted)
            stage = LegacyCandidate;
        return new ShortQualityVerdict(
            verdict.ShotCode, stage, false, verdict.Blockers, notes, media.PlaybackArtifactId, verdict.VisualObservation);
    }

    public static IReadOnlyList<ShotWorkflowPath> Ep01Workflow()
    {
        return ObserveEp01().Select(verdict =>
        {
            var legacy = verdict.Stage == NotInProductionOs;
            return new ShotWorkflowPath(
                verdict.ShotCode,
                legacy ? LegacyCandidate : "PRODUCTION_OS",
                true,
                false,
                legacy ? BindExistingMedia : ReviewPlayback,
                verdict.Blockers,
                verdict.PlaybackArtifactId ?? LegacyPlaybackRef(verdict.ShotCode));
        }).ToArray();
    }

    public static string? LegacyPlaybackRef(string shotCode) => shotCode switch
    {
        "EP01-SC01-SH03" => "9F3XNLOMVOstXuQnDWXSK_kYgN6Z0V.mp4",
        "EP01-SC01-SH04" => "cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4",
        "EP01-SC01-SH05" => "i3okll9-JNQfyKFh6b34R_ywaRJ8bt.mp4",
        _ => null,
    };

    public static EpisodeAssemblyPlan PlanEpisode(
        IReadOnlyList<string> requiredShots,
        IReadOnlyDictionary<string, ShortQualityVerdict> shots,
        IReadOnlyList<string?> playbackArtifactIds,
        string? assemblyArtifactId,
        string? reviewedAssemblyArtifactId,
        string? assemblyDecision,
        bool assemblyFileVerified)
    {
        var blockers = new List<string>();
        var rows = new List<AssemblyProvenanceRow>();
        for (var i = 0; i < requiredShots.Count; i++)
        {
            var shot = requiredShots[i];
            var id = i < playbackArtifactIds.Count ? playbackArtifactIds[i] : null;
            var playback = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            rows.Add(new AssemblyProvenanceRow(i + 1, shot, playback));
            if (playback is null)
                blockers.Add(AssemblyProvenanceGap + ":" + shot);
            if (!shots.TryGetValue(shot, out var verdict) || !verdict.ProductionReady)
                blockers.Add(ShotNotAccepted + ":" + shot);
            else if (playback is not null
                && !string.Equals(verdict.PlaybackArtifactId, playback, StringComparison.OrdinalIgnoreCase))
                blockers.Add(ShortProductionAcceptanceV1.ReviewArtifactMismatch + ":" + shot);
        }
        if (playbackArtifactIds.Count != requiredShots.Count)
            blockers.Add(AssemblyProvenanceGap);
        var canAssemble = !blockers.Exists(b =>
            b.StartsWith(ShotNotAccepted, StringComparison.Ordinal)
            || b.StartsWith(AssemblyProvenanceGap, StringComparison.Ordinal)
            || b.StartsWith(ShortProductionAcceptanceV1.ReviewArtifactMismatch, StringComparison.Ordinal));
        var acceptance = ClassifyEpisode(
            requiredShots, shots, assemblyArtifactId, reviewedAssemblyArtifactId, assemblyDecision, assemblyFileVerified);
        foreach (var blocker in acceptance.Blockers)
        {
            if (!blockers.Contains(blocker)) blockers.Add(blocker);
        }
        var key = string.Join("|", rows.Select(r => r.Order + ":" + r.ShotCode + ":" + r.PlaybackArtifactId));
        return new EpisodeAssemblyPlan(
            canAssemble,
            canAssemble && acceptance.ProductionReady,
            rows,
            blockers,
            acceptance.Notes,
            key,
            assemblyArtifactId);
    }

    public static string DirectorText(string blocker)
    {
        var code = blocker.Split(':')[0];
        return code switch
        {
            var c when c == ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback => DirectorSpeechClock,
            var c when c == MouthClockMismatch => DirectorMouthClock,
            var c when c == MixClockSplit => DirectorMixClock,
            var c when c == NotInProductionOs || c == LegacyCandidate => DirectorLegacy,
            var c when c == ShortProductionAcceptanceV1.ActingSubjectContradictsStory => DirectorActing,
            var c when c == ActionSentenceIncomplete => DirectorActionCut,
            var c when c == SpokenLineIncomplete => DirectorLineCut,
            var c when c == RequiredPropMissing || c == PropNotAttested => DirectorProp,
            var c when c == MouthDuringIntendedSilence => DirectorMouthSilence,
            var c when c == DialogueWindowMismatch => DirectorDialogueWindow,
            var c when c == ReviewHashMismatch || c == ReviewHashMissing => DirectorReviewHash,
            _ => "",
        };
    }

    public static bool VisualAttestationsComplete(ShortQualityEvidence evidence)
    {
        var have = new HashSet<string>(evidence.VisualAttestations ?? [], StringComparer.Ordinal);
        if (!have.Contains(AttestAction) || !have.Contains(AttestContinuity) || !have.Contains(AttestMotion))
            return false;
        if (evidence.DirectDialogue && !have.Contains(AttestMouth))
            return false;
        return true;
    }

    public static bool ContainsStageDirection(string? spoken) =>
        PerformanceContractRules.SpokenText(spoken) != (spoken ?? "").Trim()
        && (spoken ?? "").Contains('(', StringComparison.Ordinal);

    static List<string> ScriptBlockers(ShortQualityEvidence evidence)
    {
        var blockers = new List<string>();
        if (IsBrokenTakePaper(evidence.Story) || IsBrokenTakePaper(evidence.ActingBeat))
            blockers.Add(ActionSentenceIncomplete);
        if (evidence.DirectDialogue && ContainsStageDirection(evidence.SpokenLineSent))
            blockers.Add(StageDirectionInSpokenLine);
        if (evidence.DirectDialogue && (evidence.SpokenLineComplete == false || string.IsNullOrWhiteSpace(evidence.SpokenLineSent)))
            blockers.Add(SpokenLineIncomplete);
        if (evidence.DirectDialogue && SpokenLineLooksTruncated(evidence.SpokenLineSent) && !blockers.Contains(SpokenLineIncomplete))
            blockers.Add(SpokenLineIncomplete);
        if (evidence.RequiredProps is { Count: > 0 })
        {
            if (evidence.ObservedProps is null)
            {
                if (ReviewTargetsPlayback(evidence))
                    blockers.Add(PropNotAttested);
            }
            else
            {
                var have = new HashSet<string>(evidence.ObservedProps, StringComparer.OrdinalIgnoreCase);
                foreach (var prop in evidence.RequiredProps)
                {
                    if (!have.Contains(prop))
                        blockers.Add(RequiredPropMissing + ":" + prop);
                }
            }
        }
        var picture = evidence.PictureDurationSec ?? evidence.PlaybackDurationSec;
        blockers.AddRange(EvaluateClocks(new AudiovisualClock(
            evidence.DirectDialogue,
            picture,
            evidence.SpeechDurationSec,
            evidence.LipsyncDurationSec ?? 0,
            evidence.ApprovedDialogue,
            evidence.MouthMovesDuringSilence,
            evidence.MouthAligned,
            evidence.MixTimeline)));
        return blockers;
    }

    public static bool SpokenLineLooksTruncated(string? line)
    {
        var text = (line ?? "").Trim();
        return text.EndsWith("...", StringComparison.Ordinal);
    }

    static bool ReviewTargetsPlayback(ShortQualityEvidence evidence) =>
        !string.IsNullOrWhiteSpace(evidence.QualityDecision)
        && !string.IsNullOrWhiteSpace(evidence.ReviewedArtifactId)
        && string.Equals(evidence.ReviewedArtifactId, evidence.PlaybackArtifactId, StringComparison.OrdinalIgnoreCase);

    static ShortQualityEvidence Sh01() => new(
        "EP01-SC01-SH01",
        ProductionTaskStatuses.Succeeded,
        ProductionArtifactStatuses.Verified,
        "video/mp4",
        5.042m,
        "EP01-SC01-SH01",
        "EP01-SC01-SH01",
        true,
        null,
        null,
        "2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2",
        ProductionEp01Sh01IngressCanon.Story,
        ProductionEp01Sh01IngressCanon.ActingBeat,
        true,
        5.042m,
        5.042m,
        ProductionEp01Sh01VoiceIngressCanon.LineText,
        true,
        false,
        [],
        "SEEN");

    static ShortQualityEvidence Sh02()
    {
        ShortQualityEvidence row = new(
        "EP01-SC01-SH02",
        ProductionTaskStatuses.Succeeded,
        ProductionArtifactStatuses.Verified,
        "video/mp4",
        8m,
        "EP01-SC01-SH02",
        "EP01-SC01-SH02",
        true,
        null,
        null,
        PilotPlaybackArtifactId,
        ProductionEp01Sh02IngressCanon.Story,
        ProductionEp01Sh02IngressCanon.ActingBeat,
        true,
        2.534m,
        8m,
        PerformanceContractRules.SpokenText(ProductionEp01Sh02VoiceIngressCanon.LineText),
        true,
        false,
        [],
        "SEEN",
        ["red-score", "bag"],
        ["bag"],
        2.5m,
        8m);
        return row with { MouthMovesDuringSilence = true };
    }

    static ShortQualityEvidence Legacy(string shot, string observation)
    {
        var paper = shot is "EP01-SC01-SH03" or "EP01-SC01-SH04";
        return new(
            shot, null, null, null, 0, shot, shot, false, null, null, null,
            shot.EndsWith("SH04", StringComparison.Ordinal) ? BrokenTakePaperPrefix : CompleteTakePaperAction,
            shot.EndsWith("SH04", StringComparison.Ordinal) ? BrokenTakePaperPrefix : "",
            shot is "EP01-SC01-SH03" or "EP01-SC01-SH05" or "EP01-SC01-SH06",
            0, 0, "", shot is not "EP01-SC01-SH03", null, [], observation,
            paper ? ["red-score"] : null,
            paper ? [] : null);
    }
}

public readonly record struct DialogueInterval(decimal StartSec, decimal EndSec);

public readonly record struct MixSpeechSpan(decimal StartSec, decimal EndSec, bool CarriesSpeech);

public sealed record AudiovisualClock(
    bool DirectDialogue,
    decimal PictureDurationSec,
    decimal AudioDurationSec,
    decimal LipsyncDurationSec,
    IReadOnlyList<DialogueInterval>? ApprovedDialogue,
    bool? MouthMovesDuringSilence,
    bool? MouthAlignedDuringSpeech,
    IReadOnlyList<MixSpeechSpan>? MixTimeline);

public sealed record VoiceDraftDecision(bool Permitted, string SpokenLine, string? Blocker);

public sealed record FutureVideoInput(string Story, string ActingBeat, string VisualContract, string InputGeneration);

public sealed record ShortQualityEvidence(
    string ShotCode,
    string? TaskStatus,
    string? VerificationStatus,
    string? MimeType,
    decimal DurationSec,
    string? ArtifactShotCode,
    string? BindingShotCode,
    bool IdentityBindingActive,
    string? QualityDecision,
    string? ReviewedArtifactId,
    string? PlaybackArtifactId,
    string? Story,
    string? ActingBeat,
    bool DirectDialogue,
    decimal SpeechDurationSec,
    decimal PlaybackDurationSec,
    string? SpokenLineSent,
    bool? SpokenLineComplete,
    bool? MouthAligned,
    IReadOnlyList<string> VisualAttestations,
    string VisualObservation,
    IReadOnlyList<string>? RequiredProps = null,
    IReadOnlyList<string>? ObservedProps = null,
    decimal? LipsyncDurationSec = null,
    decimal? PictureDurationSec = null,
    string? ReviewedContentHash = null,
    string? PlaybackContentHash = null,
    bool? MouthMovesDuringSilence = null,
    IReadOnlyList<DialogueInterval>? ApprovedDialogue = null,
    IReadOnlyList<MixSpeechSpan>? MixTimeline = null)
{
    public ShortAcceptanceEvidence ToAcceptance() => new(
        TaskStatus,
        VerificationStatus,
        MimeType,
        DurationSec,
        ArtifactShotCode,
        BindingShotCode,
        IdentityBindingActive,
        QualityDecision,
        ReviewedArtifactId,
        PlaybackArtifactId,
        Story,
        ActingBeat,
        DirectDialogue,
        SpeechDurationSec,
        PlaybackDurationSec);
}

public sealed record ShortQualityVerdict(
    string ShotCode,
    string Stage,
    bool ProductionReady,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Notes,
    string? PlaybackArtifactId,
    string VisualObservation);

public sealed record EpisodeQualityVerdict(
    bool ProductionReady,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Notes,
    string? AssemblyArtifactId);

public sealed record ShotAuthoritativeContract(
    string ShotCode,
    string Story,
    string ActingBeat,
    string SpokenLine,
    bool DirectDialogue,
    bool LineComplete,
    string SpeakerId,
    IReadOnlyList<string> RequiredProps,
    string Continuity);

public sealed record FutureProductionPackage(
    string Story,
    string ActingBeat,
    string VisualContract,
    string SpokenLine,
    IReadOnlyList<string> RequiredProps,
    string Continuity,
    string InputGeneration,
    bool SpokenLineTruncated);

public sealed record ShotWorkflowPath(
    string ShotCode,
    string PathKind,
    bool Executable,
    bool ProductionReady,
    string NextAction,
    IReadOnlyList<string> Blockers,
    string? PlaybackRef);

public sealed record AssemblyProvenanceRow(int Order, string ShotCode, string? PlaybackArtifactId);

public sealed record EpisodeAssemblyPlan(
    bool CanAssemble,
    bool ProductionReady,
    IReadOnlyList<AssemblyProvenanceRow> Provenance,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Notes,
    string ProvenanceKey,
    string? AssemblyArtifactId);
