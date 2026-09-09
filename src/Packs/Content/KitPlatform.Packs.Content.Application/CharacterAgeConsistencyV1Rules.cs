using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_AGE_CONSISTENCY_V1 — shared Age Profile / Age Policy / Age Gate.
/// Does not invent pixel age scores. Does not rewrite locked Identity/Master/DNA/PRP.
/// Does not mix Age Profile into Project Visual Style or Character Identity SHA.
/// </summary>
public static class CharacterAgeConsistencyV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_AGE_CONSISTENCY_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_AGE_CONSISTENCY_V1_REGRESSION";

    public const string StatusPass = "PASS";
    public const string StatusFail = "FAIL";
    public const string StatusNotEvaluated = "NOT_EVALUATED";
    public const string StatusBlocked = "BLOCKED";
    public const string StatusPendingReview = "PENDING_REVIEW";
    public const string StatusWarning = "WARNING";

    public const string LifeStageInfant = "INFANT";
    public const string LifeStageChild = "CHILD";
    public const string LifeStageTeen = "TEEN";
    public const string LifeStageYoungAdult = "YOUNG_ADULT";
    public const string LifeStageAdult = "ADULT";
    public const string LifeStageMature = "MATURE";
    public const string LifeStageSenior = "SENIOR";

    public const string RejectAgeMismatch = "AGE_MISMATCH";
    public const string GateNotReady = "AGE_PROFILE_NOT_READY";
    public const string GateInvalid = "INVALID_AGE_PROFILE";
    public const string GateInvalidChronological = "INVALID_CHRONOLOGICAL_AGE";
    public const string GateInvalidTargetRange = "INVALID_TARGET_AGE_RANGE";
    public const string GateCrpIncomplete = "CRP_INCOMPLETE";
    public const string GateEvaluationUnavailable = "AGE_EVALUATION_NOT_AVAILABLE";
    public const string GateReviewNotReady = "AGE_REVIEW_NOT_READY";
    public const string GateReadyBlocked = "AGE_CONSISTENCY_REQUIRED";

    public const int WarningToleranceYears = 2;

    /// <summary>Single Age Policy. Change ranges only here.</summary>
    public static readonly IReadOnlyList<AgePolicyBand> PolicyBands =
    [
        new(0, 3, 1, LifeStageInfant),
        new(4, 7, 1, LifeStageChild),
        new(8, 12, 1, LifeStageChild),
        new(13, 17, 1, LifeStageTeen),
        new(18, 29, 2, LifeStageYoungAdult),
        new(30, 49, 3, LifeStageAdult),
        new(50, 69, 4, LifeStageMature),
        new(70, 200, 5, LifeStageSenior),
    ];

    public static CharacterAgeConsistencyOptions Options { get; } = new();

    public static AgePolicyBand BandOf(int ageYears) =>
        PolicyBands.FirstOrDefault(b => ageYears >= b.MinAge && ageYears <= b.MaxAge)
        ?? PolicyBands[^1];

    public static int ToleranceYearsOf(int ageYears) => BandOf(ageYears).ToleranceYears;

    public static string LifeStageOf(int ageYears) => BandOf(ageYears).LifeStage;

    public static AgeExpressionTarget FromCanonicalAge(
        int ageYears,
        int? overrideMin = null,
        int? overrideMax = null,
        string? overrideProvenance = null)
    {
        var band = BandOf(ageYears);
        var min = overrideMin ?? Math.Max(0, ageYears - band.ToleranceYears);
        var max = overrideMax ?? ageYears + band.ToleranceYears;
        if (min > max) (min, max) = (max, min);
        return new AgeExpressionTarget(
            ChronologicalAge: ageYears,
            TargetAppearanceAgeMin: min,
            TargetAppearanceAgeMax: max,
            LifeStage: band.LifeStage,
            OverrideProvenance: string.IsNullOrWhiteSpace(overrideProvenance) ? null : overrideProvenance.Trim(),
            Guidance: GuidanceOf(band.LifeStage),
            Avoid: AvoidOf(band.LifeStage));
    }

    public static string? ValidateProfile(int? chronologicalAge, int? min, int? max)
    {
        if (chronologicalAge is null or < 1 or > 120 || min is null || max is null)
            return GateNotReady;
        if (min > max) return GateInvalid;
        return null;
    }

    public static bool GenerationAllowed(int? chronologicalAge, int? min, int? max) =>
        ValidateProfile(chronologicalAge, min, max) is null;

    public static bool MayDirectorReview(int coverage, bool officialLocked) =>
        coverage >= 4 && !officialLocked;

    public static bool MayMarkPass(int coverage, bool officialLocked) =>
        MayDirectorReview(coverage, officialLocked);

    public static bool ReadyRequiresAgePass(bool officialLocked) => !officialLocked;

    public static bool MayBecomeReady(bool officialLocked, string? ageStatus) =>
        officialLocked
        || string.Equals(ageStatus, StatusPass, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GuidanceOf(string lifeStage) => lifeStage switch
    {
        LifeStageInfant or LifeStageChild =>
        [
            "clearly a child",
            "child appearance",
            "child facial proportions",
            "child body proportions",
            "age-appropriate head/body proportion",
            "natural child posture",
            "child hairstyle",
            "age-appropriate clothing",
        ],
        LifeStageTeen =>
        [
            "teenage appearance",
            "teen facial proportions",
            "teen body proportions",
            "age-appropriate hairstyle",
        ],
        LifeStageYoungAdult =>
        [
            "clearly adult but youthful",
            "natural youthful adult facial proportions",
            "natural adult body proportions",
            "age-appropriate skin and facial features",
            "age-appropriate hairstyle",
            "natural adult posture",
        ],
        LifeStageAdult or LifeStageMature =>
        [
            "clearly mature adult",
            "adult appearance",
            "mature adult facial proportions",
            "natural adult body proportions",
            "age-appropriate skin and facial features",
            "age-appropriate hairstyle",
            "natural adult posture",
        ],
        LifeStageSenior =>
        [
            "senior adult appearance",
            "age-appropriate mature features",
            "natural senior proportions",
        ],
        _ => ["age-appropriate appearance"],
    };

    public static IReadOnlyList<string> AvoidOf(string lifeStage) => lifeStage switch
    {
        LifeStageInfant or LifeStageChild =>
        [
            "teenage appearance",
            "adult appearance",
            "adolescent appearance",
            "adult facial maturity",
            "adult musculature",
            "elderly features",
        ],
        LifeStageTeen =>
        [
            "young child appearance",
            "middle-aged appearance",
            "elderly appearance",
        ],
        LifeStageYoungAdult =>
        [
            "teenage appearance",
            "middle-aged appearance",
            "elderly appearance",
            "childlike appearance",
        ],
        LifeStageAdult or LifeStageMature =>
        [
            "young-adult appearance",
            "teenage appearance",
            "childlike appearance",
            "elderly appearance",
        ],
        LifeStageSenior =>
        [
            "childlike appearance",
            "teenage appearance",
            "young-adult glamorization",
        ],
        _ => ["age-inappropriate appearance"],
    };

    public static IReadOnlyList<string> CompileAgeAppearanceProfile(
        AgeExpressionTarget target, string? gender = null)
    {
        var g = (gender ?? "").Trim().ToLowerInvariant();
        var female = g is "female" or "nữ" or "nu";
        var male = g is "male" or "nam";
        var person = target.LifeStage switch
        {
            LifeStageInfant or LifeStageChild => female ? "Vietnamese girl" : male ? "Vietnamese boy" : "Vietnamese child",
            LifeStageTeen => female ? "Vietnamese teenage girl" : male ? "Vietnamese teenage boy" : "Vietnamese teenager",
            LifeStageYoungAdult => female ? "Young Vietnamese adult woman" : male ? "Young Vietnamese adult man" : "Young Vietnamese adult",
            LifeStageSenior => female ? "Vietnamese senior woman" : male ? "Vietnamese senior man" : "Vietnamese senior adult",
            _ => female ? "Vietnamese adult woman" : male ? "Vietnamese adult man" : "Vietnamese adult",
        };
        var decade = DecadePhrase(target.ChronologicalAge, target.LifeStage);
        var subject = female ? "woman" : male ? "man" : "person";
        var poss = female ? "her" : male ? "his" : "their";
        var lines = new List<string> { person };
        lines.Add($"approximately {target.TargetAppearanceAgeMin}-{target.TargetAppearanceAgeMax} years old");
        if (target.LifeStage is LifeStageInfant or LifeStageChild)
        {
            lines.Add("clearly a child");
            lines.Add($"natural facial proportions and maturity appropriate for an {target.ChronologicalAge}-year-old");
        }
        else if (target.LifeStage == LifeStageTeen)
        {
            lines.Add("clearly a teenager");
            lines.Add($"natural facial maturity appropriate for a {subject} in {poss} teens");
        }
        else if (target.LifeStage == LifeStageYoungAdult)
        {
            lines.Add("clearly adult but youthful");
            lines.Add($"natural facial maturity appropriate for a {subject} in {poss} {decade}");
        }
        else if (target.LifeStage == LifeStageSenior)
        {
            lines.Add("clearly senior adult");
            lines.Add($"natural facial maturity appropriate for a senior {subject}");
        }
        else
        {
            lines.Add("clearly mature adult");
            lines.Add($"natural facial maturity appropriate for a {subject} in {poss} {decade}");
        }
        lines.AddRange(target.Guidance);
        lines.AddRange(target.Avoid.Select(a => "no " + a));
        return lines;
    }

    public static string AgeAppearanceProfileText(AgeExpressionTarget target, string? gender = null) =>
        string.Join("; ", CompileAgeAppearanceProfile(target, gender));

    public static string AgePromptBlock(AgeExpressionTarget target, string? gender = null)
    {
        var profile = AgeAppearanceProfileText(target, gender);
        return string.Join(" ",
            $"ChronologicalAge: {target.ChronologicalAge}.",
            $"TargetAppearanceAgeMin: {target.TargetAppearanceAgeMin}.",
            $"TargetAppearanceAgeMax: {target.TargetAppearanceAgeMax}.",
            $"AgeAppearanceProfile: {profile}.");
    }

    public static string AgeExpressionBrief(AgeExpressionTarget target, string? gender = null) =>
        AgePromptBlock(target, gender);

    public static object GenerationContractAge(AgeExpressionTarget target, string? gender = null) => new
    {
        chronologicalAge = target.ChronologicalAge,
        targetAppearanceAgeMin = target.TargetAppearanceAgeMin,
        targetAppearanceAgeMax = target.TargetAppearanceAgeMax,
        lifeStage = target.LifeStage,
        appearanceProfile = CompileAgeAppearanceProfile(target, gender),
        brief = AgeAppearanceProfileText(target, gender),
    };

    public static object CompileAgeExpression(AgeExpressionTarget target, string? gender = null) => new
    {
        chronologicalAge = target.ChronologicalAge,
        targetAppearanceAgeMin = target.TargetAppearanceAgeMin,
        targetAppearanceAgeMax = target.TargetAppearanceAgeMax,
        expressionMinAge = target.TargetAppearanceAgeMin,
        expressionMaxAge = target.TargetAppearanceAgeMax,
        lifeStage = target.LifeStage,
        overrideProvenance = target.OverrideProvenance,
        ageAppearanceProfile = CompileAgeAppearanceProfile(target, gender),
        ageExpressionGuidance = target.Guidance,
        avoid = target.Avoid,
    };

    public static object AgeProfileFingerprint(AgeExpressionTarget target) => new
    {
        chronological_age = target.ChronologicalAge,
        target_appearance_age_min = target.TargetAppearanceAgeMin,
        target_appearance_age_max = target.TargetAppearanceAgeMax,
    };

    public static string AgeProfileSha(AgeExpressionTarget target) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            AgeProfileFingerprint(target), CanonicalOptions)));

    public static AgeArtifactResult EvaluateArtifact(AgeExpressionTarget target, int? apparentAge)
    {
        if (apparentAge is null)
        {
            return new AgeArtifactResult(null, StatusNotEvaluated, null,
                "Apparent age was not evaluated. Director remains the authority.");
        }

        var age = apparentAge.Value;
        var passMin = target.TargetAppearanceAgeMin;
        var passMax = target.TargetAppearanceAgeMax;
        var warnMin = passMin - Options.WarningToleranceYears;
        var warnMax = passMax + Options.WarningToleranceYears;
        if (age >= passMin && age <= passMax)
            return new AgeArtifactResult(age, StatusPass, 100, null);
        if (age >= warnMin && age <= warnMax)
        {
            return new AgeArtifactResult(age, StatusWarning, 70,
                $"Apparent age {age} is slightly outside the target range {passMin}–{passMax}.");
        }

        return new AgeArtifactResult(age, StatusFail, 0,
            $"Nhân vật {target.ChronologicalAge} tuổi nhưng hình ảnh biểu hiện khoảng {age} tuổi.");
    }

    public static AgeSetResult EvaluateSet(AgeExpressionTarget target, IReadOnlyList<AgeArtifactInput> artifacts)
    {
        var results = artifacts
            .Select(a => (a.Type, Result: EvaluateArtifact(target, a.ApparentAge)))
            .ToList();
        var statuses = results.Select(x => x.Result.Status).ToList();
        IReadOnlyList<AgeArtifactDto> dto = results
            .Select(x => new AgeArtifactDto(x.Type, x.Result.ApparentAge, x.Result.Status, x.Result.Score, x.Result.Reason))
            .ToList();
        if (statuses.Count == 0 || statuses.All(s => s == StatusNotEvaluated))
        {
            return new AgeSetResult(StatusNotEvaluated, null,
                "Age Consistency is awaiting Director review. No automatic age score was invented.", dto);
        }

        if (statuses.Any(s => s == StatusFail))
        {
            return new AgeSetResult(StatusFail, 0, "Tuổi biểu hiện không phù hợp với tuổi nhân vật.", dto);
        }

        if (statuses.Any(s => s == StatusWarning))
        {
            return new AgeSetResult(StatusWarning, 70,
                "Tuổi biểu hiện lệch nhẹ so với mục tiêu. Director quyết định.", dto);
        }

        var evaluated = results.Where(x => x.Result.Score is not null).ToList();
        var score = evaluated.Count == 0 ? (int?)null : (int)Math.Round(evaluated.Average(x => x.Result.Score!.Value));
        return new AgeSetResult(StatusPass, score, null, dto);
    }

    public static bool FaceAndAgeIndependent() => true;

    public static bool OverallPass(bool identityPass, bool stylePass, bool anglePass, string ageStatus)
    {
        if (!identityPass || !stylePass || !anglePass) return false;
        return !string.Equals(ageStatus, StatusFail, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CharacterReadyAllowed(
        bool profileReady, bool identityReady, bool masterLocked, bool dnaLocked, bool prpLocked,
        bool crpComplete, bool facePass, bool stylePass, bool agePass, bool directorApproved, bool crpLocked) =>
        profileReady && identityReady && masterLocked && dnaLocked && prpLocked
        && crpComplete && facePass && stylePass && agePass && directorApproved && crpLocked;

    public static bool MayRegenerateAfterAgeMismatch(string? rejectReasonCode, bool officialLocked) =>
        !officialLocked
        && string.Equals(rejectReasonCode, RejectAgeMismatch, StringComparison.OrdinalIgnoreCase);

    public static bool LockedBlocksRegeneration(bool officialLocked, bool characterReady) =>
        officialLocked && characterReady;

    public static bool UsesCharacterSpecificBranch() => false;
    public static bool InventsApparentAge() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool GeneratesVideo() => false;
    public static bool GeneratesProduction() => false;
    public static bool CallsGeminiForValidation() => false;

    public static string NormalizeReviewResult(string? raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        return t is StatusPass or StatusFail ? t : "";
    }

    public static string? ReviewGate(string? result, int coverage, bool officialLocked)
    {
        if (officialLocked) return "AUTHORITY_LOCKED";
        if (!MayDirectorReview(coverage, officialLocked)) return GateReviewNotReady;
        return NormalizeReviewResult(result).Length == 0 ? "AGE_REVIEW_INVALID" : null;
    }

    public static string PublicAgeReason(AgeSetResult set, AgeExpressionTarget target)
    {
        if (set.Status == StatusFail)
        {
            var failed = set.Artifacts.FirstOrDefault(a => a.Status == StatusFail);
            return failed?.Reason ?? set.Reason ?? "Tuổi biểu hiện không phù hợp.";
        }

        return $"Tuổi nhân vật: {target.ChronologicalAge} tuổi. Tuổi biểu hiện mục tiêu: {target.TargetAppearanceAgeMin}–{target.TargetAppearanceAgeMax} tuổi.";
    }

    public static string DecadePhrase(int chronologicalAge, string? lifeStage = null) => chronologicalAge switch
    {
        >= 18 and <= 24 => "early twenties",
        >= 25 and <= 29 => "late twenties",
        >= 30 and <= 34 => "early thirties",
        >= 35 and <= 39 => "late thirties",
        >= 40 and <= 44 => "early forties",
        >= 45 and <= 49 => "late forties",
        >= 50 and <= 59 => "fifties",
        >= 60 and <= 69 => "sixties",
        _ => string.IsNullOrWhiteSpace(lifeStage) ? "stated age" : lifeStage.Trim().ToLowerInvariant().Replace('_', ' '),
    };

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed record AgePolicyBand(int MinAge, int MaxAge, int ToleranceYears, string LifeStage);

public sealed record CharacterAgeConsistencyOptions
{
    public int WarningToleranceYears { get; init; } = CharacterAgeConsistencyV1Rules.WarningToleranceYears;
    public IReadOnlyList<AgePolicyBand> Bands { get; init; } = CharacterAgeConsistencyV1Rules.PolicyBands;
}

public sealed record AgeExpressionTarget(
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string LifeStage,
    string? OverrideProvenance,
    IReadOnlyList<string> Guidance,
    IReadOnlyList<string> Avoid)
{
    public int CanonicalAge => ChronologicalAge;
    public int ExpressionMinAge => TargetAppearanceAgeMin;
    public int ExpressionMaxAge => TargetAppearanceAgeMax;
}

public sealed record AgeArtifactInput(string Type, int? ApparentAge);

public sealed record AgeArtifactResult(int? ApparentAge, string Status, int? Score, string? Reason);

public sealed record AgeArtifactDto(string Type, int? ApparentAge, string Status, int? Score, string? Reason);

public sealed record AgeSetResult(
    string Status,
    int? Score,
    string? Reason,
    IReadOnlyList<AgeArtifactDto> Artifacts);

public sealed record CharacterStudioAgeDto(
    int? ChronologicalAge,
    int? TargetAppearanceAgeMin,
    int? TargetAppearanceAgeMax,
    string ConsistencyStatus,
    string? LifeStage = null,
    string? AppearanceProfile = null,
    bool? Pass = null,
    int? Score = null,
    int? EvaluatedAgeMin = null,
    int? EvaluatedAgeMax = null,
    string? Reason = null,
    string? Evaluator = null,
    DateTimeOffset? EvaluatedAt = null,
    string? GateCode = null);

public sealed record CharacterAgeConsistencyReviewRequestDto(
    string? Result = null,
    string? ReasonCode = null,
    string? Note = null);

public sealed record CharacterAgeConsistencyDto(
    string DocumentId,
    string CharacterId,
    int CanonicalAge,
    int ExpressionMinAge,
    int ExpressionMaxAge,
    string LifeStage,
    string Status,
    int? Score,
    string? Reason,
    IReadOnlyList<AgeArtifactDto> Artifacts,
    int? ChronologicalAge = null,
    int? TargetAppearanceAgeMin = null,
    int? TargetAppearanceAgeMax = null,
    string? AppearanceProfile = null,
    bool? Pass = null,
    int? EvaluatedAgeMin = null,
    int? EvaluatedAgeMax = null,
    string? Evaluator = null,
    DateTimeOffset? EvaluatedAt = null,
    string? GateCode = null);
