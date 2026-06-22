using System.Data;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Inventory;
using PharmaCore.Application.Procurement;
using PharmaCore.Infrastructure.Data;
using PharmaCore.Infrastructure.Inventory;

namespace PharmaCore.Infrastructure.Procurement;

internal sealed class ProcurementRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly InventoryRepository _inventory;

    public ProcurementRepository(IDbConnectionFactory db, ITenantContext tenant, InventoryRepository inventory)
    {
        _db = db;
        _tenant = tenant;
        _inventory = inventory;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var extra = activeOnly ? " AND status = 1 AND deleted_at IS NULL" : " AND deleted_at IS NULL";
        var sql = $"""
            SELECT
                id AS Id, supplier_code AS SupplierCode, supplier_name AS SupplierName,
                tax_code AS TaxCode, contact_name AS ContactName, phone AS Phone,
                email AS Email, address AS Address, payment_terms AS PaymentTerms, status AS Status
            FROM suppliers
            WHERE tenant_id = @TenantId {extra}
            ORDER BY supplier_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<SupplierDto>(sql, new { TenantId })).ToList();
    }

    public async Task<SupplierDto?> GetSupplierAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id, supplier_code AS SupplierCode, supplier_name AS SupplierName,
                tax_code AS TaxCode, contact_name AS ContactName, phone AS Phone,
                email AS Email, address AS Address, payment_terms AS PaymentTerms, status AS Status
            FROM suppliers
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SupplierDto>(sql, new { Id = id, TenantId });
    }

    public async Task<Guid> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO suppliers (
                tenant_id, supplier_code, supplier_name, tax_code, contact_name,
                phone, email, address, payment_terms
            )
            VALUES (
                @TenantId, @SupplierCode, @SupplierName, @TaxCode, @ContactName,
                @Phone, @Email, @Address, @PaymentTerms
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            SupplierCode = request.SupplierCode.Trim(),
            SupplierName = request.SupplierName.Trim(),
            request.TaxCode,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.PaymentTerms,
        });
    }

    public async Task<bool> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE suppliers SET
                supplier_name = @SupplierName, tax_code = @TaxCode, contact_name = @ContactName,
                phone = @Phone, email = @Email, address = @Address,
                payment_terms = @PaymentTerms, status = @Status, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            SupplierName = request.SupplierName.Trim(),
            request.TaxCode,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.PaymentTerms,
            request.Status,
        }) > 0;
    }

    public async Task<bool> SoftDeleteSupplierAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE suppliers SET deleted_at = NOW(), status = 2, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    public async Task<bool> SupplierExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(SELECT 1 FROM suppliers WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { Id = id, TenantId });
    }

    public async Task<PurchaseOrderPaymentLink?> GetPurchaseOrderPaymentLinkAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT supplier_id AS SupplierId, status AS Status
            FROM purchase_orders
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PurchaseOrderPaymentLink>(sql, new { Id = id, TenantId });
    }

    public async Task<GoodsReceiptPaymentLink?> GetGoodsReceiptPaymentLinkAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT supplier_id AS SupplierId, purchase_order_id AS PurchaseOrderId, status AS Status
            FROM goods_receipts
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<GoodsReceiptPaymentLink>(sql, new { Id = id, TenantId });
    }

    public async Task<string> NextNumberAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string prefix,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT(*)::int + 1 FROM {tableName} WHERE tenant_id = @TenantId";
        var seq = await conn.QuerySingleAsync<int>(sql, new { TenantId }, tx);
        return $"{prefix}-{seq:D6}";
    }

    public async Task<Guid> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var poNumber = await NextNumberAsync(conn, tx, "PO", "purchase_orders", cancellationToken);
        decimal subtotal = 0;
        foreach (var item in request.Items)
            subtotal += item.OrderedQty * item.UnitPrice;

        const string headerSql = """
            INSERT INTO purchase_orders (
                tenant_id, po_number, supplier_id, warehouse_id, expected_date,
                status, subtotal, tax_amount, total_amount, notes, created_by
            )
            VALUES (
                @TenantId, @PoNumber, @SupplierId, @WarehouseId, @ExpectedDate,
                @Status, @Subtotal, 0, @TotalAmount, @Notes, @CreatedBy
            )
            RETURNING id
            """;
        var poId = await conn.QuerySingleAsync<Guid>(headerSql, new
        {
            TenantId,
            PoNumber = poNumber,
            request.SupplierId,
            request.WarehouseId,
            request.ExpectedDate,
            Status = PurchaseOrderStatuses.Draft,
            Subtotal = subtotal,
            TotalAmount = subtotal,
            request.Notes,
            CreatedBy = createdBy,
        }, tx);

        const string itemSql = """
            INSERT INTO purchase_order_items (
                tenant_id, purchase_order_id, product_id, product_unit_id, ordered_qty, unit_price, line_total
            )
            VALUES (@TenantId, @PoId, @ProductId, @ProductUnitId, @OrderedQty, @UnitPrice, @LineTotal)
            """;
        foreach (var item in request.Items)
        {
            await conn.ExecuteAsync(itemSql, new
            {
                TenantId,
                PoId = poId,
                item.ProductId,
                item.ProductUnitId,
                item.OrderedQty,
                item.UnitPrice,
                LineTotal = item.OrderedQty * item.UnitPrice,
            }, tx);
        }

        await tx.CommitAsync(cancellationToken);
        return poId;
    }

    public async Task<bool> UpdatePurchaseOrderAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string poSql = """
            SELECT status AS Status
            FROM purchase_orders
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        var status = await conn.QuerySingleOrDefaultAsync<short?>(poSql, new { Id = id, TenantId }, tx);
        if (status is null)
            return false;

        if (status is not (
            PurchaseOrderStatuses.Draft or
            PurchaseOrderStatuses.Approved or
            PurchaseOrderStatuses.PartiallyReceived))
            throw new InvalidOperationException("Không sửa được đơn ở trạng thái này.");

        const string existingSql = """
            SELECT
                id AS Id, product_id AS ProductId, product_unit_id AS ProductUnitId,
                received_qty AS ReceivedQty, ordered_qty AS OrderedQty, unit_price AS UnitPrice
            FROM purchase_order_items
            WHERE purchase_order_id = @PoId
            """;
        var existing = (await conn.QueryAsync<PoItemEditRow>(existingSql, new { PoId = id }, tx)).ToList();
        var existingById = existing.ToDictionary(x => x.Id);

        var keepIds = request.Items
            .Where(i => i.Id is Guid)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        foreach (var row in existing)
        {
            if (keepIds.Contains(row.Id))
                continue;
            if (row.ReceivedQty > 0)
                throw new InvalidOperationException("Không xóa được dòng đã nhận hàng.");
            await conn.ExecuteAsync(
                "DELETE FROM purchase_order_items WHERE id = @Id AND purchase_order_id = @PoId",
                new { row.Id, PoId = id },
                tx);
        }

        decimal subtotal = 0;
        foreach (var item in request.Items)
        {
            var lineTotal = item.OrderedQty * item.UnitPrice;
            subtotal += lineTotal;

            if (item.Id is Guid itemId)
            {
                if (!existingById.TryGetValue(itemId, out var current))
                    throw new InvalidOperationException("Dòng PO không tồn tại.");

                if (current.ReceivedQty > 0)
                {
                    if (current.ProductId != item.ProductId ||
                        current.ProductUnitId != item.ProductUnitId ||
                        current.OrderedQty != item.OrderedQty ||
                        current.UnitPrice != item.UnitPrice)
                        throw new InvalidOperationException("Không sửa được dòng đã nhận hàng.");
                }
                else
                {
                    if (current.ProductId != item.ProductId || current.ProductUnitId != item.ProductUnitId)
                        throw new InvalidOperationException("Không đổi sản phẩm trên dòng đã tạo. Xóa và thêm dòng mới.");
                    if (current.UnitPrice != item.UnitPrice)
                        throw new InvalidOperationException("Không đổi đơn giá PO. Giá thực nhập tại phiếu nhập.");
                    if (item.OrderedQty < current.OrderedQty)
                        throw new InvalidOperationException("Chỉ được tăng SL đặt hoặc xóa dòng chưa nhận.");
                }

                const string updateItemSql = """
                    UPDATE purchase_order_items SET
                        product_id = @ProductId, product_unit_id = @ProductUnitId,
                        ordered_qty = @OrderedQty, unit_price = @UnitPrice, line_total = @LineTotal
                    WHERE id = @Id AND purchase_order_id = @PoId
                    """;
                await conn.ExecuteAsync(updateItemSql, new
                {
                    Id = itemId,
                    PoId = id,
                    item.ProductId,
                    item.ProductUnitId,
                    item.OrderedQty,
                    item.UnitPrice,
                    LineTotal = lineTotal,
                }, tx);
            }
            else
            {
                const string insertItemSql = """
                    INSERT INTO purchase_order_items (
                        tenant_id, purchase_order_id, product_id, product_unit_id,
                        ordered_qty, unit_price, line_total
                    )
                    VALUES (@TenantId, @PoId, @ProductId, @ProductUnitId, @OrderedQty, @UnitPrice, @LineTotal)
                    """;
                await conn.ExecuteAsync(insertItemSql, new
                {
                    TenantId,
                    PoId = id,
                    item.ProductId,
                    item.ProductUnitId,
                    item.OrderedQty,
                    item.UnitPrice,
                    LineTotal = lineTotal,
                }, tx);
            }
        }

        const string updateHeaderSql = """
            UPDATE purchase_orders SET
                expected_date = @ExpectedDate, notes = @Notes,
                subtotal = @Subtotal, total_amount = @TotalAmount, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await conn.ExecuteAsync(updateHeaderSql, new
        {
            Id = id,
            TenantId,
            request.ExpectedDate,
            request.Notes,
            Subtotal = subtotal,
            TotalAmount = subtotal,
        }, tx);

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    private sealed class PoItemEditRow
    {
        public Guid Id { get; init; }
        public Guid ProductId { get; init; }
        public Guid ProductUnitId { get; init; }
        public decimal ReceivedQty { get; init; }
        public decimal OrderedQty { get; init; }
        public decimal UnitPrice { get; init; }
    }

    public async Task<IReadOnlyList<PurchaseOrderListItemDto>> GetPurchaseOrdersAsync(
        PurchaseOrderListFilter filter,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "p.tenant_id = @TenantId" };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(
                "(p.po_number ILIKE @Search OR s.supplier_name ILIKE @Search OR s.supplier_code ILIKE @Search)");
            parameters.Add("Search", $"%{filter.Search.Trim()}%");
        }

        if (filter.SupplierId is Guid supplierId)
        {
            conditions.Add("p.supplier_id = @SupplierId");
            parameters.Add("SupplierId", supplierId);
        }

        if (filter.WarehouseId is Guid warehouseId)
        {
            conditions.Add("p.warehouse_id = @WarehouseId");
            parameters.Add("WarehouseId", warehouseId);
        }

        if (filter.Status is short status)
        {
            conditions.Add("p.status = @Status");
            parameters.Add("Status", status);
        }

        if (filter.DateFrom is DateOnly dateFrom)
        {
            conditions.Add("p.order_date >= @DateFrom");
            parameters.Add("DateFrom", dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (filter.DateTo is DateOnly dateTo)
        {
            conditions.Add("p.order_date < @DateToExclusive");
            parameters.Add("DateToExclusive", dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (filter.ProductId is Guid productId)
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1 FROM purchase_order_items i
                    WHERE i.purchase_order_id = p.id AND i.product_id = @ProductId
                )
                """);
            parameters.Add("ProductId", productId);
        }

        if (filter.PendingReceiptOnly)
        {
            conditions.Add("p.status IN (@StatusApproved, @StatusPartial)");
            conditions.Add("""
                EXISTS (
                    SELECT 1 FROM purchase_order_items i
                    WHERE i.purchase_order_id = p.id AND i.received_qty < i.ordered_qty
                )
                """);
            parameters.Add("StatusApproved", PurchaseOrderStatuses.Approved);
            parameters.Add("StatusPartial", PurchaseOrderStatuses.PartiallyReceived);
        }

        if (!filter.IncludeArchived)
            conditions.Add("p.deleted_at IS NULL");

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT
                p.id AS Id, p.po_number AS PoNumber, p.supplier_id AS SupplierId,
                s.supplier_name AS SupplierName, p.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, p.status AS Status,
                p.order_date AS OrderDate, p.total_amount AS TotalAmount,
                (SELECT COUNT(*)::int FROM purchase_order_items i WHERE i.purchase_order_id = p.id) AS ItemCount,
                p.deleted_at AS DeletedAt
            FROM purchase_orders p
            INNER JOIN suppliers s ON s.id = p.supplier_id
            INNER JOIN warehouses w ON w.id = p.warehouse_id
            WHERE {where}
            ORDER BY p.order_date DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<PurchaseOrderListItemDto>(sql, parameters)).ToList();
    }

    public async Task<LastPurchasePriceHintDto> GetLastPurchasePriceHintAsync(
        Guid supplierId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT price AS UnitPrice, price_date AS PriceDate, source AS Source, document_number AS DocumentNumber
            FROM (
                SELECT gri.unit_cost AS price, gr.receipt_date AS price_date, 'grn' AS source, gr.grn_number AS document_number
                FROM goods_receipt_items gri
                INNER JOIN goods_receipts gr ON gr.id = gri.goods_receipt_id
                WHERE gr.tenant_id = @TenantId AND gr.supplier_id = @SupplierId AND gri.product_id = @ProductId
                  AND gr.status = @GrnCompleted AND gr.deleted_at IS NULL

                UNION ALL

                SELECT poi.unit_price, po.order_date, 'po', po.po_number
                FROM purchase_order_items poi
                INNER JOIN purchase_orders po ON po.id = poi.purchase_order_id
                WHERE po.tenant_id = @TenantId AND po.supplier_id = @SupplierId AND poi.product_id = @ProductId
                  AND po.status <> @PoCancelled AND po.deleted_at IS NULL
            ) recent
            ORDER BY price_date DESC
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<LastPurchasePriceHintDto>(sql, new
        {
            TenantId,
            SupplierId = supplierId,
            ProductId = productId,
            GrnCompleted = GoodsReceiptStatuses.Completed,
            PoCancelled = PurchaseOrderStatuses.Cancelled,
        });
        return row ?? new LastPurchasePriceHintDto(null, null, null, null);
    }

    public async Task<PurchaseOrderDetailDto?> GetPurchaseOrderAsync(
        Guid id,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var deletedFilter = includeArchived ? "" : " AND p.deleted_at IS NULL";
        var headerSql = $"""
            SELECT
                p.id AS Id, p.po_number AS PoNumber, p.supplier_id AS SupplierId,
                s.supplier_name AS SupplierName, p.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, p.status AS Status,
                p.order_date AS OrderDate, p.expected_date AS ExpectedDate,
                p.subtotal AS Subtotal, p.tax_amount AS TaxAmount, p.total_amount AS TotalAmount,
                p.notes AS Notes, p.deleted_at AS DeletedAt
            FROM purchase_orders p
            INNER JOIN suppliers s ON s.id = p.supplier_id
            INNER JOIN warehouses w ON w.id = p.warehouse_id
            WHERE p.id = @Id AND p.tenant_id = @TenantId{deletedFilter}
            """;
        const string itemsSql = """
            SELECT
                i.id AS Id, i.product_id AS ProductId, pr.product_code AS ProductCode,
                pr.product_name AS ProductName, i.product_unit_id AS ProductUnitId,
                u.unit_name AS UnitName, i.ordered_qty AS OrderedQty, i.received_qty AS ReceivedQty,
                i.unit_price AS UnitPrice, i.line_total AS LineTotal
            FROM purchase_order_items i
            INNER JOIN products pr ON pr.id = i.product_id
            INNER JOIN product_units u ON u.id = i.product_unit_id
            WHERE i.purchase_order_id = @PoId
            ORDER BY pr.product_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<PurchaseOrderHeaderRow>(headerSql, new { Id = id, TenantId });
        if (header is null) return null;
        var items = (await conn.QueryAsync<PurchaseOrderItemDto>(itemsSql, new { PoId = id })).ToList();
        return new PurchaseOrderDetailDto(
            header.Id, header.PoNumber, header.SupplierId, header.SupplierName,
            header.WarehouseId, header.WarehouseName, header.Status, header.OrderDate,
            header.ExpectedDate, header.Subtotal, header.TaxAmount, header.TotalAmount,
            header.Notes, items, header.DeletedAt);
    }

    public async Task<bool> TransitionPurchaseOrderStatusAsync(
        Guid id,
        short fromStatus,
        short toStatus,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var auditSet = toStatus switch
        {
            PurchaseOrderStatuses.Approved =>
                ", approved_at = NOW(), approved_by = @UserId",
            PurchaseOrderStatuses.Cancelled =>
                ", cancelled_at = NOW(), cancelled_by = @UserId",
            PurchaseOrderStatuses.Closed =>
                ", closed_at = NOW(), closed_by = @UserId",
            _ => "",
        };
        var sql = $"""
            UPDATE purchase_orders SET status = @ToStatus, updated_at = NOW(){auditSet}
            WHERE id = @Id AND tenant_id = @TenantId AND status = @FromStatus AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            UserId = userId,
        }) > 0;
    }

    public async Task<bool> SoftDeletePurchaseOrderAsync(
        Guid id,
        Guid deletedBy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE purchase_orders
            SET deleted_at = NOW(), deleted_by = @DeletedBy, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
              AND status = @Cancelled AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            DeletedBy = deletedBy,
            Cancelled = PurchaseOrderStatuses.Cancelled,
        }) > 0;
    }

    public async Task<bool> PurgePurchaseOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM purchase_orders
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NOT NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    public async Task<Guid> CreateGoodsReceiptAsync(
        CreateGoodsReceiptRequest request,
        Guid receivedBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        if (request.PurchaseOrderId is Guid poId)
        {
            const string poCheck = """
                SELECT supplier_id AS SupplierId, warehouse_id AS WarehouseId, status AS Status
                FROM purchase_orders
                WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
                """;
            var po = await conn.QuerySingleOrDefaultAsync<PoCheckRow>(
                poCheck, new { Id = poId, TenantId }, tx)
                ?? throw new InvalidOperationException("Đơn mua hàng không tồn tại.");

            if (po.Status != PurchaseOrderStatuses.Approved && po.Status != PurchaseOrderStatuses.PartiallyReceived)
                throw new InvalidOperationException("PO phải ở trạng thái Đã duyệt hoặc Nhận một phần.");
            if (po.SupplierId != request.SupplierId || po.WarehouseId != request.WarehouseId)
                throw new InvalidOperationException("NCC và kho nhận phải khớp với PO.");
        }

        var grnNumber = await NextNumberAsync(conn, tx, "GRN", "goods_receipts", cancellationToken);
        var receiptDate = (request.ReceiptDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        const string headerSql = """
            INSERT INTO goods_receipts (
                tenant_id, grn_number, purchase_order_id, supplier_id, warehouse_id,
                status, receipt_date, received_by, notes
            )
            VALUES (
                @TenantId, @GrnNumber, @PurchaseOrderId, @SupplierId, @WarehouseId,
                @Status, @ReceiptDate, @ReceivedBy, @Notes
            )
            RETURNING id
            """;
        var grnId = await conn.QuerySingleAsync<Guid>(headerSql, new
        {
            TenantId,
            GrnNumber = grnNumber,
            request.PurchaseOrderId,
            request.SupplierId,
            request.WarehouseId,
            Status = GoodsReceiptStatuses.Draft,
            ReceiptDate = receiptDate,
            ReceivedBy = receivedBy,
            request.Notes,
        }, tx);

        const string itemSql = """
            INSERT INTO goods_receipt_items (
                tenant_id, goods_receipt_id, purchase_order_item_id, product_id, product_unit_id,
                batch_number, manufacture_date, expiry_date, quantity, unit_cost, line_total
            )
            VALUES (
                @TenantId, @GrnId, @PurchaseOrderItemId, @ProductId, @ProductUnitId,
                @BatchNumber, @ManufactureDate, @ExpiryDate, @Quantity, @UnitCost, @LineTotal
            )
            """;
        foreach (var item in request.Items)
        {
            if (item.PurchaseOrderItemId is Guid poItemId)
            {
                const string poItemSql = """
                    SELECT ordered_qty AS OrderedQty, received_qty AS ReceivedQty
                    FROM purchase_order_items WHERE id = @Id
                    """;
                var poItem = await conn.QuerySingleOrDefaultAsync<PoItemQtyRow>(
                    poItemSql, new { Id = poItemId }, tx)
                    ?? throw new InvalidOperationException("Dòng PO không tồn tại.");
                if (poItem.ReceivedQty + item.Quantity > poItem.OrderedQty)
                    throw new InvalidOperationException("Số lượng nhận vượt quá PO.");
            }

            await conn.ExecuteAsync(itemSql, new
            {
                TenantId,
                GrnId = grnId,
                item.PurchaseOrderItemId,
                item.ProductId,
                item.ProductUnitId,
                BatchNumber = item.BatchNumber.Trim(),
                item.ManufactureDate,
                item.ExpiryDate,
                item.Quantity,
                item.UnitCost,
                LineTotal = item.Quantity * item.UnitCost,
            }, tx);
        }

        await tx.CommitAsync(cancellationToken);
        return grnId;
    }

    public async Task CompleteGoodsReceiptAsync(Guid grnId, Guid completedBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string headerSql = """
            SELECT id AS Id, purchase_order_id AS PurchaseOrderId, supplier_id AS SupplierId,
                   warehouse_id AS WarehouseId, status AS Status
            FROM goods_receipts
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            FOR UPDATE
            """;
        var header = await conn.QuerySingleOrDefaultAsync<GrnHeaderRow>(headerSql, new { Id = grnId, TenantId }, tx)
            ?? throw new InvalidOperationException("Phiếu nhập không tồn tại.");

        if (header.Status == GoodsReceiptStatuses.Completed)
            throw new InvalidOperationException("Phiếu đã hoàn tất.");
        if (header.Status == GoodsReceiptStatuses.Cancelled)
            throw new InvalidOperationException("Phiếu đã hủy.");

        const string itemsSql = """
            SELECT
                id AS Id, purchase_order_item_id AS PurchaseOrderItemId, product_id AS ProductId,
                batch_number AS BatchNumber, manufacture_date AS ManufactureDate, expiry_date AS ExpiryDate,
                quantity AS Quantity, unit_cost AS UnitCost
            FROM goods_receipt_items WHERE goods_receipt_id = @GrnId
            """;
        var items = (await conn.QueryAsync<GrnItemRow>(itemsSql, new { GrnId = grnId }, tx)).ToList();
        if (items.Count == 0)
            throw new InvalidOperationException("Phiếu không có dòng hàng.");

        foreach (var item in items)
        {
            var existingId = await _inventory.FindBatchIdByKeyAsync(
                conn, tx, header.WarehouseId, item.ProductId, item.BatchNumber, cancellationToken);

            Guid batchId;
            if (existingId is Guid id)
            {
                batchId = id;
                await _inventory.IncreaseBatchQuantityAsync(conn, tx, batchId, item.Quantity, cancellationToken);
                await conn.ExecuteAsync(
                    "UPDATE inventory_batches SET supplier_id = @SupplierId, goods_receipt_item_id = @GrnItemId, updated_at = NOW() WHERE id = @BatchId AND tenant_id = @TenantId",
                    new { SupplierId = header.SupplierId, GrnItemId = item.Id, BatchId = batchId, TenantId }, tx);
            }
            else
            {
                batchId = await InsertBatchFromGrnAsync(
                    conn, tx, header.WarehouseId, header.SupplierId, item, cancellationToken);
            }

            await _inventory.InsertMovementAsync(
                conn, tx, header.WarehouseId, batchId, item.ProductId,
                StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, grnId,
                item.Quantity, item.UnitCost, null, cancellationToken);

            if (item.PurchaseOrderItemId is Guid poItemId)
            {
                await conn.ExecuteAsync(
                    "UPDATE purchase_order_items SET received_qty = received_qty + @Qty WHERE id = @Id",
                    new { Qty = item.Quantity, Id = poItemId }, tx);
            }
        }

        if (header.PurchaseOrderId is Guid poId)
            await RefreshPurchaseOrderStatusAsync(conn, tx, poId, cancellationToken);

        await conn.ExecuteAsync(
            """
            UPDATE goods_receipts
            SET status = @Status, completed_at = NOW(), completed_by = @CompletedBy, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new { Id = grnId, TenantId, Status = GoodsReceiptStatuses.Completed, CompletedBy = completedBy }, tx);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task CancelGoodsReceiptAsync(Guid grnId, Guid cancelledBy, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE goods_receipts
            SET status = @Cancelled, cancelled_at = NOW(), cancelled_by = @CancelledBy, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = grnId,
            TenantId,
            Draft = GoodsReceiptStatuses.Draft,
            Cancelled = GoodsReceiptStatuses.Cancelled,
            CancelledBy = cancelledBy,
        });
        if (rows == 0)
            throw new InvalidOperationException("Không hủy được phiếu nhập (chỉ hủy phiếu Nháp).");
    }

    public async Task<bool> SoftDeleteGoodsReceiptAsync(
        Guid id,
        Guid deletedBy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE goods_receipts
            SET deleted_at = NOW(), deleted_by = @DeletedBy, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
              AND status = @Cancelled AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            DeletedBy = deletedBy,
            Cancelled = GoodsReceiptStatuses.Cancelled,
        }) > 0;
    }

    public async Task<bool> PurgeGoodsReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM goods_receipts
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NOT NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    private async Task<Guid> InsertBatchFromGrnAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid supplierId,
        GrnItemRow item,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory_batches (
                tenant_id, warehouse_id, product_id, batch_number,
                manufacture_date, expiry_date, unit_cost, quantity_received, quantity_available,
                supplier_id, goods_receipt_item_id
            )
            VALUES (
                @TenantId, @WarehouseId, @ProductId, @BatchNumber,
                @ManufactureDate, @ExpiryDate, @UnitCost, @Quantity, @Quantity,
                @SupplierId, @GrnItemId
            )
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            item.ProductId,
            BatchNumber = item.BatchNumber,
            item.ManufactureDate,
            item.ExpiryDate,
            item.UnitCost,
            item.Quantity,
            SupplierId = supplierId,
            GrnItemId = item.Id,
        }, tx);
    }

    private async Task RefreshPurchaseOrderStatusAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid poId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*)::int AS Total,
                COUNT(*) FILTER (WHERE received_qty >= ordered_qty)::int AS FullyReceived,
                COUNT(*) FILTER (WHERE received_qty > 0)::int AS AnyReceived
            FROM purchase_order_items WHERE purchase_order_id = @PoId
            """;
        var stats = await conn.QuerySingleAsync<(int Total, int FullyReceived, int AnyReceived)>(
            sql, new { PoId = poId }, tx);

        short status = stats.FullyReceived == stats.Total && stats.Total > 0
            ? PurchaseOrderStatuses.Received
            : stats.AnyReceived > 0
                ? PurchaseOrderStatuses.PartiallyReceived
                : PurchaseOrderStatuses.Approved;

        await conn.ExecuteAsync(
            "UPDATE purchase_orders SET status = @Status, updated_at = NOW() WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL",
            new { Id = poId, TenantId, Status = status }, tx);
    }

    public async Task<IReadOnlyList<GoodsReceiptListItemDto>> GetGoodsReceiptsAsync(
        GoodsReceiptListFilter filter,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "g.tenant_id = @TenantId" };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(
                "(g.grn_number ILIKE @Search OR COALESCE(p.po_number, '') ILIKE @Search OR s.supplier_name ILIKE @Search OR s.supplier_code ILIKE @Search)");
            parameters.Add("Search", $"%{filter.Search.Trim()}%");
        }

        if (filter.SupplierId is Guid supplierId)
        {
            conditions.Add("g.supplier_id = @SupplierId");
            parameters.Add("SupplierId", supplierId);
        }

        if (filter.WarehouseId is Guid warehouseId)
        {
            conditions.Add("g.warehouse_id = @WarehouseId");
            parameters.Add("WarehouseId", warehouseId);
        }

        if (filter.Status is short status)
        {
            conditions.Add("g.status = @Status");
            parameters.Add("Status", status);
        }

        if (filter.DateFrom is DateOnly dateFrom)
        {
            conditions.Add("g.receipt_date >= @DateFrom");
            parameters.Add("DateFrom", dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (filter.DateTo is DateOnly dateTo)
        {
            conditions.Add("g.receipt_date < @DateToExclusive");
            parameters.Add("DateToExclusive", dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (filter.PurchaseOrderId is Guid purchaseOrderId)
        {
            conditions.Add("g.purchase_order_id = @PurchaseOrderId");
            parameters.Add("PurchaseOrderId", purchaseOrderId);
        }

        if (filter.ProductId is Guid productId)
        {
            conditions.Add("""
                EXISTS (
                    SELECT 1 FROM goods_receipt_items i
                    WHERE i.goods_receipt_id = g.id AND i.product_id = @ProductId
                )
                """);
            parameters.Add("ProductId", productId);
        }

        if (!filter.IncludeArchived)
            conditions.Add("g.deleted_at IS NULL");

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT
                g.id AS Id, g.grn_number AS GrnNumber, g.supplier_id AS SupplierId,
                s.supplier_name AS SupplierName, g.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, g.purchase_order_id AS PurchaseOrderId,
                p.po_number AS PoNumber, g.status AS Status, g.receipt_date AS ReceiptDate,
                (SELECT COUNT(*)::int FROM goods_receipt_items i WHERE i.goods_receipt_id = g.id) AS ItemCount,
                g.deleted_at AS DeletedAt
            FROM goods_receipts g
            INNER JOIN suppliers s ON s.id = g.supplier_id
            INNER JOIN warehouses w ON w.id = g.warehouse_id
            LEFT JOIN purchase_orders p ON p.id = g.purchase_order_id
            WHERE {where}
            ORDER BY g.receipt_date DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<GoodsReceiptListItemDto>(sql, parameters)).ToList();
    }

    public async Task<GoodsReceiptDetailDto?> GetGoodsReceiptAsync(
        Guid id,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var deletedFilter = includeArchived ? "" : " AND g.deleted_at IS NULL";
        var headerSql = $"""
            SELECT
                g.id AS Id, g.grn_number AS GrnNumber, g.supplier_id AS SupplierId,
                s.supplier_name AS SupplierName, g.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName, g.purchase_order_id AS PurchaseOrderId,
                p.po_number AS PoNumber, g.status AS Status, g.receipt_date AS ReceiptDate,
                g.notes AS Notes, g.deleted_at AS DeletedAt
            FROM goods_receipts g
            INNER JOIN suppliers s ON s.id = g.supplier_id
            INNER JOIN warehouses w ON w.id = g.warehouse_id
            LEFT JOIN purchase_orders p ON p.id = g.purchase_order_id
            WHERE g.id = @Id AND g.tenant_id = @TenantId{deletedFilter}
            """;
        const string itemsSql = """
            SELECT
                i.id AS Id, i.purchase_order_item_id AS PurchaseOrderItemId,
                i.product_id AS ProductId, pr.product_code AS ProductCode, pr.product_name AS ProductName,
                i.product_unit_id AS ProductUnitId, u.unit_name AS UnitName,
                i.batch_number AS BatchNumber, i.manufacture_date AS ManufactureDate,
                i.expiry_date AS ExpiryDate, i.quantity AS Quantity, i.unit_cost AS UnitCost, i.line_total AS LineTotal
            FROM goods_receipt_items i
            INNER JOIN products pr ON pr.id = i.product_id
            INNER JOIN product_units u ON u.id = i.product_unit_id
            WHERE i.goods_receipt_id = @GrnId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<GrnDetailHeaderRow>(headerSql, new { Id = id, TenantId });
        if (header is null) return null;
        var items = (await conn.QueryAsync<GoodsReceiptItemDto>(itemsSql, new { GrnId = id })).ToList();
        return new GoodsReceiptDetailDto(
            header.Id, header.GrnNumber, header.SupplierId, header.SupplierName,
            header.WarehouseId, header.WarehouseName, header.PurchaseOrderId, header.PoNumber,
            header.Status, header.ReceiptDate, header.Notes, items, header.DeletedAt);
    }

    public async Task<IReadOnlyList<SupplierPaymentListItemDto>> GetSupplierPaymentsAsync(
        SupplierPaymentListFilter filter,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "sp.tenant_id = @TenantId", "sp.deleted_at IS NULL" };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", TenantId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add(
                "(sp.payment_number ILIKE @Search OR s.supplier_name ILIKE @Search OR s.supplier_code ILIKE @Search OR COALESCE(po.po_number, '') ILIKE @Search OR COALESCE(gr.grn_number, '') ILIKE @Search)");
            parameters.Add("Search", $"%{filter.Search.Trim()}%");
        }

        if (filter.SupplierId is Guid supplierId)
        {
            conditions.Add("sp.supplier_id = @SupplierId");
            parameters.Add("SupplierId", supplierId);
        }

        if (filter.Status is short status)
        {
            conditions.Add("sp.status = @Status");
            parameters.Add("Status", status);
        }

        if (filter.DateFrom is DateOnly dateFrom)
        {
            conditions.Add("sp.payment_date >= @DateFrom");
            parameters.Add("DateFrom", dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        if (filter.DateTo is DateOnly dateTo)
        {
            conditions.Add("sp.payment_date < @DateTo");
            parameters.Add("DateTo", dateTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        var where = string.Join(" AND ", conditions);
        var sql = $"""
            SELECT
                sp.id AS Id, sp.payment_number AS PaymentNumber, sp.supplier_id AS SupplierId,
                s.supplier_name AS SupplierName, sp.amount AS Amount, sp.payment_method AS PaymentMethod,
                sp.status AS Status, sp.payment_date AS PaymentDate, sp.posted_at AS PostedAt,
                sp.purchase_order_id AS PurchaseOrderId, po.po_number AS PoNumber,
                sp.goods_receipt_id AS GoodsReceiptId, gr.grn_number AS GrnNumber, sp.notes AS Notes
            FROM supplier_payments sp
            INNER JOIN suppliers s ON s.id = sp.supplier_id
            LEFT JOIN purchase_orders po ON po.id = sp.purchase_order_id
            LEFT JOIN goods_receipts gr ON gr.id = sp.goods_receipt_id
            WHERE {where}
            ORDER BY sp.payment_date DESC, sp.created_at DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<SupplierPaymentListItemDto>(sql, parameters)).ToList();
    }

    public async Task<SupplierPaymentListItemDto?> GetSupplierPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                sp.id AS Id, sp.payment_number AS PaymentNumber, sp.supplier_id AS SupplierId,
                s.supplier_name AS SupplierName, sp.amount AS Amount, sp.payment_method AS PaymentMethod,
                sp.status AS Status, sp.payment_date AS PaymentDate, sp.posted_at AS PostedAt,
                sp.purchase_order_id AS PurchaseOrderId, po.po_number AS PoNumber,
                sp.goods_receipt_id AS GoodsReceiptId, gr.grn_number AS GrnNumber, sp.notes AS Notes
            FROM supplier_payments sp
            INNER JOIN suppliers s ON s.id = sp.supplier_id
            LEFT JOIN purchase_orders po ON po.id = sp.purchase_order_id
            LEFT JOIN goods_receipts gr ON gr.id = sp.goods_receipt_id
            WHERE sp.id = @Id AND sp.tenant_id = @TenantId AND sp.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SupplierPaymentListItemDto>(sql, new { Id = id, TenantId });
    }

    public async Task<Guid> CreateSupplierPaymentAsync(
        CreateSupplierPaymentRequest request,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var paymentNumber = await NextNumberAsync(conn, tx, "PAY", "supplier_payments", cancellationToken);
        var paymentDate = (request.PaymentDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        const string sql = """
            INSERT INTO supplier_payments (
                tenant_id, supplier_id, purchase_order_id, goods_receipt_id,
                payment_number, amount, payment_method, payment_date, notes, status, created_by
            )
            VALUES (
                @TenantId, @SupplierId, @PurchaseOrderId, @GoodsReceiptId,
                @PaymentNumber, @Amount, @PaymentMethod, @PaymentDate, @Notes, @Status, @CreatedBy
            )
            RETURNING id
            """;
        var id = await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            request.SupplierId,
            request.PurchaseOrderId,
            request.GoodsReceiptId,
            PaymentNumber = paymentNumber,
            request.Amount,
            request.PaymentMethod,
            PaymentDate = paymentDate,
            request.Notes,
            Status = SupplierPaymentStatuses.Draft,
            CreatedBy = createdBy,
        }, tx);
        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<bool> UpdateSupplierPaymentAsync(Guid id, UpdateSupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        var paymentDate = request.PaymentDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        const string sql = """
            UPDATE supplier_payments SET
                supplier_id = @SupplierId,
                purchase_order_id = @PurchaseOrderId,
                goods_receipt_id = @GoodsReceiptId,
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
            request.SupplierId,
            request.PurchaseOrderId,
            request.GoodsReceiptId,
            request.Amount,
            request.PaymentMethod,
            PaymentDate = paymentDate,
            request.Notes,
            Draft = SupplierPaymentStatuses.Draft,
        });
        return rows > 0;
    }

    public async Task<bool> PostSupplierPaymentAsync(Guid id, Guid postedBy, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE supplier_payments SET
                status = @Posted,
                posted_at = NOW(),
                posted_by = @PostedBy
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            Draft = SupplierPaymentStatuses.Draft,
            Posted = SupplierPaymentStatuses.Posted,
            PostedBy = postedBy,
        });
        return rows > 0;
    }

    public async Task<bool> CancelSupplierPaymentAsync(Guid id, Guid cancelledBy, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE supplier_payments SET
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
            Draft = SupplierPaymentStatuses.Draft,
            Cancelled = SupplierPaymentStatuses.Cancelled,
            CancelledBy = cancelledBy,
        });
        return rows > 0;
    }

    public async Task<bool> WarehouseExistsAsync(Guid warehouseId, CancellationToken cancellationToken) =>
        await _inventory.WarehouseExistsAsync(warehouseId, cancellationToken);

    public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        await _inventory.ProductExistsAsync(productId, cancellationToken);

    private sealed class PoCheckRow
    {
        public Guid SupplierId { get; init; }
        public Guid WarehouseId { get; init; }
        public short Status { get; init; }
    }

    private sealed class PoItemQtyRow
    {
        public decimal OrderedQty { get; init; }
        public decimal ReceivedQty { get; init; }
    }

    private sealed class PurchaseOrderHeaderRow
    {
        public Guid Id { get; init; }
        public string PoNumber { get; init; } = "";
        public Guid SupplierId { get; init; }
        public string SupplierName { get; init; } = "";
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public short Status { get; init; }
        public DateTime OrderDate { get; init; }
        public DateOnly? ExpectedDate { get; init; }
        public decimal Subtotal { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public string? Notes { get; init; }
        public DateTime? DeletedAt { get; init; }
    }

    private sealed class GrnHeaderRow
    {
        public Guid Id { get; init; }
        public Guid? PurchaseOrderId { get; init; }
        public Guid SupplierId { get; init; }
        public Guid WarehouseId { get; init; }
        public short Status { get; init; }
    }

    private sealed class GrnItemRow
    {
        public Guid Id { get; init; }
        public Guid? PurchaseOrderItemId { get; init; }
        public Guid ProductId { get; init; }
        public string BatchNumber { get; init; } = "";
        public DateOnly? ManufactureDate { get; init; }
        public DateOnly ExpiryDate { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitCost { get; init; }
    }

    private sealed class GrnDetailHeaderRow
    {
        public Guid Id { get; init; }
        public string GrnNumber { get; init; } = "";
        public Guid SupplierId { get; init; }
        public string SupplierName { get; init; } = "";
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public Guid? PurchaseOrderId { get; init; }
        public string? PoNumber { get; init; }
        public short Status { get; init; }
        public DateTime ReceiptDate { get; init; }
        public string? Notes { get; init; }
        public DateTime? DeletedAt { get; init; }
    }
}

internal sealed record PurchaseOrderPaymentLink(Guid SupplierId, short Status);

internal sealed record GoodsReceiptPaymentLink(Guid SupplierId, Guid? PurchaseOrderId, short Status);
