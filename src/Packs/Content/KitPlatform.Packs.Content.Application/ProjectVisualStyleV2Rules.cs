using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_PROJECT_VISUAL_STYLE_V2 — project-level stylized 3D language.
/// Describes HOW the world looks. Does not decide who the character is,
/// chronological age, appearance age, or role. Does not overwrite PVS V1,
/// call Gemini, generate characters, auto-approve, auto-lock, or branch
/// on character name.
/// </summary>
public static class ProjectVisualStyleV2Rules
{
    public const string DocumentId = "FAMIXA_PROJECT_VISUAL_STYLE_V2";
    public const string SuiteId = "FAMIXA_PROJECT_VISUAL_STYLE_V2_REGRESSION";
    public const string Version = "V2";
    public const string ExtraKey = "visualStyleRevision";
    public const string StyleKey = "3D_STYLIZED_REALISM";
    public const string StyleName = "FAMIXA 3D Stylized Realism";
    public const string DisplayName = "Famixa Stylized 3D V2";

    public const string StylizationLevel = "STRONG";
    public const string PhotorealismLevel = "LOW";
    public const string CharacterReadability = "HIGH";

    public const string StatusDraft = "DRAFT";
    public const string StatusPendingReview = "PENDING_REVIEW";
    public const string StatusApproved = "APPROVED";
    public const string StatusRejected = "REJECTED";
    public const string StatusLocked = "LOCKED";

    public const string GateConfirmation = "CONFIRMATION_REQUIRED";
    public const string GateNotReady = "PROJECT_VISUAL_STYLE_NOT_READY";
    public const string GateInvalid = "PROJECT_VISUAL_STYLE_INVALID";
    public const string GateOpenRevision = "BLOCK_OPEN_REVISION";
    public const string GateInvalidState = "BLOCK_INVALID_STATE";
    public const string GateNotApproved = "VISUAL_STYLE_REVISION_NOT_APPROVED";
    public const string GateLockedSilent = "LOCKED_CHARACTER_NO_SILENT_MUTATION";

    public const string ProtectedV1Sha = "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58";

    public const string StyleIntent =
        "Believable human characters expressed through a premium, warm, stylized 3D animation language.";

    public const string CanonicalStyleDescription =
        "Famixa uses a polished stylized 3D character language: "
        + "natural human proportions with gently simplified facial geometry, "
        + "expressive but restrained eyes, soft modeled facial forms, "
        + "clean stylized skin and hair materials, contemporary clothing, "
        + "soft cinematic lighting, and warm believable environments. "
        + "Characters should feel designed as expressive 3D characters rather "
        + "than photographic humans. The visual treatment remains natural, "
        + "age-appropriate, contemporary and emotionally approachable, "
        + "without becoming photorealistic, anime, chibi, toy-like or "
        + "exaggerated cartoon.";

    public const string StylePromptBlock =
        "FAMIXA 3D Stylized Realism: a premium designed stylized 3D human character, "
        + "believable human identity and age rendered through character design, "
        + "soft simplified facial planes, expressive but restrained eyes, "
        + "clean stylized skin, coherent stylized hair masses, "
        + "believable human anatomy, warm approachable expression, "
        + "contemporary everyday visual context, soft cinematic lighting, "
        + "subtle depth of field, clean premium materials, "
        + "clearly recognizable as a stylized 3D character in the Famixa visual universe, "
        + "not a photorealistic human portrait.";

    public const string NegativeStyleBlock =
        "NOT_PHOTOREALISTIC, NOT_HYPER_REAL_CGI, NOT_PHOTOGRAPHIC_PORTRAIT, "
        + "NOT_REALISTIC_HUMAN_PORTRAIT, NOT_REAL_PERSON_REPLICA, NOT_STOCK_PHOTO, "
        + "NOT_DOCUMENTARY_PHOTOGRAPHY, NOT_PASSPORT_PHOTO, NOT_CORPORATE_HEADSHOT, "
        + "NOT_BEAUTY_PHOTOGRAPHY, NOT_FASHION_EDITORIAL, NOT_CELEBRITY_LOOK, "
        + "NOT_GLAMOUR_MODEL, NOT_GAME_REALISM, NOT_DOCUMENTARY_REALISM, "
        + "NOT_ELDERLY_STEREOTYPE, NOT_RURAL_STEREOTYPE, NOT_OLD_FASHIONED_DEFAULT, "
        + "NOT_EXCESSIVE_SKIN_TEXTURE, NOT_VISIBLE_PORES, NOT_PHOTOGRAPHIC_HAIR, "
        + "NOT_HYPER_DETAILED_EYES, NOT_ANIME, NOT_MANGA, NOT_CHIBI, "
        + "NOT_CARTOON_FLAT, NOT_EXAGGERATED_CARTOON, NOT_PLASTIC_TOY, "
        + "NOT_WAX_FIGURE, NOT_UNCANNY_HUMAN";

