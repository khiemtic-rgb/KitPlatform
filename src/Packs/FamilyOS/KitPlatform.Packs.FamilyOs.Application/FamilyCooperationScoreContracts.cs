namespace KitPlatform.Packs.FamilyOs;

public sealed record FamilyCooperationPillarsDto(
    int TeamCompletion,
    int FamilyStreak,
    int HelpEachOther,
    int TeamUnlock,
    int FamilyHarmony);

public sealed record FamilyCooperationDayPointDto(
    DateOnly ScoreDate,
    int Total);

public sealed record FamilyCooperationScoreDto(
    string Period,
    DateOnly From,
    DateOnly To,
    int Total,
    string HeadlineVi,
    FamilyCooperationPillarsDto Pillars,
    IReadOnlyList<FamilyCooperationDayPointDto> Sparkline);

public interface IFamilyCooperationScoreService
{
    Task<FamilyCooperationScoreDto> GetAsync(
        Guid familyId,
        string? period = "week",
        CancellationToken cancellationToken = default);
}
