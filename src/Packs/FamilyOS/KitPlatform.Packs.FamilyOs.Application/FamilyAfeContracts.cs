namespace KitPlatform.Packs.FamilyOs;

// ─── Child request (Smart Proposal) ──────────────────────────────────────────

public static class FamilyChildRequestKinds
{
    public const string ScreenMinutes = "screen_minutes";
    public const string DayMission = "day_mission";
    public const string PauseRoutine = "pause_routine";
    public const string MovieNight = "movie_night";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ScreenMinutes, DayMission, PauseRoutine, MovieNight, Other,
    };
}

public static class FamilyChildRequestReasons
{
    public const string NoExtraClass = "no_extra_class";
    public const string ChoresDone = "chores_done";
    public const string HomeworkDone = "homework_done";
    public const string PlayWithFriend = "play_with_friend";
    public const string Other = "other";

    public static readonly IReadOnlyDictionary<string, string> LabelsVi =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NoExtraClass] = "Hôm nay không có học thêm",
            [ChoresDone] = "Đã hoàn thành việc nhà",
            [HomeworkDone] = "Đã học xong",
            [PlayWithFriend] = "Muốn chơi cùng bạn",
            [Other] = "Lý do khác",
        };
}

public static class FamilyChildRequestStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Partial = "partial";
    public const string Expired = "expired";
}

public sealed record FamilyChildRequestDto(
    Guid Id,
    Guid FamilyId,
    Guid MemberId,
    string MemberName,
    DateOnly FlowDate,
    string Kind,
    int? AmountMinutes,
    string? TitleVi,
    TimeOnly? WindowStart,
    TimeOnly? WindowEnd,
    IReadOnlyList<string> ReasonCodes,
    string? ReasonNote,
    string Status,
    string? AiSummaryVi,
    string? AiRecommend,
    int? GrantedMinutes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);

public sealed record FamilyChildRequestCreateRequest(
    Guid MemberId,
    DateOnly? FlowDate,
    string? Kind,
    int? AmountMinutes,
    IReadOnlyList<string>? ReasonCodes,
    string? ReasonNote,
    string? TitleVi = null,
    TimeOnly? WindowStart = null,
    TimeOnly? WindowEnd = null);

public sealed record FamilyChildRequestDecideRequest(
    Guid DecidedByMemberId,
    string Decision,
    int? GrantedMinutes = null,
    string? Note = null);

public interface IFamilyChildRequestService
{
    Task<IReadOnlyList<FamilyChildRequestDto>> ListAsync(
        Guid familyId,
        string? status = null,
        Guid? memberId = null,
        int limit = 40,
        CancellationToken cancellationToken = default);

    Task<FamilyChildRequestDto> CreateAsync(
        Guid familyId,
        FamilyChildRequestCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyChildRequestDto> DecideAsync(
        Guid familyId,
        Guid requestId,
        FamilyChildRequestDecideRequest request,
        CancellationToken cancellationToken = default);
}

// ─── AI proposal ─────────────────────────────────────────────────────────────

public static class FamilyAiProposalKinds
{
    public const string ScreenBudget = "screen_budget";
    public const string ScreenAdjust = "screen_adjust";
    public const string FamilyMode = "family_mode";
    public const string MovieNight = "movie_night";
    public const string PauseRoutine = "pause_routine";
    public const string RewardMinutes = "reward_minutes";
    /// <summary>Suggest deactivating optional templates on a dense routine (apply from tomorrow).</summary>
    public const string RoutineTrim = "routine_trim";
    public const string Other = "other";
}

public static class FamilyAiProposalStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
}

public sealed record FamilyAiProposalDto(
    Guid Id,
    Guid FamilyId,
    Guid? MemberId,
    string? MemberName,
    string Kind,
    string TitleVi,
    string BodyVi,
    string? PayloadJson,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);

public sealed record FamilyAiProposalDecideRequest(
    Guid DecidedByMemberId,
    string Decision);

public interface IFamilyAiProposalService
{
    Task<IReadOnlyList<FamilyAiProposalDto>> ListPendingAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyAiProposalDto?> TryCreateAsync(
        Guid familyId,
        string kind,
        string titleVi,
        string bodyVi,
        string? payloadJson,
        string sourceRef,
        Guid? memberId = null,
        CancellationToken cancellationToken = default);

