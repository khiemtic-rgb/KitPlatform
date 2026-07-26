using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyStarLedgerRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyStarLedgerRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<int> GetBalanceAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(SUM(delta), 0)::int
            FROM pack_family.star_ledger
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
    }

    public async Task<IReadOnlyDictionary<Guid, LedgerRow>> ListForCommitmentsAsync(
        IEnumerable<Guid> commitmentIds,
        CancellationToken cancellationToken)
    {
        var ids = commitmentIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, LedgerRow>();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LedgerRow>(
            """
            SELECT
                commitment_id AS CommitmentId,
                delta AS Delta,
                tier AS Tier,
                star_reward AS StarReward,
                late_minutes AS LateMinutes
            FROM pack_family.star_ledger
            WHERE tenant_id = @TenantId
              AND commitment_id = ANY(@CommitmentIds)
            """,
            new { TenantId, CommitmentIds = ids });
        return rows.ToDictionary(r => r.CommitmentId);
    }

    public async Task<StarAwardDto?> ApplyDoneAsync(
        Guid familyId,
        Guid commitmentId,
        Guid memberId,
        StarAwardResult award,
        int starReward,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.star_ledger (
                tenant_id, family_id, member_id, commitment_id,
                delta, tier, star_reward, late_minutes
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @CommitmentId,
                @Delta, @Tier, @StarReward, @LateMinutes
            )
            -- Match partial unique index ux_star_ledger_commitment (commitment_id IS NOT NULL).
            ON CONFLICT (tenant_id, commitment_id) WHERE commitment_id IS NOT NULL DO UPDATE SET
                member_id = EXCLUDED.member_id,
                delta = EXCLUDED.delta,
                tier = EXCLUDED.tier,
                star_reward = EXCLUDED.star_reward,
                late_minutes = EXCLUDED.late_minutes,
                created_at = NOW()
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                CommitmentId = commitmentId,
                award.Delta,
                award.Tier,
                StarReward = starReward,
                award.LateMinutes,
            },
            tx);

        var balance = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(SUM(delta), 0)::int
            FROM pack_family.star_ledger
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId },
            tx);

        await tx.CommitAsync(cancellationToken);

        return new StarAwardDto(
            award.Delta,
            balance,
            award.Tier,
            award.LateMinutes,
            award.LabelVi);
    }

    public async Task RemoveForCommitmentAsync(
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            DELETE FROM pack_family.star_ledger
            WHERE tenant_id = @TenantId AND commitment_id = @CommitmentId
            """,
            new { TenantId, CommitmentId = commitmentId });
    }

    internal sealed class LedgerRow
    {
        public Guid CommitmentId { get; init; }
        public int Delta { get; init; }
        public string Tier { get; init; } = "";
        public int StarReward { get; init; }
        public int? LateMinutes { get; init; }
    }
}
