using Dapper;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Success;

internal sealed class GrowthDeskRepository
{
    private const string VnTz = "Asia/Ho_Chi_Minh";

    private readonly IDbConnectionFactory _db;

    public GrowthDeskRepository(IDbConnectionFactory db) => _db = db;

    public async Task<(DateOnly BusinessDate, IReadOnlyList<OpportunityRow> Rows)> ListOpportunitiesTodayAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
            WITH today AS (
                SELECT (NOW() AT TIME ZONE '{VnTz}')::date AS d
            )
            SELECT
                rs.id AS SuggestionId,
                rs.customer_id AS CustomerId,
                COALESCE(c.full_name, '') AS CustomerName,
                c.phone AS CustomerPhone,
                rs.order_label AS OrderLabel,
                so.order_number AS OrderNumber,
                rs.suggested_for_date AS SuggestedForDate,
                so.order_date AS OrderDate,
                rs.status AS Status,
                CASE
                    WHEN rs.status = 'pending'
                         AND rs.suggested_for_date = (SELECT d FROM today)
                        THEN '{GrowthOpportunityBuckets.RefillDue}'
                    WHEN rs.status = 'pending'
                         AND rs.suggested_for_date IS NOT NULL
                         AND rs.suggested_for_date < (SELECT d FROM today)
                        THEN '{GrowthOpportunityBuckets.RefillOverdue}'
                    WHEN rs.status = 'snoozed'
                         AND (
                             rs.snoozed_until IS NULL
                             OR (rs.snoozed_until AT TIME ZONE '{VnTz}')::date <= (SELECT d FROM today)
                         )
                        THEN '{GrowthOpportunityBuckets.SnoozedExpiring}'
                    ELSE NULL
                END AS Bucket,
                CASE
                    WHEN rs.suggested_for_date IS NOT NULL
                         AND rs.suggested_for_date < (SELECT d FROM today)
                        THEN ((SELECT d FROM today) - rs.suggested_for_date)
                    ELSE NULL
                END AS DaysOverdue,
                (SELECT d FROM today) AS BusinessDate
            FROM repurchase_suggestions rs
            INNER JOIN sales_orders so ON so.id = rs.sales_order_id
            INNER JOIN customers c ON c.id = rs.customer_id
            WHERE rs.tenant_id = @TenantId
              AND (
                    (rs.status = 'pending'
                     AND rs.suggested_for_date IS NOT NULL
                     AND rs.suggested_for_date <= (SELECT d FROM today))
                 OR (rs.status = 'snoozed'
                     AND (
                         rs.snoozed_until IS NULL
                         OR (rs.snoozed_until AT TIME ZONE '{VnTz}')::date <= (SELECT d FROM today)
                     ))
              )
            ORDER BY
                CASE
                    WHEN rs.status = 'pending' AND rs.suggested_for_date < (SELECT d FROM today) THEN 0
                    WHEN rs.status = 'pending' THEN 1
                    ELSE 2
                END,
                rs.suggested_for_date NULLS LAST,
                c.full_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<OpportunityRow>(sql, new { TenantId = tenantId })).ToList();
        var businessDate = rows.FirstOrDefault()?.BusinessDate
            ?? await conn.ExecuteScalarAsync<DateOnly>(
                $"SELECT (NOW() AT TIME ZONE '{VnTz}')::date");
        return (businessDate, rows.Where(r => !string.IsNullOrEmpty(r.Bucket)).ToList());
    }

    public async Task<int> CountOpportunitiesTodayAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = $"""
            WITH today AS (
                SELECT (NOW() AT TIME ZONE '{VnTz}')::date AS d
            )
            SELECT COUNT(*)::int
            FROM repurchase_suggestions rs
            WHERE rs.tenant_id = @TenantId
              AND (
                    (rs.status = 'pending'
                     AND rs.suggested_for_date IS NOT NULL
                     AND rs.suggested_for_date <= (SELECT d FROM today))
                 OR (rs.status = 'snoozed'
                     AND (
                         rs.snoozed_until IS NULL
                         OR (rs.snoozed_until AT TIME ZONE '{VnTz}')::date <= (SELECT d FROM today)
                     ))
              )
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(sql, new { TenantId = tenantId });
    }

    public async Task<SuggestionForCareRow?> GetSuggestionForCareAsync(
        Guid tenantId,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                rs.id AS Id,
                rs.customer_id AS CustomerId,
                rs.sales_order_id AS SalesOrderId,
                rs.status AS Status,
                rs.order_label AS OrderLabel,
                so.order_number AS OrderNumber,
                so.warehouse_id AS WarehouseId
            FROM repurchase_suggestions rs
            INNER JOIN sales_orders so ON so.id = rs.sales_order_id
            WHERE rs.id = @SuggestionId
              AND rs.tenant_id = @TenantId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SuggestionForCareRow>(sql, new
        {
            SuggestionId = suggestionId,
            TenantId = tenantId,
        });
    }

    public async Task<IReadOnlyList<DraftLineSourceRow>> ListOrderLinesForDraftAsync(
        Guid tenantId,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                soi.product_id AS ProductId,
                soi.product_unit_id AS ProductUnitId,
                soi.quantity AS Quantity
            FROM sales_order_items soi
            INNER JOIN sales_orders so ON so.id = soi.sales_order_id
            WHERE soi.sales_order_id = @SalesOrderId
              AND so.tenant_id = @TenantId
              AND soi.quantity > 0
            ORDER BY soi.id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<DraftLineSourceRow>(sql, new
        {
            SalesOrderId = salesOrderId,
            TenantId = tenantId,
        })).ToList();
    }

    public async Task<OpenCareDraftRow?> FindOpenCareDraftAsync(
        Guid tenantId,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                gca.id AS CareActionId,
                gca.draft_order_id AS DraftOrderId,
                d.draft_number AS DraftNumber,
                gca.customer_id AS CustomerId
            FROM growth_care_actions gca
            INNER JOIN customer_draft_orders d ON d.id = gca.draft_order_id
            WHERE gca.tenant_id = @TenantId
              AND gca.repurchase_suggestion_id = @SuggestionId
              AND gca.action_type = 'create_draft'
              AND gca.draft_order_id IS NOT NULL
              AND d.status IN (@Draft, @Sent, @Confirmed)
            ORDER BY gca.created_at DESC
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OpenCareDraftRow>(sql, new
        {
            TenantId = tenantId,
            SuggestionId = suggestionId,
            Draft = CustomerDraftOrderStatuses.Draft,
            Sent = CustomerDraftOrderStatuses.Sent,
            Confirmed = CustomerDraftOrderStatuses.Confirmed,
        });
    }

    public async Task<Guid> InsertCareActionAsync(
        Guid tenantId,
        Guid suggestionId,
        Guid customerId,
        Guid? actorUserId,
        Guid draftOrderId,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO growth_care_actions (
                id, tenant_id, repurchase_suggestion_id, customer_id,
                actor_user_id, action_type, draft_order_id, notes
            ) VALUES (
                @Id, @TenantId, @SuggestionId, @CustomerId,
                @ActorUserId, 'create_draft', @DraftOrderId, @Notes
            )
            RETURNING id
            """;

        var id = Guid.NewGuid();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            Id = id,
            TenantId = tenantId,
            SuggestionId = suggestionId,
            CustomerId = customerId,
            ActorUserId = actorUserId,
            DraftOrderId = draftOrderId,
            Notes = notes,
        });
    }

    public async Task<WeeklyRefillRow> GetWeeklyRefillAsync(
        Guid tenantId,
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM repurchase_suggestions
                    WHERE tenant_id = @TenantId
                      AND suggested_for_date IS NOT NULL
                      AND suggested_for_date >= @WeekStart
                      AND suggested_for_date <= @WeekEnd
                ), 0) AS DueCount,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM repurchase_suggestions
                    WHERE tenant_id = @TenantId
                      AND notified_at IS NOT NULL
                      AND (notified_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= @WeekStart
                      AND (notified_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= @WeekEnd
                ), 0) AS NotifiedCount,
                COALESCE((
                    SELECT COUNT(*)::int
                    FROM repurchase_suggestions
                    WHERE tenant_id = @TenantId
                      AND converted_at IS NOT NULL
                      AND (converted_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= @WeekStart
                      AND (converted_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= @WeekEnd
                ), 0) AS ConvertedCount,
                COALESCE((
                    SELECT SUM(so.total_amount)
                    FROM repurchase_suggestions rs
                    INNER JOIN sales_orders so ON so.id = rs.converted_sales_order_id
                    WHERE rs.tenant_id = @TenantId
                      AND rs.converted_sales_order_id IS NOT NULL
                      AND rs.converted_at IS NOT NULL
                      AND (rs.converted_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date >= @WeekStart
                      AND (rs.converted_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date <= @WeekEnd
                ), 0) AS AttributedRevenue
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<WeeklyRefillRow>(sql, new
        {
            TenantId = tenantId,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
        });
    }

    public async Task<DateOnly> GetVnTodayAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<DateOnly>(
            $"SELECT (NOW() AT TIME ZONE '{VnTz}')::date");
    }

    internal sealed class OpportunityRow
    {
        public Guid SuggestionId { get; init; }
        public Guid CustomerId { get; init; }
        public string CustomerName { get; init; } = "";
        public string? CustomerPhone { get; init; }
        public string? OrderLabel { get; init; }
        public string OrderNumber { get; init; } = "";
        public DateOnly? SuggestedForDate { get; init; }
        public DateTime? OrderDate { get; init; }
        public string Status { get; init; } = "";
        public string? Bucket { get; init; }
        public int? DaysOverdue { get; init; }
        public DateOnly BusinessDate { get; init; }
    }

    internal sealed class SuggestionForCareRow
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public Guid SalesOrderId { get; init; }
        public string Status { get; init; } = "";
        public string? OrderLabel { get; init; }
        public string OrderNumber { get; init; } = "";
        public Guid? WarehouseId { get; init; }
    }

    internal sealed class DraftLineSourceRow
    {
        public Guid ProductId { get; init; }
        public Guid ProductUnitId { get; init; }
        public decimal Quantity { get; init; }
    }

    internal sealed class OpenCareDraftRow
    {
        public Guid CareActionId { get; init; }
        public Guid DraftOrderId { get; init; }
        public string DraftNumber { get; init; } = "";
        public Guid CustomerId { get; init; }
    }

    internal sealed class WeeklyRefillRow
    {
        public int DueCount { get; init; }
        public int NotifiedCount { get; init; }
        public int ConvertedCount { get; init; }
        public decimal AttributedRevenue { get; init; }
    }
}