    Task<FamilyAiProposalDto> DecideAsync(
        Guid familyId,
        Guid proposalId,
        FamilyAiProposalDecideRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Scan signals and enqueue adaptive proposals (idempotent via source_ref).</summary>
    Task<int> ScanAdaptiveAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}

// ─── Decision Inbox ──────────────────────────────────────────────────────────

public static class FamilyDecisionKinds
{
    public const string AwaitingStars = "awaiting_stars";
    public const string ConsequenceConfirm = "consequence_confirm";
    public const string TeamUnlock = "team_unlock";
    public const string RewardFulfill = "reward_fulfill";
    public const string ChildRequest = "child_request";
    public const string AiProposal = "ai_proposal";
}

public sealed record FamilyDecisionItemDto(
    string Kind,
    string Id,
    string TitleVi,
    string BodyVi,
    string? Recommend,
    Guid? MemberId,
    string? MemberName,
    DateTimeOffset CreatedAt,
    string? RefType,
    Guid? RefId);

public sealed record FamilyDecisionInboxDto(
    int TotalCount,
    string HeadlineVi,
    IReadOnlyList<FamilyDecisionItemDto> Items);

public interface IFamilyDecisionInboxService
{
    Task<FamilyDecisionInboxDto> GetInboxAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}

// ─── Family Mode ─────────────────────────────────────────────────────────────

public static class FamilyModeKinds
{
    public const string Normal = "normal";
    public const string Summer = "summer";
    public const string Exam = "exam";
    public const string Travel = "travel";
    public const string Weekend = "weekend";
    public const string Holiday = "holiday";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Normal, Summer, Exam, Travel, Weekend, Holiday,
    };

    public static string LabelVi(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        Normal => "Bình thường",
        Summer => "Nghỉ hè",
        Exam => "Thi học kỳ",
        Travel => "Du lịch",
        Weekend => "Cuối tuần",
        Holiday => "Nghỉ lễ",
        _ => kind,
    };
}

public sealed record FamilyModeActivateRequest(
    string Mode,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? ActivatedByMemberId,
    bool ConfirmNow = true);

public sealed record FamilyModeActivateResult(
    string Mode,
    string LabelVi,
    CalendarPeriodDto? Period,
    string MessageVi,
    Guid? PrimaryRoutineId = null,
    string? PrimaryRoutineName = null,
    int PrimaryTemplateCount = 0);

public interface IFamilyModeService
{
    Task<FamilyModeActivateResult> ActivateAsync(
        Guid familyId,
        FamilyModeActivateRequest request,
        CancellationToken cancellationToken = default);
}

// ─── Screen Time Wallet ──────────────────────────────────────────────────────

public sealed record FamilyScreenWalletDto(
    Guid Id,
    Guid FamilyId,
    Guid MemberId,
    string MemberName,
    int IsoYear,
    int IsoWeek,
    int BudgetMinutes,
    int SpentMinutes,
    int EarnedMinutes,
    int GrantedMinutes,
    int RemainingMinutes,
    string Status);

public sealed record FamilyScreenWalletProposeRequest(
    Guid MemberId,
    int? BudgetMinutes,
    Guid? ProposedByMemberId);

public sealed record FamilyScreenWalletSpendRequest(
    Guid MemberId,
    int Minutes,
    DateOnly? FlowDate,
    string? Note);

public interface IFamilyScreenWalletService
{
    Task<IReadOnlyList<FamilyScreenWalletDto>> ListWeekAsync(
        Guid familyId,
        int? isoYear = null,
        int? isoWeek = null,
        CancellationToken cancellationToken = default);

    Task<FamilyScreenWalletDto> ProposeBudgetAsync(
        Guid familyId,
        FamilyScreenWalletProposeRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyScreenWalletDto> ActivateAsync(
        Guid familyId,
        Guid walletId,
        Guid decidedByMemberId,
        CancellationToken cancellationToken = default);

    Task<FamilyScreenWalletDto> SpendAsync(
        Guid familyId,
        FamilyScreenWalletSpendRequest request,
        CancellationToken cancellationToken = default);

    Task ApplyGrantAsync(
        Guid familyId,
        Guid memberId,
        int minutes,
        string sourceRef,
        string? noteVi,
        CancellationToken cancellationToken = default);

    Task ApplyEarnAsync(
        Guid familyId,
        Guid memberId,
        int minutes,
        string sourceRef,
        string? noteVi,
        CancellationToken cancellationToken = default);

    int SuggestBudgetMinutes(string? ageBand, string? modeKind);
}

// ─── Family Score ────────────────────────────────────────────────────────────

public sealed record FamilyScoreDto(
    int Score,
    string Band,
    string HeadlineVi,
    bool AllowBonusMinutes,
    int BeautifulDays,
    int BestStreak,
    int RoutinePct,
    bool ChallengeActive);

public interface IFamilyScoreService
{
    Task<FamilyScoreDto> GetWeekScoreAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}
