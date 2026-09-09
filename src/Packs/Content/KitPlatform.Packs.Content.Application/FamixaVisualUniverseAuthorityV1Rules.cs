using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1 — project-level visual universe.
/// Characters inherit this language; they do not define it.
/// Wraps current PVS + structured Character Design Language.
/// Does not replace PVS V1, mutate locked characters, auto-approve, or call Gemini.
/// </summary>
public static class FamixaVisualUniverseAuthorityV1Rules
{
    public const string DocumentId = "FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1";
    public const string SuiteId = "FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1_REGRESSION";
    public const string ExtraKey = "visualUniverseAuthority";
    public const string Version = "V1";
    public const string AuthorityId = "FAMIXA-VISUAL-UNIVERSE-V1";
    public const string ProjectId = "FAMIXA";
    public const string StyleId = "3D_STYLIZED_REALISM";
    public const string StyleName = "FAMIXA Visual Universe";

    public const string StatusDraft = "DRAFT";
    public const string StatusCalibrationPending = "CALIBRATION_PENDING";
    public const string StatusCalibrationReady = "CALIBRATION_READY";
    public const string StatusPendingReview = "PENDING_REVIEW";
    public const string StatusApproved = "APPROVED";
    public const string StatusLocked = "LOCKED";
    public const string StatusRejected = "REJECTED";
    public const string StatusSuperseded = "SUPERSEDED";

    public const string StylizationLevel = "STRONG";
    public const string RealismCeiling = "LOW_TO_MODERATE";
    public const string PhotorealismCeiling = "LOW";

    public const string GateNotReady = "VISUAL_UNIVERSE_NOT_READY";
    public const string GateAuthorityNotLocked = "VISUAL_STYLE_AUTHORITY_NOT_LOCKED";
    public const string GateDesignLanguageNotReady = "CHARACTER_DESIGN_LANGUAGE_NOT_READY";
    public const string GateReferenceNotReady = "STYLE_REFERENCE_PACK_NOT_READY";
    public const string GateCalibrationNotApproved = "STYLE_CALIBRATION_NOT_APPROVED";
    public const string GateAuthorityShaMismatch = "STYLE_AUTHORITY_SHA_MISMATCH";
    public const string GateDesignLanguageShaMismatch = "DESIGN_LANGUAGE_SHA_MISMATCH";
    public const string GateBindingMismatch = "VISUAL_AUTHORITY_BINDING_MISMATCH";
    public const string GateAgeNotReady = "AGE_PROFILE_NOT_READY";
    public const string GateIdentityNotReady = "IDENTITY_NOT_READY";
    public const string GateConfirmation = "CONFIRMATION_REQUIRED";
    public const string GateOpenRevision = "BLOCK_OPEN_REVISION";
    public const string GateInvalidState = "BLOCK_INVALID_STATE";
    public const string GateNotApproved = "VISUAL_UNIVERSE_NOT_APPROVED";
    public const string GateLockedSilent = "LOCKED_CHARACTER_NO_SILENT_MUTATION";
    public const string StyleStale = "STYLE_STALE";

    public const string StyleIntent =
        "Stylized 3D character universe. Characters are designed inhabitants of one coherent "
        + "animated / stylized 3D world. High quality never means more photorealistic.";

    public const string FaceLanguage =
        "Facial stylization visible. Simplified facial planes. Soft cheek structure. Softer jaw than photographic anatomy. "
        + "Simplified nose, mouth, ears, and eyebrows. Designed 3D face, not a real-person portrait.";

    public const string EyeLanguage =
        "Expressive, slightly enlarged relative to photorealistic human eyes, clean iris and sclera, "
        + "soft highlight, readable expression. Same eye grammar for children and adults.";

    public const string HairLanguage =
        "Grouped 3D hair clumps, readable silhouette, controlled edges, stylized specular. "
        + "Hairstyle stays identity. No photographic strand realism.";

    public const string BodyLanguage =
        "Slightly stylized head/body relationship, readable shoulders, simplified limbs and hands, "
        + "designed silhouette. Child and adult stay in the same studio language.";

    public const string ProportionLanguage =
        "Child, adult, and senior proportions may differ. Stylization range stays STRONG. "
        + "Age changes maturity, not visual language.";

