namespace KitPlatform.Packs.FamilyOs;

public static class FamilyMemoryKinds
{
    public const string BeautifulDay = "beautiful_day";
    public const string StreakMilestone = "streak_milestone";
    public const string Gratitude = "gratitude";
    public const string Photo = "photo";
    public const string TeamUnlock = "team_unlock";
    public const string Reward = "reward";
    public const string FirstTime = "first_time";
    public const string Manual = "manual";
    public const string Help = "help";
    public const string TeamDay = "team_day";
    public const string ParentHabit = "parent_habit";
    public const string ParentVoice = "parent_voice";
    public const string EveningCircle = "evening_circle";
    /// <summary>Child photo/voice moment for family warmth — not study evidence.</summary>
    public const string KidMoment = "kid_moment";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BeautifulDay, StreakMilestone, Gratitude, Photo, TeamUnlock, Reward, FirstTime, Manual,
        Help, TeamDay, ParentHabit, ParentVoice, EveningCircle, KidMoment,
    };
}

public sealed record FamilyMemoryDto(
    Guid Id,
    Guid FamilyId,
    Guid? MemberId,
    string? MemberName,
    DateOnly FlowDate,
    string Kind,
    string TitleVi,
    string? NoteVi,
    string? Icon,
    string? PhotoUrl,
    bool IsFavorite,
    DateTimeOffset HappenedAt);

public sealed record FamilyMemoryCreateRequest(
    DateOnly? FlowDate,
    Guid? MemberId,
    string? Kind,
    string TitleVi,
    string? NoteVi,
    string? Icon,
    string? PhotoUrl);

/// <summary>Monthly recap — “tháng này nhà mình đã sống ra sao”.</summary>
public sealed record FamilyMemoryRecapDto(
    DateOnly From,
    DateOnly To,
    int TotalCount,
    int BeautifulDays,
    int GratitudeCount,
    int PhotoCount,
    int CelebrationCount,
    int BestStreak,
    string HeadlineVi,
    IReadOnlyList<FamilyMemoryDto> Highlights);

public interface IFamilyMemoryService
{
    Task<IReadOnlyList<FamilyMemoryDto>> ListAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        bool favoritesOnly = false,
        int limit = 60,
        Guid? memberId = null,
        CancellationToken cancellationToken = default);

    Task<FamilyMemoryDto> CreateAsync(
        Guid familyId,
        FamilyMemoryCreateRequest request,
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(
        Guid familyId,
        Guid memoryId,
        bool isFavorite,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid familyId,
        Guid memoryId,
        CancellationToken cancellationToken = default);

    Task<FamilyMemoryRecapDto> GetRecapAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Auto-capture from an event. No-op when the same source_ref already exists.</summary>
    Task<bool> TryCaptureAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        string kind,
        string titleVi,
        string? noteVi = null,
        string? icon = null,
        string? photoUrl = null,
        string? sourceRef = null,
        Guid? memberId = null,
        CancellationToken cancellationToken = default);
}
