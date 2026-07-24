namespace KitPlatform.Application.Learning;

public sealed record LearningProgramListItemDto(
    Guid Id,
    string Code,
    string PackCode,
    string Title,
    string? Summary,
    string Locale,
    int Version,
    int ModuleCount,
    int SortOrder);

public sealed record LearningModuleListItemDto(
    Guid Id,
    string Code,
    string Title,
    string? Summary,
    int DurationMinutes,
    string LevelCode,
    IReadOnlyList<string> CompetencyCodes,
    int SortOrder,
    int PassScorePct,
    bool RequireAck,
    int QuestionCount);

public sealed record LearningProgramDetailDto(
    Guid Id,
    string Code,
    string PackCode,
    string Title,
    string? Summary,
    string Locale,
    int Version,
    IReadOnlyList<LearningModuleListItemDto> Modules);

public sealed record LearningModuleDetailDto(
    Guid Id,
    Guid ProgramId,
    string Code,
    string Title,
    string? Summary,
    string BodyMarkdown,
    int DurationMinutes,
    string LevelCode,
    IReadOnlyList<string> CompetencyCodes,
    int PassScorePct,
    bool RequireAck,
    IReadOnlyList<LearningQuizQuestionPublicDto> Questions,
    bool IsTenantCustomized = false,
    bool RequireObservation = false);

public sealed record UpsertLearningModuleTenantContentRequest(
    string? Title,
    string? Summary,
    string BodyMarkdown);

public sealed record LearningQuizQuestionPublicDto(
    Guid Id,
    int SortOrder,
    string Prompt,
    IReadOnlyList<string> Options);

public sealed record LearningEnrollmentDto(
    Guid Id,
    Guid ProgramId,
    string ProgramTitle,
    string ProgramCode,
    Guid EmployeeId,
    string EmployeeName,
    string Status,
    DateTime AssignedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int ModulesTotal,
    int ModulesPassed);

public sealed record LearningModuleProgressDto(
    Guid ModuleId,
    string ModuleCode,
    string Title,
    string LevelCode,
    int SortOrder,
    string Status,
    int? ScorePct,
    int QuizAttempts,
    DateTime? AcknowledgedAt,
    bool RequireAck,
    bool RequireObservation = false,
    DateTime? ObservedAt = null,
    string? ObserverName = null,
    string? AcknowledgeSelfieUrl = null);

public sealed record AcknowledgeLearningModuleRequest(string? SelfieUrl = null);

public sealed record LearningMyLearningDto(
    LearningEnrollmentDto? Enrollment,
    IReadOnlyList<LearningModuleProgressDto> Modules,
    IReadOnlyList<LearningCredentialDto> Credentials);

public sealed record LearningCredentialDto(
    string CompetencyCode,
    string LevelCode,
    int? ScorePct,
    DateTime EarnedAt,
    Guid? SourceModuleId);

public sealed record LearningCompetencyRosterItemDto(
    Guid EmployeeId,
    string EmployeeName,
    int CredentialCount,
    int ModulesPassed,
    int ModulesTotal,
    string? EnrollmentStatus,
    IReadOnlyList<string> CompetencyCodes);

public sealed record LearningGateCheckDto(
    string PermissionCode,
    string Mode,
    bool Satisfied,
    bool HasOverride,
    IReadOnlyList<string> RequiredCompetencies,
    IReadOnlyList<string> MissingCompetencies,
    string? Message);

public sealed record CreateLearningGateOverrideRequest(
    Guid EmployeeId,
    string PermissionCode,
    string Reason,
    DateTime? ExpiresAt);

public sealed record LearningEvaluationDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    int PeriodYear,
    int PeriodMonth,
    int ScoreKnowledge,
    int ScoreAttitude,
    int ScoreCare,
    int ScoreStock,
    int ScoreDiscipline,
    int AverageScore,
    string? Comment,
    DateTime ReviewedAt,
    string? EmployeeFeedback = null,
    string? NextMonthGoal = null,
    DateTime? EmployeeRespondedAt = null,
    int? EngagementPulse = null);

public sealed record UpsertLearningEvaluationRequest(
    Guid EmployeeId,
    int PeriodYear,
    int PeriodMonth,
    int ScoreKnowledge,
    int ScoreAttitude,
    int ScoreCare,
    int ScoreStock,
    int ScoreDiscipline,
    string? Comment);