    public const string HumanRealismBoundary =
        "AGE REALISTIC. IDENTITY REALISTIC. ANATOMY BELIEVABLE. RENDERING STYLIZED. "
        + "Keep chronological age, gender presentation, basic anatomy, and recognizable identity. "
        + "Stylize facial planes, eyes, skin, hair masses, texture complexity, and lighting softness. "
        + "Do not decide chronological age or appearance age.";

    public const string CharacterDesignLanguage =
        "Designed 3D character: friendly, approachable, emotionally readable, natural, "
        + "contemporary, warm, polished, family-oriented. Soft facial planes, clean cheeks, "
        + "slightly simplified facial structure, controlled nose geometry, clean jawline, "
        + "expressive eyes, natural lips, smooth non-plastic skin. Not a celebrity portrait, "
        + "stock photograph, documentary photograph, corporate headshot, passport portrait, "
        + "or fashion editorial.";

    public const string FaceStyle =
        "Recognizable, naturally attractive, age-appropriate, softly modeled stylized 3D face "
        + "with clean simplified facial planes, soft transitions, readable silhouette, "
        + "natural eyebrows, simplified nose, soft lips, approachable expression, subtle asymmetry. "
        + "No photorealistic pores, hyper-real wrinkles, beauty-model face, celebrity look, "
        + "or photographic microtexture. Visual Style does not change identity.";

    public const string EyeStyle =
        "Expressive, warm, clear, slightly stylized, emotionally readable 3D eyes with a natural gaze "
        + "and restrained iris detail. Not photorealistic human eyes, glass-like photographic eyes, "
        + "horror eyes, anime eyes, or oversized cartoon eyes.";

    public const string SkinStyle =
        "Smooth stylized 3D skin that feels alive: subtle natural variation, soft subsurface, "
        + "clean shading, believable but stylized. No visible photographic pores, hyper-real texture, "
        + "excessive wrinkles, photographic blemishes, plastic toy skin, or wax skin.";

    public const string HairStyle =
        "Designed 3D hair masses with clean silhouette, controlled volume, natural movement, "
        + "and stylized strand groups. Do not change hairstyle only to stylize. "
        + "No hyper-real individual hair photography or wet photographic hair.";

    public const string BodyStyle =
        "Believable human anatomy, character-specific, stylized but human. "
        + "Render the given age as a designed 3D character. "
        + "Do not convert a child into a miniature adult or chibi. "
        + "Do not convert an adult into a fashion-model or hyper-muscular CGI body.";

    public const string ClothingStyle =
        "Contemporary, simple, clean, believable, family-friendly clothing with stylized realistic fabric. "
        + "Do not default to rural, elderly, corporate, luxury, fashion-editorial, or historical clothing "
        + "unless Character Identity or Story explicitly requires it.";

    public const string LightingStyle =
        "Soft cinematic, warm natural light, gentle contrast, clean highlights, soft shadows, "
        + "dimensional 3D form, emotionally warm. No hard studio portrait flash, passport lighting, "
        + "beauty campaign lighting, dramatic movie lighting, or hyper-real photography lighting.";

    public const string MaterialStyle =
        "Simplified premium clean tactile stylized materials. Premium animation film, not photoreal CGI advertisement.";

    public const string EnvironmentStyle =
        "Contemporary, clean, warm, believable, softly detailed, family-oriented environment. "
        + "Do not default to rural countryside, old rustic house, or traditional village. "
        + "Role is narrative and does not imply a rural parent, farmer, or old-fashioned teacher.";

    public const string BackgroundStyle =
        "Clean contemporary everyday environment, soft depth of field, visually quiet. "
        + "Support the character. Do not default to countryside or rural stereotype.";

