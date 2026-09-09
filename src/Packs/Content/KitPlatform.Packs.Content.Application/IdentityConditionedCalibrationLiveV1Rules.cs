namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_LIVE_V1 — isolated live-run control.
/// Stops at Director confirmation. Does not call Gemini from this layer.
/// Does not overwrite historical independent pixels. Does not mutate Minh / VUA / PVS / CDL.
/// </summary>
public static class IdentityConditionedCalibrationLiveV1Rules
{
    public const string DocumentId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_LIVE_V1";
    public const string SuiteId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_LIVE_V1_REGRESSION";
    public const string ReadyForDirectorConfirmation = "READY_FOR_DIRECTOR_CONFIRMATION";
    public const string SubjectAnchorFailed = "IDENTITY_ANCHOR_FAILED";
    public const string GateRun = "CALIBRATION_RUN_NOT_READY";
    public const string GateOverwrite = "CALIBRATION_ARTIFACT_IMMUTABLE";

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool MutatesCharacters() => false;
    public static bool MutatesAuthority() => false;
    public static bool MutatesDatabase() => false;
    public static bool RequiresMigration() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool OverwritesHistorical() => false;
    public static bool CountsHistoricalAsIdentity() => false;
    public static bool PromotesVua() => false;

    public static string NewRunId() => "CAL-RUN-" + Guid.NewGuid().ToString("N");

    public static string ArtifactFileName(string runId, string subjectId, string view) =>
        $"{Sanitize(runId)}-{Sanitize(subjectId)}-{Sanitize(view)}.jpg";

    public static string ArtifactRelativeDir(string packId, string runId) =>
        Path.Combine("CALIBRATION", Sanitize(packId), Sanitize(runId));

    public static bool HasActiveRun(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        !string.IsNullOrWhiteSpace(pack.CalibrationRunId);

    public static bool HasIndependentHistoricalPixels(VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        pack.Subjects.SelectMany(s => s.Artifacts).Any(a =>
            string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)
            && ProjectVisualStyleV1Rules.LookLikeSha(a.Sha256)
            && !IdentityConditionedCalibrationV1Rules.IsValidIdentityAnchor(pack, a)
            && !IdentityConditionedCalibrationV1Rules.IsIdentityConditioned(pack, a));

    public static VisualCalibrationPackV1Rules.PackSnapshot Isolate(
        VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        if (HasActiveRun(pack))
            return pack;
        var historical = pack.Subjects.SelectMany(s => s.Artifacts)
            .Count(a => string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)
                && ProjectVisualStyleV1Rules.LookLikeSha(a.Sha256));
        var run = NewRunId();
        var exec = VisualCalibrationPackV1Rules.NewExecutionId();
        if (IdentityConditionedCalibrationV1Rules.IdentityReadyForReview(pack))
            return pack with { CalibrationRunId = run };
        if (historical > 0)
        {
            var evidence = pack.Subjects.SelectMany(s => s.Artifacts)
                .Where(a => string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)
                    && ProjectVisualStyleV1Rules.LookLikeSha(a.Sha256))
                .ToList();
            var fresh = VisualCalibrationPackV1Rules.CompilePack();
            return fresh with
            {
                PackId = pack.PackId,
                VisualUniverseSha = pack.VisualUniverseSha,
                ProjectVisualStyleSha = pack.ProjectVisualStyleSha,
                CharacterDesignLanguageSha = pack.CharacterDesignLanguageSha,
                CalibrationRunId = run,
                GenerationExecutionId = exec,
                HistoricalPixelCount = historical,
                HistoricalArtifacts = evidence,
                Status = VisualCalibrationPackV1Rules.StatusDraft,
                CurrentAuthority = false,
                AuthorityTransitioned = false,
            };
        }
        return pack with
        {
            CalibrationRunId = run,
            GenerationExecutionId = pack.GenerationExecutionId ?? exec,
        };
    }

    public static bool SlotReusable(
        VisualCalibrationPackV1Rules.PackSnapshot pack, string slotId)
    {
        var artifact = pack.Subjects.SelectMany(s => s.Artifacts)
            .FirstOrDefault(a => string.Equals(a.SlotId, slotId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    VisualCalibrationPackV1Rules.SlotIdOf(a.SubjectId, a.ViewType),
                    slotId, StringComparison.OrdinalIgnoreCase));
        if (artifact is null) return false;
        return IdentityConditionedCalibrationV1Rules.IsFront(artifact.ViewType)
            ? IdentityConditionedCalibrationV1Rules.IsValidIdentityAnchor(pack, artifact)
            : IdentityConditionedCalibrationV1Rules.IsIdentityConditioned(pack, artifact);
    }

    public static IdentityConditionedSlotLog ToLog(VisualCalibrationGenerationRequest request) =>
        new(
            request.SubjectId,
            request.SlotId,
            request.ViewType,
            request.GenerationExecutionId,
            request.CalibrationRunId,
            request.ReferenceCount,
            request.ReferenceRole ?? "NONE",
            request.VisualUniverseSha,
            request.ProjectVisualStyleSha,
            request.CharacterDesignLanguageSha,
            request.CompiledPromptSha,
            false,
            false);

    public static IdentityConditionedReviewMatrix ReviewMatrixOf(
        VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var rows = pack.Subjects.SelectMany(s => s.Artifacts.Select(a =>
            new IdentityConditionedReviewRow(
                s.CalibrationSubjectId,
                a.ViewType,
                a.Path,
                a.Sha256,
                a.ReferenceRole ?? (IdentityConditionedCalibrationV1Rules.IsFront(a.ViewType) ? "NONE" : null),
                a.GenerationStatus ?? VisualCalibrationPackV1Rules.SlotGenerationStatus(pack, a),
                IdentityConditionedCalibrationV1Rules.IsValidIdentityAnchor(pack, a),
                IdentityConditionedCalibrationV1Rules.IsIdentityConditioned(pack, a)))).ToList();
        var identity = IdentityConditionedCalibrationV1Rules.GetCoverage(pack);
        var pixels = VisualCalibrationPackV1Rules.GetCoverage(pack);
        return new IdentityConditionedReviewMatrix(
            rows,
            identity.IdentityAnchorsValid,
            identity.IdentityAnchorsExpected,
            identity.IdentityConditionedValid,
            identity.IdentityConditionedExpected,
            pixels.Valid,
            pixels.Required,
            0,
            rows.Count(r => string.Equals(r.Status, VisualCalibrationPackV1Rules.SlotFailed, StringComparison.OrdinalIgnoreCase)),
            rows.Count(r => string.Equals(r.Status, IdentityConditionedCalibrationV1Rules.GateAnchor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Status, SubjectAnchorFailed, StringComparison.OrdinalIgnoreCase)),
            false);
    }

    public static IdentityConditionedLiveReadiness Readiness(
        VisualCalibrationPackV1Rules.PackSnapshot? live = null)
    {
        var source = live ?? VisualCalibrationPackV1Rules.CompilePack();
        var isolated = Isolate(source);
        var plan = IdentityConditionedCalibrationV1Rules.CompilePlan(
            isolated, isolated.GenerationExecutionId);
        var snapshot = IdentityConditionedCalibrationV1Rules.SnapshotOf(isolated);
        return new IdentityConditionedLiveReadiness(
            DocumentId,
            ReadyForDirectorConfirmation,
            isolated.CalibrationRunId,
            isolated.GenerationExecutionId,
            isolated.HistoricalPixelCount,
            false,
            false,
            false,
            0,
            IdentityConditionedCalibrationV1Rules.FrontFirst(plan),
            IdentityConditionedCalibrationV1Rules.DownstreamRequiresFront(),
            !OverwritesHistorical(),
            IdentityConditionedCalibrationV1Rules.UnifiedCompilerBound(
                plan[0].CompiledPrompt, snapshot),
            !AutoApprove(),
            !AutoLock(),
            !PromotesVua(),
            VisualCalibrationPackV1Rules.GateConfirmation,
            plan.Select(ToLog).ToList(),
            ReviewMatrixOf(isolated),
            plan);
    }

    private static string Sanitize(string? value)
    {
        var t = (value ?? "").Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            t = t.Replace(c, '_');
        return string.IsNullOrWhiteSpace(t) ? "UNKNOWN" : t;
    }
}