public sealed record SubmitLearningEvaluationFeedbackRequest(
    string? EmployeeFeedback,
    string? NextMonthGoal,
    int? EngagementPulse = null);

public sealed record LearningRecognitionDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string Kind,
    string Title,
    string? Body,
    string? BadgeCode,
    DateTime CreatedAt,
    int? CustomerRating = null);

/** Phản hồi sau mua từ app khách — có sao, không lộ SĐT khách. */
public sealed record LearningCustomerFeedbackDto(
    Guid Id,
    Guid? EmployeeId,
    string EmployeeName,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    Guid? RecognitionId);

/** Hộp thư riêng QL ↔ NV (chỉ người gửi + người nhận). */
public sealed record LearningMailMessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderName,
    string Body,
    DateTime CreatedAt,
    bool IsMine);

public sealed record LearningMailThreadListItemDto(
    Guid Id,
    string Subject,
    Guid RecipientEmployeeId,
    string RecipientEmployeeName,
    Guid CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int UnreadCount,
    string? LastMessagePreview,
    string? RelatedEventLabel,
    Guid? RelatedRecognitionId,
    Guid? RelatedFeedbackId,
    Guid? RelatedEvaluationId);

public sealed record LearningMailThreadDetailDto(
    Guid Id,
    string Subject,
    Guid RecipientEmployeeId,
    string RecipientEmployeeName,
    Guid CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? RelatedEventLabel,
    Guid? RelatedRecognitionId,
    Guid? RelatedFeedbackId,
    Guid? RelatedEvaluationId,
    IReadOnlyList<LearningMailMessageDto> Messages);

public sealed record CreateLearningMailThreadRequest(
    string Subject,
    string Body,
    IReadOnlyList<Guid> RecipientEmployeeIds,
    Guid? RelatedRecognitionId = null,
    Guid? RelatedFeedbackId = null,
    Guid? RelatedEvaluationId = null);

/// <summary>
/// One private thread per recipient (privacy unchanged). Same subject/body for all.
/// </summary>
public sealed record CreateLearningMailThreadsResultDto(
    int CreatedCount,
    IReadOnlyList<LearningMailThreadDetailDto> Threads);

public sealed record ReplyLearningMailRequest(string Body);

public sealed record LearningMailUnreadCountDto(int UnreadCount);

public sealed record CreateLearningRecognitionRequest(
    Guid EmployeeId,
    string Kind,
    string Title,
    string? Body,
    string? BadgeCode,
    bool IsPublic = true,
    int? CustomerRating = null);

public sealed record LearningBadgeDto(
    string BadgeCode,
    string Title,
    DateTime EarnedAt);

public sealed record LearningCareerLevelDto(
    Guid Id,
    string Code,
    string Title,
    string? Summary,
    int SortOrder,
    int MinMonthsTenure,
    int MinAvgEvaluate,
    IReadOnlyList<string> RequiredCompetencyCodes);

public sealed record LearningCareerRosterItemDto(
    Guid EmployeeId,
    string EmployeeName,
    Guid? CurrentLevelId,
    string? CurrentLevelCode,
    string? CurrentLevelTitle,
    Guid? NextLevelId,
    string? NextLevelCode,
    string? NextLevelTitle,
    bool EligibleForNext,
    IReadOnlyList<string> MissingReasons,
    int TenureMonths,
    int? LatestAvgEvaluate,
    int CredentialCount);

public sealed record PromoteLearningCareerRequest(
    Guid EmployeeId,
    Guid ToLevelId,
    string? Comment,
    bool Force = false);

public sealed record LearningCareerPromotionDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string? FromLevelTitle,
    string ToLevelTitle,
    string Status,
    bool EligibilityOk,
    IReadOnlyList<string> MissingReasons,
    string? Comment,
    DateTime DecidedAt);

public sealed record LearningPeopleDashboardDto(
    int EmployeeCount,
    int EnrolledCount,
    int CompletedEnrollmentCount,
    int ModulesPassedTotal,
    int ModulesTotalAssigned,
    int TrainingCompletionPct,
    int CredentialCount,
    int? AvgEvaluateScore,
    int EvaluationsThisMonth,
    int RecognitionCount30d,
    int BadgeCount,
    IReadOnlyList<LearningCareerLevelCountDto> CareerLevelCounts,
    int UnevaluatedThisMonth,
    int MissingPosBasicCount,
    int EligiblePromotionCount,
    IReadOnlyList<string> ActionItems,
    int ModulesPassedThisWeek = 0,
    int PerfectScoresThisWeek = 0,
    int RecognitionsThisWeek = 0,
    int PromotionsThisWeek = 0,
    IReadOnlyList<string>? CelebrationItems = null,
    int PendingFeedbackCount = 0,
    int MissingCloseChecklistBranchesToday = 0);

