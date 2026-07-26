namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Family Weekly Report — Evidence Engine. Every number is aggregated from real
/// day-flow / commitment / star / reminder data. Provenance fields (DataDays,
/// IsPartial, RemindersTracked) tell the UI when a figure is incomplete so we
/// never present an estimate as a fact.
/// </summary>
public sealed record FamilyWeeklyReportDto(
    Guid FamilyId,
    string Timezone,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Days,
    int DataDays,
    bool IsPartial,
    string? Note,
    DateTimeOffset GeneratedAt,
    int TotalCommitments,
    int DoneCount,
    int OnTimeDoneCount,
    int LateDoneCount,
    int SkippedCount,
    int PendingCount,
    double? CompletionRate,
    double? OnTimeRate,
    int StarsEarned,
    FamilyHealthScoreDto Health,
    FamilyWeeklyReminderDto Reminders,
    IReadOnlyList<FamilyWeeklyMemberDto> Members,
    IReadOnlyList<FamilyWeeklyHabitDto> Habits,
    IReadOnlyList<string> Highlights,
    /// <summary>Family Mirror — reflective weekly view (opt-in parents only).</summary>
    FamilyMirrorDto Mirror);

/// <summary>
/// Family Health Score — weighted blend of measurable signals only.
/// Base weights: completion 30 · reminder calm 30 · streak 20 · on-time 20.
/// When opt-in parent check-ins exist, a light ParentProgress leg (10%) is mixed in
/// without punishing families that have no parent data yet.
/// Never invents a score when there is no data (Score = null).
/// </summary>
public sealed record FamilyHealthScoreDto(
    int? Score,
    int? Completion,
    int? ReminderCalm,
    int? Streak,
    int? OnTime,
    string? Label,
    string? PromiseLine,
    /// <summary>Parent check-in rate 0–100 when shared goals have data; else null.</summary>
    int? ParentProgress = null);

/// <summary>
/// Non-judgmental weekly mirror: child routines + opt-in parent habits + household wins.
/// Parent slice is empty unless at least one goal has <c>share_with_family = true</c>.
/// </summary>
public sealed record FamilyMirrorDto(
    FamilyMirrorChildDto Child,
    FamilyMirrorParentDto Parent,
    FamilyMirrorHouseholdDto Household,
    IReadOnlyList<string> Reflections,
    FamilyMirrorChallengeDto? Challenge = null);

public sealed record FamilyMirrorChallengeDto(
    Guid ChallengeId,
    string Title,
    string Status,
    string RewardLabel,
    int LegsComplete,
    int LegsTotal);

public sealed record FamilyMirrorChildDto(
    int MemberCount,
    int TotalCommitments,
    int DoneCount,
    double? CompletionRate,
    int StarsEarned,
    int BestStreakDays,
    IReadOnlyList<FamilyWeeklyMemberDto> Members);

public sealed record FamilyMirrorParentDto(
    bool AnyShared,
    int SharedGoalCount,
    int CheckinDoneCount,
    int CheckinExpectedCount,
    double? CheckinRate,
    IReadOnlyList<FamilyMirrorParentGoalDto> Goals);

public sealed record FamilyMirrorParentGoalDto(
    Guid GoalId,
    Guid MemberId,
    string MemberName,
    string Title,
    string? Emoji,
    int TargetDaysPerWeek,
    int DoneDays,
    bool TodayDone);

public sealed record FamilyMirrorHouseholdDto(
    int TeamUnlocksConfirmed,
    int StarsEarned,
    int ReminderCount,
    bool RemindersTracked);

public sealed record FamilyWeeklyReminderDto(
    bool Tracked,
    int Count,
    int PreviousCount,
    double? DeltaPct);

public sealed record FamilyWeeklyMemberDto(
    Guid? MemberId,
    string Name,
    int TotalCommitments,
    int DoneCount,
    int OnTimeDoneCount,
    int SkippedCount,
    double? CompletionRate,
    int StarsEarned,
    int CurrentStreakDays);

public sealed record FamilyWeeklyHabitDto(
    Guid? TemplateId,
    string Title,
    string? MemberName,
    int Occurrences,
    int DoneCount,
    int ForgotCount,
    double? DoneRate,
    double? PreviousDoneRate,
    string Trend);

public static class FamilyHabitTrends
{
    public const string Up = "up";
    public const string Down = "down";
    public const string Flat = "flat";
    public const string New = "new";
}

public interface IFamilyInsightService
{
    /// <summary>
    /// Builds a weekly report ending at <paramref name="asOf"/> (family-local
    /// date, default = today). Compares against the preceding equal-length window.
    /// </summary>
    Task<FamilyWeeklyReportDto> GetWeeklyReportAsync(
        Guid familyId,
        DateOnly? asOf = null,
        int days = 7,
        CancellationToken cancellationToken = default);
}
