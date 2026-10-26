using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class TradeRepository : ITradeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public TradeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<Trade?> GetByIdAsync(
        Guid tradeId,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireCurrent(TenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Trade>(
            """
            SELECT
                trade_id AS TradeId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                trading_account_id AS TradingAccountId,
                external_trade_id AS ExternalTradeId,
                symbol AS Symbol,
                side AS Side,
                quantity AS Quantity,
                price AS Price,
                total_value AS TotalValue,
                fee_amount AS FeeAmount,
                fee_currency AS FeeCurrency,
                executed_at AS ExecutedAt,
                recorded_at AS RecordedAt,
                source AS Source,
                source_type AS SourceType,
                is_observed AS IsObserved,
                corrects_trade_id AS CorrectsTradeId,
                order_id AS OrderId,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.trade
            WHERE trade_id = @TradeId
              AND tenant_id = @TenantId
            """,
            new { TradeId = tradeId, TenantId });
    }

    public async Task<Trade?> GetByExternalIdAsync(
        Guid tradingAccountId,
        string externalTradeId,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireCurrent(TenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Trade>(
            """
            SELECT
                trade_id AS TradeId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                trading_account_id AS TradingAccountId,
                external_trade_id AS ExternalTradeId,
                symbol AS Symbol,
                side AS Side,
                quantity AS Quantity,
                price AS Price,
                total_value AS TotalValue,
                fee_amount AS FeeAmount,
                fee_currency AS FeeCurrency,
                executed_at AS ExecutedAt,
                recorded_at AS RecordedAt,
                source AS Source,
                source_type AS SourceType,
                is_observed AS IsObserved,
                corrects_trade_id AS CorrectsTradeId,
                order_id AS OrderId,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.trade
            WHERE trading_account_id = @TradingAccountId
              AND external_trade_id = @ExternalTradeId
              AND tenant_id = @TenantId
            """,
            new { TradingAccountId = tradingAccountId, TenantId, ExternalTradeId = externalTradeId });
    }

    public async Task<IReadOnlyList<Trade>> ListForPersonAsync(
        Guid personId,
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireMatch(TenantId, tenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        
        var sql = """
            SELECT
                trade_id AS TradeId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                trading_account_id AS TradingAccountId,
                external_trade_id AS ExternalTradeId,
                symbol AS Symbol,
                side AS Side,
                quantity AS Quantity,
                price AS Price,
                total_value AS TotalValue,
                fee_amount AS FeeAmount,
                fee_currency AS FeeCurrency,
                executed_at AS ExecutedAt,
                recorded_at AS RecordedAt,
                source AS Source,
                source_type AS SourceType,
                is_observed AS IsObserved,
                corrects_trade_id AS CorrectsTradeId,
                order_id AS OrderId,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.trade
            WHERE person_id = @PersonId
              AND tenant_id = @TenantId
            """;

        if (from is not null)
            sql += " AND executed_at >= @From";
        if (to is not null)
            sql += " AND executed_at <= @To";

        sql += " ORDER BY executed_at";

        var trades = await conn.QueryAsync<Trade>(sql, new { PersonId = personId, TenantId, From = from, To = to });
        return trades.ToList();
    }

    public async Task<IReadOnlyList<Trade>> ListBySymbolAsync(
        Guid personId,
        Guid tenantId,
        string symbol,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireMatch(TenantId, tenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var trades = await conn.QueryAsync<Trade>(
            """
            SELECT
                trade_id AS TradeId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                trading_account_id AS TradingAccountId,
                external_trade_id AS ExternalTradeId,
                symbol AS Symbol,
                side AS Side,
                quantity AS Quantity,
                price AS Price,
                total_value AS TotalValue,
                fee_amount AS FeeAmount,
                fee_currency AS FeeCurrency,
                executed_at AS ExecutedAt,
                recorded_at AS RecordedAt,
                source AS Source,
                source_type AS SourceType,
                is_observed AS IsObserved,
                corrects_trade_id AS CorrectsTradeId,
                order_id AS OrderId,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.trade
            WHERE person_id = @PersonId
              AND tenant_id = @TenantId
              AND symbol = @Symbol
            ORDER BY executed_at
            """,
            new { PersonId = personId, TenantId, Symbol = symbol });

        return trades.ToList();
    }

    public async Task<IReadOnlyList<Trade>> ListForAccountAsync(
        Guid tradingAccountId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireMatch(TenantId, tenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var trades = await conn.QueryAsync<Trade>(
            """
            SELECT
                trade_id AS TradeId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                trading_account_id AS TradingAccountId,
                external_trade_id AS ExternalTradeId,
                symbol AS Symbol,
                side AS Side,
                quantity AS Quantity,
                price AS Price,
                total_value AS TotalValue,
                fee_amount AS FeeAmount,
                fee_currency AS FeeCurrency,
                executed_at AS ExecutedAt,
                recorded_at AS RecordedAt,
                source AS Source,
                source_type AS SourceType,
                is_observed AS IsObserved,
                corrects_trade_id AS CorrectsTradeId,
                order_id AS OrderId,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.trade
            WHERE trading_account_id = @TradingAccountId
              AND tenant_id = @TenantId
            ORDER BY executed_at
            """,
            new { TradingAccountId = tradingAccountId, TenantId });

        return trades.ToList();
    }

    public async Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        Guid tradingAccountId,
        string externalTradeId,
        string symbol,
        string side,
        decimal quantity,
        decimal price,
        decimal? totalValue,
        decimal? feeAmount,
        string? feeCurrency,
        DateTimeOffset executedAt,
        string source,
        string sourceType,
        bool isObserved,
        Guid? correctsTradeId,
        Guid? orderId,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        TenantScope.RequireMatch(TenantId, tenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var tradeId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            INSERT INTO pack_personal_intelligence.trade (
                person_id,
                tenant_id,
                trading_account_id,
                external_trade_id,
                symbol,
                side,
                quantity,
                price,
                total_value,
                fee_amount,
                fee_currency,
                executed_at,
                source,
                source_type,
                is_observed,
                corrects_trade_id,
                order_id,
                created_by
            )
            VALUES (
                @PersonId,
                @TenantId,
                @TradingAccountId,
                @ExternalTradeId,
                @Symbol,
                @Side,
                @Quantity,
                @Price,
                @TotalValue,
                @FeeAmount,
                @FeeCurrency,
                @ExecutedAt,
                @Source,
                @SourceType,
                @IsObserved,
                @CorrectsTradeId,
                @OrderId,
                @CreatedBy
            )
            ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING
            RETURNING trade_id
            """,
            new
            {
                PersonId = personId,
                TenantId,
                TradingAccountId = tradingAccountId,
                ExternalTradeId = externalTradeId,
                Symbol = symbol,
                Side = side,
                Quantity = quantity,
                Price = price,
                TotalValue = totalValue,
                FeeAmount = feeAmount,
                FeeCurrency = feeCurrency,
                ExecutedAt = executedAt,
                Source = source,
                SourceType = sourceType,
                IsObserved = isObserved,
                CorrectsTradeId = correctsTradeId,
                OrderId = orderId,
                CreatedBy = createdBy
            });

        if (tradeId is not null)
            return tradeId.Value;

        var existing = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT trade_id
            FROM pack_personal_intelligence.trade
            WHERE trading_account_id = @TradingAccountId
              AND external_trade_id = @ExternalTradeId
              AND tenant_id = @TenantId
            """,
            new { TradingAccountId = tradingAccountId, ExternalTradeId = externalTradeId, TenantId });

        return existing ?? throw new InvalidOperationException("The existing trade is outside the current tenant.");
    }
}
