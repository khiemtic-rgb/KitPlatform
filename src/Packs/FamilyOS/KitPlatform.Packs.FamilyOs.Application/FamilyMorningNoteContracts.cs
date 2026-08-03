namespace KitPlatform.Packs.FamilyOs;

public sealed record FamilyMorningNoteDto(
    DateOnly FlowDate,
    Guid? MemberId,
    string BodyVi,
    string Tone,
    IReadOnlyList<string> FocusTitles,
    int AgeYears,
    string AgeBand,
    int ParentNudgesLast7Days,
    double RecentStudyDoneRate,
    bool IsTemplate);

public interface IFamilyMorningNoteService
{
    Task<FamilyMorningNoteDto> GetMorningNoteAsync(
        Guid familyId,
        Guid? memberId,
        DateOnly? flowDate,
        CancellationToken cancellationToken = default);
}
