namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_V1 — FRONT-first identity anchor.
/// Reuses View order + IImageGenerator refs. Does not create a second style compiler.
/// Does not call Gemini. Does not mutate VUA / PVS / CDL / Minh.
/// </summary>
public static class IdentityConditionedCalibrationV1Rules
{
    public const string DocumentId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_V1";
    public const string SuiteId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_V1_REGRESSION";
    public const string IdentityAnchorView = "FRONT";
    public const string ReferenceRoleAnchor = "IDENTITY_ANCHOR";
    public const string GateIdentity = "CALIBRATION_IDENTITY_NOT_READY";
    public const string GateAnchor = "IDENTITY_ANCHOR_NOT_READY";
    public const string GateReference = "IDENTITY_REFERENCE_NOT_READY";

    public static readonly IReadOnlyList<string> GenerationOrder =
        CharacterStudioIdentityLockV1Rules.GenerationOrder;

    public static readonly IReadOnlyList<string> ConditionedViews =
        ["THREE_QUARTER", "SIDE", "FULL_BODY"];

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool MutatesCharacters() => false;
    public static bool MutatesAuthority() => false;
    public static bool MutatesDatabase() => false;
    public static bool RequiresMigration() => false;
    public static bool CreatesSecondStyleCompiler() => false;
    public static bool IndependentDownstreamGenerationAllowed() => false;
    public static bool LegacyFallback() => false;
    public static bool FrontFirstEnforced() => true;
    public static bool DownstreamRequiresFront() => true;
    public static bool IdentityConditioningReady() => true;

