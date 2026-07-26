namespace KitPlatform.Packs.FamilyOs;

public static class FamilyChallengeStatuses
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Expired = "expired";
    public const string Canceled = "canceled";
}

public static class FamilyChallengeLegKinds
{
    public const string Parent = "parent";
    public const string Child = "child";
    public const string Household = "household";
}

public sealed record FamilyChallengeLegDto(
    Guid Id,
    Guid? MemberId,
    string? MemberName,
    string LegKind,
    string Title,
    string? Emoji,
    int TargetDays,
    int DoneDays,
    bool TodayDone,
    bool IsComplete,
    int SortOrder);

public sealed record FamilyChallengeDto(
    Guid Id,
    Guid FamilyId,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string Status,
    string Title,
    string RewardCode,
    string RewardLabel,
    Guid? AcceptedBy,
    DateTimeOffset? CompletedAt,
    Guid? UnlockId,
    int LegsComplete,
    int LegsTotal,
    IReadOnlyList<FamilyChallengeLegDto> Legs);

public sealed record AcceptFamilyChallengeRequest(
    Guid AcceptedBy,
    string? Title = null,
    string? RewardCode = null,
    string? RewardLabel = null);

public sealed record FamilyChallengeCheckinRequest(
    Guid ActorMemberId,
    string Status,
    DateOnly? Date = null);

public interface IFamilyChallengeService
{
    Task<FamilyChallengeDto?> GetCurrentAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyChallengeDto> AcceptAsync(
        Guid familyId,
        AcceptFamilyChallengeRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyChallengeDto> CheckinLegAsync(
        Guid familyId,
        Guid legId,
        FamilyChallengeCheckinRequest request,
        CancellationToken cancellationToken = default);
}
