using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1 — shared character-construction language.
/// Answers how a Famixa character is designed. Does not decide who they are,
/// chronological age, role, story, or Project Visual Style. Does not branch
/// on character name. Does not call Gemini, generate pixels, mutate Master /
/// DNA / PRP / CRP, or change PVS authority.
/// </summary>
public static class CharacterDesignLanguageV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1";
    public const string SuiteId = "FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1_REGRESSION";
    public const string Version = "V1";
    public const string Name = "Famixa Character Design Language V1";
    public const string Status = "DRAFT";

    public const string StylizationLevel = "STRONG";
    public const string PhotorealismLevel = "LOW";

    public const string BandChild = "CHILD_STRONGER";
    public const string BandTeen = "TEEN_MODERATE";
    public const string BandYoungAdult = "YOUNG_ADULT_MODERATE";
    public const string BandAdult = "ADULT_MODERATE";
    public const string BandOlder = "OLDER_STYLIZED_MATURE";

    public const string CorePrinciple =
        "Stylized 3D characters first, realistic humans second. "
        + "Design a recognizable 3D character using one Famixa character-design language "
        + "while preserving believable age, gender presentation, anatomy, and identity. "
        + "Do not make a realistic human and then render it in 3D.";

    public const string FaceLanguage =
        "Simplified 3D facial planes, softened anatomical transitions, clean silhouette, "
        + "controlled facial proportions, expressive but believable eyes, "
        + "slightly stylized eye-to-face relationship, soft cheeks where age-appropriate, "
        + "clean jaw and cheek structure, non-photographic facial geometry. "
        + "Subtle asymmetry and natural human variation are allowed. "
        + "Not a live-action actor, photographic portrait, hyperreal human scan, "
        + "or uncanny synthetic face.";

    public const string EyeLanguage =
        "Expressive, clear, warm, slightly stylized eyes with a slightly larger-than-photoreal "
        + "eye-to-face relationship, clean iris design, controlled highlights, "
        + "soft eyelid construction, natural placement, coherent across ages. "
        + "Children may have more pronounced eye stylization. "
        + "Adults keep the same eye language with age-appropriate proportions. "
        + "Adult eyes are not photorealistic and adults must not look like children.";

    public const string SkinLanguage =
        "Stylized 3D skin: smooth controlled surface, subtle subsurface, soft material response, "
        + "believable color variation, clean 3D shading, simplified micro-detail. "
        + "Not photographic pores, hyperreal texture, beauty-photography retouching, "
        + "live-action skin, excessive specular realism, or skin-scan artifacts.";

    public const string HairLanguage =
        "Coherent 3D hair masses: clear primary shape, controlled volume, stylized strand grouping, "
        + "clean silhouette, age-appropriate hairstyle, natural movement. "
        + "Not photographic individual strands, hyperreal hair, random strand noise, "
        + "or overly detailed hair simulation.";

    public const string BodyLanguage =
        "Believable, natural, age-appropriate, gender-appropriate anatomy with subtle stylization "
        + "and coherent views. Children may have a slightly larger head impression, shorter limbs, "
        + "and a softer body. Adults have believable adult proportions and natural "
        + "shoulder/torso/limb relationships. Not extreme cartoon, superhero exaggeration, "
        + "photographic body rendering, or childlike adult proportions.";

    public const string ExpressionLanguage =
        "Default expression is calm, approachable, warm, naturally expressive, "
        + "with a subtle smile or a neutral-friendly face. "
        + "Personality may modify expression but must not override the design language. "
        + "Not an exaggerated cartoon face, frozen photographic expression, uncanny smile, "
        + "or excessive teeth.";

    public const string GenderLanguage =
        "Gender presentation stays natural and age-appropriate. "
        + "Do not force one face shape, hairstyle family, or body type for all males or females. "
        + "Character-specific identity plus age profile plus gender presentation "
        + "plus this shared design language produce the final character.";

    public const string UniverseLanguage =
        "Different people, same character universe. Shared facial construction, eye language, "
        + "facial-plane treatment, skin, hair construction, material language, anatomical stylization, "
        + "softness, expressive quality, and 3D character feeling. "
        + "Do not copy one character onto another. Do not make every face identical.";

    public const string LifestyleBoundary =
        "Default lifestyle is contemporary Vietnamese. Lifestyle may influence clothing and grooming "
        + "but must not override Character Design Language. "
        + "Do not infer rural equals elderly, father equals old, teacher equals realistic, "
        + "or mother equals middle-aged.";

    public const string RoleBoundary =
        "Role is narrative context only. Role does not set face, age appearance, visual style, "
        + "clothing style, body type, or generation identity.";

    public const string PhotorealismBoundary =
        "Suppress photorealistic human appearance. "
        + "Do not overcorrect into flat cartoon, 2D illustration, anime, toy plastic, "
        + "childish cartoon, exaggerated Pixar imitation, or generic mascot. "
        + "Target: stylized 3D character with believable human anatomy and age.";

    public const string PositivePromptBlock =
        "stylized 3D character, coherent character-design language, softened facial geometry, "
        + "expressive eyes, simplified facial planes, smooth stylized skin, designed hair masses, "
        + "believable anatomy, age-appropriate proportions, warm approachable expression, "
        + "polished 3D character rendering, unified character universe, consistent stylization, "
        + "controlled realism, non-photographic character design";

    public const string NegativePromptBlock =
        "photorealistic human, live-action actor, photographic portrait, realistic human photograph, "
        + "hyperreal human, hyperreal skin, skin pores, photographic wrinkles, human scan, "
        + "DSLR portrait, editorial portrait, documentary photography, cinematic live-action actor, "
        + "realistic celebrity portrait, AI photorealistic portrait, "
        + "flat cartoon, 2D illustration, anime, toy plastic, childish cartoon, "
        + "exaggerated Pixar imitation, generic mascot";

    public static readonly string[] ForbiddenNames =
        ["Minh", "Nam", "Linh", "An", "Thảo", "Thao"];

    public static readonly string[] PhotorealTokens =
    [
        "photorealistic human", "live-action actor", "photographic portrait",
        "hyperreal human", "skin pores", "human scan", "DSLR portrait",
    ];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRegenerate() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool Persists() => false;
    public static bool MutatesMaster() => false;
    public static bool MutatesDna() => false;
    public static bool MutatesPrp() => false;
    public static bool MutatesCrp() => false;
    public static bool ChangesProjectVisualStyle() => false;
    public static bool UsesCharacterName() => false;
    public static bool UsesCharacterSpecificBranch() => false;
    public static bool RoleSetsDesign() => false;
    public static bool ReplacesIdentity() => false;
    public static bool ReplacesAgePolicy() => false;
    public static bool ReplacesProjectVisualStyle() => false;
    public static bool ReplacesMasterAuthority() => false;

    public static string Canonical() => string.Join('\n', new[]
    {
        "CHARACTER_DESIGN_LANGUAGE_V1",
        "Name=" + Name,
        "Version=" + Version,
        "Status=" + Status,
        "CorePrinciple=" + CorePrinciple,
        "FaceLanguage=" + FaceLanguage,
        "EyeLanguage=" + EyeLanguage,
        "SkinLanguage=" + SkinLanguage,
        "HairLanguage=" + HairLanguage,
        "BodyLanguage=" + BodyLanguage,
        "ExpressionLanguage=" + ExpressionLanguage,
        "GenderLanguage=" + GenderLanguage,
        "UniverseLanguage=" + UniverseLanguage,
        "LifestyleBoundary=" + LifestyleBoundary,
        "RoleBoundary=" + RoleBoundary,
        "PhotorealismBoundary=" + PhotorealismBoundary,
        "PositivePromptBlock=" + PositivePromptBlock,
        "NegativePromptBlock=" + NegativePromptBlock,
        "StylizationLevel=" + StylizationLevel,
        "PhotorealismLevel=" + PhotorealismLevel,
        "AgeOwnedByCdl=false",
        "RoleOwnedByCdl=false",
        "CharacterNameOwnedByCdl=false",
        "PvsOwnedByCdl=false",
        "MasterOwnedByCdl=false",
    });

    public static string Sha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(Canonical()));

    public static string? Validate()
    {
        if (string.IsNullOrWhiteSpace(CorePrinciple)
            || string.IsNullOrWhiteSpace(FaceLanguage)
            || string.IsNullOrWhiteSpace(EyeLanguage)
            || string.IsNullOrWhiteSpace(SkinLanguage)
            || string.IsNullOrWhiteSpace(HairLanguage)
            || string.IsNullOrWhiteSpace(BodyLanguage)
            || string.IsNullOrWhiteSpace(ExpressionLanguage)
            || string.IsNullOrWhiteSpace(PositivePromptBlock)
            || string.IsNullOrWhiteSpace(NegativePromptBlock))
            return "CHARACTER_DESIGN_LANGUAGE_INVALID";
        if (!ProjectVisualStyleV1Rules.LookLikeSha(Sha()))
            return "CHARACTER_DESIGN_LANGUAGE_SHA_INVALID";
        if (ContainsCharacterName(Canonical()) || ContainsCharacterName(PositivePromptBlock))
            return "CHARACTER_DESIGN_LANGUAGE_HAS_CHARACTER_NAME";
        return null;
    }

    public static bool IsValid() => Validate() is null;

    public static string StylizationBandOf(string? lifeStage) => (lifeStage ?? "").Trim().ToUpperInvariant() switch
    {
        CharacterAgeConsistencyV1Rules.LifeStageInfant
            or CharacterAgeConsistencyV1Rules.LifeStageChild => BandChild,
        CharacterAgeConsistencyV1Rules.LifeStageTeen => BandTeen,
        CharacterAgeConsistencyV1Rules.LifeStageYoungAdult => BandYoungAdult,
        CharacterAgeConsistencyV1Rules.LifeStageAdult => BandAdult,
        _ => BandOlder,
    };

    public static string AgeAdaptiveBlock(AgeExpressionTarget age)
    {
        var band = StylizationBandOf(age.LifeStage);
        var shared =
            $"Age-adaptive stylization for chronological age {age.ChronologicalAge}, "
            + $"target appearance {age.TargetAppearanceAgeMin}–{age.TargetAppearanceAgeMax}. "
            + "Child is more stylized. Adult is less exaggerated but still clearly stylized. "
            + "Child is not a cartoon. Adult is not photorealistic.";
        var specific = band switch
        {
            BandChild =>
                "Stronger stylization, softer facial geometry, relatively larger eyes, "
                + "slightly larger head-to-body impression, youthful facial structure, "
                + "softer cheeks, simplified facial planes.",
            BandTeen =>
                "Moderate stylization, transitional proportions, youthful facial structure, "
                + "less childlike exaggeration.",
            BandYoungAdult =>
                "Moderate stylization, believable adult anatomy, expressive eyes, "
                + "softened facial planes, stylized but mature face.",
            BandAdult =>
                "Moderate stylization, mature facial proportions, believable age markers, "
                + "controlled stylization, avoid photographic realism.",
            _ =>
                "Stylized mature anatomy. Age characteristics are preserved. "
                + "Wrinkles and age markers are simplified and stylized. "
                + "Must not become a photorealistic elderly portrait.",
        };
        return string.Join(" ", shared, specific);
    }

    public static CharacterDesignLanguageCompile Compile(
        int chronologicalAge,
        string? gender = null,
        string? ageAppearanceProfile = null)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(chronologicalAge);
        var appearance = string.IsNullOrWhiteSpace(ageAppearanceProfile)
            ? CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age, gender)
            : ageAppearanceProfile.Trim();
        var adaptive = AgeAdaptiveBlock(age);
        var positive = string.Join(" ",
            CorePrinciple,
            PositivePromptBlock,
            "Face: " + FaceLanguage,
            "Eyes: " + EyeLanguage,
            "Skin: " + SkinLanguage,
            "Hair: " + HairLanguage,
            "Body: " + BodyLanguage,
            "Expression: " + ExpressionLanguage,
            adaptive);
        var combined = ComposeWithAgeAppearance(positive, appearance);
        return new CharacterDesignLanguageCompile(
            Sha(),
            age.ChronologicalAge,
            age.TargetAppearanceAgeMin,
            age.TargetAppearanceAgeMax,
            age.LifeStage,
            StylizationBandOf(age.LifeStage),
            adaptive,
            positive,
            NegativePromptBlock + ". " + PhotorealismBoundary,
            combined);
    }

    public static string ComposeWithAgeAppearance(string designLanguageBlock, string ageAppearanceProfile) =>
        string.Join(" ", new[]
        {
            "Character Design Language:",
            (designLanguageBlock ?? "").Trim(),
            "Age Appearance Profile:",
            (ageAppearanceProfile ?? "").Trim(),
            RoleBoundary,
            LifestyleBoundary,
            "Character Design Language does not replace identity, age policy, Project Visual Style, or Master.",
        }.Where(x => x.Length > 0));

    public static string ComposeGenerationPrompt(
        string projectVisualStyle,
        string designLanguage,
        string identity,
        string ageAppearance,
        string masterConstraints,
        string view,
        string negatives)
    {
        return string.Join(" ", new[]
        {
            (projectVisualStyle ?? "").Trim(),
            (designLanguage ?? "").Trim(),
            (identity ?? "").Trim(),
            (ageAppearance ?? "").Trim(),
            (masterConstraints ?? "").Trim(),
            (view ?? "").Trim(),
            (negatives ?? "").Trim(),
        }.Where(x => x.Length > 0));
    }

    public static bool ContainsCharacterName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text
            .Replace("CharacterNameOwnedByCdl", "", StringComparison.OrdinalIgnoreCase)
            .Replace("character name", "", StringComparison.OrdinalIgnoreCase);
        return Regex.IsMatch(normalized, @"\b(Minh|Nam|Linh|Thảo|Thao)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool ContainsRoleBranch(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && (text.Contains("if role", StringComparison.OrdinalIgnoreCase)
            || text.Contains("role ==", StringComparison.OrdinalIgnoreCase)
            || text.Contains("make older", StringComparison.OrdinalIgnoreCase)
            || text.Contains("make rural", StringComparison.OrdinalIgnoreCase));

    public static bool HasPhotorealismSuppression(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && PhotorealTokens.Count(t => text.Contains(t, StringComparison.OrdinalIgnoreCase)) >= 3;

    public static bool HasStylizedLanguage(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains("stylized 3D", StringComparison.OrdinalIgnoreCase)
        && text.Contains("facial planes", StringComparison.OrdinalIgnoreCase);

    public static bool SameDefinition(CharacterDesignLanguageCompile a, CharacterDesignLanguageCompile b) =>
        string.Equals(a.DefinitionSha, b.DefinitionSha, StringComparison.OrdinalIgnoreCase);

    public static CharacterDesignLanguageDefinitionDto ToDefinitionDto() =>
        new(
            DocumentId,
            Version,
            Name,
            Status,
            CorePrinciple,
            FaceLanguage,
            EyeLanguage,
            SkinLanguage,
            HairLanguage,
            BodyLanguage,
            ExpressionLanguage,
            GenderLanguage,
            AgeAdaptiveStylization: true,
            CharacterSpecificBranching: false,
            PositivePromptBlock,
            NegativePromptBlock,
            StylizationLevel,
            PhotorealismLevel,
            Sha(),
            Canonical(),
            ProviderCalled: false,
            GenerationExecuted: false,
            GeminiCalled: false,
            AutoApprove: false,
            AutoLock: false,
            Persisted: false);

    public static CharacterDesignLanguageCompileDto ToCompileDto(CharacterDesignLanguageCompile compiled) =>
        new(
            DocumentId,
            compiled.DefinitionSha,
            compiled.ChronologicalAge,
            compiled.TargetAppearanceAgeMin,
            compiled.TargetAppearanceAgeMax,
            compiled.LifeStage,
            compiled.StylizationBand,
            compiled.AgeAdaptiveBlock,
            compiled.PositivePromptBlock,
            compiled.NegativePromptBlock,
            compiled.CombinedWithAgeAppearance,
            ProviderCalled: false,
            GenerationExecuted: false,
            GeminiCalled: false,
            Persisted: false);
}

public sealed record CharacterDesignLanguageCompile(
    string DefinitionSha,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string LifeStage,
    string StylizationBand,
    string AgeAdaptiveBlock,
    string PositivePromptBlock,
    string NegativePromptBlock,
    string CombinedWithAgeAppearance);
