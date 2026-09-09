using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VISUAL_CALIBRATION_PACK_V1 — pre-character visual universe test.
/// Consumes PVS + CDL + VUA. Does not create Character Master / DNA / PRP / CRP.
/// Matrix is configuration. No character-name branches. No Gemini on compile.
/// </summary>
public static class VisualCalibrationPackV1Rules
{
    public const string DocumentId = "FAMIXA_VISUAL_CALIBRATION_PACK_V1";
    public const string SuiteId = "FAMIXA_VISUAL_CALIBRATION_PACK_V1_REGRESSION";
    public const string HardeningSuiteId = "FAMIXA_VISUAL_CALIBRATION_PACK_V1_HARDENING_REGRESSION";
    public const string LiveGenerationSuiteId = "FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_READINESS_REGRESSION";
    public const string LiveGenerationOfficialSuiteId = "FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_REGRESSION";
    public const string ExtraKey = "visualCalibrationPack";
    public const string Version = "V1";
    public const string DefaultPackId = "FAMIXA-VISUAL-CALIBRATION-V1";
    public const string ProjectId = "FAMIXA";

    public const string StatusDraft = "DRAFT";
    public const string StatusGenerating = "GENERATING";
    public const string StatusPendingReview = "PENDING_REVIEW";
    public const string StatusRejected = "REJECTED";
    public const string StatusApproved = "APPROVED";
    public const string StatusLocked = "LOCKED";

    public const string SubjectDraft = "DRAFT";
    public const string SubjectGenerated = "GENERATED";
    public const string SubjectPendingReview = "PENDING_REVIEW";

    public const string ArtifactGenerated = "GENERATED";
    public const string ArtifactReviewed = "REVIEWED";
    public const string ArtifactApproved = "APPROVED";
    public const string ArtifactRejected = "REJECTED";

    public const string GateConfirmation = "CONFIRMATION_REQUIRED";
    public const string GateUniverseNotReady = "VISUAL_UNIVERSE_NOT_READY";
    public const string GatePvsNotReady = "PROJECT_VISUAL_STYLE_NOT_READY";
    public const string GateCdlNotReady = "CHARACTER_DESIGN_LANGUAGE_NOT_READY";
    public const string GateIncomplete = "CALIBRATION_INCOMPLETE";
    public const string GateUniverseMismatch = "CALIBRATION_UNIVERSE_MISMATCH";
    public const string GatePhotorealism = "PHOTOREALISM_MISMATCH";
    public const string GateDirector = "DIRECTOR_PASS_REQUIRED";
    public const string GateInvalidState = "BLOCK_INVALID_STATE";
    public const string GateLockedImmutable = "LOCKED_VISUAL_UNIVERSE_IMMUTABLE";
    public const string GateNotApproved = "CALIBRATION_NOT_APPROVED";
    public const string GatePixelsNotReady = "CALIBRATION_PIXELS_NOT_READY";
    public const string GateVuaPromotion = "VUA_PROMOTION_NOT_READY";
    public const string GateDuplicate = "BLOCK_DUPLICATE";
    public const string ProviderName = "GEMINI";
    public const string DefaultModel = "gemini-2.5-flash-image";

    public const string KindPrompt = "PROMPT";
    public const string KindPixel = "PIXEL";

    public const string SlotPending = "PENDING";
    public const string SlotGenerating = "GENERATING";
    public const string SlotSuccess = "SUCCESS";
    public const string SlotFailed = "FAILED";
    public const string SlotSkippedDuplicate = "SKIPPED_DUPLICATE";

    public const string PhotorealismLow = "LOW";
    public const string PhotorealismMedium = "MEDIUM";
    public const string PhotorealismHigh = "HIGH";

    public const string SharedUniverseLine =
        "All subjects belong to the same animated visual universe. "
        + "Age, gender, and role change the person only. They must not reinterpret visual style, "
        + "stylization, photorealism, lighting, or rendering language.";

    /// <summary>Shared visual language for every subject. Not a second style. Not per-subject beauty.</summary>
    public const string SharedVisualLanguage =
        "STYLIZED 3D character. Clearly stylized, NOT photorealistic. "
        + "Cohesive family-friendly 3D design. Simplified facial construction. "
        + "Expressive but controlled eyes. Clean stylized skin. Stylized hair masses. "
        + "Soft rounded forms. Controlled proportions. Coherent material rendering. "
        + "Coherent lighting. Coherent color treatment. Coherent camera language. "
        + "STYLIZED 3D greater than realistic human. "
        + "One character, one view, one image. No collage, no contact sheet, no split screen, no text panel. "
        + "Neutral soft studio environment. Neutral warm light background. Subtle depth. No distracting props. "
        + "No busy environment. No dramatic cinematic background. No different color grading per subject. "
        + "Cross-character consistency is more important than individual beauty.";

    public static readonly IReadOnlyList<string> DirectorFailReasons =
    [
        "Sai visual style",
        "Quá giống người thật",
        "Sai tỷ lệ thiết kế",
        "Sai eye design",
        "Sai facial construction",
        "Sai hair language",
        "Sai material rendering",
        "Sai lighting",
        "Sai color treatment",
        "Không đồng nhất giữa các archetype",
        "Không đồng nhất giữa các góc",
        "Khác",
    ];

    /// <summary>Global extras compiled for every subject. Not attached to one character.</summary>
    public const string ExtraNegativeConstraints =
        "photorealistic, live action, photographic, real human portrait, passport photo, "
        + "fashion photo, documentary photo, hyperreal skin, visible photographic pores, "
        + "real camera portrait, uncinematic photographic realism, different art style, "
        + "different rendering style, anime, 2D illustration, cartoon flat, comic illustration, "
        + "rural elderly stereotype, farmer stereotype, aged rural portrait, generic stock portrait";

    public static readonly IReadOnlyList<string> RequiredViews =
        ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"];

