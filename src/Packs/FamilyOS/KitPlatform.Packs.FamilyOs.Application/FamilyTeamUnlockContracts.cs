namespace KitPlatform.Packs.FamilyOs;

public static class FamilyTeamUnlockStatuses
{
    public const string PendingConfirm = "pending_confirm";
    public const string Confirmed = "confirmed";
    public const string Deferred = "deferred";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        PendingConfirm, Confirmed, Deferred,
    };

    public static readonly HashSet<string> Decide = new(StringComparer.OrdinalIgnoreCase)
    {
        Confirmed, Deferred,
    };
}

/// <summary>P1.6 light Team Unlock when 2+ kids both done the same paired mission title.</summary>
public static class FamilySiblingComboUnlock
{
    public const string RewardCode = "sibling_combo_highfive";
    public const string DefaultLabelVi = "High-five đôi anh chị";
}

public sealed record FamilyTeamDayDto(
    DateOnly FlowDate,
    Guid? DayFlowId,
    int TeamDone,
    int TeamTotal,
    int TeamPercent,
    int RemainingMissions,
    bool TeamComplete,
    string HeroMissionLine,
    IReadOnlyList<FamilyTeamChildSliceDto> Children);

public sealed record FamilyTeamChildSliceDto(
    Guid MemberId,
    string DisplayName,
    int Done,
    int Total,
    int Open,
    int Skipped);

public sealed record FamilyTeamUnlockDto(
    Guid Id,
    Guid FamilyId,
    Guid DayFlowId,
    DateOnly FlowDate,
    string RewardCode,
    string LabelVi,
    Guid? AgreementId,
    int TeamDone,
    int TeamTotal,
    int TeamPercent,
    string Status,
    Guid? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    string? DecisionNote,
    DateTimeOffset CreatedAt);

public sealed record FamilyTeamUnlockDecideRequest(
    string Status,
    Guid ConfirmedBy,
    string? DecisionNote);

public interface IFamilyTeamUnlockService
{
    Task<FamilyTeamDayDto> GetTeamDayAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyTeamUnlockDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Create pending unlock when team day is complete; no-op otherwise.</summary>
    Task<FamilyTeamUnlockDto?> EnsurePendingAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>P1.6 — light unlock when 2+ children both done the same mission title today.</summary>
    Task<FamilyTeamUnlockDto?> EnsureSiblingComboPendingAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<FamilyTeamUnlockDto> DecideAsync(
        Guid familyId,
        Guid unlockId,
        FamilyTeamUnlockDecideRequest request,
        CancellationToken cancellationToken = default);
}
