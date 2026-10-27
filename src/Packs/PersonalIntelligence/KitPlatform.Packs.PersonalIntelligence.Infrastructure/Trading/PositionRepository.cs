using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class PositionRepository : IPositionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public PositionRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<Position?> GetByIdAsync(Guid positionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Position>(
            """
            SELECT position_id AS PositionId, person_id AS PersonId, tenant_id AS TenantId,
                   trading_account_id AS TradingAccountId, symbol AS Symbol, quantity AS Quantity,
                   cost_basis AS CostBasis, market_value AS MarketValue, unrealized_pnl AS UnrealizedPnl,
                   as_of_at AS AsOfAt, recorded_at AS RecordedAt, source AS Source,
                   source_type AS SourceType, is_observed AS IsObserved,
                   external_position_id AS ExternalPositionId, created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.position
            WHERE position_id = @PositionId AND tenant_id = @TenantId
            """,
            new { PositionId = positionId, TenantId });
    }

    public async Task<Position?> GetByKeyAsync(Guid tradingAccountId, string symbol, DateTimeOffset asOfAt, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Position>(
            """
            SELECT position_id AS PositionId, person_id AS PersonId, tenant_id AS TenantId,
                   trading_account_id AS TradingAccountId, symbol AS Symbol, quantity AS Quantity,
                   cost_basis AS CostBasis, market_value AS MarketValue, unrealized_pnl AS UnrealizedPnl,
                   as_of_at AS AsOfAt, recorded_at AS RecordedAt, source AS Source,
                   source_type AS SourceType, is_observed AS IsObserved,
                   external_position_id AS ExternalPositionId, created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.position
            WHERE trading_account_id = @TradingAccountId
              AND symbol = @Symbol
              AND as_of_at = @AsOfAt
            """,
            new { TradingAccountId = tradingAccountId, Symbol = symbol, AsOfAt = asOfAt });
    }

    public async Task<IReadOnlyList<Position>> GetCurrentForPersonAsync(Guid personId, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var positions = await conn.QueryAsync<Position>(
            """
            SELECT position_id AS PositionId, person_id AS PersonId, tenant_id AS TenantId,
                   trading_account_id AS TradingAccountId, symbol AS Symbol, quantity AS Quantity,
                   cost_basis AS CostBasis, market_value AS MarketValue, unrealized_pnl AS UnrealizedPnl,
                   as_of_at AS AsOfAt, recorded_at AS RecordedAt, source AS Source,
                   source_type AS SourceType, is_observed AS IsObserved,
                   external_position_id AS ExternalPositionId, created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.position
            WHERE person_id = @PersonId AND tenant_id = @TenantId
              AND as_of_at = (SELECT MAX(as_of_at) FROM pack_personal_intelligence.position
                              WHERE person_id = @PersonId AND tenant_id = @TenantId)
            ORDER BY symbol
            """,
            new { PersonId = personId, TenantId = tenantId });
        return positions.ToList();
    }

    public async Task<IReadOnlyList<Position>> ListHistoryForSymbolAsync(Guid personId, Guid tenantId, string symbol, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var positions = await conn.QueryAsync<Position>(
            """
            SELECT position_id AS PositionId, person_id AS PersonId, tenant_id AS TenantId,
                   trading_account_id AS TradingAccountId, symbol AS Symbol, quantity AS Quantity,
                   cost_basis AS CostBasis, market_value AS MarketValue, unrealized_pnl AS UnrealizedPnl,
                   as_of_at AS AsOfAt, recorded_at AS RecordedAt, source AS Source,
                   source_type AS SourceType, is_observed AS IsObserved,
                   external_position_id AS ExternalPositionId, created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.position
            WHERE person_id = @PersonId AND tenant_id = @TenantId AND symbol = @Symbol
            ORDER BY as_of_at
            """,
            new { PersonId = personId, TenantId = tenantId, Symbol = symbol });
        return positions.ToList();
    }

    public async Task<Guid> CreateAsync(Guid personId, Guid tenantId, Guid tradingAccountId, string symbol,
        decimal quantity, decimal? costBasis, decimal? marketValue, decimal? unrealizedPnl,
        DateTimeOffset asOfAt, string source, string sourceType, bool isObserved,
        string? externalPositionId, Guid? createdBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var positionId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.position (
                person_id, tenant_id, trading_account_id, symbol, quantity,
                cost_basis, market_value, unrealized_pnl, as_of_at, source,
                source_type, is_observed, external_position_id, created_by
            )
            VALUES (
                @PersonId, @TenantId, @TradingAccountId, @Symbol, @Quantity,
                @CostBasis, @MarketValue, @UnrealizedPnl, @AsOfAt, @Source,
                @SourceType, @IsObserved, @ExternalPositionId, @CreatedBy
            )
            ON CONFLICT (trading_account_id, symbol, as_of_at) DO UPDATE
                SET trading_account_id = EXCLUDED.trading_account_id
            RETURNING position_id
            """,
            new
            {
                PersonId = personId, TenantId = tenantId, TradingAccountId = tradingAccountId,
                Symbol = symbol, Quantity = quantity, CostBasis = costBasis,
                MarketValue = marketValue, UnrealizedPnl = unrealizedPnl, AsOfAt = asOfAt,
                Source = source, SourceType = sourceType, IsObserved = isObserved,
                ExternalPositionId = externalPositionId, CreatedBy = createdBy
            });
        return positionId;
    }
}
