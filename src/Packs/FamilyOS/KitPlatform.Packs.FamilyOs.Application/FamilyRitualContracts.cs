namespace KitPlatform.Packs.FamilyOs;

public static class FamilyRitualCodes
{
    public const string DinnerTogether = "dinner_together";
    public const string ThanksEachOther = "thanks_each_other";
    public const string SharedChore = "shared_chore";

    public static readonly (string Code, string LabelVi, int Sort)[] Defaults =
    [
        (DinnerTogether, "Ăn tối cùng nhau", 1),
        (ThanksEachOther, "Cảm ơn nhau trong tuần", 2),
        (SharedChore, "Một việc nhà làm chung", 3),
    ];
}

public sealed record FamilyRitualDto(
    string Code,
    string LabelVi,
    string Cadence,
    bool DoneThisPeriod,
    DateOnly PeriodStart,
    DateTimeOffset? DoneAt);

public sealed record FamilyRitualCheckinRequest(
    string RitualCode,
    Guid? NotedBy = null,
    string? NoteVi = null);

public interface IFamilyRitualService
{
    Task<IReadOnlyList<FamilyRitualDto>> ListWeekAsync(
        Guid familyId,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default);

    Task<FamilyRitualDto> CheckinAsync(
        Guid familyId,
        FamilyRitualCheckinRequest request,
        CancellationToken cancellationToken = default);
}