public sealed record LearningCareerLevelCountDto(
    string LevelCode,
    string LevelTitle,
    int EmployeeCount);

public sealed record LearningEmployeeEvidenceDto(
    Guid EmployeeId,
    string EmployeeName,
    int ModulesPassed,
    int ModulesTotal,
    string? EnrollmentStatus,
    IReadOnlyList<string> CompetencyCodes,
    bool HasPosBasic,
    string? CurrentLevelTitle,
    string? NextLevelTitle,
    bool EligibleForNext,
    IReadOnlyList<string> CareerMissingReasons,
    int TenureMonths,
    int? SuggestedKnowledge,
    int? SuggestedAttitude,
    int? SuggestedCare,
    int? SuggestedStock,
    int? SuggestedDiscipline,
    string SuggestionNote,
    int CloseChecklistDaysThisMonth = 0,
    int CloseStreakDays = 0,
    int? OrderCountThisMonth = null,
    decimal? SalesNetThisMonth = null,
    int? LatestEngagementPulse = null);

public sealed record LearningMyHabitsDto(
    int CloseChecklistDaysThisMonth,
    int CloseStreakDays,
    bool ClosedToday,
    bool OpenedToday,
    int TenureMonths,
    bool HasTenure12Badge,
    bool HasCloseStreak7Badge,
    IReadOnlyList<string> Tips);

public sealed record AssignLearningProgramRequest(Guid? EmployeeId, Guid ProgramId);

public sealed record SubmitLearningQuizRequest(
    IReadOnlyList<int> Answers,
    bool Practice = false);

public sealed record SubmitLearningQuizResultDto(
    bool Passed,
    int ScorePct,
    int PassScorePct,
    string ProgressStatus,
    IReadOnlyList<string> EarnedCompetencies);

public sealed record LearningMonthlyDrillStatusDto(
    int PeriodYear,
    int PeriodMonth,
    bool Eligible,
    bool Completed,
    DateTimeOffset? CompletedAt,
    int? ScorePct,
    int PassedModuleCount,
    string? Hint);

public sealed record LearningMonthlyDrillQuestionDto(
    Guid Id,
    string Prompt,
    IReadOnlyList<string> Options,
    string ModuleTitle,
    string LevelCode);

public sealed record LearningMonthlyDrillStartDto(
    int PeriodYear,
    int PeriodMonth,
    IReadOnlyList<LearningMonthlyDrillQuestionDto> Questions);

public sealed record LearningMonthlyDrillAnswerItem(Guid QuestionId, int SelectedIndex);

public sealed record SubmitLearningMonthlyDrillRequest(
    IReadOnlyList<LearningMonthlyDrillAnswerItem> Answers);

public sealed record LearningMonthlyDrillResultDto(
    bool Passed,
    int ScorePct,
    int PassScorePct,
    int CorrectCount,
    int QuestionCount,
    bool AlreadyCompleted);

public sealed record LearningObservationPendingDto(
    Guid EmployeeId,
    string EmployeeName,
    Guid ModuleId,
    string ModuleTitle,
    string ModuleCode,
    string LevelCode,
    int? ScorePct,
    DateTime? CompletedAt);

public sealed record LearningObservationDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid ModuleId,
    string ModuleTitle,
    IReadOnlyDictionary<string, bool> Criteria,
    string? Note,
    DateTime ObservedAt,
    string ObserverName);

public sealed record SubmitLearningObservationRequest(
    Guid EmployeeId,
    Guid ModuleId,
    IReadOnlyDictionary<string, bool> Criteria,
    string? Note);

public interface ILearningCatalogService
{
    Task<IReadOnlyList<LearningProgramListItemDto>> ListProgramsAsync(
        CancellationToken cancellationToken = default);

    Task<LearningProgramDetailDto?> GetProgramAsync(
        Guid programId,
        CancellationToken cancellationToken = default);

