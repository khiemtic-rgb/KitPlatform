using System.Text.Json;
using Dapper;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerEngagementEventRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerEngagementEventRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertEventAsync(
        Guid tenantId,
        Guid accountId,
        Guid customerId,
        string eventType,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_engagement_events (tenant_id, account_id, customer_id, event_type, metadata)
            VALUES (@TenantId, @AccountId, @CustomerId, @EventType, @Metadata::jsonb)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = tenantId,
            AccountId = accountId,
            CustomerId = customerId,
            EventType = eventType,
            Metadata = metadataJson,
        });
    }

    public async Task<bool> HasAppOpenTodayAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM customer_engagement_events
                WHERE tenant_id = @TenantId
                  AND account_id = @AccountId
                  AND event_type = 'app_open'
                  AND event_at >= date_trunc('day', NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')
                       AT TIME ZONE 'Asia/Ho_Chi_Minh'
            )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { TenantId = tenantId, AccountId = accountId });
    }

    public async Task<(Guid CustomerId, Guid TenantId)?> FindAccountContextAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT customer_id AS CustomerId, tenant_id AS TenantId
            FROM customer_accounts
            WHERE id = @AccountId AND status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<AccountContextRow>(sql, new { AccountId = accountId });
        return row is null ? null : (row.CustomerId, row.TenantId);
    }

    private sealed class AccountContextRow
    {
        public Guid CustomerId { get; init; }
        public Guid TenantId { get; init; }
    }
}

internal sealed class CustomerEngagementEventService : ICustomerEngagementEventService
{
    private readonly CustomerEngagementEventRepository _repo;

    public CustomerEngagementEventService(CustomerEngagementEventRepository repo) => _repo = repo;

    public async Task RecordEventAsync(
        Guid tenantId,
        Guid accountId,
        Guid customerId,
        string eventType,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var json = metadata is null || metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(metadata);
        await _repo.InsertEventAsync(tenantId, accountId, customerId, eventType, json, cancellationToken);
    }

    public async Task TryRecordDailyAppOpenAsync(
        Guid tenantId,
        Guid accountId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (await _repo.HasAppOpenTodayAsync(tenantId, accountId, cancellationToken))
            return;

        await RecordEventAsync(
            tenantId,
            accountId,
            customerId,
            CustomerEngagementEventTypes.AppOpen,
            cancellationToken: cancellationToken);
    }
}