    public const string CameraStyle =
        "Camera serves character-readable composition only. Clean framing, natural perspective, "
        + "moderate depth of field. Same identity, Master, DNA, PRP, Age, Appearance, and Project Visual Style. "
        + "Change only camera, framing, and composition. Do not use camera to change age. "
        + "No fashion photography, extreme lens, or portrait-photography aesthetic.";

    public static readonly string[] Preview =
    [
        "Nhân vật 3D stylized thuộc vũ trụ Famixa, không phải chân dung người thật",
        "Mặt designed 3D, mặt phẳng mềm, mắt giàu biểu cảm nhưng kiềm chế",
        "Da sạch kiểu 3D, không lỗ chân lông ảnh thật",
        "Tóc theo khối stylized, giữ kiểu tóc của nhân vật",
        "Tuổi do Age Policy + Appearance Profile, không làm mọi người trẻ đi",
        "Đời sống đương đại, không mặc định nông thôn / ông già",
        "Ánh sáng cinematic ấm, chất liệu animation cao cấp",
        "Không anime / chibi / photoreal / fashion editorial / stock photo",
    ];

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRegenerate() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool OverwritesV1() => false;
    public static bool IsProjectLevel() => true;
    public static bool DependsOnCharacterName() => false;
    public static bool DependsOnRole() => false;
    public static bool DecidesChronologicalAge() => false;
    public static bool DecidesAppearanceAge() => false;
    public static bool DuplicatesAppearanceProfile() => false;
    public static bool ProviderOwnsStyle() => false;

    public static bool MayRequest(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || status == StatusDraft
        || status == StatusRejected
        || status == StatusLocked;

    public static bool MayApprove(string? status) => status == StatusPendingReview;
    public static bool MayReject(string? status) => status == StatusPendingReview;
    public static bool MayLock(string? status) => status == StatusApproved;

    public static bool CandidateIsAuthority(string? status) => status == StatusLocked;
    public static bool CurrentRemainsAuthority(string? status) => status != StatusLocked;

    public static bool IsOpen(string? status) =>
        status is StatusPendingReview or StatusApproved;

    public static string Canonical() => string.Join('\n', new[]
    {
        "PROJECT_STYLE_V2",
        "StyleKey=" + StyleKey,
        "StyleName=" + StyleName,
        "StyleIntent=" + StyleIntent,
        "CanonicalStyleDescription=" + CanonicalStyleDescription,
        "StylizationLevel=" + StylizationLevel,
        "PhotorealismLevel=" + PhotorealismLevel,
        "CharacterReadability=" + CharacterReadability,
        "StylePromptBlock=" + StylePromptBlock,
        "NegativeStyleBlock=" + NegativeStyleBlock,
        "HumanRealismBoundary=" + HumanRealismBoundary,
        "CharacterDesignLanguage=" + CharacterDesignLanguage,
        "FaceStyle=" + FaceStyle,
        "EyeStyle=" + EyeStyle,
        "SkinStyle=" + SkinStyle,
        "HairStyle=" + HairStyle,
        "BodyStyle=" + BodyStyle,
        "ClothingStyle=" + ClothingStyle,
        "LightingStyle=" + LightingStyle,
        "MaterialStyle=" + MaterialStyle,
        "EnvironmentStyle=" + EnvironmentStyle,
        "BackgroundStyle=" + BackgroundStyle,
        "CameraStyle=" + CameraStyle,
        "AgeOwnedByPvs=false",
        "RoleOwnedByPvs=false",
        "CharacterNameOwnedByPvs=false",
    });

    public static string Sha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(Canonical()));

    public static string BuildPrompt() => string.Join(" ", new[]
    {
        StylePromptBlock,
        CanonicalStyleDescription,
        HumanRealismBoundary,
        "Character design: " + CharacterDesignLanguage,
        "Face: " + FaceStyle,
        "Eyes: " + EyeStyle,
        "Skin: " + SkinStyle,
        "Hair: " + HairStyle,
        "Body: " + BodyStyle,
        "Clothing: " + ClothingStyle,
        "Lighting: " + LightingStyle,
        "Materials: " + MaterialStyle,
        "Environment: " + EnvironmentStyle,
        "Background: " + BackgroundStyle,
        "Camera: " + CameraStyle,
        "Forbidden style: " + NegativeStyleBlock + ".",
        "Avoid elderly-looking interpretation when inconsistent with the character's age profile.",
        "Avoid rural or old-fashioned interpretation unless Character Identity or Story requires it.",
        "Visual Style does not set age, identity, gender, role, or character-specific appearance.",
        "Do not invent a private visual style for this character.",
    });