    public const string MaterialLanguage =
        "Smooth stylized skin, controlled micro-detail, no visible pores, no photographic wrinkles, "
        + "subtle subsurface, clean cloth and hair materials.";

    public const string LightingLanguage =
        "Soft cinematic lighting, controlled highlights and shadows. No photographic portrait treatment.";

    public const string CameraLanguage =
        "Portrait, three-quarter, side, and full-body change viewpoint only. Camera does not change style.";

    public const string ColorLanguage =
        "Warm controlled saturation, clean contrast, background supports the character.";

    public const string EnvironmentLanguage =
        "Stylized contemporary environment, controlled depth of field, no documentary photography.";

    public const string PositiveConstraints =
        "stylized 3D character design; coherent animated-film character language; "
        + "simplified facial geometry; expressive eyes; clean facial planes; smooth stylized skin; "
        + "controlled material detail; simplified anatomy; designed silhouette; "
        + "consistent character-world proportions; cohesive stylized rendering; "
        + "same visual universe across age groups; same visual grammar across male and female characters";

    public const string NegativeConstraints =
        "photorealistic human, photographic portrait, live action actor, fashion photography, "
        + "documentary photography, hyperrealistic human face, realistic skin pores, "
        + "excessive skin microtexture, photographic wrinkles, realistic human anatomy, "
        + "realistic portrait photography, DSLR portrait, beauty photography, celebrity portrait, "
        + "real person appearance";

    public const string StyleReferenceInstruction =
        "Inject approved FAMIXA style exemplars before character-specific references. "
        + "Style references communicate the visual universe only. They are not production characters. "
        + "Character Master controls identity. Visual Universe Authority controls design language.";

    public static readonly IReadOnlyList<StyleExemplar> StyleExemplars =
    [
        new("ADULT_MALE", "Adult Male", 38, 35, 40, "FRONT"),
        new("ADULT_FEMALE", "Adult Female", 33, 30, 35, "FRONT"),
        new("CHILD_BOY", "Child Boy", 11, 10, 12, "FRONT"),
        new("CHILD_GIRL", "Child Girl", 11, 10, 12, "FRONT"),
        new("FULL_BODY_MALE", "Full Body Male", 38, 35, 40, "FULL_BODY"),
        new("FULL_BODY_FEMALE", "Full Body Female", 33, 30, 35, "FULL_BODY"),
    ];

    public static readonly IReadOnlyList<StyleExemplar> CalibrationArchetypes =
    [
        new("ADULT_MALE", "Adult Male", 38, 35, 40, "FRONT"),
        new("ADULT_FEMALE", "Adult Female", 33, 30, 35, "FRONT"),
        new("CHILD_BOY", "Child Boy", 11, 10, 12, "FRONT"),
        new("CHILD_GIRL", "Child Girl", 11, 10, 12, "FRONT"),
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoRegenerate() => false;
    public static bool AutoActivate() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool PersistsByCompile() => false;
    public static bool ReplacesProjectVisualStyleV1() => false;
    public static bool MutatesLockedCharacters() => false;
    public static bool UsesCharacterName() => false;
    public static bool UsesRole() => false;
    public static bool AgeChangesVisualLanguage() => false;
    public static bool GenderChangesVisualLanguage() => false;
    public static bool RoleChangesVisualLanguage() => false;
    public static bool IdentityChangesVisualLanguage() => false;
    public static bool DraftIsProductionAuthority() => false;

    public static bool IsAuthority(string? status) => status == StatusLocked;
    public static bool CurrentPvsRemainsAuthority(string? status) => status != StatusLocked;
    public static bool MayRequest(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || status is StatusDraft or StatusRejected or StatusLocked or StatusSuperseded;
    public static bool MayCalibrate(string? status) =>
        status is StatusCalibrationPending or StatusCalibrationReady or StatusRejected;
    public static bool MayApprove(string? status) =>
        status is StatusPendingReview or StatusCalibrationReady;
    public static bool MayReject(string? status) =>
        status is StatusPendingReview or StatusCalibrationReady or StatusCalibrationPending;
    public static bool MayLock(string? status) => status == StatusApproved;
    public static bool IsOpen(string? status) =>
        status is StatusCalibrationPending or StatusCalibrationReady or StatusPendingReview or StatusApproved;

    public static string DesignLanguageCanonical() => string.Join('\n', new[]
    {
        "STRUCTURED_CHARACTER_DESIGN_LANGUAGE",
        "FACE_LANGUAGE=" + FaceLanguage,
        "EYE_LANGUAGE=" + EyeLanguage,
        "HAIR_LANGUAGE=" + HairLanguage,
        "BODY_LANGUAGE=" + BodyLanguage,
        "PROPORTION_LANGUAGE=" + ProportionLanguage,
        "MATERIAL_LANGUAGE=" + MaterialLanguage,
        "LIGHTING_LANGUAGE=" + LightingLanguage,
        "CAMERA_LANGUAGE=" + CameraLanguage,
        "COLOR_LANGUAGE=" + ColorLanguage,
        "ENVIRONMENT_LANGUAGE=" + EnvironmentLanguage,
        "CDL_V2_SHA=" + CharacterDesignLanguageV2Rules.Sha(),
    });

    public static string DesignLanguageSha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(DesignLanguageCanonical()));

    public static string StyleReferencePackCanonical() => string.Join('\n',
        StyleExemplars.Select(e =>
            $"{e.Code}|{e.Label}|{e.ChronologicalAge}|{e.MinAge}-{e.MaxAge}|{e.View}|anonymous"));

    public static string StyleReferencePackSha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(StyleReferencePackCanonical()));

