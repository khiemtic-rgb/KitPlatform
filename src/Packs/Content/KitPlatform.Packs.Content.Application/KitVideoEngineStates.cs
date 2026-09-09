namespace KitPlatform.Packs.Content;

/// <summary>KIT Video Engine V1 Checkpoint 1 — production / shot state machine. Engine ≠ Project.</summary>
public static class KitVideoEngineStates
{
    public const string EngineVersion = "KIT-VIDEO-ENGINE-V1";

    public static readonly string[] Production =
    [
        "DRAFT", "SCRIPT_APPROVED", "SHOT_PLANNED", "SCENE_MASTER_READY",
        "KEYFRAME_GENERATION", "KEYFRAME_REVIEW", "KEYFRAME_APPROVED",
        "VIDEO_GENERATION", "VIDEO_READY", "LIPSYNC", "LIPSYNC_READY",
        "EDITING", "FINAL_QA", "FINAL",
    ];

    public static readonly string[] Shot =
    [
        "DRAFT", "HOLD", "READY", "KF_GENERATING", "KF_REVIEW", "KF_APPROVED",
        "I2V_READY", "I2V_GENERATING", "VIDEO_READY", "LIPSYNC_READY",
        "FINAL_SELECTED", "FAILED",
    ];

    private static readonly Dictionary<string, string[]> ProductionNext = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DRAFT"] = ["SCRIPT_APPROVED"],
        ["SCRIPT_APPROVED"] = ["SHOT_PLANNED"],
        ["SHOT_PLANNED"] = ["SCENE_MASTER_READY"],
        ["SCENE_MASTER_READY"] = ["KEYFRAME_GENERATION"],
        ["KEYFRAME_GENERATION"] = ["KEYFRAME_REVIEW"],
        ["KEYFRAME_REVIEW"] = ["KEYFRAME_APPROVED", "KEYFRAME_GENERATION"],
        ["KEYFRAME_APPROVED"] = ["VIDEO_GENERATION"],
        ["VIDEO_GENERATION"] = ["VIDEO_READY", "KEYFRAME_APPROVED"],
        ["VIDEO_READY"] = ["LIPSYNC"],
        ["LIPSYNC"] = ["LIPSYNC_READY", "VIDEO_READY"],
        ["LIPSYNC_READY"] = ["EDITING"],
        ["EDITING"] = ["FINAL_QA"],
        ["FINAL_QA"] = ["FINAL", "EDITING"],
        ["FINAL"] = [],
    };

    private static readonly Dictionary<string, string[]> ShotNext = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DRAFT"] = ["READY", "HOLD"],
        ["HOLD"] = ["READY", "DRAFT"],
        ["READY"] = ["KF_GENERATING", "HOLD"],
        ["KF_GENERATING"] = ["KF_REVIEW", "FAILED"],
        ["KF_REVIEW"] = ["KF_APPROVED", "KF_GENERATING", "FAILED"],
        ["KF_APPROVED"] = ["I2V_READY"],
        ["I2V_READY"] = ["I2V_GENERATING"],
        ["I2V_GENERATING"] = ["VIDEO_READY", "FAILED"],
        ["VIDEO_READY"] = ["LIPSYNC_READY", "FINAL_SELECTED"],
        ["LIPSYNC_READY"] = ["FINAL_SELECTED"],
        ["FINAL_SELECTED"] = [],
        ["FAILED"] = ["READY", "KF_REVIEW", "I2V_READY"],
    };

    public static void EnsureProduction(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return;
        if (!ProductionNext.TryGetValue(from, out var next) || !next.Contains(to, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Illegal production jump {from} → {to}.");
    }

    public static void EnsureShot(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return;
        if (!ShotNext.TryGetValue(from, out var next) || !next.Contains(to, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Illegal shot jump {from} → {to}.");
    }

    public static void EnsureShotMayCallProvider(string from, string to)
    {
        if (string.Equals(from, "HOLD", StringComparison.OrdinalIgnoreCase)
            && to is "KF_GENERATING" or "I2V_GENERATING" or "I2V_READY")
            throw new InvalidOperationException("HOLD: shot HOLD không được gửi Image Generation hoặc I2V.");
        EnsureShot(from, to);
    }
}
