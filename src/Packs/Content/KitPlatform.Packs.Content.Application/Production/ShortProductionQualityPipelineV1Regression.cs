namespace KitPlatform.Packs.Content.Production;

public static class ShortProductionQualityPipelineV1Regression
{
    public const string SuiteId = ShortProductionQualityPipelineV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(ShortProductionQualityPipelineV1.HistoricalIngressUnchanged(), "T01 historical ingress text stays");
        Ok(!ShortProductionQualityPipelineV1.PilotExecuted, "T02 pilot is not executed");
        Ok(!ShortProductionAcceptanceV1.ProviderSuccessIsQualityReview(), "T03 provider success is not acceptance");

        var sh01 = ShortProductionQualityPipelineV1.PrepareFutureVideo(
            ProductionEp01Sh01IngressCanon.Shot,
            ProductionEp01Sh01IngressCanon.Story,
            ProductionEp01Sh01IngressCanon.ActingBeat,
            ProductionEp01Sh01IngressCanon.VisualContract);
        Ok(sh01.InputGeneration == ShortProductionQualityPipelineV1.FutureGeneration
            && sh01.ActingBeat == ShortProductionQualityPipelineV1.Sh01FutureActing
            && sh01.Story == ProductionEp01Sh01IngressCanon.Story
            && !ShortProductionAcceptanceV1.ActingEntrantContradictsStory(sh01.Story, sh01.ActingBeat)
            && sh01.VisualContract.Contains("\"name\":\"Minh\"", StringComparison.Ordinal)
            && sh01.VisualContract.Contains("\"role\":\"primary\"", StringComparison.Ordinal),
            "T04 SH01 future request names Minh as the entrant");
        Ok(ProductionEp01Sh01IngressCanon.ActingBeat.Contains("Linh entering", StringComparison.Ordinal),
            "T05 SH01 historical acting is unchanged");

        var sh02Keep = ShortProductionQualityPipelineV1.PrepareFutureVideo(
            ProductionEp01Sh02IngressCanon.Shot,
            ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract);
        Ok(sh02Keep.ActingBeat == ProductionEp01Sh02IngressCanon.ActingBeat
            && sh02Keep.InputGeneration == ShortProductionQualityPipelineV1.HistoricalGeneration,
            "T06 server replay of SH02 keeps the historical acting");
        Ok(ShortProductionQualityPipelineV1.FutureActingForDesk(
                ProductionEp01Sh02IngressCanon.Shot, ProductionEp01Sh02IngressCanon.ActingBeat)
            == ShortProductionQualityPipelineV1.Sh02FutureActing,
            "T07 desk next SH02 request completes the truncated action");

        var broken = ShortProductionQualityPipelineV1.PrepareFutureVideo(
            "EP01-SC01-SH04",
            ShortProductionQualityPipelineV1.BrokenTakePaperPrefix,
            ShortProductionQualityPipelineV1.BrokenTakePaperPrefix,
            "");
        Ok(broken.Story == ShortProductionQualityPipelineV1.CompleteTakePaperAction
            && broken.ActingBeat == ShortProductionQualityPipelineV1.Sh03Sh04FutureActing
            && broken.ActingBeat.Contains("red", StringComparison.Ordinal)
            && !ShortProductionQualityPipelineV1.IsBrokenTakePaper(broken.Story),
            "T08 broken SH04 sentence is completed for the next request");

        var spoken = PerformanceContractRules.SpokenText(ProductionEp01Sh02VoiceIngressCanon.LineText);
        Ok(spoken == "Dạ… nay con làm bài không tốt."
            && !spoken.Contains("(lý nhí)", StringComparison.Ordinal)
            && ProductionEp01Sh02VoiceIngressCanon.LineText.Contains("(lý nhí)", StringComparison.Ordinal),
            "T09 stage direction leaves the spoken line and stays on the source line");

