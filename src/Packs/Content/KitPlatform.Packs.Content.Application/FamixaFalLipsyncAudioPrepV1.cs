namespace KitPlatform.Packs.Content;

/// <summary>
/// LipSync voice bytes stay full length. Motion performance clock is not an atrim.
/// Fal v3 documented input is video_url + audio_url — no duration trim field.
/// </summary>
public static class FamixaFalLipsyncAudioPrepV1
{
    public const string SuiteId = "FAMIXA_FAL_LIPSYNC_AUDIO_PREP_V1";

    public static bool KeepFullVoiceBytes(int voiceCount) => voiceCount == 1;

    public static double? MergePadDurationSec(int voiceCount, double? performanceDurationSec)
    {
        if (KeepFullVoiceBytes(voiceCount)) return null;
        return performanceDurationSec is > 0 ? performanceDurationSec : null;
    }
}
