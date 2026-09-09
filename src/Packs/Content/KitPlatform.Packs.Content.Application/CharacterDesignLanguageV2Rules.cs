using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2 — structured character-construction law.
/// Turns stylized 3D into compileable prompt constraints. Independent of
/// character name, role, age, gender, and PVS authority. Does not call Gemini,
/// generate pixels, approve, lock, or mutate Master / DNA / PRP / CRP.
/// </summary>
public static class CharacterDesignLanguageV2Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2";
    public const string SuiteId = "FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_REGRESSION";
    public const string Version = "V2";
    public const string Name = "Famixa Character Design Language V2";
    public const string Status = "DRAFT";

    public const string StylizationLevel = "STRONG";
    public const string StylizationTarget = "STRONG_STYLIZED_3D";
    public const int StylizationScale = 4;
    public const string PhotorealismCeiling = "LOW";
    public const string CartoonFloor = "CONTROLLED";

    public const string AxiomCharacterFirst =
        "FAMIXA characters are designed 3D characters, not realistic humans rendered in 3D.";
    public const string AxiomStylizationFirst =
        "Stylization must be readable immediately in facial proportions, eye design, "
        + "simplified anatomy, silhouette, material, and surface detail.";
    public const string AxiomHumanBelievable =
        "Characters stay human-believable: age-readable, gender-readable, naturally expressive, "
        + "anatomically sound. Not a meaningless cartoon.";
    public const string AxiomCrossCharacter =
        "Every character shares one head, eye, face, body, material, lighting, and stylization philosophy.";

    public const string FaceLanguage =
        "Head slightly stylized. Facial planes simplified. Smooth transitions. Soft clean cheeks. "
        + "Softer jaw than photographic anatomy. Simplified nose, lips, and ears. "
        + "No photographic facial anatomy, pores, hyper-realistic nasolabial folds, "
        + "or hyper-detailed wrinkles. The face must read as a designed 3D character, "
        + "not a real-person portrait.";

    public const string EyeLanguage =
        "Expressive, clean iris and sclera, soft eye shape, simplified eyelid anatomy, "
        + "emotionally readable, slightly enlarged relative to photorealistic human eyes. "
        + "Children may have a larger eye proportion. Adults keep the same eye language "
        + "without photographic veins or iris micro-detail. Not uncanny human eyes.";

    public const string NoseMouthLanguage =
        "Nose: simplified, soft bridge, controlled nostril detail, no photographic skin complexity. "
        + "Mouth: clean, subtle, stylized lip volume, natural expression, no hyper-realistic lip texture. "
        + "Expression stays warm, approachable, and readable without forcing one expression on every character.";

    public const string HairLanguage =
        "Designed 3D hair: grouped strands, controlled shapes, readable silhouette, soft volume, "
        + "consistent material. Hairstyle remains character identity. Design Language only controls "
        + "stylization, grouping, material, edge softness, and rendering. "
        + "No photographic individual strands, wet-look realism unless identity requires it, "
        + "or hyper-realistic scalp.";

    public const string BodyLanguage =
        "Slightly stylized proportions, clean silhouette, simplified anatomy, readable shoulders, "
        + "controlled neck, simplified hands and feet, natural pose. "
        + "No hyper-realistic musculature, fashion-model body, or photographic body proportions. "
        + "Child and adult proportions may differ while staying in the same design system.";

    public const string HandFootLanguage =
        "Hands: simplified, clean, anatomically believable, slightly stylized, no excessive finger detail. "
        + "Feet: simplified silhouette, believable, consistent with character scale. "
        + "No veins, photographic wrinkles, or extreme anatomical detail.";

    public const string SkinLanguage =
        "Smooth stylized 3D skin, subtle subsurface, soft shading, clean surface, believable skin tone. "
        + "Not visible pores, photorealistic pores, realistic blemishes, excessive wrinkles, "
        + "photographic microtexture, hyper-realistic SSS, or a digital-human look.";

    public const string ClothingLanguage =
        "Clean, stylized, physically believable clothing with slightly simplified readable folds. "
        + "Clothing identity stays with the character or scene. Design Language controls material only. "
        + "No photographic textile microtexture or excessive fabric wrinkles.";

    public const string LightingLanguage =
        "Soft studio or warm natural light, gentle shadows, soft highlights, clean separation, "
        + "controlled contrast. Lighting serves character readability. "
        + "Not cinematic live-action lighting, Hollywood portrait, photographic color grading, "
        + "or excessive lens/bokeh effects.";

    public const string CameraLanguage =
        "Camera changes viewpoint, composition, framing, and distance only. "
        + "FRONT, THREE_QUARTER, SIDE, and FULL_BODY share the same design language, identity, "
        + "age appearance, and material language. Camera must not change perceived character style.";

    public const string RealismCeilingLanguage =
        "PhotorealismCeiling=LOW. Clearly stylized 3D character, not a photorealistic human. "
        + "Reject photorealistic human, realistic human portrait, live-action human, digital human, "
        + "realistic CGI person, photographic skin, photographic facial texture, realistic skin pores, "
        + "realistic wrinkles, hyper-realistic anatomy, fashion photography, cinematic human portrait, "
        + "and real-person appearance.";

    public const string CartoonFloorLanguage =
        "CartoonFloor=CONTROLLED. Not chibi, not extreme oversized head, not anime eyes, "
        + "not exaggerated facial features, not toy-like plastic, "
        + "and not children's-cartoon simplification for adults.";

    public const string CrossCharacterInvariants =
        "Shared: facial design philosophy, eye language, nose and mouth simplification, "
        + "hair and skin material language, body/hand/foot simplification, edge softness, "
        + "lighting language, surface-detail ceiling, stylization level, realism ceiling, "
        + "rendering language, character silhouette philosophy. "
        + "Character-specific age, gender, height, build, hairstyle, facial identity, clothing, "
        + "role, expression, and personality may change. Design language must not.";

    public const string LifestyleBoundary =
        "Default lifestyle is contemporary Vietnamese. Do not default to rural, farmer, "
        + "elderly rural, traditional, or countryside. Role does not imply a visual stereotype.";

    public const string RoleBoundary =
        "Role is narrative only. Role does not set visual style, age appearance, clothing style, "
        + "or body type.";

    public const string PromptBlock =
        "[FAMIXA CHARACTER DESIGN LANGUAGE V2] "
        + "Create a clearly designed stylized 3D character within the FAMIXA visual system. "
        + AxiomCharacterFirst + " "
        + "Use strong but controlled stylization, simplified human anatomy, "
        + "slightly stylized facial proportions, expressive clean eyes, simplified nose and mouth, "
        + "smooth stylized skin, grouped stylized hair, clean clothing materials, "
        + "soft natural shading, soft edges, believable but intentionally designed proportions, "
        + "and warm approachable character readability. "
        + "Maintain the character's specific identity, age, gender, body type and hairstyle, "
        + "but express all of them through the same FAMIXA character design language. "
        + "Clearly stylized 3D character, not a photorealistic human. "
        + "Avoid photorealistic human appearance, realistic CGI human, digital-human look, "
        + "photographic skin, visible skin pores, hyper-realistic anatomy, photographic facial texture, "
        + "cinematic live-action appearance, extreme cartoon, anime, and chibi. "
        + "The result must belong to the same designed 3D character universe as other FAMIXA characters. "
        + "[END CHARACTER DESIGN LANGUAGE V2]";

    public const string NegativePromptBlock =
        "photorealistic human, realistic human portrait, live-action human, digital human, "
        + "realistic CGI person, photographic skin, photographic facial texture, realistic skin pores, "
        + "realistic wrinkles, hyper-realistic anatomy, fashion photography, cinematic human portrait, "
        + "real-person appearance, extreme cartoon, anime, chibi, toy plastic";

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRegenerate() => false;
    public static bool AutoActivate() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool Persists() => false;
    public static bool MutatesMaster() => false;
    public static bool MutatesDna() => false;
    public static bool MutatesPrp() => false;
    public static bool MutatesCrp() => false;
    public static bool ChangesProjectVisualStyle() => false;
    public static bool UsesCharacterName() => false;
    public static bool UsesCharacterId() => false;
    public static bool UsesRole() => false;
    public static bool UsesAge() => false;
    public static bool UsesGender() => false;
    public static bool ReplacesAgePolicy() => false;
    public static bool ReplacesAppearanceProfile() => false;
    public static bool ReplacesIdentity() => false;
    public static bool ReplacesMasterAuthority() => false;

    public static string Canonical() => string.Join('\n', new[]
    {
        "CHARACTER_DESIGN_LANGUAGE_V2",
        "Name=" + Name,
        "Version=" + Version,
        "Status=" + Status,
        "AxiomCharacterFirst=" + AxiomCharacterFirst,
        "AxiomStylizationFirst=" + AxiomStylizationFirst,
        "AxiomHumanBelievable=" + AxiomHumanBelievable,
        "AxiomCrossCharacter=" + AxiomCrossCharacter,
        "FaceLanguage=" + FaceLanguage,
        "EyeLanguage=" + EyeLanguage,
        "NoseMouthLanguage=" + NoseMouthLanguage,
        "HairLanguage=" + HairLanguage,
        "BodyLanguage=" + BodyLanguage,
        "HandFootLanguage=" + HandFootLanguage,
        "SkinLanguage=" + SkinLanguage,
        "ClothingLanguage=" + ClothingLanguage,
        "LightingLanguage=" + LightingLanguage,
        "CameraLanguage=" + CameraLanguage,
        "RealismCeilingLanguage=" + RealismCeilingLanguage,
        "CartoonFloorLanguage=" + CartoonFloorLanguage,
        "CrossCharacterInvariants=" + CrossCharacterInvariants,
        "LifestyleBoundary=" + LifestyleBoundary,
        "RoleBoundary=" + RoleBoundary,
        "PromptBlock=" + PromptBlock,
        "NegativePromptBlock=" + NegativePromptBlock,
        "StylizationLevel=" + StylizationLevel,
        "StylizationTarget=" + StylizationTarget,
        "StylizationScale=" + StylizationScale,
        "PhotorealismCeiling=" + PhotorealismCeiling,
        "CartoonFloor=" + CartoonFloor,
        "CharacterNameOwned=false",
        "CharacterIdOwned=false",
        "RoleOwned=false",
        "AgeOwned=false",
        "GenderOwned=false",
        "PvsAuthorityOwned=false",
    });

    public static string Sha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(Canonical()));

    public static string? Validate()
    {
        if (string.IsNullOrWhiteSpace(PromptBlock) || string.IsNullOrWhiteSpace(NegativePromptBlock))
            return "CHARACTER_DESIGN_LANGUAGE_V2_INVALID";
        if (!ProjectVisualStyleV1Rules.LookLikeSha(Sha()))
            return "CHARACTER_DESIGN_LANGUAGE_V2_SHA_INVALID";
        if (ContainsCharacterName(Canonical()) || ContainsCharacterId(Canonical()))
            return "CHARACTER_DESIGN_LANGUAGE_V2_HAS_CHARACTER";
        return null;
    }

    public static bool IsValid() => Validate() is null;

    /// <summary>PVS is optional context. It never changes the CDL block or SHA.</summary>
    public static string Compile(string? projectVisualStyleSha = null)
    {
        _ = projectVisualStyleSha;
        return PromptBlock;
    }

    public static string EnsureInPrompt(string? prompt)
    {
        var text = prompt ?? "";
        if (PromptHasDesignLanguage(text)) return text;
        return string.IsNullOrWhiteSpace(text) ? PromptBlock : PromptBlock + " " + text;
    }

    public static string AgeInterpretation(int chronologicalAge)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(chronologicalAge);
        var band = chronologicalAge switch
        {
            <= 12 => "stylized FAMIXA child, not a photorealistic child photograph",
            <= 17 => "stylized FAMIXA teenager, not a photographic teen portrait",
            <= 29 => "stylized FAMIXA young adult, clearly adult, not teenage, not photorealistic",
            <= 49 => "stylized FAMIXA adult, not a photorealistic middle-aged portrait",
            _ => "stylized FAMIXA older adult, not a photorealistic elderly portrait",
        };
        return $"ChronologicalAge {age.ChronologicalAge} is expressed as {band}. "
            + $"Age Policy target remains {age.TargetAppearanceAgeMin}–{age.TargetAppearanceAgeMax}. "
            + "Character Design Language does not replace Age Policy.";
    }

    public static string ResolveSceneStyle(string? sceneInstruction)
    {
        var text = (sceneInstruction ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) return PromptBlock;
        var rewritten = text
            .Replace("realistic portrait", "stylized 3D character portrait", StringComparison.OrdinalIgnoreCase)
            .Replace("photorealistic", "stylized 3D", StringComparison.OrdinalIgnoreCase)
            .Replace("live-action", "designed 3D character", StringComparison.OrdinalIgnoreCase);
        return PromptBlock + " Scene composition may change camera only: " + rewritten;
    }

    public static string ComposeProviderPrompt(
        string? projectVisualStyle,
        string? identity,
        string? ageAppearance,
        string? appearance,
        string? masterConstraints,
        string? view,
        string? scene)
    {
        return string.Join(" ", new[]
        {
            (projectVisualStyle ?? "").Trim(),
            PromptBlock,
            (identity ?? "").Trim(),
            (ageAppearance ?? "").Trim(),
            (appearance ?? "").Trim(),
            (masterConstraints ?? "").Trim(),
            (view ?? "").Trim(),
            string.IsNullOrWhiteSpace(scene) ? "" : ResolveSceneStyle(scene),
            "Forbidden: " + NegativePromptBlock + ".",
        }.Where(x => x.Length > 0));
    }

    public static bool PromptHasDesignLanguage(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains("[FAMIXA CHARACTER DESIGN LANGUAGE V2]", StringComparison.Ordinal)
        && prompt.Contains("stylized 3D character", StringComparison.OrdinalIgnoreCase);

    public static bool PromptHasAntiPhotorealism(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains("not a photorealistic human", StringComparison.OrdinalIgnoreCase)
        && prompt.Contains("digital-human", StringComparison.OrdinalIgnoreCase);

    public static bool ContainsCharacterName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text
            .Replace("CharacterNameOwned", "", StringComparison.OrdinalIgnoreCase)
            .Replace("character name", "", StringComparison.OrdinalIgnoreCase);
        return Regex.IsMatch(normalized, @"\b(Minh|Nam|Linh|Thảo|Thao)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool ContainsCharacterId(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && Regex.IsMatch(text, @"CHAR-\d+", RegexOptions.IgnoreCase);

    public static bool ContainsRole(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && Regex.IsMatch(text, @"\b(Bố|Mẹ|Cô giáo|Main Child)\b", RegexOptions.IgnoreCase);

    public static bool ContainsLifestyleStereotype(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && (text.Contains("farmer default", StringComparison.OrdinalIgnoreCase)
            || text.Contains("rural elderly default", StringComparison.OrdinalIgnoreCase)
            || text.Contains("if role", StringComparison.OrdinalIgnoreCase));

    public static CharacterDesignLanguageV2DefinitionDto ToDefinitionDto() =>
        new(
            DocumentId,
            Version,
            Name,
            Status,
            CurrentAuthority: false,
            Sha(),
            StylizationLevel,
            StylizationTarget,
            StylizationScale,
            PhotorealismCeiling,
            CartoonFloor,
            FaceLanguage,
            EyeLanguage,
            NoseMouthLanguage,
            HairLanguage,
            BodyLanguage,
            HandFootLanguage,
            SkinLanguage,
            ClothingLanguage,
            LightingLanguage,
            CameraLanguage,
            RealismCeilingLanguage,
            CartoonFloorLanguage,
            CrossCharacterInvariants,
            PromptBlock,
            NegativePromptBlock,
            Canonical(),
            ProviderCalled: false,
            GenerationExecuted: false,
            GeminiCalled: false,
            AutoApprove: false,
            AutoLock: false,
            Persisted: false);
}
