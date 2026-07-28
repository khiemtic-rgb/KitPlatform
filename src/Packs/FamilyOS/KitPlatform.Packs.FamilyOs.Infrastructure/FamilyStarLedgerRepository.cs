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

    public async Task<(int Total, int Growth, int Responsibility, int Kindness)> GetBalancesByKindAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<KindBalanceRow>(
            """
            SELECT
                COALESCE(SUM(delta), 0)::int AS Total,
                COALESCE(SUM(CASE WHEN star_kind = 'growth' THEN delta ELSE 0 END), 0)::int AS Growth,
                COALESCE(SUM(CASE WHEN star_kind = 'responsibility' THEN delta ELSE 0 END), 0)::int AS Responsibility,
                COALESCE(SUM(CASE WHEN star_kind = 'kindness' THEN delta ELSE 0 END), 0)::int AS Kindness
            FROM pack_family.star_ledger
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
        return (row.Total, row.Growth, row.Responsibility, row.Kindness);
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
                late_minutes AS LateMinutes,
                star_kind AS StarKind
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
        string starKind,
        CancellationToken cancellationToken)
    {
        var kind = FamilyCurrencyStarKinds.Normalize(starKind);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.star_ledger (
                tenant_id, family_id, member_id, commitment_id,
                delta, tier, star_reward, late_minutes, star_kind
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @CommitmentId,
                @Delta, @Tier, @StarReward, @LateMinutes, @StarKind
            )
            -- Match partial unique index ux_star_ledger_commitment (commitment_id IS NOT NULL).
            ON CONFLICT (tenant_id, commitment_id) WHERE commitment_id IS NOT NULL DO UPDATE SET
                member_id = EXCLUDED.member_id,
                delta = EXCLUDED.delta,
                tier = EXCLUDED.tier,
                star_reward = EXCLUDED.star_reward,
                late_minutes = EXCLUDED.late_minutes,
                star_kind = EXCLUDED.star_kind,
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
                StarKind = kind,
            },
            tx);

        var balances = await conn.QuerySingleAsync<KindBalanceRow>(
            """
            SELECT
                COALESCE(SUM(delta), 0)::int AS Total,
                COALESCE(SUM(CASE WHEN star_kind = 'growth' THEN delta ELSE 0 END), 0)::int AS Growth,
                COALESCE(SUM(CASE WHEN star_kind = 'responsibility' THEN delta ELSE 0 END), 0)::int AS Responsibility,
                COALESCE(SUM(CASE WHEN star_kind = 'kindness' THEN delta ELSE 0 END), 0)::int AS Kindness
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
            balances.Total,
            award.Tier,
            award.LateMinutes,
            award.LabelVi,
            kind,
            balances.Growth,
            balances.Responsibility,
            balances.Kindness);
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
        public string StarKind { get; init; } = FamilyCurrencyStarKinds.Growth;
    }

    private sealed class KindBalanceRow
    {
        public int Total { get; init; }
        public int Growth { get; init; }
        public int Responsibility { get; init; }
        public int Kindness { get; init; }
    }
}
