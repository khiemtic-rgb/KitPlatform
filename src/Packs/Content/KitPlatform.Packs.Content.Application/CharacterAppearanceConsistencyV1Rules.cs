using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_APPEARANCE_CONSISTENCY_V1 — independent Appearance gate.
/// Prompt correctness ≠ Appearance PASS. Does not invent PASS. Does not call Gemini.
/// </summary>
public static class CharacterAppearanceConsistencyV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_APPEARANCE_CONSISTENCY_V1";

    public const string StatusPass = "PASS";
    public const string StatusFail = "FAIL";
    public const string StatusNotEvaluated = "NOT_EVALUATED";
    public const string StatusBlocked = "BLOCKED";

    public const string GateNotReady = CharacterAppearanceProfileV1Rules.GateNotReady;
    public const string GateCrpIncomplete = "CRP_INCOMPLETE";
    public const string GateStereotype = "APPEARANCE_STEREOTYPE";
    public const string GateViewDrift = "APPEARANCE_VIEW_DRIFT";
    public const string GateReviewNotReady = "APPEARANCE_REVIEW_NOT_READY";
    public const string GateReviewInvalid = "APPEARANCE_REVIEW_INVALID";

    public static bool MayDirectorReview(int coverage, bool officialLocked) =>
        coverage >= 4 && !officialLocked;

    public static string NormalizeReviewResult(string? raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        return t is StatusPass or StatusFail ? t : "";
    }

    public static string? ReviewGate(string? result, int coverage, bool officialLocked)
    {
        if (officialLocked) return "AUTHORITY_LOCKED";
        if (!MayDirectorReview(coverage, officialLocked)) return GateReviewNotReady;
        return NormalizeReviewResult(result).Length == 0 ? GateReviewInvalid : null;
    }

    public static CharacterAppearanceConsistencyResult Resolve(
        CharacterAppearanceProfile? profile,
        int coverage,
        string? storedStatus,
        bool officialLocked)
    {
        if (officialLocked)
            return new CharacterAppearanceConsistencyResult(
                StatusNotEvaluated, false, null, CharacterAppearanceProfileV1Rules.MayRebuild(false) ? null : "LOCKED");
        if (profile is null || !CharacterAppearanceProfileV1Rules.IsReady(profile))
            return new CharacterAppearanceConsistencyResult(
                StatusBlocked, false, GateNotReady, "Character Appearance Profile is not ready.");
        if (coverage < 4)
            return new CharacterAppearanceConsistencyResult(
                StatusNotEvaluated, false, GateCrpIncomplete, "Appearance Consistency evaluates after 4 views.");
        if (string.Equals(storedStatus, StatusFail, StringComparison.OrdinalIgnoreCase))
            return new CharacterAppearanceConsistencyResult(StatusFail, false, GateStereotype, storedStatus);
        if (string.Equals(storedStatus, StatusPass, StringComparison.OrdinalIgnoreCase))
            return new CharacterAppearanceConsistencyResult(StatusPass, true, null, null);
        return new CharacterAppearanceConsistencyResult(
            StatusNotEvaluated, false, null, "Appearance Consistency is awaiting Director review.");
    }

    public static bool ViewsShareProfile(
        IReadOnlyDictionary<string, string> viewPrompts, CharacterAppearanceProfile profile) =>
        CharacterAppearanceProfileV1Rules.ViewsShareAppearance(viewPrompts, profile);

    public static bool ContainsProhibitedStereotype(string? text, CharacterAppearanceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return profile.NegativeAppearanceConstraints.Any(n =>
            text.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    public static bool FaceAgeStyleAppearanceIndependent() => true;

    public static bool ReadyAllowed(
        bool facePass, bool agePass, bool stylePass, bool appearancePass,
        bool crpComplete, bool directorApproved, bool locked) =>
        facePass && agePass && stylePass && appearancePass && crpComplete && directorApproved && locked;

    public static bool MayBecomeReady(bool officialLocked, string? appearanceStatus) =>
        officialLocked
        || string.Equals(appearanceStatus, StatusPass, StringComparison.OrdinalIgnoreCase);

    public static bool PromptDoesNotImplyPass() => true;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CallsGemini() => false;
    public static bool InventsAppearanceScore() => false;
    public static bool UsesCharacterSpecificBranch() => false;
}

public sealed record CharacterAppearanceConsistencyResult(
    string Status,
    bool Pass,
    string? Code,
    string? Reason);

public sealed record CharacterAppearanceConsistencyReviewRequestDto(
    string? Result = null,
    string? ReasonCode = null,
    string? Note = null);
