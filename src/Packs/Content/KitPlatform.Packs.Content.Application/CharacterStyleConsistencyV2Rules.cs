namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_STYLE_CONSISTENCY_V2 — style gate independent of Face / Age / Appearance.
/// Compilation success is not pixel PASS. Without an image evaluator the status is
/// NOT_EVALUATED. Never invents PASS 96%. Never calls Gemini.
/// </summary>
public static class CharacterStyleConsistencyV2Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_STYLE_CONSISTENCY_V2";
    public const string StatusNotEvaluated = "NOT_EVALUATED";
    public const string StatusPass = "PASS";
    public const string StatusFail = "FAIL";

    public const string GateNotEvaluated = "STYLE_CONSISTENCY_NOT_EVALUATED";
    public const string GateFail = "STYLE_CONSISTENCY_FAIL";

    public static bool FaceEqualsStyle() => false;
    public static bool AgeEqualsStyle() => false;
    public static bool AppearanceEqualsStyle() => false;
    public static bool CompilationImpliesPass() => false;
    public static bool InventsScore() => false;
    public static bool CallsGemini() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;

    public static string StatusWithoutEvaluator() => StatusNotEvaluated;

    public static string? GateWithoutEvaluator() => GateNotEvaluated;

    public static int? ScoreWithoutEvaluator() => null;

    public static bool ReadyAllowed(bool facePass, bool agePass, bool appearancePass, string? styleStatus) =>
        facePass
        && agePass
        && appearancePass
        && string.Equals(styleStatus, StatusPass, StringComparison.OrdinalIgnoreCase);

    public static bool SemanticPassConcepts(string? evidence) =>
        !string.IsNullOrWhiteSpace(evidence)
        && evidence.Contains("stylized 3D", StringComparison.OrdinalIgnoreCase)
        && evidence.Contains("designed character", StringComparison.OrdinalIgnoreCase);

    public static bool SemanticFailConcepts(string? evidence) =>
        !string.IsNullOrWhiteSpace(evidence)
        && (evidence.Contains("photorealistic human", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("digital human", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("photographic portrait", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("extreme cartoon", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("anime", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("chibi", StringComparison.OrdinalIgnoreCase));
}
