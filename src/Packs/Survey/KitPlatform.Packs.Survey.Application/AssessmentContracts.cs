namespace KitPlatform.Packs.Survey;

public static class AssessmentErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string SessionInvalid = "session_invalid";
    public const string ReportLocked = "report_locked";
    public const string NotFound = "not_found";
    public const string AlreadyCompleted = "already_completed";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
}

public sealed class AssessmentException : Exception
{
    public AssessmentException(string errorCode, string message, int statusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}

// --- Template DTOs ---

public sealed record AssessmentTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string Version,
    IReadOnlyList<AssessmentCategoryDto> Categories);

public sealed record AssessmentCategoryDto(
    string Code,
    string Name,
    int SortOrder,
    IReadOnlyList<AssessmentDimensionDto> Dimensions);

public sealed record AssessmentDimensionDto(
    string Code,
    string Name,
    IReadOnlyList<AssessmentQuestionDto> Questions);

public sealed record AssessmentQuestionDto(
    Guid Id,
    string Code,
    string Title,
    string? HelpText,
    string QuestionType,
    bool Scorable,
    bool Required,
    int SortOrder,
    IReadOnlyList<AssessmentOptionDto> Options,
    IReadOnlyDictionary<string, object>? Metadata = null);

public sealed record AssessmentOptionDto(
    Guid Id,
    string Code,
    string Label,
    short? Score,
    int SortOrder);

// --- Submission DTOs ---

public sealed record CreateAssessmentSubmissionRequest(
    string TemplateCode,
    string? TemplateVersion,
    string Source,
    string? Locale,
    string? PartnerCode = null);

public sealed record CreateAssessmentSubmissionResult(
    Guid Id,
    string SessionToken,
    string Status,
    string TemplateCode,
    string TemplateVersion,
    DateTimeOffset ExpiresAt);

public sealed record AssessmentSubmissionDetailDto(
    Guid Id,
    string Status,
    string TemplateCode,
    string TemplateVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyDictionary<string, AssessmentResponseItemDto> Responses,
    decimal? OverallScore = null,
    decimal? OverallPct = null,
    IReadOnlyList<AssessmentCategoryScoreDto>? CategoryScores = null,
    IReadOnlyList<AssessmentInsightDto>? PreviewInsights = null);

public sealed record AssessmentResponseItemDto(
    Guid? OptionId,
    string? TextValue);

public sealed record SaveAssessmentResponsesRequest(
    IReadOnlyList<AssessmentResponseInput> Responses);

public sealed record AssessmentResponseInput(
    Guid QuestionId,
    Guid? OptionId,
    string? TextValue);

public sealed record SaveAssessmentResponsesResult(
    int Saved,
    int AnsweredRequired,
    int TotalRequired);

public sealed record CompleteAssessmentResult(
    string Status,
    decimal OverallScore,
    decimal OverallPct,
    IReadOnlyList<AssessmentCategoryScoreDto> CategoryScores,
    IReadOnlyList<AssessmentInsightDto> PreviewInsights,
    bool ReportLocked,
    bool LeadCaptureRequired);

public sealed record CaptureAssessmentLeadRequest(
    string RespondentName,
    string RespondentPhone,
    string RespondentEmail,
    string RespondentOrgName,
    string? RespondentNote,
    bool ConsentMarketing,
    string? OrgScale = null);

public sealed record CaptureAssessmentLeadResult(
    string Status,
    string ReportToken,
    string Message);

public sealed record AssessmentCategoryScoreDto(
    string Code,
    string Name,
    decimal Score,
    decimal ScorePct);

public sealed record AssessmentDimensionScoreDto(
    string Code,
    string Name,
    string CategoryCode,
    decimal Score,
    decimal ScorePct);

public sealed record AssessmentInsightDto(
    string Title,
    string Body,
    string Severity);

public sealed record AssessmentRecommendationDto(
    string Title,
    string Body,
    int Priority,
    string? ProductArea,
    string? EstimateHint);

public sealed record AssessmentQualitativeTagsDto(
    string? PainPoint,
    string? PriorityNeed);

