using System.Data.Common;
using Dapper;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal static class KitSalesLeadEventWriter
{
    /// <summary>Returns 0 when the idempotency key already exists.</summary>
    public static Task<int> WriteAsync(
        DbConnection conn,
        DbTransaction tx,
        Guid tenantId,
        Guid leadId,
        string eventType,
        string? fromStatus,
        string? toStatus,
        string payloadJson,
        Guid? actorUserId,
        Guid? agentRunId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.lead_event (
                    tenant_id, lead_id, event_type, from_status, to_status, payload,
                    actor_user_id, agent_run_id, idempotency_key)
                VALUES (
                    @TenantId, @LeadId, @EventType, @FromStatus, @ToStatus, @Payload::jsonb,
                    @Actor, @RunId, @Key)
                ON CONFLICT (tenant_id, idempotency_key) DO NOTHING
                """,
                new
                {
                    TenantId = tenantId,
                    LeadId = leadId,
                    EventType = eventType,
                    FromStatus = fromStatus,
                    ToStatus = toStatus,
                    Payload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                    Actor = actorUserId,
                    RunId = agentRunId,
                    Key = idempotencyKey,
                },
                tx,
                cancellationToken: cancellationToken));
}
