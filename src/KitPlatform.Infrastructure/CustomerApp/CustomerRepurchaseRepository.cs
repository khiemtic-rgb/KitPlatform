using Dapper;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerRepurchaseRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerRepurchaseRepository(IDbConnectionFactory db) => _db = db;

    private const string SuggestionSelect = """
        SELECT
            rs.id AS Id,
            rs.sales_order_id AS SalesOrderId,
            rs.sales_order_item_id AS SalesOrderItemId,
            so.order_number AS OrderNumber,
            rs.order_label AS OrderLabel,
            rs.status AS Status,
            so.order_date AS OrderDate,
            so.reminder_days_supply AS ReminderDaysSupply,
            rs.suggested_for_date AS SuggestedForDate,
            rs.snoozed_until AS SnoozedUntil,
            rs.drink_reminders_created_at AS DrinkRemindersCreatedAt,
            rs.converted_at AS ConvertedAt,
            rs.converted_reservation_id AS ConvertedReservationId,
            rs.converted_sales_order_id AS ConvertedSalesOrderId,
            rs.created_at AS CreatedAt,
            rs.updated_at AS UpdatedAt
        FROM repurchase_suggestions rs
        INNER JOIN sales_orders so ON so.id = rs.sales_order_id
        """;

    public async Task<IReadOnlyList<CustomerRepurchaseSuggestionRow>> ListAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var sql = SuggestionSelect + """
            WHERE rs.tenant_id = @TenantId
              AND rs.customer_id = @CustomerId
              AND rs.customer_account_id = @AccountId
            ORDER BY rs.created_at DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerRepurchaseSuggestionRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            AccountId = accountId,
        })).ToList();
    }

    public async Task<CustomerRepurchaseSuggestionRow?> GetAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        var sql = SuggestionSelect + """
            WHERE rs.id = @SuggestionId
              AND rs.tenant_id = @TenantId
              AND rs.customer_id = @CustomerId
              AND rs.customer_account_id = @AccountId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerRepurchaseSuggestionRow>(sql, new
        {
            SuggestionId = suggestionId,
            TenantId = tenantId,
            CustomerId = customerId,
            AccountId = accountId,
        });
    }

    public async Task<IReadOnlyList<RepurchaseOrderLineRow>> ListOrderLinesAsync(
        Guid tenantId,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                soi.product_id AS ProductId,
                soi.quantity AS Quantity
            FROM sales_order_items soi
            INNER JOIN sales_orders so ON so.id = soi.sales_order_id
            WHERE soi.sales_order_id = @SalesOrderId
              AND so.tenant_id = @TenantId
            ORDER BY soi.id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<RepurchaseOrderLineRow>(sql, new
        {
            SalesOrderId = salesOrderId,
            TenantId = tenantId,
        })).ToList();
    }

    public async Task AcceptAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        Guid? familyMemberId,
        TimeOnly remindTime,
        DateTimeOffset nextRemindAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        if (familyMemberId is Guid memberId)
        {
            var belongs = await conn.ExecuteScalarAsync<bool>(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM family_members fm
                    INNER JOIN customer_accounts ca ON ca.id = fm.account_id
                    WHERE fm.id = @FamilyMemberId
                      AND fm.tenant_id = @TenantId
                      AND fm.status = 1
                      AND ca.customer_id = @CustomerId
                )
                """,
                new { FamilyMemberId = memberId, TenantId = tenantId, CustomerId = customerId },
                tx);
            if (!belongs)
                throw new InvalidOperationException("Người thân không thuộc tài khoản này.");
        }

        var suggestion = await conn.QuerySingleOrDefaultAsync<RepurchaseAcceptRow>(
            """
            SELECT
                id AS Id,
                sales_order_id AS SalesOrderId,
                drink_reminders_created_at AS DrinkRemindersCreatedAt
            FROM repurchase_suggestions
            WHERE id = @SuggestionId
              AND tenant_id = @TenantId
              AND customer_id = @CustomerId
              AND customer_account_id = @AccountId
            FOR UPDATE
            """,
            new
            {
                SuggestionId = suggestionId,
                TenantId = tenantId,
                CustomerId = customerId,
                AccountId = accountId,
            },
            tx);

        if (suggestion is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        if (!suggestion.DrinkRemindersCreatedAt.HasValue)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO medication_reminders (
                    tenant_id,
                    customer_id,
                    family_member_id,
                    product_id,
                    dosage_note,
                    remind_time,
                    days_of_week,
                    next_remind_at,
                    is_active
                )
                SELECT
                    @TenantId,
                    @CustomerId,
                    @FamilyMemberId,
                    soi.product_id,
                    CONCAT('Auto from order #', so.order_number),
                    @RemindTime::time,
                    ARRAY[1,2,3,4,5,6,7]::SMALLINT[],
                    @NextRemindAt,
                    TRUE
                FROM sales_order_items soi
                INNER JOIN sales_orders so ON so.id = soi.sales_order_id
                WHERE soi.sales_order_id = @SalesOrderId
                ORDER BY soi.id
                """,
                new
                {
                    TenantId = tenantId,
                    CustomerId = customerId,
                    FamilyMemberId = familyMemberId,
                    SalesOrderId = suggestion.SalesOrderId,
                    RemindTime = remindTime.ToString("HH:mm:ss"),
                    NextRemindAt = nextRemindAt.UtcDateTime,
                },
                tx);

            await conn.ExecuteAsync(
                """
                UPDATE repurchase_suggestions
                SET drink_reminders_created_at = NOW(),
                    family_member_id = @FamilyMemberId,
                    updated_at = NOW()
                WHERE id = @SuggestionId
                """,
                new { SuggestionId = suggestionId, FamilyMemberId = familyMemberId },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<bool> MarkConvertedAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE repurchase_suggestions
            SET status = 'converted',
                converted_at = NOW(),
                converted_reservation_id = @ReservationId,
                snoozed_until = NULL,
                updated_at = NOW()
            WHERE id = @SuggestionId
              AND tenant_id = @TenantId
              AND customer_id = @CustomerId
              AND customer_account_id = @AccountId
              AND status IN ('pending', 'snoozed')
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            SuggestionId = suggestionId,
            TenantId = tenantId,
            CustomerId = customerId,
            AccountId = accountId,
            ReservationId = reservationId,
        });
        return rows > 0;
    }

    public async Task<bool> UpdateStatusAsync(
        Guid tenantId,
        Guid customerId,
        Guid accountId,
        Guid suggestionId,
        string status,
        DateTimeOffset? snoozedUntil,
        bool setDismissedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE repurchase_suggestions
            SET status = @Status,
                snoozed_until = @SnoozedUntil,
                dismissed_at = CASE WHEN @SetDismissedAt = TRUE THEN NOW() ELSE dismissed_at END,
                updated_at = NOW()
            WHERE id = @SuggestionId
              AND tenant_id = @TenantId
              AND customer_id = @CustomerId
              AND customer_account_id = @AccountId
              AND status IN ('pending', 'snoozed')
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            SuggestionId = suggestionId,
            TenantId = tenantId,
            CustomerId = customerId,
            AccountId = accountId,
            Status = status,
            SnoozedUntil = snoozedUntil?.UtcDateTime,
            SetDismissedAt = setDismissedAt,
        });
        return rows > 0;
    }
}

internal sealed class CustomerRepurchaseSuggestionRow
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid? SalesOrderItemId { get; set; }
    public string OrderNumber { get; set; } = "";
    public string OrderLabel { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public int? ReminderDaysSupply { get; set; }
    public DateOnly? SuggestedForDate { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public DateTime? DrinkRemindersCreatedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public Guid? ConvertedReservationId { get; set; }
    public Guid? ConvertedSalesOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class RepurchaseAcceptRow
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public DateTime? DrinkRemindersCreatedAt { get; set; }
}

internal sealed class RepurchaseOrderLineRow
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
}
