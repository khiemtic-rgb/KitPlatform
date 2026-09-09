namespace KitPlatform.Packs.Content;

/// <summary>
/// FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_DIRECTOR_REVIEW_V1 —
/// deterministic Director review of existing identity-conditioned calibration pixels.
/// Technical integrity may be machine-verified. Visual quality is Director-only.
/// Does not generate, call Gemini/Runway, mutate authority, approve, lock, or migrate.
/// </summary>
public static class IdentityConditionedCalibrationDirectorReviewV1Rules
{
    public const string DocumentId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_DIRECTOR_REVIEW_V1";
    public const string SuiteId = "FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_DIRECTOR_REVIEW_V1_REGRESSION";
    public const string ReadyStatus = "DIRECTOR_REVIEW_READY";

    public const string Pending = "PENDING";
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string ReviewRequired = "REVIEW_REQUIRED";

    public const string PackNotReviewed = "NOT_REVIEWED";
    public const string PackInReview = "IN_REVIEW";

    public const string GateTechnical = "TECHNICAL_ARTIFACT_INVALID";
    public const string GatePassBlocked = "DIRECTOR_PASS_BLOCKED";
    public const string GateRunMix = "CALIBRATION_RUN_MISMATCH";
    public const string GateMark = "VISUAL_PASS_NOT_READY";
    public const string HistoricalLabel = "HISTORICAL — NOT ELIGIBLE FOR CURRENT APPROVAL";

    public const string ReasonIdentityDrift = "IDENTITY_DRIFT";
    public const string ReasonAgeMismatch = "AGE_MISMATCH";
    public const string ReasonAppearanceDrift = "APPEARANCE_DRIFT";
    public const string ReasonFaceInconsistency = "FACE_INCONSISTENCY";
    public const string ReasonViewMismatch = "VIEW_MISMATCH";
    public const string ReasonWardrobeInconsistency = "WARDROBE_INCONSISTENCY";
    public const string ReasonVisualStyleDrift = "VISUAL_STYLE_DRIFT";
    public const string ReasonCrossCharacterCollision = "CROSS_CHARACTER_COLLISION";
    public const string ReasonPhotorealismLeakage = "PHOTOREALISM_LEAKAGE";
    public const string ReasonTechnicalInvalid = "TECHNICAL_ARTIFACT_INVALID";
    public const string ReasonIdentityAnchorInvalid = "IDENTITY_ANCHOR_INVALID";
    public const string ReasonReferenceBindingInvalid = "REFERENCE_BINDING_INVALID";
    public const string ReasonOther = "OTHER";

    public static readonly IReadOnlyList<string> VisualGateNames =
    [
        "IdentityGate",
        "AgeGate",
        "AppearanceGate",
        "FaceConsistencyGate",
        "ViewConsistencyGate",
        "WardrobeConsistencyGate",
        "VisualUniverseGate",
        "StylizationGate",
        "CrossCharacterGate",
        "PhotorealismGate",
    ];

    public static readonly IReadOnlyList<string> FailureReasons =
    [
        ReasonIdentityDrift,
        ReasonAgeMismatch,
        ReasonAppearanceDrift,
        ReasonFaceInconsistency,
        ReasonViewMismatch,
        ReasonWardrobeInconsistency,
        ReasonVisualStyleDrift,
        ReasonCrossCharacterCollision,
        ReasonPhotorealismLeakage,
        ReasonTechnicalInvalid,
        ReasonIdentityAnchorInvalid,
        ReasonReferenceBindingInvalid,
        ReasonOther,
    ];

    public static bool CallsGemini() => false;
    public static bool CreatesPixels() => false;
    public static bool CreatesVideo() => false;
    public static bool ProviderCalled() => false;
    public static bool MutatesCharacters() => false;
    public static bool MutatesAuthority() => false;
    public static bool MutatesDatabase() => false;
    public static bool RequiresMigration() => false;
    public static bool RegeneratesOnFail() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoPassFromTechnical() => false;
    public static bool CountsHistorical() => false;
    public static bool FoundationComplete() => false;
    public static bool ArchitectureReady() => true;
    public static bool LiveReady() => true;
    public static bool DirectorReviewInfrastructure() => true;

    public static bool IsDecision(string? value) =>
        value is Pending or Pass or Fail or ReviewRequired;