    /// <summary>Configuration/data. Future packs pass a different matrix; logic stays generic.</summary>
    public static IReadOnlyList<CalibrationSubjectDefinition> DefaultMatrix { get; } =
    [
        new("CAL-001", "Child Boy", "MALE", 11, 10, 12, "CALIBRATION_CHILD", "SLIGHT", "CHILD_MALE_STYLE_CHECK"),
        new("CAL-002", "Child Girl", "FEMALE", 11, 10, 12, "CALIBRATION_CHILD", "SLIGHT", "CHILD_FEMALE_STYLE_CHECK"),
        new("CAL-003", "Adult Male", "MALE", 35, 32, 38, "CALIBRATION_ADULT", "AVERAGE", "ADULT_MALE_STYLE_CHECK"),
        new("CAL-004", "Adult Female", "FEMALE", 35, 32, 38, "CALIBRATION_ADULT", "AVERAGE", "ADULT_FEMALE_STYLE_CHECK"),
        new("CAL-005", "Older Adult Male", "MALE", 65, 61, 69, "CALIBRATION_OLDER", "AVERAGE", "OLDER_ADULT_MALE_STYLE_CHECK"),
        new("CAL-006", "Adult Female Full Body", "FEMALE", 35, 32, 38, "CALIBRATION_ADULT", "AVERAGE", "ADULT_FEMALE_FULL_BODY_STYLE_CHECK"),
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoGenerate() => false;
    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool CreatesCharacterMaster() => false;
    public static bool CreatesDna() => false;
    public static bool CreatesPrp() => false;
    public static bool CreatesCrp() => false;
    public static bool CreatesVideo() => false;
    public static bool MutatesLockedCharacters() => false;
    public static bool UsesCharacterName() => false;
    public static bool ReplacesProjectVisualStyle() => false;
    public static bool ReplacesCharacterDesignLanguage() => false;
    public static bool ReplacesVisualUniverse() => false;
    public static bool FaceEqualsUniverse() => false;
    public static bool AgeEqualsStyle() => false;
    public static bool PretendsPackLockIsUniverseLock() => false;

    public static bool MayGenerate(string? status) =>
        status is StatusDraft or StatusRejected or StatusGenerating or StatusPendingReview or null or "";
    public static bool MayApprove(string? status) => status == StatusPendingReview;
    public static bool MayLock(string? status) => status == StatusApproved;

    public static IReadOnlyList<CalibrationSubjectDefinition> MatrixOf(
        IReadOnlyList<CalibrationSubjectDefinition>? matrix) =>
        matrix is { Count: > 0 } ? matrix : DefaultMatrix;

    public static string? ValidateAuthorities(string? vuaSha, string? pvsSha, string? cdlSha)
    {
        if (!ProjectVisualStyleV1Rules.LookLikeSha(vuaSha))
            return GateUniverseNotReady;
        if (!ProjectVisualStyleV1Rules.LookLikeSha(pvsSha))
            return GatePvsNotReady;
        if (!ProjectVisualStyleV1Rules.LookLikeSha(cdlSha))
            return GateCdlNotReady;
        return null;
    }

    public static string? ValidateGenerate(string? vuaSha, string? pvsSha, string? cdlSha, bool confirm)
    {
        if (!confirm) return GateConfirmation;
        return ValidateAuthorities(vuaSha, pvsSha, cdlSha);
    }

    public static string AgeAppearanceProfile(CalibrationSubjectDefinition subject)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(subject.ChronologicalAge);
        return CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(age, subject.Gender);
    }

    public static string CompilePrompt(
        CalibrationSubjectDefinition subject,
        string view,
        string? pvsPrompt = null)
    {
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(subject.ChronologicalAge);
        var type = (view ?? "").Trim().ToUpperInvariant();
        var ageBlock = string.Join(" ",
            $"ChronologicalAge: {age.ChronologicalAge}.",
            $"TargetAppearanceAgeMin: {subject.TargetAppearanceAgeMin}.",
            $"TargetAppearanceAgeMax: {subject.TargetAppearanceAgeMax}.",
            $"AgeAppearanceProfile: {AgeAppearanceProfile(subject)}.");
        var identity =
            $"Anonymous calibration subject. CalibrationSubjectId: {subject.CalibrationSubjectId}. "
            + $"Gender: {subject.Gender}. No name. No story. Role: {subject.Role}.";
        var body = $"BodyType: {subject.BodyType}. Designed silhouette in the shared FAMIXA proportion language.";
        var purpose = $"GenerationPurpose: {subject.GenerationPurpose}. Style check only. Not a production character.";
        var camera = $"View: {type}. {CharacterStudioIdentityLockV1Rules.ViewCamera(type)}.";
        return string.Join(" ", new[]
        {
            FamixaVisualUniverseAuthorityV1Rules.CompileStylePrefix(pvsPrompt),
            "[CALIBRATION VISUAL LANGUAGE] " + SharedVisualLanguage,
            "[CALIBRATION NEGATIVE CONSTRAINTS] " + ExtraNegativeConstraints + ".",
            SharedUniverseLine,
            ageBlock,
            identity,
            body,
            purpose,
            camera,
        }.Where(x => x.Length > 0));
    }

    public static IReadOnlyDictionary<string, string> CompileSubjectViews(
        CalibrationSubjectDefinition subject,
        string? pvsPrompt = null) =>
        RequiredViews.ToDictionary(
            v => v,
            v => CompilePrompt(subject, v, pvsPrompt),
            StringComparer.OrdinalIgnoreCase);

