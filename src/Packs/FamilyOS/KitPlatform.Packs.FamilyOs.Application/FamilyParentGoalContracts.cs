namespace KitPlatform.Packs.FamilyOs;

/// <summary>Check-in state for a parent goal on a specific day.</summary>
public static class ParentGoalCheckinStatuses
{
    public const string Done = "done";
    public const string Skip = "skip";
    /// <summary>Client-only sentinel: remove today's check-in row.</summary>
    public const string Clear = "clear";
}

/// <summary>
/// Parent Progress goal (opt-in). A guardian/caregiver's own light habit with
/// manual daily check-ins. Never child-facing unless <see cref="ShareWithFamily"/>.
/// </summary>
public sealed record ParentGoalDto(
    Guid Id,
    Guid FamilyId,
    Guid MemberId,
    string MemberName,
    string Title,
    string? Emoji,
    int TargetDaysPerWeek,
    bool ShareWithFamily,
    bool IsActive,
    int SortOrder,
    /// <summary>Today's check-in status: done | skip | null (not checked in).</summary>
    string? TodayStatus,
    /// <summary>Days marked done within the trailing 7-day window (incl. today).</summary>
    int WeekDoneCount,
    /// <summary>Consecutive done days ending today (or yesterday if today blank).</summary>
    int CurrentStreak);

public sealed record CreateParentGoalRequest(
    Guid MemberId,
    string Title,
    string? Emoji = null,
    int? TargetDaysPerWeek = null,
    bool? ShareWithFamily = null);

public sealed record UpdateParentGoalRequest(
    string? Title = null,
    string? Emoji = null,
    int? TargetDaysPerWeek = null,
    bool? ShareWithFamily = null,
    bool? IsActive = null);

public sealed record ParentGoalCheckinRequest(
    string Status,
    DateOnly? Date = null,
    string? Note = null);

/// <summary>
/// Household-visible slice of parent progress — only goals a parent opted to share.
/// Used by Family Mirror / home "Gia đình hôm nay".
/// </summary>
public sealed record SharedParentProgressDto(
    Guid MemberId,
    string MemberName,
    Guid GoalId,
    string Title,
    string? Emoji,
    int TargetDaysPerWeek,
    bool TodayDone,
    int WeekDoneCount);

public interface IFamilyParentGoalService
{
    /// <summary>Goals for one parent member (their own view — includes private goals).</summary>
    Task<IReadOnlyList<ParentGoalDto>> ListForMemberAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task<ParentGoalDto> CreateAsync(
        Guid familyId,
        CreateParentGoalRequest request,
        CancellationToken cancellationToken = default);

    Task<ParentGoalDto> UpdateAsync(
        Guid familyId,
        Guid goalId,
        UpdateParentGoalRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid familyId,
        Guid goalId,
        CancellationToken cancellationToken = default);

    Task<ParentGoalDto> CheckinAsync(
        Guid familyId,
        Guid goalId,
        ParentGoalCheckinRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Only opt-in shared goals across the family (privacy-safe household view).</summary>
    Task<IReadOnlyList<SharedParentProgressDto>> ListSharedAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}
