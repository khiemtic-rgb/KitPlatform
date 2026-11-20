using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Infrastructure.Success;

internal sealed class LossPreventionRepository
{
    public const decimal DefaultCashVarianceThreshold = 10_000m;
    public const string CycleCountReasonPrefix = "[cycle_count]";

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public LossPreventionRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<LossCashVarianceShiftDto>> ListTodayShiftsAsync(
        decimal threshold,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var (dayStart, dayEnd) = VietnamBusinessCalendar.TodayRangeUtc(DateTime.UtcNow);
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND s.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid ShiftId,
            string ShiftNumber,
            Guid WarehouseId,
            string WarehouseName,
            Guid BranchId,
            string BranchName,
            short Status,
            decimal OpeningCash,
            decimal? ClosingCash,
            decimal? ExpectedCash,
            decimal? CashVariance,
            DateTime OpenedAt,
            DateTime? ClosedAt,
            string? OpenedByName,
            string? ClosedByName)>(
            $"""
            SELECT
                s.id AS ShiftId,
                s.shift_number AS ShiftNumber,
                s.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                w.branch_id AS BranchId,
                b.branch_name AS BranchName,
                s.status AS Status,
                s.opening_cash AS OpeningCash,
                s.closing_cash AS ClosingCash,
                s.expected_cash AS ExpectedCash,
                s.cash_variance AS CashVariance,
                s.opened_at AS OpenedAt,
                s.closed_at AS ClosedAt,
                COALESCE(eo.full_name, uo.username) AS OpenedByName,
                COALESCE(ec.full_name, uc.username) AS ClosedByName
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id AND w.tenant_id = s.tenant_id
            INNER JOIN branches b ON b.id = w.branch_id AND b.tenant_id = s.tenant_id
            {ShiftActorJoins}
            WHERE s.tenant_id = @TenantId
              AND (
                    s.status = @Open
                 OR (s.status = @Closed AND s.closed_at >= @DayStart AND s.closed_at < @DayEnd)
              )
              {warehouseFilter}
            ORDER BY
                CASE WHEN s.cash_variance IS NULL THEN 1 ELSE 0 END,
                ABS(COALESCE(s.cash_variance, 0)) DESC,
                s.opened_at DESC
            """,
            new
            {
                TenantId,
                DayStart = dayStart,
                DayEnd = dayEnd,
                Open = SalesShiftStatuses.Open,
                Closed = SalesShiftStatuses.Closed,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        return rows.Select(r =>
        {
            var abs = Math.Abs(r.CashVariance ?? 0);
            var status = r.Status == SalesShiftStatuses.Open ? "open" : "closed";
            var isAlert = r.Status == SalesShiftStatuses.Closed
                          && r.CashVariance is not null
                          && abs > threshold;
            return new LossCashVarianceShiftDto(
                r.ShiftId,
                r.ShiftNumber,
                r.WarehouseId,
                r.WarehouseName,
                r.BranchId,
                r.BranchName,
                status,
                r.OpeningCash,
                r.ClosingCash,
                r.ExpectedCash,
                r.CashVariance,
                abs,
                isAlert,
                r.OpenedAt,
                r.ClosedAt,
                r.OpenedByName,
                r.ClosedByName);
        }).ToList();
    }

    /// <summary>Closed shifts in [from, to) by closed_at, with closer display name (for per-closer grouping).</summary>
    public async Task<IReadOnlyList<CashVarianceShiftSample>> ListClosedShiftVarianceSamplesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? branchId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var branchFilter = branchId is not null ? "AND w.branch_id = @BranchId" : string.Empty;
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND s.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid? CloserUserId, string? CloserName, decimal? CashVariance)>(
            $"""
            SELECT
                s.closed_by AS CloserUserId,
                COALESCE(ec.full_name, uc.username) AS CloserName,
                s.cash_variance AS CashVariance
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id AND w.tenant_id = s.tenant_id
            {ShiftActorJoins}
            WHERE s.tenant_id = @TenantId
              AND s.status = @Closed
              AND s.closed_at >= @FromUtc AND s.closed_at < @ToUtc
              AND s.cash_variance IS NOT NULL
              {branchFilter}
              {warehouseFilter}
            """,
            new
            {
                TenantId,
                Closed = SalesShiftStatuses.Closed,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });
        return rows
            .Select(r => new CashVarianceShiftSample(
                r.CloserUserId,
                r.CloserName ?? CashVarianceAccountability.UnknownCloserName,
                r.CashVariance))
            .ToList();
    }

    private const string ShiftActorJoins = """
        LEFT JOIN users uo ON uo.id = s.opened_by AND uo.tenant_id = s.tenant_id
        LEFT JOIN employees eo ON eo.id = uo.employee_id AND eo.tenant_id = s.tenant_id
        LEFT JOIN users uc ON uc.id = s.closed_by AND uc.tenant_id = s.tenant_id
        LEFT JOIN employees ec ON ec.id = uc.employee_id AND ec.tenant_id = s.tenant_id
        """;

    public async Task<IReadOnlyList<LossEmployeeCancelRowDto>> ListCancellationsByEmployeeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? branchId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var filters = BuildSalesScopeFilters(branchId, allowedWarehouseIds);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LossEmployeeCancelRowDto>(
            $"""
            SELECT
                o.employee_id AS EmployeeId,
                COALESCE(e.full_name, '(Không gắn NV)') AS EmployeeName,
                COUNT(*)::int AS CancelCount,
                COALESCE(SUM(o.total_amount), 0) AS CancelValue
            FROM sales_orders o
            LEFT JOIN employees e ON e.id = o.employee_id AND e.tenant_id = o.tenant_id
            INNER JOIN warehouses w ON w.id = o.warehouse_id AND w.tenant_id = o.tenant_id
            WHERE o.tenant_id = @TenantId
              AND o.status = @Cancelled
              AND o.updated_at >= @FromUtc AND o.updated_at < @ToUtc
              {filters}
            GROUP BY o.employee_id, e.full_name
            ORDER BY CancelValue DESC, CancelCount DESC
            """,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Cancelled = SalesOrderStatuses.Cancelled,
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LossEmployeeDiscountRowDto>> ListDiscountsByEmployeeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? branchId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var filters = BuildSalesScopeFilters(branchId, allowedWarehouseIds);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LossEmployeeDiscountRowDto>(
            $"""
            SELECT
                o.employee_id AS EmployeeId,
                COALESCE(e.full_name, '(Không gắn NV)') AS EmployeeName,
                COUNT(*)::int AS OrderCount,
                COALESCE(SUM(o.discount_amount), 0) AS OrderDiscountAmount,
                COALESCE(SUM(line.line_discount), 0) AS LineDiscountAmount,
                COALESCE(SUM(o.discount_amount), 0) + COALESCE(SUM(line.line_discount), 0) AS TotalPosDiscount
            FROM sales_orders o
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(i.discount_amount), 0) AS line_discount
                FROM sales_order_items i
                WHERE i.sales_order_id = o.id
            ) line ON TRUE
            LEFT JOIN employees e ON e.id = o.employee_id AND e.tenant_id = o.tenant_id
            INNER JOIN warehouses w ON w.id = o.warehouse_id AND w.tenant_id = o.tenant_id
            WHERE o.tenant_id = @TenantId
              AND o.status = @Completed
              AND o.order_date >= @FromUtc AND o.order_date < @ToUtc
              AND (COALESCE(o.discount_amount, 0) > 0 OR COALESCE(line.line_discount, 0) > 0)
              {filters}
            GROUP BY o.employee_id, e.full_name
            ORDER BY TotalPosDiscount DESC, OrderCount DESC
            """,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Completed = SalesOrderStatuses.Completed,
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<LossEmployeeAdjustmentRowDto>> ListAdjustmentsByEmployeeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? branchId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND a.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var branchFilter = branchId is not null ? "AND w.branch_id = @BranchId" : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LossEmployeeAdjustmentRowDto>(
            $"""
            SELECT
                u.employee_id AS EmployeeId,
                COALESCE(e.full_name, u.username, '(Không gắn NV)') AS EmployeeName,
                COUNT(DISTINCT a.id)::int AS AdjustmentCount,
                COALESCE(SUM(ABS(i.difference_quantity) * COALESCE(b.unit_cost, 0)), 0) AS AbsVarianceValue
            FROM inventory_adjustments a
            INNER JOIN inventory_adjustment_items i ON i.adjustment_id = a.id
            INNER JOIN inventory_batches b ON b.id = i.batch_id
            INNER JOIN users u ON u.id = a.approved_by
            LEFT JOIN employees e ON e.id = u.employee_id AND e.tenant_id = a.tenant_id
            INNER JOIN warehouses w ON w.id = a.warehouse_id AND w.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId
              AND a.status = @Approved
              AND a.approved_by IS NOT NULL
              AND a.approved_at >= @FromUtc AND a.approved_at < @ToUtc
              {branchFilter}
              {warehouseFilter}
            GROUP BY u.employee_id, COALESCE(e.full_name, u.username, '(Không gắn NV)')
            ORDER BY AbsVarianceValue DESC, AdjustmentCount DESC
            """,
            new
            {
                TenantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Approved = AdjustmentStatuses.Approved,
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });
        return rows.AsList();
    }

    public async Task<(IReadOnlyList<LossAuditFeedItemDto> Items, int Total)> ListAuditFeedAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? branchId,
        Guid? userId,
        string? eventType,
        Guid[]? allowedWarehouseIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        var eventFilter = eventType is not null ? "AND mapped.event_type = @EventType" : string.Empty;
        var userFilter = userId is not null ? "AND mapped.actor_user_id = @UserId" : string.Empty;
        var branchFilter = branchId is not null ? "AND mapped.branch_id = @BranchId" : string.Empty;
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND (mapped.warehouse_id IS NULL OR mapped.warehouse_id = ANY(@AllowedWarehouseIds))"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        const string mappedCte = """
            WITH mapped AS (
                SELECT
                    a.id,
                    a.occurred_at,
                    a.actor_user_id,
                    u.username AS actor_username,
                    COALESCE(a.entity_type, a.activity_type) AS entity_type,
                    a.entity_id,
                    a.activity_action,
                    a.metadata,
                    CASE
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'sales_order'
                             AND a.activity_action IN ('draft_create', 'complete')
                            THEN 'order_create'
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'sales_order'
                             AND a.activity_action = 'draft_update'
                            THEN 'order_edit'
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'sales_order'
                             AND a.activity_action = 'cancel'
                            THEN 'order_cancel'
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'sales_order'
                             AND a.activity_action = 'discount'
                            THEN 'discount'
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'sales_return'
                             AND a.activity_action = 'complete'
                            THEN 'return'
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'inventory_adjustment'
                             AND a.activity_action IN ('approve', 'create')
                             AND (
                                    adj.reason ILIKE '%nội bộ%'
                                 OR adj.reason ILIKE '%noi bo%'
                                 OR adj.reason ILIKE '%internal_issue%'
                                 OR adj.reason ILIKE '%internal issue%'
                             )
                            THEN 'internal_issue'
                        WHEN COALESCE(a.entity_type, a.activity_type) = 'inventory_adjustment'
                             AND a.activity_action IN ('approve', 'create')
                            THEN 'stock_adjust'
                        ELSE NULL
                    END AS event_type,
                    COALESCE(o.warehouse_id, ret_o.warehouse_id, adj.warehouse_id) AS warehouse_id,
                    COALESCE(ow.branch_id, ret_w.branch_id, adj_w.branch_id) AS branch_id,
                    COALESCE(ob.branch_name, ret_b.branch_name, adj_b.branch_name) AS branch_name,
                    COALESCE(
                        o.order_number,
                        sr.return_number,
                        adj.adjustment_number,
                        a.metadata->>'OrderNumber',
                        a.metadata->>'orderNumber',
                        a.metadata->>'ReturnNumber',
                        a.metadata->>'returnNumber',
                        a.metadata->>'AdjustmentNumber',
                        a.metadata->>'adjustmentNumber'
                    ) AS document_number
                FROM kit_audit.activity_log a
                LEFT JOIN users u ON u.id = a.actor_user_id
                LEFT JOIN sales_orders o
                    ON o.id = a.entity_id
                   AND o.tenant_id = a.tenant_id
                   AND COALESCE(a.entity_type, a.activity_type) = 'sales_order'
                LEFT JOIN warehouses ow ON ow.id = o.warehouse_id AND ow.tenant_id = o.tenant_id
                LEFT JOIN branches ob ON ob.id = ow.branch_id AND ob.tenant_id = o.tenant_id
                LEFT JOIN sales_returns sr
                    ON sr.id = a.entity_id
                   AND sr.tenant_id = a.tenant_id
                   AND COALESCE(a.entity_type, a.activity_type) = 'sales_return'
                LEFT JOIN sales_orders ret_o ON ret_o.id = sr.sales_order_id AND ret_o.tenant_id = sr.tenant_id
                LEFT JOIN warehouses ret_w ON ret_w.id = ret_o.warehouse_id AND ret_w.tenant_id = ret_o.tenant_id
                LEFT JOIN branches ret_b ON ret_b.id = ret_w.branch_id AND ret_b.tenant_id = ret_o.tenant_id
                LEFT JOIN inventory_adjustments adj
                    ON adj.id = a.entity_id
                   AND adj.tenant_id = a.tenant_id
                   AND COALESCE(a.entity_type, a.activity_type) = 'inventory_adjustment'
                LEFT JOIN warehouses adj_w ON adj_w.id = adj.warehouse_id AND adj_w.tenant_id = adj.tenant_id
                LEFT JOIN branches adj_b ON adj_b.id = adj_w.branch_id AND adj_b.tenant_id = adj.tenant_id
                WHERE a.tenant_id = @TenantId
                  AND a.occurred_at >= @FromUtc
                  AND a.occurred_at < @ToUtc
            )
            """;

        var sql = $"""
            {mappedCte}
            SELECT COUNT(*)::int
            FROM mapped
            WHERE mapped.event_type IS NOT NULL
              {eventFilter}
              {userFilter}
              {branchFilter}
              {warehouseFilter};

            {mappedCte}
            SELECT
                mapped.id AS Id,
                mapped.occurred_at AS OccurredAt,
                mapped.event_type AS EventType,
                mapped.actor_user_id AS ActorUserId,
                mapped.actor_username AS ActorUsername,
                mapped.entity_type AS EntityType,
                mapped.entity_id AS EntityId,
                mapped.document_number AS DocumentNumber,
                mapped.branch_id AS BranchId,
                mapped.branch_name AS BranchName,
                mapped.activity_action AS Action,
                mapped.metadata::text AS PayloadJson
            FROM mapped
            WHERE mapped.event_type IS NOT NULL
              {eventFilter}
              {userFilter}
              {branchFilter}
              {warehouseFilter}
            ORDER BY mapped.occurred_at DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            TenantId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            BranchId = branchId,
            UserId = userId,
            EventType = eventType,
            AllowedWarehouseIds = allowedWarehouseIds,
            PageSize = pageSize,
            Offset = offset,
        });

        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<AuditFeedRow>()).ToList();
        var items = rows.Select(MapAuditFeedItem).ToList();
        return (items, total);
    }

    public async Task<(string WarehouseName, Guid BranchId, string BranchName)?> GetWarehouseMetaAsync(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<WarehouseMetaRow>(
            """
            SELECT w.warehouse_name AS WarehouseName, w.branch_id AS BranchId, b.branch_name AS BranchName
            FROM warehouses w
            INNER JOIN branches b ON b.id = w.branch_id AND b.tenant_id = w.tenant_id
            WHERE w.tenant_id = @TenantId AND w.id = @WarehouseId AND w.deleted_at IS NULL
            """,
            new { TenantId, WarehouseId = warehouseId });
        return row is null ? null : (row.WarehouseName, row.BranchId, row.BranchName);
    }

    public async Task<IReadOnlyList<LossCycleCountSuggestionDto>> ListCycleCountSuggestionsAsync(
        Guid warehouseId,
        Guid[]? allowedWarehouseIds,
        int limit,
        CancellationToken cancellationToken)
    {
        if (allowedWarehouseIds is { Length: > 0 } && !allowedWarehouseIds.Contains(warehouseId))
            return Array.Empty<LossCycleCountSuggestionDto>();

        var (dayStart, _) = VietnamBusinessCalendar.TodayRangeUtc(DateTime.UtcNow);
        var weekStart = dayStart.AddDays(-7);
        var takeHot = Math.Max(5, (limit + 1) / 2);
        var takeMin = Math.Max(3, limit / 3);
        var takeRandom = limit;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var hot = (await conn.QueryAsync<(Guid ProductId, string Sku, string ProductName, decimal OnHand, decimal? MinStock)>(
            """
            SELECT
                p.id AS ProductId,
                p.product_code AS Sku,
                p.product_name AS ProductName,
                COALESCE(stock.qty, 0) AS OnHand,
                p.min_stock_qty AS MinStock
            FROM products p
            INNER JOIN (
                SELECT i.product_id, SUM(i.quantity) AS sold_qty
                FROM sales_order_items i
                INNER JOIN sales_orders o ON o.id = i.sales_order_id
                WHERE o.tenant_id = @TenantId
                  AND o.warehouse_id = @WarehouseId
                  AND o.status = @Completed
                  AND o.order_date >= @WeekStart AND o.order_date < @DayEnd
                GROUP BY i.product_id
            ) s ON s.product_id = p.id
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(b.quantity_available), 0) AS qty
                FROM inventory_batches b
                WHERE b.tenant_id = p.tenant_id AND b.product_id = p.id AND b.warehouse_id = @WarehouseId
            ) stock ON TRUE
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL AND p.status = 1
            ORDER BY s.sold_qty DESC
            LIMIT @TakeHot
            """,
            new
            {
                TenantId,
                WarehouseId = warehouseId,
                Completed = SalesOrderStatuses.Completed,
                WeekStart = weekStart,
                DayEnd = DateTime.UtcNow,
                TakeHot = takeHot,
            })).ToList();

        var min = (await conn.QueryAsync<(Guid ProductId, string Sku, string ProductName, decimal OnHand, decimal? MinStock)>(
            """
            SELECT
                p.id AS ProductId,
                p.product_code AS Sku,
                p.product_name AS ProductName,
                COALESCE(SUM(b.quantity_available), 0) AS OnHand,
                p.min_stock_qty AS MinStock
            FROM products p
            LEFT JOIN inventory_batches b
              ON b.product_id = p.id AND b.warehouse_id = @WarehouseId AND b.tenant_id = p.tenant_id
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL AND p.status = 1
              AND COALESCE(p.min_stock_qty, 0) > 0
            GROUP BY p.id, p.product_code, p.product_name, p.min_stock_qty
            HAVING COALESCE(SUM(b.quantity_available), 0) <= COALESCE(p.min_stock_qty, 0)
            ORDER BY COALESCE(SUM(b.quantity_available), 0) ASC
            LIMIT @TakeMin
            """,
            new { TenantId, WarehouseId = warehouseId, TakeMin = takeMin })).ToList();

        var random = (await conn.QueryAsync<(Guid ProductId, string Sku, string ProductName, decimal OnHand, decimal? MinStock)>(
            """
            SELECT
                p.id AS ProductId,
                p.product_code AS Sku,
                p.product_name AS ProductName,
                COALESCE(stock.qty, 0) AS OnHand,
                p.min_stock_qty AS MinStock
            FROM products p
            INNER JOIN LATERAL (
                SELECT COALESCE(SUM(b.quantity_available), 0) AS qty
                FROM inventory_batches b
                WHERE b.tenant_id = p.tenant_id AND b.product_id = p.id AND b.warehouse_id = @WarehouseId
            ) stock ON stock.qty > 0
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL AND p.status = 1
            ORDER BY random()
            LIMIT @TakeRandom
            """,
            new { TenantId, WarehouseId = warehouseId, TakeRandom = takeRandom })).ToList();

        var map = new Dictionary<Guid, LossCycleCountSuggestionDto>();
        void Add(IEnumerable<(Guid ProductId, string Sku, string ProductName, decimal OnHand, decimal? MinStock)> rows, string source)
        {
            foreach (var r in rows)
            {
                if (map.Count >= limit) break;
                if (map.ContainsKey(r.ProductId)) continue;
                map[r.ProductId] = new LossCycleCountSuggestionDto(
                    r.ProductId, r.Sku, r.ProductName, source, r.OnHand, r.MinStock);
            }
        }

        Add(hot, "hot7d");
        Add(min, "min_stock");
        Add(random, "random");
        return map.Values.ToList();
    }

    public async Task<LossCycleCountStatusDto> GetCycleCountStatusTodayAsync(
        Guid? branchId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var businessDate = VietnamBusinessCalendar.Today(DateTime.UtcNow);
        var (dayStart, dayEnd) = VietnamBusinessCalendar.TodayRangeUtc(DateTime.UtcNow);
        var branchFilter = branchId is not null ? "AND w.branch_id = @BranchId" : string.Empty;
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND a.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var active = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Number)?>(
            $"""
            SELECT a.id AS Id, a.adjustment_number AS Number
            FROM inventory_adjustments a
            INNER JOIN warehouses w ON w.id = a.warehouse_id AND w.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId
              AND a.status = @Counting
              AND a.reason ILIKE @ReasonPrefix
              {branchFilter}
              {warehouseFilter}
            ORDER BY a.created_at DESC
            LIMIT 1
            """,
            new
            {
                TenantId,
                Counting = AdjustmentStatuses.Counting,
                ReasonPrefix = CycleCountReasonPrefix + "%",
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        if (active is { } open)
        {
            return new LossCycleCountStatusDto(
                businessDate, "in_progress", open.Id, open.Number, null, 0);
        }

        var approved = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Number, int VarianceSkuCount)?>(
            $"""
            SELECT
                a.id AS Id,
                a.adjustment_number AS Number,
                COUNT(*) FILTER (WHERE ABS(i.difference_quantity) > 0)::int AS VarianceSkuCount
            FROM inventory_adjustments a
            INNER JOIN warehouses w ON w.id = a.warehouse_id AND w.tenant_id = a.tenant_id
            LEFT JOIN inventory_adjustment_items i ON i.adjustment_id = a.id
            WHERE a.tenant_id = @TenantId
              AND a.status = @Approved
              AND a.reason ILIKE @ReasonPrefix
              AND a.approved_at >= @DayStart AND a.approved_at < @DayEnd
              {branchFilter}
              {warehouseFilter}
            GROUP BY a.id, a.adjustment_number
            ORDER BY a.approved_at DESC
            LIMIT 1
            """,
            new
            {
                TenantId,
                Approved = AdjustmentStatuses.Approved,
                ReasonPrefix = CycleCountReasonPrefix + "%",
                DayStart = dayStart,
                DayEnd = dayEnd,
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        if (approved is null)
            return new LossCycleCountStatusDto(businessDate, "not_done", null, null, null, 0);

        var row = approved.Value;
        var status = row.VarianceSkuCount > 0 ? "has_variance" : "done";
        return new LossCycleCountStatusDto(businessDate, status, row.Id, row.Number, null, row.VarianceSkuCount);
    }

    public async Task<IReadOnlyList<LossCycleCountVarianceRowDto>> ListCycleCountVarianceAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? branchId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var branchFilter = branchId is not null ? "AND w.branch_id = @BranchId" : string.Empty;
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND a.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            DateTime ApprovedAt,
            Guid ProductId,
            string Sku,
            string ProductName,
            Guid AdjustmentId,
            string AdjustmentNumber,
            decimal SystemQuantity,
            decimal ActualQuantity,
            decimal DifferenceQuantity)>(
            $"""
            SELECT
                a.approved_at AS ApprovedAt,
                p.id AS ProductId,
                p.product_code AS Sku,
                p.product_name AS ProductName,
                a.id AS AdjustmentId,
                a.adjustment_number AS AdjustmentNumber,
                SUM(i.system_quantity) AS SystemQuantity,
                SUM(i.actual_quantity) AS ActualQuantity,
                SUM(i.difference_quantity) AS DifferenceQuantity
            FROM inventory_adjustments a
            INNER JOIN warehouses w ON w.id = a.warehouse_id AND w.tenant_id = a.tenant_id
            INNER JOIN inventory_adjustment_items i ON i.adjustment_id = a.id
            INNER JOIN inventory_batches b ON b.id = i.batch_id
            INNER JOIN products p ON p.id = b.product_id AND p.tenant_id = a.tenant_id
            WHERE a.tenant_id = @TenantId
              AND a.status = @Approved
              AND a.reason ILIKE @ReasonPrefix
              AND a.approved_at >= @FromUtc AND a.approved_at < @ToUtc
              AND ABS(i.difference_quantity) > 0
              {branchFilter}
              {warehouseFilter}
            GROUP BY a.approved_at, p.id, p.product_code, p.product_name, a.id, a.adjustment_number
            ORDER BY a.approved_at DESC, ABS(SUM(i.difference_quantity)) DESC
            """,
            new
            {
                TenantId,
                Approved = AdjustmentStatuses.Approved,
                ReasonPrefix = CycleCountReasonPrefix + "%",
                FromUtc = fromUtc,
                ToUtc = toUtc,
                BranchId = branchId,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        return rows.Select(r => new LossCycleCountVarianceRowDto(
            VietnamBusinessCalendar.Today(r.ApprovedAt),
            r.ProductId,
            r.Sku,
            r.ProductName,
            r.AdjustmentId,
            r.AdjustmentNumber,
            r.SystemQuantity,
            r.ActualQuantity,
            r.DifferenceQuantity,
            $"/inventory/adjustments/{r.AdjustmentId}/count")).ToList();
    }

    private static LossAuditFeedItemDto MapAuditFeedItem(AuditFeedRow r)
    {
        var docHref = r.EventType switch
        {
            "order_create" or "order_edit" or "order_cancel" or "discount" => "/sales/orders",
            "return" => "/sales/returns",
            "stock_adjust" or "internal_issue" => "/inventory/adjustments",
            _ => null,
        };
        var summary = BuildAuditSummary(r.EventType, r.DocumentNumber, r.Action, r.PayloadJson);
        return new LossAuditFeedItemDto(
            r.Id,
            r.OccurredAt,
            r.EventType,
            r.ActorUserId,
            r.ActorUsername,
            summary,
            r.EntityType,
            r.EntityId,
            r.DocumentNumber,
            docHref,
            r.BranchId,
            r.BranchName);
    }

    private static string BuildAuditSummary(string eventType, string? documentNumber, string action, string? payloadJson)
    {
        var doc = string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber.Trim();
        return eventType switch
        {
            "order_create" => doc is null ? $"Tạo hóa đơn ({action})" : $"Tạo hóa đơn {doc}",
            "order_edit" => doc is null ? "Sửa nháp hóa đơn" : $"Sửa nháp {doc}",
            "order_cancel" => doc is null ? "Hủy nháp hóa đơn" : $"Hủy nháp {doc}",
            "discount" => BuildDiscountSummary(doc, payloadJson),
            "return" => doc is null ? "Trả hàng" : $"Trả hàng {doc}",
            "stock_adjust" => doc is null ? $"Điều chỉnh tồn ({action})" : $"Điều chỉnh tồn {doc} ({action})",
            "internal_issue" => doc is null ? $"Xuất nội bộ ({action})" : $"Xuất nội bộ {doc} ({action})",
            _ => doc ?? eventType,
        };
    }

    private static string BuildDiscountSummary(string? documentNumber, string? payloadJson)
    {
        decimal? amount = null;
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
                if (doc.RootElement.TryGetProperty("posDiscountTotal", out var p)
                    || doc.RootElement.TryGetProperty("PosDiscountTotal", out p))
                    amount = p.GetDecimal();
            }
            catch (System.Text.Json.JsonException)
            {
                // ignore malformed payload
            }
        }

        var head = documentNumber is null ? "Giảm giá POS" : $"Giảm giá POS {documentNumber}";
        return amount is null ? head : $"{head}: {amount:N0}";
    }

    private static string BuildSalesScopeFilters(Guid? branchId, Guid[]? allowedWarehouseIds)
    {
        var branchFilter = branchId is not null ? "AND w.branch_id = @BranchId" : string.Empty;
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND o.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        return $"{branchFilter} {warehouseFilter}";
    }

    private sealed class AuditFeedRow
    {
        public Guid Id { get; init; }
        public DateTime OccurredAt { get; init; }
        public string EventType { get; init; } = "";
        public Guid? ActorUserId { get; init; }
        public string? ActorUsername { get; init; }
        public string EntityType { get; init; } = "";
        public Guid? EntityId { get; init; }
        public string? DocumentNumber { get; init; }
        public Guid? BranchId { get; init; }
        public string? BranchName { get; init; }
        public string Action { get; init; } = "";
        public string? PayloadJson { get; init; }
    }

    private sealed class WarehouseMetaRow
    {
        public string WarehouseName { get; init; } = "";
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = "";
    }
}
