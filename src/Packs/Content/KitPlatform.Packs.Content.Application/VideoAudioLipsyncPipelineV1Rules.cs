namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1 — stage gates, finalize, mix contract.
/// Does not call Gemini, Runway, ElevenLabs, or Fal. Does not mutate authority.
/// </summary>
public static class VideoAudioLipsyncPipelineV1Rules
{
    public const string DocumentId = "FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1";
    public const string SuiteId = "FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1_REGRESSION";
    public const string ProjectVisualMode = ProjectVisualModeAuthorityV1Rules.Mode3dStylized;
    public const string I2vProvider = "runway";
    public const string I2vModel = "gen4_turbo";
    public const bool I2vIsMuteTake = true;
    public const string LipsyncProvider = "fal";
    public const string LipsyncEndpoint = "fal-ai/sync-lipsync";
    public const string CanonicalFinalRender = "SERVER_FFMPEG";
    public const string BrowserWebmRole = "PREVIEW_ONLY";
    public const string StaffOverlayRole = "PREVIEW_ONLY";
    public const bool StaffOverlayEligibleForFinal = false;
    public const bool MultiSpeakerLipsyncSupported = false;
    public const bool ListenerMouthControl = false;
    public const bool LastFrameUsed = false;
    public const bool LastFrameOptional = true;
    public const bool InventVoiceId = false;
    public const bool InventSecondVisualCompiler = false;
    public const double SpeechLeadIn = 0.2;
    public const double SpeechTail = 0.22;
    public const double DialogueLinearGain = 1;
    public const int CanonicalSampleRate = 48000;
    public const int CanonicalChannels = 2;
    public const string CanonicalAudioCodec = "aac";

    public const string GateLipsyncRequired = "LIPSYNC_REQUIRED";
    public const string GateVoiceRequired = "VOICE_REQUIRED";
    public const string GateMultiSpeaker = "MULTI_SPEAKER_LIPSYNC_UNSUPPORTED";
    public const string GateVisualMode = "VISUAL_MODE_CONFLICT";
    public const string GateLegacyPhotoreal = "LEGACY_PHOTOREAL_CANON";
    public const string GatePreviewOnly = "PREVIEW_ONLY";
    public const string GateNotEligible = "NOT_ELIGIBLE_FOR_FINAL";
    public const string GateSpeaker = "SPEAKER_REQUIRED";

    public sealed record DialogueCue(
        string DialogueId,
        string ShotId,
        string SpeakerCharacterId,
        string Text,
        string? VoiceId,
        double StartSec,
        double EndSec,
        string? AudioAssetId,
        double AudioDurationSec,
        bool Ready);

    public sealed record ShotProductionInput(
        string ShotId,
        bool HasCanonicalKf,
        IReadOnlyList<DialogueCue> Cues,
        bool HasMuteTake,
        bool HasLipSync,
        bool MixReady = false,
        string? KfVisualMode = null,
        string? ProjectVisualMode = null,
        bool IdentityLocked = true,
        bool LegacyPhotorealAttached = false,
        bool OverlayPreview = false,
        double TakeDurationSec = 0,
        bool DirectorApproved = false);

    public sealed record PreflightResult(
        bool Allowed,
        IReadOnlyList<string> Blockers,
        string Kind,
        string Stage,
        string FinalSource,
        bool VoiceReady,
        bool VideoReady,
        bool LipSyncReady,
        bool MixReady,
        bool FinalReady,
        string AvSync,
        double VideoDurationSec,
        double AudioDurationSec,
        double FinalDurationSec);

