namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_VISUAL_FOUNDATION_FINALIZATION_V1 — read-only foundation gate.
/// Does not generate, approve, lock, promote, or mutate authority/characters.
/// TECHNICAL PASS is not VISUAL PASS. VISUAL style PASS is not IDENTITY PASS.
/// </summary>
public static class VisualFoundationFinalizationV1Rules
{
    public const string DocumentId = "FAMIXA_VISUAL_FOUNDATION_FINALIZATION_V1";
    public const string SuiteId = "FAMIXA_VISUAL_FOUNDATION_FINALIZATION_V1_REGRESSION";

    public const string TechnicalPass = "TECHNICAL_PASS";
    public const string VisualPass = "VISUAL_PASS";
    public const string IdentityPass = "IDENTITY_PASS";
    public const string GateFail = "FAIL";
    public const string GatePass = "PASS";
    public const string FoundationNotReady = "NOT_READY";
    public const string FoundationReady = "FOUNDATION_COMPLETE";
    public const string StateNotReady = "NOT_READY";
    public const string NextIdentityConditioning = "AUTHORIZE_IDENTITY_CONDITIONED_LIVE_GENERATION";

    public static bool CallsGemini() => false;
    public static bool CallsRunway() => false;
    public static bool CallsFal() => false;
    public static bool CreatesPixels() => false;
    public static bool CreatesVideo() => false;
    public static bool MutatesDatabase() => false;
    public static bool MutatesCharacters() => false;
    public static bool MutatesAuthority() => false;
    public static bool Approves() => false;
    public static bool Locks() => false;
    public static bool Promotes() => false;
    public static bool CreatesSecondStyleCompiler() => false;
    public static bool ReplacesPvsOrCdlOrVua() => false;

    public static bool TechnicalPassIsVisualPass() => false;
    public static bool VisualStylePassIsIdentityPass() => false;
    public static bool MasterShaBindsProviderIdentity() => false;
    public static bool CalibrationUsesIdentityRefs() => true;
    public static bool CalibrationGeneratesIndependently() => false;
    public static bool CameraMayRedesignIdentity() => false;
    public static bool WatermarkForbiddenInCalibrationNegatives() =>
        ExtraNegativeHasWatermark(VisualCalibrationPackV1Rules.ExtraNegativeConstraints);

