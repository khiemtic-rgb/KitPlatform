using System.Data.Common;
using Dapper;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal static class KitSalesLeadGuard
{
    internal const string ArchivedMessage = "Lead đã lưu trữ, không nhận thao tác mới.";

    /// <summary>
    /// Throws when the lead is missing or archived. Inside a transaction the row is held
    /// FOR NO KEY UPDATE: an archive (FOR UPDATE) waits for this write, and two writers on one
    /// lead queue instead of deadlocking when both later update the lead row.
    /// </summary>
    public static async Task<string> EnsureWorkableAsync(
        DbConnection conn,
        DbTransaction? tx,
        Guid tenantId,
        Guid leadId,
        CancellationToken cancellationToken)
    {
        var status = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                tx is null
                    ? "SELECT lead_status FROM pack_sales.lead WHERE id = @LeadId AND tenant_id = @TenantId"
                    : "SELECT lead_status FROM pack_sales.lead WHERE id = @LeadId AND tenant_id = @TenantId FOR NO KEY UPDATE",
                new { LeadId = leadId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
        if (status is null)
            throw new InvalidOperationException("Lead không tồn tại.");
        if (LeadRetention.DenyWork(status) is not null)
            throw new InvalidOperationException(ArchivedMessage);
        return status;
    }
}
