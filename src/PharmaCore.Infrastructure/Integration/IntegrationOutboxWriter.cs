using System.Data;
using System.Text.Json;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Integration;

namespace PharmaCore.Infrastructure.Integration;

internal sealed class IntegrationOutboxWriter : IIntegrationOutboxWriter
{
    private readonly ITenantContext _tenant;

    public IntegrationOutboxWriter(ITenantContext tenant) => _tenant = tenant;

    public async Task WriteAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        object data,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var eventId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            eventType,
            eventVersion = 1,
            tenantId = _tenant.TenantId,
            occurredAt = DateTime.UtcNow,
            actorUserId,
            data,
        };
        var payloadJson = JsonSerializer.Serialize(envelope);

        const string sql = """
            INSERT INTO integration_outbox (
                tenant_id, event_type, event_version, aggregate_type, aggregate_id, payload
            )
            VALUES (
                @TenantId, @EventType, @EventVersion, @AggregateType, @AggregateId, @Payload::jsonb
            )
            """;
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            EventType = eventType,
            EventVersion = (short)1,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Payload = payloadJson,
        }, tx);
    }
}
