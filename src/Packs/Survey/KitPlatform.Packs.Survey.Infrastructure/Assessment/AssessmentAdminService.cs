using Microsoft.Extensions.Options;
using KitPlatform.Packs.Survey;
using KitPlatform.Application.Configuration;

namespace KitPlatform.Packs.Survey.Infrastructure;

internal sealed class AssessmentAdminService : IAssessmentAdminService
{
    private readonly AssessmentRepository _repo;
    private readonly IAssessmentSubmissionService _submissions;
    private readonly PlatformSettings _platform;

    public AssessmentAdminService(
        AssessmentRepository repo,
        IAssessmentSubmissionService submissions,
        IOptions<PlatformSettings> platform)
    {
        _repo = repo;
        _submissions = submissions;
        _platform = platform.Value;
    }

    public AssessmentAdminAccessDto GetAccess() =>
        new(_platform.IsKapAdminEnabled);

    public async Task<AssessmentSubmissionListResultDto> ListSubmissionsAsync(
        AssessmentSubmissionListQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var (items, total) = await _repo.ListSubmissionsAsync(
            query.Page,
            query.PageSize,
            query.Status,
            query.HasLead,
            query.PartnerId,
            query.LeadPipelineStatus,
            query.Archived,
            cancellationToken);

        return new AssessmentSubmissionListResultDto(
            items.Select(MapListItem).ToList(),
            total,
            Math.Max(1, query.Page),
            Math.Clamp(query.PageSize, 1, 100));
    }

    public async Task<AssessmentSubmissionDetailAdminDto?> GetSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var submission = await _repo.GetSubmissionAsync(submissionId, cancellationToken);
        if (submission is null)
            return null;

        var counts = await _repo.GetRequiredCountsAsync(submissionId, cancellationToken);
        var responses = await _repo.GetResponsesAsync(submissionId, cancellationToken);
        var categoryScores = await _repo.GetCategoryScoresAsync(submissionId, cancellationToken);
        var insights = await _repo.GetInsightsAsync(submissionId, cancellationToken);
        var recommendations = await _repo.GetRecommendationsAsync(submissionId, cancellationToken);
        var scoringRows = await _repo.GetScoringDataAsync(submissionId, cancellationToken);
        var qualitative = AssessmentScoringEngine.Compute(scoringRows).Qualitative;

        return new AssessmentSubmissionDetailAdminDto(
            submission.Id,
            submission.Status,
            submission.TemplateCode,
            submission.TemplateVersion,
            submission.StartedAt,
            submission.CompletedAt,
            submission.LeadCapturedAt,
            submission.OverallScore,
            submission.OverallPct,
            submission.RespondentName,
            submission.RespondentPhone,
            submission.RespondentEmail,
            submission.RespondentOrgName,
            submission.RespondentNote,
            submission.ConsentMarketing,
            categoryScores.Select(c => new AssessmentCategoryScoreDto(c.Code, c.Name, c.Score, c.ScorePct)).ToList(),
            insights.Select(i => new AssessmentInsightDto(i.Title, i.Body, i.Severity)).ToList(),
            recommendations.Select(r => new AssessmentRecommendationDto(
                r.Title, r.Body, r.Priority, r.ProductArea, r.EstimateHint)).ToList(),
            new AssessmentQualitativeTagsDto(qualitative.PainPointTag, qualitative.PriorityNeedTag),
            responses.Count,
            counts.TotalRequired);
    }

    public async Task<AssessmentFullReportDto> GetReportAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        return await _submissions.GetReportForAdminAsync(submissionId, cancellationToken);
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> GetReportPdfAsync(
        Guid submissionId,
        KapReportPdfKind kind = KapReportPdfKind.Consulting,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        return await _submissions.GetReportPdfForAdminAsync(submissionId, kind, cancellationToken);
    }

    public async Task<bool> UpdateLeadPipelineAsync(
        Guid submissionId,
        UpdateLeadPipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var allowedPipeline = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "new", "contacted", "demo_scheduled", "demo_done", "won", "lost", "nurturing",
        };
        var allowedCommission = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "none", "pending", "approved", "paid", "void",
        };

        if (!allowedPipeline.Contains(request.LeadPipelineStatus))
            throw new InvalidOperationException("Trạng thái pipeline không hợp lệ.");

        string? commission = null;
        if (!string.IsNullOrWhiteSpace(request.CommissionStatus))
        {
            if (!allowedCommission.Contains(request.CommissionStatus))
                throw new InvalidOperationException("Trạng thái hoa hồng không hợp lệ.");
            commission = request.CommissionStatus.Trim().ToLowerInvariant();
        }

        return await _repo.UpdateLeadPipelineAsync(
            submissionId,
            request.LeadPipelineStatus.Trim().ToLowerInvariant(),
            commission,
            cancellationToken);
    }

    public async Task<bool> ArchiveSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var state = await _repo.GetArchiveStateAsync(submissionId, cancellationToken);
        if (state is null)
            return false;

        var (commissionStatus, archivedAt) = state.Value;
        if (archivedAt.HasValue)
            return true;

        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pending", "approved", "paid",
        };
        if (blocked.Contains(commissionStatus ?? "none"))
        {
            throw new InvalidOperationException(
                "Không thể ẩn lead khi hoa hồng đang pending/approved/paid. Hãy đặt commission = void trước.");
        }

        return await _repo.SetArchivedAtAsync(submissionId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<bool> UnarchiveSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();
        var state = await _repo.GetArchiveStateAsync(submissionId, cancellationToken);
        if (state is null)
            return false;
        if (!state.Value.ArchivedAt.HasValue)
            return true;
        return await _repo.SetArchivedAtAsync(submissionId, null, cancellationToken);
    }

    private static AssessmentSubmissionListItemDto MapListItem(AssessmentSubmissionListRow row) =>
        new(
            row.Id,
            row.Status,
            row.TemplateCode,
            row.TemplateVersion,
            row.StartedAt,
            row.CompletedAt,
            row.LeadCapturedAt,
            row.OverallScore,
            row.OverallPct,
            row.ResponseCount,
            row.RespondentName,
            row.RespondentPhone,
            row.RespondentEmail,
            row.RespondentOrgName,
            row.PartnerId,
            row.PartnerCode,
            row.PartnerName,
            row.LeadPipelineStatus,
            row.CommissionStatus,
            row.ArchivedAt);

    private void EnsureEnabled()
    {
        if (!_platform.IsKapAdminEnabled)
            throw new UnauthorizedAccessException("Tính năng không khả dụng trên triển khai này.");
    }
}