        var observed = ShortProductionQualityPipelineV1.ObserveEp01();
        Ok(observed.Count == 6, "T10 six EP01 shots");
        Ok(observed.All(v => !v.ProductionReady), "T11 no EP01 shot is production accepted");
        var sh01v = observed[0];
        Ok(sh01v.Stage == ShortProductionAcceptanceV1.TechnicallyValid
            && sh01v.Blockers.Contains(ShortProductionAcceptanceV1.ActingSubjectContradictsStory)
            && sh01v.Blockers.Contains(ShortProductionQualityPipelineV1.MouthClockMismatch)
            && sh01v.PlaybackArtifactId == "2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2",
            "T12 SH01 is technically valid and not visually accepted");
        var sh02v = observed[1];
        Ok(sh02v.Stage == ShortProductionAcceptanceV1.TechnicallyValid
            && sh02v.Blockers.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            && sh02v.Blockers.Contains(ShortProductionQualityPipelineV1.MouthClockMismatch)
            && sh02v.Blockers.Contains(ShortProductionQualityPipelineV1.MixClockSplit)
            && sh02v.Blockers.Contains(ShortProductionQualityPipelineV1.RequiredPropMissing + ":red-score")
            && sh02v.PlaybackArtifactId == ShortProductionQualityPipelineV1.PilotPlaybackArtifactId,
            "T13 SH02 speech, mouth, mix clock, and red-score prop block acceptance");
        Ok(observed[3].Stage == ShortProductionQualityPipelineV1.NotInProductionOs
            && observed[3].Blockers.Contains(ShortProductionQualityPipelineV1.ActionSentenceIncomplete),
            "T14 SH04 broken action is recorded without an OS task");
        Ok(observed[5].VisualObservation.StartsWith("NOT_FETCHED", StringComparison.Ordinal) && !observed[5].ProductionReady, "T15 SH06 pixels were not fetched");

        var episode = ShortProductionQualityPipelineV1.ObserveEp01Episode();
        Ok(!episode.ProductionReady
            && episode.Blockers.Contains(ShortProductionQualityPipelineV1.AssemblyNotReviewed)
            && episode.Blockers.Any(b => b.EndsWith("EP01-SC01-SH06", StringComparison.Ordinal)),
            "T16 episode is blocked");

        var accepted = AcceptedShot("GEN-SC01-SH01", speaking: true);
        var silent = AcceptedShot("GEN-SC01-SH02", speaking: false);
        var ready = ShortProductionQualityPipelineV1.ClassifyEpisode(
            ["GEN-SC01-SH01", "GEN-SC01-SH02"],
            new Dictionary<string, ShortQualityVerdict>
            {
                ["GEN-SC01-SH01"] = accepted,
                ["GEN-SC01-SH02"] = silent,
            },
            "asm-gen",
            "asm-gen",
            ShortProductionAcceptanceV1.DecisionAccept,
            true);
        Ok(ready.ProductionReady, "T17 a different script can pass the same episode gate");

        var stale = ShortProductionQualityPipelineV1.ClassifyEpisode(
            ["GEN-SC01-SH01", "GEN-SC01-SH02"],
            new Dictionary<string, ShortQualityVerdict>
            {
                ["GEN-SC01-SH01"] = accepted,
                ["GEN-SC01-SH02"] = silent,
            },
            "asm-gen-2",
            "asm-gen",
            ShortProductionAcceptanceV1.DecisionAccept,
            true);
        Ok(!stale.ProductionReady
            && stale.Notes.Contains(ShortProductionQualityPipelineV1.AssemblyTechnicalIsNotAcceptance),
            "T18 a new assembly file does not inherit the old review");

