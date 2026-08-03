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
    string? BehaviorTacticCode = null,
    string CommitmentKind = FamilyCommitmentKinds.Chore,
    string EvidencePolicy = FamilyEvidencePolicies.Optional,
    bool EvidenceSatisfied = true,
    DateTimeOffset? EvidenceSatisfiedAt = null,
    string? EvidenceSatisfiedBy = null,
    string? EvidenceGateLabelVi = null,
    DateTimeOffset? StartedAt = null,
    /// <summary>P0.5 — true when duration gate ok (or N/A). Study only.</summary>
    bool StudyDurationMet = true,
    int? StudyMinDurationMinutes = null,
    /// <summary>P0.5 — photo uploaded but not yet parent/retrieval-satisfied.</summary>
    bool EvidenceSubmitted = false);

public sealed record EnsureDayFlowRequest(
    DateOnly? FlowDate,
    Guid? RoutineId,
    bool ForceRebuild = false);

public sealed record UpdateCommitmentProgressRequest(
    string Status,
    string? SkipReason,
    string? EvidenceUrl = null,
    bool ParentOverride = false);

public sealed record SetCommitmentEvidencePolicyRequest(string EvidencePolicy);

/// <summary>P0.5 — parent checklist before study evidence counts for stars.</summary>
public sealed record VerifyCommitmentEvidenceRequest(
    bool IsTodaysWork,
    bool WithinCommitmentWindow,
    bool MatchesCommitment,
    bool OverrideDuration = false,
    string? Note = null);

/// <summary>P0.8 — parent rejects photo evidence; child must re-upload.</summary>
public static class FamilyEvidenceRejectReasons
{
    public const string WrongContent = "wrong_content";
    public const string NotStudy = "not_study";
    public const string NotTodays = "not_todays";
    public const string Unclear = "unclear";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        WrongContent, NotStudy, NotTodays, Unclear,
    };

    public static string LabelVi(string? code) =>
        (code ?? "").Trim().ToLowerInvariant() switch
        {
            WrongContent => "Nội dung không khớp cam kết",
            NotStudy => "Không thấy bài / vở / màn hình học",
            NotTodays => "Không phải bài hôm nay",
            Unclear => "Ảnh chưa rõ — cần gửi lại",
            _ => "Bằng chứng chưa đạt",
        };

    public static string ChildMessageVi(string? code, string commitmentTitle)
    {
        var title = string.IsNullOrWhiteSpace(commitmentTitle) ? "việc học" : commitmentTitle.Trim();
        return (code ?? "").Trim().ToLowerInvariant() switch
        {
            WrongContent =>
                $"Ảnh «{title}» chưa khớp nội dung cam kết. Con gửi lại bài / vở / màn hình học đúng việc nhé.",
            NotStudy =>
                $"Ảnh «{title}» chưa thấy bài học. Con chụp lại vở / sách / màn hình đang học giúp bố mẹ nhé.",
            NotTodays =>
                $"Ảnh «{title}» có vẻ không phải bài hôm nay. Con gửi bằng chứng của hôm nay giúp bố mẹ nhé.",
            Unclear =>
                $"Ảnh «{title}» chưa rõ. Con gửi lại ảnh rõ hơn để bố mẹ xác nhận được sao nhé.",
            _ =>
                $"Bằng chứng «{title}» chưa đạt. Con gửi lại giúp bố mẹ nhé.",
        };
    }
}

public sealed record RejectCommitmentEvidenceRequest(string ReasonCode, string? Note = null);

public sealed record AddAdHocCommitmentRequest(
    DateOnly? FlowDate,
    Guid? MemberId,
    string Title,
    string? Description = null,
    TimeOnly? WindowStart = null,
    TimeOnly? WindowEnd = null,
    int? ExpectedDurationMinutes = null,
    string? Priority = null);


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

    /// <summary>Parent confirms evidence for study_focus (checklist + marks satisfied + posts pending stars).</summary>
    Task<CommitmentDto> VerifyCommitmentEvidenceAsync(
        Guid familyId,
        Guid commitmentId,
        VerifyCommitmentEvidenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Parent rejects photo evidence — clear upload so child re-submits.</summary>
    Task<CommitmentDto> RejectCommitmentEvidenceAsync(
        Guid familyId,
        Guid commitmentId,
        RejectCommitmentEvidenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Pilot / P0: set evidence_policy on a day commitment (e.g. required_hard).</summary>
    Task<CommitmentDto> SetCommitmentEvidencePolicyAsync(
        Guid familyId,
        Guid commitmentId,
        SetCommitmentEvidencePolicyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>One-off mission for a day (template_id null). Survives routine rebuild.</summary>
    Task<CommitmentDto> AddAdHocCommitmentAsync(
        Guid familyId,
        AddAdHocCommitmentRequest request,
        CancellationToken cancellationToken = default);
}