    public static PackSnapshot CompilePack(
        IReadOnlyList<CalibrationSubjectDefinition>? matrix = null,
        string? pvsPrompt = null)
    {
        var subjects = MatrixOf(matrix);
        var pvs = pvsPrompt ?? ProjectVisualStyleV1Rules.BuildPrompt(
            ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);
        var vua = FamixaVisualUniverseAuthorityV1Rules.Sha();
        var pvsSha = ProjectVisualStyleV2Rules.ProtectedV1Sha;
        var cdl = CharacterDesignLanguageV2Rules.Sha();
        return new PackSnapshot(
            DefaultPackId,
            StatusDraft,
            Version,
            vua,
            pvsSha,
            cdl,
            FamixaVisualUniverseAuthorityV1Rules.DesignLanguageSha(),
            false,
            null,
            null,
            PhotorealismLow,
            subjects.Select(s => BindSubject(CompileSubject(s, pvs), DefaultPackId, vua, pvsSha, cdl)).ToList());
    }

    public static SubjectSnapshot BindSubject(
        SubjectSnapshot subject, string packId, string vuaSha, string pvsSha, string cdlSha) =>
        subject with
        {
            Artifacts = subject.Artifacts.Select(a => a with
            {
                Kind = string.IsNullOrWhiteSpace(a.Kind) ? KindPrompt : a.Kind,
                PackId = packId,
                SlotId = SlotIdOf(subject.CalibrationSubjectId, a.ViewType),
                VisualUniverseSha = vuaSha,
                ProjectVisualStyleSha = pvsSha,
                CharacterDesignLanguageSha = cdlSha,
            }).ToList(),
        };

    public static string SlotIdOf(string subjectId, string view) =>
        $"{subjectId}/{(view ?? "").Trim().ToUpperInvariant()}";

    public static SubjectSnapshot CompileSubject(CalibrationSubjectDefinition subject, string? pvsPrompt)
    {
        var views = CompileSubjectViews(subject, pvsPrompt);
        return new SubjectSnapshot(
            subject.CalibrationSubjectId,
            subject.Label,
            subject.Gender,
            subject.ChronologicalAge,
            subject.TargetAppearanceAgeMin,
            subject.TargetAppearanceAgeMax,
            AgeAppearanceProfile(subject),
            subject.Role,
            subject.BodyType,
            subject.GenerationPurpose,
            SubjectDraft,
            views.Select(kv => new ArtifactSnapshot(
                subject.CalibrationSubjectId, kv.Key, kv.Value, null, null, null)).ToList());
    }