public sealed record AssessmentFullReportDto(
    Guid SubmissionId,
    string TemplateCode,
    DateTimeOffset? CompletedAt,
    decimal OverallScore,
    decimal OverallPct,
    IReadOnlyList<AssessmentCategoryScoreDto> CategoryScores,
    IReadOnlyList<AssessmentDimensionScoreDto> DimensionScores,
    IReadOnlyList<AssessmentInsightDto> Insights,
    IReadOnlyList<AssessmentRecommendationDto> Recommendations,
    AssessmentQualitativeTagsDto QualitativeTags,
    AssessmentReportPdfDto Pdf,
    AssessmentReportIntelligenceDto? Intelligence = null,
    string? OrgScale = null);

public sealed record AssessmentReportPdfDto(
    bool Available,
    string? DownloadUrl);

public interface IAssessmentTemplateService
{
    Task<AssessmentTemplateDto?> GetByCodeAsync(
        string code,
        string? version,
        CancellationToken cancellationToken = default);
}

public sealed record AssessmentAdminAccessDto(bool Enabled);

public sealed record AssessmentSubmissionListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    bool? HasLead = null,
    Guid? PartnerId = null,
    string? LeadPipelineStatus = null,
    /// <summary>false = active only (default); true = archived only.</summary>
    bool Archived = false);

public sealed record AssessmentSubmissionListItemDto(
    Guid Id,
    string Status,
    string TemplateCode,
    string TemplateVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LeadCapturedAt,
    decimal? OverallScore,
    decimal? OverallPct,
    int ResponseCount,
    string? RespondentName,
    string? RespondentPhone,
    string? RespondentEmail,
    string? RespondentOrgName,
    Guid? PartnerId = null,
    string? PartnerCode = null,
    string? PartnerName = null,
    string LeadPipelineStatus = "new",
    string CommissionStatus = "none",
    DateTimeOffset? ArchivedAt = null);

public sealed record AssessmentSubmissionListResultDto(
    IReadOnlyList<AssessmentSubmissionListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record AssessmentSubmissionDetailAdminDto(
    Guid Id,
    string Status,
    string TemplateCode,
    string TemplateVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LeadCapturedAt,
    decimal? OverallScore,
    decimal? OverallPct,
    string? RespondentName,
    string? RespondentPhone,
    string? RespondentEmail,
    string? RespondentOrgName,
    string? RespondentNote,
    bool ConsentMarketing,
    IReadOnlyList<AssessmentCategoryScoreDto> CategoryScores,
    IReadOnlyList<AssessmentInsightDto> Insights,
    IReadOnlyList<AssessmentRecommendationDto> Recommendations,
    AssessmentQualitativeTagsDto QualitativeTags,
    int ResponseCount,
    int RequiredCount);

public interface IAssessmentAdminService
{
    AssessmentAdminAccessDto GetAccess();

    Task<AssessmentSubmissionListResultDto> ListSubmissionsAsync(
        AssessmentSubmissionListQuery query,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionDetailAdminDto?> GetSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<AssessmentFullReportDto> GetReportAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string FileName, string ContentType)> GetReportPdfAsync(
        Guid submissionId,
        KapReportPdfKind kind = KapReportPdfKind.Consulting,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateLeadPipelineAsync(
        Guid submissionId,
        UpdateLeadPipelineRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> ArchiveSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<bool> UnarchiveSubmissionAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);
}

public interface IAssessmentSubmissionService
{
    Task<CreateAssessmentSubmissionResult> CreateAsync(
        CreateAssessmentSubmissionRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionDetailDto> GetAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<SaveAssessmentResponsesResult> SaveResponsesAsync(
        Guid submissionId,
        string sessionToken,
        SaveAssessmentResponsesRequest request,
        CancellationToken cancellationToken = default);

    Task<CompleteAssessmentResult> CompleteAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<CaptureAssessmentLeadResult> CaptureLeadAsync(
        Guid submissionId,
        string sessionToken,
        CaptureAssessmentLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<AssessmentFullReportDto> GetReportAsync(
        Guid submissionId,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string FileName, string ContentType)> GetReportPdfAsync(
        Guid submissionId,
        string sessionToken,
        KapReportPdfKind kind = KapReportPdfKind.Consulting,
        CancellationToken cancellationToken = default);

    Task<AssessmentFullReportDto> GetReportForAdminAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string FileName, string ContentType)> GetReportPdfForAdminAsync(
        Guid submissionId,
        KapReportPdfKind kind = KapReportPdfKind.Consulting,
        CancellationToken cancellationToken = default);
}
