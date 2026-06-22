using System.Data;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Configuration;
using PharmaCore.Application.Integration;
using PharmaCore.Application.Inventory;
using PharmaCore.Application.Sales;
using PharmaCore.Infrastructure.Data;
using PharmaCore.Infrastructure.Inventory;

namespace PharmaCore.Infrastructure.Sales;

internal sealed class SalesRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly InventoryRepository _inventory;
    private readonly IBatchResolver _batchResolver;
    private readonly ITenantSettingsService _tenantSettings;
    private readonly IIntegrationOutboxWriter _outbox;

    public SalesRepository(
        IDbConnectionFactory db,
        ITenantContext tenant,
        InventoryRepository inventory,
        IBatchResolver batchResolver,
        ITenantSettingsService tenantSettings,
        IIntegrationOutboxWriter outbox)
    {
        _db = db;
        _tenant = tenant;
        _inventory = inventory;
        _batchResolver = batchResolver;
        _tenantSettings = tenantSettings;
        _outbox = outbox;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<CustomerListItemDto>> SearchCustomersAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "c.tenant_id = @TenantId", "c.deleted_at IS NULL" };
        var param = new DynamicParameters();
        param.Add("TenantId", TenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(c.full_name ILIKE @Search OR c.phone ILIKE @Search OR c.customer_code ILIKE @Search)");
            param.Add("Search", $"%{search.Trim()}%");
        }

        var sql = $"""
            SELECT
                c.id AS Id, c.customer_code AS CustomerCode, c.full_name AS FullName,
                c.phone AS Phone, c.email AS Email
            FROM customers c
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY c.full_name
            LIMIT 50
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<CustomerListItemDto>(sql, param)).ToList();
    }

    public async Task<PosProductLookupDto?> LookupByBarcodeAsync(
        string barcode,
        Guid warehouseId,
        short priceType,
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
                pr.price AS UnitPrice,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = @TenantId AND b.warehouse_id = @WarehouseId
                      AND b.product_id = p.id AND b.quantity_available > 0
                ), 0) AS StockAvailable
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
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var lookup = await conn.QuerySingleOrDefaultAsync<PosProductLookupDto>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            PriceType = priceType,
            Barcode = barcode.Trim(),
        });
        if (lookup is null) return null;

        var stockInSaleUnit = lookup.ConversionFactor > 0
            ? lookup.StockAvailable / lookup.ConversionFactor
            : lookup.StockAvailable;
        lookup = lookup with { StockAvailable = stockInSaleUnit };

        var batchMode = await _tenantSettings.GetBatchModeAsync(cancellationToken);
        if (batchMode == TenantBatchMode.Off)
        {
            return lookup with
            {
                BatchHints = null,
                StockSourceLabel = StockSourceLabels.SystemBook,
            };
        }

        var batches = await _batchResolver.GetAvailableBatchesAsync(
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
        var row = await conn.QuerySingleOrDefaultAsync<PosStockCheckDto>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            ProductUnitId = productUnitId,
        });
        if (row is null) return null;

        var stockInSaleUnit = row.ConversionFactor > 0
            ? row.StockAvailable / row.ConversionFactor
            : row.StockAvailable;
        return row with { StockAvailable = stockInSaleUnit, StockSourceLabel = StockSourceLabels.SystemBook };
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

            var batches = await _batchResolver.GetAvailableBatchesAsync(
                conn, request.WarehouseId, item.ProductId, cancellationToken);
            var baseNeeded = item.Quantity * unit.ConversionFactor;
            IReadOnlyList<FifoAllocationResult> allocations;
            try
            {
                allocations = _batchResolver.AllocateFromBatches(batches, baseNeeded);
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

    public async Task<IReadOnlyList<SalesOrderListItemDto>> GetSalesOrdersAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                o.id AS Id, o.order_number AS OrderNumber, o.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, o.customer_id AS CustomerId,
                c.full_name AS CustomerName, o.status AS Status, o.order_date AS OrderDate,
                o.total_amount AS TotalAmount,
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
            WHERE o.tenant_id = @TenantId
            ORDER BY o.order_date DESC
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<SalesOrderListItemDto>(sql, new
        {
            TenantId,
            ReturnCompleted = SalesReturnStatuses.Completed,
        })).ToList();
    }

    public async Task<SalesOrderDetailDto?> GetSalesOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                o.id AS Id, o.order_number AS OrderNumber, o.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, o.customer_id AS CustomerId,
                c.full_name AS CustomerName, o.status AS Status, o.order_date AS OrderDate,
                o.subtotal AS Subtotal, o.discount_amount AS DiscountAmount, o.total_amount AS TotalAmount,
                o.order_discount_type AS OrderDiscountType, o.order_discount_value AS OrderDiscountValue,
                o.notes AS Notes,
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
        var items = (await conn.QueryAsync<SalesOrderItemDto>(itemsSql, new
        {
            OrderId = id,
            ReturnCompleted = SalesReturnStatuses.Completed,
        })).ToList();
        var payments = (await conn.QueryAsync<SalesPaymentDto>(paymentsSql, new { OrderId = id })).ToList();
        var refundPayments = (await conn.QueryAsync<SalesRefundPaymentSummaryDto>(refundPaymentsSql, new
        {
            OrderId = id,
            ReturnCompleted = SalesReturnStatuses.Completed,
        })).ToList();
        var lineDiscountTotal = items.Sum(i => i.DiscountAmount);
        return new SalesOrderDetailDto(
            header.Id, header.OrderNumber, header.WarehouseId, header.WarehouseName,
            header.CustomerId, header.CustomerName, header.Status, header.OrderDate,
            header.Subtotal, header.DiscountAmount, lineDiscountTotal,
            header.OrderDiscountType, header.OrderDiscountValue, header.TotalAmount, header.TotalRefunded,
            header.Notes, items, payments, refundPayments,
            header.SalesShiftId, header.ShiftNumber);
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
        var pricing = SalesPricing.PriceOrder(pricedInputs, orderDiscount);
        SalesPricing.ValidateDiscounts(pricing, discountPolicy);

        var payments = NormalizePayments(request.Payments, pricing.TotalAmount);
        var linePlans = await BuildFifoPlansAsync(conn, tx, request.WarehouseId, pricing.Lines, cancellationToken);

        var orderId = await InsertSaleHeaderAsync(
            conn, tx, orderNumber, branchId, request.WarehouseId, request.CustomerId, employeeId,
            SalesOrderStatuses.Completed, request.PriceType, pricing.SubtotalGross, pricing.OrderDiscountAmount,
            request.OrderDiscountType, request.OrderDiscountValue ?? 0, pricing.TotalAmount, request.Notes,
            shiftId);

        await InsertCompletedLinesAsync(conn, tx, orderId, request.WarehouseId, linePlans, cancellationToken);
        await InsertPaymentsAsync(conn, tx, orderId, payments);

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
                pricing.TotalAmount,
                status = SalesOrderStatuses.Completed,
                salesShiftId = shiftId,
            },
            userId,
            cancellationToken);

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
        var pricing = SalesPricing.PriceOrder(pricedInputs, orderDiscount);
        SalesPricing.ValidateDiscounts(pricing, discountPolicy);

        var orderId = await InsertSaleHeaderAsync(
            conn, tx, orderNumber, branchId, request.WarehouseId, request.CustomerId, employeeId,
            SalesOrderStatuses.Draft, request.PriceType, pricing.SubtotalGross, pricing.OrderDiscountAmount,
            request.OrderDiscountType, request.OrderDiscountValue ?? 0, pricing.TotalAmount, request.Notes);

        foreach (var line in pricing.Lines)
        {
            await InsertDraftItemAsync(conn, tx, orderId, line);
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
        var pricing = SalesPricing.PriceOrder(pricedInputs, orderDiscount);
        SalesPricing.ValidateDiscounts(pricing, discountPolicy);

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
        foreach (var line in pricing.Lines)
        {
            await InsertDraftItemAsync(conn, tx, id, line);
        }

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteDraftSaleAsync(
        Guid id,
        IReadOnlyList<CreateSalePaymentRequest>? payments,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string headerSql = """
            SELECT
                warehouse_id AS WarehouseId, price_type AS PriceType, subtotal AS Subtotal,
                discount_amount AS OrderDiscountAmount, total_amount AS TotalAmount,
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

        const string draftItemsSql = """
            SELECT
                product_id AS ProductId, product_unit_id AS ProductUnitId, quantity AS Quantity,
                discount_type AS DiscountType, discount_value AS DiscountValue
            FROM sales_order_items WHERE sales_order_id = @OrderId
            """;
        var draftItems = (await conn.QueryAsync<CreateSaleLineRequest>(draftItemsSql, new { OrderId = id }, tx)).ToList();
        if (draftItems.Count == 0)
            throw new InvalidOperationException("Đơn nháp không có dòng bán.");

        await conn.ExecuteAsync("DELETE FROM sales_order_items WHERE sales_order_id = @OrderId", new { OrderId = id }, tx);

        var pricedInputs = await ResolveSaleLineInputsAsync(conn, tx, draftItems, header.PriceType, cancellationToken);
        var orderDiscount = new SaleDiscountInput(header.OrderDiscountType, header.OrderDiscountValue);
        var pricing = SalesPricing.PriceOrder(pricedInputs, orderDiscount);
        var paymentList = NormalizePayments(payments, pricing.TotalAmount);
        var linePlans = await BuildFifoPlansAsync(conn, tx, header.WarehouseId, pricing.Lines, cancellationToken);

        await conn.ExecuteAsync("""
            UPDATE sales_orders SET
                status = @Completed, subtotal = @Subtotal, discount_amount = @OrderDiscountAmount,
                total_amount = @TotalAmount, sales_shift_id = @ShiftId
            WHERE id = @Id AND tenant_id = @TenantId
            """, new
        {
            Id = id,
            TenantId,
            Completed = SalesOrderStatuses.Completed,
            Subtotal = pricing.SubtotalGross,
            OrderDiscountAmount = pricing.OrderDiscountAmount,
            TotalAmount = pricing.TotalAmount,
            ShiftId = shiftId,
        }, tx);

        await InsertCompletedLinesAsync(conn, tx, id, header.WarehouseId, linePlans, cancellationToken);
        await InsertPaymentsAsync(conn, tx, id, paymentList);

        var orderNumber = await conn.QuerySingleAsync<string>(
            "SELECT order_number FROM sales_orders WHERE id = @Id",
            new { Id = id }, tx);
        var customerId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT customer_id FROM sales_orders WHERE id = @Id",
            new { Id = id }, tx);

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
                totalAmount = pricing.TotalAmount,
                status = SalesOrderStatuses.Completed,
                salesShiftId = shiftId,
            },
            _tenant.UserId,
            cancellationToken);

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
                   discount_amount AS OrderDiscountAmount
            FROM sales_orders WHERE id = @Id AND tenant_id = @TenantId
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

            var refundAmount = SalesPricing.ComputeLineRefundAmount(
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

        var refundPayments = NormalizePayments(request.Payments, totalRefund);
        await InsertReturnPaymentsAsync(conn, tx, returnId, refundPayments);

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
            WHERE r.tenant_id = @TenantId
              AND (
                  @Search IS NULL
                  OR r.return_number ILIKE @SearchPattern
                  OR o.order_number ILIKE @SearchPattern
              )
            ORDER BY r.return_date DESC
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        var rows = await conn.QueryAsync<ReturnListRow>(sql, new
        {
            TenantId,
            Search = pattern is null ? null : search!.Trim(),
            SearchPattern = pattern,
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
        return shiftId is null ? null : await GetShiftAsync(conn, shiftId.Value);
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
            throw new InvalidOperationException("Kho này đang có ca mở. Hãy đóng ca trước khi mở ca mới.");

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
        return (await GetShiftAsync(shiftId, cancellationToken))!;
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
        if (batchMode == TenantBatchMode.Off)
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

    private async Task<List<PlannedSaleLine>> BuildFifoPlansAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        IReadOnlyList<PricedSaleLineResult> lines,
        CancellationToken cancellationToken)
    {
        var linePlans = new List<PlannedSaleLine>();
        foreach (var pricedLine in lines)
        {
            var baseQtyNeeded = pricedLine.Quantity * pricedLine.ConversionFactor;
            IReadOnlyList<FifoAllocationResult> allocations;
            try
            {
                allocations = await _batchResolver.AllocateFifoAsync(
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
                var batches = await _batchResolver.GetAvailableBatchesAsync(
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
                    alloc.BaseQuantity));
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
        string? notes,
        Guid? salesShiftId = null)
    {
        const string sql = """
            INSERT INTO sales_orders (
                tenant_id, order_number, branch_id, warehouse_id, customer_id, employee_id,
                status, price_type, subtotal, discount_amount, order_discount_type, order_discount_value,
                total_amount, notes, sales_shift_id
            )
            VALUES (
                @TenantId, @OrderNumber, @BranchId, @WarehouseId, @CustomerId, @EmployeeId,
                @Status, @PriceType, @Subtotal, @OrderDiscountAmount, @OrderDiscountType, @OrderDiscountValue,
                @TotalAmount, @Notes, @SalesShiftId
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
            Notes = notes,
            SalesShiftId = salesShiftId,
        }, tx);
    }

    private static async Task InsertDraftItemAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        PricedSaleLineResult line)
    {
        const string sql = """
            INSERT INTO sales_order_items (
                sales_order_id, product_id, product_unit_id, batch_id,
                quantity, unit_price, discount_amount, discount_type, discount_value, line_total
            )
            VALUES (
                @OrderId, @ProductId, @ProductUnitId, NULL,
                @Quantity, @UnitPrice, @DiscountAmount, @DiscountType, @DiscountValue, @LineTotal
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
        }, tx);
    }

    private async Task InsertCompletedLinesAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid orderId,
        Guid warehouseId,
        List<PlannedSaleLine> linePlans,
        CancellationToken cancellationToken)
    {
        foreach (var plan in linePlans)
        {
            const string itemSql = """
                INSERT INTO sales_order_items (
                    sales_order_id, product_id, product_unit_id, batch_id, batch_source,
                    quantity, unit_price, discount_amount, discount_type, discount_value, line_total
                )
                VALUES (
                    @OrderId, @ProductId, @ProductUnitId, @BatchId, @BatchSource,
                    @Quantity, @UnitPrice, @DiscountAmount, @DiscountType, @DiscountValue, @LineTotal
                )
                RETURNING id
                """;
            var itemId = await conn.QuerySingleAsync<Guid>(itemSql, new
            {
                OrderId = orderId,
                plan.ProductId,
                plan.ProductUnitId,
                plan.BatchId,
                BatchSource = SalesBatchSources.FefoAuto,
                plan.Quantity,
                plan.UnitPrice,
                plan.DiscountAmount,
                DiscountType = plan.DiscountType,
                DiscountValue = plan.DiscountValue,
                plan.LineTotal,
            }, tx);

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
        decimal BaseQuantity);

    private sealed class DraftHeaderRow
    {
        public Guid WarehouseId { get; init; }
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

    private sealed class UnitRow
    {
        public decimal ConversionFactor { get; init; }
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
        public decimal TotalRefunded { get; init; }
        public string? Notes { get; init; }
        public Guid? SalesShiftId { get; init; }
        public string? ShiftNumber { get; init; }
    }
}