    public static string NormalizeDecision(string? value)
    {
        var t = (value ?? "").Trim().ToUpperInvariant();
        if (t is "REVIEW REQUIRED" or "REVIEW-REQUIRED") return ReviewRequired;
        return IsDecision(t) ? t : Pending;
    }

    public static int ReferenceCountOf(VisualCalibrationPackV1Rules.ArtifactSnapshot? artifact, string view)
    {
        if (artifact is null) return IdentityConditionedCalibrationV1Rules.IsFront(view) ? 0 : -1;
        if (IdentityConditionedCalibrationV1Rules.IsFront(view))
            return string.IsNullOrWhiteSpace(artifact.ReferenceRole)
                || string.Equals(artifact.ReferenceRole, "NONE", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1;
        return string.Equals(
            artifact.ReferenceRole,
            IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor,
            StringComparison.OrdinalIgnoreCase)
            && ProjectVisualStyleV1Rules.LookLikeSha(artifact.IdentityAnchorSha256)
                ? 1
                : 0;
    }

    public static VisualCalibrationPackV1Rules.ArtifactSnapshot? CurrentArtifact(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        string subjectId,
        string view)
    {
        var artifact = pack.Subjects
            .Where(s => string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(s => s.Artifacts)
            .FirstOrDefault(a => string.Equals(a.ViewType, view, StringComparison.OrdinalIgnoreCase));
        if (artifact is null) return null;
        if (IsHistoricalIndependent(pack, artifact)) return null;
        return artifact;
    }

    public static bool IsHistoricalIndependent(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.ArtifactSnapshot artifact)
    {
        if (pack.HistoricalArtifacts?.Any(h =>
                ProjectVisualStyleV1Rules.SameSha(h.Sha256, artifact.Sha256)
                && string.Equals(h.SubjectId, artifact.SubjectId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(h.ViewType, artifact.ViewType, StringComparison.OrdinalIgnoreCase)) == true)
            return true;
        if (!string.Equals(artifact.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!ProjectVisualStyleV1Rules.LookLikeSha(artifact.Sha256))
            return false;
        return !IdentityConditionedCalibrationV1Rules.IsValidIdentityAnchor(pack, artifact)
            && !IdentityConditionedCalibrationV1Rules.IsIdentityConditioned(pack, artifact)
            && !IdentityConditionedCalibrationV1Rules.IsFront(artifact.ViewType);
    }

    public static IdentityConditionedCalibrationTechnicalIntegrity EvaluateTechnical(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.CalibrationSubjectDefinition subject,
        string view,
        VisualCalibrationPackV1Rules.ArtifactSnapshot? artifact)
    {
        var front = IdentityConditionedCalibrationV1Rules.FindFront(pack, subject.CalibrationSubjectId);
        var exists = artifact is not null;
        var pixel = exists && VisualCalibrationPackV1Rules.IsValidPixel(pack, artifact!);
        var kindPixel = exists
            && string.Equals(artifact!.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase);
        var shaExists = exists && !string.IsNullOrWhiteSpace(artifact!.Sha256);
        var shaValid = exists && ProjectVisualStyleV1Rules.LookLikeSha(artifact!.Sha256);
        var subjectMatch = exists
            && string.Equals(artifact!.SubjectId, subject.CalibrationSubjectId, StringComparison.OrdinalIgnoreCase);
        var packMatch = exists
            && (string.IsNullOrWhiteSpace(artifact!.PackId)
                || string.Equals(artifact.PackId, pack.PackId, StringComparison.OrdinalIgnoreCase));
        var runMatch = !string.IsNullOrWhiteSpace(pack.CalibrationRunId);
        var viewMatch = exists
            && string.Equals(artifact!.ViewType, view, StringComparison.OrdinalIgnoreCase);
        var vuaMatch = exists
            && ProjectVisualStyleV1Rules.LookLikeSha(artifact!.VisualUniverseSha)
            && ProjectVisualStyleV1Rules.SameSha(artifact.VisualUniverseSha, pack.VisualUniverseSha);
        var pvsMatch = exists
            && ProjectVisualStyleV1Rules.LookLikeSha(artifact!.ProjectVisualStyleSha)
            && ProjectVisualStyleV1Rules.SameSha(artifact.ProjectVisualStyleSha, pack.ProjectVisualStyleSha);
        var cdlMatch = exists
            && ProjectVisualStyleV1Rules.LookLikeSha(artifact!.CharacterDesignLanguageSha)
            && ProjectVisualStyleV1Rules.SameSha(artifact.CharacterDesignLanguageSha, pack.CharacterDesignLanguageSha);
        var compiled = exists && ProjectVisualStyleV1Rules.LookLikeSha(artifact!.CompiledPromptSha);
        var execution = exists && !string.IsNullOrWhiteSpace(artifact!.ExecutionId);
        var refs = ReferenceCountOf(artifact, view);
        var frontRefsOk = IdentityConditionedCalibrationV1Rules.IsFront(view) && refs == 0;
        var downRefsOk = !IdentityConditionedCalibrationV1Rules.IsFront(view)
            && refs == 1
            && string.Equals(
                artifact?.ReferenceRole,
                IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor,
                StringComparison.OrdinalIgnoreCase);
        var downSubjectOk = IdentityConditionedCalibrationV1Rules.IsFront(view)
            || (front is not null
                && exists
                && string.Equals(artifact!.SubjectId, front.SubjectId, StringComparison.OrdinalIgnoreCase)
                && ProjectVisualStyleV1Rules.SameSha(artifact.IdentityAnchorSha256, front.Sha256));
        var notOtherSubject = IdentityConditionedCalibrationV1Rules.IsFront(view)
            || front is null
            || !pack.Subjects.SelectMany(s => s.Artifacts).Any(a =>
                IdentityConditionedCalibrationV1Rules.IsValidIdentityAnchor(pack, a)
                && !string.Equals(a.SubjectId, subject.CalibrationSubjectId, StringComparison.OrdinalIgnoreCase)
                && ProjectVisualStyleV1Rules.SameSha(artifact?.IdentityAnchorSha256, a.Sha256));
        var notHistorical = exists && !IsHistoricalIndependent(pack, artifact!);
        var identityAnchor = IdentityConditionedCalibrationV1Rules.IsFront(view)
            ? exists && IdentityConditionedCalibrationV1Rules.IsValidIdentityAnchor(pack, artifact!)
            : front is not null;
        var referenceBinding = IdentityConditionedCalibrationV1Rules.IsFront(view)
            ? frontRefsOk
            : downRefsOk && downSubjectOk && notOtherSubject;
        var snapshotBinding = runMatch && packMatch && viewMatch && subjectMatch && notHistorical;
        var authorityBinding = vuaMatch && pvsMatch && cdlMatch;
        var compilerBinding = compiled && execution;
        var artifactIntegrity = exists && pixel && kindPixel && shaExists && shaValid;
        var pass = artifactIntegrity
            && identityAnchor
            && referenceBinding
            && snapshotBinding
            && authorityBinding
            && compilerBinding
            && notHistorical;
        return new IdentityConditionedCalibrationTechnicalIntegrity(
            Flag(artifactIntegrity),
            Flag(identityAnchor),
            Flag(referenceBinding),
            Flag(snapshotBinding),
            Flag(authorityBinding),
            Flag(compilerBinding),
            pass ? Pass : Fail,
            pass ? null : GateTechnical);
    }

    public static bool MayDirectorPass(
        IdentityConditionedCalibrationTechnicalIntegrity technical,
        IdentityConditionedCalibrationVisualGates gates,
        string? decision)
    {
        if (technical.TechnicalStatus != Pass) return false;
        if (NormalizeDecision(decision) != Pass) return false;
        return gates.AllPass();
    }

    public static IdentityConditionedCalibrationVisualGates PendingGates() =>
        new(Pending, Pending, Pending, Pending, Pending, Pending, Pending, Pending, Pending, Pending);

    public static IdentityConditionedCalibrationDirectorReview BuildImageReview(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.CalibrationSubjectDefinition subject,
        string view,
        IdentityConditionedCalibrationDirectorDecision? saved)
    {
        var artifact = CurrentArtifact(pack, subject.CalibrationSubjectId, view);
        var technical = EvaluateTechnical(pack, subject, view, artifact);
        var gates = saved?.Gates ?? PendingGates();
        var requested = NormalizeDecision(saved?.Decision);
        var decision = requested == Pass && !MayDirectorPass(technical, gates, requested)
            ? Pending
            : requested;
        var front = IdentityConditionedCalibrationV1Rules.FindFront(pack, subject.CalibrationSubjectId);
        var role = IdentityConditionedCalibrationV1Rules.IsFront(view)
            ? (string.IsNullOrWhiteSpace(artifact?.ReferenceRole) ? "NONE" : artifact!.ReferenceRole)
            : artifact?.ReferenceRole ?? IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor;
        return new IdentityConditionedCalibrationDirectorReview(
            saved?.ReviewId ?? $"REV-{pack.CalibrationRunId}-{subject.CalibrationSubjectId}-{view}",
            pack.CalibrationRunId ?? "",
            pack.PackId,
            subject.CalibrationSubjectId,
            subject.Role,
            view,
            artifact?.SlotId ?? VisualCalibrationPackV1Rules.SlotIdOf(subject.CalibrationSubjectId, view),
            artifact?.Path,
            artifact?.Sha256,
            role,
            IdentityConditionedCalibrationV1Rules.IsFront(view) ? null : front?.SlotId,
            IdentityConditionedCalibrationV1Rules.IsFront(view) ? null : front?.Sha256 ?? artifact?.IdentityAnchorSha256,
            technical.ArtifactIntegrity,
            technical.IdentityAnchorValid,
            technical.ReferenceBindingValid,
            technical.SnapshotBindingValid,
            technical.AuthorityBindingValid,
            technical.CompilerBindingValid,
            technical.TechnicalStatus,
            gates.IdentityGate,
            gates.AgeGate,
            gates.AppearanceGate,
            gates.FaceConsistencyGate,
            gates.ViewConsistencyGate,
            gates.WardrobeConsistencyGate,
            gates.VisualUniverseGate,
            gates.StylizationGate,
            gates.CrossCharacterGate,
            gates.PhotorealismGate,
            decision,
            saved?.DirectorNote,
            saved?.FailureReason,
            saved?.ReviewedAt,
            saved?.ReviewedBy,
            IdentityConditionedCalibrationV1Rules.IsFront(view),
            !IdentityConditionedCalibrationV1Rules.IsFront(view)
                && technical.TechnicalStatus == Pass);
    }

    public static IdentityConditionedCalibrationSubjectReview BuildSubjectReview(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        VisualCalibrationPackV1Rules.CalibrationSubjectDefinition subject,
        IdentityConditionedCalibrationDirectorDecision? saved)
    {
        var views = IdentityConditionedCalibrationV1Rules.GenerationOrder
            .Select(view => BuildImageReview(pack, subject, view, saved))
            .ToList();
        var technical = views.All(v => v.TechnicalStatus == Pass) && views.Count == 4 ? Pass : Fail;
        var missingView = views.Any(v => string.IsNullOrWhiteSpace(v.ArtifactPath) || v.TechnicalStatus != Pass);
        var requested = NormalizeDecision(saved?.Decision);
        var gates = saved?.Gates ?? PendingGates();
        string status;
        if (requested == Fail || gates.AnyFail())
            status = Fail;
        else if (requested == ReviewRequired || gates.AnyReviewRequired())
            status = ReviewRequired;
        else if (requested == Pass
            && technical == Pass
            && !missingView
            && gates.AllPass()
            && views.All(v => v.Decision == Pass))
            status = Pass;
        else
            status = Pending;
        return new IdentityConditionedCalibrationSubjectReview(
            subject.CalibrationSubjectId,
            subject.Label,
            subject.Role,
            subject.ChronologicalAge,
            subject.TargetAppearanceAgeMin,
            subject.TargetAppearanceAgeMax,
            technical,
            status,
            views,
            gates,
            requested,
            saved?.DirectorNote,
            saved?.FailureReason,
            saved?.ReviewedAt,
            saved?.ReviewedBy);
    }

    public static IdentityConditionedCalibrationPackReview BuildPackReview(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        IReadOnlyDictionary<string, IdentityConditionedCalibrationDirectorDecision>? saved = null,
        bool visualPassMarked = false,
        string? requestedRunId = null)
    {
        var isolated = BindReviewRun(pack);
        var mix = !string.IsNullOrWhiteSpace(requestedRunId)
            && !string.IsNullOrWhiteSpace(isolated.CalibrationRunId)
            && !string.Equals(requestedRunId, isolated.CalibrationRunId, StringComparison.OrdinalIgnoreCase);
        var working = mix
            ? isolated with { CalibrationRunId = requestedRunId }
            : isolated;
        var decisions = mix
            ? new Dictionary<string, IdentityConditionedCalibrationDirectorDecision>(StringComparer.OrdinalIgnoreCase)
            : saved ?? new Dictionary<string, IdentityConditionedCalibrationDirectorDecision>(StringComparer.OrdinalIgnoreCase);
        var subjects = VisualCalibrationPackV1Rules.MatrixOf(null)
            .Select(subject =>
            {
                decisions.TryGetValue(subject.CalibrationSubjectId, out var row);
                return BuildSubjectReview(working, subject, row);
            })
            .ToList();
        var images = subjects.SelectMany(s => s.Views).ToList();
        var identity = IdentityConditionedCalibrationV1Rules.GetCoverage(working);
        var pixels = images.Count(i => i.TechnicalStatus == Pass);
        var anchors = images.Count(i => i.IsIdentityAnchor && i.TechnicalStatus == Pass);
        var conditioned = images.Count(i => i.IsIdentityConditioned);
        var subjectPass = subjects.Count(s => s.SubjectReviewStatus == Pass);
        var fail = subjects.Count(s => s.SubjectReviewStatus == Fail)
            + images.Count(i => i.Decision == Fail || i.TechnicalStatus == Fail && i.Decision != Pending);
        var reviewRequired = subjects.Count(s => s.SubjectReviewStatus == ReviewRequired)
            + images.Count(i => i.Decision == ReviewRequired);
        var reviewed = images.Count(i => i.Decision is Pass or Fail or ReviewRequired);
        var crossFail = subjects.Any(s => s.Gates.CrossCharacterGate is Fail or ReviewRequired);
        var photoFail = subjects.Any(s => s.Gates.PhotorealismGate is Fail or ReviewRequired);
        var universeFail = subjects.Any(s => s.Gates.VisualUniverseGate is Fail or ReviewRequired);
        var unresolvedRequired = subjects.Any(s => s.SubjectReviewStatus == ReviewRequired)
            || images.Any(i => i.Decision == ReviewRequired);
        var unresolvedFail = subjects.Any(s => s.SubjectReviewStatus == Fail)
            || images.Any(i => i.Decision == Fail);
        var allVisual = subjects.All(s => s.Gates.AllPass()) && subjects.Count == 6;
        var packReady = !mix
            && pixels == 24
            && anchors == 6
            && conditioned == 18
            && subjectPass == 6
            && allVisual
            && !unresolvedRequired
            && !unresolvedFail
            && !crossFail
            && !photoFail
            && !universeFail;
        var anyReview = reviewed > 0 || subjects.Any(s => s.Decision != Pending);
        string packDecision;
        if (mix) packDecision = PackNotReviewed;
        else if (unresolvedFail) packDecision = Fail;
        else if (unresolvedRequired) packDecision = ReviewRequired;
        else if (packReady) packDecision = Pass;
        else if (anyReview) packDecision = PackInReview;
        else packDecision = PackNotReviewed;
        var directorSixPass = subjects.Count(s => s.Decision == Pass) == 6
            && subjects.Count == 6
            && !unresolvedFail
            && !unresolvedRequired;
        var visualPass = visualPassMarked
            && ((packReady && packDecision == Pass) || directorSixPass);
        var historical = (working.HistoricalArtifacts ?? [])
            .Select(a => new IdentityConditionedCalibrationHistoricalRow(
                a.SubjectId,
                a.ViewType,
                a.Path,
                a.Sha256,
                HistoricalLabel))
            .ToList();
        return new IdentityConditionedCalibrationPackReview(
            DocumentId,
            ReadyStatus,
            working.CalibrationRunId,
            working.PackId,
            subjects.Count,
            6,
            images.Count,
            24,
            anchors,
            6,
            conditioned,
            18,
            pixels,
            24,
            reviewed,
            24,
            subjectPass,
            6,
            subjects.Count(s => s.SubjectReviewStatus == Fail),
            subjects.Count(s => s.SubjectReviewStatus == ReviewRequired),
            visualPass,
            packDecision,
            mix ? GateRunMix : null,
            false,
            false,
            false,
            0,
            0,
            false,
            false,
            false,
            ArchitectureReady(),
            LiveReady(),
            DirectorReviewInfrastructure(),
            visualPass,
            FoundationComplete(),
            subjects,
            historical);
    }

    public static IdentityConditionedCalibrationDirectorDecision ApplyDecision(
        IdentityConditionedCalibrationTechnicalIntegrity technical,
        IdentityConditionedCalibrationDirectorDecision incoming,
        IdentityConditionedCalibrationDirectorDecision? previous)
    {
        var gates = incoming.Gates;
        var requested = NormalizeDecision(incoming.Decision);
        if (requested == Pass && !MayDirectorPass(technical, gates, requested))
            return incoming with
            {
                Decision = requested,
                Gates = gates,
                DirectorNote = incoming.DirectorNote,
                FailureReason = incoming.FailureReason,
                ReviewedAt = incoming.ReviewedAt,
                ReviewedBy = incoming.ReviewedBy,
            };
        if (requested == Pending && previous?.Decision == Fail)
            return previous with
            {
                Gates = gates,
                DirectorNote = incoming.DirectorNote ?? previous.DirectorNote,
                ReviewedAt = incoming.ReviewedAt ?? previous.ReviewedAt,
                ReviewedBy = incoming.ReviewedBy ?? previous.ReviewedBy,
            };
        return incoming with { Decision = requested, Gates = gates };
    }

    public static bool CanMarkVisualPass(IdentityConditionedCalibrationPackReview review) =>
        review.PackDecision == Pass
        && review.TechnicalIntegrityValid == 24
        && review.FrontAnchorsValid == 6
        && review.IdentityConditionedValid == 18
        && review.SubjectPass == 6
        && !review.VisualPass
        && review.Fail == 0
        && review.ReviewRequired == 0
        && !review.GeminiCalled
        && !review.GenerationExecuted
        && !review.ProviderCalled;

    public static bool CanRecordDirectorVisualPass(IdentityConditionedCalibrationPackReview review) =>
        review.Subjects.Count == 6
        && review.Subjects.Count(s => s.Decision == Pass) == 6
        && !review.VisualPass
        && review.Fail == 0
        && review.ReviewRequired == 0
        && !review.GeminiCalled
        && !review.GenerationExecuted
        && !review.ProviderCalled;

    public static IdentityConditionedCalibrationPackReview MarkVisualPass(
        IdentityConditionedCalibrationPackReview review,
        bool confirm)
    {
        if (!confirm || (!CanMarkVisualPass(review) && !CanRecordDirectorVisualPass(review)))
            return review with { VisualPass = false, GateCode = review.GateCode ?? GateMark };
        return review with
        {
            VisualPass = true,
            PackDecision = Pass,
            IdentityConditionedCalibrationVisualPass = true,
            GateCode = null,
            GeminiCalled = false,
            GenerationExecuted = false,
            ProviderCalled = false,
            PixelArtifactCreated = 0,
            VideoArtifactCreated = 0,
            CharacterMutation = false,
            AuthorityMutation = false,
            DatabaseMutation = false,
            FoundationComplete = false,
        };
    }

    public static VisualCalibrationPackV1Rules.PackSnapshot BindReviewRun(
        VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        if (!string.IsNullOrWhiteSpace(pack.CalibrationRunId))
            return pack;
        var stableId = pack.GenerationExecutionId ?? ("CAL-REVIEW-" + pack.PackId);
        if (IdentityConditionedCalibrationLiveV1Rules.HasIndependentHistoricalPixels(pack)
            && !IdentityConditionedCalibrationV1Rules.IdentityReadyForReview(pack))
        {
            var isolated = IdentityConditionedCalibrationLiveV1Rules.Isolate(pack);
            return isolated with
            {
                CalibrationRunId = stableId,
                GenerationExecutionId = pack.GenerationExecutionId ?? isolated.GenerationExecutionId,
            };
        }
        return pack with { CalibrationRunId = stableId };
    }

    public static bool AuthorityShaUnchanged() =>
        CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
            CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
            CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
            CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
            CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
        && CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
            == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1"
        && CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
            == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc"
        && CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
            == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444"
        && CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
            == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7"
        && ProjectVisualStyleV1Rules.SameSha(
            ProjectVisualStyleV2Rules.ProtectedV1Sha,
            "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58")
        && ProjectVisualStyleV2Rules.Sha()
            == "56b57eee8388538fe7277d49bf6e53e013c495338329c4d5cd4549688fe1530d"
        && CharacterDesignLanguageV2Rules.Sha()
            == "683ce6bd64b1588c38db325cf4ce724d4f66be48b581b7f044ab25a065c88153"
        && FamixaVisualUniverseAuthorityV1Rules.Sha()
            == "4e9c4bad9ee0d241d1896846a574828865dcdd9e96e18751c251b3c77ea76726";

    public static VisualCalibrationPackV1Rules.PackSnapshot CompleteRun(
        string runId = "CAL-RUN-DIRECTOR-REVIEW")
    {
        var pack = VisualCalibrationPackV1Rules.WithPixelCoverage(
            VisualCalibrationPackV1Rules.CompilePack() with
            {
                Status = VisualCalibrationPackV1Rules.StatusPendingReview,
                CalibrationRunId = runId,
                GenerationExecutionId = "CAL-EXEC-DIRECTOR-REVIEW",
            },
            24);
        return pack with { CalibrationRunId = runId };
    }

    public static IdentityConditionedCalibrationDirectorDecision AllPassDecision(
        string subjectId,
        string runId,
        string packId) =>
        new(
            $"REV-{runId}-{subjectId}",
            runId,
            packId,
            subjectId,
            new IdentityConditionedCalibrationVisualGates(
                Pass, Pass, Pass, Pass, Pass, Pass, Pass, Pass, Pass, Pass),
            Pass,
            "Director reviewed pixels.",
            null,
            DateTimeOffset.UtcNow,
            "director");

    private static string Flag(bool pass) => pass ? Pass : Fail;
}

public sealed record IdentityConditionedCalibrationTechnicalIntegrity(
    string ArtifactIntegrity,
    string IdentityAnchorValid,
    string ReferenceBindingValid,
    string SnapshotBindingValid,
    string AuthorityBindingValid,
    string CompilerBindingValid,
    string TechnicalStatus,
    string? GateCode);

public sealed record IdentityConditionedCalibrationVisualGates(
    string IdentityGate,
    string AgeGate,
    string AppearanceGate,
    string FaceConsistencyGate,
    string ViewConsistencyGate,
    string WardrobeConsistencyGate,
    string VisualUniverseGate,
    string StylizationGate,
    string CrossCharacterGate,
    string PhotorealismGate)
{
    public bool AllPass() =>
        IdentityGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && AgeGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && AppearanceGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && FaceConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && ViewConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && WardrobeConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && VisualUniverseGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && StylizationGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && CrossCharacterGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass
        && PhotorealismGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Pass;

    public bool AnyFail() =>
        IdentityGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || AgeGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || AppearanceGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || FaceConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || ViewConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || WardrobeConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || VisualUniverseGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || StylizationGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || CrossCharacterGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail
        || PhotorealismGate == IdentityConditionedCalibrationDirectorReviewV1Rules.Fail;

    public bool AnyReviewRequired() =>
        IdentityGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || AgeGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || AppearanceGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || FaceConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || ViewConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || WardrobeConsistencyGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || VisualUniverseGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || StylizationGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || CrossCharacterGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired
        || PhotorealismGate == IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired;
}

public sealed record IdentityConditionedCalibrationDirectorDecision(
    string ReviewId,
    string CalibrationRunId,
    string PackId,
    string SubjectId,
    IdentityConditionedCalibrationVisualGates Gates,
    string Decision,
    string? DirectorNote,
    string? FailureReason,
    DateTimeOffset? ReviewedAt,
    string? ReviewedBy);

public sealed record IdentityConditionedCalibrationDirectorReview(
    string ReviewId,
    string CalibrationRunId,
    string PackId,
    string SubjectId,
    string SubjectType,
    string View,
    string? ArtifactId,
    string? ArtifactPath,
    string? ArtifactSha256,
    string? ReferenceRole,
    string? ReferenceArtifactId,
    string? ReferenceArtifactSha256,
    string ArtifactIntegrity,
    string IdentityAnchorValid,
    string ReferenceBindingValid,
    string SnapshotBindingValid,
    string AuthorityBindingValid,
    string CompilerBindingValid,
    string TechnicalStatus,
    string IdentityGate,
    string AgeGate,
    string AppearanceGate,
    string FaceConsistencyGate,
    string ViewConsistencyGate,
    string WardrobeConsistencyGate,
    string VisualUniverseGate,
    string StylizationGate,
    string CrossCharacterGate,
    string PhotorealismGate,
    string Decision,
    string? DirectorNote,
    string? FailureReason,
    DateTimeOffset? ReviewedAt,
    string? ReviewedBy,
    bool IsIdentityAnchor,
    bool IsIdentityConditioned);

public sealed record IdentityConditionedCalibrationSubjectReview(
    string SubjectId,
    string Label,
    string SubjectType,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string TechnicalStatus,
    string SubjectReviewStatus,
    IReadOnlyList<IdentityConditionedCalibrationDirectorReview> Views,
    IdentityConditionedCalibrationVisualGates Gates,
    string Decision,
    string? DirectorNote,
    string? FailureReason,
    DateTimeOffset? ReviewedAt,
    string? ReviewedBy);

public sealed record IdentityConditionedCalibrationHistoricalRow(
    string SubjectId,
    string View,
    string? Path,
    string? Sha256,
    string Label);

public sealed record IdentityConditionedCalibrationPackReview(
    string DocumentId,
    string Status,
    string? CalibrationRunId,
    string PackId,
    int SubjectsFound,
    int SubjectsExpected,
    int ViewsFound,
    int ViewsExpected,
    int FrontAnchorsValid,
    int FrontAnchorsExpected,
    int IdentityConditionedValid,
    int IdentityConditionedExpected,
    int TechnicalIntegrityValid,
    int TechnicalIntegrityExpected,
    int DirectorReviewCount,
    int DirectorReviewExpected,
    int SubjectPass,
    int SubjectExpected,
    int Fail,
    int ReviewRequired,
    bool VisualPass,
    string PackDecision,
    string? GateCode,
    bool GeminiCalled,
    bool GenerationExecuted,
    bool ProviderCalled,
    int PixelArtifactCreated,
    int VideoArtifactCreated,
    bool CharacterMutation,
    bool AuthorityMutation,
    bool DatabaseMutation,
    bool IdentityConditionedCalibrationArchitecture,
    bool IdentityConditionedCalibrationLive,
    bool DirectorReviewInfrastructure,
    bool IdentityConditionedCalibrationVisualPass,
    bool FoundationComplete,
    IReadOnlyList<IdentityConditionedCalibrationSubjectReview> Subjects,
    IReadOnlyList<IdentityConditionedCalibrationHistoricalRow> Historical);

public sealed record IdentityConditionedCalibrationDirectorReviewSaveRequest(
    string? IdentityGate,
    string? AgeGate,
    string? AppearanceGate,
    string? FaceConsistencyGate,
    string? ViewConsistencyGate,
    string? WardrobeConsistencyGate,
    string? VisualUniverseGate,
    string? StylizationGate,
    string? CrossCharacterGate,
    string? PhotorealismGate,
    string? Decision,
    string? DirectorNote,
    string? FailureReason,
    bool MarkVisualPass = false);

public interface IIdentityConditionedCalibrationDirectorReviewService
{
    Task<IdentityConditionedCalibrationPackReview> GetAsync(
        string? runId, CancellationToken cancellationToken = default);
    Task<IdentityConditionedCalibrationSubjectReview?> GetSubjectAsync(
        string runId, string subjectId, CancellationToken cancellationToken = default);
    Task<IdentityConditionedCalibrationPackReview> SaveSubjectAsync(
        string runId,
        string subjectId,
        IdentityConditionedCalibrationDirectorReviewSaveRequest request,
        string actor,
        CancellationToken cancellationToken = default);
    Task<IdentityConditionedCalibrationPackReview> MarkVisualPassAsync(
        string runId, bool confirm, string actor, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}
