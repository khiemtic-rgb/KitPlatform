using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class PortfolioSnapshotRepository : IPortfolioSnapshotRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public PortfolioSnapshotRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<PortfolioSnapshot?> GetByIdAsync(Guid portfolioSnapshotId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PortfolioSnapshot>(
            """
            SELECT portfolio_snapshot_id AS PortfolioSnapshotId, person_id AS PersonId, tenant_id AS TenantId,
                   total_value AS TotalValue, base_currency AS BaseCurrency, cash_balance AS CashBalance,
                   positions_value AS PositionsValue, unrealized_pnl AS UnrealizedPnl,
                   realized_pnl_inception AS RealizedPnlInception, as_of_at AS AsOfAt,
                   recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                   is_observed AS IsObserved, external_snapshot_id AS ExternalSnapshotId,
                   created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.portfolio_snapshot
            WHERE portfolio_snapshot_id = @PortfolioSnapshotId AND tenant_id = @TenantId
            """,
            new { PortfolioSnapshotId = portfolioSnapshotId, TenantId });
    }

    public async Task<PortfolioSnapshot?> GetByKeyAsync(Guid personId, DateTimeOffset asOfAt, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PortfolioSnapshot>(
            """
            SELECT portfolio_snapshot_id AS PortfolioSnapshotId, person_id AS PersonId, tenant_id AS TenantId,
                   total_value AS TotalValue, base_currency AS BaseCurrency, cash_balance AS CashBalance,
                   positions_value AS PositionsValue, unrealized_pnl AS UnrealizedPnl,
                   realized_pnl_inception AS RealizedPnlInception, as_of_at AS AsOfAt,
                   recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                   is_observed AS IsObserved, external_snapshot_id AS ExternalSnapshotId,
                   created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.portfolio_snapshot
            WHERE person_id = @PersonId AND as_of_at = @AsOfAt
            """,
            new { PersonId = personId, AsOfAt = asOfAt });
    }

    public async Task<IReadOnlyList<PortfolioSnapshot>> ListTimelineAsync(Guid personId, Guid tenantId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var sql = """
            SELECT portfolio_snapshot_id AS PortfolioSnapshotId, person_id AS PersonId, tenant_id AS TenantId,
                   total_value AS TotalValue, base_currency AS BaseCurrency, cash_balance AS CashBalance,
                   positions_value AS PositionsValue, unrealized_pnl AS UnrealizedPnl,
                   realized_pnl_inception AS RealizedPnlInception, as_of_at AS AsOfAt,
                   recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                   is_observed AS IsObserved, external_snapshot_id AS ExternalSnapshotId,
                   created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.portfolio_snapshot
            WHERE person_id = @PersonId AND tenant_id = @TenantId
            """;
        if (from is not null) sql += " AND as_of_at >= @From";
        if (to is not null) sql += " AND as_of_at <= @To";
        sql += " ORDER BY as_of_at";

        var snapshots = await conn.QueryAsync<PortfolioSnapshot>(sql, new { PersonId = personId, TenantId = tenantId, From = from, To = to });
        return snapshots.ToList();
    }

    public async Task<PortfolioSnapshot?> GetLatestAsync(Guid personId, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PortfolioSnapshot>(
            """
            SELECT portfolio_snapshot_id AS PortfolioSnapshotId, person_id AS PersonId, tenant_id AS TenantId,
                   total_value AS TotalValue, base_currency AS BaseCurrency, cash_balance AS CashBalance,
                   positions_value AS PositionsValue, unrealized_pnl AS UnrealizedPnl,
                   realized_pnl_inception AS RealizedPnlInception, as_of_at AS AsOfAt,
                   recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                   is_observed AS IsObserved, external_snapshot_id AS ExternalSnapshotId,
                   created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.portfolio_snapshot
            WHERE person_id = @PersonId AND tenant_id = @TenantId
            ORDER BY as_of_at DESC
            LIMIT 1
            """,
            new { PersonId = personId, TenantId = tenantId });
    }

    public async Task<Guid> CreateAsync(Guid personId, Guid tenantId, decimal? totalValue, string baseCurrency,
        decimal? cashBalance, decimal? positionsValue, decimal? unrealizedPnl, decimal? realizedPnlInception,
        DateTimeOffset asOfAt, string source, string sourceType, bool isObserved,
        string? externalSnapshotId, Guid? createdBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var portfolioSnapshotId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.portfolio_snapshot (
                person_id, tenant_id, total_value, base_currency, cash_balance,
                positions_value, unrealized_pnl, realized_pnl_inception, as_of_at,
                source, source_type, is_observed, external_snapshot_id, created_by
            )
            VALUES (
                @PersonId, @TenantId, @TotalValue, @BaseCurrency, @CashBalance,
                @PositionsValue, @UnrealizedPnl, @RealizedPnlInception, @AsOfAt,
                @Source, @SourceType, @IsObserved, @ExternalSnapshotId, @CreatedBy
            )
            ON CONFLICT (person_id, as_of_at) DO UPDATE
                SET person_id = EXCLUDED.person_id
            RETURNING portfolio_snapshot_id
            """,
            new
            {
                PersonId = personId, TenantId = tenantId, TotalValue = totalValue,
                BaseCurrency = baseCurrency, CashBalance = cashBalance, PositionsValue = positionsValue,
                UnrealizedPnl = unrealizedPnl, RealizedPnlInception = realizedPnlInception,
                AsOfAt = asOfAt, Source = source, SourceType = sourceType,
                IsObserved = isObserved, ExternalSnapshotId = externalSnapshotId, CreatedBy = createdBy
            });
        return portfolioSnapshotId;
    }
}
