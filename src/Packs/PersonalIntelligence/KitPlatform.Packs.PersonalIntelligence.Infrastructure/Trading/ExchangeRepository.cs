using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class ExchangeRepository : IExchangeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ExchangeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<Exchange?> GetByIdAsync(
        Guid exchangeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Exchange>(
            """
            SELECT
                exchange_id AS ExchangeId,
                tenant_id AS TenantId,
                exchange_code AS ExchangeCode,
                exchange_name AS ExchangeName,
                exchange_type AS ExchangeType,
                external_provider AS ExternalProvider,
                status AS Status,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.exchange
            WHERE exchange_id = @ExchangeId
              AND tenant_id = @TenantId
            """,
            new { ExchangeId = exchangeId, TenantId });
    }

    public async Task<Exchange?> GetByCodeAsync(
        Guid tenantId,
        string exchangeCode,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Exchange>(
            """
            SELECT
                exchange_id AS ExchangeId,
                tenant_id AS TenantId,
                exchange_code AS ExchangeCode,
                exchange_name AS ExchangeName,
                exchange_type AS ExchangeType,
                external_provider AS ExternalProvider,
                status AS Status,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.exchange
            WHERE tenant_id = @TenantId
              AND exchange_code = @ExchangeCode
            """,
            new { TenantId = tenantId, ExchangeCode = exchangeCode });
    }

    public async Task<IReadOnlyList<Exchange>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var exchanges = await conn.QueryAsync<Exchange>(
            """
            SELECT
                exchange_id AS ExchangeId,
                tenant_id AS TenantId,
                exchange_code AS ExchangeCode,
                exchange_name AS ExchangeName,
                exchange_type AS ExchangeType,
                external_provider AS ExternalProvider,
                status AS Status,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM pack_personal_intelligence.exchange
            WHERE tenant_id = @TenantId
            ORDER BY exchange_name
            """,
            new { TenantId = tenantId });

        return exchanges.ToList();
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        string exchangeCode,
        string exchangeName,
        string? exchangeType,
        string? externalProvider,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var exchangeId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.exchange (
                tenant_id,
                exchange_code,
                exchange_name,
                exchange_type,
                external_provider,
                created_by
            )
            VALUES (
                @TenantId,
                @ExchangeCode,
                @ExchangeName,
                @ExchangeType,
                @ExternalProvider,
                @CreatedBy
            )
            ON CONFLICT (tenant_id, exchange_code) DO UPDATE
                SET tenant_id = EXCLUDED.tenant_id
            RETURNING exchange_id
            """,
            new
            {
                TenantId = tenantId,
                ExchangeCode = exchangeCode,
                ExchangeName = exchangeName,
                ExchangeType = exchangeType,
                ExternalProvider = externalProvider,
                CreatedBy = createdBy
            });

        return exchangeId;
    }
}