public sealed record IdentityConditionedSlotLog(
    string SubjectId,
    string SlotId,
    string View,
    string ExecutionId,
    string? CalibrationRunId,
    int ReferenceCount,
    string ReferenceRole,
    string VisualUniverseSha,
    string PvsSha,
    string CdlSha,
    string CompiledPromptSha,
    bool ProviderCalled,
    bool GenerationExecuted);

public sealed record IdentityConditionedReviewRow(
    string Subject,
    string View,
    string? Artifact,
    string? Sha,
    string? Reference,
    string Status,
    bool IdentityAnchor,
    bool IdentityConditioned);

public sealed record IdentityConditionedReviewMatrix(
    IReadOnlyList<IdentityConditionedReviewRow> Rows,
    int FrontValid,
    int FrontExpected,
    int ConditionedValid,
    int ConditionedExpected,
    int PixelValid,
    int PixelExpected,
    int ProviderCalls,
    int Failed,
    int Blocked,
    bool VisualPass);

public sealed record IdentityConditionedLiveReadiness(
    string DocumentId,
    string Status,
    string? CalibrationRunId,
    string? GenerationExecutionId,
    int HistoricalPixelsPreserved,
    bool GeminiCalled,
    bool GenerationExecuted,
    bool ProviderCalled,
    int PixelCreated,
    bool FrontFirstEnforced,
    bool DownstreamRequiresFront,
    bool HistoricalOverwriteForbidden,
    bool UnifiedCompilerBound,
    bool AutoApproveForbidden,
    bool AutoLockForbidden,
    bool VuaPromotionForbidden,
    string GateCode,
    IReadOnlyList<IdentityConditionedSlotLog> Logs,
    IdentityConditionedReviewMatrix Review,
    IReadOnlyList<VisualCalibrationGenerationRequest> Plan);