        var ticksOnly = ShortProductionQualityPipelineV1.Classify(CleanEvidence() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
            VisualAttestations = [],
        });
        Ok(ticksOnly.Stage == ShortProductionAcceptanceV1.QualityReviewed
            && ticksOnly.Blockers.Contains(ShortProductionQualityPipelineV1.VisualReviewIncomplete)
            && !ticksOnly.ProductionReady,
            "T19 an accept without the visual attestations is not production accept");

        var package = ShortProductionQualityPipelineV1.PrepareFuturePackage(
            ProductionEp01Sh02IngressCanon.Shot,
            ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract,
            ProductionEp01Sh02VoiceIngressCanon.LineText);
        Ok(package.InputGeneration == ShortProductionQualityPipelineV1.FutureGeneration
            && package.ActingBeat == ShortProductionQualityPipelineV1.Sh02FutureActing
            && package.SpokenLine == "Dạ… nay con làm bài không tốt."
            && package.RequiredProps.Contains("red-score")
            && package.Continuity.Contains("chair", StringComparison.Ordinal),
            "T20 the next SH02 package carries acting, spoken line, prop, and continuity");
        Ok(ShortProductionQualityPipelineV1.PrepareFutureVideo(
                ProductionEp01Sh02IngressCanon.Shot,
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract).InputGeneration
            == ShortProductionQualityPipelineV1.HistoricalGeneration,
            "T21 server replay of the historical SH02 acting stays historical");

        var sh05 = ShortProductionQualityPipelineV1.PrepareFutureVideo(
            "EP01-SC01-SH05", "Minh ngồi sẵn.", "Minh is already seated.", "");
        Ok(sh05.ActingBeat == ShortProductionQualityPipelineV1.Sh05FutureActing
            && sh05.ActingBeat.Contains("not already seated", StringComparison.Ordinal),
            "T22 the next SH05 request does not repeat the seated start");
        var sh03 = ShortProductionQualityPipelineV1.Ep01Contract("EP01-SC01-SH03");
        Ok(sh03 is not null
            && sh03.LineComplete
            && sh03.SpokenLine == ShortProductionQualityPipelineV1.Sh03RecordedLine
            && !ShortProductionQualityPipelineV1.SpokenLineLooksTruncated(sh03.SpokenLine),
            "T23 the recorded SH03 line is complete and stays locked");

        var clocks = ShortProductionQualityPipelineV1.FutureClockBlockers(true, 2.534m, 8m, 2.5m);
        Ok(clocks.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            && clocks.Contains(ShortProductionQualityPipelineV1.MixClockSplit),
            "T24 a split lipsync and a short line block the next clock");
        Ok(ShortProductionQualityPipelineV1.FutureClockBlockers(true, 4m, 4.4m).Count == 0,
            "T25 a line within the tolerance does not block the clock");

        var paths = ShortProductionQualityPipelineV1.Ep01Workflow();
        Ok(paths.Count == 6 && paths.All(p => p.Executable) && paths.All(p => !p.ProductionReady),
            "T26 every EP01 shot has an executable path and none is accepted");
        Ok(paths[2].PathKind == ShortProductionQualityPipelineV1.LegacyCandidate
            && paths[2].NextAction == ShortProductionQualityPipelineV1.BindExistingMedia
            && paths[2].PlaybackRef == "9F3XNLOMVOstXuQnDWXSK_kYgN6Z0V.mp4"
            && paths[5].PlaybackRef is null,
            "T27 legacy shots bind the known file and SH06 stays unbound");
        var bound = ShortProductionQualityPipelineV1.BindLegacyCandidate(new ShortQualityEvidence(
            "EP01-SC01-SH04",
            null, null, "video/mp4", 5.031m, "EP01-SC01-SH04", "EP01-SC01-SH04",
            false, ShortProductionAcceptanceV1.DecisionAccept, "cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4",
            "cvSjg3pDIS1WwwSMqwDuX_IUVQ7RU9.mp4",
            ShortProductionQualityPipelineV1.BrokenTakePaperPrefix,
            ShortProductionQualityPipelineV1.BrokenTakePaperPrefix,
            false, 0, 5.031m, "", true, null,
            [ShortProductionQualityPipelineV1.AttestAction, ShortProductionQualityPipelineV1.AttestContinuity, ShortProductionQualityPipelineV1.AttestMotion],
            "SEEN_LEGACY"));
        Ok(bound.Stage == ShortProductionQualityPipelineV1.LegacyCandidate
            && !bound.ProductionReady
            && bound.Notes.Contains(ShortProductionQualityPipelineV1.LegacyIsNotAcceptance)
            && bound.Blockers.Contains(ShortProductionQualityPipelineV1.ActionSentenceIncomplete),
            "T28 a legacy file with an accept tick is still not production accepted");

        var prop = ShortProductionQualityPipelineV1.Classify(CleanEvidence() with
        {
            RequiredProps = ["red-score"],
            ObservedProps = ["blank-paper"],
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
            VisualAttestations =
            [
                ShortProductionQualityPipelineV1.AttestAction,
                ShortProductionQualityPipelineV1.AttestContinuity,
                ShortProductionQualityPipelineV1.AttestMotion,
            ],
        });
        Ok(!prop.ProductionReady && prop.Blockers.Contains(ShortProductionQualityPipelineV1.RequiredPropMissing + ":red-score"),
            "T29 a missing required prop blocks an otherwise complete review");

        var workflowShots = paths.ToDictionary(
            p => p.ShotCode,
            p => new ShortQualityVerdict(p.ShotCode, p.PathKind, false, p.Blockers, [], p.PlaybackRef, ""));
        var blockedPlan = ShortProductionQualityPipelineV1.PlanEpisode(
            ShortProductionQualityPipelineV1.Ep01ShotOrder,
            workflowShots,
            paths.Select(p => (string?)p.PlaybackRef).ToArray(),
            "asm-ep01", "asm-ep01", ShortProductionAcceptanceV1.DecisionAccept, true);
        Ok(!blockedPlan.CanAssemble
            && !blockedPlan.ProductionReady
            && blockedPlan.Provenance.Count == 6
            && blockedPlan.Provenance[0].Order == 1
            && blockedPlan.Provenance[0].ShotCode == "EP01-SC01-SH01"
            && blockedPlan.Blockers.Any(b => b.EndsWith("EP01-SC01-SH06", StringComparison.Ordinal)),
            "T30 the six-shot plan keeps order and refuses the unbound shot");

        var readyPlan = ShortProductionQualityPipelineV1.PlanEpisode(
            ["GEN-SC01-SH01", "GEN-SC01-SH02"],
            new Dictionary<string, ShortQualityVerdict>
            {
                ["GEN-SC01-SH01"] = accepted,
                ["GEN-SC01-SH02"] = silent,
            },
            ["art-GEN-SC01-SH01", "art-GEN-SC01-SH02"],
            "asm-gen", "asm-gen", ShortProductionAcceptanceV1.DecisionAccept, true);
        Ok(readyPlan.CanAssemble && readyPlan.ProductionReady
            && readyPlan.ProvenanceKey.Contains("art-GEN-SC01-SH01", StringComparison.Ordinal),
            "T31 a different script can assemble when each playback and the assembly file are accepted");
        var stalePlan = ShortProductionQualityPipelineV1.PlanEpisode(
            ["GEN-SC01-SH01", "GEN-SC01-SH02"],
            new Dictionary<string, ShortQualityVerdict>
            {
                ["GEN-SC01-SH01"] = accepted,
                ["GEN-SC01-SH02"] = silent,
            },
            ["art-GEN-SC01-SH01", "art-GEN-SC01-SH02"],
            "asm-gen-2", "asm-gen", ShortProductionAcceptanceV1.DecisionAccept, true);
        Ok(stalePlan.CanAssemble && !stalePlan.ProductionReady
            && stalePlan.Notes.Contains(ShortProductionQualityPipelineV1.AssemblyTechnicalIsNotAcceptance),
            "T32 a verified assembly file is still not episode acceptance");

        Ok(!ShortProductionQualityPipelineV1.ConcatPermittedIsEpisodeAcceptance(),
            "T33 permission to concat is not episode acceptance");
        Ok(!ShortProductionQualityPipelineV1.FullEpisodeConcatPermitted(ShortProductionQualityPipelineV1.Ep01ShotOrder, null),
            "T34 the full episode cannot concat without six accepted playbacks");
        var sixReady = ShortProductionQualityPipelineV1.Ep01ShotOrder.ToDictionary(
            shot => shot,
            shot => AcceptedShot(shot, speaking: false));
        Ok(ShortProductionQualityPipelineV1.FullEpisodeConcatPermitted(ShortProductionQualityPipelineV1.Ep01ShotOrder, sixReady),
            "T35 six accepted playbacks permit concat and still require an assembly review");
        Ok(ProductionEpisodeAssemblyContract.OrderGate(ProductionEpisodeAssemblyContract.OfficialEp01Order, false) is null,
            "T36 the historical two-shot order stays a technical concat");
        Ok(ProductionEpisodeAssemblyContract.OrderGate(ShortProductionQualityPipelineV1.Ep01ShotOrder, false)
            == ShortProductionQualityPipelineV1.ShotNotAccepted,
            "T37 the six-shot order is refused until the quality permit");
        Ok(ProductionEpisodeAssemblyContract.OrderGate(ShortProductionQualityPipelineV1.Ep01ShotOrder, true) is null,
            "T38 the six-shot order is allowed after the quality permit");
        Ok(ShortProductionQualityPipelineV1.DirectorText(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            == ShortProductionQualityPipelineV1.DirectorSpeechClock,
            "T39 the director names the speech-clock failure");

        var sh03Voice = ShortProductionQualityPipelineV1.PrepareVoiceDraft(
            "EP01-SC01-SH03", "Điểm thấp thì lần sau gỡ được mà.");
        Ok(sh03Voice.Permitted
            && sh03Voice.SpokenLine == ShortProductionQualityPipelineV1.Sh03RecordedLine,
            "T40 the approved SH03 line is the locked line");
        var sh03Guess = ShortProductionQualityPipelineV1.PrepareVoiceDraft(
            "EP01-SC01-SH03", "Điểm thấp thì lần sau gỡ...");
        Ok(!sh03Guess.Permitted
            && sh03Guess.Blocker == ShortProductionQualityPipelineV1.SpokenLineIncomplete
            && sh03Guess.SpokenLine.Length == 0,
            "T40b a truncated SH03 line stays refused");
        var sh03Package = ShortProductionQualityPipelineV1.PrepareFuturePackage(
            "EP01-SC01-SH03",
            ShortProductionQualityPipelineV1.Sh03Sh04FutureActing,
            ShortProductionQualityPipelineV1.Sh03Sh04FutureActing,
            "",
            ShortProductionQualityPipelineV1.Sh03RecordedLine);
        Ok(!sh03Package.SpokenLineTruncated
            && sh03Package.SpokenLine == ShortProductionQualityPipelineV1.Sh03RecordedLine
            && sh03Package.Story.Contains("red", StringComparison.Ordinal)
            && sh03Package.ActingBeat.Contains("folded test", StringComparison.Ordinal),
            "T41 the next SH03 package keeps the action and carries the locked line");

        var sh05Voice = ShortProductionQualityPipelineV1.PrepareVoiceDraft("EP01-SC01-SH05", "");
        Ok(sh05Voice.Permitted && sh05Voice.SpokenLine == ShortProductionQualityPipelineV1.Sh05SpokenLine,
            "T42 an empty SH05 request receives the locked line");
        var sh02Voice = ShortProductionQualityPipelineV1.PrepareVoiceDraft(
            "EP01-SC01-SH02", ProductionEp01Sh02VoiceIngressCanon.LineText);
        Ok(sh02Voice.Permitted
            && sh02Voice.SpokenLine == "Dạ… nay con làm bài không tốt."
            && ProductionEp01Sh02VoiceIngressCanon.LineText.Contains("(lý nhí)", StringComparison.Ordinal),
            "T43 the next SH02 voice drops the stage cue and keeps the locked line");
        var other = ShortProductionQualityPipelineV1.PrepareVoiceDraft("GEN-SC01-SH01", "Khoan đã...");
        Ok(other.Permitted && other.SpokenLine == "Khoan đã...",
            "T44 another script may end with an ellipsis");
        var sh05Wrong = ShortProductionQualityPipelineV1.PrepareVoiceDraft("EP01-SC01-SH05", "Minh ngồi xuống.");
        Ok(!sh05Wrong.Permitted
            && ShortProductionQualityPipelineV1.Sh05SpokenLine == "Lát mẹ rảnh… chỉ lại chỗ này cho con nhé?",
            "T45 a different SH05 sentence does not replace the locked line");

        var staleActing = "Minh đứng yên và nhìn tờ giấy trắng.";
        var staleSh02 = ShortProductionQualityPipelineV1.PrepareFutureVideo(
            ProductionEp01Sh02IngressCanon.Shot,
            ProductionEp01Sh02IngressCanon.Story,
            staleActing,
            "");
        Ok(staleSh02.InputGeneration == ShortProductionQualityPipelineV1.FutureGeneration
            && staleSh02.ActingBeat == ShortProductionQualityPipelineV1.Sh02FutureActing
            && !staleActing.Contains("đưa hai", StringComparison.Ordinal),
            "T46 a stale SH02 sentence is replaced without matching the truncated phrase");
        var prompt = ShortProductionQualityPipelineV1.VideoPromptOf(staleSh02.Story, staleSh02.ActingBeat, staleSh02.VisualContract);
        Ok(prompt.Contains("red score", StringComparison.Ordinal)
            && prompt.Contains("bag", StringComparison.Ordinal)
            && prompt.Contains(ShortProductionQualityPipelineV1.Sh02FutureActing, StringComparison.Ordinal)
            && !prompt.Contains("(lý nhí)", StringComparison.Ordinal)
            && !prompt.Contains("đưa hai  ", StringComparison.Ordinal),
            "T47 the approved SH02 acting reaches the video prompt");

        var partial = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m,
            [new DialogueInterval(0m, 2.5m)],
            false, true,
            [new MixSpeechSpan(0m, 2.5m, true), new MixSpeechSpan(2.5m, 8m, false)]));
        Ok(!partial.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            && !partial.Contains(ShortProductionQualityPipelineV1.MixClockSplit)
            && !partial.Contains(ShortProductionQualityPipelineV1.MouthDuringIntendedSilence),
            "T48 dialogue may occupy only the start of a longer picture");

        var two = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2m, 2m,
            [new DialogueInterval(0m, 1m), new DialogueInterval(4m, 5m)],
            false, true,
            [new MixSpeechSpan(0m, 1m, true), new MixSpeechSpan(4m, 5m, true), new MixSpeechSpan(1m, 4m, false)]));
        Ok(two.Count == 0, "T49 two declared dialogue intervals can share one picture");

        var silentMouth = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m,
            [new DialogueInterval(0m, 2.5m)],
            true, true,
            [new MixSpeechSpan(0m, 2.5m, true), new MixSpeechSpan(2.5m, 8m, false)]));
        Ok(silentMouth.Contains(ShortProductionQualityPipelineV1.MouthDuringIntendedSilence)
            && !silentMouth.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback),
            "T50 mouth movement during intended silence still fails");

        var cutAudio = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 1m, 2.5m,
            [new DialogueInterval(0m, 2.5m)],
            false, true,
            [new MixSpeechSpan(0m, 2.5m, true)]));
        Ok(cutAudio.Contains(ShortProductionQualityPipelineV1.SpokenLineIncomplete),
            "T51 audio shorter than the declared dialogue window is truncated");

        var wrongWindow = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m,
            [new DialogueInterval(3m, 5.5m)],
            false, true,
            [new MixSpeechSpan(0m, 2.5m, true), new MixSpeechSpan(2.5m, 8m, false)]));
        Ok(wrongWindow.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch),
            "T52 speech placed outside the declared window fails");

        Ok(observed[1].Blockers.Contains(ShortProductionQualityPipelineV1.MouthDuringIntendedSilence),
            "T53 recorded SH02 still fails for mouth movement on the silent interval");

        var rebound = ShortProductionQualityPipelineV1.Classify(CleanEvidence() with
        {
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-1",
            PlaybackArtifactId = "art-1",
            ReviewedContentHash = "old",
            PlaybackContentHash = "new",
            VisualAttestations =
            [
                ShortProductionQualityPipelineV1.AttestAction,
                ShortProductionQualityPipelineV1.AttestContinuity,
                ShortProductionQualityPipelineV1.AttestMotion,
            ],
        });
        Ok(!rebound.ProductionReady
            && rebound.Blockers.Contains(ShortProductionQualityPipelineV1.ReviewHashMismatch),
            "T54 a new hash does not inherit the previous accept");
        Ok(ShortProductionQualityPipelineV1.ReviewBindsPlayback("art-1", "abc", "art-1", "abc")
            && !ShortProductionQualityPipelineV1.ReviewBindsPlayback("art-1", "abc", "art-2", "abc")
            && !ShortProductionQualityPipelineV1.ReviewBindsPlayback("art-1", "abc", "art-1", "def"),
            "T55 review binds only the same artifact id and hash");

        var names = ProductionStorageFileNames.Candidates("abc12345.mp4");
        Ok(names.Contains("abc12345.mp4") && names[0] == "abc12345.mp4",
            "T56 a storage key that already names the file keeps that name");
        Ok(ProductionStorageFileNames.Candidates("abc12345").Contains("abc12345.mp4"),
            "T57 a bare file id still resolves to the written mp4");

        var take = new ProductionArtifact(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ProductionArtifactRoles.VideoTake,
            "video/mp4", 32, 5, "sha-take", "key", null, DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "snap");
        var mix = take with { ArtifactId = Guid.NewGuid(), Role = ProductionArtifactRoles.Mix, ByteSha256 = "sha-mix" };
        var otherTake = take with { ArtifactId = Guid.NewGuid(), ByteSha256 = "sha-other" };
        var one = ProductionAttemptOutput.Select([take, mix], take.AttemptId, ProductionArtifactRoles.VideoTake);
        Ok(one.Artifact == take && !one.Ambiguous, "T58 replay selects the verified video take, not the mix");
        var ambiguous = ProductionAttemptOutput.Select([take, otherTake], take.AttemptId, ProductionArtifactRoles.VideoTake);
        Ok(ambiguous.Ambiguous && ambiguous.Artifact is null, "T59 two verified takes on one attempt are not a success");

        Ok(ShortProductionQualityPipelineV1.ClockToleranceSec == 0.75m, "T60 the clock tolerance stays 0.75 seconds");
        Ok(VeoMotionDurationMapping.ToProviderDurationSec(8) == 8
            && VeoMotionDurationMapping.ToProviderDurationSec(6) == 6
            && VeoMotionDurationMapping.ToProviderDurationSec(5) == 4,
            "T61 Veo duration mapping stays");
        var undeclared = ShortProductionQualityPipelineV1.FutureClockBlockers(true, 2.534m, 8m, 2.5m);
        Ok(undeclared.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback)
            && !undeclared.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch),
            "T62 an 8 second picture and a 2.5 second line stay blocked");
        Ok(ProductionDirectorTiming.ApprovedSpeech(new DirectorTimingDecision(
                8m, "DEFAULT_TAKE", "COMPUTED", "VIDEO", 0.2m, 2.7m)) is null,
            "T63 a computed 0.2 second lead-in is not a director interval");
        Ok(ProductionDirectorTiming.ApprovedSpeech(new DirectorTimingDecision(
                5m, ProductionDirectorTiming.Source, ProductionDirectorTiming.DecisionType, ProductionDirectorTiming.Stage)) is null
            && ProductionDirectorTiming.ApprovedSpeech(new DirectorTimingDecision(
                10m, ProductionDirectorTiming.Source, ProductionDirectorTiming.DecisionType, ProductionDirectorTiming.Stage)) is null,
            "T64 a 5 or 10 second picture duration is not a speech interval");

        var clockPicture = ClockMedia(ProductionArtifactRoles.VideoTake, 8m, "sha-picture");
        var clockLipsync = ClockMedia(ProductionArtifactRoles.Lipsync, 2.5m, "sha-speech");
        var placed = ProductionMixContract.Build(
            clockPicture, clockLipsync, "VIDEO", "LIPSYNC", "director-speech", new DirectorSpeechInterval(2m, 4.5m));
        var placedClock = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m,
            [new DialogueInterval(2m, 4.5m)],
            false, true,
            ProductionMixContract.SpeechSpans(placed)));
        Ok(!placedClock.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch)
            && !placedClock.Contains(ShortProductionAcceptanceV1.SpeechDoesNotCoverPlayback),
            "T65 the mix span that covers the director interval has no clock mismatch");
        var atZero = ProductionMixContract.Build(clockPicture, clockLipsync, "VIDEO", "LIPSYNC", "director-speech");
        var zeroClock = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m,
            [new DialogueInterval(2m, 4.5m)],
            false, true,
            ProductionMixContract.SpeechSpans(atZero)));
        Ok(zeroClock.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch),
            "T66 speech left at zero does not cover the director interval");
        var noSpan = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m,
            [new DialogueInterval(2m, 4.5m)],
            false, true,
            []));
        Ok(noSpan.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch),
            "T67 a missing mix span stays blocked");
        var speechAudio = placed.AudioSegments.First(s => s.SourceRole == ProductionArtifactRoles.Lipsync);
        var speechPicture = placed.PictureSegments.First(s => s.SourceRole == ProductionArtifactRoles.Lipsync);
        Ok(placed.OutputDurationSec == 8m
            && speechAudio.DestEndSec - speechAudio.DestStartSec == 2.5m
            && speechPicture.DestStartSec == speechAudio.DestStartSec
            && speechPicture.DestEndSec == speechAudio.DestEndSec
            && speechAudio.DestStartSec == 2m,
            "T68 duration and lip-sync sync stay intact and speech does not move to zero");

        return fail;
    }

    static ProductionArtifact ClockMedia(string role, decimal duration, string sha) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, "video/mp4", 32, duration, sha,
            "key", null, DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "snap");

    static ShortQualityVerdict AcceptedShot(string shot, bool speaking)
    {
        var attestations = new List<string>
        {
            ShortProductionQualityPipelineV1.AttestAction,
            ShortProductionQualityPipelineV1.AttestContinuity,
            ShortProductionQualityPipelineV1.AttestMotion,
        };
        if (speaking) attestations.Add(ShortProductionQualityPipelineV1.AttestMouth);
        return ShortProductionQualityPipelineV1.Classify(CleanEvidence() with
        {
            ShotCode = shot,
            ArtifactShotCode = shot,
            BindingShotCode = shot,
            QualityDecision = ShortProductionAcceptanceV1.DecisionAccept,
            ReviewedArtifactId = "art-" + shot,
            PlaybackArtifactId = "art-" + shot,
            DirectDialogue = speaking,
            SpeechDurationSec = speaking ? 4m : 0,
            PlaybackDurationSec = 4m,
            SpokenLineSent = speaking ? "Cầm giúp mình cuốn này." : "",
            SpokenLineComplete = true,
            MouthAligned = speaking ? true : null,
            VisualAttestations = attestations,
            Story = "An đứng ở cửa và đưa cuốn sách.",
            ActingBeat = "An stands at the door and holds the book out.",
            ReviewedContentHash = "hash-" + shot,
            PlaybackContentHash = "hash-" + shot,
        });
    }

    static ShortQualityEvidence CleanEvidence() => new(
        "GEN-SC01-SH01",
        ProductionTaskStatuses.Succeeded,
        ProductionArtifactStatuses.Verified,
        "video/mp4",
        4m,
        "GEN-SC01-SH01",
        "GEN-SC01-SH01",
        false,
        null,
        null,
        "art-1",
        "An đứng ở cửa và đưa cuốn sách.",
        "An stands at the door and holds the book out.",
        false,
        0,
        4m,
        "",
        true,
        null,
        [],
        "FIXTURE");
}
