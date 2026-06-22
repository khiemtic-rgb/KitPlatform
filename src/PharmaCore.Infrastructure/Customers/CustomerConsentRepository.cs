using System.Data;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Customers;
using PharmaCore.Application.Integration;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Customers;

internal sealed class CustomerConsentRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IIntegrationOutboxWriter _outbox;

    public CustomerConsentRepository(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IIntegrationOutboxWriter outbox)
    {
        _db = db;
        _tenant = tenant;
        _outbox = outbox;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { CustomerId = customerId, TenantId });
    }

    public async Task<IReadOnlyList<CustomerConsentDto>> GetConsentsAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id, customer_id AS CustomerId, channel AS Channel, purpose AS Purpose,
                granted AS Granted, granted_at AS GrantedAt, revoked_at AS RevokedAt,
                source AS Source, notes AS Notes
            FROM customer_consents
            WHERE tenant_id = @TenantId AND customer_id = @CustomerId
            ORDER BY purpose, channel
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerConsentDto>(sql, new { TenantId, CustomerId = customerId })).ToList();
    }

    public async Task<IReadOnlyList<CustomerConsentDto>> UpsertConsentsAsync(
        Guid customerId,
        UpsertCustomerConsentsRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng đồng ý.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var exists = await conn.QuerySingleAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """,
            new { CustomerId = customerId, TenantId }, tx);
        if (!exists)
            throw new InvalidOperationException("Khách hàng không tồn tại.");

        foreach (var item in request.Items)
        {
            var now = DateTime.UtcNow;
            const string upsertSql = """
                INSERT INTO customer_consents (
                    tenant_id, customer_id, channel, purpose, granted, granted_at, revoked_at,
                    source, captured_by, notes
                )
                VALUES (
                    @TenantId, @CustomerId, @Channel, @Purpose, @Granted,
                    @GrantedAt, @RevokedAt, @Source, @CapturedBy, @Notes
                )
                ON CONFLICT (tenant_id, customer_id, channel, purpose) DO UPDATE SET
                    granted = EXCLUDED.granted,
                    granted_at = EXCLUDED.granted_at,
                    revoked_at = EXCLUDED.revoked_at,
                    source = EXCLUDED.source,
                    captured_by = EXCLUDED.captured_by,
                    notes = EXCLUDED.notes,
                    updated_at = NOW()
                RETURNING id
                """;
            var consentId = await conn.QuerySingleAsync<Guid>(upsertSql, new
            {
                TenantId,
                CustomerId = customerId,
                item.Channel,
                item.Purpose,
                item.Granted,
                GrantedAt = item.Granted ? now : (DateTime?)null,
                RevokedAt = item.Granted ? (DateTime?)null : now,
                item.Source,
                CapturedBy = userId,
                item.Notes,
            }, tx);

            await _outbox.WriteAsync(
                conn, tx,
                IntegrationOutboxEventTypes.CustomerConsentUpdated,
                IntegrationOutboxAggregateTypes.CustomerConsent,
                consentId,
                new
                {
                    consentId,
                    customerId,
                    item.Channel,
                    item.Purpose,
                    item.Granted,
                    item.Source,
                },
                userId,
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return await GetConsentsAsync(customerId, cancellationToken);
    }
}

internal sealed class CustomerConsentService : ICustomerConsentService
{
    private readonly CustomerConsentRepository _repository;
    private readonly ITenantContext _tenant;

    public CustomerConsentService(CustomerConsentRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        _repository.CustomerExistsAsync(customerId, cancellationToken);

    public async Task<IReadOnlyList<CustomerConsentDto>> GetConsentsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repository.CustomerExistsAsync(customerId, cancellationToken))
            return [];
        return await _repository.GetConsentsAsync(customerId, cancellationToken);
    }

    public Task<IReadOnlyList<CustomerConsentDto>> UpsertConsentsAsync(
        Guid customerId,
        UpsertCustomerConsentsRequest request,
        CancellationToken cancellationToken = default) =>
        _repository.UpsertConsentsAsync(customerId, request, _tenant.UserId, cancellationToken);
}