    public static bool ExtraNegativeHasWatermark(string? negatives)
    {
        if (string.IsNullOrWhiteSpace(negatives)) return false;
        return negatives.Contains("watermark", StringComparison.OrdinalIgnoreCase)
            || negatives.Contains("logo", StringComparison.OrdinalIgnoreCase)
            || negatives.Contains("caption", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CameraInvariantCompile(VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var subject = VisualCalibrationPackV1Rules.DefaultMatrix[0];
        var views = VisualCalibrationPackV1Rules.CompileSubjectViews(subject);
        return VisualCalibrationPackV1Rules.ViewsDifferOnlyByCamera(views)
            && VisualCalibrationPackV1Rules.AgeDoesNotChangeStyle(pack);
    }

    public static bool CameraInvariantPixelReady() => false;

    public static bool ProductionHasIdentityRefsPolicy()
    {
        var front = CharacterStudioIdentityLockV1Rules.ViewRefs("FRONT");
        var side = CharacterStudioIdentityLockV1Rules.ViewRefs("SIDE");
        return front.SequenceEqual(["MASTER"])
            && side.SequenceEqual(["MASTER", "FRONT"]);
    }

    public static bool CalibrationRequestHasIdentityAuthority(VisualCalibrationGenerationRequest request) =>
        !string.IsNullOrWhiteSpace(request.CompiledPrompt)
        && request.CompiledPrompt.Contains("MasterSha256:", StringComparison.OrdinalIgnoreCase);

    public static bool ProviderMayRedefineStyle(UnifiedVisualContract? contract) =>
        !SeriesStillVisualIngressV1Rules.ProviderMayCall(contract);

    public static bool IsFoundationReady(
        bool styleAuthority,
        bool identityGeneration,
        bool cameraPixel,
        bool watermark,
        bool lockedImmutable) =>
        styleAuthority && identityGeneration && cameraPixel && watermark && lockedImmutable;

    public static VisualFoundationFinalizationReport Audit()
    {
        var before = CaptureShas();
        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var plan = VisualCalibrationGenerationServiceV1.CompilePlan(pack, "CAL-EXEC-FOUNDATION");
        var snapshot = VisualUniverseSnapshotV1Rules.FromAuthorities(
            FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            FamixaVisualUniverseAuthorityV1Rules.ToDto(
                FamixaVisualUniverseAuthorityV1Rules.StatusDraft, false),
            ProjectVisualStyleV2Rules.ProtectedV1Sha);
        var still = UnifiedVisualCompilerV1Rules.Compile(snapshot);
        var styleOk = ProjectVisualStyleV1Rules.LookLikeSha(pack.VisualUniverseSha)
            && ProjectVisualStyleV1Rules.SameSha(pack.ProjectVisualStyleSha, ProjectVisualStyleV2Rules.ProtectedV1Sha)
            && ProjectVisualStyleV1Rules.LookLikeSha(pack.CharacterDesignLanguageSha)
            && SeriesStillVisualIngressV1Rules.ProviderMayCall(still);
        var compileCamera = CameraInvariantCompile(pack);
        var refsPolicy = ProductionHasIdentityRefsPolicy();
        var identityGen = CalibrationUsesIdentityRefs()
            && !CalibrationGeneratesIndependently()
            && IdentityConditionedCalibrationV1Rules.FrontFirst(plan)
            && plan.All(r => IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType)
                ? r.ReferenceCount == 0
                : r.ReferenceCount == 1);
        var watermark = WatermarkForbiddenInCalibrationNegatives();
        var locked = CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
            CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
            CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
            CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
            CharacterAuthorityInitializationV1Rules.ProtectedCrpSha);
        var ready = IsFoundationReady(styleOk, identityGen, CameraInvariantPixelReady(), watermark, locked);
        var after = CaptureShas();
        return new VisualFoundationFinalizationReport(
            DocumentId,
            ready ? FoundationReady : FoundationNotReady,
            styleOk ? GatePass : GateFail,
            GateFail,
            identityGen ? GatePass : GateFail,
            compileCamera ? GatePass : GateFail,
            CameraInvariantPixelReady() ? GatePass : GateFail,
            watermark ? GatePass : GateFail,
            locked ? GatePass : GateFail,
            SeriesStillVisualIngressV1Rules.ProviderMayCall(still)
                && !SeriesStillVisualIngressV1Rules.IdentityBriefBypassesCompiler()
                ? GatePass : GateFail,
            TechnicalPassIsVisualPass() || VisualStylePassIsIdentityPass()
                ? GateFail : GatePass,
            CalibrationGeneratesIndependently(),
            refsPolicy,
            plan.Count == 24,
            before,
            after,
            SameCapture(before, after),
            NextIdentityConditioning);
    }

    public static VisualFoundationShaCapture CaptureShas() => new(
        FamixaVisualUniverseAuthorityV1Rules.Sha(),
        ProjectVisualStyleV2Rules.ProtectedV1Sha,
        CharacterDesignLanguageV2Rules.Sha(),
        CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
        CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
        CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
        CharacterAuthorityInitializationV1Rules.ProtectedCrpSha);

    public static bool SameCapture(VisualFoundationShaCapture a, VisualFoundationShaCapture b) =>
        ProjectVisualStyleV1Rules.SameSha(a.Vua, b.Vua)
        && ProjectVisualStyleV1Rules.SameSha(a.Pvs, b.Pvs)
        && ProjectVisualStyleV1Rules.SameSha(a.Cdl, b.Cdl)
        && ProjectVisualStyleV1Rules.SameSha(a.Master, b.Master)
        && ProjectVisualStyleV1Rules.SameSha(a.Dna, b.Dna)
        && ProjectVisualStyleV1Rules.SameSha(a.Prp, b.Prp)
        && ProjectVisualStyleV1Rules.SameSha(a.Crp, b.Crp);
}

public sealed record VisualFoundationShaCapture(
    string Vua,
    string Pvs,
    string Cdl,
    string Master,
    string Dna,
    string Prp,
    string Crp);

public sealed record VisualFoundationFinalizationReport(
    string DocumentId,
    string FoundationState,
    string StyleAuthority,
    string VisualReadiness,
    string IdentityReadiness,
    string CameraInvariantCompile,
    string CameraInvariantPixel,
    string NoWatermark,
    string LockedCharacters,
    string ProviderBoundary,
    string PassSeparation,
    bool CalibrationIndependentSlots,
    bool ProductionIdentityRefPolicy,
    bool Compiled24,
    VisualFoundationShaCapture Before,
    VisualFoundationShaCapture After,
    bool AuthorityUnchanged,
    string NextStep);
