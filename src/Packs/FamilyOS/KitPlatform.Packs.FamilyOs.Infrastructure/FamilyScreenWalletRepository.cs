using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyScreenWalletRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyScreenWalletRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        w.id AS Id,
        w.family_id AS FamilyId,
        w.member_id AS MemberId,
        m.display_name AS MemberName,
        w.iso_year AS IsoYear,
        w.iso_week AS IsoWeek,
        w.budget_minutes AS BudgetMinutes,
        w.spent_minutes AS SpentMinutes,
        w.earned_minutes AS EarnedMinutes,
        w.granted_minutes AS GrantedMinutes,
        w.status AS Status
        """;

    public async Task<IReadOnlyList<WalletRow>> ListWeekAsync(
        Guid familyId,
        int isoYear,
        int isoWeek,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<WalletRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.screen_time_wallet w
            INNER JOIN pack_family.membership m
                ON m.tenant_id = w.tenant_id AND m.id = w.member_id AND m.deleted_at IS NULL
            WHERE w.tenant_id = @TenantId
              AND w.family_id = @FamilyId
              AND w.iso_year = @IsoYear
              AND w.iso_week = @IsoWeek
              AND w.deleted_at IS NULL
            ORDER BY m.display_name
            """,
            new { TenantId, FamilyId = familyId, IsoYear = isoYear, IsoWeek = isoWeek });
        return rows.AsList();
    }

    public async Task<WalletRow?> GetAsync(
        Guid familyId,
        Guid walletId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<WalletRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.screen_time_wallet w
            INNER JOIN pack_family.membership m
                ON m.tenant_id = w.tenant_id AND m.id = w.member_id AND m.deleted_at IS NULL
            WHERE w.tenant_id = @TenantId
              AND w.family_id = @FamilyId
              AND w.id = @WalletId
              AND w.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, WalletId = walletId });
    }

    public async Task<WalletRow?> GetMemberWeekAsync(
        Guid familyId,
        Guid memberId,
        int isoYear,
        int isoWeek,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<WalletRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.screen_time_wallet w
            INNER JOIN pack_family.membership m
                ON m.tenant_id = w.tenant_id AND m.id = w.member_id AND m.deleted_at IS NULL
            WHERE w.tenant_id = @TenantId
              AND w.family_id = @FamilyId
              AND w.member_id = @MemberId
              AND w.iso_year = @IsoYear
              AND w.iso_week = @IsoWeek
              AND w.deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                IsoYear = isoYear,
                IsoWeek = isoWeek,
            });
    }

    public async Task<Guid> UpsertProposedAsync(
        Guid familyId,
        Guid memberId,
        int isoYear,
        int isoWeek,
        int budgetMinutes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.screen_time_wallet (
                tenant_id, family_id, member_id, iso_year, iso_week,
                budget_minutes, status
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @IsoYear, @IsoWeek,
                @BudgetMinutes, 'proposed'
            )
            ON CONFLICT (tenant_id, family_id, member_id, iso_year, iso_week)
            DO UPDATE SET
                budget_minutes = EXCLUDED.budget_minutes,
                status = CASE
                    WHEN pack_family.screen_time_wallet.status = 'active' THEN 'active'
                    ELSE 'proposed'
                END,
                updated_at = NOW(),
                deleted_at = NULL
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                IsoYear = isoYear,
                IsoWeek = isoWeek,
                BudgetMinutes = budgetMinutes,
            });
    }

    public async Task ActivateAsync(
        Guid familyId,
        Guid walletId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.screen_time_wallet
            SET status = 'active', updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @WalletId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, WalletId = walletId });
    }

    public async Task AdjustCountersAsync(
        Guid familyId,
        Guid walletId,
        int spentDelta,
        int earnedDelta,
        int grantedDelta,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.screen_time_wallet
            SET spent_minutes = GREATEST(0, spent_minutes + @SpentDelta),
                earned_minutes = GREATEST(0, earned_minutes + @EarnedDelta),
                granted_minutes = GREATEST(0, granted_minutes + @GrantedDelta),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @WalletId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                WalletId = walletId,
                SpentDelta = spentDelta,
                EarnedDelta = earnedDelta,
                GrantedDelta = grantedDelta,
            });
    }

    public async Task<bool> TryInsertLedgerAsync(
        Guid familyId,
        Guid walletId,
        Guid memberId,
        DateOnly flowDate,
        string entryKind,
        int minutesDelta,
        string? noteVi,
        string sourceRef,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.screen_time_ledger (
                    tenant_id, family_id, wallet_id, member_id, flow_date,
                    entry_kind, minutes_delta, note_vi, source_ref
                )
                VALUES (
                    @TenantId, @FamilyId, @WalletId, @MemberId, @FlowDate,
                    @EntryKind, @MinutesDelta, @NoteVi, @SourceRef
                )
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    WalletId = walletId,
                    MemberId = memberId,
                    FlowDate = flowDate,
                    EntryKind = entryKind,
                    MinutesDelta = minutesDelta,
                    NoteVi = noteVi,
                    SourceRef = sourceRef,
                });
            return true;
        }
        catch
        {
            // unique source — already applied
            return false;
        }
    }

    public async Task<IReadOnlyList<WalletWeekAggRow>> ListRecentActiveWeeksAsync(
        Guid familyId,
        Guid memberId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<WalletWeekAggRow>(
            """
            SELECT budget_minutes AS BudgetMinutes,
                   spent_minutes AS SpentMinutes,
                   earned_minutes AS EarnedMinutes,
                   granted_minutes AS GrantedMinutes,
                   status AS Status
            FROM pack_family.screen_time_wallet
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND deleted_at IS NULL
              AND status = 'active'
            ORDER BY iso_year DESC, iso_week DESC
            LIMIT @Limit
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, Limit = limit });
        return rows.AsList();
    }

    internal sealed class WalletRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = "";
        public int IsoYear { get; init; }
        public int IsoWeek { get; init; }
        public int BudgetMinutes { get; init; }
        public int SpentMinutes { get; init; }
        public int EarnedMinutes { get; init; }
        public int GrantedMinutes { get; init; }
        public string Status { get; init; } = "";
    }

    internal sealed class WalletWeekAggRow
    {
        public int BudgetMinutes { get; init; }
        public int SpentMinutes { get; init; }
        public int EarnedMinutes { get; init; }
        public int GrantedMinutes { get; init; }
        public string Status { get; init; } = "";
    }
}