    public static bool SameProjectMode(string? kfMode, string? projectMode = ProjectVisualMode)
    {
        var project = string.IsNullOrWhiteSpace(projectMode) ? ProjectVisualMode : projectMode.Trim();
        if (string.IsNullOrWhiteSpace(kfMode)) return true;
        return string.Equals(kfMode.Trim(), project, StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> UniqueSpeakers(IEnumerable<DialogueCue> cues) =>
        cues.Select(c => (c.SpeakerCharacterId ?? "").Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static string ClassifyShot(IReadOnlyList<DialogueCue> cues)
    {
        var n = UniqueSpeakers(cues).Count;
        if (n == 0) return "SILENT_SHOT";
        return n == 1 ? "SINGLE_SPEAKER_SHOT" : "MULTI_SPEAKER_SHOT";
    }

    public static (IReadOnlyList<DialogueCue> Cues, IReadOnlyList<string> Discarded, IReadOnlyList<string> MergedIds)
        MergeSameSpeakerCues(IReadOnlyList<DialogueCue> cues)
    {
        if (cues.Count <= 1)
            return (cues.ToArray(), Array.Empty<string>(), cues.Select(c => c.DialogueId).ToArray());
        var speakers = UniqueSpeakers(cues);
        if (speakers.Count != 1)
            return (cues.ToArray(), Array.Empty<string>(), cues.Select(c => c.DialogueId).ToArray());
        var first = cues[0];
        var last = cues[^1];
        var dur = cues.Sum(c => c.AudioDurationSec > 0 ? c.AudioDurationSec : Math.Max(0, c.EndSec - c.StartSec));
        var merged = first with
        {
            Text = string.Join(' ', cues.Select(c => c.Text)),
            EndSec = last.EndSec,
            AudioDurationSec = dur,
            Ready = cues.All(c => c.Ready),
        };
        return (new[] { merged }, Array.Empty<string>(), cues.Select(c => c.DialogueId).ToArray());
    }

    public static int I2vDurationForDialogue(double lastAudioEndSec)
    {
        var need = Math.Max(0, lastAudioEndSec) + SpeechTail;
        return need <= 5 ? 5 : 10;
    }

    public static bool DialogueExceedsI2vCap(double lastAudioEndSec) =>
        lastAudioEndSec + SpeechTail > 10.05;

    public static double StitchSeconds(string kind, double takeDurationSec, double lastAudioEndSec)
    {
        if (kind is "SILENT_SHOT" or "AMBIENCE_ONLY_SHOT")
            return takeDurationSec > 0 ? takeDurationSec : 5;
        var speech = lastAudioEndSec + SpeechTail;
        var take = takeDurationSec > 0 ? takeDurationSec : I2vDurationForDialogue(lastAudioEndSec);
        return Math.Max(speech, take);
    }

    public static string AvSyncStatus(double videoSec, double audioSec)
    {
        if (videoSec <= 0 && audioSec <= 0) return "FAIL";
        var delta = Math.Abs(videoSec - audioSec);
        if (delta <= 0.12) return "PASS";
        return delta <= 0.4 ? "WARNING" : "FAIL";
    }

    public static bool VoiceReadyOf(IReadOnlyList<DialogueCue> cues)
    {
        if (cues.Count == 0) return true;
        return cues.All(c =>
            !string.IsNullOrWhiteSpace(c.SpeakerCharacterId)
            && !string.IsNullOrWhiteSpace(c.VoiceId)
            && !string.IsNullOrWhiteSpace(c.AudioAssetId)
            && c.AudioDurationSec > 0.2
            && c.Ready);
    }

    public static (bool Kf, bool I2v, bool Voice, bool Lipsync, bool Mix, bool Character, bool VisualMode)
        RetryTouches(string stage)
    {
        return (
            false,
            stage == "i2v",
            stage == "voice",
            stage == "lipsync",
            stage == "mix",
            false,
            false);
    }

    public static PreflightResult Preflight(ShotProductionInput input)
    {
        var blockers = new List<string>();
        var kind = ClassifyShot(input.Cues);
        var spoken = kind is "SINGLE_SPEAKER_SHOT" or "MULTI_SPEAKER_SHOT";
        var lastEnd = input.Cues.Count == 0
            ? 0
            : input.Cues.Max(c => Math.Max(c.EndSec, c.StartSec + c.AudioDurationSec));
        var audio = spoken ? lastEnd : 0;
        var video = input.TakeDurationSec > 0 ? input.TakeDurationSec : (spoken ? I2vDurationForDialogue(lastEnd) : 5);
        var finalDur = StitchSeconds(kind, input.TakeDurationSec, lastEnd);

        if (input.LegacyPhotorealAttached) blockers.Add(GateLegacyPhotoreal);
        if (!SameProjectMode(input.KfVisualMode, input.ProjectVisualMode)) blockers.Add(GateVisualMode);
        if (input.Cues.Any(c => string.IsNullOrWhiteSpace(c.SpeakerCharacterId))) blockers.Add(GateSpeaker);
        if (!input.HasCanonicalKf) blockers.Add("MISSING_CANONICAL_KEYFRAME");

        var voiceReady = VoiceReadyOf(input.Cues);
        if (spoken && !voiceReady) blockers.Add(GateVoiceRequired);

        var videoReady = input.HasCanonicalKf && input.HasMuteTake;
        if (!videoReady && input.HasCanonicalKf) blockers.Add("MISSING_MUTE_TAKE");

        var lipReady = !spoken;
        var finalSource = "NONE";
        if (!spoken)
        {
            lipReady = true;
            finalSource = videoReady ? "I2V_ALLOWED" : "NONE";
        }
        else if (kind == "MULTI_SPEAKER_SHOT")
        {
            lipReady = false;
            blockers.Add(GateMultiSpeaker);
            finalSource = "NONE";
        }
        else if (!input.HasLipSync)
        {
            lipReady = false;
            blockers.Add(GateLipsyncRequired);
            finalSource = input.OverlayPreview ? "TTS_OVERLAY" : "RAW_I2V";
        }
        else
        {
            lipReady = true;
            finalSource = "LIPSYNC_VIDEO";
        }

        if (input.OverlayPreview && spoken)
        {
            blockers.Add(GatePreviewOnly);
            blockers.Add(GateNotEligible);
        }

        var mixReady = spoken
            ? input.MixReady && kind == "SINGLE_SPEAKER_SHOT"
            : videoReady && input.MixReady || !spoken && videoReady;
        if (!spoken) mixReady = videoReady;

        var finalReady =
            !blockers.Contains(GateLegacyPhotoreal)
            && !blockers.Contains(GateVisualMode)
            && !blockers.Contains(GateSpeaker)
            && input.HasCanonicalKf
            && videoReady
            && !input.OverlayPreview
            && kind != "MULTI_SPEAKER_SHOT"
            && (!spoken || (voiceReady && lipReady && mixReady && finalSource == "LIPSYNC_VIDEO"))
            && (spoken || finalSource == "I2V_ALLOWED");

        var stage = "DRAFT";
        if (input.HasCanonicalKf) stage = "VISUAL_READY";
        if (input.HasCanonicalKf && (!spoken || voiceReady)) stage = "VOICE_READY";
        if (videoReady) stage = "VIDEO_READY";
        if (videoReady && lipReady) stage = "LIPSYNC_READY";
        if (videoReady && lipReady && mixReady) stage = "MIX_READY";
        if (finalReady) stage = input.DirectorApproved ? "APPROVED" : "DIRECTOR_REVIEW";
        if (!finalReady && spoken && videoReady && !lipReady) stage = "BLOCKED";

        var avAudio = spoken ? Math.Max(audio, video) : video;
        return new PreflightResult(
            finalReady,
            blockers.Distinct().ToArray(),
            kind,
            stage,
            finalSource,
            voiceReady,
            videoReady,
            lipReady,
            mixReady,
            finalReady,
            AvSyncStatus(video, avAudio),
            video,
            audio,
            finalDur);
    }

    public static bool OverlayCannotFinalize(ShotProductionInput input) =>
        Preflight(input with { OverlayPreview = true }).Blockers.Contains(GateNotEligible);

    public static bool AuthorityUntouched() =>
        ProjectVisualMode == "3D_STYLIZED_REALISM"
        && !InventSecondVisualCompiler
        && !InventVoiceId
        && VideoVisualIngressV1Rules.StyleCompiler == "IUnifiedVisualCompiler";
}