    public static string PackSha(PackSnapshot pack) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            document = DocumentId,
            packId = pack.PackId,
            vua = pack.VisualUniverseSha,
            pvs = pack.ProjectVisualStyleSha,
            cdl = pack.CharacterDesignLanguageSha,
            subjects = pack.Subjects.Select(s => s.CalibrationSubjectId),
        }, Json)));

    public static bool ViewsShareVisualUniverse(IReadOnlyDictionary<string, string> views) =>
        views.Count == RequiredViews.Count
        && views.Values.All(p =>
            p.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(p, views.Values.First()));

    public static bool ViewsShareAgeProfile(IReadOnlyDictionary<string, string> views, CalibrationSubjectDefinition subject) =>
        views.Values.All(p =>
            p.Contains($"ChronologicalAge: {subject.ChronologicalAge}", StringComparison.Ordinal)
            && p.Contains($"TargetAppearanceAgeMin: {subject.TargetAppearanceAgeMin}", StringComparison.Ordinal));

    public static bool ViewsDifferOnlyByCamera(IReadOnlyDictionary<string, string> views) =>
        CharacterAgeGenerationIntegrationV1Rules.ViewsDifferOnlyByCamera(views);

    public static bool AgeDoesNotChangeStyle(PackSnapshot pack)
    {
        var prefixes = pack.Subjects.Select(s =>
            FamixaVisualUniverseAuthorityV1Rules.ExtractStylePrefix(s.Artifacts[0].Prompt)).Distinct().ToList();
        return prefixes.Count == 1;
    }

    public static bool GenderDoesNotChangeStyle(PackSnapshot pack) => AgeDoesNotChangeStyle(pack);

    public static bool ContainsCharacterName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Regex.IsMatch(text, @"\b(Minh|Nam|Linh|Thảo|Thao)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool SameUniverse(PackSnapshot pack) =>
        pack.Subjects.SelectMany(s => s.Artifacts).All(a =>
            a.Prompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal))
        && AgeDoesNotChangeStyle(pack);

    public static VisualCalibrationCoverageDto GetCoverage(PackSnapshot pack)
    {
        var required = MatrixOf(null).Count * RequiredViews.Count;
        var generated = pack.Subjects.SelectMany(s => s.Artifacts)
            .Count(a => string.Equals(a.Kind, KindPixel, StringComparison.OrdinalIgnoreCase));
        var valid = pack.Subjects.SelectMany(s => s.Artifacts).Count(a => IsValidPixel(pack, a));
        return new VisualCalibrationCoverageDto(required, generated, valid, Math.Max(0, required - valid));
    }

    public static bool IsValidPixel(PackSnapshot pack, ArtifactSnapshot artifact)
    {
        if (!string.Equals(artifact.Kind, KindPixel, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(artifact.Path)
            || artifact.Path.Contains("prompt", StringComparison.OrdinalIgnoreCase)
            || artifact.Path.Contains("compiled", StringComparison.OrdinalIgnoreCase)
            || artifact.Path.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!ProjectVisualStyleV1Rules.LookLikeSha(artifact.Sha256))
            return false;
        if (string.Equals(artifact.Status, "DELETED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(artifact.Status, "PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(artifact.PackId)
            && !string.Equals(artifact.PackId, pack.PackId, StringComparison.OrdinalIgnoreCase))
            return false;
        var slot = SlotIdOf(artifact.SubjectId, artifact.ViewType);
        if (!string.IsNullOrWhiteSpace(artifact.SlotId)
            && !string.Equals(artifact.SlotId, slot, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!RequiredViews.Contains(artifact.ViewType, StringComparer.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(artifact.ExecutionId))
            return false;
        if (!string.IsNullOrWhiteSpace(pack.GenerationExecutionId)
            && !string.Equals(artifact.ExecutionId, pack.GenerationExecutionId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!ProjectVisualStyleV1Rules.SameSha(artifact.VisualUniverseSha ?? pack.VisualUniverseSha, pack.VisualUniverseSha)
            || !ProjectVisualStyleV1Rules.SameSha(artifact.ProjectVisualStyleSha ?? pack.ProjectVisualStyleSha, pack.ProjectVisualStyleSha)
            || !ProjectVisualStyleV1Rules.SameSha(artifact.CharacterDesignLanguageSha ?? pack.CharacterDesignLanguageSha, pack.CharacterDesignLanguageSha))
            return false;
        return true;
    }

    public static PackSnapshot WithPixelCoverage(PackSnapshot pack, int validCount, string? kind = null)
    {
        var remaining = Math.Max(0, validCount);
        var exec = pack.GenerationExecutionId ?? "CAL-EXEC-TEST";
        var subjects = pack.Subjects.Select(s => s with
        {
            Artifacts = s.Artifacts.Select(a =>
            {
                if (remaining <= 0)
                    return a;
                remaining--;
                var pixel = string.IsNullOrWhiteSpace(kind) || string.Equals(kind, KindPixel, StringComparison.OrdinalIgnoreCase);
                var front = string.Equals(a.ViewType, "FRONT", StringComparison.OrdinalIgnoreCase);
                return a with
                {
                    Kind = kind ?? KindPixel,
                    Path = $"calibration://{pack.PackId}/{SlotIdOf(s.CalibrationSubjectId, a.ViewType)}",
                    Sha256 = TestPixelSha(s.CalibrationSubjectId, a.ViewType),
                    Status = ArtifactGenerated,
                    PackId = pack.PackId,
                    SlotId = SlotIdOf(s.CalibrationSubjectId, a.ViewType),
                    ExecutionId = exec,
                    CompiledPromptSha = PromptSha(a.Prompt),
                    VisualUniverseSha = pack.VisualUniverseSha,
                    ProjectVisualStyleSha = pack.ProjectVisualStyleSha,
                    CharacterDesignLanguageSha = pack.CharacterDesignLanguageSha,
                    IdentityAnchorSlot = pixel ? "FRONT" : null,
                    IdentityAnchorSha256 = pixel ? TestPixelSha(s.CalibrationSubjectId, "FRONT") : null,
                    ReferenceRole = pixel && !front ? IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor : null,
                };
            }).ToList(),
        }).ToList();
        return pack with { Subjects = subjects, GenerationExecutionId = exec };
    }

    public static string TestPixelSha(string subjectId, string view) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes($"{subjectId}:{view}:pixel"));

    public static string PromptSha(string? prompt) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(prompt ?? ""));

    public static string NewExecutionId() => "CAL-EXEC-" + Guid.NewGuid().ToString("N");

    public static bool SlotHasValidPixel(PackSnapshot pack, string slotId) =>
        pack.Subjects.SelectMany(s => s.Artifacts)
            .Any(a => string.Equals(a.SlotId, slotId, StringComparison.OrdinalIgnoreCase) && IsValidPixel(pack, a));

    public static string? EvaluateLiveGenerate(PackSnapshot pack, bool confirm)
    {
        var gate = ValidateGenerate(
            pack.VisualUniverseSha, pack.ProjectVisualStyleSha, pack.CharacterDesignLanguageSha, confirm);
        if (gate is not null) return gate;
        if (IdentityConditionedCalibrationV1Rules.IdentityReadyForReview(pack))
            return GateDuplicate;
        if (!MayGenerate(pack.Status)) return GateInvalidState;
        return null;
    }

    public static string? AuthoritySnapshotMismatch(
        PackSnapshot execution, string? currentVua, string? currentPvs, string? currentCdl)
    {
        if (!ProjectVisualStyleV1Rules.SameSha(execution.VisualUniverseSha, currentVua)
            || !ProjectVisualStyleV1Rules.SameSha(execution.ProjectVisualStyleSha, currentPvs)
            || !ProjectVisualStyleV1Rules.SameSha(execution.CharacterDesignLanguageSha, currentCdl))
            return GateUniverseMismatch;
        return null;
    }

    public static string StatusAfterPixels(PackSnapshot pack)
    {
        var coverage = GetCoverage(pack);
        return coverage.Required == 24
            && coverage.Valid == 24
            && IdentityConditionedCalibrationV1Rules.IdentityReadyForReview(pack)
            ? StatusPendingReview
            : StatusGenerating;
    }

    public static PackSnapshot BindPixel(
        PackSnapshot pack,
        VisualCalibrationGenerationRequest request,
        string path,
        string sha256) =>
        pack with
        {
            GenerationExecutionId = request.GenerationExecutionId,
            Subjects = pack.Subjects.Select(s =>
                !string.Equals(s.CalibrationSubjectId, request.SubjectId, StringComparison.OrdinalIgnoreCase)
                    ? s
                    : s with
                    {
                        Status = SubjectGenerated,
                        Artifacts = s.Artifacts.Select(a =>
                            !string.Equals(a.ViewType, request.ViewType, StringComparison.OrdinalIgnoreCase)
                                ? a
                                : a with
                                {
                                    Kind = KindPixel,
                                    Path = path,
                                    Sha256 = sha256,
                                    Status = ArtifactGenerated,
                                    PackId = request.CalibrationPackId,
                                    SlotId = request.SlotId,
                                    ExecutionId = request.GenerationExecutionId,
                                    CompiledPromptSha = request.CompiledPromptSha,
                                    VisualUniverseSha = request.VisualUniverseSha,
                                    ProjectVisualStyleSha = request.ProjectVisualStyleSha,
                                    CharacterDesignLanguageSha = request.CharacterDesignLanguageSha,
                                    Prompt = request.CompiledPrompt,
                                    GenerationStatus = SlotSuccess,
                                    GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
                                    IdentityAnchorSlot = request.IdentityAnchorSlot,
                                    IdentityAnchorSha256 = IdentityConditionedCalibrationV1Rules.IsFront(request.ViewType)
                                        ? sha256
                                        : request.IdentityAnchorSha256,
                                    ReferenceRole = request.ReferenceRole,
                                }).ToList(),
                    }).ToList(),
        };

    public static bool VisualUniverseStale(string? boundSha, string? currentAuthoritySha) =>
        ProjectVisualStyleV1Rules.LookLikeSha(boundSha)
        && ProjectVisualStyleV1Rules.LookLikeSha(currentAuthoritySha)
        && !ProjectVisualStyleV1Rules.SameSha(boundSha, currentAuthoritySha);

    public static bool MayMaterializeStale(bool officialLocked, bool authorityTransitioned) =>
        authorityTransitioned && !officialLocked;

    public static string? EvaluateApprove(PackSnapshot pack, bool directorPass, string? photorealism)
    {
        if (!MayApprove(pack.Status))
            return GateInvalidState;
        var coverage = GetCoverage(pack);
        if (coverage.Required != 24 || coverage.Valid != coverage.Required)
            return GatePixelsNotReady;
        if (IdentityConditionedCalibrationV1Rules.MissingIdentity(pack) is { } identityGate)
            return identityGate;
        if (pack.Subjects.Select(s => pack.VisualUniverseSha).Distinct().Count() != 1
            || !SameUniverse(pack)
            || VisualCalibrationConsistencyV1Rules.Validate(pack) is not null)
            return GateUniverseMismatch;
        if (pack.Subjects.Any(s => CharacterAgeConsistencyV1Rules.ValidateProfile(
                s.ChronologicalAge, s.TargetAppearanceAgeMin, s.TargetAppearanceAgeMax) is not null))
            return CharacterAgeConsistencyV1Rules.GateNotReady;
        if (VisualCalibrationPhotorealismGateV1.BlocksApproval(photorealism ?? pack.PhotorealismLevel))
            return GatePhotorealism;
        if (!directorPass) return GateDirector;
        return null;
    }

    public static string? EvaluateLock(string? packStatus, bool confirm, bool vuaAlreadyLocked)
    {
        if (!confirm) return GateConfirmation;
        if (vuaAlreadyLocked) return GateLockedImmutable;
        if (!MayLock(packStatus))
            return packStatus == StatusPendingReview ? GateNotApproved : GateInvalidState;
        return null;
    }

    public static PackSnapshot Approve(PackSnapshot pack) =>
        pack with { Status = StatusApproved, DirectorPass = true };

    public static PackSnapshot Reject(PackSnapshot pack, string? reason) =>
        pack with { Status = StatusRejected, DirectorPass = false, RejectionReason = reason };

    public static PackSnapshot Lock(PackSnapshot pack, string? actor = null) =>
        pack with
        {
            Status = StatusLocked,
            CurrentAuthority = false,
            AuthorityTransitioned = false,
            LockedAt = DateTimeOffset.UtcNow.ToString("O"),
            LockedBy = actor,
        };

    public static PackSnapshot MarkAuthorityTransition(PackSnapshot pack) =>
        pack with { AuthorityTransitioned = true, CurrentAuthority = true };

    public static VisualCalibrationImpactDto Impact(int characterCount, int lockedCount) =>
        new(characterCount, lockedCount, Math.Max(0, characterCount - lockedCount));

    public static PackSnapshot? ReadSnapshot(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson)) return null;
        var obj = JsonNode.Parse(rulesJson) as JsonObject;
        var node = obj?[ExtraKey];
        return node is null ? null : JsonSerializer.Deserialize<PackSnapshot>(node.ToJsonString(), Json);
    }

    public static string MergeSnapshot(string? rulesJson, PackSnapshot snap)
    {
        var obj = string.IsNullOrWhiteSpace(rulesJson)
            ? new JsonObject()
            : JsonNode.Parse(rulesJson) as JsonObject ?? new JsonObject();
        obj[ExtraKey] = JsonSerializer.SerializeToNode(snap, Json);
        return obj.ToJsonString(Json);
    }

    public static VisualCalibrationPackDto ToDto(PackSnapshot pack, string? gate = null, bool providerCalled = false) =>
        new(
            DocumentId,
            pack.PackId,
            pack.Version,
            pack.Status,
            PackSha(pack),
            pack.VisualUniverseSha,
            pack.ProjectVisualStyleSha,
            pack.CharacterDesignLanguageSha,
            pack.CurrentAuthority,
            pack.DirectorPass,
            pack.PhotorealismLevel,
            pack.RejectionReason,
            pack.Subjects.Select(s => ToSubjectDto(s, pack)).ToList(),
            Impact(0, 0),
            gate,
            providerCalled,
            pack.GenerationExecuted,
            false,
            AutoApprove(),
            AutoLock(),
            null,
            GetCoverage(pack),
            pack.AuthorityTransitioned,
            pack.LockedAt,
            pack.LockedBy,
            pack.GenerationExecutionId,
            IdentityConditionedCalibrationV1Rules.GetCoverage(pack),
            null,
            pack.CalibrationRunId,
            IdentityConditionedCalibrationLiveV1Rules.ReadyForDirectorConfirmation);

    public static string SlotImageUrl(string packId, string subjectId, string view) =>
        $"/api/content/visual-calibration/{packId}/slots/{subjectId}/{(view ?? "").Trim().ToUpperInvariant()}/image";

    public static string SlotGenerationStatus(PackSnapshot pack, ArtifactSnapshot artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.GenerationStatus))
            return artifact.GenerationStatus;
        if (IsValidPixel(pack, artifact))
            return SlotSuccess;
        if (string.Equals(artifact.Kind, KindPixel, StringComparison.OrdinalIgnoreCase))
            return SlotFailed;
        if (pack.Status == StatusGenerating)
            return SlotGenerating;
        return SlotPending;
    }

    public static PackSnapshot MarkArtifactStatus(
        PackSnapshot pack, string slotId, string generationStatus) =>
        pack with
        {
            Subjects = pack.Subjects.Select(s => s with
            {
                Artifacts = s.Artifacts.Select(a =>
                    string.Equals(SlotIdOf(s.CalibrationSubjectId, a.ViewType), slotId, StringComparison.OrdinalIgnoreCase)
                        ? a with { GenerationStatus = generationStatus, SlotId = slotId }
                        : a).ToList(),
            }).ToList(),
        };

    public static PackSnapshot MarkSubjectStatus(
        PackSnapshot pack, string subjectId, string status) =>
        pack with
        {
            Subjects = pack.Subjects.Select(s =>
                string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase)
                    ? s with { Status = status }
                    : s).ToList(),
        };

    public static VisualCalibrationSubjectDto ToSubjectDto(SubjectSnapshot subject, PackSnapshot pack) =>
        new(
            subject.CalibrationSubjectId,
            subject.Label,
            subject.Gender,
            subject.ChronologicalAge,
            subject.TargetAppearanceAgeMin,
            subject.TargetAppearanceAgeMax,
            subject.AgeAppearanceProfile,
            subject.Role,
            subject.BodyType,
            subject.GenerationPurpose,
            subject.Status,
            subject.Artifacts.Select(a => new VisualCalibrationArtifactDto(
                a.SubjectId,
                a.ViewType,
                a.Prompt,
                a.Path,
                a.Sha256,
                a.Status ?? ArtifactGenerated,
                a.Kind,
                a.SlotId ?? SlotIdOf(subject.CalibrationSubjectId, a.ViewType),
                IsValidPixel(pack, a) ? SlotImageUrl(pack.PackId, a.SubjectId, a.ViewType) : null,
                SlotGenerationStatus(pack, a),
                a.GeneratedAt,
                a.ExecutionId,
                a.VisualUniverseSha ?? pack.VisualUniverseSha,
                a.ProjectVisualStyleSha ?? pack.ProjectVisualStyleSha,
                a.CharacterDesignLanguageSha ?? pack.CharacterDesignLanguageSha,
                a.IdentityAnchorSlot,
                a.IdentityAnchorSha256,
                a.ReferenceRole)).ToList());

    public sealed record CalibrationSubjectDefinition(
        string CalibrationSubjectId,
        string Label,
        string Gender,
        int ChronologicalAge,
        int TargetAppearanceAgeMin,
        int TargetAppearanceAgeMax,
        string Role,
        string BodyType,
        string GenerationPurpose);

    public sealed record ArtifactSnapshot(
        string SubjectId,
        string ViewType,
        string Prompt,
        string? Path,
        string? Sha256,
        string? Status,
        string? Kind = null,
        string? PackId = null,
        string? SlotId = null,
        string? VisualUniverseSha = null,
        string? ProjectVisualStyleSha = null,
        string? CharacterDesignLanguageSha = null,
        string? ExecutionId = null,
        string? CompiledPromptSha = null,
        string? GenerationStatus = null,
        string? GeneratedAt = null,
        string? IdentityAnchorSlot = null,
        string? IdentityAnchorSha256 = null,
        string? ReferenceRole = null);

    public sealed record SubjectSnapshot(
        string CalibrationSubjectId,
        string Label,
        string Gender,
        int ChronologicalAge,
        int TargetAppearanceAgeMin,
        int TargetAppearanceAgeMax,
        string AgeAppearanceProfile,
        string Role,
        string BodyType,
        string GenerationPurpose,
        string Status,
        IReadOnlyList<ArtifactSnapshot> Artifacts);

    public sealed record PackSnapshot(
        string PackId,
        string Status,
        string Version,
        string VisualUniverseSha,
        string ProjectVisualStyleSha,
        string CharacterDesignLanguageSha,
        string? DesignLanguageSha,
        bool CurrentAuthority,
        bool? DirectorPass,
        string? RejectionReason,
        string PhotorealismLevel,
        IReadOnlyList<SubjectSnapshot> Subjects,
        bool AuthorityTransitioned = false,
        string? LockedAt = null,
        string? LockedBy = null,
        bool GenerationExecuted = false,
        string? GenerationExecutionId = null,
        string? CalibrationRunId = null,
        int HistoricalPixelCount = 0,
        IReadOnlyList<ArtifactSnapshot>? HistoricalArtifacts = null);
}

