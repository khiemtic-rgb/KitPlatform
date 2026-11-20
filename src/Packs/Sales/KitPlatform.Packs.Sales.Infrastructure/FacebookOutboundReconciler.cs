using System.Data.Common;
using System.Text.Json;
using Dapper;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Turns a dispatch with no recorded result into outbound_unknown plus a human task.
/// It never sends and never retries.
/// </summary>
internal static class FacebookOutboundReconciler
{
    public const string TaskAction = "check_outbound";
    public const string TaskTitle = "Kiểm hộp thư Page: tin Facebook không rõ đã gửi chưa";

    public static string UnknownKey(Guid interactionId, Guid approvalEventId) =>
        $"outbound_unknown:{interactionId:N}:{approvalEventId:N}";

    public static Task InsertTaskAsync(
        DbConnection conn,
        DbTransaction tx,
        Guid tenantId,
        Guid leadId,
        string title,
        Guid? actorUserId) =>
        conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.task (tenant_id, lead_id, action_code, title, created_by_user_id)
                VALUES (@TenantId, @LeadId, @Action, @Title, @Actor)
                """,
                new { TenantId = tenantId, LeadId = leadId, Action = TaskAction, Title = title, Actor = actorUserId },
                tx));

    public static async Task<int> ReconcileAsync(
        DbConnection conn,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var stale = (await conn.QueryAsync<StaleRow>(
            new CommandDefinition(
                """
                SELECT d.lead_id AS LeadId,
                       d.payload->>'interactionId' AS InteractionId,
                       d.payload->>'approvalEventId' AS ApprovalEventId
                FROM pack_sales.lead_event d
                WHERE d.tenant_id = @TenantId
                  AND d.event_type = 'outbound_dispatching'
                  AND d.occurred_at < NOW() - make_interval(secs => @StaleSeconds)
                  AND NOT EXISTS (
                      SELECT 1 FROM pack_sales.lead_event o
                      WHERE o.tenant_id = d.tenant_id
                        AND o.lead_id = d.lead_id
                        AND o.event_type = ANY(@Outcomes)
                        AND o.payload->>'approvalEventId' = d.payload->>'approvalEventId')
                ORDER BY d.occurred_at
                LIMIT 50
                """,
                new
                {
                    TenantId = tenantId,
                    StaleSeconds = FacebookOutboundRules.DispatchStaleAfter.TotalSeconds,
                    Outcomes = FacebookOutboundRules.DispatchOutcomeEvents.ToArray(),
                },
                cancellationToken: cancellationToken))).ToList();

        var marked = 0;
        foreach (var row in stale)
        {
            if (!Guid.TryParse(row.InteractionId, out var interactionId)
                || !Guid.TryParse(row.ApprovalEventId, out var approvalEventId))
                continue;

            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            var written = await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, tenantId, row.LeadId, "outbound_unknown", null, null,
                JsonSerializer.Serialize(new { interactionId, approvalEventId, reason = "stale_dispatch" }),
                null, null, UnknownKey(interactionId, approvalEventId), cancellationToken);
            if (written > 0)
            {
                await InsertTaskAsync(conn, tx, tenantId, row.LeadId, TaskTitle, null);
                marked++;
            }

            await tx.CommitAsync(cancellationToken);
        }

        return marked;
    }

    private sealed class StaleRow
    {
        public Guid LeadId { get; init; }
        public string? InteractionId { get; init; }
        public string? ApprovalEventId { get; init; }
    }
}
