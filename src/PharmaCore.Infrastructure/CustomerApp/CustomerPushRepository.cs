using System.Text.Json;
using Dapper;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerPushRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerPushRepository(IDbConnectionFactory db) => _db = db;

    public async Task<string> GetDeviceTokensJsonAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT device_tokens::text
            FROM customer_accounts
            WHERE id = @AccountId AND tenant_id = @TenantId AND status = 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<string?>(sql, new { AccountId = accountId, TenantId = tenantId })
            ?? "[]";
    }

    public async Task SaveDeviceTokensJsonAsync(
        Guid tenantId,
        Guid accountId,
        string json,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_accounts
            SET device_tokens = @Json::jsonb, updated_at = NOW()
            WHERE id = @AccountId AND tenant_id = @TenantId AND status = 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new { AccountId = accountId, TenantId = tenantId, Json = json });
        if (rows == 0)
            throw new InvalidOperationException("Tài khoản khách không tồn tại.");
    }

    public async Task<Guid?> GetCustomerIdForAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT customer_id
            FROM customer_accounts
            WHERE id = @AccountId AND tenant_id = @TenantId AND status = 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid?>(sql, new { AccountId = accountId, TenantId = tenantId });
    }

    public async Task<IReadOnlyList<DueReminderPushRow>> ListDueRemindersAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                mr.id AS ReminderId,
                mr.tenant_id AS TenantId,
                mr.customer_id AS CustomerId,
                mr.product_id AS ProductId,
                mr.family_member_id AS FamilyMemberId,
                fm.full_name AS FamilyMemberName,
                COALESCE(fm.notify_caregiver, FALSE) AS NotifyCaregiver,
                p.product_name AS ProductName,
                mr.dosage_note AS DosageNote,
                mr.remind_time AS RemindTime,
                mr.days_of_week AS DaysOfWeek,
                mr.next_remind_at AS NextRemindAt,
                ca.id AS AccountId,
                ca.device_tokens::text AS DeviceTokensJson
            FROM medication_reminders mr
            INNER JOIN products p ON p.id = mr.product_id AND p.tenant_id = mr.tenant_id
            INNER JOIN customer_accounts ca
                ON ca.customer_id = mr.customer_id
               AND ca.tenant_id = mr.tenant_id
               AND ca.status = 1
            LEFT JOIN family_members fm
                ON fm.id = mr.family_member_id
               AND fm.tenant_id = mr.tenant_id
            WHERE mr.is_active = TRUE
              AND mr.next_remind_at IS NOT NULL
              AND mr.next_remind_at <= NOW()
            ORDER BY mr.next_remind_at
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<DueReminderPushRow>(sql, new { Limit = limit })).ToList();
    }

    public async Task UpdateReminderNextAtAsync(
        Guid tenantId,
        Guid customerId,
        Guid reminderId,
        DateTimeOffset? nextRemindAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE medication_reminders
            SET next_remind_at = @NextRemindAt, updated_at = NOW()
            WHERE id = @ReminderId
              AND tenant_id = @TenantId
              AND customer_id = @CustomerId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            ReminderId = reminderId,
            TenantId = tenantId,
            CustomerId = customerId,
            NextRemindAt = nextRemindAt?.UtcDateTime,
        });
    }

    public async Task InsertNotificationAsync(
        Guid tenantId,
        Guid customerId,
        string title,
        string body,
        object payload,
        CancellationToken cancellationToken) =>
        InsertNotificationAsync(tenantId, customerId, title, body, payload, "system", null, cancellationToken);

    public async Task InsertNotificationAsync(
        Guid tenantId,
        Guid customerId,
        string title,
        string body,
        object payload,
        string category,
        string? href,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_notifications (
                tenant_id, customer_id, channel, category, title, body, href, payload, sent_at
            )
            VALUES (
                @TenantId, @CustomerId, 1, @Category, @Title, @Body, @Href, @Payload::jsonb, NOW()
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            Category = category,
            Title = title,
            Body = body,
            Href = href,
            Payload = JsonSerializer.Serialize(payload),
        });
    }

    public async Task<CustomerPushTargetRow?> GetPushTargetByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS AccountId, device_tokens::text AS DeviceTokensJson
            FROM customer_accounts
            WHERE tenant_id = @TenantId
              AND customer_id = @CustomerId
              AND status = 1
            ORDER BY updated_at DESC
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerPushTargetRow>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId });
    }
}

/// <summary>Dapper map theo property — tránh lỗi constructor record với PG array/time.</summary>
internal sealed class DueReminderPushRow
{
    public Guid ReminderId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? FamilyMemberId { get; set; }
    public string? FamilyMemberName { get; set; }
    public bool NotifyCaregiver { get; set; }
    public string ProductName { get; set; } = "";
    public string? DosageNote { get; set; }
    public TimeOnly RemindTime { get; set; }
    public short[] DaysOfWeek { get; set; } = [];
    public DateTime NextRemindAt { get; set; }
    public Guid AccountId { get; set; }
    public string DeviceTokensJson { get; set; } = "[]";
}

internal sealed record StoredPushSubscription(
    string Endpoint,
    string P256dh,
    string Auth,
    DateTimeOffset? RegisteredAt);

internal sealed record CustomerPushTargetRow(Guid AccountId, string DeviceTokensJson);
