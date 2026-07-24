namespace KitPlatform.Packs.FamilyOs;

public sealed record AccountabilityDayGlanceDto(
    DateOnly Date,
    bool IsScored,
    bool IsBeautifulDay,
    int ChildDone,
    int ChildSkipped,
    int ChildOpen,
    int ChildLateDone,
    int AppliedConsequences);

public sealed record AccountabilityGlanceDto(
    DateOnly From,
    DateOnly To,
    DateOnly Today,
    bool TodayIsBeautifulDay,
    int CurrentStreak,
    IReadOnlyList<AccountabilityDayGlanceDto> Days);

public interface IFamilyAccountabilityGlanceService
{
    Task<AccountabilityGlanceDto> GetGlanceAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);
}
