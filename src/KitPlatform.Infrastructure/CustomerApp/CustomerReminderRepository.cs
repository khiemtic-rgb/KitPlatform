using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerReminderRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerReminderRepository(IDbConnectionFactory db) => _db = db;

    public async Task<bool> ProductExistsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM products
                WHERE id = @ProductId AND tenant_id = @TenantId AND deleted_at IS NULL)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { ProductId = productId, TenantId = tenantId });
    }

    public async Task<bool> FamilyMemberExistsAsync(
        Guid tenantId,
        Guid accountId,
        Guid familyMemberId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM family_members
                WHERE id = @FamilyMemberId
                  AND tenant_id = @TenantId
                  AND account_id = @AccountId
                  AND status = 1)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new
        {
            FamilyMemberId = familyMemberId,
            TenantId = tenantId,
            AccountId = accountId,
        });
    }

    public async Task<bool> FamilyMemberBelongsToCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid familyMemberId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1
                FROM family_members fm
                INNER JOIN customer_accounts ca ON ca.id = fm.account_id
                WHERE fm.id = @FamilyMemberId
                  AND fm.tenant_id = @TenantId
                  AND ca.customer_id = @CustomerId
                  AND fm.status = 1)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new
        {
            FamilyMemberId = familyMemberId,
            TenantId = tenantId,
            CustomerId = customerId,
        });
    }

    public async Task<IReadOnlyList<MedicationReminderRow>> ListAsync(
        Guid tenantId,
        Guid customerId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                mr.id AS Id,
                mr.product_id AS ProductId,
                mr.family_member_id AS FamilyMemberId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                mr.dosage_note AS DosageNote,
                mr.remind_time AS RemindTime,
                mr.days_of_week AS DaysOfWeek,
                mr.next_remind_at AS NextRemindAt,
                mr.is_active AS IsActive,
                mr.created_at AS CreatedAt,
                mr.updated_at AS UpdatedAt
            FROM medication_reminders mr
            INNER JOIN products p ON p.id = mr.product_id AND p.tenant_id = @TenantId
            WHERE mr.tenant_id = @TenantId
              AND mr.customer_id = @CustomerId
              AND (@IncludeInactive = TRUE OR mr.is_active = TRUE)
            ORDER BY mr.created_at ASC, mr.id ASC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MedicationReminderRow>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId, IncludeInactive = includeInactive });
        return rows.AsList();
    }

    public async Task<MedicationReminderRow?> GetByIdAsync(
        Guid tenantId,
        Guid customerId,
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                mr.id AS Id,
                mr.product_id AS ProductId,
                mr.family_member_id AS FamilyMemberId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                mr.dosage_note AS DosageNote,
                mr.remind_time AS RemindTime,
                mr.days_of_week AS DaysOfWeek,
                mr.next_remind_at AS NextRemindAt,
                mr.is_active AS IsActive,
                mr.created_at AS CreatedAt,
                mr.updated_at AS UpdatedAt
            FROM medication_reminders mr
            INNER JOIN products p ON p.id = mr.product_id AND p.tenant_id = @TenantId
            WHERE mr.id = @ReminderId
              AND mr.tenant_id = @TenantId
              AND mr.customer_id = @CustomerId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MedicationReminderRow>(
            sql,
            new { ReminderId = reminderId, TenantId = tenantId, CustomerId = customerId });
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        Guid customerId,
        Guid productId,
        Guid? familyMemberId,
        string? dosageNote,
        TimeSpan remindTime,
        int[] daysOfWeek,
        DateTimeOffset? nextRemindAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO medication_reminders (
                tenant_id,
                customer_id,
                product_id,
                family_member_id,
                dosage_note,
                remind_time,
                days_of_week,
                next_remind_at,
                is_active)
            VALUES (
                @TenantId,
                @CustomerId,
                @ProductId,
                @FamilyMemberId,
                @DosageNote,
                @RemindTime,
                @DaysOfWeek,
                @NextRemindAt,
                TRUE)
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            ProductId = productId,
            FamilyMemberId = familyMemberId,
            DosageNote = dosageNote,
            RemindTime = remindTime,
            DaysOfWeek = daysOfWeek,
            NextRemindAt = nextRemindAt,
        });
    }

    public async Task<bool> UpdateAsync(
        Guid tenantId,
        Guid customerId,
        Guid reminderId,
        Guid productId,
        Guid? familyMemberId,
        string? dosageNote,
        TimeSpan remindTime,
        int[] daysOfWeek,
        DateTimeOffset? nextRemindAt,
        bool isActive,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE medication_reminders
            SET product_id = @ProductId,
                family_member_id = @FamilyMemberId,
                dosage_note = @DosageNote,
                remind_time = @RemindTime,
                days_of_week = @DaysOfWeek,
                next_remind_at = @NextRemindAt,
                is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @ReminderId
              AND tenant_id = @TenantId
              AND customer_id = @CustomerId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(sql, new
        {
            ReminderId = reminderId,
            TenantId = tenantId,
            CustomerId = customerId,
            ProductId = productId,
            FamilyMemberId = familyMemberId,
            DosageNote = dosageNote,
            RemindTime = remindTime,
            DaysOfWeek = daysOfWeek,
            NextRemindAt = nextRemindAt,
            IsActive = isActive,
        });
        return affected > 0;
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId,
        Guid customerId,
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE medication_reminders
            SET is_active = FALSE,
                next_remind_at = NULL,
                updated_at = NOW()
            WHERE id = @ReminderId
              AND tenant_id = @TenantId
              AND customer_id = @CustomerId
              AND is_active = TRUE
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(
            sql,
            new { ReminderId = reminderId, TenantId = tenantId, CustomerId = customerId });
        return affected > 0;
    }

    public async Task<IReadOnlyList<MedicationReminderRow>> ListDueAsync(
        Guid tenantId,
        Guid customerId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                mr.id AS Id,
                mr.product_id AS ProductId,
                mr.family_member_id AS FamilyMemberId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                mr.dosage_note AS DosageNote,
                mr.remind_time AS RemindTime,
                mr.days_of_week AS DaysOfWeek,
                mr.next_remind_at AS NextRemindAt,
                mr.is_active AS IsActive,
                mr.created_at AS CreatedAt,
                mr.updated_at AS UpdatedAt
            FROM medication_reminders mr
            INNER JOIN products p ON p.id = mr.product_id AND p.tenant_id = @TenantId
            WHERE mr.tenant_id = @TenantId
              AND mr.customer_id = @CustomerId
              AND mr.is_active = TRUE
              AND mr.next_remind_at IS NOT NULL
              AND mr.next_remind_at <= @UtcNow
            ORDER BY mr.next_remind_at ASC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MedicationReminderRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            UtcNow = utcNow.UtcDateTime,
        });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<MedicationReminderRow>> ListFamilyDueAsync(
        Guid tenantId,
        Guid customerId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                mr.id AS Id,
                mr.product_id AS ProductId,
                mr.family_member_id AS FamilyMemberId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                mr.dosage_note AS DosageNote,
                mr.remind_time AS RemindTime,
                mr.days_of_week AS DaysOfWeek,
                mr.next_remind_at AS NextRemindAt,
                mr.is_active AS IsActive,
                mr.created_at AS CreatedAt,
                mr.updated_at AS UpdatedAt
            FROM medication_reminders mr
            INNER JOIN products p ON p.id = mr.product_id AND p.tenant_id = @TenantId
            INNER JOIN family_members fm
                ON fm.id = mr.family_member_id
               AND fm.tenant_id = @TenantId
               AND fm.notify_caregiver = TRUE
               AND fm.status = 1
            WHERE mr.tenant_id = @TenantId
              AND mr.customer_id = @CustomerId
              AND mr.is_active = TRUE
              AND mr.family_member_id IS NOT NULL
              AND mr.next_remind_at IS NOT NULL
              AND mr.next_remind_at <= @UtcNow
            ORDER BY mr.next_remind_at ASC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MedicationReminderRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            UtcNow = utcNow.UtcDateTime,
        });
        return rows.AsList();
    }

    public async Task InsertAdherenceEventAsync(
        Guid tenantId,
        Guid customerId,
        Guid? reminderId,
        Guid productId,
        Guid? familyMemberId,
        DateTimeOffset scheduledAt,
        string response,
        int? snoozeMinutes,
        CancellationToken cancellationToken,
        string? skipReason = null)
    {
        const string sql = """
            INSERT INTO medication_adherence_events (
                tenant_id, customer_id, medication_reminder_id, product_id,
                family_member_id, scheduled_at, response, snooze_minutes, skip_reason)
            VALUES (
                @TenantId, @CustomerId, @ReminderId, @ProductId,
                @FamilyMemberId, @ScheduledAt, @Response, @SnoozeMinutes, @SkipReason)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            ReminderId = reminderId,
            ProductId = productId,
            FamilyMemberId = familyMemberId,
            ScheduledAt = scheduledAt.UtcDateTime,
            Response = response,
            SnoozeMinutes = snoozeMinutes,
            SkipReason = skipReason,
        });
    }

    public async Task<AdherenceSummaryRow> GetAdherenceSummaryAsync(
        Guid tenantId,
        Guid customerId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var vnToday = DateOnly.FromDateTime(utcNow.ToOffset(TimeSpan.FromHours(7)).Date);

        const string sql = """
            SELECT
                (SELECT COUNT(*)::int
                 FROM medication_reminders
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                   AND is_active = TRUE AND next_remind_at IS NOT NULL
                   AND next_remind_at <= @UtcNow) AS DueCount,
                (SELECT COUNT(*)::int
                 FROM medication_adherence_events
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                   AND response = 'taken'
                   AND (scheduled_at AT TIME ZONE 'UTC' AT TIME ZONE 'Asia/Ho_Chi_Minh')::date = @VnToday) AS TakenToday,
                (SELECT COUNT(*)::int
                 FROM medication_adherence_events
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                   AND response = 'skipped'
                   AND (scheduled_at AT TIME ZONE 'UTC' AT TIME ZONE 'Asia/Ho_Chi_Minh')::date = @VnToday) AS SkippedToday,
                (SELECT COUNT(*)::int
                 FROM medication_reminders
                 WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                   AND is_active = TRUE) AS ScheduledToday
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<AdherenceSummaryRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            UtcNow = utcNow.UtcDateTime,
            VnToday = vnToday,
        });

        row.MissedStreakDays = await CountMissedStreakDaysAsync(conn, tenantId, customerId, utcNow);
        return row;
    }

    private static async Task<int> CountMissedStreakDaysAsync(
        System.Data.IDbConnection conn,
        Guid tenantId,
        Guid customerId,
        DateTimeOffset utcNow)
    {
        const string sql = """
            WITH days AS (
                SELECT generate_series(
                    (CURRENT_DATE AT TIME ZONE 'Asia/Ho_Chi_Minh')::date - 6,
                    (CURRENT_DATE AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
                    '1 day'::interval
                )::date AS d
            ),
            taken_days AS (
                SELECT DISTINCT (scheduled_at AT TIME ZONE 'UTC' AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS d
                FROM medication_adherence_events
                WHERE tenant_id = @TenantId
                  AND customer_id = @CustomerId
                  AND response = 'taken'
                  AND scheduled_at >= @UtcNow - INTERVAL '7 days'
            )
            SELECT COUNT(*)::int
            FROM (
                SELECT d.d
                FROM days d
                LEFT JOIN taken_days t ON t.d = d.d
                WHERE t.d IS NULL
                ORDER BY d.d DESC
            ) missed
            """;

        var streak = await conn.ExecuteScalarAsync<int>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            UtcNow = utcNow.UtcDateTime,
        });
        return streak;
    }
}

internal sealed class AdherenceSummaryRow
{
    public int DueCount { get; set; }
    public int TakenToday { get; set; }
    public int SkippedToday { get; set; }
    public int ScheduledToday { get; set; }
    public int MissedStreakDays { get; set; }
}

/// <summary>Dapper map theo property — tránh lỗi constructor record với PG array.</summary>
internal sealed class MedicationReminderRow
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? FamilyMemberId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? DosageNote { get; set; }
    public TimeOnly RemindTime { get; set; }
    public short[] DaysOfWeek { get; set; } = [];
    public DateTime? NextRemindAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

