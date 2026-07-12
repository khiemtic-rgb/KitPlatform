using System.Data;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Application.Integration;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Packs.Pharmacy;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Packs.Pharmacy.Rx;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Infrastructure.Loyalty;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class SalesRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly InventoryRepository _inventory;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly IPricingEngine _pricing;
    private readonly ITenantSettingsService _tenantSettings;
    private readonly IIntegrationOutboxWriter _outbox;
    private readonly IPlatformEventWriter _platformEvents;
    private readonly IWorkflowEngine _workflow;
    private readonly LoyaltyPosService _loyaltyPos;
    private readonly VoucherPosService _voucherPos;
    private readonly PrescriptionRepository _prescriptions;

    public SalesRepository(
        IDbConnectionFactory db,
        ITenantContext tenant,
        InventoryRepository inventory,
        IInventoryEngine inventoryEngine,
        IPricingEngine pricing,
        ITenantSettingsService tenantSettings,
        IIntegrationOutboxWriter outbox,
        IPlatformEventWriter platformEvents,
        IWorkflowEngine workflow,
        LoyaltyPosService loyaltyPos,
        VoucherPosService voucherPos,
        PrescriptionRepository prescriptions)
    {
        _db = db;
        _tenant = tenant;
        _inventory = inventory;
        _inventoryEngine = inventoryEngine;
        _pricing = pricing;
        _tenantSettings = tenantSettings;
        _outbox = outbox;
        _platformEvents = platformEvents;
        _workflow = workflow;
        _loyaltyPos = loyaltyPos;
        _voucherPos = voucherPos;
        _prescriptions = prescriptions;
    }

    private Guid TenantId => _tenant.TenantId;

    private Task EnforceSaleDiscountPolicyAsync(
        SaleOrderPricingResult pricing,
        SalesDiscountPolicy discountPolicy,
        Guid? approvedWorkflowTaskId = null,
        Guid? salesOrderId = null,
        CancellationToken cancellationToken = default) =>
        _workflow.EnforcePosDiscountApprovalAsync(
            pricing,
            discountPolicy,
            approvedWorkflowTaskId,
            salesOrderId,
            cancellationToken);

    public async Task<IReadOnlyList<CustomerListItemDto>> SearchCustomersAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "c.tenant_id = @TenantId", "c.deleted_at IS NULL" };
        var param = new DynamicParameters();
        param.Add("TenantId", TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("""
                (
                    c.full_name ILIKE @Search
                    OR c.phone ILIKE @Search
                    OR c.customer_code ILIKE @Search
                    OR (@SearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @SearchDigitsPattern)
                )
                """);
            param.Add("Search", $"%{search.Trim()}%");
            AddPhoneDigitSearchParams(param, search, "Search");
        }

        var sql = $"""
            SELECT
                c.id AS Id, c.customer_code AS CustomerCode, c.full_name AS FullName,
                c.phone AS Phone, c.email AS Email,
                c.allow_credit AS AllowCredit, c.credit_limit AS CreditLimit,
                COALESCE((
                    SELECT SUM(so.outstanding)
                    FROM sales_orders so
                    WHERE so.customer_id = c.id
                      AND so.tenant_id = c.tenant_id
                      AND so.status = @Completed
                      AND so.outstanding > 0
                ), 0) AS CurrentOutstanding
            FROM customers c
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY c.full_name
            LIMIT 50
            """;
        param.Add("Completed", SalesOrderStatuses.Completed);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerListItemDto>(sql, param)).ToList();
    }

    public async Task<PosProductLookupDto?> LookupByBarcodeAsync(
        string barcode,
        Guid warehouseId,
        short priceType,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<PosProductLookupRow>(
            PosProductLookupByBarcodeSql,
            new
            {
                TenantId,
                WarehouseId = warehouseId,
                PriceType = priceType,
                Barcode = barcode.Trim(),
            });
        return row is null ? null : await CompletePosLookupAsync(conn, row, warehouseId, cancellationToken);
    }

    public async Task<PosProductLookupDto?> LookupByProductCodeAsync(
        string productCode,
        Guid warehouseId,
        short priceType,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<PosProductLookupRow>(
            PosProductLookupByCodeSql,
            new
            {
                TenantId,
                WarehouseId = warehouseId,
                PriceType = priceType,
                ProductCode = productCode.Trim(),
            });
        return row is null ? null : await CompletePosLookupAsync(conn, row, warehouseId, cancellationToken);
    }

    private async Task<PosProductLookupDto> CompletePosLookupAsync(
        IDbConnection conn,
        PosProductLookupRow row,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var stockInSaleUnit = row.ConversionFactor > 0
            ? row.StockAvailable / row.ConversionFactor
            : row.StockAvailable;
        var lookup = new PosProductLookupDto(
            row.ProductId,
            row.ProductCode,
            row.ProductName,
            row.ProductUnitId,
            row.UnitName,
            row.ConversionFactor,
            row.UnitPrice,
            stockInSaleUnit,
            row.DispensingClass);

        var batchMode = await _tenantSettings.GetBatchModeAsync(cancellationToken);
        if (batchMode == TenantBatchMode.Off)
        {
            return lookup with
            {
                BatchHints = null,
                StockSourceLabel = StockSourceLabels.SystemBook,
            };
        }

        var batches = await _inventoryEngine.GetAvailableBatchesAsync(
            conn, warehouseId, lookup.ProductId, cancellationToken);
        var hints = batches
            .Select((b, index) => new PosBatchHintDto(
                b.Id,
                b.BatchNumber,
                b.ExpiryDate,
                lookup.ConversionFactor > 0
                    ? b.QuantityAvailable / lookup.ConversionFactor
                    : b.QuantityAvailable,
                index == 0))
            .ToList();

        return lookup with { BatchHints = hints, StockSourceLabel = StockSourceLabels.SystemBook };
    }

    public async Task ReportRxPosBlockAsync(
        ReportRxPosBlockRequest request,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var rxSettings = await _tenantSettings.GetRxSettingsAsync(cancellationToken);
        if (!rxSettings.PosBlockedAudit)
            return;

        if (RxEnforcementMode.Parse(rxSettings.EnforcementMode) != RxEnforcementMode.Strict)
            return;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        const string productSql = """
            SELECT dispensing_class AS DispensingClass
            FROM products
            WHERE id = @ProductId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        var dispensingClass = await conn.QuerySingleOrDefaultAsync<string?>(
            productSql, new { request.ProductId, TenantId });
        if (!DispensingClass.RequiresPrescription(dispensingClass))
            return;

        var branchId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT branch_id FROM warehouses
            WHERE id = @WarehouseId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { request.WarehouseId, TenantId });

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_pharmacy.rx_pos_block_events
                (tenant_id, branch_id, warehouse_id, product_id, user_id, source)
            VALUES
                (@TenantId, @BranchId, @WarehouseId, @ProductId, @UserId, 'pos_scan')
            """,
            new
            {
                TenantId,
                BranchId = branchId,
                request.WarehouseId,
                request.ProductId,
                UserId = userId,
            });
    }

    private const string PosProductLookupSelect = """
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE(p.dispensing_class, CASE p.drug_type WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END) AS DispensingClass,
                u.id AS ProductUnitId,
                u.unit_name AS UnitName,
                u.conversion_factor AS ConversionFactor,
                pr.price AS UnitPrice,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = @TenantId AND b.warehouse_id = @WarehouseId
                      AND b.product_id = p.id AND b.quantity_available > 0
                ), 0) AS StockAvailable
            """;

    private const string PosProductLookupByBarcodeSql = PosProductLookupSelect + """

            FROM product_barcodes bc
            INNER JOIN products p ON p.id = bc.product_id
            INNER JOIN LATERAL (
                SELECT id, unit_name, conversion_factor
                FROM product_units pu
                WHERE pu.product_id = p.id AND pu.tenant_id = @TenantId
                ORDER BY pu.is_sale_unit DESC, pu.is_base_unit DESC, pu.unit_name
                LIMIT 1
            ) u ON TRUE
            LEFT JOIN LATERAL (
                SELECT price FROM product_prices pp
                WHERE pp.tenant_id = @TenantId AND pp.product_id = p.id
                  AND pp.product_unit_id = u.id AND pp.price_type = @PriceType
                  AND pp.status = 1 AND pp.effective_from <= NOW()
                  AND (pp.effective_to IS NULL OR pp.effective_to > NOW())
                ORDER BY pp.effective_from DESC
                LIMIT 1
            ) pr ON TRUE
            WHERE bc.tenant_id = @TenantId AND bc.barcode = @Barcode
              AND bc.status = 1 AND p.deleted_at IS NULL
            LIMIT 1
            """;

    private const string PosProductLookupByCodeSql = PosProductLookupSelect + """

            FROM products p
            INNER JOIN LATERAL (
                SELECT id, unit_name, conversion_factor
                FROM product_units pu
                WHERE pu.product_id = p.id AND pu.tenant_id = @TenantId
                ORDER BY pu.is_sale_unit DESC, pu.is_base_unit DESC, pu.unit_name
                LIMIT 1
            ) u ON TRUE
            LEFT JOIN LATERAL (
                SELECT price FROM product_prices pp
                WHERE pp.tenant_id = @TenantId AND pp.product_id = p.id
                  AND pp.product_unit_id = u.id AND pp.price_type = @PriceType
                  AND pp.status = 1 AND pp.effective_from <= NOW()
                  AND (pp.effective_to IS NULL OR pp.effective_to > NOW())
                ORDER BY pp.effective_from DESC
                LIMIT 1
            ) pr ON TRUE
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL
              AND lower(trim(p.product_code)) = lower(trim(@ProductCode))
            LIMIT 1
            """;

    public async Task<PosStockCheckDto?> GetPosStockByUnitAsync(
        Guid warehouseId,
        Guid productUnitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                u.id AS ProductUnitId,
                u.unit_name AS UnitName,
                u.conversion_factor AS ConversionFactor,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = @TenantId AND b.warehouse_id = @WarehouseId
                      AND b.product_id = p.id AND b.quantity_available > 0
                ), 0) AS StockAvailable
            FROM product_units u
            INNER JOIN products p ON p.id = u.product_id
            WHERE u.id = @ProductUnitId AND u.tenant_id = @TenantId AND p.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<PosStockCheckRow>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            ProductUnitId = productUnitId,
        });
        if (row is null) return null;

        var stockInSaleUnit = row.ConversionFactor > 0
            ? row.StockAvailable / row.ConversionFactor
            : row.StockAvailable;
        return new PosStockCheckDto(
            row.ProductId,
            row.ProductCode,
            row.ProductName,
            row.ProductUnitId,
            row.UnitName,
            row.ConversionFactor,
            stockInSaleUnit,
            StockSourceLabels.SystemBook);
    }

    public async Task<IReadOnlyList<PosStockCheckDto>> GetPosStockBulkAsync(
        Guid warehouseId,
        IReadOnlyList<Guid> productUnitIds,
        CancellationToken cancellationToken)
    {
        if (productUnitIds.Count == 0)
            return [];

        const string sql = """
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                u.id AS ProductUnitId,
                u.unit_name AS UnitName,
                u.conversion_factor AS ConversionFactor,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = @TenantId AND b.warehouse_id = @WarehouseId
                      AND b.product_id = p.id AND b.quantity_available > 0
                ), 0) AS StockAvailable
            FROM product_units u
            INNER JOIN products p ON p.id = u.product_id
            WHERE u.tenant_id = @TenantId AND p.deleted_at IS NULL
              AND u.id = ANY(@ProductUnitIds)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PosStockCheckRow>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            ProductUnitIds = productUnitIds.ToArray(),
        });

        return rows.Select(row =>
        {
            var stockInSaleUnit = row.ConversionFactor > 0
                ? row.StockAvailable / row.ConversionFactor
                : row.StockAvailable;
            return new PosStockCheckDto(
                row.ProductId,
                row.ProductCode,
                row.ProductName,
                row.ProductUnitId,
                row.UnitName,
                row.ConversionFactor,
                stockInSaleUnit,
                StockSourceLabels.SystemBook);
        }).ToList();
    }

    public async Task<IReadOnlyList<PosProductSearchItemDto>> SearchPosProductsAsync(
        string search,
        Guid warehouseId,
        short priceType,
        CancellationToken cancellationToken)
    {
        var term = search.Trim();
        if (string.IsNullOrWhiteSpace(term))
            return [];

        const string sql = """
            SELECT
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE((
                    SELECT bc.barcode
                    FROM product_barcodes bc
                    WHERE bc.product_id = p.id AND bc.tenant_id = @TenantId
                      AND bc.is_primary = TRUE AND bc.status = 1
                    LIMIT 1
                ), p.product_code) AS LookupCode,
                u.unit_name AS UnitName,
                u.conversion_factor AS ConversionFactor,
                COALESCE(pr.price, 0) AS UnitPrice,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = @TenantId AND b.warehouse_id = @WarehouseId
                      AND b.product_id = p.id AND b.quantity_available > 0
                ), 0) AS StockAvailable
            FROM products p
            INNER JOIN LATERAL (
                SELECT id, unit_name, conversion_factor
                FROM product_units pu
                WHERE pu.product_id = p.id AND pu.tenant_id = @TenantId
                ORDER BY pu.is_sale_unit DESC, pu.is_base_unit DESC, pu.unit_name
                LIMIT 1
            ) u ON TRUE
            LEFT JOIN LATERAL (
                SELECT price FROM product_prices pp
                WHERE pp.tenant_id = @TenantId AND pp.product_id = p.id
                  AND pp.product_unit_id = u.id AND pp.price_type = @PriceType
                  AND pp.status = 1 AND pp.effective_from <= NOW()
                  AND (pp.effective_to IS NULL OR pp.effective_to > NOW())
                ORDER BY pp.effective_from DESC
                LIMIT 1
            ) pr ON TRUE
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL AND p.status = 1
              AND (
                p.product_name ILIKE @Pattern
                OR p.product_code ILIKE @Pattern
                OR EXISTS (
                    SELECT 1 FROM product_barcodes bc
                    WHERE bc.product_id = p.id AND bc.tenant_id = @TenantId
                      AND bc.status = 1 AND bc.barcode ILIKE @Pattern
                )
              )
            ORDER BY p.product_name
            LIMIT 20
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PosProductSearchRow>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            PriceType = priceType,
            Pattern = $"%{term}%",
        });

        return rows.Select(row =>
        {
            var stockInSaleUnit = row.ConversionFactor > 0
                ? row.StockAvailable / row.ConversionFactor
                : row.StockAvailable;
            return new PosProductSearchItemDto(
                row.ProductCode,
                row.ProductName,
                row.LookupCode,
                row.UnitName,
                row.UnitPrice,
                stockInSaleUnit);
        }).ToList();
    }

    public async Task<PosAllocationPreviewDto> PreviewPosAllocationAsync(
        PosAllocationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            return new PosAllocationPreviewDto([], StockSourceLabels.SystemBook);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var lines = new List<PosAllocationPreviewLineDto>();

        foreach (var item in request.Items)
        {
            var unit = await conn.QuerySingleOrDefaultAsync<ProductUnitRow>(
                """
                SELECT p.product_code AS ProductCode, p.product_name AS ProductName,
                       u.unit_name AS UnitName, u.conversion_factor AS ConversionFactor
                FROM product_units u
                INNER JOIN products p ON p.id = u.product_id
                WHERE u.id = @ProductUnitId AND u.tenant_id = @TenantId AND p.deleted_at IS NULL
                """,
                new { ProductUnitId = item.ProductUnitId, TenantId });

            if (unit is null)
                throw new InvalidOperationException($"Đơn vị sản phẩm {item.ProductUnitId} không tồn tại.");

            var batches = await _inventoryEngine.GetAvailableBatchesAsync(
                conn, request.WarehouseId, item.ProductId, cancellationToken);
            var baseNeeded = item.Quantity * unit.ConversionFactor;
            IReadOnlyList<FifoAllocationResult> allocations;
            try
            {
                allocations = _inventoryEngine.AllocateFromBatches(batches, baseNeeded);
            }
            catch (InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Không đủ tồn kho theo FEFO cho {unit.ProductCode} — {unit.ProductName} " +
                    $"(cần {item.Quantity:N0} {unit.UnitName}, còn {SumSaleStock(batches, unit.ConversionFactor):N0} {unit.UnitName}).");
            }

            var batchById = batches.ToDictionary(b => b.Id);

            var previewAllocs = allocations.Select(a =>
            {
                var batch = batchById[a.BatchId];
                var saleQty = unit.ConversionFactor > 0 ? a.BaseQuantity / unit.ConversionFactor : a.BaseQuantity;
                return new PosBatchAllocationPreviewDto(
                    a.BatchId,
                    batch.BatchNumber,
                    batch.ExpiryDate,
                    saleQty,
                    batch.QuantityAvailable);
            }).ToList();

            lines.Add(new PosAllocationPreviewLineDto(
                item.ProductId,
                unit.ProductCode,
                unit.ProductName,
                item.ProductUnitId,
                unit.UnitName,
                item.Quantity,
                previewAllocs));
        }

        return new PosAllocationPreviewDto(lines, StockSourceLabels.SystemBook);
    }

    private static decimal SumSaleStock(IReadOnlyList<BatchAvailabilityDto> batches, decimal conversionFactor) =>
        conversionFactor > 0
            ? batches.Sum(b => b.QuantityAvailable) / conversionFactor
            : batches.Sum(b => b.QuantityAvailable);

    private sealed class ProductUnitRow
    {
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string UnitName { get; init; } = "";
        public decimal ConversionFactor { get; init; }
    }

    public async Task<(IReadOnlyList<SalesOrderListItemDto> Items, int Total)> GetSalesOrdersAsync(
        SalesOrderListFilter filter,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND o.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var statusFilter = filter.Status is short status
            ? "AND o.status = @Status"
            : string.Empty;
        var searchFilter = BuildSalesOrderSearchFilter(filter);

        var where = $"""
            o.tenant_id = @TenantId
              {warehouseFilter}
              {statusFilter}
              {searchFilter}
            """;
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM sales_orders o
            INNER JOIN warehouses w ON w.id = o.warehouse_id
            LEFT JOIN customers c ON c.id = o.customer_id
            WHERE {where}
            """;
        var sql = $"""
            SELECT
                o.id AS Id, o.order_number AS OrderNumber, o.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, o.customer_id AS CustomerId,
                c.full_name AS CustomerName, o.status AS Status, o.order_date AS OrderDate,
                o.total_amount AS TotalAmount,
                o.amount_paid AS AmountPaid,
                o.outstanding AS Outstanding,
                (SELECT COUNT(*)::int FROM sales_order_items i WHERE i.sales_order_id = o.id) AS ItemCount,
                COALESCE((
                    SELECT SUM(ri.refund_amount)
                    FROM sales_returns sr
                    INNER JOIN sales_return_items ri ON ri.sales_return_id = sr.id
                    WHERE sr.sales_order_id = o.id AND sr.status = @ReturnCompleted
                ), 0) AS TotalRefunded,
                o.sales_shift_id AS SalesShiftId,
                sh.shift_number AS ShiftNumber
            FROM sales_orders o
            INNER JOIN warehouses w ON w.id = o.warehouse_id
            LEFT JOIN customers c ON c.id = o.customer_id
            LEFT JOIN sales_shifts sh ON sh.id = o.sales_shift_id
            WHERE {where}
            ORDER BY o.order_date DESC
            LIMIT @PageSize OFFSET @Offset
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var customerSearch = filter.CustomerSearch?.Trim();
        var customerSearchDigits = ExtractPhoneDigits(customerSearch);
        var legacySearch = filter.Search?.Trim();
        var legacySearchDigits = ExtractPhoneDigits(legacySearch);
        var args = new
        {
            TenantId,
            AllowedWarehouseIds = allowedWarehouseIds,
            ReturnCompleted = SalesReturnStatuses.Completed,
            Status = filter.Status,
            SearchPattern = string.IsNullOrWhiteSpace(legacySearch) ? null : $"%{legacySearch}%",
            SearchDigits = legacySearchDigits,
            SearchDigitsPattern = BuildPhoneDigitsPattern(legacySearchDigits),
            CustomerSearchPattern = string.IsNullOrWhiteSpace(customerSearch) ? null : $"%{customerSearch}%",
            CustomerSearchDigits = customerSearchDigits,
            CustomerSearchDigitsPattern = BuildPhoneDigitsPattern(customerSearchDigits),
            DocumentSearchPattern = string.IsNullOrWhiteSpace(filter.DocumentSearch) ? null : $"%{filter.DocumentSearch.Trim()}%",
            PageSize = pageSize,
            Offset = offset,
        };
        var total = await conn.ExecuteScalarAsync<int>(countSql, args);
        var items = (await conn.QueryAsync<SalesOrderListItemDto>(sql, args)).ToList();
        return (items, total);
    }

    public async Task<SalesOrderDetailDto?> GetSalesOrderAsync(
        Guid id,
        CancellationToken cancellationToken,
        bool freshSale = false)
    {
        const string headerSql = """
            SELECT
                o.id AS Id, o.order_number AS OrderNumber, o.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, o.customer_id AS CustomerId,
                c.full_name AS CustomerName, o.status AS Status, o.order_date AS OrderDate,
                o.subtotal AS Subtotal, o.discount_amount AS DiscountAmount, o.total_amount AS TotalAmount,
                o.amount_paid AS AmountPaid, o.outstanding AS Outstanding,
                o.order_discount_type AS OrderDiscountType, o.order_discount_value AS OrderDiscountValue,
                o.notes AS Notes,
                COALESCE((
                    SELECT SUM(ri.refund_amount)
                    FROM sales_returns sr
                    INNER JOIN sales_return_items ri ON ri.sales_return_id = sr.id
                    WHERE sr.sales_order_id = o.id AND sr.status = @ReturnCompleted
                ), 0) AS TotalRefunded,
                o.sales_shift_id AS SalesShiftId,
                sh.shift_number AS ShiftNumber,
                (
                    SELECT lt.points
                    FROM loyalty_transactions lt
                    WHERE lt.sales_order_id = o.id AND lt.transaction_type = 1
                    LIMIT 1
                ) AS LoyaltyPointsEarned,
                o.loyalty_points_redeemed AS LoyaltyPointsRedeemed,
                o.loyalty_discount_amount AS LoyaltyDiscountAmount,
                o.voucher_discount_amount AS VoucherDiscountAmount,
                v.voucher_code AS VoucherCode,
                v.voucher_name AS VoucherName
            FROM sales_orders o
            INNER JOIN warehouses w ON w.id = o.warehouse_id
            LEFT JOIN customers c ON c.id = o.customer_id
            LEFT JOIN sales_shifts sh ON sh.id = o.sales_shift_id
            LEFT JOIN vouchers v ON v.id = o.voucher_id
            WHERE o.id = @Id AND o.tenant_id = @TenantId
            """;
        const string itemsSql = """
            SELECT
                i.id AS Id, i.product_id AS ProductId, p.product_code AS ProductCode,
                p.product_name AS ProductName, i.product_unit_id AS ProductUnitId, u.unit_name AS UnitName,
                i.batch_id AS BatchId, b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate, i.quantity AS Quantity,
                i.unit_price AS UnitPrice, i.discount_amount AS DiscountAmount,
                i.discount_type AS DiscountType, i.discount_value AS DiscountValue,
                i.line_total AS LineTotal,
                COALESCE((
                    SELECT SUM(ri.quantity)
                    FROM sales_return_items ri
                    INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                    WHERE ri.sales_order_item_id = i.id AND r.status = @ReturnCompleted
                ), 0) AS ReturnedQuantity
            FROM sales_order_items i
            INNER JOIN products p ON p.id = i.product_id
            INNER JOIN product_units u ON u.id = i.product_unit_id
            LEFT JOIN inventory_batches b ON b.id = i.batch_id
            WHERE i.sales_order_id = @OrderId
            ORDER BY p.product_name, b.batch_number NULLS LAST
            """;
        const string itemsFreshSql = """
            SELECT
                i.id AS Id, i.product_id AS ProductId, p.product_code AS ProductCode,
                p.product_name AS ProductName, i.product_unit_id AS ProductUnitId, u.unit_name AS UnitName,
                i.batch_id AS BatchId, b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate, i.quantity AS Quantity,
                i.unit_price AS UnitPrice, i.discount_amount AS DiscountAmount,
                i.discount_type AS DiscountType, i.discount_value AS DiscountValue,
                i.line_total AS LineTotal,
                0::decimal AS ReturnedQuantity
            FROM sales_order_items i
            INNER JOIN products p ON p.id = i.product_id
            INNER JOIN product_units u ON u.id = i.product_unit_id
            LEFT JOIN inventory_batches b ON b.id = i.batch_id
            WHERE i.sales_order_id = @OrderId
            ORDER BY p.product_name, b.batch_number NULLS LAST
            """;
        const string paymentsSql = """
            SELECT id AS Id, payment_method AS PaymentMethod, amount AS Amount, paid_at AS PaidAt
            FROM sales_payments WHERE sales_order_id = @OrderId
            ORDER BY paid_at
            """;
        const string refundPaymentsSql = """
            SELECT rp.payment_method AS PaymentMethod, SUM(rp.amount) AS Amount
            FROM sales_return_payments rp
            INNER JOIN sales_returns sr ON sr.id = rp.sales_return_id
            WHERE sr.sales_order_id = @OrderId AND sr.status = @ReturnCompleted
            GROUP BY rp.payment_method
            ORDER BY rp.payment_method
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<SalesOrderHeaderRow>(headerSql, new
        {
            Id = id,
            TenantId,
            ReturnCompleted = SalesReturnStatuses.Completed,
        });
        if (header is null) return null;
        var items = (await conn.QueryAsync<SalesOrderItemDto>(
            freshSale ? itemsFreshSql : itemsSql,
            new
            {
                OrderId = id,
                ReturnCompleted = SalesReturnStatuses.Completed,
            })).ToList();
        var payments = (await conn.QueryAsync<SalesPaymentDto>(paymentsSql, new { OrderId = id })).ToList();
        var refundPayments = freshSale
            ? []
            : (await conn.QueryAsync<SalesRefundPaymentSummaryDto>(refundPaymentsSql, new
            {
                OrderId = id,
                ReturnCompleted = SalesReturnStatuses.Completed,
            })).ToList();
        var totalRefunded = freshSale ? 0m : header.TotalRefunded;
        var lineDiscountTotal = items.Sum(i => i.DiscountAmount);
        return new SalesOrderDetailDto(
            header.Id, header.OrderNumber, header.WarehouseId, header.WarehouseName,
            header.CustomerId, header.CustomerName, header.Status, header.OrderDate,
            header.Subtotal, header.DiscountAmount, lineDiscountTotal,
            header.OrderDiscountType, header.OrderDiscountValue, header.TotalAmount,
            header.AmountPaid, header.Outstanding, totalRefunded,
            header.Notes, items, payments, refundPayments,
            header.SalesShiftId, header.ShiftNumber, header.LoyaltyPointsEarned,
            header.LoyaltyPointsRedeemed, header.LoyaltyDiscountAmount,
            header.VoucherDiscountAmount, header.VoucherCode, header.VoucherName);
    }

    public async Task<SaleOrderPricingResult> PriceSaleLinesAsync(
        IReadOnlyList<CreateSaleLineRequest> items,
        short priceType,
        SaleDiscountInput? orderDiscount,
        SalesDiscountPolicy discountPolicy,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng bán.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var pricedInputs = await ResolveSaleLineInputsAsync(conn, tx, items, priceType, cancellationToken);
        var pricing = _pricing.PriceSaleOrder(pricedInputs, orderDiscount);
        await EnforceSaleDiscountPolicyAsync(pricing, discountPolicy, cancellationToken: cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return pricing;
    }

    public async Task<Guid> CreateCompletedSaleAsync(
        CreateSaleRequest request,
        Guid userId,
        SalesDiscountPolicy discountPolicy,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng bán.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var (branchId, employeeId) = await ResolveSaleContextAsync(
            conn, tx, request.WarehouseId, request.CustomerId, userId);
        await EnsureOpenShiftAsync(conn, tx, request.WarehouseId);
        var shiftId = await ResolveOpenShiftIdAsync(conn, tx, request.WarehouseId);
        var orderNumber = await _inventory.NextDocumentNumberAsync(conn, tx, "SO", "sales_orders", cancellationToken);

        var pricedInputs = await ResolveSaleLineInputsAsync(conn, tx, request.Items, request.PriceType, cancellationToken);
        var orderDiscount = new SaleDiscountInput(request.OrderDiscountType, request.OrderDiscountValue);
        var pricing = _pricing.PriceSaleOrder(pricedInputs, orderDiscount);
        await EnforceSaleDiscountPolicyAsync(
            pricing,
            discountPolicy,
            request.DiscountOverrideWorkflowTaskId,
            cancellationToken: cancellationToken);
        if (pricing.Lines.Count != request.Items.Count)
            throw new InvalidOperationException("Không đồng bộ số dòng đơn nháp.");

        var rxSettings = await _tenantSettings.GetRxSettingsAsync(cancellationToken);
        await RxDispensingEnforcer.EnforceStrictSaleAsync(
            conn,
            tx,
            TenantId,
            request.Items,
            request.PrescriptionId,
            rxSettings,
            userId,
            request.WarehouseId,
            branchId,
            "pos_complete",
            cancellationToken,
            request.ConnectRxHandoffId);

        var voucher = await _voucherPos.ResolveVoucherAsync(
            TenantId,
            request.CustomerId,
            request.CustomerVoucherId,
            pricing.TotalAmount,
            conn,
            tx,
            cancellationToken);

        var redeem = await _loyaltyPos.ResolveRedeemAsync(
            TenantId,
            request.CustomerId,
            request.LoyaltyPointsToRedeem,
            request.LoyaltyDiscountAmount,
            voucher.OrderTotalAfterVoucher,
            conn,
            tx,
            cancellationToken);

        var paymentResolution = await NormalizeAndValidatePaymentsAsync(
            conn, tx, request.CustomerId, request.Payments, redeem.FinalTotal, cancellationToken);
        var linePlans = await BuildFifoPlansAsync(
            conn,
            tx,
            request.WarehouseId,
            pricing.Lines,
            pricedInputs,
            cancellationToken);
        var orderReminder = SalesOrderReminderNormalizer.Normalize(
            request.OrderReminderLabel,
            request.OrderReminderDaysSupply);

        var orderId = await InsertSaleHeaderAsync(
            conn, tx, orderNumber, branchId, request.WarehouseId, request.CustomerId, employeeId,
            SalesOrderStatuses.Completed, request.PriceType, pricing.SubtotalGross, pricing.OrderDiscountAmount,
            request.OrderDiscountType, request.OrderDiscountValue ?? 0, redeem.FinalTotal,
            paymentResolution.AmountPaid, paymentResolution.Outstanding, request.Notes,
            shiftId, voucher.VoucherId, voucher.DiscountAmount, redeem.PointsRedeemed, redeem.DiscountAmount,
            orderReminder.Label, orderReminder.DaysSupply, request.PrescriptionId, request.ConnectRxHandoffId);

        var insertedLines = await InsertCompletedLinesAsync(
            conn,
            tx,
            orderId,
            request.WarehouseId,
            linePlans,
            cancellationToken);
        await InsertPaymentsAsync(conn, tx, orderId, paymentResolution.CashPayments);

        if (request.PrescriptionId is Guid prescriptionId)
        {
            var dispenseItems = insertedLines
                .Where(x => x.PrescriptionLineId.HasValue)
                .Select(x => new PrescriptionDispenseSaleItem(
                    x.SalesOrderItemId,
                    x.PrescriptionLineId!.Value,
                    x.Quantity))
                .ToList();
            await _prescriptions.ApplyDispenseFromSaleAsync(
                conn,
                tx,
                prescriptionId,
                orderId,
                dispenseItems,
                userId,
                cancellationToken);
        }

        await TryMarkConnectHandoffConsumedAsync(conn, tx, request.ConnectRxHandoffId, userId);

        if (voucher.VoucherId is Guid voucherId && voucher.CustomerVoucherId is Guid customerVoucherId)
        {
            await _voucherPos.TryMarkUsedAsync(customerVoucherId, voucherId, orderId, conn, tx);
        }

        if (redeem.PointsRedeemed > 0 && request.CustomerId is Guid redeemCustomer)
        {
            var programId = await _loyaltyPos.GetDefaultProgramIdAsync(TenantId, conn, tx, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy chương trình tích điểm.");
            await _loyaltyPos.TryRedeemForCompletedSaleAsync(
                TenantId,
                redeemCustomer,
                programId,
                orderId,
                orderNumber,
                redeem.PointsRedeemed,
                conn,
                tx,
                cancellationToken);
        }

        await _loyaltyPos.TryEarnForCompletedSaleAsync(
            TenantId,
            request.CustomerId,
            orderId,
            orderNumber,
            paymentResolution.AmountPaid,
            conn,
            tx,
            cancellationToken);

        await _outbox.WriteAsync(
            conn, tx,
            IntegrationOutboxEventTypes.OrderCompleted,
            IntegrationOutboxAggregateTypes.SalesOrder,
            orderId,
            new
            {
                orderId,
                orderNumber,
                request.WarehouseId,
                request.CustomerId,
                totalAmount = redeem.FinalTotal,
                loyaltyPointsRedeemed = redeem.PointsRedeemed,
                loyaltyDiscountAmount = redeem.DiscountAmount,
                status = SalesOrderStatuses.Completed,
                salesShiftId = shiftId,
            },
            userId,
            cancellationToken);

        await WriteSalesOrderCompletedPlatformEventAsync(
            conn,
            tx,
            orderId,
            orderNumber,
            request.WarehouseId,
            request.CustomerId,
            redeem.FinalTotal,
            redeem.PointsRedeemed,
            redeem.DiscountAmount,
            shiftId,
            userId,
            cancellationToken);

        await PharmacySalesOrderWriter.EnsureCompletedSaleAsync(
            conn,
            tx,
            TenantId,
            _tenant.WorkspaceId,
            orderId,
            orderNumber,
            request.CustomerId,
            SalesOrderStatuses.Completed,
            redeem.FinalTotal,
            DateTime.UtcNow,
            userId,
            cancellationToken);

        await TryUpsertRepurchaseSuggestionAsync(conn, tx, orderId);
        await TryAutoCreateDrinkRemindersFromOrderAsync(conn, tx, orderId);

        await tx.CommitAsync(cancellationToken);
        return orderId;
    }

    public async Task<Guid> CreateDraftSaleAsync(
        CreateSaleRequest request,
        Guid userId,
        SalesDiscountPolicy discountPolicy,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng bán.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var (branchId, employeeId) = await ResolveSaleContextAsync(conn, tx, request.WarehouseId, request.CustomerId, userId);
        var orderNumber = await _inventory.NextDocumentNumberAsync(conn, tx, "SO", "sales_orders", cancellationToken);
        var pricedInputs = await ResolveSaleLineInputsAsync(conn, tx, request.Items, request.PriceType, cancellationToken);
        var orderDiscount = new SaleDiscountInput(request.OrderDiscountType, request.OrderDiscountValue);
        var pricing = _pricing.PriceSaleOrder(pricedInputs, orderDiscount);
        await EnforceSaleDiscountPolicyAsync(
            pricing,
            discountPolicy,
            request.DiscountOverrideWorkflowTaskId,
            cancellationToken: cancellationToken);

        var orderId = await InsertSaleHeaderAsync(
            conn, tx, orderNumber, branchId, request.WarehouseId, request.CustomerId, employeeId,
            SalesOrderStatuses.Draft, request.PriceType, pricing.SubtotalGross, pricing.OrderDiscountAmount,
            request.OrderDiscountType, request.OrderDiscountValue ?? 0, pricing.TotalAmount, 0, 0, request.Notes,
            prescriptionId: request.PrescriptionId,
            connectRxHandoffId: request.ConnectRxHandoffId);

        for (var i = 0; i < pricing.Lines.Count; i++)
        {
            await InsertDraftItemAsync(
                conn,
                tx,
                orderId,
                pricing.Lines[i],
                request.Items[i].PrescriptionLineId);
        }

        await tx.CommitAsync(cancellationToken);
        return orderId;
    }

    public async Task<bool> UpdateDraftSaleAsync(
        Guid id,
        UpdateDraftSaleRequest request,
        SalesDiscountPolicy discountPolicy,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng bán.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var status = await conn.QuerySingleOrDefaultAsync<short?>(
            "SELECT status FROM sales_orders WHERE id = @Id AND tenant_id = @TenantId",
            new { Id = id, TenantId }, tx);
        if (status != SalesOrderStatuses.Draft)
            throw new InvalidOperationException("Chỉ sửa được đơn ở trạng thái Nháp.");

        var pricedInputs = await ResolveSaleLineInputsAsync(conn, tx, request.Items, request.PriceType, cancellationToken);
        var orderDiscount = new SaleDiscountInput(request.OrderDiscountType, request.OrderDiscountValue);
        var pricing = _pricing.PriceSaleOrder(pricedInputs, orderDiscount);
        await EnforceSaleDiscountPolicyAsync(
            pricing,
            discountPolicy,
            request.DiscountOverrideWorkflowTaskId,
            salesOrderId: id,
            cancellationToken: cancellationToken);
        if (pricing.Lines.Count != request.Items.Count)
            throw new InvalidOperationException("Không đồng bộ số dòng đơn nháp.");

        await conn.ExecuteAsync("""
            UPDATE sales_orders SET
                customer_id = @CustomerId, price_type = @PriceType,
                subtotal = @Subtotal, discount_amount = @OrderDiscountAmount,
                order_discount_type = @OrderDiscountType, order_discount_value = @OrderDiscountValue,
                total_amount = @TotalAmount, notes = @Notes
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft
            """, new
        {
            Id = id,
            TenantId,
            request.CustomerId,
            request.PriceType,
            Subtotal = pricing.SubtotalGross,
            OrderDiscountAmount = pricing.OrderDiscountAmount,
            OrderDiscountType = request.OrderDiscountType,
            OrderDiscountValue = request.OrderDiscountValue ?? 0,
            TotalAmount = pricing.TotalAmount,
            request.Notes,
            Draft = SalesOrderStatuses.Draft,
        }, tx);

        await conn.ExecuteAsync("DELETE FROM sales_order_items WHERE sales_order_id = @OrderId", new { OrderId = id }, tx);
        for (var i = 0; i < pricing.Lines.Count; i++)
        {
            await InsertDraftItemAsync(
                conn,
                tx,
                id,
                pricing.Lines[i],
                request.Items[i].PrescriptionLineId);
        }

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteDraftSaleAsync(
        Guid id,
        CompleteDraftSaleRequest? request,
        SalesDiscountPolicy discountPolicy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string headerSql = """
            SELECT
                warehouse_id AS WarehouseId, customer_id AS CustomerId, price_type AS PriceType,
                subtotal AS Subtotal, discount_amount AS OrderDiscountAmount, total_amount AS TotalAmount,
                order_discount_type AS OrderDiscountType, order_discount_value AS OrderDiscountValue
            FROM sales_orders
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft
            """;
        var header = await conn.QuerySingleOrDefaultAsync<DraftHeaderRow>(headerSql, new
        {
            Id = id,
            TenantId,
            Draft = SalesOrderStatuses.Draft,
        }, tx) ?? throw new InvalidOperationException("Đơn nháp không tồn tại.");

        await EnsureOpenShiftAsync(conn, tx, header.WarehouseId);
        var shiftId = await ResolveOpenShiftIdAsync(conn, tx, header.WarehouseId);

        var syncFromRequest = request?.Items is { Count: > 0 };
        IReadOnlyList<CreateSaleLineRequest> saleItems;
        if (syncFromRequest)
        {
            saleItems = request!.Items!;
        }
        else
        {
            const string draftItemsSql = """
                SELECT
                    product_id AS ProductId, product_unit_id AS ProductUnitId, quantity AS Quantity,
                    discount_type AS DiscountType, discount_value AS DiscountValue,
                    prescription_line_id AS PrescriptionLineId
                FROM sales_order_items WHERE sales_order_id = @OrderId
                """;
            saleItems = (await conn.QueryAsync<DraftSaleLineRow>(draftItemsSql, new { OrderId = id }, tx))
                .Select(row => new CreateSaleLineRequest(
                    row.ProductId,
                    row.ProductUnitId,
                    row.Quantity,
                    row.DiscountType,
                    row.DiscountValue,
                    null,
                    row.PrescriptionLineId))
                .ToList();
        }

        if (saleItems.Count == 0)
            throw new InvalidOperationException("Đơn nháp không có dòng bán.");

        await conn.ExecuteAsync("DELETE FROM sales_order_items WHERE sales_order_id = @OrderId", new { OrderId = id }, tx);

        var orderDiscount = syncFromRequest
            ? new SaleDiscountInput(request!.OrderDiscountType, request.OrderDiscountValue)
            : new SaleDiscountInput(header.OrderDiscountType, header.OrderDiscountValue);
        var pricedInputs = await ResolveSaleLineInputsAsync(conn, tx, saleItems, header.PriceType, cancellationToken);
        var pricing = _pricing.PriceSaleOrder(pricedInputs, orderDiscount);
        await EnforceSaleDiscountPolicyAsync(
            pricing,
            discountPolicy,
            request?.DiscountOverrideWorkflowTaskId,
            salesOrderId: id,
            cancellationToken: cancellationToken);
        var customerId = syncFromRequest ? request!.CustomerId : header.CustomerId;
        var notes = syncFromRequest ? request!.Notes : null;

        var branchId = await conn.QuerySingleAsync<Guid>(
            "SELECT branch_id FROM warehouses WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL",
            new { Id = header.WarehouseId, TenantId }, tx);
        var rxSettings = await _tenantSettings.GetRxSettingsAsync(cancellationToken);
        await RxDispensingEnforcer.EnforceStrictSaleAsync(
            conn,
            tx,
            TenantId,
            saleItems.ToList(),
            request?.PrescriptionId,
            rxSettings,
            userId: null,
            header.WarehouseId,
            branchId,
            "pos_checkout",
            cancellationToken,
            request?.ConnectRxHandoffId);

        var voucher = await _voucherPos.ResolveVoucherAsync(
            TenantId,
            customerId,
            request?.CustomerVoucherId,
            pricing.TotalAmount,
            conn,
            tx,
            cancellationToken);

        var redeem = await _loyaltyPos.ResolveRedeemAsync(
            TenantId,
            customerId,
            request?.LoyaltyPointsToRedeem,
            request?.LoyaltyDiscountAmount,
            voucher.OrderTotalAfterVoucher,
            conn,
            tx,
            cancellationToken);

        var paymentResolution = await NormalizeAndValidatePaymentsAsync(
            conn, tx, customerId, request?.Payments, redeem.FinalTotal, cancellationToken);
        var linePlans = await BuildFifoPlansAsync(
            conn,
            tx,
            header.WarehouseId,
            pricing.Lines,
            pricedInputs,
            cancellationToken);
        var orderReminder = SalesOrderReminderNormalizer.Normalize(
            request?.OrderReminderLabel,
            request?.OrderReminderDaysSupply);

        await conn.ExecuteAsync("""
            UPDATE sales_orders SET
                status = @Completed, customer_id = @CustomerId, subtotal = @Subtotal,
                discount_amount = @OrderDiscountAmount, order_discount_type = @OrderDiscountType,
                order_discount_value = @OrderDiscountValue, total_amount = @TotalAmount,
                amount_paid = @AmountPaid, outstanding = @Outstanding,
                voucher_id = @VoucherId,
                voucher_discount_amount = @VoucherDiscountAmount,
                loyalty_points_redeemed = @LoyaltyPointsRedeemed,
                loyalty_discount_amount = @LoyaltyDiscountAmount,
                sales_shift_id = @ShiftId, notes = COALESCE(@Notes, notes),
                reminder_label = @ReminderLabel,
                reminder_days_supply = @ReminderDaysSupply,
                prescription_id = @PrescriptionId,
                connect_rx_handoff_id = COALESCE(@ConnectRxHandoffId, connect_rx_handoff_id)
            WHERE id = @Id AND tenant_id = @TenantId
            """, new
        {
            Id = id,
            TenantId,
            CustomerId = customerId,
            Completed = SalesOrderStatuses.Completed,
            Subtotal = pricing.SubtotalGross,
            OrderDiscountAmount = pricing.OrderDiscountAmount,
            OrderDiscountType = orderDiscount.DiscountType,
            OrderDiscountValue = orderDiscount.DiscountValue ?? 0,
            TotalAmount = redeem.FinalTotal,
            AmountPaid = paymentResolution.AmountPaid,
            Outstanding = paymentResolution.Outstanding,
            VoucherId = voucher.VoucherId,
            VoucherDiscountAmount = voucher.DiscountAmount,
            LoyaltyPointsRedeemed = redeem.PointsRedeemed,
            LoyaltyDiscountAmount = redeem.DiscountAmount,
            ShiftId = shiftId,
            Notes = notes,
            ReminderLabel = orderReminder.Label,
            ReminderDaysSupply = orderReminder.DaysSupply,
            PrescriptionId = request?.PrescriptionId,
            ConnectRxHandoffId = request?.ConnectRxHandoffId,
        }, tx);

        var insertedLines = await InsertCompletedLinesAsync(
            conn,
            tx,
            id,
            header.WarehouseId,
            linePlans,
            cancellationToken);
        await InsertPaymentsAsync(conn, tx, id, paymentResolution.CashPayments);

        if (request?.PrescriptionId is Guid prescriptionId)
        {
            var dispenseItems = insertedLines
                .Where(x => x.PrescriptionLineId.HasValue)
                .Select(x => new PrescriptionDispenseSaleItem(
                    x.SalesOrderItemId,
                    x.PrescriptionLineId!.Value,
                    x.Quantity))
                .ToList();
            await _prescriptions.ApplyDispenseFromSaleAsync(
                conn,
                tx,
                prescriptionId,
                id,
                dispenseItems,
                _tenant.UserId,
                cancellationToken);
        }

        await TryMarkConnectHandoffConsumedAsync(
            conn,
            tx,
            request?.ConnectRxHandoffId,
            _tenant.UserId == Guid.Empty ? null : _tenant.UserId);

        var orderNumber = await conn.QuerySingleAsync<string>(
            "SELECT order_number FROM sales_orders WHERE id = @Id",
            new { Id = id }, tx);

        if (voucher.VoucherId is Guid voucherId && voucher.CustomerVoucherId is Guid customerVoucherId)
        {
            await _voucherPos.TryMarkUsedAsync(customerVoucherId, voucherId, id, conn, tx);
        }

        if (redeem.PointsRedeemed > 0 && customerId is Guid redeemCustomer)
        {
            var programId = await _loyaltyPos.GetDefaultProgramIdAsync(TenantId, conn, tx, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy chương trình tích điểm.");
            await _loyaltyPos.TryRedeemForCompletedSaleAsync(
                TenantId,
                redeemCustomer,
                programId,
                id,
                orderNumber,
                redeem.PointsRedeemed,
                conn,
                tx,
                cancellationToken);
        }

        await _loyaltyPos.TryEarnForCompletedSaleAsync(
            TenantId,
            customerId,
            id,
            orderNumber,
            paymentResolution.AmountPaid,
            conn,
            tx,
            cancellationToken);

        await _outbox.WriteAsync(
            conn, tx,
            IntegrationOutboxEventTypes.OrderCompleted,
            IntegrationOutboxAggregateTypes.SalesOrder,
            id,
            new
            {
                orderId = id,
                orderNumber,
                warehouseId = header.WarehouseId,
                customerId,
                totalAmount = redeem.FinalTotal,
                loyaltyPointsRedeemed = redeem.PointsRedeemed,
                loyaltyDiscountAmount = redeem.DiscountAmount,
                status = SalesOrderStatuses.Completed,
                salesShiftId = shiftId,
            },
            _tenant.UserId,
            cancellationToken);

        await WriteSalesOrderCompletedPlatformEventAsync(
            conn,
            tx,
            id,
            orderNumber,
            header.WarehouseId,
            customerId,
            redeem.FinalTotal,
            redeem.PointsRedeemed,
            redeem.DiscountAmount,
            shiftId,
            _tenant.UserId,
            cancellationToken);

        await PharmacySalesOrderWriter.EnsureCompletedSaleAsync(
            conn,
            tx,
            TenantId,
            _tenant.WorkspaceId,
            id,
            orderNumber,
            customerId,
            SalesOrderStatuses.Completed,
            redeem.FinalTotal,
            DateTime.UtcNow,
            _tenant.UserId,
            cancellationToken);

        await TryUpsertRepurchaseSuggestionAsync(conn, tx, id);
        await TryAutoCreateDrinkRemindersFromOrderAsync(conn, tx, id);

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelDraftSaleAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE sales_orders SET status = @Cancelled
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            Draft = SalesOrderStatuses.Draft,
            Cancelled = SalesOrderStatuses.Cancelled,
        }) > 0;
    }

    public async Task<Guid> CreateSaleReturnAsync(
        Guid salesOrderId,
        CreateSaleReturnRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng trả.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string orderSql = """
            SELECT id AS Id, warehouse_id AS WarehouseId, status AS Status,
                   discount_amount AS OrderDiscountAmount,
                   total_amount AS TotalAmount, amount_paid AS AmountPaid, outstanding AS Outstanding
            FROM sales_orders WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var order = await conn.QuerySingleOrDefaultAsync<ReturnOrderRow>(orderSql, new { Id = salesOrderId, TenantId }, tx)
            ?? throw new InvalidOperationException("Đơn bán không tồn tại.");
        if (order.Status is not (SalesOrderStatuses.Completed or SalesOrderStatuses.Refunded))
            throw new InvalidOperationException("Chỉ trả hàng trên đơn đã hoàn tất.");

        await EnsureOpenShiftAsync(conn, tx, order.WarehouseId);
        var shiftId = await ResolveOpenShiftIdAsync(conn, tx, order.WarehouseId);

        var merchandiseNet = await conn.QuerySingleAsync<decimal>(
            "SELECT COALESCE(SUM(line_total), 0) FROM sales_order_items WHERE sales_order_id = @OrderId",
            new { OrderId = salesOrderId }, tx);

        var returnNumber = await _inventory.NextDocumentNumberAsync(conn, tx, "SR", "sales_returns", cancellationToken);
        decimal totalRefund = 0;

        const string returnInsert = """
            INSERT INTO sales_returns (tenant_id, return_number, sales_order_id, reason, status, sales_shift_id)
            VALUES (@TenantId, @ReturnNumber, @SalesOrderId, @Reason, @Status, @ShiftId)
            RETURNING id
            """;
        var returnId = await conn.QuerySingleAsync<Guid>(returnInsert, new
        {
            TenantId,
            ReturnNumber = returnNumber,
            SalesOrderId = salesOrderId,
            request.Reason,
            Status = SalesReturnStatuses.Completed,
            ShiftId = shiftId,
        }, tx);

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Số lượng trả phải lớn hơn 0.");

            const string itemSql = """
                SELECT
                    i.id AS Id, i.product_id AS ProductId, i.batch_id AS BatchId,
                    i.quantity AS SoldQuantity, i.unit_price AS UnitPrice, i.line_total AS LineTotal,
                    COALESCE((
                        SELECT SUM(ri.quantity)
                        FROM sales_return_items ri
                        INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                        WHERE ri.sales_order_item_id = i.id AND r.status = @ReturnCompleted
                    ), 0) AS ReturnedQuantity
                FROM sales_order_items i
                WHERE i.id = @ItemId AND i.sales_order_id = @OrderId AND i.batch_id IS NOT NULL
                """;
            var item = await conn.QuerySingleOrDefaultAsync<ReturnableItemRow>(itemSql, new
            {
                ItemId = line.SalesOrderItemId,
                OrderId = salesOrderId,
                ReturnCompleted = SalesReturnStatuses.Completed,
            }, tx) ?? throw new InvalidOperationException("Dòng đơn không hợp lệ để trả.");

            var remaining = item.SoldQuantity - item.ReturnedQuantity;
            if (line.Quantity > remaining + 0.0001m)
                throw new InvalidOperationException("Số lượng trả vượt quá đã bán.");

            var conversionFactor = await conn.QuerySingleAsync<decimal>(
                """
                SELECT u.conversion_factor FROM sales_order_items i
                INNER JOIN product_units u ON u.id = i.product_unit_id
                WHERE i.id = @ItemId
                """,
                new { ItemId = line.SalesOrderItemId }, tx);

            var baseReturnQty = line.Quantity * conversionFactor;

            var unitCost = await conn.QuerySingleAsync<decimal>(
                "SELECT unit_cost FROM inventory_batches WHERE id = @BatchId AND tenant_id = @TenantId",
                new { item.BatchId, TenantId }, tx);

            var refundAmount = _pricing.ComputeSaleLineRefundAmount(
                item.LineTotal,
                item.SoldQuantity,
                line.Quantity,
                merchandiseNet,
                order.OrderDiscountAmount);
            totalRefund += refundAmount;

            const string returnItemSql = """
                INSERT INTO sales_return_items (
                    sales_return_id, sales_order_item_id, batch_id, quantity, refund_amount
                )
                VALUES (@ReturnId, @SalesOrderItemId, @BatchId, @Quantity, @RefundAmount)
                """;
            await conn.ExecuteAsync(returnItemSql, new
            {
                ReturnId = returnId,
                line.SalesOrderItemId,
                item.BatchId,
                line.Quantity,
                RefundAmount = refundAmount,
            }, tx);

            await _inventory.IncreaseBatchQuantityAsync(conn, tx, item.BatchId, baseReturnQty, cancellationToken);
            await _inventory.InsertMovementAsync(
                conn, tx, order.WarehouseId, item.BatchId, item.ProductId,
                StockMovementTypes.In, StockReferenceTypes.SalesReturn, returnId,
                baseReturnQty, unitCost, null, cancellationToken);
        }

        var refundPayments = BuildReturnCashPayments(request.Payments, totalRefund, order.Outstanding, order.AmountPaid);
        await InsertReturnPaymentsAsync(conn, tx, returnId, refundPayments);

        await ApplySaleReturnFinancialAdjustmentAsync(
            conn, tx, salesOrderId, TenantId, totalRefund, order.Outstanding, order.AmountPaid);

        var allReturned = await conn.QuerySingleAsync<bool>("""
            SELECT NOT EXISTS (
                SELECT 1 FROM sales_order_items i
                WHERE i.sales_order_id = @OrderId AND i.batch_id IS NOT NULL
                  AND i.quantity > COALESCE((
                      SELECT SUM(ri.quantity)
                      FROM sales_return_items ri
                      INNER JOIN sales_returns r ON r.id = ri.sales_return_id
                      WHERE ri.sales_order_item_id = i.id AND r.status = @ReturnCompleted
                  ), 0) + 0.0001
            )
            """, new { OrderId = salesOrderId, ReturnCompleted = SalesReturnStatuses.Completed }, tx);

        if (allReturned)
        {
            await conn.ExecuteAsync(
                "UPDATE sales_orders SET status = @Refunded WHERE id = @Id AND tenant_id = @TenantId",
                new { Id = salesOrderId, TenantId, Refunded = SalesOrderStatuses.Refunded }, tx);
        }

        await _outbox.WriteAsync(
            conn, tx,
            IntegrationOutboxEventTypes.SalesReturnCompleted,
            IntegrationOutboxAggregateTypes.SalesReturn,
            returnId,
            new
            {
                returnId,
                returnNumber,
                salesOrderId,
                totalRefund,
                salesShiftId = shiftId,
            },
            _tenant.UserId,
            cancellationToken);

        await _platformEvents.WriteAsync(
            conn,
            tx,
            PlatformEventTypes.SalesReturnCompleted,
            PlatformEventAggregateTypes.SalesReturn,
            returnId,
            new
            {
                returnId,
                returnNumber,
                salesOrderId,
                totalRefund,
                salesShiftId = shiftId,
            },
            _tenant.UserId,
            cancellationToken: cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return returnId;
    }

    public async Task<SalesReturnDetailDto?> GetSaleReturnAsync(Guid id, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                r.id AS Id, r.return_number AS ReturnNumber, r.sales_order_id AS SalesOrderId,
                o.order_number AS OrderNumber, r.return_date AS ReturnDate, r.status AS Status,
                r.reason AS Reason,
                r.sales_shift_id AS SalesShiftId, sh.shift_number AS ShiftNumber
            FROM sales_returns r
            INNER JOIN sales_orders o ON o.id = r.sales_order_id
            LEFT JOIN sales_shifts sh ON sh.id = r.sales_shift_id
            WHERE r.id = @Id AND r.tenant_id = @TenantId
            """;
        const string itemsSql = """
            SELECT
                ri.id AS Id, ri.sales_order_item_id AS SalesOrderItemId,
                p.product_code AS ProductCode, p.product_name AS ProductName,
                b.batch_number AS BatchNumber, ri.quantity AS Quantity, ri.refund_amount AS RefundAmount
            FROM sales_return_items ri
            INNER JOIN sales_order_items i ON i.id = ri.sales_order_item_id
            INNER JOIN products p ON p.id = i.product_id
            INNER JOIN inventory_batches b ON b.id = ri.batch_id
            WHERE ri.sales_return_id = @ReturnId
            ORDER BY p.product_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<ReturnHeaderRow>(headerSql, new { Id = id, TenantId });
        if (header is null) return null;
        var items = (await conn.QueryAsync<SalesReturnItemDto>(itemsSql, new { ReturnId = id })).ToList();
        const string paymentsSql = """
            SELECT id AS Id, payment_method AS PaymentMethod, amount AS Amount, paid_at AS PaidAt
            FROM sales_return_payments WHERE sales_return_id = @ReturnId
            ORDER BY paid_at
            """;
        var payments = (await conn.QueryAsync<SalesPaymentDto>(paymentsSql, new { ReturnId = id })).ToList();
        return new SalesReturnDetailDto(
            header.Id, header.ReturnNumber, header.SalesOrderId, header.OrderNumber,
            header.ReturnDate, header.Status, header.Reason,
            items.Sum(i => i.RefundAmount), items, payments,
            header.SalesShiftId, header.ShiftNumber);
    }

    public async Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsAsync(
        int limit,
        string? search,
        string? customerSearch,
        string? documentSearch,
        CancellationToken cancellationToken)
    {
        var searchFilter = BuildSalesReturnSearchFilter(search, customerSearch, documentSearch);
        var sql = $"""
            SELECT
                r.id AS Id, r.return_number AS ReturnNumber, r.sales_order_id AS SalesOrderId,
                o.order_number AS OrderNumber, r.return_date AS ReturnDate, r.status AS Status,
                COALESCE(t.total_refund, 0) AS TotalRefund,
                r.sales_shift_id AS SalesShiftId, sh.shift_number AS ShiftNumber
            FROM sales_returns r
            INNER JOIN sales_orders o ON o.id = r.sales_order_id
            LEFT JOIN customers c ON c.id = o.customer_id
            LEFT JOIN sales_shifts sh ON sh.id = r.sales_shift_id
            LEFT JOIN (
                SELECT sales_return_id, SUM(refund_amount) AS total_refund
                FROM sales_return_items
                GROUP BY sales_return_id
            ) t ON t.sales_return_id = r.id
            WHERE r.tenant_id = @TenantId
              {searchFilter}
            ORDER BY r.return_date DESC
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        var customerPattern = string.IsNullOrWhiteSpace(customerSearch) ? null : $"%{customerSearch.Trim()}%";
        var documentPattern = string.IsNullOrWhiteSpace(documentSearch) ? null : $"%{documentSearch.Trim()}%";
        var searchDigits = ExtractPhoneDigits(search);
        var customerSearchDigits = ExtractPhoneDigits(customerSearch);
        var rows = await conn.QueryAsync<ReturnListRow>(sql, new
        {
            TenantId,
            Search = pattern is null ? null : search!.Trim(),
            SearchPattern = pattern,
            SearchDigits = searchDigits,
            SearchDigitsPattern = BuildPhoneDigitsPattern(searchDigits),
            CustomerSearchPattern = customerPattern,
            CustomerSearchDigits = customerSearchDigits,
            CustomerSearchDigitsPattern = BuildPhoneDigitsPattern(customerSearchDigits),
            DocumentSearchPattern = documentPattern,
            Limit = Math.Clamp(limit, 1, 200),
        });
        return rows.Select(r => new SalesReturnListItemDto(
            r.Id, r.ReturnNumber, r.SalesOrderId, r.OrderNumber,
            r.ReturnDate, r.Status, r.TotalRefund, r.SalesShiftId, r.ShiftNumber)).ToList();
    }

    public async Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsByOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id, r.return_number AS ReturnNumber, r.sales_order_id AS SalesOrderId,
                o.order_number AS OrderNumber, r.return_date AS ReturnDate, r.status AS Status,
                COALESCE(t.total_refund, 0) AS TotalRefund,
                r.sales_shift_id AS SalesShiftId, sh.shift_number AS ShiftNumber
            FROM sales_returns r
            INNER JOIN sales_orders o ON o.id = r.sales_order_id
            LEFT JOIN sales_shifts sh ON sh.id = r.sales_shift_id
            LEFT JOIN (
                SELECT sales_return_id, SUM(refund_amount) AS total_refund
                FROM sales_return_items
                GROUP BY sales_return_id
            ) t ON t.sales_return_id = r.id
            WHERE r.tenant_id = @TenantId AND r.sales_order_id = @SalesOrderId
            ORDER BY r.return_date DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ReturnListRow>(sql, new { TenantId, SalesOrderId = salesOrderId });
        return rows.Select(r => new SalesReturnListItemDto(
            r.Id, r.ReturnNumber, r.SalesOrderId, r.OrderNumber,
            r.ReturnDate, r.Status, r.TotalRefund, r.SalesShiftId, r.ShiftNumber)).ToList();
    }

    public async Task<SalesShiftSummaryDto> GetShiftSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        if (from >= to)
            throw new InvalidOperationException("Khoảng thời gian không hợp lệ.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await BuildShiftSummaryAsync(conn, from, to, openingCash: 0, closingCash: null);
    }

    public async Task<IReadOnlyList<SalesShiftListItemDto>> GetShiftsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.id AS Id, s.shift_number AS ShiftNumber, s.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                COALESCE(e.full_name, u.username) AS OpenedByUserName,
                s.opened_at AS OpenedAt, s.closed_at AS ClosedAt,
                s.opening_cash AS OpeningCash, s.closing_cash AS ClosingCash,
                s.cash_variance AS CashVariance, s.status AS Status
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id
            INNER JOIN users u ON u.id = s.opened_by
            LEFT JOIN employees e ON e.id = u.employee_id
            WHERE s.tenant_id = @TenantId
            ORDER BY s.opened_at DESC
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<SalesShiftListItemDto>(sql, new { TenantId, Limit = limit })).ToList();
    }

    public async Task<SalesShiftDetailDto?> GetOpenShiftAsync(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var shiftId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT id FROM sales_shifts
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND status = @Open
            """,
            new { TenantId, WarehouseId = warehouseId, Open = SalesShiftStatuses.Open });
        if (shiftId is null)
            return null;

        var detail = await GetShiftAsync(conn, shiftId.Value);
        if (detail is not null)
            return detail;

        // Retry on a fresh connection (avoids rare null after concurrent commit).
        return await GetShiftAsync(shiftId.Value, cancellationToken);
    }

    public async Task<SalesShiftDetailDto?> GetShiftAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await GetShiftAsync(conn, id);
    }

    public async Task<SalesShiftDetailDto> OpenShiftAsync(
        OpenSalesShiftRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.OpeningCash < 0)
            throw new InvalidOperationException("Quỹ đầu ca không được âm.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await ResolveSaleContextAsync(conn, tx, request.WarehouseId, null, userId);

        var hasOpen = await conn.QuerySingleAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM sales_shifts
                WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND status = @Open
            )
            """,
            new { TenantId, WarehouseId = request.WarehouseId, Open = SalesShiftStatuses.Open }, tx);
        if (hasOpen)
        {
            await tx.CommitAsync(cancellationToken);
            var existing = await GetOpenShiftAsync(request.WarehouseId, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Kho này đang có ca mở nhưng không tải được chi tiết. Hãy tải lại trang.");
            return existing;
        }

        var shiftNumber = await _inventory.NextDocumentNumberAsync(conn, tx, "SH", "sales_shifts", cancellationToken);
        var shiftId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO sales_shifts (
                tenant_id, warehouse_id, opened_by, shift_number, opening_cash, status
            )
            VALUES (@TenantId, @WarehouseId, @OpenedBy, @ShiftNumber, @OpeningCash, @Open)
            RETURNING id
            """,
            new
            {
                TenantId,
                request.WarehouseId,
                OpenedBy = userId,
                ShiftNumber = shiftNumber,
                request.OpeningCash,
                Open = SalesShiftStatuses.Open,
            }, tx);

        await tx.CommitAsync(cancellationToken);
        return await GetShiftAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException(
                "Ca đã mở nhưng không tải được chi tiết. Hãy tải lại trang.");
    }

    public async Task<SalesShiftDetailDto> CloseShiftAsync(
        Guid id,
        CloseSalesShiftRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (request.ClosingCash < 0)
            throw new InvalidOperationException("Tiền đếm cuối ca không được âm.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var shift = await conn.QuerySingleOrDefaultAsync<ShiftCloseRow>(
            """
            SELECT id AS Id, opened_at AS OpenedAt, opening_cash AS OpeningCash, status AS Status
            FROM sales_shifts
            WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE
            """,
            new { Id = id, TenantId }, tx)
            ?? throw new InvalidOperationException("Ca làm việc không tồn tại.");

        if (shift.Status != SalesShiftStatuses.Open)
            throw new InvalidOperationException("Ca đã được đóng.");

        var summary = await BuildShiftSummaryAsync(
            conn, shift.OpenedAt, DateTime.UtcNow, shift.OpeningCash, request.ClosingCash);
        var variance = request.ClosingCash - summary.ExpectedCash;

        await conn.ExecuteAsync(
            """
            UPDATE sales_shifts SET
                status = @Closed, closed_at = NOW(), closed_by = @ClosedBy,
                closing_cash = @ClosingCash, expected_cash = @ExpectedCash,
                cash_variance = @CashVariance, close_notes = @CloseNotes
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new
            {
                Id = id,
                TenantId,
                Closed = SalesShiftStatuses.Closed,
                ClosedBy = userId,
                request.ClosingCash,
                ExpectedCash = summary.ExpectedCash,
                CashVariance = variance,
                request.CloseNotes,
            }, tx);

        await tx.CommitAsync(cancellationToken);
        return (await GetShiftAsync(id, cancellationToken))!;
    }

    private async Task<SalesShiftDetailDto?> GetShiftAsync(IDbConnection conn, Guid id)
    {
        const string sql = """
            SELECT
                s.id AS Id, s.shift_number AS ShiftNumber, s.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                COALESCE(eo.full_name, uo.username) AS OpenedByUserName,
                COALESCE(ec.full_name, uc.username) AS ClosedByUserName,
                s.opened_at AS OpenedAt, s.closed_at AS ClosedAt,
                s.opening_cash AS OpeningCash, s.closing_cash AS ClosingCash,
                s.expected_cash AS ExpectedCash, s.cash_variance AS CashVariance,
                s.status AS Status, s.close_notes AS CloseNotes
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id
            INNER JOIN users uo ON uo.id = s.opened_by
            LEFT JOIN employees eo ON eo.id = uo.employee_id
            LEFT JOIN users uc ON uc.id = s.closed_by
            LEFT JOIN employees ec ON ec.id = uc.employee_id
            WHERE s.id = @Id AND s.tenant_id = @TenantId
            """;
        var header = await conn.QuerySingleOrDefaultAsync<ShiftHeaderRow>(sql, new { Id = id, TenantId });
        if (header is null) return null;

        var end = header.ClosedAt ?? DateTime.UtcNow;
        var summary = await BuildShiftSummaryAsync(
            conn, header.OpenedAt, end, header.OpeningCash, header.ClosingCash);
        var lotAlerts = await GetShiftLotComplianceAlertsAsync(conn, id);

        return new SalesShiftDetailDto(
            header.Id, header.ShiftNumber, header.WarehouseId, header.WarehouseName,
            header.OpenedByUserName, header.ClosedByUserName,
            header.OpenedAt, header.ClosedAt,
            header.OpeningCash, header.ClosingCash, header.ExpectedCash, header.CashVariance,
            header.Status, header.CloseNotes, summary, lotAlerts);
    }

    private async Task<SalesShiftSummaryDto> BuildShiftSummaryAsync(
        IDbConnection conn,
        DateTime from,
        DateTime to,
        decimal openingCash,
        decimal? closingCash)
    {
        const string salesSql = """
            SELECT sp.payment_method AS PaymentMethod, COALESCE(SUM(sp.amount), 0) AS Amount
            FROM sales_payments sp
            INNER JOIN sales_orders o ON o.id = sp.sales_order_id
            WHERE o.tenant_id = @TenantId
              AND sp.paid_at >= @From AND sp.paid_at < @To
            GROUP BY sp.payment_method
            """;
        const string refundSql = """
            SELECT rp.payment_method AS PaymentMethod, COALESCE(SUM(rp.amount), 0) AS Amount
            FROM sales_return_payments rp
            INNER JOIN sales_returns r ON r.id = rp.sales_return_id
            WHERE r.tenant_id = @TenantId
              AND rp.paid_at >= @From AND rp.paid_at < @To
            GROUP BY rp.payment_method
            """;

        var salesRows = (await conn.QueryAsync<ShiftAmountRow>(salesSql, new { TenantId, From = from, To = to })).ToList();
        var refundRows = (await conn.QueryAsync<ShiftAmountRow>(refundSql, new { TenantId, From = from, To = to })).ToList();

        var salesMap = salesRows.ToDictionary(r => r.PaymentMethod, r => r.Amount);
        var refundMap = refundRows.ToDictionary(r => r.PaymentMethod, r => r.Amount);
        var methods = salesMap.Keys.Union(refundMap.Keys).OrderBy(m => m).ToList();

        var byMethod = methods.Select(method =>
        {
            var sales = salesMap.GetValueOrDefault(method);
            var refunds = refundMap.GetValueOrDefault(method);
            return new SalesShiftPaymentSummaryDto(method, sales, refunds, sales - refunds);
        }).ToList();

        var totalSales = byMethod.Sum(x => x.SalesAmount);
        var totalRefunds = byMethod.Sum(x => x.RefundAmount);
        var cashSales = byMethod.FirstOrDefault(x => x.PaymentMethod == SalesPaymentMethods.Cash)?.SalesAmount ?? 0;
        var cashRefunds = byMethod.FirstOrDefault(x => x.PaymentMethod == SalesPaymentMethods.Cash)?.RefundAmount ?? 0;
        var expectedCash = openingCash + cashSales - cashRefunds;
        decimal? variance = closingCash.HasValue ? closingCash.Value - expectedCash : null;

        return new SalesShiftSummaryDto(
            from, to, totalSales, totalRefunds, totalSales - totalRefunds, byMethod,
            openingCash, cashSales, cashRefunds, expectedCash, closingCash, variance);
    }

    private async Task EnsureOpenShiftAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId)
    {
        var hasOpen = await conn.QuerySingleAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM sales_shifts
                WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND status = @Open
            )
            """,
            new { TenantId, WarehouseId = warehouseId, Open = SalesShiftStatuses.Open }, tx);
        if (!hasOpen)
            throw new InvalidOperationException("Chưa mở ca cho kho này. Vui lòng mở ca trước khi bán hoặc hoàn tiền.");
    }

    private static async Task<Guid> ResolveOpenShiftIdAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid tenantId)
    {
        return await conn.QuerySingleAsync<Guid>(
            """
            SELECT id FROM sales_shifts
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND status = @Open
            """,
            new { TenantId = tenantId, WarehouseId = warehouseId, Open = SalesShiftStatuses.Open }, tx);
    }

    private Task<Guid> ResolveOpenShiftIdAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId) =>
        ResolveOpenShiftIdAsync(conn, tx, warehouseId, TenantId);

    private async Task<List<ShiftLotComplianceAlertDto>> GetShiftLotComplianceAlertsAsync(
        IDbConnection conn,
        Guid shiftId)
    {
        var batchMode = await _tenantSettings.GetBatchModeAsync();
        if (!TenantBatchModeCompliance.EnablesShiftLotComplianceAlerts(batchMode))
            return [];

        const string sql = """
            SELECT DISTINCT
                oi.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                b_sold.batch_number AS SoldBatchNumber,
                b_sold.expiry_date AS SoldExpiryDate,
                b_earlier.batch_number AS EarlierBatchNumber,
                b_earlier.expiry_date AS EarlierExpiryDate,
                b_earlier.quantity_available AS EarlierBookQuantity
            FROM sales_orders o
            INNER JOIN sales_order_items oi ON oi.sales_order_id = o.id
            INNER JOIN inventory_batches b_sold ON b_sold.id = oi.batch_id
            INNER JOIN products p ON p.id = oi.product_id
            INNER JOIN inventory_batches b_earlier
                ON b_earlier.product_id = oi.product_id
               AND b_earlier.warehouse_id = o.warehouse_id
               AND b_earlier.tenant_id = o.tenant_id
               AND b_earlier.quantity_available > 0
               AND b_earlier.id <> b_sold.id
               AND (
                    b_earlier.expiry_date < b_sold.expiry_date
                    OR (b_earlier.expiry_date IS NOT DISTINCT FROM b_sold.expiry_date
                        AND b_earlier.created_at < b_sold.created_at)
                    OR (b_earlier.expiry_date IS NULL AND b_sold.expiry_date IS NOT NULL)
               )
            WHERE o.sales_shift_id = @ShiftId
              AND o.tenant_id = @TenantId
              AND o.status IN (@Completed, @Refunded)
              AND oi.batch_id IS NOT NULL
            ORDER BY p.product_name, b_sold.batch_number
            """;

        var rows = await conn.QueryAsync<LotAlertRow>(sql, new
        {
            ShiftId = shiftId,
            TenantId,
            Completed = SalesOrderStatuses.Completed,
            Refunded = SalesOrderStatuses.Refunded,
        });

        return rows
            .Select(r => new ShiftLotComplianceAlertDto(
                r.ProductId, r.ProductCode, r.ProductName,
                r.SoldBatchNumber, r.SoldExpiryDate,
                r.EarlierBatchNumber, r.EarlierExpiryDate, r.EarlierBookQuantity,
                StockSourceLabels.SystemBook))
            .ToList();
    }

    private async Task<List<(CreateSaleLineRequest Request, decimal UnitPrice, decimal ConversionFactor)>> ResolveSaleLineInputsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        IReadOnlyList<CreateSaleLineRequest> items,
        short priceType,
        CancellationToken cancellationToken)
    {
        var result = new List<(CreateSaleLineRequest, decimal, decimal)>();
        foreach (var line in items)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Số lượng bán phải lớn hơn 0.");

            const string unitSql = """
                SELECT conversion_factor AS ConversionFactor
                FROM product_units
                WHERE id = @UnitId AND product_id = @ProductId AND tenant_id = @TenantId
                """;
            var unit = await conn.QuerySingleOrDefaultAsync<UnitRow>(
                unitSql, new { UnitId = line.ProductUnitId, ProductId = line.ProductId, TenantId }, tx)
                ?? throw new InvalidOperationException("Đơn vị tính không hợp lệ.");

            const string priceSql = """
                SELECT price FROM product_prices
                WHERE tenant_id = @TenantId AND product_id = @ProductId AND product_unit_id = @ProductUnitId
                  AND price_type = @PriceType AND status = 1
                  AND effective_from <= NOW() AND (effective_to IS NULL OR effective_to > NOW())
                ORDER BY effective_from DESC LIMIT 1
                """;
            var unitPrice = await conn.QuerySingleOrDefaultAsync<decimal?>(priceSql, new
            {
                TenantId,
                line.ProductId,
                line.ProductUnitId,
                PriceType = priceType,
            }, tx);
            if (unitPrice is null or <= 0)
                throw new InvalidOperationException($"Chưa có giá bán cho sản phẩm {line.ProductId}.");

            result.Add((line, unitPrice.Value, unit.ConversionFactor));
        }

        return result;
    }

    private sealed record SalePaymentResolution(
        List<CreateSalePaymentRequest> CashPayments,
        decimal AmountPaid,
        decimal Outstanding);

    private async Task<SalePaymentResolution> NormalizeAndValidatePaymentsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid? customerId,
        IReadOnlyList<CreateSalePaymentRequest>? payments,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        if (totalAmount <= 0.01m)
            return new SalePaymentResolution([new CreateSalePaymentRequest(SalesPaymentMethods.Cash, 0)], 0, 0);

        // payments omitted = thu đủ (POS/API mặc định). payments: [] = ghi nợ cả đơn (POS cash 0).
        if (payments is null)
        {
            return new SalePaymentResolution(
                [new CreateSalePaymentRequest(SalesPaymentMethods.Cash, totalAmount)],
                totalAmount,
                0);
        }

        var list = payments
            .Where(p => p.Amount > 0.01m && p.PaymentMethod != SalesPaymentMethods.Credit)
            .ToList();

        var paid = list.Sum(p => p.Amount);
        if (paid > totalAmount + 0.01m)
            throw new InvalidOperationException("Số tiền thu vượt tổng đơn.");

        var outstanding = Math.Round(Math.Max(0, totalAmount - paid), 2, MidpointRounding.AwayFromZero);
        if (outstanding > 0.01m)
        {
            await ValidateCustomerCreditAsync(conn, tx, customerId, outstanding, cancellationToken);
            return new SalePaymentResolution(list, Math.Round(paid, 2, MidpointRounding.AwayFromZero), outstanding);
        }

        if (list.Count == 0)
            list.Add(new CreateSalePaymentRequest(SalesPaymentMethods.Cash, totalAmount));
        else if (Math.Abs(paid - totalAmount) > 0.01m)
            throw new InvalidOperationException("Tổng thanh toán phải bằng tổng đơn.");

        return new SalePaymentResolution(list, totalAmount, 0);
    }

    private async Task ValidateCustomerCreditAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid? customerId,
        decimal newOutstanding,
        CancellationToken cancellationToken)
    {
        if (customerId is not Guid customer)
            throw new InvalidOperationException("Chọn khách hàng để ghi nợ.");

        var credit = await conn.QuerySingleOrDefaultAsync<CustomerCreditRow>(
            """
            SELECT allow_credit AS AllowCredit, credit_limit AS CreditLimit
            FROM customers
            WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { CustomerId = customer, TenantId },
            tx);

        if (credit is null)
            throw new InvalidOperationException("Khách hàng không tồn tại.");

        if (!credit.AllowCredit)
            throw new InvalidOperationException("Khách hàng chưa được phép ghi nợ.");

        if (credit.CreditLimit is > 0)
        {
            var currentOutstanding = await conn.ExecuteScalarAsync<decimal>(
                """
                SELECT COALESCE(SUM(outstanding), 0)
                FROM sales_orders
                WHERE tenant_id = @TenantId
                  AND customer_id = @CustomerId
                  AND status = @Completed
                  AND outstanding > 0
                """,
                new
                {
                    TenantId,
                    CustomerId = customer,
                    Completed = SalesOrderStatuses.Completed,
                },
                tx);

            if (currentOutstanding + newOutstanding > credit.CreditLimit + 0.01m)
            {
                throw new InvalidOperationException(
                    $"Vượt hạn mức nợ ({credit.CreditLimit:N0} đ). " +
                    $"Đang nợ {currentOutstanding:N0} đ, thêm {newOutstanding:N0} đ.");
            }
        }
    }

    private static List<CreateSalePaymentRequest> NormalizePayments(
        IReadOnlyList<CreateSalePaymentRequest>? payments,
        decimal totalAmount)
    {
        if (totalAmount <= 0.01m)
            return [new CreateSalePaymentRequest(SalesPaymentMethods.Cash, 0)];

        var list = payments?.Where(p => p.Amount > 0.01m).ToList() ?? [];
        if (list.Count == 0)
            list.Add(new CreateSalePaymentRequest(SalesPaymentMethods.Cash, totalAmount));
        else if (Math.Abs(list.Sum(p => p.Amount) - totalAmount) > 0.01m)
            throw new InvalidOperationException("Tổng thanh toán phải bằng tổng đơn.");

        return list;
    }

    private async Task<BatchLockRow?> FindLockedBatchByNumberAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid productId,
        string batchNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, quantity_available AS QuantityAvailable, unit_cost AS UnitCost
            FROM inventory_batches
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId
              AND product_id = @ProductId AND batch_number = @BatchNumber
              AND quantity_available > 0
            FOR UPDATE
            """;
        return await conn.QuerySingleOrDefaultAsync<BatchLockRow>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            ProductId = productId,
            BatchNumber = batchNumber,
        }, tx);
    }

    private async Task<List<PlannedSaleLine>> BuildFifoPlansAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        IReadOnlyList<PricedSaleLineResult> lines,
        IReadOnlyList<(CreateSaleLineRequest Request, decimal UnitPrice, decimal ConversionFactor)> resolvedInputs,
        CancellationToken cancellationToken)
    {
        if (lines.Count != resolvedInputs.Count)
            throw new InvalidOperationException("Không đồng bộ dữ liệu dòng bán.");

        var batchMode = await _tenantSettings.GetBatchModeAsync(cancellationToken);
        var linePlans = new List<PlannedSaleLine>();
        for (var index = 0; index < lines.Count; index++)
        {
            var pricedLine = lines[index];
            var requestLine = resolvedInputs[index].Request;
            var baseQtyNeeded = pricedLine.Quantity * pricedLine.ConversionFactor;
            var labelBatch = pricedLine.BatchNumber?.Trim();

            var useLabelScan = !string.IsNullOrWhiteSpace(labelBatch)
                && batchMode is TenantBatchMode.LabelOptional or TenantBatchMode.LabelRequired;

            IReadOnlyList<FifoAllocationResult> allocations;
            short batchSource;

            if (useLabelScan)
            {
                var batch = await FindLockedBatchByNumberAsync(
                    conn, tx, warehouseId, pricedLine.ProductId, labelBatch!, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Số lô \"{labelBatch}\" không có tồn khả dụng.");

                if (batch.QuantityAvailable + 0.0001m < baseQtyNeeded)
                {
                    var productCode = await conn.QuerySingleOrDefaultAsync<string>(
                        "SELECT product_code FROM products WHERE id = @Id AND tenant_id = @TenantId",
                        new { Id = pricedLine.ProductId, TenantId }, tx);
                    throw new InvalidOperationException(
                        $"Lô \"{labelBatch}\" không đủ tồn cho {productCode ?? pricedLine.ProductId.ToString()} " +
                        $"(cần {baseQtyNeeded:N0} đv cơ sở, còn {batch.QuantityAvailable:N0}).");
                }

                allocations = [new FifoAllocationResult(batch.Id, baseQtyNeeded, batch.UnitCost)];
                batchSource = SalesBatchSources.LabelScan;
            }
            else
            {
                try
                {
                    allocations = await _inventoryEngine.AllocateFifoAsync(
                        conn, tx, warehouseId, pricedLine.ProductId, baseQtyNeeded, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    var unit = await conn.QuerySingleOrDefaultAsync<ProductUnitRow>(
                        """
                        SELECT p.product_code AS ProductCode, p.product_name AS ProductName,
                               u.unit_name AS UnitName, u.conversion_factor AS ConversionFactor
                        FROM product_units u
                        INNER JOIN products p ON p.id = u.product_id
                        WHERE u.id = @UnitId AND u.tenant_id = @TenantId
                        """,
                        new { UnitId = pricedLine.ProductUnitId, TenantId }, tx);
                    var batches = await _inventoryEngine.GetAvailableBatchesAsync(
                        conn, warehouseId, pricedLine.ProductId, cancellationToken);
                    var availableSale = SumSaleStock(batches, pricedLine.ConversionFactor);
                    var code = unit?.ProductCode ?? pricedLine.ProductId.ToString();
                    var name = unit?.ProductName ?? "";
                    var unitName = unit?.UnitName ?? "đv";
                    throw new InvalidOperationException(
                        $"Không đủ tồn kho theo FEFO cho {code}" +
                        (string.IsNullOrEmpty(name) ? "" : $" — {name}") +
                        $" (cần {pricedLine.Quantity:N0} {unitName}, còn {availableSale:N0} {unitName}).");
                }

                batchSource = SalesBatchSources.FefoAuto;
            }

            decimal lineQtyRemaining = pricedLine.Quantity;
            foreach (var alloc in allocations)
            {
                var saleQty = alloc.BaseQuantity / pricedLine.ConversionFactor;
                var chunkGross = saleQty * pricedLine.UnitPrice;
                var chunkDiscount = pricedLine.Quantity > 0
                    ? pricedLine.DiscountAmount * (saleQty / pricedLine.Quantity)
                    : 0m;
                var chunkNet = chunkGross - chunkDiscount;
                linePlans.Add(new PlannedSaleLine(
                    pricedLine.ProductId,
                    pricedLine.ProductUnitId,
                    alloc.BatchId,
                    saleQty,
                    pricedLine.UnitPrice,
                    chunkNet,
                    chunkDiscount,
                    pricedLine.DiscountType,
                    pricedLine.DiscountValue,
                    alloc.UnitCost,
                    alloc.BaseQuantity,
                    batchSource,
                    requestLine.PrescriptionLineId));
                lineQtyRemaining -= saleQty;
            }

            if (lineQtyRemaining > 0.0001m)
            {
                var productLabel = await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT product_code FROM products WHERE id = @Id AND tenant_id = @TenantId",
                    new { Id = pricedLine.ProductId, TenantId }, tx);
                throw new InvalidOperationException(
                    $"Không đủ tồn kho theo FEFO cho {productLabel ?? pricedLine.ProductId.ToString()} " +
                    $"(cần {pricedLine.Quantity:N0} × hệ số {pricedLine.ConversionFactor:N0} = {pricedLine.Quantity * pricedLine.ConversionFactor:N0} đv cơ sở).");
            }
        }

        return linePlans;
    }

    private async Task<(Guid BranchId, Guid? EmployeeId)> ResolveSaleContextAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid? customerId,
        Guid userId)
    {
        var branchId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT branch_id FROM warehouses WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL",
            new { Id = warehouseId, TenantId }, tx)
            ?? throw new InvalidOperationException("Kho không tồn tại.");

        if (customerId is Guid cid)
        {
            var exists = await conn.QuerySingleAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM customers WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL)",
                new { Id = cid, TenantId }, tx);
            if (!exists) throw new InvalidOperationException("Khách hàng không tồn tại.");
        }

        var employeeId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT employee_id FROM users WHERE id = @UserId AND tenant_id = @TenantId",
            new { UserId = userId, TenantId }, tx);

        return (branchId, employeeId);
    }

    private async Task<Guid> InsertSaleHeaderAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string orderNumber,
        Guid branchId,
        Guid warehouseId,
        Guid? customerId,
        Guid? employeeId,
        short status,
        short priceType,
        decimal subtotalGross,
        decimal orderDiscountAmount,
        short? orderDiscountType,
        decimal orderDiscountValue,
        decimal totalAmount,
        decimal amountPaid,
        decimal outstanding,
        string? notes,
        Guid? salesShiftId = null,
        Guid? voucherId = null,
        decimal voucherDiscountAmount = 0,
        decimal loyaltyPointsRedeemed = 0,
        decimal loyaltyDiscountAmount = 0,
        string? reminderLabel = null,
        int? reminderDaysSupply = null,
        Guid? prescriptionId = null,
        Guid? connectRxHandoffId = null)
    {
        const string sql = """
            INSERT INTO sales_orders (
                tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
                status, price_type, subtotal, discount_amount, order_discount_type, order_discount_value,
                total_amount, amount_paid, outstanding, notes, sales_shift_id, voucher_id, voucher_discount_amount,
                loyalty_points_redeemed, loyalty_discount_amount, reminder_label, reminder_days_supply, prescription_id,
                connect_rx_handoff_id
            )
            VALUES (
                @TenantId, @OrderNumber, @BranchId, @WarehouseId, @CustomerId, @EmployeeId,
                @Status, @PriceType, @Subtotal, @OrderDiscountAmount, @OrderDiscountType, @OrderDiscountValue,
                @TotalAmount, @AmountPaid, @Outstanding, @Notes, @SalesShiftId, @VoucherId, @VoucherDiscountAmount,
                @LoyaltyPointsRedeemed, @LoyaltyDiscountAmount, @ReminderLabel, @ReminderDaysSupply, @PrescriptionId,
                @ConnectRxHandoffId
            )
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            OrderNumber = orderNumber,
            BranchId = branchId,
            WarehouseId = warehouseId,
            CustomerId = customerId,
            EmployeeId = employeeId,
            Status = status,
            PriceType = priceType,
            Subtotal = subtotalGross,
            OrderDiscountAmount = orderDiscountAmount,
            OrderDiscountType = orderDiscountType,
            OrderDiscountValue = orderDiscountValue,
            TotalAmount = totalAmount,
            AmountPaid = amountPaid,
            Outstanding = outstanding,
            Notes = notes,
            SalesShiftId = salesShiftId,
            VoucherId = voucherId,
            VoucherDiscountAmount = voucherDiscountAmount,
            LoyaltyPointsRedeemed = loyaltyPointsRedeemed,
            LoyaltyDiscountAmount = loyaltyDiscountAmount,
            ReminderLabel = reminderLabel,
            ReminderDaysSupply = reminderDaysSupply,
            PrescriptionId = prescriptionId,
            ConnectRxHandoffId = connectRxHandoffId,
        }, tx);
    }

    private async Task TryMarkConnectHandoffConsumedAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid? connectRxHandoffId,
        Guid? actorUserId)
    {
        if (connectRxHandoffId is not Guid handoffId) return;

        await conn.ExecuteAsync("""
            UPDATE pack_connect.rx_handoffs
            SET handoff_status = 'consumed',
                consumed_at = COALESCE(consumed_at, NOW()),
                consumed_by = COALESCE(consumed_by, @Actor),
                updated_at = NOW()
            WHERE id = @Id
              AND pharmacy_tenant_id = @TenantId
              AND handoff_status IN ('pending_pharmacy', 'consumed')
              AND dismissed_at IS NULL
            """, new
        {
            Id = handoffId,
            TenantId,
            Actor = actorUserId,
        }, tx);

        // Đồng bộ hàng đợi tín hiệu Connect (ack) khi đã bán xong đơn PK.
        await conn.ExecuteAsync("""
            UPDATE pack_connect.status_events e
            SET event_status = 'consumed',
                consumed_at = COALESCE(e.consumed_at, NOW()),
                consumed_by = COALESCE(e.consumed_by, @Actor),
                updated_at = NOW()
            FROM pack_connect.rx_handoffs h
            WHERE h.id = @Id
              AND h.pharmacy_tenant_id = @TenantId
              AND e.pharmacy_tenant_id = @TenantId
              AND e.event_status = 'pending_pharmacy'
              AND (
                    e.id = h.status_event_id
                 OR (e.source_type = 'clinic_rx' AND e.source_id = h.id)
              )
            """, new
        {
            Id = handoffId,
            TenantId,
            Actor = actorUserId,
        }, tx);
    }

    private async Task TryUpsertRepurchaseSuggestionAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId)
    {
        const string sql = """
            INSERT INTO repurchase_suggestions (
                tenant_id, customer_id, customer_account_id, sales_order_id,
                order_label, status, suggested_for_date
            )
            SELECT
                so.tenant_id,
                so.customer_id,
                ca.id,
                so.id,
                COALESCE(NULLIF(TRIM(so.reminder_label), ''), CONCAT('Đơn ', so.order_number)),
                'pending',
                (so.order_date + (so.reminder_days_supply || ' days')::interval)::date
            FROM sales_orders so
            CROSS JOIN LATERAL (
                SELECT id
                FROM customer_accounts
                WHERE customer_id = so.customer_id
                  AND tenant_id = so.tenant_id
                ORDER BY created_at ASC
                LIMIT 1
            ) ca
            WHERE so.id = @OrderId
              AND so.tenant_id = @TenantId
              AND so.reminder_days_supply IS NOT NULL
              AND so.customer_id IS NOT NULL
            ON CONFLICT (sales_order_id) DO UPDATE SET
                order_label = EXCLUDED.order_label,
                suggested_for_date = EXCLUDED.suggested_for_date,
                updated_at = NOW()
            """;

        await conn.ExecuteAsync(sql, new { OrderId = orderId, TenantId }, tx);
    }

    private async Task TryAutoCreateDrinkRemindersFromOrderAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId)
    {
        var suggestion = await conn.QuerySingleOrDefaultAsync<RepurchaseDrinkBootstrapRow>(
            """
            SELECT
                rs.id AS Id,
                rs.drink_reminders_created_at AS DrinkRemindersCreatedAt,
                so.customer_id AS CustomerId
            FROM repurchase_suggestions rs
            INNER JOIN sales_orders so ON so.id = rs.sales_order_id AND so.tenant_id = rs.tenant_id
            WHERE rs.sales_order_id = @OrderId
              AND rs.tenant_id = @TenantId
              AND so.reminder_days_supply IS NOT NULL
              AND so.customer_id IS NOT NULL
            FOR UPDATE OF rs
            """,
            new { OrderId = orderId, TenantId },
            tx);

        if (suggestion is null || suggestion.DrinkRemindersCreatedAt.HasValue)
            return;

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
                NULL,
                soi.product_id,
                CONCAT('Auto from order #', so.order_number),
                TIME '08:00',
                ARRAY[1,2,3,4,5,6,7]::SMALLINT[],
                @NextRemindAt,
                TRUE
            FROM sales_order_items soi
            INNER JOIN sales_orders so ON so.id = soi.sales_order_id
            WHERE soi.sales_order_id = @OrderId
            ORDER BY soi.id
            """,
            new
            {
                TenantId,
                CustomerId = suggestion.CustomerId,
                OrderId = orderId,
                NextRemindAt = DateTime.UtcNow.AddHours(1),
            },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE repurchase_suggestions
            SET drink_reminders_created_at = NOW(),
                updated_at = NOW()
            WHERE id = @SuggestionId
            """,
            new { SuggestionId = suggestion.Id },
            tx);
    }

    private sealed class RepurchaseDrinkBootstrapRow
    {
        public Guid Id { get; set; }
        public DateTime? DrinkRemindersCreatedAt { get; set; }
        public Guid CustomerId { get; set; }
    }

    private static async Task InsertDraftItemAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        PricedSaleLineResult line,
        Guid? prescriptionLineId)
    {
        const string sql = """
            INSERT INTO sales_order_items (
                sales_order_id, product_id, product_unit_id, batch_id,
                quantity, unit_price, discount_amount, discount_type, discount_value, line_total, prescription_line_id
            )
            VALUES (
                @OrderId, @ProductId, @ProductUnitId, NULL,
                @Quantity, @UnitPrice, @DiscountAmount, @DiscountType, @DiscountValue, @LineTotal, @PrescriptionLineId
            )
            """;
        await conn.ExecuteAsync(sql, new
        {
            OrderId = orderId,
            line.ProductId,
            line.ProductUnitId,
            line.Quantity,
            line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            DiscountType = line.DiscountType,
            DiscountValue = line.DiscountValue,
            LineTotal = line.LineTotal,
            PrescriptionLineId = prescriptionLineId,
        }, tx);
    }

    private async Task<List<CompletedSaleLineInsert>> InsertCompletedLinesAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        Guid warehouseId,
        List<PlannedSaleLine> linePlans,
        CancellationToken cancellationToken)
    {
        var inserted = new List<CompletedSaleLineInsert>(linePlans.Count);
        foreach (var plan in linePlans)
        {
            const string itemSql = """
                INSERT INTO sales_order_items (
                    sales_order_id, product_id, product_unit_id, batch_id, batch_source,
                    quantity, unit_price, discount_amount, discount_type, discount_value, line_total, prescription_line_id
                )
                VALUES (
                    @OrderId, @ProductId, @ProductUnitId, @BatchId, @BatchSource,
                    @Quantity, @UnitPrice, @DiscountAmount, @DiscountType, @DiscountValue, @LineTotal, @PrescriptionLineId
                )
                RETURNING id
                """;
            var itemId = await conn.QuerySingleAsync<Guid>(itemSql, new
            {
                OrderId = orderId,
                plan.ProductId,
                plan.ProductUnitId,
                plan.BatchId,
                BatchSource = plan.BatchSource,
                plan.Quantity,
                plan.UnitPrice,
                plan.DiscountAmount,
                DiscountType = plan.DiscountType,
                DiscountValue = plan.DiscountValue,
                plan.LineTotal,
                plan.PrescriptionLineId,
            }, tx);
            inserted.Add(new CompletedSaleLineInsert(itemId, plan.PrescriptionLineId, plan.Quantity));

            const string allocSql = """
                INSERT INTO sales_order_batch_allocations (
                    sales_order_item_id, batch_id, quantity, unit_cost
                )
                VALUES (@ItemId, @BatchId, @Quantity, @UnitCost)
                """;
            await conn.ExecuteAsync(allocSql, new
            {
                ItemId = itemId,
                plan.BatchId,
                Quantity = plan.BaseQuantity,
                plan.UnitCost,
            }, tx);

            await _inventory.DecreaseBatchQuantityAsync(conn, tx, plan.BatchId, plan.BaseQuantity, cancellationToken);
            await _inventory.InsertMovementAsync(
                conn, tx, warehouseId, plan.BatchId, plan.ProductId,
                StockMovementTypes.Out, StockReferenceTypes.SalesOrder, orderId,
                plan.BaseQuantity, plan.UnitCost, null, cancellationToken);
        }

        return inserted;
    }

    private static decimal SplitReturnRefund(
        decimal totalRefund,
        decimal outstanding,
        decimal amountPaid,
        out decimal debtReduced,
        out decimal paidReduced)
    {
        var remaining = totalRefund;
        debtReduced = Math.Min(remaining, Math.Max(0, outstanding));
        remaining -= debtReduced;
        paidReduced = Math.Min(remaining, Math.Max(0, amountPaid));
        return paidReduced;
    }

    private static List<CreateSalePaymentRequest> BuildReturnCashPayments(
        IReadOnlyList<CreateSalePaymentRequest>? payments,
        decimal totalRefund,
        decimal outstanding,
        decimal amountPaid)
    {
        var cashRefund = SplitReturnRefund(totalRefund, outstanding, amountPaid, out _, out _);
        if (cashRefund <= 0.01m)
            return [new CreateSalePaymentRequest(SalesPaymentMethods.Cash, 0)];

        var method = payments?.FirstOrDefault(p => p.PaymentMethod != SalesPaymentMethods.Credit)?.PaymentMethod
            ?? SalesPaymentMethods.Cash;
        return
        [
            new CreateSalePaymentRequest(
                method,
                Math.Round(cashRefund, 2, MidpointRounding.AwayFromZero)),
        ];
    }

    private static async Task ApplySaleReturnFinancialAdjustmentAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        Guid tenantId,
        decimal totalRefund,
        decimal outstanding,
        decimal amountPaid)
    {
        SplitReturnRefund(totalRefund, outstanding, amountPaid, out var debtReduced, out var paidReduced);
        const string sql = """
            UPDATE sales_orders SET
                total_amount = GREATEST(0, total_amount - @TotalRefund),
                outstanding = GREATEST(0, outstanding - @DebtReduced),
                amount_paid = GREATEST(0, amount_paid - @PaidReduced)
            WHERE id = @OrderId AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(sql, new
        {
            OrderId = orderId,
            TenantId = tenantId,
            TotalRefund = totalRefund,
            DebtReduced = debtReduced,
            PaidReduced = paidReduced,
        }, tx);
    }

    private static async Task InsertReturnPaymentsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid returnId,
        List<CreateSalePaymentRequest> payments)
    {
        const string paymentSql = """
            INSERT INTO sales_return_payments (sales_return_id, payment_method, amount)
            VALUES (@ReturnId, @PaymentMethod, @Amount)
            """;
        foreach (var payment in payments)
        {
            await conn.ExecuteAsync(paymentSql, new
            {
                ReturnId = returnId,
                payment.PaymentMethod,
                payment.Amount,
            }, tx);
        }
    }

    private static async Task InsertPaymentsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        List<CreateSalePaymentRequest> payments)
    {
        const string paymentSql = """
            INSERT INTO sales_payments (sales_order_id, payment_method, amount)
            VALUES (@OrderId, @PaymentMethod, @Amount)
            """;
        foreach (var payment in payments)
        {
            await conn.ExecuteAsync(paymentSql, new
            {
                OrderId = orderId,
                payment.PaymentMethod,
                payment.Amount,
            }, tx);
        }
    }

    private sealed record PlannedSaleLine(
        Guid ProductId,
        Guid ProductUnitId,
        Guid BatchId,
        decimal Quantity,
        decimal UnitPrice,
        decimal LineTotal,
        decimal DiscountAmount,
        short? DiscountType,
        decimal DiscountValue,
        decimal UnitCost,
        decimal BaseQuantity,
        short BatchSource,
        Guid? PrescriptionLineId);

    private sealed record CompletedSaleLineInsert(
        Guid SalesOrderItemId,
        Guid? PrescriptionLineId,
        decimal Quantity);

    private sealed class BatchLockRow
    {
        public Guid Id { get; init; }
        public decimal QuantityAvailable { get; init; }
        public decimal UnitCost { get; init; }
    }

    private sealed class DraftHeaderRow
    {
        public Guid WarehouseId { get; init; }
        public Guid? CustomerId { get; init; }
        public short PriceType { get; init; }
        public decimal Subtotal { get; init; }
        public decimal OrderDiscountAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public short? OrderDiscountType { get; init; }
        public decimal OrderDiscountValue { get; init; }
    }

    private sealed class ReturnOrderRow
    {
        public Guid Id { get; init; }
        public Guid WarehouseId { get; init; }
        public short Status { get; init; }
        public decimal OrderDiscountAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal AmountPaid { get; init; }
        public decimal Outstanding { get; init; }
    }

    private sealed class ReturnableItemRow
    {
        public Guid Id { get; init; }
        public Guid ProductId { get; init; }
        public Guid BatchId { get; init; }
        public decimal SoldQuantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal LineTotal { get; init; }
        public decimal ReturnedQuantity { get; init; }
    }

    private sealed class ShiftAmountRow
    {
        public short PaymentMethod { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class ShiftHeaderRow
    {
        public Guid Id { get; init; }
        public string ShiftNumber { get; init; } = "";
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public string OpenedByUserName { get; init; } = "";
        public string? ClosedByUserName { get; init; }
        public DateTime OpenedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public decimal OpeningCash { get; init; }
        public decimal? ClosingCash { get; init; }
        public decimal? ExpectedCash { get; init; }
        public decimal? CashVariance { get; init; }
        public short Status { get; init; }
        public string? CloseNotes { get; init; }
    }

    private sealed class ShiftCloseRow
    {
        public Guid Id { get; init; }
        public DateTime OpenedAt { get; init; }
        public decimal OpeningCash { get; init; }
        public short Status { get; init; }
    }

    private sealed class ReturnHeaderRow
    {
        public Guid Id { get; init; }
        public string ReturnNumber { get; init; } = "";
        public Guid SalesOrderId { get; init; }
        public string OrderNumber { get; init; } = "";
        public DateTime ReturnDate { get; init; }
        public short Status { get; init; }
        public string? Reason { get; init; }
        public Guid? SalesShiftId { get; init; }
        public string? ShiftNumber { get; init; }
    }

    private sealed class ReturnListRow
    {
        public Guid Id { get; init; }
        public string ReturnNumber { get; init; } = "";
        public Guid SalesOrderId { get; init; }
        public string OrderNumber { get; init; } = "";
        public DateTime ReturnDate { get; init; }
        public short Status { get; init; }
        public decimal TotalRefund { get; init; }
        public Guid? SalesShiftId { get; init; }
        public string? ShiftNumber { get; init; }
    }

    private sealed class LotAlertRow
    {
        public Guid ProductId { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string SoldBatchNumber { get; init; } = "";
        public DateOnly? SoldExpiryDate { get; init; }
        public string EarlierBatchNumber { get; init; } = "";
        public DateOnly? EarlierExpiryDate { get; init; }
        public decimal EarlierBookQuantity { get; init; }
    }

    private sealed record PosProductLookupRow(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        string DispensingClass,
        Guid ProductUnitId,
        string UnitName,
        decimal ConversionFactor,
        decimal UnitPrice,
        decimal StockAvailable);

    private sealed record PosStockCheckRow(
        Guid ProductId,
        string ProductCode,
        string ProductName,
        Guid ProductUnitId,
        string UnitName,
        decimal ConversionFactor,
        decimal StockAvailable);

    private sealed record PosProductSearchRow(
        string ProductCode,
        string ProductName,
        string LookupCode,
        string UnitName,
        decimal ConversionFactor,
        decimal UnitPrice,
        decimal StockAvailable);

    private sealed class UnitRow
    {
        public decimal ConversionFactor { get; init; }
    }

    private sealed class CustomerCreditRow
    {
        public bool AllowCredit { get; init; }
        public decimal? CreditLimit { get; init; }
    }

    private sealed class SalesOrderHeaderRow
    {
        public Guid Id { get; init; }
        public string OrderNumber { get; init; } = "";
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public Guid? CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public short Status { get; init; }
        public DateTime OrderDate { get; init; }
        public decimal Subtotal { get; init; }
        public decimal DiscountAmount { get; init; }
        public short? OrderDiscountType { get; init; }
        public decimal OrderDiscountValue { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal AmountPaid { get; init; }
        public decimal Outstanding { get; init; }
        public decimal TotalRefunded { get; init; }
        public string? Notes { get; init; }
        public Guid? SalesShiftId { get; init; }
        public string? ShiftNumber { get; init; }
        public int? LoyaltyPointsEarned { get; init; }
        public decimal LoyaltyPointsRedeemed { get; init; }
        public decimal LoyaltyDiscountAmount { get; init; }
        public decimal VoucherDiscountAmount { get; init; }
        public string? VoucherCode { get; init; }
        public string? VoucherName { get; init; }
    }

    private sealed class DraftSaleLineRow
    {
        public Guid ProductId { get; init; }
        public Guid ProductUnitId { get; init; }
        public decimal Quantity { get; init; }
        public short? DiscountType { get; init; }
        public decimal? DiscountValue { get; init; }
        public Guid? PrescriptionLineId { get; init; }
    }

    public async Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customers
                WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { CustomerId = customerId, TenantId });
    }

    public async Task<SalesOrderPaymentLink?> GetSalesOrderPaymentLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                so.id AS Id,
                so.customer_id AS CustomerId,
                so.warehouse_id AS WarehouseId,
                so.status AS Status,
                so.outstanding AS Outstanding
            FROM sales_orders so
            WHERE so.id = @Id AND so.tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SalesOrderPaymentLink>(sql, new { Id = id, TenantId });
    }

    public async Task<IReadOnlyList<CustomerPaymentListItemDto>> GetCustomerPaymentsAsync(
        CustomerPaymentListFilter filter,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "cp.tenant_id = @TenantId",
            "cp.deleted_at IS NULL",
        };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(filter.Search)
            && string.IsNullOrWhiteSpace(filter.CustomerSearch)
            && string.IsNullOrWhiteSpace(filter.DocumentSearch))
        {
            conditions.Add("""
                (
                    cp.payment_number ILIKE @Search
                    OR c.full_name ILIKE @Search
                    OR c.customer_code ILIKE @Search
                    OR c.phone ILIKE @Search
                    OR COALESCE(so.order_number, '') ILIKE @Search
                    OR (@SearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @SearchDigitsPattern)
                )
                """);
            parameters.Add("Search", $"%{filter.Search.Trim()}%");
            AddPhoneDigitSearchParams(parameters, filter.Search, "Search");
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerSearch))
        {
            conditions.Add("""
                (
                    c.full_name ILIKE @CustomerSearch
                    OR c.customer_code ILIKE @CustomerSearch
                    OR c.phone ILIKE @CustomerSearch
                    OR (@CustomerSearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @CustomerSearchDigitsPattern)
                )
                """);
            parameters.Add("CustomerSearch", $"%{filter.CustomerSearch.Trim()}%");
            AddPhoneDigitSearchParams(parameters, filter.CustomerSearch, "CustomerSearch");
        }

        if (!string.IsNullOrWhiteSpace(filter.DocumentSearch))
        {
            conditions.Add("(cp.payment_number ILIKE @DocumentSearch OR COALESCE(so.order_number, '') ILIKE @DocumentSearch)");
            parameters.Add("DocumentSearch", $"%{filter.DocumentSearch.Trim()}%");
        }

        if (filter.CustomerId is Guid customerId)
        {
            conditions.Add("cp.customer_id = @CustomerId");
            parameters.Add("CustomerId", customerId);
        }

        if (filter.Status is short status)
        {
            conditions.Add("cp.status = @Status");
            parameters.Add("Status", status);
        }

        if (filter.DateFrom is DateOnly dateFrom)
        {
            conditions.Add("cp.payment_date >= @DateFrom");
            parameters.Add("DateFrom", dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (filter.DateTo is DateOnly dateTo)
        {
            conditions.Add("cp.payment_date < @DateTo");
            parameters.Add("DateTo", dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (allowedWarehouseIds is { Length: > 0 })
        {
            conditions.Add("""
                (
                    cp.sales_order_id IS NULL
                    OR EXISTS (
                        SELECT 1 FROM sales_orders so_w
                        WHERE so_w.id = cp.sales_order_id
                          AND so_w.tenant_id = cp.tenant_id
                          AND so_w.warehouse_id = ANY(@AllowedWarehouseIds)
                    )
                )
                """);
            parameters.Add("AllowedWarehouseIds", allowedWarehouseIds);
        }

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT
                cp.id AS Id, cp.payment_number AS PaymentNumber, cp.customer_id AS CustomerId,
                c.full_name AS CustomerName, cp.amount AS Amount, cp.payment_method AS PaymentMethod,
                cp.status AS Status, cp.payment_date AS PaymentDate, cp.posted_at AS PostedAt,
                cp.sales_order_id AS SalesOrderId, so.order_number AS OrderNumber, cp.notes AS Notes
            FROM customer_payments cp
            INNER JOIN customers c ON c.id = cp.customer_id
            LEFT JOIN sales_orders so ON so.id = cp.sales_order_id
            WHERE {where}
            ORDER BY cp.payment_date DESC, cp.created_at DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerPaymentListItemDto>(sql, parameters)).ToList();
    }

    public async Task<CustomerPaymentListItemDto?> GetCustomerPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                cp.id AS Id, cp.payment_number AS PaymentNumber, cp.customer_id AS CustomerId,
                c.full_name AS CustomerName, cp.amount AS Amount, cp.payment_method AS PaymentMethod,
                cp.status AS Status, cp.payment_date AS PaymentDate, cp.posted_at AS PostedAt,
                cp.sales_order_id AS SalesOrderId, so.order_number AS OrderNumber, cp.notes AS Notes
            FROM customer_payments cp
            INNER JOIN customers c ON c.id = cp.customer_id
            LEFT JOIN sales_orders so ON so.id = cp.sales_order_id
            WHERE cp.id = @Id AND cp.tenant_id = @TenantId AND cp.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CustomerPaymentListItemDto>(sql, new { Id = id, TenantId });
    }

    public async Task<Guid> CreateCustomerPaymentAsync(
        CreateCustomerPaymentRequest request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var paymentNumber = await NextCustomerPaymentNumberAsync(conn, tx, cancellationToken);
        var paymentDate = (request.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        const string sql = """
            INSERT INTO customer_payments (
                tenant_id, customer_id, sales_order_id,
                payment_number, amount, payment_method, payment_date, notes, status, created_by
            )
            VALUES (
                @TenantId, @CustomerId, @SalesOrderId,
                @PaymentNumber, @Amount, @PaymentMethod, @PaymentDate, @Notes, @Status, @CreatedBy
            )
            RETURNING id
            """;
        var id = await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            request.CustomerId,
            request.SalesOrderId,
            PaymentNumber = paymentNumber,
            request.Amount,
            request.PaymentMethod,
            PaymentDate = paymentDate,
            request.Notes,
            Status = CustomerPaymentStatuses.Draft,
            CreatedBy = createdBy,
        }, tx);
        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<bool> UpdateCustomerPaymentAsync(
        Guid id,
        UpdateCustomerPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var paymentDate = request.PaymentDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        const string sql = """
            UPDATE customer_payments SET
                customer_id = @CustomerId,
                sales_order_id = @SalesOrderId,
                amount = @Amount,
                payment_method = @PaymentMethod,
                payment_date = COALESCE(@PaymentDate, payment_date),
                notes = @Notes
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            request.CustomerId,
            request.SalesOrderId,
            request.Amount,
            request.PaymentMethod,
            PaymentDate = paymentDate,
            request.Notes,
            Draft = CustomerPaymentStatuses.Draft,
        });
        return rows > 0;
    }

    public async Task<bool> PostCustomerPaymentAsync(Guid id, Guid postedBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string loadSql = """
            SELECT id AS Id, customer_id AS CustomerId, sales_order_id AS SalesOrderId,
                   amount AS Amount, payment_method AS PaymentMethod, status AS Status
            FROM customer_payments
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            FOR UPDATE
            """;
        var payment = await conn.QuerySingleOrDefaultAsync<CustomerPaymentPostRow>(
            loadSql, new { Id = id, TenantId }, tx);
        if (payment is null || payment.Status != CustomerPaymentStatuses.Draft)
            return false;

        var remaining = payment.Amount;
        if (payment.SalesOrderId is Guid orderId)
        {
            await ApplyCustomerCollectionToOrderAsync(
                conn, tx, orderId, payment.CustomerId, remaining, payment.PaymentMethod, cancellationToken);
        }
        else
        {
            const string ordersSql = """
                SELECT id AS Id, outstanding AS Outstanding
                FROM sales_orders
                WHERE tenant_id = @TenantId
                  AND customer_id = @CustomerId
                  AND status = @Completed
                  AND outstanding > 0.009
                ORDER BY order_date, order_number
                FOR UPDATE
                """;
            var orders = (await conn.QueryAsync<OrderOutstandingRow>(
                ordersSql,
                new { TenantId, payment.CustomerId, Completed = SalesOrderStatuses.Completed },
                tx)).ToList();

            foreach (var order in orders)
            {
                if (remaining <= 0.009m)
                    break;

                var apply = Math.Min(remaining, order.Outstanding);
                await ApplyCustomerCollectionToOrderAsync(
                    conn, tx, order.Id, payment.CustomerId, apply, payment.PaymentMethod, cancellationToken);
                remaining -= apply;
            }

            if (remaining > 0.009m)
                throw new InvalidOperationException("Số tiền thu vượt tổng còn nợ của khách hàng.");
        }

        const string postSql = """
            UPDATE customer_payments SET
                status = @Posted,
                posted_at = NOW(),
                posted_by = @PostedBy
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft AND deleted_at IS NULL
            """;
        var rows = await conn.ExecuteAsync(postSql, new
        {
            Id = id,
            TenantId,
            Draft = CustomerPaymentStatuses.Draft,
            Posted = CustomerPaymentStatuses.Posted,
            PostedBy = postedBy,
        }, tx);

        await tx.CommitAsync(cancellationToken);
        return rows > 0;
    }

    public async Task<bool> CancelCustomerPaymentAsync(Guid id, Guid cancelledBy, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_payments SET
                status = @Cancelled,
                cancelled_at = NOW(),
                cancelled_by = @CancelledBy
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            Draft = CustomerPaymentStatuses.Draft,
            Cancelled = CustomerPaymentStatuses.Cancelled,
            CancelledBy = cancelledBy,
        });
        return rows > 0;
    }

    public async Task<IReadOnlyList<SalesOrderReceivableSourceRow>> GetSalesOrderReceivableSourceRowsAsync(
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND so.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var sql = $"""
            SELECT
                c.id AS CustomerId,
                c.customer_code AS CustomerCode,
                c.full_name AS CustomerName,
                c.phone AS CustomerPhone,
                so.id AS SalesOrderId,
                so.order_number AS OrderNumber,
                so.order_date AS OrderDate,
                so.total_amount AS OrderTotal,
                so.amount_paid AS AmountPaid,
                so.outstanding AS Outstanding
            FROM sales_orders so
            INNER JOIN customers c ON c.id = so.customer_id
            WHERE so.tenant_id = @TenantId
              AND so.status = @Completed
              AND so.outstanding > 0.009
              AND so.customer_id IS NOT NULL
              AND c.deleted_at IS NULL
              {warehouseFilter}
            ORDER BY c.full_name, so.order_date, so.order_number
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<SalesOrderReceivableSourceRow>(sql, new
        {
            TenantId,
            Completed = SalesOrderStatuses.Completed,
            AllowedWarehouseIds = allowedWarehouseIds,
        })).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetUnlinkedCustomerPaymentTotalsAsync(
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND so.id IS NOT NULL AND so.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var sql = $"""
            SELECT cp.customer_id AS CustomerId, COALESCE(SUM(cp.amount), 0) AS TotalPaid
            FROM customer_payments cp
            LEFT JOIN sales_orders so ON so.id = cp.sales_order_id AND so.tenant_id = cp.tenant_id
            WHERE cp.tenant_id = @TenantId
              AND cp.status = @PaymentPosted
              AND cp.sales_order_id IS NULL
              {warehouseFilter}
            GROUP BY cp.customer_id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid CustomerId, decimal TotalPaid)>(sql, new
        {
            TenantId,
            PaymentPosted = CustomerPaymentStatuses.Posted,
            AllowedWarehouseIds = allowedWarehouseIds,
        });
        return rows.ToDictionary(x => x.CustomerId, x => x.TotalPaid);
    }

    private async Task ApplyCustomerCollectionToOrderAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        Guid customerId,
        decimal amount,
        short paymentMethod,
        CancellationToken cancellationToken)
    {
        if (amount <= 0.009m)
            return;

        const string lockSql = """
            SELECT customer_id AS CustomerId, status AS Status, outstanding AS Outstanding
            FROM sales_orders
            WHERE id = @OrderId AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var order = await conn.QuerySingleOrDefaultAsync<OrderCollectionRow>(
            lockSql, new { OrderId = orderId, TenantId }, tx);
        if (order is null)
            throw new InvalidOperationException("Đơn bán không tồn tại.");
        if (order.CustomerId != customerId)
            throw new InvalidOperationException("Đơn bán không thuộc khách hàng đã chọn.");
        if (order.Status != SalesOrderStatuses.Completed)
            throw new InvalidOperationException("Chỉ thu nợ trên đơn bán đã hoàn tất.");
        if (amount > order.Outstanding + 0.009m)
            throw new InvalidOperationException("Số tiền thu vượt còn nợ của đơn bán.");

        const string updateSql = """
            UPDATE sales_orders SET
                amount_paid = amount_paid + @Amount,
                outstanding = outstanding - @Amount
            WHERE id = @OrderId AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(updateSql, new { OrderId = orderId, TenantId, Amount = amount }, tx);

        const string paymentSql = """
            INSERT INTO sales_payments (sales_order_id, payment_method, amount)
            VALUES (@OrderId, @PaymentMethod, @Amount)
            """;
        await conn.ExecuteAsync(paymentSql, new
        {
            OrderId = orderId,
            PaymentMethod = paymentMethod,
            Amount = amount,
        }, tx);
    }

    private async Task<string> NextCustomerPaymentNumberAsync(
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int + 1 FROM customer_payments WHERE tenant_id = @TenantId
            """;
        var seq = await conn.QuerySingleAsync<int>(sql, new { TenantId }, tx);
        return $"THU-{seq:D6}";
    }

    private sealed class CustomerPaymentPostRow
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public Guid? SalesOrderId { get; init; }
        public decimal Amount { get; init; }
        public short PaymentMethod { get; init; }
        public short Status { get; init; }
    }

    private sealed class OrderOutstandingRow
    {
        public Guid Id { get; init; }
        public decimal Outstanding { get; init; }
    }

    private sealed class OrderCollectionRow
    {
        public Guid CustomerId { get; init; }
        public short Status { get; init; }
        public decimal Outstanding { get; init; }
    }

    private static string BuildSalesOrderSearchFilter(SalesOrderListFilter filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.CustomerSearch))
        {
            parts.Add("""
                (
                    COALESCE(c.full_name, '') ILIKE @CustomerSearchPattern
                    OR COALESCE(c.phone, '') ILIKE @CustomerSearchPattern
                    OR (@CustomerSearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @CustomerSearchDigitsPattern)
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(filter.DocumentSearch))
        {
            parts.Add("o.order_number ILIKE @DocumentSearchPattern");
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(filter.Search))
        {
            parts.Add("""
                (
                    o.order_number ILIKE @SearchPattern
                    OR w.warehouse_name ILIKE @SearchPattern
                    OR COALESCE(c.full_name, '') ILIKE @SearchPattern
                    OR COALESCE(c.phone, '') ILIKE @SearchPattern
                    OR (@SearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @SearchDigitsPattern)
                )
                """);
        }

        return parts.Count == 0 ? string.Empty : $"AND {string.Join(" AND ", parts)}";
    }

    private static string BuildSalesReturnSearchFilter(string? search, string? customerSearch, string? documentSearch)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customerSearch))
        {
            parts.Add("""
                (
                    COALESCE(c.full_name, '') ILIKE @CustomerSearchPattern
                    OR COALESCE(c.phone, '') ILIKE @CustomerSearchPattern
                    OR (@CustomerSearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @CustomerSearchDigitsPattern)
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(documentSearch))
        {
            parts.Add(
                "(r.return_number ILIKE @DocumentSearchPattern OR o.order_number ILIKE @DocumentSearchPattern)");
        }

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(search))
        {
            parts.Add("""
                (
                    r.return_number ILIKE @SearchPattern
                    OR o.order_number ILIKE @SearchPattern
                    OR COALESCE(c.full_name, '') ILIKE @SearchPattern
                    OR COALESCE(c.phone, '') ILIKE @SearchPattern
                    OR (@SearchDigits <> '' AND regexp_replace(COALESCE(c.phone, ''), '\D', '', 'g') ILIKE @SearchDigitsPattern)
                )
                """);
        }

        return parts.Count == 0 ? string.Empty : $"AND {string.Join(" AND ", parts)}";
    }

    private static string ExtractPhoneDigits(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    private static string? BuildPhoneDigitsPattern(string digits) =>
        string.IsNullOrEmpty(digits) ? null : $"%{digits}%";

    private static void AddPhoneDigitSearchParams(DynamicParameters parameters, string? searchText, string prefix)
    {
        var digits = ExtractPhoneDigits(searchText);
        parameters.Add($"{prefix}Digits", digits);
        parameters.Add($"{prefix}DigitsPattern", BuildPhoneDigitsPattern(digits));
    }

    private Task WriteSalesOrderCompletedPlatformEventAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        string orderNumber,
        Guid warehouseId,
        Guid? customerId,
        decimal totalAmount,
        decimal loyaltyPointsRedeemed,
        decimal loyaltyDiscountAmount,
        Guid? salesShiftId,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        _platformEvents.WriteAsync(
            conn,
            tx,
            PlatformEventTypes.SalesOrderCompleted,
            PlatformEventAggregateTypes.SalesOrder,
            orderId,
            new
            {
                orderId,
                orderNumber,
                warehouseId,
                customerId,
                totalAmount,
                loyaltyPointsRedeemed,
                loyaltyDiscountAmount,
                status = SalesOrderStatuses.Completed,
                salesShiftId,
            },
            actorUserId,
            source: PharmacyPackDefinition.EventSource,
            cancellationToken: cancellationToken);
}
