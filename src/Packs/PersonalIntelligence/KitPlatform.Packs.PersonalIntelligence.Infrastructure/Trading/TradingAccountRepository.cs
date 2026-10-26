using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class TradingAccountRepository : ITradingAccountRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public TradingAccountRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<TradingAccount?> GetByIdAsync(
        Guid tradingAccountId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TradingAccount>(
            """
            SELECT
                trading_account_id AS TradingAccountId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                exchange_id AS ExchangeId,
                external_account_id AS ExternalAccountId,
                account_name AS AccountName,
                account_type AS AccountType,
                status AS Status,
                row_version AS RowVersion,
                created_at AS CreatedAt,
                created_by AS CreatedBy,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                deleted_at AS DeletedAt
            FROM pack_personal_intelligence.trading_account
            WHERE trading_account_id = @TradingAccountId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """,
            new { TradingAccountId = tradingAccountId, TenantId = tenantId });
    }

    public async Task<TradingAccount?> GetByExternalIdAsync(
        Guid personId,
        Guid exchangeId,
        string externalAccountId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TradingAccount>(
            """
            SELECT
                trading_account_id AS TradingAccountId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                exchange_id AS ExchangeId,
                external_account_id AS ExternalAccountId,
                account_name AS AccountName,
                account_type AS AccountType,
                status AS Status,
                row_version AS RowVersion,
                created_at AS CreatedAt,
                created_by AS CreatedBy,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                deleted_at AS DeletedAt
            FROM pack_personal_intelligence.trading_account
            WHERE person_id = @PersonId
              AND exchange_id = @ExchangeId
              AND external_account_id = @ExternalAccountId
              AND deleted_at IS NULL
            """,
            new { PersonId = personId, ExchangeId = exchangeId, ExternalAccountId = externalAccountId });
    }

    public async Task<IReadOnlyList<TradingAccount>> ListForPersonAsync(
        Guid personId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var accounts = await conn.QueryAsync<TradingAccount>(
            """
            SELECT
                trading_account_id AS TradingAccountId,
                person_id AS PersonId,
                tenant_id AS TenantId,
                exchange_id AS ExchangeId,
                external_account_id AS ExternalAccountId,
                account_name AS AccountName,
                account_type AS AccountType,
                status AS Status,
                row_version AS RowVersion,
                created_at AS CreatedAt,
                created_by AS CreatedBy,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                deleted_at AS DeletedAt
            FROM pack_personal_intelligence.trading_account
            WHERE person_id = @PersonId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            ORDER BY created_at DESC
            """,
            new { PersonId = personId, TenantId = tenantId });

        return accounts.ToList();
    }

    public async Task<Guid> CreateAsync(
        Guid personId,
        Guid tenantId,
        Guid exchangeId,
        string externalAccountId,
        string? accountName,
        string? accountType,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var tradingAccountId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_personal_intelligence.trading_account (
                person_id,
                tenant_id,
                exchange_id,
                external_account_id,
                account_name,
                account_type,
                created_by,
                updated_by
            )
            VALUES (
                @PersonId,
                @TenantId,
                @ExchangeId,
                @ExternalAccountId,
                @AccountName,
                @AccountType,
                @CreatedBy,
                @CreatedBy
            )
            ON CONFLICT (person_id, exchange_id, external_account_id) DO UPDATE
                SET person_id = EXCLUDED.person_id
            RETURNING trading_account_id
            """,
            new
            {
                PersonId = personId,
                TenantId = tenantId,
                ExchangeId = exchangeId,
                ExternalAccountId = externalAccountId,
                AccountName = accountName,
                AccountType = accountType,
                CreatedBy = createdBy
            });

        return tradingAccountId;
    }
}
