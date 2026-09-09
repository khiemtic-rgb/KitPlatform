using System.Linq;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_AGE_GATE_V1 — Age Consistency is an independent Studio gate.
/// Prompt correctness is AGE_PROFILE_READY, never Age PASS. PASS is an image evaluation.
/// Does not call Gemini. Does not invent apparent age. Does not rewrite locked characters.
/// </summary>
public static class CharacterAgeGateV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_AGE_GATE_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_AGE_GATE_V1_REGRESSION";
    public const string EvaluatorDeferred = "DEFERRED_V1";
    public const string EvaluatorDirector = "DIRECTOR";

    public const string StatusNotEvaluated = CharacterAgeConsistencyV1Rules.StatusNotEvaluated;
    public const string StatusPass = CharacterAgeConsistencyV1Rules.StatusPass;
    public const string StatusFail = CharacterAgeConsistencyV1Rules.StatusFail;
    public const string StatusBlocked = CharacterAgeConsistencyV1Rules.StatusBlocked;

    public const string GateProfileNotReady = "AGE_PROFILE_NOT_READY";
    public const string GateInvalidChronological = "INVALID_CHRONOLOGICAL_AGE";
    public const string GateInvalidTargetRange = "INVALID_TARGET_AGE_RANGE";
    public const string GateCrpIncomplete = "CRP_INCOMPLETE";
    public const string GateEvaluationUnavailable = "AGE_EVALUATION_NOT_AVAILABLE";

    public const string ReasonOlder = "Visual age appears older than target range.";
    public const string ReasonYounger = "Visual age appears younger than target range.";
    public const string ReasonOutside = "Visual age appears outside the target range.";
    public const string ReasonNotEvaluated = "Age Consistency is not evaluated. Prompt correctness is not Age PASS.";

    public static bool AgeProfileReady(int? chronologicalAge, int? min, int? max, string? profile) =>
        ValidateProfile(chronologicalAge, min, max, profile) is null;

    public static bool AgeInstructionValid(int? chronologicalAge, int? min, int? max, string? profile) =>
        AgeProfileReady(chronologicalAge, min, max, profile);

    public static bool AgePass(string? status) =>
        string.Equals(status, StatusPass, StringComparison.OrdinalIgnoreCase);

    public static string? ValidateChronologicalAge(int? chronologicalAge)
    {
        if (chronologicalAge is null) return GateProfileNotReady;
        return chronologicalAge is < 1 or > 120 ? GateInvalidChronological : null;
    }

    public static string? ValidateTargetRange(int? min, int? max)
    {
        if (min is null || max is null) return GateProfileNotReady;
        return min > max ? GateInvalidTargetRange : null;
    }

    public static string? ValidateProfile(int? chronologicalAge, int? min, int? max, string? profile)
    {
        var chrono = ValidateChronologicalAge(chronologicalAge);
        if (chrono is not null) return chrono;
        var range = ValidateTargetRange(min, max);
        if (range is not null) return range;
        return string.IsNullOrWhiteSpace(profile) ? GateProfileNotReady : null;
    }

    public static string? ValidateEvaluationInput(
        int? chronologicalAge, int? min, int? max, string? profile, int coverage)
    {
        var profileGate = ValidateProfile(chronologicalAge, min, max, profile);
        if (profileGate is not null) return profileGate;
        return coverage < 4 ? GateCrpIncomplete : null;
    }

    public static AgeConsistencyResult Resolve(
        AgeExpressionTarget? target,
        string? profile,
        int coverage,
        string? storedStatus = null,
        int? storedScore = null,
        int? evaluatedAgeMin = null,
        int? evaluatedAgeMax = null,
        string? storedReason = null,
        string? evaluator = null,
        DateTimeOffset? evaluatedAt = null)
    {
        var chrono = target?.ChronologicalAge;
        var min = target?.TargetAppearanceAgeMin;
        var max = target?.TargetAppearanceAgeMax;
        var profileGate = ValidateProfile(chrono, min, max, profile);
        if (profileGate is not null)
            return Blocked(profileGate, min, max, storedReason ?? ReasonFor(profileGate), evaluator);
        if (coverage < 4)
        {
            return new AgeConsistencyResult(
                StatusNotEvaluated, false, null, min, max, null, null,
                ReasonFor(GateCrpIncomplete), evaluator ?? EvaluatorDeferred, null, GateCrpIncomplete);
        }

        var stored = (storedStatus ?? "").Trim().ToUpperInvariant();
        if (stored == StatusFail)
        {
            return new AgeConsistencyResult(
                StatusFail, false, storedScore ?? 0, min, max, evaluatedAgeMin, evaluatedAgeMax,
                storedReason ?? VisualAgeReason(min, max, evaluatedAgeMin, evaluatedAgeMax),
                evaluator ?? EvaluatorDirector, evaluatedAt, CharacterAgeConsistencyV1Rules.RejectAgeMismatch);
        }

        if (stored == StatusPass)
        {
            return new AgeConsistencyResult(
                StatusPass, true, storedScore, min, max, evaluatedAgeMin, evaluatedAgeMax,
                storedReason, evaluator ?? EvaluatorDirector, evaluatedAt, null);
        }

        return new AgeConsistencyResult(
            StatusNotEvaluated, false, null, min, max, null, null,
            ReasonNotEvaluated, evaluator ?? EvaluatorDeferred, null, null);
    }

    public static AgeConsistencyResult FromPrompt(bool promptContainsAgeProfile)
    {
        if (!promptContainsAgeProfile)
            return Blocked(GateProfileNotReady, null, null, ReasonFor(GateProfileNotReady), EvaluatorDeferred);
        return new AgeConsistencyResult(
            StatusNotEvaluated, false, null, null, null, null, null,
            ReasonNotEvaluated, EvaluatorDeferred, null, null);
    }

    public static bool PromptDoesNotImplyPass(string? prompt, AgeExpressionTarget target, string? gender)
    {
        var profile = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(target, gender);
        var contains = !string.IsNullOrWhiteSpace(prompt)
            && prompt.Contains(profile, StringComparison.Ordinal);
        var result = FromPrompt(contains);
        return result.Status == StatusNotEvaluated && !result.Pass;
    }

    public static string VisualAgeReason(int? targetMin, int? targetMax, int? evaluatedMin, int? evaluatedMax)
    {
        if (evaluatedMin is null && evaluatedMax is null)
            return ReasonOutside;
        var apparent = evaluatedMax ?? evaluatedMin;
        if (apparent is not null && targetMax is not null && apparent > targetMax)
            return ReasonOlder;
        apparent = evaluatedMin ?? evaluatedMax;
        if (apparent is not null && targetMin is not null && apparent < targetMin)
            return ReasonYounger;
        return ReasonOutside;
    }

    public static bool ReadyAllowed(
        bool facePass, bool agePass, bool stylePass, bool crpComplete, bool directorApproved, bool locked) =>
        facePass && agePass && stylePass && crpComplete && directorApproved && locked;

    public static bool CharacterReadyAllowed(
        bool profileReady, bool identityReady, bool masterLocked, bool dnaLocked, bool prpLocked,
        bool crpComplete, bool facePass, bool stylePass, bool agePass, bool directorApproved, bool crpLocked) =>
        CharacterAgeConsistencyV1Rules.CharacterReadyAllowed(
            profileReady, identityReady, masterLocked, dnaLocked, prpLocked,
            crpComplete, facePass, stylePass, agePass, directorApproved, crpLocked)
        && ReadyAllowed(facePass, agePass, stylePass, crpComplete, directorApproved, crpLocked);

    public static bool MayBecomeReady(bool officialLocked, string? ageStatus) =>
        CharacterAgeConsistencyV1Rules.MayBecomeReady(officialLocked, ageStatus);

    public static bool NotEvaluatedCannotBecomePass(string? status) =>
        !string.Equals(status, StatusNotEvaluated, StringComparison.OrdinalIgnoreCase)
        || !AgePass(status);

    public static AgeConsistencyResult Blocked(
        string code, int? targetMin, int? targetMax, string? reason, string? evaluator) =>
        new(StatusBlocked, false, null, targetMin, targetMax, null, null,
            reason ?? ReasonFor(code), evaluator ?? EvaluatorDeferred, null, code);

    public static string ReasonFor(string? code) => (code ?? "").Trim().ToUpperInvariant() switch
    {
        GateProfileNotReady => "Age Appearance Profile is missing.",
        GateInvalidChronological => "Chronological age is invalid.",
        GateInvalidTargetRange => "Target appearance age range is invalid.",
        GateCrpIncomplete => "Character reference pack is incomplete.",
        GateEvaluationUnavailable => "Age evaluation is not available.",
        _ => ReasonNotEvaluated,
    };

    public static bool UsesCharacterNameOrId(string profile, string? name, string? characterId, string? role)
    {
        if (string.IsNullOrWhiteSpace(profile)) return false;
        if (!string.IsNullOrWhiteSpace(characterId)
            && profile.Contains(characterId.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrWhiteSpace(role)
            && role.Trim().Length > 3
            && profile.Contains(role.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        var token = (name ?? "").Trim();
        if (token.Length < 2) return false;
        if ("vietnamese".Contains(token, StringComparison.OrdinalIgnoreCase))
            return false;
        return profile.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    public static bool UsesCharacterSpecificBranch() => false;
    public static bool InventsApparentAge() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRegenerate() => false;
    public static bool GeneratesVideo() => false;
    public static bool CallsGemini() => false;
    public static bool ChangesProjectVisualStyle() => false;
}

public sealed record AgeConsistencyResult(
    string Status,
    bool Pass,
    int? Score,
    int? TargetAgeMin,
    int? TargetAgeMax,
    int? EvaluatedAgeMin,
    int? EvaluatedAgeMax,
    string? Reason,
    string? Evaluator,
    DateTimeOffset? EvaluatedAt,
    string? Code = null);

public sealed record AgeConsistencyEvaluationRequest(
    string CharacterId,
    int? ChronologicalAge,
    int? TargetAppearanceAgeMin,
    int? TargetAppearanceAgeMax,
    string? AgeAppearanceProfile,
    int Coverage,
    IReadOnlyList<AgeConsistencyViewImage>? Views = null,
    string? StoredStatus = null,
    int? StoredScore = null,
    int? EvaluatedAgeMin = null,
    int? EvaluatedAgeMax = null,
    string? StoredReason = null,
    string? StoredEvaluator = null,
    DateTimeOffset? EvaluatedAt = null);

public sealed record AgeConsistencyViewImage(
    string View,
    byte[]? Bytes,
    string? Sha256);

public interface ICharacterAgeConsistencyEvaluator
{
    string EvaluatorId { get; }
    AgeConsistencyResult Evaluate(AgeConsistencyEvaluationRequest request);
}

/// <summary>
/// V1 MVP. Does not inspect pixels and never invents Age PASS from a correct prompt.
/// </summary>
public sealed class DeferredCharacterAgeConsistencyEvaluator : ICharacterAgeConsistencyEvaluator
{
    public string EvaluatorId => CharacterAgeGateV1Rules.EvaluatorDeferred;

    public AgeConsistencyResult Evaluate(AgeConsistencyEvaluationRequest request)
    {
        var target = request.ChronologicalAge is int age
            ? CharacterAgeConsistencyV1Rules.FromCanonicalAge(
                age, request.TargetAppearanceAgeMin, request.TargetAppearanceAgeMax)
            : null;
        var blocked = CharacterAgeGateV1Rules.ValidateEvaluationInput(
            request.ChronologicalAge,
            request.TargetAppearanceAgeMin,
            request.TargetAppearanceAgeMax,
            request.AgeAppearanceProfile,
            request.Coverage);
        if (blocked is not null)
        {
            return CharacterAgeGateV1Rules.Blocked(
                blocked,
                request.TargetAppearanceAgeMin,
                request.TargetAppearanceAgeMax,
                CharacterAgeGateV1Rules.ReasonFor(blocked),
                EvaluatorId);
        }

        var resolved = CharacterAgeGateV1Rules.Resolve(
            target,
            request.AgeAppearanceProfile,
            request.Coverage,
            request.StoredStatus,
            request.StoredScore,
            request.EvaluatedAgeMin,
            request.EvaluatedAgeMax,
            request.StoredReason,
            request.StoredEvaluator ?? EvaluatorId,
            request.EvaluatedAt);
        if (resolved.Status == CharacterAgeGateV1Rules.StatusNotEvaluated)
        {
            return resolved with
            {
                Evaluator = EvaluatorId,
                Reason = CharacterAgeGateV1Rules.ReasonNotEvaluated,
            };
        }

        return resolved;
    }
}
