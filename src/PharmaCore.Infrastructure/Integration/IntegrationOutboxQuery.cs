using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Integration;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Integration;

internal sealed class IntegrationOutboxQuery : IIntegrationOutboxQuery
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public IntegrationOutboxQuery(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<IntegrationOutboxItemDto>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 200);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                id AS Id,
                event_type AS EventType,
                aggregate_type AS AggregateType,
                aggregate_id AS AggregateId,
                occurred_at AS OccurredAt,
                published_at AS PublishedAt,
                publish_attempts AS PublishAttempts,
                last_error AS LastError
            FROM integration_outbox
            WHERE tenant_id = @TenantId
            ORDER BY occurred_at DESC
            LIMIT @Limit
            """;

        var rows = await conn.QueryAsync<IntegrationOutboxItemDto>(
            sql,
            new { TenantId = _tenant.TenantId, Limit = take });
        return rows.ToList();
    }
}
