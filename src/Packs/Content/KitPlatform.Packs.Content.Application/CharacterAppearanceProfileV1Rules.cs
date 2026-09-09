using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1 — shared Appearance Compiler.
/// Role ≠ age. Role ≠ lifestyle. Project Visual Style ≠ character aesthetic.
/// Does not branch on character name. Does not call Gemini or rewrite locked authority.
/// </summary>
public static class CharacterAppearanceProfileV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1_REGRESSION";
    public const string PolicyId = "FAMIXA_CHARACTER_APPEARANCE_POLICY_V1";

    public const string StatusReady = "READY";
    public const string StatusNotReady = "NOT_READY";
    public const string GateNotReady = "APPEARANCE_PROFILE_NOT_READY";
    public const string GateInvalid = "APPEARANCE_PROFILE_INVALID";
    public const string GateShaMissing = "APPEARANCE_PROFILE_SHA_MISSING";

    public const string DefaultLifestyle =
        "Contemporary Vietnamese everyday lifestyle. Natural modern social context. Approachable and believable. No unnecessary socioeconomic stereotype.";
    public const string DefaultSocial =
        "modern everyday social context";
    public const string RoleIsNarrative =
        "Role is a narrative function only. It does not set age, lifestyle, grooming, or clothing.";

    public static readonly string[] StereotypeTokens =
    [
        "elderly", "senior", "grandmother", "grandfather", "retirement",
        "farmer", "rural elderly", "nông dân", "nông thôn", "ông già",
        "frail", "traditional old", "aged facial", "55-65", "55–65",
    ];

    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>Compiler input. Character name is intentionally absent.</summary>
    public sealed record AppearanceSource(
        int ChronologicalAge,
        string? Gender,
        string? Role,
        string? Personality,
        string? Description,
        string? ExtraDescription,
        string? ProjectVisualStyleSha);

    public static CharacterAppearanceProfile Compile(AppearanceSource source)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(source.ChronologicalAge);
        var ageText = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age, source.Gender);
        var gender = NormalizeGender(source.Gender);
        var role = NormalizeRole(source.Role);
        var maturity = CompileFacialMaturity(age, gender);
        var lifestyle = CompileLifestyle(source);
        var energy = CompileEnergy(age.LifeStage);
        var grooming = CompileGrooming(age.LifeStage);
        var clothing = CompileClothing(age.LifeStage);
        var body = CompileBody(age.LifeStage);
        var positives = CompilePositives(age, gender, maturity, lifestyle, energy, grooming, clothing, body);
        var negatives = CompileNegatives(age);
        var agePolicySha = CharacterAgeConsistencyV1Rules.AgeProfileSha(age);
        var sourceSha = SourceSha(source);
        var status = CharacterAgeConsistencyV1Rules.ValidateProfile(
            age.ChronologicalAge, age.TargetAppearanceAgeMin, age.TargetAppearanceAgeMax) is null
            && !string.IsNullOrWhiteSpace(ageText)
            ? StatusReady
            : StatusNotReady;
        var draft = new CharacterAppearanceProfile(
            age.ChronologicalAge,
            age.TargetAppearanceAgeMin,
            age.TargetAppearanceAgeMax,
            ageText,
            gender,
            role,
            lifestyle,
            grooming,
            energy,
            clothing,
            maturity,
            body,
            DefaultSocial,
            positives,
            negatives,
            sourceSha,
            agePolicySha,
            source.ProjectVisualStyleSha,
            "",
            status);
        return draft with { ProfileSha = ProfileSha(draft) };
    }

    public static string? Validate(CharacterAppearanceProfile? profile)
    {
        if (profile is null)
            return GateNotReady;
        if (string.IsNullOrWhiteSpace(profile.ProfileSha))
            return GateShaMissing;
        if (string.IsNullOrWhiteSpace(profile.AgeAppearanceProfile)
            || string.IsNullOrWhiteSpace(profile.FacialMaturityProfile)
            || string.IsNullOrWhiteSpace(profile.LifestyleProfile))
            return GateNotReady;
        if (CharacterAgeConsistencyV1Rules.ValidateProfile(
            profile.ChronologicalAge, profile.TargetAppearanceAgeMin, profile.TargetAppearanceAgeMax) is { } age)
            return age;
        if (!string.Equals(profile.ProfileSha, ProfileSha(profile with { ProfileSha = "" }), StringComparison.OrdinalIgnoreCase))
            return GateInvalid;
        return null;
    }

    public static bool IsReady(CharacterAppearanceProfile? profile) => Validate(profile) is null;

    public static bool MayRebuild(bool officialLocked) => !officialLocked;

    public static bool RoleSetsAge() => false;
    public static bool RoleSetsLifestyle() => false;
    public static bool StyleSetsAppearance() => false;
    public static bool UsesCharacterSpecificBranch() => false;
    public static bool UsesCharacterName() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRegenerate() => false;
    public static bool CallsGemini() => false;
    public static bool GeneratesVideo() => false;
    public static bool ChangesProjectVisualStyle() => false;
    public static bool RegeneratesExistingCharacters() => false;

    public static string ProfileSha(CharacterAppearanceProfile profile)
    {
        var payload = new
        {
            document = DocumentId,
            policy = PolicyId,
            chronologicalAge = profile.ChronologicalAge,
            targetAppearanceAgeMin = profile.TargetAppearanceAgeMin,
            targetAppearanceAgeMax = profile.TargetAppearanceAgeMax,
            ageAppearanceProfile = profile.AgeAppearanceProfile,
            gender = profile.Gender,
            role = profile.Role,
            lifestyleProfile = profile.LifestyleProfile,
            groomingProfile = profile.GroomingProfile,
            energyProfile = profile.EnergyProfile,
            clothingProfile = profile.ClothingProfile,
            facialMaturityProfile = profile.FacialMaturityProfile,
            bodyPresenceProfile = profile.BodyPresenceProfile,
            socialVisualContext = profile.SocialVisualContext,
            positives = profile.PositiveAppearanceConstraints,
            negatives = profile.NegativeAppearanceConstraints,
            sourceCharacterProfileSha = profile.SourceCharacterProfileSha,
            sourceAgePolicySha = profile.SourceAgePolicySha,
            sourceProjectVisualStyleSha = profile.SourceProjectVisualStyleSha,
            status = profile.Status,
        };
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Canonical)));
    }

    public static string SourceSha(AppearanceSource source)
    {
        var payload = new
        {
            chronologicalAge = source.ChronologicalAge,
            gender = NormalizeGender(source.Gender),
            role = NormalizeRole(source.Role),
            personality = (source.Personality ?? "").Trim().ToLowerInvariant(),
            description = StripStereotype((source.Description ?? "").Trim()),
            extra = StripStereotype((source.ExtraDescription ?? "").Trim()),
        };
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Canonical)));
    }

    public static string PromptBlock(CharacterAppearanceProfile profile) =>
        string.Join(" ",
            $"FacialMaturityProfile: {profile.FacialMaturityProfile}.",
            $"LifestyleProfile: {profile.LifestyleProfile}.",
            $"EnergyProfile: {profile.EnergyProfile}.",
            $"GroomingProfile: {profile.GroomingProfile}.",
            $"ClothingProfile: {profile.ClothingProfile}.",
            $"BodyPresenceProfile: {profile.BodyPresenceProfile}.",
            $"SocialVisualContext: {profile.SocialVisualContext}.",
            RoleIsNarrative,
            string.IsNullOrWhiteSpace(profile.Role) ? "" : $"NarrativeRole: {profile.Role}.",
            "PositiveAppearanceConstraints: " + string.Join("; ", profile.PositiveAppearanceConstraints) + ".",
            "NegativeAppearanceConstraints: " + string.Join("; ", profile.NegativeAppearanceConstraints) + ".",
            $"AppearanceProfileSha: {profile.ProfileSha}.");

    public static string PublicFacialMaturity(int chronologicalAge)
    {
        var stage = CharacterAgeConsistencyV1Rules.LifeStageOf(chronologicalAge);
        return stage switch
        {
            CharacterAgeConsistencyV1Rules.LifeStageInfant
                or CharacterAgeConsistencyV1Rules.LifeStageChild => "Child",
            CharacterAgeConsistencyV1Rules.LifeStageTeen => "Teen",
            CharacterAgeConsistencyV1Rules.LifeStageYoungAdult => "Youthful adult",
            CharacterAgeConsistencyV1Rules.LifeStageSenior => "Older adult",
            _ => "Adult / youthful mature",
        };
    }

    public static bool PromptContainsAppearance(string? prompt, CharacterAppearanceProfile profile) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains("FacialMaturityProfile:", StringComparison.Ordinal)
        && prompt.Contains(profile.FacialMaturityProfile, StringComparison.Ordinal)
        && prompt.Contains("LifestyleProfile:", StringComparison.Ordinal)
        && prompt.Contains(profile.LifestyleProfile, StringComparison.Ordinal)
        && prompt.Contains(profile.ProfileSha, StringComparison.OrdinalIgnoreCase);

    public static bool ViewsShareAppearance(
        IReadOnlyDictionary<string, string> viewPrompts, CharacterAppearanceProfile profile) =>
        viewPrompts.Count > 0 && viewPrompts.Values.All(p => PromptContainsAppearance(p, profile));

    public static bool StyleBlockIndependent(string? stylePrompt, CharacterAppearanceProfile profile) =>
        string.IsNullOrWhiteSpace(stylePrompt)
        || (!stylePrompt.Contains(profile.LifestyleProfile, StringComparison.Ordinal)
            && !stylePrompt.Contains("FacialMaturityProfile:", StringComparison.Ordinal));

    public static bool DescriptionOverridesAge(AppearanceSource source, CharacterAppearanceProfile profile)
    {
        var prose = ((source.Description ?? "") + " " + (source.Role ?? "")).ToLowerInvariant();
        if (prose.Contains("ông") || prose.Contains("elderly") || prose.Contains("bố") || prose.Contains("father"))
            return profile.TargetAppearanceAgeMax <= 49
                && profile.NegativeAppearanceConstraints.Any(n =>
                    n.Contains("elderly", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static string CompileFacialMaturity(AgeExpressionTarget age, string gender)
    {
        var range = $"{age.TargetAppearanceAgeMin}–{age.TargetAppearanceAgeMax}";
        return age.LifeStage switch
        {
            CharacterAgeConsistencyV1Rules.LifeStageInfant
                or CharacterAgeConsistencyV1Rules.LifeStageChild =>
                $"child facial structure; clearly a child; natural child facial maturity; approximately {range}",
            CharacterAgeConsistencyV1Rules.LifeStageTeen =>
                $"teenage facial structure; clearly a teenager; approximately {range}",
            CharacterAgeConsistencyV1Rules.LifeStageYoungAdult =>
                $"youthful adult facial structure; clearly adult; not a teenager; healthy skin; natural facial maturity; approximately {range}",
            CharacterAgeConsistencyV1Rules.LifeStageSenior =>
                $"older adult facial structure; age-appropriate senior maturity; approximately {range}",
            CharacterAgeConsistencyV1Rules.LifeStageMature =>
                $"older adult facial structure; clearly mature adult; natural facial maturity; approximately {range}",
            _ =>
                $"clearly adult; mature adult facial structure; youthful-to-middle adult appearance; healthy skin; natural facial maturity; approximately {range}",
        };
    }

    private static string CompileLifestyle(AppearanceSource source)
    {
        var prose = ((source.Description ?? "") + " " + (source.ExtraDescription ?? "")).Trim();
        if (ContainsStereotype(prose) || string.IsNullOrWhiteSpace(prose))
            return DefaultLifestyle;
        return DefaultLifestyle;
    }

    private static string CompileEnergy(string lifeStage) => lifeStage switch
    {
        CharacterAgeConsistencyV1Rules.LifeStageInfant
            or CharacterAgeConsistencyV1Rules.LifeStageChild => "curious, energetic, approachable",
        CharacterAgeConsistencyV1Rules.LifeStageTeen => "alert, approachable, natural",
        CharacterAgeConsistencyV1Rules.LifeStageSenior
            or CharacterAgeConsistencyV1Rules.LifeStageMature => "calm, dignified, approachable",
        _ => "warm, active, approachable, alert",
    };

    private static string CompileGrooming(string lifeStage) => lifeStage switch
    {
        CharacterAgeConsistencyV1Rules.LifeStageInfant
            or CharacterAgeConsistencyV1Rules.LifeStageChild => "neat, healthy, age-appropriate child grooming",
        CharacterAgeConsistencyV1Rules.LifeStageSenior => "neat, healthy, age-appropriate older-adult grooming",
        _ => "neat, healthy, contemporary adult grooming",
    };

    private static string CompileClothing(string lifeStage) => lifeStage switch
    {
        CharacterAgeConsistencyV1Rules.LifeStageInfant
            or CharacterAgeConsistencyV1Rules.LifeStageChild => "simple contemporary children's clothing",
        CharacterAgeConsistencyV1Rules.LifeStageTeen => "simple contemporary teen clothing",
        CharacterAgeConsistencyV1Rules.LifeStageSenior => "simple contemporary older-adult clothing",
        _ => "simple modern casual adult clothing",
    };

    private static string CompileBody(string lifeStage) => lifeStage switch
    {
        CharacterAgeConsistencyV1Rules.LifeStageInfant
            or CharacterAgeConsistencyV1Rules.LifeStageChild => "healthy child body presence; natural child posture",
        CharacterAgeConsistencyV1Rules.LifeStageSenior => "healthy older-adult body presence; natural posture",
        _ => "healthy natural adult body presence; alert posture; not frail",
    };

    private static IReadOnlyList<string> CompilePositives(
        AgeExpressionTarget age, string gender, string maturity, string lifestyle,
        string energy, string grooming, string clothing, string body)
    {
        var person = gender switch
        {
            "female" => age.LifeStage is CharacterAgeConsistencyV1Rules.LifeStageInfant
                or CharacterAgeConsistencyV1Rules.LifeStageChild ? "Vietnamese girl"
                : age.LifeStage == CharacterAgeConsistencyV1Rules.LifeStageYoungAdult ? "contemporary Vietnamese adult woman"
                : "contemporary Vietnamese adult woman",
            "male" => age.LifeStage is CharacterAgeConsistencyV1Rules.LifeStageInfant
                or CharacterAgeConsistencyV1Rules.LifeStageChild ? "Vietnamese boy"
                : age.LifeStage == CharacterAgeConsistencyV1Rules.LifeStageYoungAdult ? "contemporary Vietnamese adult man"
                : "contemporary Vietnamese adult man",
            _ => "contemporary Vietnamese person",
        };
        if (age.LifeStage is CharacterAgeConsistencyV1Rules.LifeStageInfant
            or CharacterAgeConsistencyV1Rules.LifeStageChild)
            person = gender == "female" ? "Vietnamese girl" : gender == "male" ? "Vietnamese boy" : "Vietnamese child";
        else if (age.LifeStage == CharacterAgeConsistencyV1Rules.LifeStageTeen)
            person = gender == "female" ? "Vietnamese teenage girl" : gender == "male" ? "Vietnamese teenage boy" : "Vietnamese teenager";
        else if (age.LifeStage is CharacterAgeConsistencyV1Rules.LifeStageMature
            or CharacterAgeConsistencyV1Rules.LifeStageSenior)
            person = gender == "female" ? "contemporary Vietnamese older woman" : gender == "male" ? "contemporary Vietnamese older man" : "contemporary Vietnamese older adult";
        return
        [
            person,
            $"approximately {age.TargetAppearanceAgeMin}–{age.TargetAppearanceAgeMax} years old",
            maturity,
            lifestyle,
            energy,
            grooming,
            clothing,
            body,
            DefaultSocial,
            "believable Vietnamese appearance",
            "age-appropriate grooming and clothing",
            "approachable expression",
        ];
    }

    private static IReadOnlyList<string> CompileNegatives(AgeExpressionTarget age)
    {
        var avoid = age.Avoid.ToList();
        if (age.LifeStage is CharacterAgeConsistencyV1Rules.LifeStageYoungAdult
            or CharacterAgeConsistencyV1Rules.LifeStageAdult
            && age.TargetAppearanceAgeMax < 50)
        {
            avoid.AddRange(
            [
                "elderly appearance",
                "senior facial structure",
                "deep age wrinkles",
                "strongly aged skin",
                "frail elderly appearance",
                "retirement-age appearance",
                "55–65 appearance",
                "strongly gray hair",
                "frail posture",
                "elderly body language",
                "rural elderly stereotype",
                "farmer stereotype",
                "traditional elderly clothing",
                "aged facial structure",
            ]);
        }

        if (age.LifeStage == CharacterAgeConsistencyV1Rules.LifeStageYoungAdult)
        {
            avoid.Add("teenager");
            avoid.Add("middle-aged appearance");
        }

        return avoid.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeGender(string? raw)
    {
        var g = (raw ?? "").Trim().ToLowerInvariant();
        if (g is "female" or "nữ" or "nu" or "woman" or "girl") return "female";
        if (g is "male" or "nam" or "man" or "boy") return "male";
        return g;
    }

    private static string NormalizeRole(string? raw)
    {
        var t = (raw ?? "").Trim();
        return t.Length == 0 ? "" : t;
    }

    private static bool ContainsStereotype(string text)
    {
        var t = (text ?? "").ToLowerInvariant();
        return StereotypeTokens.Any(token => t.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string StripStereotype(string text)
    {
        var t = text ?? "";
        foreach (var token in StereotypeTokens)
            t = t.Replace(token, "", StringComparison.OrdinalIgnoreCase);
        return string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed record CharacterAppearanceProfile(
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string AgeAppearanceProfile,
    string Gender,
    string Role,
    string LifestyleProfile,
    string GroomingProfile,
    string EnergyProfile,
    string ClothingProfile,
    string FacialMaturityProfile,
    string BodyPresenceProfile,
    string SocialVisualContext,
    IReadOnlyList<string> PositiveAppearanceConstraints,
    IReadOnlyList<string> NegativeAppearanceConstraints,
    string SourceCharacterProfileSha,
    string SourceAgePolicySha,
    string? SourceProjectVisualStyleSha,
    string ProfileSha,
    string Status);

public sealed record CharacterStudioAppearanceDto(
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string FacialMaturityLabel,
    string LifestyleProfile,
    string EnergyProfile,
    string GroomingProfile,
    string ConsistencyStatus,
    string? ProfileSha = null,
    string? Role = null,
    string? ClothingProfile = null,
    string? GateCode = null);
