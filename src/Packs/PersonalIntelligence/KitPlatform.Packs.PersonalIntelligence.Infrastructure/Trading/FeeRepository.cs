using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class FeeRepository : IFeeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FeeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<Fee?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Fee>(
            """
            SELECT
                fee_id AS FeeId, person_id AS PersonId, tenant_id AS TenantId,
                trading_account_id AS TradingAccountId, trade_id AS TradeId,
                fee_type AS FeeType, fee_amount AS FeeAmount, fee_currency AS FeeCurrency,
                external_fee_id AS ExternalFeeId, incurred_at AS IncurredAt,
                recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.fee
            WHERE fee_id = @FeeId AND tenant_id = @TenantId
            """,
            new { FeeId = feeId, TenantId });
    }

    public async Task<Fee?> GetByExternalIdAsync(Guid tradingAccountId, string externalFeeId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Fee>(
            """
            SELECT
                fee_id AS FeeId, person_id AS PersonId, tenant_id AS TenantId,
                trading_account_id AS TradingAccountId, trade_id AS TradeId,
                fee_type AS FeeType, fee_amount AS FeeAmount, fee_currency AS FeeCurrency,
                external_fee_id AS ExternalFeeId, incurred_at AS IncurredAt,
                recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.fee
            WHERE trading_account_id = @TradingAccountId
              AND external_fee_id = @ExternalFeeId
            """,
            new { TradingAccountId = tradingAccountId, ExternalFeeId = externalFeeId });
    }

    public async Task<IReadOnlyList<Fee>> ListForPersonAsync(Guid personId, Guid tenantId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var sql = """
            SELECT
                fee_id AS FeeId, person_id AS PersonId, tenant_id AS TenantId,
                trading_account_id AS TradingAccountId, trade_id AS TradeId,
                fee_type AS FeeType, fee_amount AS FeeAmount, fee_currency AS FeeCurrency,
                external_fee_id AS ExternalFeeId, incurred_at AS IncurredAt,
                recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.fee
            WHERE person_id = @PersonId AND tenant_id = @TenantId
            """;
        if (from is not null) sql += " AND incurred_at >= @From";
        if (to is not null) sql += " AND incurred_at <= @To";
        sql += " ORDER BY incurred_at";

        var fees = await conn.QueryAsync<Fee>(sql, new { PersonId = personId, TenantId = tenantId, From = from, To = to });
        return fees.ToList();
    }

    public async Task<IReadOnlyList<Fee>> ListForTradeAsync(Guid tradeId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var fees = await conn.QueryAsync<Fee>(
            """
            SELECT
                fee_id AS FeeId, person_id AS PersonId, tenant_id AS TenantId,
                trading_account_id AS TradingAccountId, trade_id AS TradeId,
                fee_type AS FeeType, fee_amount AS FeeAmount, fee_currency AS FeeCurrency,
                external_fee_id AS ExternalFeeId, incurred_at AS IncurredAt,
                recorded_at AS RecordedAt, source AS Source, source_type AS SourceType,
                created_at AS CreatedAt, created_by AS CreatedBy
            FROM pack_personal_intelligence.fee
            WHERE trade_id = @TradeId
            ORDER BY incurred_at
            """,
            new { TradeId = tradeId });
        return fees.ToList();
    }

    public async Task<Guid> CreateAsync(Guid personId, Guid tenantId, Guid tradingAccountId, Guid? tradeId,
        string feeType, decimal feeAmount, string feeCurrency, string? externalFeeId,
        DateTimeOffset incurredAt, string source, string sourceType, Guid? createdBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var feeId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.fee (
                person_id, tenant_id, trading_account_id, trade_id, fee_type,
                fee_amount, fee_currency, external_fee_id, incurred_at,
                source, source_type, created_by
            )
            VALUES (
                @PersonId, @TenantId, @TradingAccountId, @TradeId, @FeeType,
                @FeeAmount, @FeeCurrency, @ExternalFeeId, @IncurredAt,
                @Source, @SourceType, @CreatedBy
            )
            RETURNING fee_id
            """,
            new
            {
                PersonId = personId, TenantId = tenantId, TradingAccountId = tradingAccountId,
                TradeId = tradeId, FeeType = feeType, FeeAmount = feeAmount,
                FeeCurrency = feeCurrency, ExternalFeeId = externalFeeId,
                IncurredAt = incurredAt, Source = source, SourceType = sourceType, CreatedBy = createdBy
            });
        return feeId;
    }
}
