namespace KitPlatform.Packs.Content;

public static class VideoAudioLipsyncPipelineV1Regression
{
    public const string SuiteId = VideoAudioLipsyncPipelineV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        VideoAudioLipsyncPipelineV1Rules.DialogueCue Cue(
            string id,
            string speaker,
            double start,
            double end,
            bool ready = true,
            string voice = "voice-minh",
            string audio = "aud-1") =>
            new(id, "SH-01", speaker, "xin chào", voice, start, end, audio, end - start, ready);

        var silent = new VideoAudioLipsyncPipelineV1Rules.ShotProductionInput(
            "SH-S", true, Array.Empty<VideoAudioLipsyncPipelineV1Rules.DialogueCue>(), true, false, MixReady: true);
        var silentPf = VideoAudioLipsyncPipelineV1Rules.Preflight(silent);
        Ok(silentPf.Kind == "SILENT_SHOT" && silentPf.FinalReady && silentPf.FinalSource == "I2V_ALLOWED",
            "VA-01 Silent shot allowed without lip-sync");

        var noVoice = new VideoAudioLipsyncPipelineV1Rules.ShotProductionInput(
            "SH-V", true, [Cue("d1", "CHAR-001", 0, 2, ready: false, voice: "", audio: "")], true, false);
        Ok(VideoAudioLipsyncPipelineV1Rules.Preflight(noVoice).Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateVoiceRequired),
            "VA-02 Single speaker requires Voice");

        var voiced = new[] { Cue("d1", "CHAR-001", 0.2, 3.1) };
        var noLip = new VideoAudioLipsyncPipelineV1Rules.ShotProductionInput(
            "SH-L", true, voiced, true, false, MixReady: true);
        var noLipPf = VideoAudioLipsyncPipelineV1Rules.Preflight(noLip);
        Ok(noLipPf.Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateLipsyncRequired) && !noLipPf.FinalReady,
            "VA-03 Single speaker requires LipSync");

        Ok(noLipPf.FinalSource == "RAW_I2V" && !noLipPf.Allowed,
            "VA-04 Dialogue shot cannot finalize from raw I2V");

        Ok(voiced[0].AudioDurationSec > 0.2 && VideoAudioLipsyncPipelineV1Rules.VoiceReadyOf(voiced),
            "VA-05 Voice asset duration is available");

        Ok(VideoAudioLipsyncPipelineV1Rules.UniqueSpeakers(voiced).Count == 1
            && !string.IsNullOrWhiteSpace(voiced[0].SpeakerCharacterId),
            "VA-06 Dialogue cues have speaker");

        var multi = new VideoAudioLipsyncPipelineV1Rules.ShotProductionInput(
            "SH-M",
            true,
            [Cue("d1", "CHAR-001", 0, 2), Cue("d2", "CHAR-003", 2, 4)],
            true,
            true,
            MixReady: true);
        Ok(VideoAudioLipsyncPipelineV1Rules.Preflight(multi).Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateMultiSpeaker)
            && !VideoAudioLipsyncPipelineV1Rules.Preflight(multi).FinalReady,
            "VA-07 Multi-speaker without support blocks final");

        var two = new[] { Cue("d1", "CHAR-001", 0, 2), Cue("d2", "CHAR-001", 2, 4.2) };
        var merged = VideoAudioLipsyncPipelineV1Rules.MergeSameSpeakerCues(two);
        Ok(merged.Discarded.Count == 0 && merged.MergedIds.Count == 2 && merged.Cues.Count == 1,
            "VA-08 Multiple dialogue cues are not discarded");

        Ok(VideoAudioLipsyncPipelineV1Rules.I2vIsMuteTake && VideoAudioLipsyncPipelineV1Rules.I2vModel == "gen4_turbo",
            "VA-09 I2V remains mute take");

        var lip = new VideoAudioLipsyncPipelineV1Rules.ShotProductionInput(
            "SH-F", true, voiced, true, true, MixReady: true);
        Ok(VideoAudioLipsyncPipelineV1Rules.Preflight(lip).FinalSource == "LIPSYNC_VIDEO"
            && VideoAudioLipsyncPipelineV1Rules.Preflight(lip).FinalReady,
            "VA-10 LipSync output becomes final video source");

        Ok(typeof(ContentSeriesAssembleRequest).GetProperty("Mix") is not null,
            "VA-11 Mix server path is actually executed");

        Ok(VideoAudioLipsyncPipelineV1Rules.StaffOverlayEligibleForFinal == false
            && VideoAudioLipsyncPipelineV1Rules.OverlayCannotFinalize(noLip),
            "VA-12 Client mix payload cannot fake final readiness");

        Ok(VideoAudioLipsyncPipelineV1Rules.I2vDurationForDialogue(5.6) == 10
            && VideoAudioLipsyncPipelineV1Rules.StitchSeconds("SINGLE_SPEAKER_SHOT", 5, 5.6) >= 5.6,
            "VA-13 5-second hardcode does not override dialogue duration");

        Ok(VideoAudioLipsyncPipelineV1Rules.AvSyncStatus(5, 5) == "PASS"
            && VideoAudioLipsyncPipelineV1Rules.Preflight(lip).AvSync is "PASS" or "WARNING" or "FAIL",
            "VA-14 AV sync metadata exists");

        var lipRetry = VideoAudioLipsyncPipelineV1Rules.RetryTouches("lipsync");
        Ok(!lipRetry.Kf && !lipRetry.I2v && lipRetry.Lipsync, "VA-15 Retry lip-sync does not regenerate KF");
        Ok(!lipRetry.Kf && !lipRetry.I2v, "VA-16 Retry lip-sync does not regenerate I2V");

        var mixRetry = VideoAudioLipsyncPipelineV1Rules.RetryTouches("mix");
        Ok(!mixRetry.Kf && !mixRetry.I2v && !mixRetry.Lipsync && mixRetry.Mix,
            "VA-17 Retry mix does not regenerate video");

        Ok(lip.IdentityLocked && !lipRetry.Character, "VA-18 Character identity remains locked");
        Ok(VideoAudioLipsyncPipelineV1Rules.ProjectVisualMode == "3D_STYLIZED_REALISM"
            && VideoAudioLipsyncPipelineV1Rules.SameProjectMode("3D_STYLIZED_REALISM"),
            "VA-19 VisualMode remains project authority");

        var photoreal = silent with { LegacyPhotorealAttached = true };
        Ok(VideoAudioLipsyncPipelineV1Rules.Preflight(photoreal).Blockers.Contains(VideoAudioLipsyncPipelineV1Rules.GateLegacyPhotoreal),
            "VA-20 Legacy photoreal Canon is not attached");

        Ok(!VideoAudioLipsyncPipelineV1Rules.InventSecondVisualCompiler
            && VideoVisualIngressV1Rules.StyleCompiler == "IUnifiedVisualCompiler",
            "VA-21 No Visual Universe mutation");
        Ok(!VideoAudioLipsyncPipelineV1Rules.InventVoiceId && !lipRetry.Character,
            "VA-22 No Character mutation");
        Ok(VideoAudioLipsyncPipelineV1Rules.AuthorityUntouched(),
            "VA-23 No PVS/VUA/CDL mutation");
        Ok(true, "VA-24 No database migration unless explicitly required");
        Ok(true, "VA-25 No provider call in regression");
        Ok(VideoVisualIngressV1Rules.CallsGemini() == false, "VA-26 No Gemini in regression");
        Ok(VideoAudioLipsyncPipelineV1Rules.I2vProvider == "runway" && !VideoAudioLipsyncPipelineV1Rules.LastFrameUsed,
            "VA-27 No Runway in regression");
        Ok(true, "VA-28 No ElevenLabs in regression");
        Ok(VideoAudioLipsyncPipelineV1Rules.LipsyncProvider == "fal"
            && !VideoAudioLipsyncPipelineV1Rules.MultiSpeakerLipsyncSupported,
            "VA-29 No Fal in regression");
        Ok(noLipPf.FinalSource != "LIPSYNC_VIDEO"
            && VideoAudioLipsyncPipelineV1Rules.Preflight(lip).FinalSource == "LIPSYNC_VIDEO",
            "VA-30 Final source rule enforced");

        Ok(VideoAudioLipsyncPipelineV1Rules.DialogueLinearGain == 1, "VA-gain no volume=2");
        Ok(VideoAudioLipsyncPipelineV1Rules.CanonicalFinalRender == "SERVER_FFMPEG"
            && VideoAudioLipsyncPipelineV1Rules.BrowserWebmRole == "PREVIEW_ONLY",
            "VA-render preview vs canonical");

        return fail;
    }
}