    Task<LearningModuleDetailDto?> GetModuleAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default);

    Task<LearningModuleDetailDto> UpsertModuleTenantContentAsync(
        Guid moduleId,
        UpsertLearningModuleTenantContentRequest request,
        CancellationToken cancellationToken = default);

    Task<LearningModuleDetailDto> RevertModuleTenantContentAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default);
}

public interface ILearningProgressService
{
    Task<LearningMyLearningDto> GetMyLearningAsync(CancellationToken cancellationToken = default);

    Task<LearningEnrollmentDto> EnsureMyEnrollmentAsync(
        Guid programId,
        CancellationToken cancellationToken = default);

    Task<LearningEnrollmentDto> AssignAsync(
        AssignLearningProgramRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningEnrollmentDto>> ListEnrollmentsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningCompetencyRosterItemDto>> ListCompetencyRosterAsync(
        CancellationToken cancellationToken = default);

    Task<LearningGateCheckDto> CheckGateAsync(
        string permissionCode,
        CancellationToken cancellationToken = default);

    Task CreateGateOverrideAsync(
        CreateLearningGateOverrideRequest request,
        CancellationToken cancellationToken = default);

    Task<LearningEvaluationDto> UpsertEvaluationAsync(
        UpsertLearningEvaluationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningEvaluationDto>> ListEvaluationsAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningEvaluationDto>> ListMyEvaluationsAsync(
        CancellationToken cancellationToken = default);

    Task<LearningEvaluationDto> SubmitMyEvaluationFeedbackAsync(
        Guid evaluationId,
        SubmitLearningEvaluationFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<LearningRecognitionDto> CreateRecognitionAsync(
        CreateLearningRecognitionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningRecognitionDto>> ListRecognitionsAsync(
        int take = 30,
        bool mineOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningCustomerFeedbackDto>> ListRecentCustomerFeedbackAsync(
        int hours = 48,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningBadgeDto>> ListMyBadgesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningCareerLevelDto>> ListCareerLevelsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningCareerRosterItemDto>> ListCareerRosterAsync(
        CancellationToken cancellationToken = default);

    Task<LearningCareerPromotionDto> PromoteCareerAsync(
        PromoteLearningCareerRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningCareerPromotionDto>> ListCareerPromotionsAsync(
        int take = 30,
        CancellationToken cancellationToken = default);

    Task<LearningPeopleDashboardDto> GetPeopleDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<LearningEmployeeEvidenceDto?> GetEmployeeEvidenceAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<LearningMyHabitsDto> GetMyHabitsAsync(
        CancellationToken cancellationToken = default);

    Task AcknowledgeModuleAsync(
        Guid moduleId,
        string? selfieUrl = null,
        CancellationToken cancellationToken = default);

    Task StartModuleAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default);

    Task<SubmitLearningQuizResultDto> SubmitQuizAsync(
        Guid moduleId,
        SubmitLearningQuizRequest request,
        CancellationToken cancellationToken = default);

    Task<LearningMonthlyDrillStatusDto> GetMonthlyDrillStatusAsync(
        CancellationToken cancellationToken = default);

    Task<LearningMonthlyDrillStartDto> StartMonthlyDrillAsync(
        CancellationToken cancellationToken = default);

    Task<LearningMonthlyDrillResultDto> SubmitMonthlyDrillAsync(
        SubmitLearningMonthlyDrillRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningObservationPendingDto>> ListPendingObservationsAsync(
        CancellationToken cancellationToken = default);

    Task<LearningObservationDto> SubmitObservationAsync(
        SubmitLearningObservationRequest request,
        CancellationToken cancellationToken = default);

    Task<LearningObservationDto?> GetMyModuleObservationAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default);

    Task<CreateLearningMailThreadsResultDto> CreateMailThreadAsync(
        CreateLearningMailThreadRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningMailThreadListItemDto>> ListMailThreadsAsync(
        CancellationToken cancellationToken = default);

    Task<LearningMailThreadDetailDto?> GetMailThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);

    Task<LearningMailMessageDto> ReplyMailThreadAsync(
        Guid threadId,
        ReplyLearningMailRequest request,
        CancellationToken cancellationToken = default);

    Task MarkMailThreadReadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default);

    Task<LearningMailUnreadCountDto> GetMailUnreadCountAsync(
        CancellationToken cancellationToken = default);
}