    public static bool IsFront(string? view) =>
        string.Equals((view ?? "").Trim(), IdentityAnchorView, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ViewRefs(string view) =>
        IsFront(view) ? [] : [IdentityAnchorView];

    public static int ExecutionOrder(string view)
    {
        var type = (view ?? "").Trim().ToUpperInvariant();
        var i = GenerationOrder.ToList().FindIndex(v => v == type);
        return i < 0 ? int.MaxValue : i + 1;
    }

    public static CalibrationIdentityContract ContractOf(
        VisualCalibrationPackV1Rules.CalibrationSubjectDefinition subject,
        string view,
        string? anchorPath = null,
        string? anchorSha = null) =>
        new(
            subject.CalibrationSubjectId,
            subject.Role,
            subject.Gender,
            subject.ChronologicalAge,
            subject.TargetAppearanceAgeMin,
            subject.TargetAppearanceAgeMax,
            subject.BodyType,
            null,
            null,
            null,
            "locked to FRONT identity-anchor wardrobe",
            IdentityAnchorView,
            IsFront(view) ? VisualCalibrationPackV1Rules.SlotIdOf(subject.CalibrationSubjectId, IdentityAnchorView) : null,
            anchorSha,
            IsFront(view) ? null : ReferenceRoleAnchor,
            anchorPath,
            VisualCalibrationPackV1Rules.ExtraNegativeConstraints);

    public static bool ContractContainsStyleAuthority(CalibrationIdentityContract contract) =>
        ContainsStyleAuthority(contract.Hair)
        || ContainsStyleAuthority(contract.Eyes)
        || ContainsStyleAuthority(contract.Face)
        || ContainsStyleAuthority(contract.Wardrobe)
        || ContainsStyleAuthority(contract.SubjectType);

    public static bool ContainsStyleAuthority(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)
            || text.Contains("photorealistic", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Project Visual Style", StringComparison.OrdinalIgnoreCase);
    }

    public static VisualUniverseSnapshot SnapshotOf(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        VisualUniverseSnapshotV1Rules.FromAuthorities(
            FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            FamixaVisualUniverseAuthorityV1Rules.ToDto(
                FamixaVisualUniverseAuthorityV1Rules.StatusDraft, false),
            pack.ProjectVisualStyleSha,
            pack.PackId,
            VisualCalibrationPackV1Rules.PackSha(pack));

    public static bool UnifiedCompilerBound(string compiledPrompt, VisualUniverseSnapshot snapshot)
    {
        var compiled = UnifiedVisualCompilerV1Rules.Compile(snapshot);
        return compiledPrompt.StartsWith(compiled.StyleLayer, StringComparison.Ordinal)
            && compiledPrompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)
            && !UnifiedVisualCompilerV1Rules.RawPromptIsStyleAuthority();
    }

    public static IReadOnlyList<VisualCalibrationGenerationRequest> CompilePlan(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        string? executionId = null,
        string? pvsPrompt = null)
    {
        var exec = string.IsNullOrWhiteSpace(executionId)
            ? VisualCalibrationPackV1Rules.NewExecutionId()
            : executionId.Trim();
        var style = pvsPrompt ?? ProjectVisualStyleV1Rules.BuildPrompt(
            ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!);
        return VisualCalibrationPackV1Rules.MatrixOf(null)
            .SelectMany(subject => GenerationOrder.Select(view =>
                CompileOne(pack, subject, view, exec, style)))
            .OrderBy(r => r.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ExecutionOrder)
            .ToList();
    }

    public static VisualCalibrationGenerationRequest CompileOne(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.CalibrationSubjectDefinition subject,
        string view,
        string executionId,
        string? pvsPrompt)
    {
        var type = view.Trim().ToUpperInvariant();
        var front = FindFront(pack, subject.CalibrationSubjectId);
        var anchorSha = IsFront(type) ? null : front?.Sha256;
        var anchorPath = IsFront(type) ? null : front?.Path;
        var identity = ContractOf(subject, type, anchorPath, anchorSha);
        var prompt = VisualCalibrationPromptCompilerV1.Compile(subject, type, pvsPrompt);
        var refs = ViewRefs(type);
        return new VisualCalibrationGenerationRequest(
            pack.PackId,
            VisualCalibrationPackV1Rules.SlotIdOf(subject.CalibrationSubjectId, type),
            subject.CalibrationSubjectId,
            type,
            pack.VisualUniverseSha,
            pack.ProjectVisualStyleSha,
            pack.CharacterDesignLanguageSha,
            prompt,
            VisualCalibrationPackV1Rules.PromptSha(prompt),
            VisualCalibrationPackV1Rules.ProviderName,
            VisualCalibrationPackV1Rules.DefaultModel,
            executionId,
            subject.ChronologicalAge,
            subject.TargetAppearanceAgeMin,
            subject.TargetAppearanceAgeMax,
            VisualCalibrationPackV1Rules.AgeAppearanceProfile(subject),
            identity,
            ExecutionOrder(type),
            IsFront(type),
            IdentityAnchorView,
            anchorPath,
            anchorSha,
            IsFront(type) ? null : ReferenceRoleAnchor,
            refs.Count,
            pack.CalibrationRunId);
    }

    public static VisualCalibrationGenerationRequest BindLiveAnchor(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationGenerationRequest request)
    {
        if (IsFront(request.ViewType))
            return request with { IdentityAnchorSlot = IdentityAnchorView, ReferenceCount = 0, ReferenceRole = null };
        var front = FindFront(pack, request.SubjectId);
        var def = VisualCalibrationPackV1Rules.DefaultMatrix.FirstOrDefault(s =>
            string.Equals(s.CalibrationSubjectId, request.SubjectId, StringComparison.OrdinalIgnoreCase));
        var identity = request.Identity
            ?? (def is null ? null : ContractOf(def, request.ViewType, front?.Path, front?.Sha256));
        return request with
        {
            IdentityAnchorSlot = IdentityAnchorView,
            IdentityAnchorPath = front?.Path,
            IdentityAnchorSha256 = front?.Sha256,
            ReferenceRole = ReferenceRoleAnchor,
            ReferenceCount = 1,
            Identity = identity is null
                ? null
                : identity with
                {
                    IdentityAnchorSha256 = front?.Sha256,
                    IdentityAnchorPath = front?.Path,
                    ReferenceRole = ReferenceRoleAnchor,
                    SubjectId = request.SubjectId,
                },
        };
    }

    public static VisualCalibrationPackV1Rules.ArtifactSnapshot? FindFront(
        VisualCalibrationPackV1Rules.PackSnapshot pack, string subjectId) =>
        pack.Subjects
            .Where(s => string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(s => s.Artifacts)
            .FirstOrDefault(a => IsValidIdentityAnchor(pack, a));

    public static bool IsValidIdentityAnchor(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.ArtifactSnapshot artifact) =>
        IsFront(artifact.ViewType)
        && VisualCalibrationPackV1Rules.IsValidPixel(pack, artifact)
        && ProjectVisualStyleV1Rules.LookLikeSha(artifact.Sha256)
        && !string.IsNullOrWhiteSpace(artifact.Path)
        && string.Equals(artifact.IdentityAnchorSlot, IdentityAnchorView, StringComparison.OrdinalIgnoreCase);

    public static bool IsIdentityConditioned(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.ArtifactSnapshot artifact)
    {
        if (IsFront(artifact.ViewType) || !VisualCalibrationPackV1Rules.IsValidPixel(pack, artifact))
            return false;
        if (!string.Equals(artifact.ReferenceRole, ReferenceRoleAnchor, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(artifact.IdentityAnchorSlot, IdentityAnchorView, StringComparison.OrdinalIgnoreCase))
            return false;
        var front = FindFront(pack, artifact.SubjectId);
        return front is not null
            && ProjectVisualStyleV1Rules.SameSha(artifact.IdentityAnchorSha256, front.Sha256)
            && ProjectVisualStyleV1Rules.SameSha(front.VisualUniverseSha ?? pack.VisualUniverseSha, pack.VisualUniverseSha)
            && ProjectVisualStyleV1Rules.SameSha(front.ProjectVisualStyleSha ?? pack.ProjectVisualStyleSha, pack.ProjectVisualStyleSha)
            && ProjectVisualStyleV1Rules.SameSha(front.CharacterDesignLanguageSha ?? pack.CharacterDesignLanguageSha, pack.CharacterDesignLanguageSha);
    }

    public static VisualCalibrationIdentityCoverageDto GetCoverage(VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var subjects = VisualCalibrationPackV1Rules.MatrixOf(null).Count;
        var downstream = subjects * (GenerationOrder.Count - 1);
        var anchors = pack.Subjects.SelectMany(s => s.Artifacts).Count(a => IsValidIdentityAnchor(pack, a));
        var conditioned = pack.Subjects.SelectMany(s => s.Artifacts).Count(a => IsIdentityConditioned(pack, a));
        return new VisualCalibrationIdentityCoverageDto(
            subjects, anchors, downstream, conditioned,
            Math.Max(0, subjects - anchors) + Math.Max(0, downstream - conditioned));
    }

    public static bool IdentityReadyForReview(VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var id = GetCoverage(pack);
        var pixels = VisualCalibrationPackV1Rules.GetCoverage(pack);
        return pixels is { Required: 24, Valid: 24 }
            && id is { IdentityAnchorsExpected: 6, IdentityAnchorsValid: 6 }
            && id is { IdentityConditionedExpected: 18, IdentityConditionedValid: 18 };
    }

    public static string? MissingIdentity(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        IdentityReadyForReview(pack) ? null : GateIdentity;

    public static bool LooksLikeAnchorPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && !path.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("prompt", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("compiled", StringComparison.OrdinalIgnoreCase);

    public static bool ProviderMayCall(
        VisualCalibrationGenerationRequest request,
        VisualCalibrationPackV1Rules.PackSnapshot? pack = null)
    {
        if (VisualCalibrationPackV1Rules.ValidateAuthorities(
                request.VisualUniverseSha, request.ProjectVisualStyleSha, request.CharacterDesignLanguageSha) is not null)
            return false;
        if (!ProjectVisualStyleV1Rules.LookLikeSha(request.CompiledPromptSha)
            || string.IsNullOrWhiteSpace(request.CompiledPrompt)
            || !request.CompiledPrompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal))
            return false;
        if (IsFront(request.ViewType))
            return request.ReferenceCount == 0 && string.IsNullOrWhiteSpace(request.ReferenceRole);
        if (IndependentDownstreamGenerationAllowed() || request.ReferenceCount == 0)
            return false;
        if (!string.Equals(request.IdentityAnchorSlot, IdentityAnchorView, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.ReferenceRole, ReferenceRoleAnchor, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!ProjectVisualStyleV1Rules.LookLikeSha(request.IdentityAnchorSha256)
            || !LooksLikeAnchorPath(request.IdentityAnchorPath))
            return false;
        if (request.Identity is not null
            && !string.Equals(request.Identity.SubjectId, request.SubjectId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (pack is not null)
        {
            var front = FindFront(pack, request.SubjectId);
            if (front is null
                || !ProjectVisualStyleV1Rules.SameSha(front.Sha256, request.IdentityAnchorSha256)
                || !string.Equals(front.PackId ?? pack.PackId, request.CalibrationPackId, StringComparison.OrdinalIgnoreCase)
                || !ProjectVisualStyleV1Rules.SameSha(front.VisualUniverseSha ?? pack.VisualUniverseSha, request.VisualUniverseSha)
                || !ProjectVisualStyleV1Rules.SameSha(front.ProjectVisualStyleSha ?? pack.ProjectVisualStyleSha, request.ProjectVisualStyleSha)
                || !ProjectVisualStyleV1Rules.SameSha(front.CharacterDesignLanguageSha ?? pack.CharacterDesignLanguageSha, request.CharacterDesignLanguageSha))
                return false;
        }
        return true;
    }

    public static bool FrontFirst(IReadOnlyList<VisualCalibrationGenerationRequest> plan) =>
        VisualCalibrationPackV1Rules.MatrixOf(null).All(subject =>
        {
            var rows = plan.Where(r =>
                string.Equals(r.SubjectId, subject.CalibrationSubjectId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.ExecutionOrder).ToList();
            return rows.Count == 4
                && IsFront(rows[0].ViewType)
                && rows[0].ExecutionOrder == 1
                && rows.Select(r => r.ViewType).SequenceEqual(GenerationOrder);
        });

    public static IdentityConditionedCalibrationReport Audit(
        VisualCalibrationPackV1Rules.PackSnapshot? live = null)
    {
        var pack = live ?? VisualCalibrationPackV1Rules.CompilePack();
        var plan = CompilePlan(pack, "CAL-EXEC-IDENTITY-DRYRUN");
        var snapshot = SnapshotOf(pack);
        var pixels = VisualCalibrationPackV1Rules.GetCoverage(pack);
        var identity = GetCoverage(pack);
        return new IdentityConditionedCalibrationReport(
            DocumentId,
            IdentityConditioningReady(),
            identity.IdentityAnchorsExpected,
            identity.IdentityAnchorsValid,
            identity.IdentityConditionedExpected,
            identity.IdentityConditionedValid,
            pixels,
            identity,
            FrontFirstEnforced() && FrontFirst(plan),
            DownstreamRequiresFront(),
            !IndependentDownstreamGenerationAllowed(),
            plan.All(r => ProjectVisualStyleV1Rules.SameSha(r.VisualUniverseSha, pack.VisualUniverseSha)),
            plan.All(r => UnifiedCompilerBound(r.CompiledPrompt, snapshot)),
            IdentityConditioningReady() && FrontFirst(plan) && DownstreamRequiresFront(),
            false,
            false,
            plan.Select(r => ToSlotPlan(r, pack)).ToList());
    }

    public static IdentityConditionedSlotPlan ToSlotPlan(
        VisualCalibrationGenerationRequest request,
        VisualCalibrationPackV1Rules.PackSnapshot? pack = null) =>
        new(
            request.SubjectId,
            request.SlotId,
            request.ViewType,
            request.ExecutionOrder,
            request.IsIdentityAnchor,
            request.IdentityAnchorSlot,
            request.ReferenceCount,
            request.ReferenceRole,
            request.IdentityAnchorSha256,
            request.VisualUniverseSha,
            request.ProjectVisualStyleSha,
            request.CharacterDesignLanguageSha,
            request.CompiledPromptSha,
            ProviderMayCall(request, pack));
}

public sealed record CalibrationIdentityContract(
    string SubjectId,
    string SubjectType,
    string Gender,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string BodyType,
    string? Hair,
    string? Eyes,
    string? Face,
    string? Wardrobe,
    string IdentityAnchorSlot,
    string? IdentityAnchorArtifactId,
    string? IdentityAnchorSha256,
    string? ReferenceRole,
    string? IdentityAnchorPath = null,
    string? ExtraNegativeConstraints = null);

public sealed record VisualCalibrationIdentityCoverageDto(
    int IdentityAnchorsExpected,
    int IdentityAnchorsValid,
    int IdentityConditionedExpected,
    int IdentityConditionedValid,
    int IdentityMissing);

public sealed record IdentityConditionedSlotPlan(
    string Subject,
    string Slot,
    string View,
    int ExecutionOrder,
    bool Anchor,
    string? IdentityAnchorSlot,
    int ReferenceCount,
    string? ReferenceRole,
    string? ReferenceSha,
    string VisualUniverseSha,
    string PvsSha,
    string CdlSha,
    string CompiledPromptSha,
    bool ProviderMayCall);

public sealed record IdentityConditionedCalibrationReport(
    string DocumentId,
    bool IdentityConditioningReady,
    int IdentityAnchorsExpected,
    int IdentityAnchorsValid,
    int IdentityConditionedExpected,
    int IdentityConditionedValid,
    VisualCalibrationCoverageDto PixelCoverage,
    VisualCalibrationIdentityCoverageDto IdentityCoverage,
    bool FrontFirstEnforced,
    bool DownstreamRequiresFront,
    bool IndependentDownstreamGenerationForbidden,
    bool VisualUniverseBound,
    bool UnifiedCompilerBound,
    bool ProviderMayCall,
    bool GeminiCalled,
    bool GenerationExecuted,
    IReadOnlyList<IdentityConditionedSlotPlan> Plan);
