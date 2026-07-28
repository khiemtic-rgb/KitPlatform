namespace KitPlatform.Packs.FamilyOs;

public sealed record StarAwardDto(
    int Delta,
    int Balance,
    string Tier,
    int? LateMinutes,
    string LabelVi,
    string? StarKind = null,
    int? GrowthBalance = null,
    int? ResponsibilityBalance = null,
    int? KindnessBalance = null,
    string? CurrencyMessageVi = null);

public sealed record MemberStarBalanceDto(
    Guid MemberId,
    int Balance,
    int Growth = 0,
    int Responsibility = 0,
    int Kindness = 0);

public interface IFamilyStarService
{
    Task<StarAwardDto?> SyncCommitmentStarsAsync(
        Guid familyId,
        Guid commitmentId,
        string newStatus,
        CancellationToken cancellationToken = default);

    Task<StarAwardDto?> ApprovePendingStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task RevokeCommitmentStarsAsync(
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task<int> GetMemberBalanceAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task<MemberStarBalanceDto> GetMemberBalancesAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, StarAwardResult>> GetCommitmentAwardsAsync(
        IEnumerable<Guid> commitmentIds,
        CancellationToken cancellationToken = default);

    /// <summary>Backfill pending/posted stars when a done commitment missed sync (legacy rows).</summary>
    Task RepairMissingPendingStarsAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken = default);
}