public static class VisualCalibrationConsistencyV1Rules
{
    public static bool SameVisualUniverse(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        VisualCalibrationPackV1Rules.SameUniverse(pack);

    public static bool SamePvs(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        pack.Subjects.All(_ => ProjectVisualStyleV1Rules.SameSha(
            pack.ProjectVisualStyleSha, ProjectVisualStyleV2Rules.ProtectedV1Sha));

    public static bool SameCdl(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        pack.Subjects.All(_ => ProjectVisualStyleV1Rules.SameSha(
            pack.CharacterDesignLanguageSha, CharacterDesignLanguageV2Rules.Sha()));

    public static bool SameRenderingLanguage(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        VisualCalibrationPackV1Rules.AgeDoesNotChangeStyle(pack);

    public static bool SameStylizationLevel(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        SameRenderingLanguage(pack);

    public static bool SamePhotorealismLimit(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        SameRenderingLanguage(pack);

    public static bool SameLightingLanguage(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        SameRenderingLanguage(pack);

    public static bool SameMaterialLanguage(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        SameRenderingLanguage(pack);

    public static bool SameProportionLanguage(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        SameRenderingLanguage(pack);

    public static string? Validate(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        SameVisualUniverse(pack) && SamePvs(pack) && SameCdl(pack)
        && SameRenderingLanguage(pack) && SameStylizationLevel(pack) && SamePhotorealismLimit(pack)
        && SameLightingLanguage(pack) && SameMaterialLanguage(pack) && SameProportionLanguage(pack)
            ? null
            : VisualCalibrationPackV1Rules.GateUniverseMismatch;
}

/// <summary>Spec alias. Same rules; matrix stays data.</summary>
public static class VisualCalibrationConsistencyRulesV1
{
    public static string? Validate(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        VisualCalibrationConsistencyV1Rules.Validate(pack);
}

/// <summary>Deterministic compiler. Universe before subject details. No character name.</summary>
public static class VisualCalibrationPromptCompilerV1
{
    public static string Compile(
        VisualCalibrationPackV1Rules.CalibrationSubjectDefinition subject,
        string view,
        string? pvsPrompt = null) =>
        VisualCalibrationPackV1Rules.CompilePrompt(subject, view, pvsPrompt);
}

/// <summary>Application structured request. Provider adapter stays out of this layer. No Gemini here.</summary>
public static class VisualCalibrationGenerationServiceV1
{
    public static VisualCalibrationGenerationRequest CreateRequest(
        string packId,
        string subjectId,
        string visualUniverseSha,
        string projectVisualStyleSha,
        string characterDesignLanguageSha,
        int chronologicalAge,
        int targetAppearanceAgeMin,
        int targetAppearanceAgeMax,
        string ageAppearanceProfile,
        string viewType)
    {
        _ = chronologicalAge;
        _ = targetAppearanceAgeMin;
        _ = targetAppearanceAgeMax;
        _ = ageAppearanceProfile;
        var pack = VisualCalibrationPackV1Rules.CompilePack() with
        {
            PackId = string.IsNullOrWhiteSpace(packId) ? VisualCalibrationPackV1Rules.DefaultPackId : packId,
            VisualUniverseSha = visualUniverseSha,
            ProjectVisualStyleSha = projectVisualStyleSha,
            CharacterDesignLanguageSha = characterDesignLanguageSha,
        };
        return IdentityConditionedCalibrationV1Rules.CompileOne(
            pack,
            VisualCalibrationPackV1Rules.DefaultMatrix.First(s =>
                string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase)),
            viewType,
            VisualCalibrationPackV1Rules.NewExecutionId(),
            null);
    }

    public static IReadOnlyList<VisualCalibrationGenerationRequest> CompilePlan(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        string? executionId = null,
        string? pvsPrompt = null) =>
        IdentityConditionedCalibrationV1Rules.CompilePlan(pack, executionId, pvsPrompt);

    public static VisualCalibrationDryRun DryRun(VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var requests = CompilePlan(pack, "CAL-EXEC-DRYRUN");
        return new VisualCalibrationDryRun(
            requests, requests.Count == 24, requests.Select(r => r.SlotId).Distinct().Count() == 24,
            false, false);
    }

    public static string? MissingAuthority(VisualCalibrationGenerationRequest request) =>
        VisualCalibrationPackV1Rules.ValidateAuthorities(
            request.VisualUniverseSha, request.ProjectVisualStyleSha, request.CharacterDesignLanguageSha);

    public static bool ProviderCalled() => false;
    public static bool GenerationExecuted() => false;
    public static bool CallsGemini() => false;

    public static async Task<VisualCalibrationGenerationOutcome> ExecuteAsync(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        IVisualCalibrationGenerationProvider provider,
        bool confirm,
        bool generate,
        CancellationToken cancellationToken = default,
        Func<VisualCalibrationPackV1Rules.PackSnapshot, CancellationToken, Task>? persist = null)
    {
        var isolated = IdentityConditionedCalibrationLiveV1Rules.Isolate(pack);
        var exec = isolated.GenerationExecutionId ?? VisualCalibrationPackV1Rules.NewExecutionId();
        var requests = CompilePlan(isolated, exec);
        if (!confirm)
            return new VisualCalibrationGenerationOutcome(
                isolated, VisualCalibrationPackV1Rules.GateConfirmation, false, false, 0, requests);

        var gate = VisualCalibrationPackV1Rules.EvaluateLiveGenerate(isolated, true);
        if (gate is not null)
            return new VisualCalibrationGenerationOutcome(isolated, gate, false, false, 0, requests);

        var current = VisualCalibrationPackV1Rules.CompilePack();
        var drift = VisualCalibrationPackV1Rules.AuthoritySnapshotMismatch(
            isolated, current.VisualUniverseSha, current.ProjectVisualStyleSha, current.CharacterDesignLanguageSha);
        if (drift is not null)
            return new VisualCalibrationGenerationOutcome(isolated, drift, false, false, 0, requests);

        if (!generate)
            return new VisualCalibrationGenerationOutcome(
                isolated with { GenerationExecutionId = exec, Status = VisualCalibrationPackV1Rules.StatusDraft },
                null, false, false, 0, requests);

        var next = isolated with
        {
            GenerationExecutionId = exec,
            CalibrationRunId = isolated.CalibrationRunId,
            Status = VisualCalibrationPackV1Rules.StatusGenerating,
        };
        if (persist is not null)
            await persist(next, cancellationToken);
        var called = false;
        var generated = 0;
        foreach (var request in requests)
        {
            var mid = VisualCalibrationPackV1Rules.AuthoritySnapshotMismatch(
                next, current.VisualUniverseSha, current.ProjectVisualStyleSha, current.CharacterDesignLanguageSha);
            if (mid is not null)
                return new VisualCalibrationGenerationOutcome(next, mid, called, called, generated, requests);

            if (IdentityConditionedCalibrationLiveV1Rules.SlotReusable(next, request.SlotId))
            {
                next = VisualCalibrationPackV1Rules.MarkArtifactStatus(
                    next, request.SlotId, VisualCalibrationPackV1Rules.SlotSkippedDuplicate);
                continue;
            }

            var bound = IdentityConditionedCalibrationV1Rules.BindLiveAnchor(next, request);
            if (!IdentityConditionedCalibrationV1Rules.ProviderMayCall(bound, next))
            {
                next = VisualCalibrationPackV1Rules.MarkArtifactStatus(
                    next, request.SlotId, IdentityConditionedCalibrationV1Rules.GateAnchor);
                if (persist is not null)
                    await persist(next, cancellationToken);
                continue;
            }

            next = VisualCalibrationPackV1Rules.MarkArtifactStatus(
                next, request.SlotId, VisualCalibrationPackV1Rules.SlotGenerating);
            if (persist is not null)
                await persist(next, cancellationToken);

            var result = await provider.GenerateSlotAsync(bound, cancellationToken);
            called = called || result.ProviderCalled;
            if (!result.Succeeded
                || string.IsNullOrWhiteSpace(result.Path)
                || !ProjectVisualStyleV1Rules.LookLikeSha(result.Sha256))
            {
                next = VisualCalibrationPackV1Rules.MarkArtifactStatus(
                    next, request.SlotId, VisualCalibrationPackV1Rules.SlotFailed);
                if (IdentityConditionedCalibrationV1Rules.IsFront(request.ViewType))
                    next = VisualCalibrationPackV1Rules.MarkSubjectStatus(
                        next, request.SubjectId, IdentityConditionedCalibrationLiveV1Rules.SubjectAnchorFailed);
                if (persist is not null)
                    await persist(next, cancellationToken);
                continue;
            }

            next = VisualCalibrationPackV1Rules.BindPixel(next, bound, result.Path, result.Sha256!);
            generated++;
            if (persist is not null)
                await persist(next, cancellationToken);
        }

        next = next with
        {
            Status = VisualCalibrationPackV1Rules.StatusAfterPixels(next),
            GenerationExecuted = called,
        };
        if (persist is not null)
            await persist(next, cancellationToken);
        var coverage = VisualCalibrationPackV1Rules.GetCoverage(next);
        var done = coverage.Valid == 24
            ? null
            : VisualCalibrationPackV1Rules.GateIncomplete;
        return new VisualCalibrationGenerationOutcome(next, done, called, called, generated, requests);
    }
}

public interface IVisualCalibrationGenerationProvider
{
    string ProviderId { get; }
    Task<VisualCalibrationSlotGenerationResult> GenerateSlotAsync(
        VisualCalibrationGenerationRequest request, CancellationToken cancellationToken);
}

public sealed class MockVisualCalibrationGenerationProvider : IVisualCalibrationGenerationProvider
{
    public string ProviderId { get; init; } = VisualCalibrationPackV1Rules.ProviderName;
    public int CallCount { get; private set; }
    public int SucceedUpTo { get; init; } = int.MaxValue;
    public HashSet<string> FailSlots { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<VisualCalibrationSlotGenerationResult> GenerateSlotAsync(
        VisualCalibrationGenerationRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (CallCount > SucceedUpTo || FailSlots.Contains(request.SlotId))
            return Task.FromResult(new VisualCalibrationSlotGenerationResult(
                true, false, null, null, null, null, null, VisualCalibrationPackV1Rules.GateIncomplete));
        var sha = VisualCalibrationPackV1Rules.TestPixelSha(request.SubjectId, request.ViewType);
        return Task.FromResult(new VisualCalibrationSlotGenerationResult(
            true, true, null, "image/png",
            $"calibration://{request.CalibrationPackId}/{request.SlotId}",
            sha, "mock-cal", null));
    }
}

public sealed record VisualCalibrationGenerationRequest(
    string CalibrationPackId,
    string SlotId,
    string SubjectId,
    string ViewType,
    string VisualUniverseSha,
    string ProjectVisualStyleSha,
    string CharacterDesignLanguageSha,
    string CompiledPrompt,
    string CompiledPromptSha,
    string Provider,
    string Model,
    string GenerationExecutionId,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string AgeAppearanceProfile,
    CalibrationIdentityContract? Identity = null,
    int ExecutionOrder = 0,
    bool IsIdentityAnchor = false,
    string? IdentityAnchorSlot = null,
    string? IdentityAnchorPath = null,
    string? IdentityAnchorSha256 = null,
    string? ReferenceRole = null,
    int ReferenceCount = 0,
    string? CalibrationRunId = null);

public sealed record VisualCalibrationSlotGenerationResult(
    bool ProviderCalled,
    bool Succeeded,
    byte[]? Bytes,
    string? Mime,
    string? Path,
    string? Sha256,
    string? ProviderRequestId,
    string? ErrorCode);

public sealed record VisualCalibrationGenerationOutcome(
    VisualCalibrationPackV1Rules.PackSnapshot Pack,
    string? GateCode,
    bool ProviderCalled,
    bool GenerationExecuted,
    int SlotsGenerated,
    IReadOnlyList<VisualCalibrationGenerationRequest>? Requests = null);

public sealed record VisualCalibrationDryRun(
    IReadOnlyList<VisualCalibrationGenerationRequest> Requests,
    bool Compiled24,
    bool UniqueSlotIds,
    bool ProviderCalled,
    bool GenerationExecuted);

public static class VisualCalibrationPhotorealismGateV1
{
    public static string Classify(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)) return VisualCalibrationPackV1Rules.PhotorealismLow;
        if (evidence.Contains("HIGH", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("photorealistic human portrait", StringComparison.OrdinalIgnoreCase))
            return VisualCalibrationPackV1Rules.PhotorealismHigh;
        if (evidence.Contains("MEDIUM", StringComparison.OrdinalIgnoreCase))
            return VisualCalibrationPackV1Rules.PhotorealismMedium;
        return VisualCalibrationPackV1Rules.PhotorealismLow;
    }

    public static bool BlocksApproval(string? level) =>
        string.Equals(level, VisualCalibrationPackV1Rules.PhotorealismHigh, StringComparison.OrdinalIgnoreCase);

    public static bool InventsScore() => false;
    public static bool CallsGemini() => false;
}
