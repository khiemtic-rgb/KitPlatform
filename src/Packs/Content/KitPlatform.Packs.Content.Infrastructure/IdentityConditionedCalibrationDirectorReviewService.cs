using System.Collections.Concurrent;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// In-memory Director Review store. Does not write calibration pixels, authority, or schema.
/// </summary>
internal sealed class IdentityConditionedCalibrationDirectorReviewService
    : IIdentityConditionedCalibrationDirectorReviewService
{
    private static readonly ConcurrentDictionary<string, ReviewState> Store = new(StringComparer.OrdinalIgnoreCase);
    private readonly IVisualCalibrationPackService _packs;

    public IdentityConditionedCalibrationDirectorReviewService(IVisualCalibrationPackService packs)
    {
        _packs = packs;
    }

    public IReadOnlyList<string> RunRegression() =>
        IdentityConditionedCalibrationDirectorReviewV1Regression.Run();

    public async Task<IdentityConditionedCalibrationPackReview> GetAsync(
        string? runId, CancellationToken cancellationToken = default)
    {
        var pack = await LoadPackAsync(cancellationToken);
        var state = ResolveState(pack, runId);
        return IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
            pack, state.Subjects, state.VisualPassMarked, runId);
    }

    public async Task<IdentityConditionedCalibrationSubjectReview?> GetSubjectAsync(
        string runId, string subjectId, CancellationToken cancellationToken = default)
    {
        var workspace = await GetAsync(runId, cancellationToken);
        return workspace.Subjects.FirstOrDefault(s =>
            string.Equals(s.SubjectId, subjectId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IdentityConditionedCalibrationPackReview> SaveSubjectAsync(
        string runId,
        string subjectId,
        IdentityConditionedCalibrationDirectorReviewSaveRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var pack = await LoadPackAsync(cancellationToken);
        var isolated = IdentityConditionedCalibrationDirectorReviewV1Rules.BindReviewRun(pack);
        if (!string.Equals(isolated.CalibrationRunId, runId, StringComparison.OrdinalIgnoreCase))
            return IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
                pack, null, false, runId);

        var def = VisualCalibrationPackV1Rules.DefaultMatrix.FirstOrDefault(s =>
            string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase));
        if (def is null)
            return IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack);

        var state = Store.GetOrAdd(runId, _ => new ReviewState());
        state.Subjects.TryGetValue(subjectId, out var previous);
        var incoming = new IdentityConditionedCalibrationDirectorDecision(
            previous?.ReviewId ?? $"REV-{runId}-{subjectId}",
            runId,
            isolated.PackId,
            subjectId,
            new IdentityConditionedCalibrationVisualGates(
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.IdentityGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.AgeGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.AppearanceGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.FaceConsistencyGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.ViewConsistencyGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.WardrobeConsistencyGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.VisualUniverseGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.StylizationGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.CrossCharacterGate),
                IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.PhotorealismGate)),
            IdentityConditionedCalibrationDirectorReviewV1Rules.NormalizeDecision(request.Decision),
            request.DirectorNote,
            request.FailureReason,
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(actor) ? "director" : actor.Trim());

        var technical = IdentityConditionedCalibrationV1Rules.GenerationOrder
            .Select(view => IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(
                isolated, def, view,
                IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(isolated, subjectId, view)))
            .ToList();
        var worst = technical.Any(t => t.TechnicalStatus != IdentityConditionedCalibrationDirectorReviewV1Rules.Pass)
            ? technical.First(t => t.TechnicalStatus != IdentityConditionedCalibrationDirectorReviewV1Rules.Pass)
            : technical[0];
        var applied = IdentityConditionedCalibrationDirectorReviewV1Rules.ApplyDecision(worst, incoming, previous);
        state.Subjects[subjectId] = applied;

        var workspace = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
            pack, state.Subjects, state.VisualPassMarked);
        if (request.MarkVisualPass)
        {
            var marked = IdentityConditionedCalibrationDirectorReviewV1Rules.MarkVisualPass(workspace, true);
            state.VisualPassMarked = marked.VisualPass;
            return marked;
        }
        return workspace;
    }

    public async Task<IdentityConditionedCalibrationPackReview> MarkVisualPassAsync(
        string runId, bool confirm, string actor, CancellationToken cancellationToken = default)
    {
        _ = actor;
        var pack = await LoadPackAsync(cancellationToken);
        var state = ResolveState(pack, runId);
        var workspace = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
            pack, state.Subjects, state.VisualPassMarked, runId);
        var packPixels = VisualCalibrationPackV1Rules.GetCoverage(pack).Valid;
        if (confirm
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.CanMarkVisualPass(workspace)
            && IdentityConditionedCalibrationDirectorReviewV1Rules.CanRecordDirectorVisualPass(workspace)
            && packPixels < 24)
        {
            return workspace with
            {
                VisualPass = false,
                GateCode = IdentityConditionedCalibrationDirectorReviewV1Rules.GateMark,
            };
        }
        var marked = IdentityConditionedCalibrationDirectorReviewV1Rules.MarkVisualPass(workspace, confirm);
        if (marked.VisualPass)
            state.VisualPassMarked = true;
        return marked;
    }

    private async Task<VisualCalibrationPackV1Rules.PackSnapshot> LoadPackAsync(CancellationToken cancellationToken) =>
        await _packs.GetSnapshotAsync(VisualCalibrationPackV1Rules.DefaultPackId, cancellationToken);

    private static ReviewState ResolveState(VisualCalibrationPackV1Rules.PackSnapshot pack, string? runId)
    {
        var isolated = IdentityConditionedCalibrationDirectorReviewV1Rules.BindReviewRun(pack);
        var key = string.IsNullOrWhiteSpace(runId) ? isolated.CalibrationRunId : runId;
        if (string.IsNullOrWhiteSpace(key))
            return new ReviewState();
        return Store.GetOrAdd(key, _ => new ReviewState());
    }

    private sealed class ReviewState
    {
        public ConcurrentDictionary<string, IdentityConditionedCalibrationDirectorDecision> Subjects { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public bool VisualPassMarked { get; set; }
    }
}
