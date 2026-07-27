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
    bool IsThinData,
    IReadOnlyList<string>? DeepHighlightsVi = null,
    bool HasAiPlusDeep = false);

/// <summary>Family Replay chữ — EOM/EOY memory narrative (no video).</summary>
public sealed record FamilyReplaySceneDto(
    DateOnly? Date,
    string Icon,
    string TitleVi,
    string? DetailVi,
    string Kind);

public sealed record FamilyReplayDto(
    Guid FamilyId,
    string FamilyName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateTimeOffset GeneratedAt,
    string MonthLabelVi,
    string TitleVi,
    string OpeningVi,
    IReadOnlyList<FamilyReplaySceneDto> Scenes,
    string ClosingVi,
    string ShareTextVi,
    bool IsThinData,
    bool HasAiPlusDeep = false);

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

    Task<FamilyReplayDto> GetMonthlyReplayAsync(
        Guid familyId,
        DateOnly? month = null,
        CancellationToken cancellationToken = default);
}
