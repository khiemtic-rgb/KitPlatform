namespace KitPlatform.Packs.FamilyOs;

public sealed record DayFlowDto(
    Guid Id,
    Guid FamilyId,
    Guid RoutineId,
    string RoutineName,
    DateOnly FlowDate,
    string Status,
    int TotalCommitments,
    int DoneCount,
    int PendingCount,
    int DueNowCount,
    int OverdueCount,
    int UpcomingCount,
    TimeOnly LocalTime,
    IReadOnlyList<CommitmentDto> Commitments);

public sealed record CommitmentDto(
    Guid Id,
    Guid DayFlowId,
    Guid? TemplateId,
    Guid? MemberId,
    string? MemberName,
    string Title,
    string? Description,
    TimeOnly? WindowStart,
    TimeOnly? WindowEnd,
    int SortOrder,
    string Status,
    string? SkipReason,
    DateTimeOffset? CompletedAt,
    bool IsLateDone,
    string ReminderState,
    string? ReminderLabel,
    string Priority,
    int? ExpectedDurationMinutes,
    string? ContextAnchor,
    IReadOnlyList<Guid> DependsOnTemplateIds,
    string? EvidenceUrl,
    DateTimeOffset? EvidenceUploadedAt,
    bool AllowEarlyComplete,
    int EarlyLeadMinutes,
    int OnTimeGraceMinutes,
    int StarReward,
    int? StarDelta = null,
    string? StarTier = null,
    string? StarLabelVi = null,
    int? MemberStarBalance = null,
    int? ProjectedStarDelta = null,
    string? ProjectedStarLabelVi = null,
    bool StarPosted = false,
    DateTimeOffset? StarComputedAt = null,
    string? HabitStage = null,
    string? HabitStageLabelVi = null,
    int HabitStreakDays = 0,
    bool ReminderSuppressed = false,
    bool NeedsReflection = false,
    string? SuggestedReflectionPrompt = null,
    int EvidenceLevel = 0,
    string? EvidenceLevelLabelVi = null,
    int? ConfidenceScore = null,
    string? ConfidenceLabelVi = null,
    bool NeedsRetrievalCheck = false,
    bool IsLearningMission = false,
    string? MotivationDriver = null,
    string? MotivationCueVi = null,
    string? InterventionLevel = null,
    string? InterventionLabelVi = null,
    bool AllowParentPush = false,
    bool AllowChildChime = false,
    string? ParentAdviceVi = null,
    string? EveningRiskBand = null,
    string? EveningRiskLabelVi = null,
    string? EveningRiskActionVi = null,
    string? BehaviorPatternCode = null,
    string? BehaviorTacticCode = null);

public sealed record EnsureDayFlowRequest(
    DateOnly? FlowDate,
    Guid? RoutineId,
    bool ForceRebuild = false);

public sealed record UpdateCommitmentProgressRequest(
    string Status,
    string? SkipReason,
    string? EvidenceUrl = null,
    bool ParentOverride = false);

public sealed record AddAdHocCommitmentRequest(
    DateOnly? FlowDate,
    Guid? MemberId,
    string Title,
    string? Description = null,
    TimeOnly? WindowStart = null,
    TimeOnly? WindowEnd = null,
    int? ExpectedDurationMinutes = null,
    string? Priority = null);

public sealed record FamilyEvidenceUploadResult(string Url);

/// <summary>F2.5 L2 — structured reflection reasons (not free-text punishment).</summary>
public static class FamilySkipReasons
{
    public const string Forgot = "forgot";
    public const string Busy = "busy";
    public const string NeedHelp = "need_help";
    public const string NotReady = "not_ready";
    public const string Sick = "sick";
    public const string Other = "other";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Forgot, Busy, NeedHelp, NotReady, Sick, Other,
    };

    public static string? LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            Forgot => "Quên",
            Busy => "Bận việc khác",
            NeedHelp => "Cần giúp",
            NotReady => "Chưa sẵn sàng",
            Sick => "Ốm / không khỏe",
            Other => "Lý do khác",
            _ => null,
        };
}

public interface IFamilyDayFlowService
{
    Task<DayFlowDto> EnsureDayFlowAsync(
        Guid familyId,
        EnsureDayFlowRequest request,
        CancellationToken cancellationToken = default);

    Task<DayFlowDto?> GetDayFlowAsync(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken = default);

    Task<CommitmentDto> UpdateCommitmentProgressAsync(
        Guid familyId,
        Guid commitmentId,
        UpdateCommitmentProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<CommitmentDto> ApproveCommitmentStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    /// <summary>One-off mission for a day (template_id null). Survives routine rebuild.</summary>
    Task<CommitmentDto> AddAdHocCommitmentAsync(
        Guid familyId,
        AddAdHocCommitmentRequest request,
        CancellationToken cancellationToken = default);
}
