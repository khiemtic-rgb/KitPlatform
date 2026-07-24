namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Family Coach Daily Insight — rule-based observation (not free-form LLM chat).
/// </summary>
public sealed record FamilyCoachInsightDto(
    DateOnly FlowDate,
    string Headline,
    string? Strength,
    string? Attention,
    string? Pattern,
    string? Proposal,
    string? ProposalCode,
    string? CtaPath,
    string? CtaLabel,
    Guid? FocusMemberId,
    string? FocusMemberName,
    Guid? FocusTemplateId,
    string? FocusCommitmentTitle,
    int DoneCount,
    int SkippedCount,
    int OpenCount,
    int TotalCount,
    int PatternForgotCount,
    int PatternWindowDays);

public static class FamilyCoachProposalCodes
{
    public const string SuggestMoveAfterDinner = "suggest_move_after_dinner";
    public const string SuggestMoveAfterSchool = "suggest_move_after_school";
    public const string OpenToday = "open_today";
    public const string SupportOverdue = "support_overdue";
}

public interface IFamilyCoachInsightService
{
    Task<FamilyCoachInsightDto> GetInsightAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);
}
