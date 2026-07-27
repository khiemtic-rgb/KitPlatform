namespace KitPlatform.Packs.FamilyOs;

/// <summary>Parent Success P1 — AI Wins digest + monthly AI Letter (templated, no LLM).</summary>
public sealed record FamilyAiWinDto(
    string Id,
    string Kind,
    string TitleVi,
    string? NoteVi,
    DateOnly FlowDate,
    string? Icon,
    bool IsFavorite,
    DateTimeOffset HappenedAt);

public sealed record FamilyAiWinsDigestDto(
    DateOnly From,
    DateOnly To,
    int TotalCount,
    string HeadlineVi,
    string SubheadVi,
    IReadOnlyList<FamilyAiWinDto> Wins);

public sealed record FamilyAiLetterDto(
    Guid FamilyId,
    string FamilyName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTimeOffset GeneratedAt,
    string MonthLabelVi,
    string GreetingVi,
    string BodyVi,
    IReadOnlyList<string> HighlightsVi,
    string ClosingVi,
    bool IsThinData);

public interface IFamilyAiDigestService
{
    Task<FamilyAiWinsDigestDto> GetWinsDigestAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<FamilyAiLetterDto> GetMonthlyLetterAsync(
        Guid familyId,
        DateOnly? month = null,
        CancellationToken cancellationToken = default);
}
