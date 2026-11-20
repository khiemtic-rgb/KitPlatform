namespace KitPlatform.Packs.Content.Production.Legacy;

/// <summary>
/// Read-only catalog of Series desk graph fields. Not authority after Work Execution V1.
/// New production paths must not read these as CURRENT / VISIBLE / ACCEPTED.
/// </summary>
public static class LegacySeriesGraphAdapter
{
    public const string Authority = "LEGACY_COMPATIBILITY_ONLY";

    public static readonly IReadOnlyList<string> NonAuthoritativeFields =
    [
        "SeriesShotRun.takeUrl",
        "SeriesShotRun.previewUrl",
        "SeriesShotRun.lipsyncUrl",
        "SeriesShotRun.lipsyncTaskId",
        "SeriesShotRun.turboTaskId",
        "SeriesShotRun.acceptedTake.url",
        "SeriesShotRun.currentTake",
        "SeriesShotRun.videoApproved",
        "SeriesShotRun.shotQa",
        "SeriesShotRun.visualQa",
        "SeriesShotRun.runwayAttempts",
        "assembleVideoUrl",
        "fallbackMotionUrl",
        "resolveTakeUrl",
        "ShotProductionFinalStore",
        "localStorage.famixa-series-pilot",
        "indexedDB.keyframePixels",
        "indexedDB.kit-famixa-tts",
        "audioUrl",
        "voiceUrl",
        "voiceAsset",
        "ttsAsset",
        "lineAudio",
        "session.ttsBlobs",
        "session.falBlobUrl",
        "nextShotProductionCommand",
        "sendTurbo",
        "startContentSeriesTurbo",
        "POST /series/turbo",
        "validateMotionCallback.INPUT_CHANGED",
        "resolveCurrentProductionVideoSource",
        "compileI2vPrompt.holdBody",
        "pack_content.video_generation_job",
        "pack_content.video_generation_execution",
    ];

    public static bool IsAuthoritative() => false;

    public static bool MayDriveNewBinding(string field) => false;
}