    public static string CompileAuthorityPrompt(string? authoritySha, ProjectVisualStyleV1Rules.StyleDefinition? v1Fallback)
    {
        if (ProjectVisualStyleV1Rules.SameSha(authoritySha, Sha()))
            return BuildPrompt();
        return v1Fallback is null ? "" : ProjectVisualStyleV1Rules.BuildPrompt(v1Fallback);
    }

    public static string? ValidateGeneration(string? sha, string? prompt)
    {
        if (!ProjectVisualStyleV1Rules.LookLikeSha(sha) || string.IsNullOrWhiteSpace(prompt))
            return GateNotReady;
        if (ProjectVisualStyleV1Rules.SameSha(sha, Sha()))
            return PromptHasV2Style(prompt) ? ProjectVisualStyleV1Rules.GateValid : GateInvalid;
        if (ProjectVisualStyleV1Rules.SameSha(sha, ProtectedV1Sha))
            return ProjectVisualStyleV1Rules.GateValid;
        return GateInvalid;
    }

    public static bool GenerationBlocked(string? sha, string? prompt) =>
        ValidateGeneration(sha, prompt) != ProjectVisualStyleV1Rules.GateValid;

    public static string ComposeGenerationPrompt(
        string appearanceBlock,
        string identityBrief,
        string masterConstraints,
        string viewCamera)
    {
        return string.Join(" ", new[]
        {
            BuildPrompt(),
            (appearanceBlock ?? "").Trim(),
            (identityBrief ?? "").Trim(),
            (masterConstraints ?? "").Trim(),
            (viewCamera ?? "").Trim(),
        }.Where(x => x.Length > 0));
    }

    public static object GenerationContract(
        string projectId,
        string characterId,
        string appearanceBlock,
        string identityBrief,
        string view)
    {
        return new
        {
            projectId = (projectId ?? ProjectVisualStyleV1Rules.DefaultProject).Trim().ToUpperInvariant(),
            characterId = CharacterStudioV1Rules.NormalizeCharacterId(characterId),
            projectVisualStyleSha = Sha(),
            projectVisualStyleBlock = BuildPrompt(),
            negativeStyleBlock = NegativeStyleBlock,
            stylizationLevel = StylizationLevel,
            photorealismLevel = PhotorealismLevel,
            characterReadability = CharacterReadability,
            characterAppearanceProfile = appearanceBlock ?? "",
            identityBrief = identityBrief ?? "",
            view = (view ?? "").Trim().ToUpperInvariant(),
            providerMayInventStyle = false,
        };
    }

