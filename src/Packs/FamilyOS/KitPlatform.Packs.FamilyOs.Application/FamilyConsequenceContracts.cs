namespace KitPlatform.Packs.FamilyOs;

public static class FamilyConsequenceEventStatuses
{
    public const string PendingConfirm = "pending_confirm";
    public const string Applied = "applied";
    public const string Waived = "waived";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        PendingConfirm, Applied, Waived,
    };

    public static readonly HashSet<string> Decide = new(StringComparer.OrdinalIgnoreCase)
    {
        Applied, Waived,
    };
}

public sealed record FamilyConsequenceEventDto(
    Guid Id,
    Guid FamilyId,
    Guid DayFlowId,
    Guid CommitmentId,
    Guid AgreementId,
    Guid? MemberId,
    string? MemberName,
    DateOnly FlowDate,
    string ConsequenceCode,
    string LabelVi,
    string? TriggerSkipReason,
    string CommitmentTitle,
    string Status,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    DateTimeOffset CreatedAt,
    SoftLockGuideDto? SoftLockGuide = null);

public sealed record DecideConsequenceEventRequest(
    string Status,
    Guid DecidedBy,
    string? DecisionNote);

public sealed record SkipConsequenceSuggestRequest(
    Guid DayFlowId,
    Guid CommitmentId,
    Guid? TemplateId,
    Guid? MemberId,
    string CommitmentTitle,
    DateOnly FlowDate,
    string SkipReason);

public interface IFamilyConsequenceService
{
    Task SuggestFromSkipAsync(
        Guid familyId,
        SkipConsequenceSuggestRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyConsequenceEventDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<FamilyConsequenceEventDto> DecideAsync(
        Guid familyId,
        Guid eventId,
        DecideConsequenceEventRequest request,
        CancellationToken cancellationToken = default);
}
