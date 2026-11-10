using System.Text.Json;
using System.Data.Common;
using Dapper;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal static class KitSalesConsentStore
{
    public static async Task<bool> MessengerOptedOutAsync(
        DbConnection conn,
        DbTransaction? tx,
        Guid tenantId,
        string psid,
        CancellationToken cancellationToken)
    {
        var hash = SalesSubjectHash.ForMessenger(psid);
        var latest = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT signal_type
                FROM pack_sales.signal
                WHERE tenant_id = @TenantId
                  AND kind = 'consent'
                  AND evidence_json->>'channel' = 'messenger'
                  AND evidence_json->>'subject_hash' = @Hash
                ORDER BY detected_at DESC
                LIMIT 1
                """,
                new { TenantId = tenantId, Hash = hash },
                tx,
                cancellationToken: cancellationToken));
        return latest == "consent_withdrawn";
    }

    public static async Task WithdrawMessengerAsync(
        DbConnection conn,
        DbTransaction tx,
        Guid tenantId,
        Guid businessId,
        string psid,
        string evidenceRef,
        CancellationToken cancellationToken)
    {
        if (await MessengerOptedOutAsync(conn, tx, tenantId, psid, cancellationToken))
            return;
        var evidence = JsonSerializer.Serialize(new
        {
            channel = "messenger",
            subject_hash = SalesSubjectHash.ForMessenger(psid),
            basis = "customer_message",
            purpose = "marketing",
            captured_at = DateTimeOffset.UtcNow,
            captured_by = "webhook",
            evidence_ref = evidenceRef,
            expires_at = (DateTimeOffset?)null,
        });
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.signal (
                    tenant_id, business_id, signal_type, description, source, kind, evidence_json)
                VALUES (
                    @TenantId, @BusinessId, 'consent_withdrawn', 'Khách từ chối nhận tin',
                    'facebook_webhook', 'consent', @Evidence::jsonb)
                """,
                new { TenantId = tenantId, BusinessId = businessId, Evidence = evidence },
                tx,
                cancellationToken: cancellationToken));
    }

    public static async Task<string?> MessengerPsidAsync(
        DbConnection conn,
        DbTransaction? tx,
        Guid tenantId,
        Guid leadId,
        CancellationToken cancellationToken) =>
        await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT i.username
                FROM pack_sales.lead l
                INNER JOIN pack_sales.business_identity i
                    ON i.business_id = l.business_id AND i.tenant_id = l.tenant_id
                WHERE l.id = @LeadId AND l.tenant_id = @TenantId
                  AND i.identity_type = 'facebook' AND i.platform = 'messenger'
                ORDER BY i.created_at DESC
                LIMIT 1
                """,
                new { LeadId = leadId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
}