    public static bool ContractHasCompiledPvs(object contract)
    {
        var json = JsonSerializer.Serialize(contract, CanonicalOptions);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("projectVisualStyleSha", out var sha)
            || !ProjectVisualStyleV1Rules.SameSha(sha.GetString(), Sha()))
            return false;
        if (!doc.RootElement.TryGetProperty("projectVisualStyleBlock", out var block)
            || !PromptHasV2Style(block.GetString()))
            return false;
        return doc.RootElement.TryGetProperty("providerMayInventStyle", out var invent)
            && invent.ValueKind == JsonValueKind.False;
    }

    public static bool PromptHasV2Style(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains(StylePromptBlock, StringComparison.Ordinal)
        && prompt.Contains("stylized 3D", StringComparison.OrdinalIgnoreCase)
        && prompt.Contains("designed", StringComparison.OrdinalIgnoreCase)
        && prompt.Contains("NOT_PHOTOREALISTIC", StringComparison.Ordinal);

    public static bool PromptBlocksPhotoreal(string? prompt) =>
        PromptHasV2Style(prompt)
        && prompt!.Contains("NOT_HYPER_REAL_CGI", StringComparison.Ordinal)
        && prompt.Contains("NOT_PHOTOGRAPHIC_PORTRAIT", StringComparison.Ordinal)
        && prompt.Contains("NOT_ANIME", StringComparison.Ordinal)
        && prompt.Contains("NOT_CHIBI", StringComparison.Ordinal)
        && prompt.Contains("NOT_PLASTIC_TOY", StringComparison.Ordinal)
        && prompt.Contains("NOT_CARTOON_FLAT", StringComparison.Ordinal);

    public static bool PromptIsContemporary(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains("contemporary", StringComparison.OrdinalIgnoreCase)
        && prompt.Contains("NOT_RURAL_STEREOTYPE", StringComparison.Ordinal)
        && prompt.Contains("NOT_ELDERLY_STEREOTYPE", StringComparison.Ordinal);

    public static bool PromptOwnsAge(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        return prompt.Contains("make everyone youthful", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("ChronologicalAge:", StringComparison.Ordinal)
            || prompt.Contains("11 years", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("38 tuổi", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("TargetAppearanceAgeMin", StringComparison.Ordinal);
    }

    public static bool PromptOwnsRole(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        return prompt.Contains("Role:", StringComparison.Ordinal)
            || prompt.Contains(" father", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("Bố", StringComparison.Ordinal)
            || prompt.Contains("mother", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("Cô giáo", StringComparison.Ordinal);
    }

    public static bool ContainsCharacterName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Replace("Vietnamese", "", StringComparison.Ordinal);
        return t.Contains("Minh", StringComparison.Ordinal)
            || t.Contains("Linh", StringComparison.Ordinal)
            || t.Contains("Thảo", StringComparison.Ordinal)
            || t.Contains("CHAR-001", StringComparison.Ordinal)
            || t.Contains("CHAR-002", StringComparison.Ordinal)
            || t.Contains("CHAR-003", StringComparison.Ordinal)
            || t.Contains("CHAR-004", StringComparison.Ordinal)
            || t.Contains("CHAR-005", StringComparison.Ordinal)
            || t.Contains("CHAR-006", StringComparison.Ordinal)
            || System.Text.RegularExpressions.Regex.IsMatch(t, @"\bNam\b");
    }

    public static bool ViewsShareStyle(IReadOnlyDictionary<string, string> viewPrompts)
    {
        if (viewPrompts.Count < 4) return false;
        return viewPrompts.Values.All(p => p.Contains(StylePromptBlock, StringComparison.Ordinal));
    }

    public static bool ArtifactStaleAfterLock(
        string? artifactSha, string? artifactPvsSha, string? previousPvsSha, string? nextPvsSha)
    {
        if (!CharacterReferencePackRules.ShaExists(artifactSha))
            return false;
        if (!CharacterReferencePackRules.ShaExists(previousPvsSha)
            || !CharacterReferencePackRules.ShaExists(nextPvsSha)
            || ProjectVisualStyleV1Rules.SameSha(previousPvsSha, nextPvsSha))
            return false;
        return string.IsNullOrWhiteSpace(artifactPvsSha)
            || ProjectVisualStyleV1Rules.SameSha(artifactPvsSha, previousPvsSha);
    }

    public static bool MutationForbidden(bool officialLocked) => officialLocked;

    public static bool SemanticKeepsAgeTruth(string pvsPrompt, AgeExpressionTarget age) =>
        !PromptOwnsAge(pvsPrompt)
        && age.ChronologicalAge > 0
        && CharacterAgeConsistencyV1Rules.ValidateProfile(
            age.ChronologicalAge, age.TargetAppearanceAgeMin, age.TargetAppearanceAgeMax) is null;

    public static bool SemanticAdultLate30s(string composed, CharacterAppearanceProfile appearance) =>
        PromptHasV2Style(composed)
        && PromptBlocksPhotoreal(composed)
        && appearance.ChronologicalAge == 38
        && appearance.TargetAppearanceAgeMin <= 35
        && appearance.TargetAppearanceAgeMax >= 41
        && composed.Contains("contemporary Vietnamese adult man", StringComparison.OrdinalIgnoreCase)
        && appearance.NegativeAppearanceConstraints.Any(n =>
            n.Contains("rural elderly", StringComparison.OrdinalIgnoreCase)
            || n.Contains("farmer", StringComparison.OrdinalIgnoreCase))
        && !PromptOwnsRole(BuildPrompt())
        && !ContainsCharacterName(BuildPrompt());

    public static bool SemanticChild(string composed, CharacterAppearanceProfile appearance) =>
        PromptHasV2Style(composed)
        && appearance.ChronologicalAge == 11
        && appearance.TargetAppearanceAgeMin <= 10
        && appearance.TargetAppearanceAgeMax >= 12
        && composed.Contains("clearly a child", StringComparison.OrdinalIgnoreCase)
        && !BuildPrompt().Contains("teenager", StringComparison.OrdinalIgnoreCase)
        && !BuildPrompt().Contains("anime child", StringComparison.OrdinalIgnoreCase);

    public static bool SemanticYoungAdultWoman(string composed, CharacterAppearanceProfile appearance) =>
        PromptHasV2Style(composed)
        && appearance.ChronologicalAge == 27
        && appearance.TargetAppearanceAgeMin <= 25
        && appearance.TargetAppearanceAgeMax >= 29
        && composed.Contains("contemporary Vietnamese adult woman", StringComparison.OrdinalIgnoreCase)
        && appearance.NegativeAppearanceConstraints.Any(n =>
            n.Contains("teenager", StringComparison.OrdinalIgnoreCase));

    public static bool SemanticOlderAdult(string composed, CharacterAppearanceProfile appearance) =>
        PromptHasV2Style(composed)
        && appearance.ChronologicalAge == 65
        && appearance.TargetAppearanceAgeMin <= 61
        && appearance.TargetAppearanceAgeMax >= 69
        && composed.Contains("older", StringComparison.OrdinalIgnoreCase)
        && !BuildPrompt().Contains("caricature", StringComparison.OrdinalIgnoreCase)
        && !BuildPrompt().Contains("photorealistic elderly portrait", StringComparison.OrdinalIgnoreCase);

    public sealed record RevisionSource(
        bool ProjectReady,
        string? CurrentVersion,
        string? CurrentSha,
        string? Status,
        bool Confirm,
        string? Actor);

    public static string? EvaluateCreate(RevisionSource source)
    {
        if (!source.Confirm)
            return GateConfirmation;
        if (!source.ProjectReady || !ProjectVisualStyleV1Rules.LookLikeSha(source.CurrentSha))
            return GateNotReady;
        if (IsOpen(source.Status))
            return GateOpenRevision;
        return null;
    }

    public static string? EvaluateAdvance(string? status, string action)
    {
        var a = (action ?? "").Trim().ToUpperInvariant();
        if (a == "APPROVE")
            return MayApprove(status) ? null : GateInvalidState;
        if (a == "REJECT")
            return MayReject(status) ? null : GateInvalidState;
        if (a == "LOCK")
            return MayLock(status) ? null : (status == StatusPendingReview ? GateNotApproved : GateInvalidState);
        return GateInvalidState;
    }

    public sealed record RevisionSnapshot(
        string Status,
        string Version,
        string CandidateSha,
        string? CurrentSha,
        string? CurrentVersion,
        string? Fingerprint,
        string? RequestedBy,
        string? ReviewedBy,
        string? ReviewDecision,
        string? RejectionReason,
        string? Notes);

    public static string Fingerprint(string? currentSha) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            document = DocumentId,
            version = Version,
            candidateSha = Sha(),
            currentSha = (currentSha ?? "").Trim().ToLowerInvariant(),
        }, CanonicalOptions)));

    public static object RequestContract(string projectId, string? visualStyleId) => new
    {
        projectId = (projectId ?? ProjectVisualStyleV1Rules.DefaultProject).Trim().ToUpperInvariant(),
        projectVisualStyleId = visualStyleId,
        projectVisualStyleRevision = Version,
        styleName = StyleName,
        styleIntent = StyleIntent,
        canonicalStyleDescription = CanonicalStyleDescription,
        stylizationLevel = StylizationLevel,
        photorealismLevel = PhotorealismLevel,
        characterReadability = CharacterReadability,
        stylePromptBlock = StylePromptBlock,
        negativeStyleBlock = NegativeStyleBlock,
        humanRealismBoundary = HumanRealismBoundary,
        faceStyle = FaceStyle,
        eyeStyle = EyeStyle,
        skinStyle = SkinStyle,
        hairStyle = HairStyle,
        bodyStyle = BodyStyle,
        clothingStyle = ClothingStyle,
        lightingStyle = LightingStyle,
        materialStyle = MaterialStyle,
        environmentStyle = EnvironmentStyle,
        backgroundStyle = BackgroundStyle,
        cameraStyle = CameraStyle,
        authoritySha256 = Sha(),
    };

    public static object PersistV2Document(
        Guid id, string projectId, string status, string sha, string? lockedBy) => new
    {
        documentId = DocumentId,
        id = id.ToString(),
        projectId,
        styleKey = StyleKey,
        styleName = StyleName,
        description = StyleIntent,
        version = Version,
        status,
        sha,
        authority = ProjectVisualStyleV1Rules.StaffAuthority,
        lockedBy,
        preview = Preview,
        prompt = BuildPrompt(),
        canonical = Canonical(),
        styleIntent = StyleIntent,
        canonicalStyleDescription = CanonicalStyleDescription,
        stylizationLevel = StylizationLevel,
        photorealismLevel = PhotorealismLevel,
        characterReadability = CharacterReadability,
        stylePromptBlock = StylePromptBlock,
        negativeStyleBlock = NegativeStyleBlock,
    };

    public static string MergeRevision(string? rulesJson, RevisionSnapshot snap)
    {
        var obj = string.IsNullOrWhiteSpace(rulesJson)
            ? new JsonObject()
            : JsonNode.Parse(rulesJson) as JsonObject ?? new JsonObject();
        obj[ExtraKey] = JsonSerializer.SerializeToNode(new
        {
            document = DocumentId,
            status = snap.Status,
            version = snap.Version,
            candidateSha = snap.CandidateSha,
            currentSha = snap.CurrentSha,
            currentVersion = snap.CurrentVersion,
            fingerprint = snap.Fingerprint,
            requestedBy = snap.RequestedBy,
            reviewedBy = snap.ReviewedBy,
            reviewDecision = snap.ReviewDecision,
            rejectionReason = snap.RejectionReason,
            notes = snap.Notes,
            styleName = StyleName,
            styleIntent = StyleIntent,
            canonicalStyleDescription = CanonicalStyleDescription,
            stylizationLevel = StylizationLevel,
            photorealismLevel = PhotorealismLevel,
            characterReadability = CharacterReadability,
            stylePromptBlock = StylePromptBlock,
            negativeStyleBlock = NegativeStyleBlock,
            humanRealismBoundary = HumanRealismBoundary,
            characterDesignLanguage = CharacterDesignLanguage,
            faceStyle = FaceStyle,
            eyeStyle = EyeStyle,
            skinStyle = SkinStyle,
            hairStyle = HairStyle,
            bodyStyle = BodyStyle,
            clothingStyle = ClothingStyle,
            lightingStyle = LightingStyle,
            materialStyle = MaterialStyle,
            environmentStyle = EnvironmentStyle,
            backgroundStyle = BackgroundStyle,
            cameraStyle = CameraStyle,
        }, CanonicalOptions);
        return obj.ToJsonString();
    }

    public static RevisionSnapshot? ReadRevision(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rulesJson);
            if (!doc.RootElement.TryGetProperty(ExtraKey, out var n) || n.ValueKind != JsonValueKind.Object)
                return null;
            string? Read(string name) =>
                n.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var status = Read("status");
            if (string.IsNullOrWhiteSpace(status)) return null;
            return new RevisionSnapshot(
                status!,
                Read("version") ?? Version,
                Read("candidateSha") ?? "",
                Read("currentSha"),
                Read("currentVersion"),
                Read("fingerprint"),
                Read("requestedBy"),
                Read("reviewedBy"),
                Read("reviewDecision"),
                Read("rejectionReason"),
                Read("notes"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool V1ShaUnchanged(string? liveSha) =>
        ProjectVisualStyleV1Rules.SameSha(liveSha, ProtectedV1Sha)
        && ProjectVisualStyleV1Rules.SameSha(
            ProjectVisualStyleV1Rules.Sha(ProjectVisualStyleV1Rules.PresetOf(StyleKey)!),
            ProtectedV1Sha);

    public static bool MoreStylizedThanV1()
    {
        var v1 = ProjectVisualStyleV1Rules.BuildPrompt(ProjectVisualStyleV1Rules.PresetOf(StyleKey)!);
        var v2 = BuildPrompt();
        return v2 != v1
            && PromptHasV2Style(v2)
            && PromptBlocksPhotoreal(v2)
            && StylizationLevel == "STRONG"
            && PhotorealismLevel == "LOW"
            && !v1.Contains(StylePromptBlock, StringComparison.Ordinal)
            && Sha() != ProtectedV1Sha;
    }

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
