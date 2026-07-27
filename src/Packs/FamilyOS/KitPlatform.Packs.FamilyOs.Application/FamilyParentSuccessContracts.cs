namespace KitPlatform.Packs.FamilyOs;

/// <summary>Parent Success P0c — Return on Parenting + Family Growth Report (server SoT).</summary>
public sealed record ParentSuccessMetricDto(
    string Id,
    string LabelVi,
    string BeforeDisplay,
    string AfterDisplay,
    string DeltaLabelVi,
    bool Positive,
    string? Unit = null);

public sealed record ParentSuccessRopDto(
    Guid FamilyId,
    int WindowDays,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int DataDays,
    bool IsPartial,
    string? PartialNoteVi,
    DateTimeOffset GeneratedAt,
    /// <summary>Composite 0–100 growth score for the window (null if insufficient data).</summary>
    int? GrowthScore,
    string HeadlineVi,
    string SummaryVi,
    string ReadyToRenewLineVi,
    IReadOnlyList<ParentSuccessMetricDto> Metrics,
    IReadOnlyList<string> GrowthBulletsVi,
    IReadOnlyList<string> OutcomesVi,
    /// <summary>Estimated parenting minutes saved vs early window (nudge delta × 3 min).</summary>
    int MinutesSavedEstimate,
    int ParentNudgesEarly,
    int ParentNudgesLate,
    int SelfStartsEarly,
    int SelfStartsLate,
    int ReminderFiredEarly,
    int ReminderFiredLate,
    int CommitmentDoneEarly,
    int CommitmentDoneLate,
    int HabitGraduations,
    int QualityMoments);

/// <summary>Parent Success P2 — evening 3Q check-in (soft, guardian-only).</summary>
public sealed record ParentSuccessCheckinDto(
    Guid Id,
    Guid FamilyId,
    Guid MemberId,
    DateOnly FlowDate,
    bool QLessNudge,
    bool QLessTension,
    bool QQualityTime,
    string? Note,
    DateTimeOffset UpdatedAt,
    string ReflectionVi);

public sealed record UpsertParentSuccessCheckinRequest(
    Guid MemberId,
    DateOnly? FlowDate,
    bool QLessNudge,
    bool QLessTension,
    bool QQualityTime,
    string? Note);

/// <summary>Light parent recognition — not Currency stars / not child badges.</summary>
public sealed record ParentAchievementDto(
    string Code,
    string TitleVi,
    string DetailVi,
    string Icon,
    bool Unlocked,
    string ProgressHintVi);

public sealed record ParentAchievementsDto(
    Guid FamilyId,
    DateOnly AsOf,
    string HeadlineVi,
    IReadOnlyList<ParentAchievementDto> Items);

/// <summary>Parent Success P3 — Trust Flywheel: tip “Đã thử” → behavior_event.</summary>
public sealed record ParentCoachActedRequest(
    Guid MemberId,
    string TipId,
    string? TipSource,
    string? Slot,
    string? TitleVi,
    DateOnly? FlowDate);

public sealed record ParentCoachActedDto(
    Guid FamilyId,
    Guid MemberId,
    DateOnly FlowDate,
    string TipId,
    bool AlreadyActed,
    string MessageVi,
    IReadOnlyList<string> ActedTipIdsToday);

public interface IFamilyParentSuccessService
{
    Task<ParentSuccessRopDto> GetRopAsync(
        Guid familyId,
        int days = 30,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default);

    Task<ParentSuccessCheckinDto?> GetEveningCheckinAsync(
        Guid familyId,
        Guid memberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<ParentSuccessCheckinDto> UpsertEveningCheckinAsync(
        Guid familyId,
        UpsertParentSuccessCheckinRequest request,
        CancellationToken cancellationToken = default);

    Task<ParentAchievementsDto> ListAchievementsAsync(
        Guid familyId,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default);

    Task<ParentCoachActedDto> RecordCoachActedAsync(
        Guid familyId,
        ParentCoachActedRequest request,
        CancellationToken cancellationToken = default);

    Task<ParentCoachActedDto> ListCoachActedTodayAsync(
        Guid familyId,
        Guid memberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);
}