    public static string StyleCalibrationPackCanonical() => string.Join('\n',
        CalibrationArchetypes.Select(e =>
            $"CAL|{e.Code}|{e.ChronologicalAge}|{e.MinAge}-{e.MaxAge}|{e.View}|archetype"));

    public static string StyleCalibrationPackSha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(StyleCalibrationPackCanonical()));

    public static string Canonical() => string.Join('\n', new[]
    {
        "VISUAL_UNIVERSE_AUTHORITY_V1",
        "AuthorityId=" + AuthorityId,
        "ProjectId=" + ProjectId,
        "Version=" + Version,
        "StyleId=" + StyleId,
        "StyleName=" + StyleName,
        "StyleIntent=" + StyleIntent,
        "StylizationLevel=" + StylizationLevel,
        "RealismCeiling=" + RealismCeiling,
        "PhotorealismCeiling=" + PhotorealismCeiling,
        "PositiveConstraints=" + PositiveConstraints,
        "NegativeConstraints=" + NegativeConstraints,
        "StyleReferenceInstruction=" + StyleReferenceInstruction,
        DesignLanguageCanonical(),
        "StyleReferencePackSha=" + StyleReferencePackSha(),
        "StyleCalibrationPackSha=" + StyleCalibrationPackSha(),
        "WrappedPvsV1Sha=" + ProjectVisualStyleV2Rules.ProtectedV1Sha,
        "WrappedCdlV2Sha=" + CharacterDesignLanguageV2Rules.Sha(),
        "CharacterNameOwned=false",
        "RoleOwned=false",
        "AgeOwned=false",
        "GenderOwned=false",
        "PvsV1Replaced=false",
    });

    public static string Sha() =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(Canonical()));

    public static string AuthorityBlock() =>
        "[FAMIXA VISUAL UNIVERSE AUTHORITY V1] "
        + StyleIntent + " "
        + "A character is an instance of the FAMIXA visual language and must not redefine it. "
        + "Age changes maturity only. Gender changes attributes only. Role is narrative only. "
        + "Identity says who the character is. Character Design Language says how the character is drawn. "
        + "Stylization=" + StylizationLevel + ". RealismCeiling=" + RealismCeiling + ".";

    public static string RealismCeilingBlock() =>
        "[REALISM CEILING] " + RealismCeiling
        + ". Realistic lighting or material quality may exist. "
        + "Realistic human anatomy, skin micro-detail, and photographic facial rendering must not dominate. "
        + "High quality must never mean more photorealistic.";

    public static string DesignLanguageBlock() =>
        "[FAMIXA STRUCTURED CHARACTER DESIGN LANGUAGE] "
        + "FACE: " + FaceLanguage + " "
        + "EYES: " + EyeLanguage + " "
        + "HAIR: " + HairLanguage + " "
        + "BODY: " + BodyLanguage + " "
        + "PROPORTION: " + ProportionLanguage + " "
        + "MATERIAL: " + MaterialLanguage + " "
        + "LIGHTING: " + LightingLanguage + " "
        + "CAMERA: " + CameraLanguage + " "
        + "COLOR: " + ColorLanguage + " "
        + "ENVIRONMENT: " + EnvironmentLanguage + " "
        + CharacterDesignLanguageV2Rules.PromptBlock;

    public static string CompileStylePrefix(string? projectVisualStylePrompt = null) =>
        string.Join(" ", new[]
        {
            AuthorityBlock(),
            (projectVisualStylePrompt ?? "").Trim(),
            DesignLanguageBlock(),
            "[STYLE REFERENCE] " + StyleReferenceInstruction,
            RealismCeilingBlock(),
            "[GLOBAL NEGATIVE CONSTRAINTS] " + NegativeConstraints + ".",
        }.Where(x => x.Length > 0));

    public static string CompileCharacterPrompt(
        string? projectVisualStylePrompt,
        string? ageAppearance,
        string? identity,
        string? appearance,
        string? composition)
    {
        return string.Join(" ", new[]
        {
            CompileStylePrefix(projectVisualStylePrompt),
            (ageAppearance ?? "").Trim(),
            (identity ?? "").Trim(),
            (appearance ?? "").Trim(),
            (composition ?? "").Trim(),
        }.Where(x => x.Length > 0));
    }

    public static bool StylePrefixIdentical(string a, string b) =>
        string.Equals(ExtractStylePrefix(a), ExtractStylePrefix(b), StringComparison.Ordinal);

    public static string ExtractStylePrefix(string? prompt)
    {
        var text = prompt ?? "";
        var ageAt = text.IndexOf("ChronologicalAge:", StringComparison.Ordinal);
        var identityAt = text.IndexOf("Single canonical visual identity", StringComparison.Ordinal);
        var cut = new[] { ageAt, identityAt }.Where(i => i >= 0).DefaultIfEmpty(text.Length).Min();
        return text[..cut].Trim();
    }

    public static bool PromptHasAuthorityOrder(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        var a = prompt.IndexOf("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal);
        var b = prompt.IndexOf("[FAMIXA STRUCTURED CHARACTER DESIGN LANGUAGE]", StringComparison.Ordinal);
        var c = prompt.IndexOf("[REALISM CEILING]", StringComparison.Ordinal);
        var d = prompt.IndexOf("[GLOBAL NEGATIVE CONSTRAINTS]", StringComparison.Ordinal);
        var e = prompt.IndexOf("ChronologicalAge:", StringComparison.Ordinal);
        return a >= 0 && b > a && c > b && d > c && (e < 0 || e > d);
    }

    public static bool PromptBlocksAdultPhotorealEscalation(string? prompt) =>
        !string.IsNullOrWhiteSpace(prompt)
        && prompt.Contains(RealismCeiling, StringComparison.Ordinal)
        && prompt.Contains("not a photorealistic human", StringComparison.OrdinalIgnoreCase)
        && prompt.Contains("live action actor", StringComparison.OrdinalIgnoreCase)
        && prompt.Contains("High quality must never mean more photorealistic", StringComparison.Ordinal);

    public static string CompileCalibrationPrompt(StyleExemplar exemplar)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(exemplar.ChronologicalAge);
        var ageBlock = string.Join(" ",
            $"ChronologicalAge: {age.ChronologicalAge}.",
            $"TargetAppearanceAgeMin: {age.TargetAppearanceAgeMin}.",
            $"TargetAppearanceAgeMax: {age.TargetAppearanceAgeMax}.",
            $"AgeAppearanceProfile: stylized FAMIXA {exemplar.Label.ToLowerInvariant()} archetype.");
        var identity = $"Anonymous style exemplar. No name. No role. No story. Archetype: {exemplar.Label}.";
        return CompileCharacterPrompt(
            ProjectVisualStyleV1Rules.BuildPrompt(ProjectVisualStyleV1Rules.PresetOf(StyleId)!),
            ageBlock,
            identity,
            "",
            $"View: {exemplar.View}. Style calibration only.");
    }

    public static string? ValidateMasterGeneration(
        string? authorityStatus,
        string? requestAuthoritySha,
        string? requestDesignLanguageSha,
        string? requestReferenceSha,
        string? requestCalibrationSha,
        bool appearanceReady,
        bool ageReady,
        bool identityReady,
        bool calibrationApproved = true)
    {
        if (!IsAuthority(authorityStatus))
            return GateAuthorityNotLocked;
        if (!LookLikeSha(DesignLanguageSha()))
            return GateDesignLanguageNotReady;
        if (!LookLikeSha(StyleReferencePackSha()))
            return GateReferenceNotReady;
        if (!calibrationApproved)
            return GateCalibrationNotApproved;
        if (!SameSha(requestAuthoritySha, Sha()))
            return GateAuthorityShaMismatch;
        if (!SameSha(requestDesignLanguageSha, DesignLanguageSha()))
            return GateDesignLanguageShaMismatch;
        if (!SameSha(requestReferenceSha, StyleReferencePackSha())
            || !SameSha(requestCalibrationSha, StyleCalibrationPackSha()))
            return GateBindingMismatch;
        if (!appearanceReady) return CharacterAppearanceProfileV1Rules.GateNotReady;
        if (!ageReady) return GateAgeNotReady;
        if (!identityReady) return GateIdentityNotReady;
        return null;
    }

    public static bool MasterGenerationBlocked(string? authorityStatus) =>
        ValidateMasterGeneration(
            authorityStatus, Sha(), DesignLanguageSha(), StyleReferencePackSha(), StyleCalibrationPackSha(),
            true, true, true) is not null;

    public static bool BindingMatch(
        string? authoritySha, string? designLanguageSha, string? pvsSha,
        string? referenceSha, string? calibrationSha)
    {
        return SameSha(authoritySha, Sha())
            && SameSha(designLanguageSha, DesignLanguageSha())
            && LookLikeSha(pvsSha)
            && SameSha(referenceSha, StyleReferencePackSha())
            && SameSha(calibrationSha, StyleCalibrationPackSha());
    }

    public static bool ArtifactStale(
        bool officialLocked, string? artifactAuthoritySha, string? currentAuthoritySha, string? currentStatus) =>
        !officialLocked
        && IsAuthority(currentStatus)
        && LookLikeSha(currentAuthoritySha)
        && !SameSha(artifactAuthoritySha, currentAuthoritySha);

    public static bool MutationForbidden(bool officialLocked) => officialLocked;

    public static string? EvaluateCreate(string? status, bool confirm, bool projectReady)
    {
        if (!confirm) return GateConfirmation;
        if (!projectReady) return GateNotReady;
        if (IsOpen(status)) return GateOpenRevision;
        return MayRequest(status) ? null : GateInvalidState;
    }

    public static string? EvaluateCalibrate(string? status, bool confirm)
    {
        if (!confirm) return GateConfirmation;
        return MayCalibrate(status) ? null : GateInvalidState;
    }

    public static string? EvaluateAdvance(string? status, string action)
    {
        var a = (action ?? "").Trim().ToUpperInvariant();
        if (a == "APPROVE") return MayApprove(status) ? null : GateInvalidState;
        if (a == "REJECT") return MayReject(status) ? null : GateInvalidState;
        if (a == "LOCK")
            return MayLock(status) ? null : (status == StatusPendingReview ? GateNotApproved : GateInvalidState);
        return GateInvalidState;
    }

    public static bool ContainsCharacterName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Replace("CharacterNameOwned", "", StringComparison.OrdinalIgnoreCase);
        return Regex.IsMatch(normalized, @"\b(Minh|Nam|Linh|Thảo|Thao)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool ContainsRole(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && Regex.IsMatch(text, @"\b(Bố|Mẹ|Cô giáo|Main Child)\b", RegexOptions.IgnoreCase);

    public static bool LookLikeSha(string? sha) => ProjectVisualStyleV1Rules.LookLikeSha(sha);
    public static bool SameSha(string? a, string? b) => ProjectVisualStyleV1Rules.SameSha(a, b);

    public sealed record StyleExemplar(string Code, string Label, int ChronologicalAge, int MinAge, int MaxAge, string View);

    public sealed record AuthoritySnapshot(
        string Status,
        string Version,
        string CandidateSha,
        string? CurrentSha,
        string? DesignLanguageSha,
        string? StyleReferencePackSha,
        string? StyleCalibrationPackSha,
        string? Fingerprint,
        string? RequestedBy,
        string? ReviewedBy,
        string? ReviewDecision,
        string? RejectionReason,
        string? Notes,
        string? CalibrationStatus,
        IReadOnlyList<CalibrationSlotSnapshot>? Slots);

    public sealed record CalibrationSlotSnapshot(
        string Code,
        string Label,
        string? Path,
        string? Sha256,
        string Prompt);

    public static string Fingerprint(string? currentSha) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            document = DocumentId,
            version = Version,
            candidateSha = Sha(),
            currentSha = (currentSha ?? "").Trim().ToLowerInvariant(),
        }, Json)));

    public static AuthoritySnapshot? ReadSnapshot(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return null;
        var obj = JsonNode.Parse(rulesJson) as JsonObject;
        var node = obj?[ExtraKey];
        if (node is null) return null;
        return JsonSerializer.Deserialize<AuthoritySnapshot>(node.ToJsonString(), Json);
    }

    public static string MergeSnapshot(string? rulesJson, AuthoritySnapshot snap)
    {
        var obj = string.IsNullOrWhiteSpace(rulesJson)
            ? new JsonObject()
            : JsonNode.Parse(rulesJson) as JsonObject ?? new JsonObject();
        obj[ExtraKey] = JsonSerializer.SerializeToNode(snap, Json);
        return obj.ToJsonString(Json);
    }

    public static VisualUniverseAuthorityDto ToDto(string? status = null, bool currentAuthority = false) =>
        new(
            DocumentId,
            AuthorityId,
            Version,
            ProjectId,
            StyleId,
            StyleName,
            status ?? StatusDraft,
            currentAuthority && IsAuthority(status),
            Sha(),
            DesignLanguageSha(),
            StyleReferencePackSha(),
            StyleCalibrationPackSha(),
            ProjectVisualStyleV2Rules.ProtectedV1Sha,
            StylizationLevel,
            RealismCeiling,
            PhotorealismCeiling,
            StyleIntent,
            FaceLanguage,
            EyeLanguage,
            HairLanguage,
            BodyLanguage,
            MaterialLanguage,
            LightingLanguage,
            PositiveConstraints,
            NegativeConstraints,
            CompileStylePrefix(),
            MayRequest(status),
            MayCalibrate(status),
            MayApprove(status),
            MayReject(status),
            MayLock(status),
            ProviderCalled: false,
            GeminiCalled: false,
            AutoApprove: false,
            AutoLock: false,
            Persisted: false);

    public static VisualUniverseCalibrationDto ToCalibrationDto(
        IReadOnlyList<CalibrationSlotSnapshot>? slots = null,
        string? status = null) =>
        new(
            DocumentId,
            StyleCalibrationPackSha(),
            status ?? StatusDraft,
            CalibrationArchetypes.Select(e =>
            {
                var existing = slots?.FirstOrDefault(s => s.Code == e.Code);
                return new VisualUniverseCalibrationSlotDto(
                    e.Code, e.Label, e.ChronologicalAge, e.MinAge, e.MaxAge, e.View,
                    existing?.Path, existing?.Sha256, CompileCalibrationPrompt(e));
            }).ToList(),
            ProviderCalled: false,
            GeminiCalled: false,
            GenerationExecuted: false);
}

public static class CharacterStyleConformanceV1Rules
{
    public const string DocumentId = "FAMIXA_CHARACTER_STYLE_CONFORMANCE_V1";
    public const string StatusPass = "PASS";
    public const string StatusFail = "FAIL";
    public const string StatusNotEvaluated = "NOT_EVALUATED";

    public static bool FaceEqualsStyle() => false;
    public static bool CompilationImpliesPass() => false;
    public static bool InventsScore() => false;
    public static bool CallsGemini() => false;
    public static bool AutoApprove() => false;
    public static string StatusWithoutEvaluator() => StatusNotEvaluated;
    public static int? ScoreWithoutEvaluator() => null;

    public static bool ReadyAllowed(
        bool facePass, bool agePass, bool appearancePass, bool identityPass,
        string? styleStatus, bool universeShaMatch, bool designLanguageShaMatch) =>
        facePass && agePass && appearancePass && identityPass
        && string.Equals(styleStatus, StatusPass, StringComparison.OrdinalIgnoreCase)
        && universeShaMatch && designLanguageShaMatch;

    public static bool BlocksReadyOnFail(string? styleStatus, bool officialLocked) =>
        !officialLocked && string.Equals(styleStatus, StatusFail, StringComparison.OrdinalIgnoreCase);
}
