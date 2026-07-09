using System.Data;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Procurement;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Infrastructure;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class ProcurementRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly InventoryRepository _inventory;
    private readonly IPricingEngine _pricing;

    public ProcurementRepository(
        IDbConnectionFactory db,
        ITenantContext tenant,
        InventoryRepository inventory,
        IPricingEngine pricing)
    {
        _db = db;
        _tenant = tenant;
        _inventory = inventory;
        _pricing = pricing;
    }

    private Guid TenantId => _tenant.TenantId;

    private static void ApplyWarehouseScope(
        List<string> conditions,
        DynamicParameters parameters,
        string column,
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds)
    {
        if (warehouseId is Guid scopedId)
        {
            conditions.Add($"{column} = @WarehouseId");
            parameters.Add("WarehouseId", scopedId);
        }
        else if (allowedWarehouseIds is { Length: > 0 })
        {
            conditions.Add($"{column} = ANY(@AllowedWarehouseIds)");
            parameters.Add("AllowedWarehouseIds", allowedWarehouseIds);
        }
    }

    public async Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var extra = activeOnly ? " AND status = 1 AND deleted_at IS NULL" : " AND deleted_at IS NULL";
        var sql = $"""
            SELECT
                id AS Id, supplier_code AS SupplierCode, supplier_name AS SupplierName,
                tax_code AS TaxCode, contact_name AS ContactName, phone AS Phone,
                email AS Email, address AS Address, payment_terms AS PaymentTerms, status AS Status,
                is_placeholder AS IsPlaceholder, wholesale_facility_code AS WholesaleFacilityCode
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
                email AS Email, address AS Address, payment_terms AS PaymentTerms, status AS Status,
                is_placeholder AS IsPlaceholder, wholesale_facility_code AS WholesaleFacilityCode
            FROM suppliers
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SupplierDto>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> IsSupplierPlaceholderAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT is_placeholder FROM suppliers
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var value = await conn.QuerySingleOrDefaultAsync<bool?>(sql, new { Id = supplierId, TenantId });
        return value == true;
    }

    public async Task SetPurchaseOrderSupplierAsync(
        Guid purchaseOrderId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE purchase_orders
            SET supplier_id = @SupplierId, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = purchaseOrderId,
            SupplierId = supplierId,
            TenantId,
            Draft = PurchaseOrderStatuses.Draft,
        });
        if (rows == 0)
            throw new InvalidOperationException("Không cập nhật được NCC trên PO nháp.");
    }

    public async Task<bool> SupplierCodeExistsAsync(string supplierCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM suppliers
                WHERE tenant_id = @TenantId
                  AND supplier_code = @SupplierCode
                  AND deleted_at IS NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(sql, new { TenantId, SupplierCode = supplierCode.Trim() });
    }

    public async Task<Guid> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO suppliers (
                tenant_id, supplier_code, supplier_name, tax_code, contact_name,
                phone, email, address, payment_terms, wholesale_facility_code
            )
            VALUES (
                @TenantId, @SupplierCode, @SupplierName, @TaxCode, @ContactName,
                @Phone, @Email, @Address, @PaymentTerms, @WholesaleFacilityCode
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
            WholesaleFacilityCode = request.WholesaleFacilityCode?.Trim(),
        });
    }

    public async Task<bool> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE suppliers SET
                supplier_name = @SupplierName, tax_code = @TaxCode, contact_name = @ContactName,
                phone = @Phone, email = @Email, address = @Address,
                payment_terms = @PaymentTerms, wholesale_facility_code = @WholesaleFacilityCode,
                status = @Status, updated_at = NOW()
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
            WholesaleFacilityCode = request.WholesaleFacilityCode?.Trim(),
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

        var treatment = await ResolveVatTreatmentAsync(conn, tx, request.VatTreatmentId, cancellationToken);
        var (taxAmount, ratePercent) = ComputePoTax(subtotal, treatment);
        var totalAmount = subtotal + taxAmount;

        const string headerSql = """
            INSERT INTO purchase_orders (
                tenant_id, po_number, supplier_id, warehouse_id, expected_date,
                status, subtotal, vat_treatment_id, tax_rate_percent, tax_amount, total_amount, notes, created_by
            )
            VALUES (
                @TenantId, @PoNumber, @SupplierId, @WarehouseId, @ExpectedDate,
                @Status, @Subtotal, @VatTreatmentId, @TaxRatePercent, @TaxAmount, @TotalAmount, @Notes, @CreatedBy
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
            VatTreatmentId = request.VatTreatmentId,
            TaxRatePercent = ratePercent,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
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

        if (status == PurchaseOrderStatuses.Draft && request.SupplierId is Guid supplierId)
        {
            var supplierExists = await conn.QuerySingleAsync<bool>(
                """
                SELECT EXISTS(
                    SELECT 1 FROM suppliers WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL)
                """,
                new { Id = supplierId, TenantId },
                tx);
            if (!supplierExists)
                throw new InvalidOperationException("NCC không tồn tại.");
            await conn.ExecuteAsync(
                """
                UPDATE purchase_orders SET supplier_id = @SupplierId, updated_at = NOW()
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = id, SupplierId = supplierId, TenantId },
                tx);
        }

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

        var treatment = await ResolveVatTreatmentAsync(conn, tx, request.VatTreatmentId, cancellationToken);
        var (taxAmount, ratePercent) = ComputePoTax(subtotal, treatment);

        const string updateHeaderSql = """
            UPDATE purchase_orders SET
                expected_date = @ExpectedDate, notes = @Notes,
                subtotal = @Subtotal, vat_treatment_id = @VatTreatmentId,
                tax_rate_percent = @TaxRatePercent, tax_amount = @TaxAmount,
                total_amount = @TotalAmount, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await conn.ExecuteAsync(updateHeaderSql, new
        {
            Id = id,
            TenantId,
            request.ExpectedDate,
            request.Notes,
            Subtotal = subtotal,
            VatTreatmentId = request.VatTreatmentId,
            TaxRatePercent = ratePercent,
            TaxAmount = taxAmount,
            TotalAmount = subtotal + taxAmount,
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

    public async Task<(IReadOnlyList<PurchaseOrderListItemDto> Items, int Total)> GetPurchaseOrdersAsync(
        PurchaseOrderListFilter filter,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

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

        if (filter.WarehouseId is Guid filterWarehouseId)
        {
            conditions.Add("p.warehouse_id = @FilterWarehouseId");
            parameters.Add("FilterWarehouseId", filterWarehouseId);
        }
        else
        {
            ApplyWarehouseScope(conditions, parameters, "p.warehouse_id", null, allowedWarehouseIds);
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
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM purchase_orders p
            INNER JOIN suppliers s ON s.id = p.supplier_id
            INNER JOIN warehouses w ON w.id = p.warehouse_id
            WHERE {where}
            """;
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
            LIMIT @PageSize OFFSET @Offset
            """;
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await conn.QueryAsync<PurchaseOrderListItemDto>(sql, parameters)).ToList();
        return (items, total);
    }

    public async Task<LastPurchasePriceHintDto> GetLastPurchasePriceHintAsync(
        Guid supplierId,
        Guid productId,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var grnWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND gr.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var poWarehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND po.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var sql = $"""
            SELECT price AS UnitPrice, price_date AS PriceDate, source AS Source, document_number AS DocumentNumber
            FROM (
                SELECT gri.unit_cost AS price, gr.receipt_date AS price_date, 'grn' AS source, gr.grn_number AS document_number
                FROM goods_receipt_items gri
                INNER JOIN goods_receipts gr ON gr.id = gri.goods_receipt_id
                WHERE gr.tenant_id = @TenantId AND gr.supplier_id = @SupplierId AND gri.product_id = @ProductId
                  AND gr.status = @GrnCompleted AND gr.deleted_at IS NULL
                  {grnWarehouseFilter}

                UNION ALL

                SELECT poi.unit_price, po.order_date, 'po', po.po_number
                FROM purchase_order_items poi
                INNER JOIN purchase_orders po ON po.id = poi.purchase_order_id
                WHERE po.tenant_id = @TenantId AND po.supplier_id = @SupplierId AND poi.product_id = @ProductId
                  AND po.status <> @PoCancelled AND po.deleted_at IS NULL
                  {poWarehouseFilter}
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
            AllowedWarehouseIds = allowedWarehouseIds,
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
                p.subtotal AS Subtotal, p.tax_rate_percent AS TaxRatePercent,
                p.tax_amount AS TaxAmount,
                p.vat_treatment_id AS VatTreatmentId,
                vt.treatment_code AS VatTreatmentCode,
                vt.treatment_name AS VatTreatmentName,
                vt.is_not_subject AS VatIsNotSubject,
                p.total_amount AS TotalAmount,
                p.notes AS Notes, p.deleted_at AS DeletedAt
            FROM purchase_orders p
            INNER JOIN suppliers s ON s.id = p.supplier_id
            INNER JOIN warehouses w ON w.id = p.warehouse_id
            INNER JOIN procurement_vat_treatments vt ON vt.id = p.vat_treatment_id
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
            header.ExpectedDate, header.Subtotal, header.TaxAmount, header.TaxRatePercent,
            header.VatTreatmentId, header.VatTreatmentCode, header.VatTreatmentName, header.VatIsNotSubject,
            header.TotalAmount,
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
            if (po.WarehouseId != request.WarehouseId)
                throw new InvalidOperationException("Kho nhận phải khớp với PO.");

            var existingDraft = await GetDraftGoodsReceiptForPoAsync(conn, tx, poId, TenantId);
            if (existingDraft is not null)
                throw new InvalidOperationException(
                    $"PO đã có phiếu chờ nhập kho {existingDraft.GrnNumber}. Hoàn tất nhập kho hoặc hủy phiếu đó trước khi tạo phiếu mới.");
        }

        var grnNumber = await NextNumberAsync(conn, tx, "GRN", "goods_receipts", cancellationToken);
        var receiptDate = (request.ReceiptDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var treatment = await ResolveVatTreatmentAsync(conn, tx, request.VatTreatmentId, cancellationToken);
        var pricingLines = request.Items
            .Select(i => (
                i.Quantity,
                i.UnitCost,
                new ProcurementDiscountInput(i.DiscountType, i.DiscountValue)))
            .ToList();
        var pricing = _pricing.PriceGoodsReceipt(
            pricingLines,
            new ProcurementDiscountInput(request.OrderDiscountType, request.OrderDiscountValue),
            treatment.RatePercent,
            treatment.IsNotSubject);

        const string headerSql = """
            INSERT INTO goods_receipts (
                tenant_id, grn_number, purchase_order_id, supplier_id, warehouse_id,
                status, receipt_date, received_by, notes, supplier_invoice_number,
                vat_treatment_id, tax_rate_percent,
                subtotal_gross, line_discount_total, merchandise_net,
                order_discount_type, order_discount_value, order_discount_amount,
                tax_amount, total_amount
            )
            VALUES (
                @TenantId, @GrnNumber, @PurchaseOrderId, @SupplierId, @WarehouseId,
                @Status, @ReceiptDate, @ReceivedBy, @Notes, @SupplierInvoiceNumber,
                @VatTreatmentId, @TaxRatePercent,
                @SubtotalGross, @LineDiscountTotal, @MerchandiseNet,
                @OrderDiscountType, @OrderDiscountValue, @OrderDiscountAmount,
                @TaxAmount, @TotalAmount
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
            SupplierInvoiceNumber = string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber)
                ? null
                : request.SupplierInvoiceNumber.Trim(),
            VatTreatmentId = request.VatTreatmentId,
            TaxRatePercent = treatment.RatePercent,
            SubtotalGross = pricing.SubtotalGross,
            LineDiscountTotal = pricing.LineDiscountTotal,
            MerchandiseNet = pricing.MerchandiseNet,
            OrderDiscountType = request.OrderDiscountType,
            OrderDiscountValue = request.OrderDiscountValue ?? 0,
            OrderDiscountAmount = pricing.OrderDiscountAmount,
            TaxAmount = pricing.TaxAmount,
            TotalAmount = pricing.TotalAmount,
        }, tx);

        const string itemSql = """
            INSERT INTO goods_receipt_items (
                tenant_id, goods_receipt_id, purchase_order_item_id, product_id, product_unit_id,
                batch_number, manufacture_date, expiry_date, quantity, unit_cost,
                discount_type, discount_value, discount_amount, line_total, inventory_unit_cost
            )
            VALUES (
                @TenantId, @GrnId, @PurchaseOrderItemId, @ProductId, @ProductUnitId,
                @BatchNumber, @ManufactureDate, @ExpiryDate, @Quantity, @UnitCost,
                @DiscountType, @DiscountValue, @DiscountAmount, @LineTotal, @InventoryUnitCost
            )
            """;
        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            var priced = pricing.Lines[index];
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
                DiscountType = priced.DiscountType,
                DiscountValue = priced.DiscountValue,
                DiscountAmount = priced.DiscountAmount,
                LineTotal = priced.LineNetTotal,
                InventoryUnitCost = priced.InventoryUnitCost,
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

        var supplierPlaceholder = await conn.QuerySingleAsync<bool>(
            """
            SELECT COALESCE(is_placeholder, FALSE)
            FROM suppliers WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { Id = header.SupplierId, TenantId },
            tx);
        if (supplierPlaceholder)
            throw new InvalidOperationException("Chọn NCC thật trước khi hoàn tất phiếu nhập.");

        const string itemsSql = """
            SELECT
                id AS Id, purchase_order_item_id AS PurchaseOrderItemId, product_id AS ProductId,
                batch_number AS BatchNumber, manufacture_date AS ManufactureDate, expiry_date AS ExpiryDate,
                quantity AS Quantity, unit_cost AS UnitCost, inventory_unit_cost AS InventoryUnitCost
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
                item.Quantity, item.InventoryUnitCost, null, cancellationToken);

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
            throw new InvalidOperationException("Không hủy được phiếu nhập (chỉ hủy phiếu chờ nhập kho).");
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
            UnitCost = item.InventoryUnitCost,
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

    public async Task<(IReadOnlyList<GoodsReceiptListItemDto> Items, int Total)> GetGoodsReceiptsAsync(
        GoodsReceiptListFilter filter,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

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

        if (filter.WarehouseId is Guid filterWarehouseId)
        {
            conditions.Add("g.warehouse_id = @FilterWarehouseId");
            parameters.Add("FilterWarehouseId", filterWarehouseId);
        }
        else
        {
            ApplyWarehouseScope(conditions, parameters, "g.warehouse_id", null, allowedWarehouseIds);
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
        var countSql = $"""
            SELECT COUNT(*)::int
            FROM goods_receipts g
            INNER JOIN suppliers s ON s.id = g.supplier_id
            INNER JOIN warehouses w ON w.id = g.warehouse_id
            LEFT JOIN purchase_orders p ON p.id = g.purchase_order_id
            WHERE {where}
            """;
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
            LIMIT @PageSize OFFSET @Offset
            """;
        parameters.Add("PageSize", pageSize);
        parameters.Add("Offset", offset);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, parameters);
        var items = (await conn.QueryAsync<GoodsReceiptListItemDto>(sql, parameters)).ToList();
        return (items, total);
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
                g.notes AS Notes, g.supplier_invoice_number AS SupplierInvoiceNumber, g.deleted_at AS DeletedAt,
                g.subtotal_gross AS SubtotalGross, g.line_discount_total AS LineDiscountTotal,
                g.merchandise_net AS MerchandiseNet, g.order_discount_type AS OrderDiscountType,
                g.order_discount_value AS OrderDiscountValue, g.order_discount_amount AS OrderDiscountAmount,
                g.vat_treatment_id AS VatTreatmentId, vt.treatment_code AS VatTreatmentCode,
                vt.treatment_name AS VatTreatmentName, vt.is_not_subject AS VatIsNotSubject,
                g.tax_rate_percent AS TaxRatePercent, g.tax_amount AS TaxAmount, g.total_amount AS TotalAmount
            FROM goods_receipts g
            INNER JOIN suppliers s ON s.id = g.supplier_id
            INNER JOIN warehouses w ON w.id = g.warehouse_id
            LEFT JOIN purchase_orders p ON p.id = g.purchase_order_id
            LEFT JOIN procurement_vat_treatments vt ON vt.id = g.vat_treatment_id
            WHERE g.id = @Id AND g.tenant_id = @TenantId{deletedFilter}
            """;
        const string itemsSql = """
            SELECT
                i.id AS Id, i.purchase_order_item_id AS PurchaseOrderItemId,
                i.product_id AS ProductId, pr.product_code AS ProductCode, pr.product_name AS ProductName,
                i.product_unit_id AS ProductUnitId, u.unit_name AS UnitName,
                i.batch_number AS BatchNumber, i.manufacture_date AS ManufactureDate,
                i.expiry_date AS ExpiryDate, i.quantity AS Quantity, i.unit_cost AS UnitCost,
                i.discount_type AS DiscountType, i.discount_value AS DiscountValue,
                i.discount_amount AS DiscountAmount, i.line_total AS LineTotal,
                i.inventory_unit_cost AS InventoryUnitCost
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
            header.Status, header.ReceiptDate, header.Notes, header.SupplierInvoiceNumber,
            header.SubtotalGross, header.LineDiscountTotal, header.MerchandiseNet,
            header.OrderDiscountType, header.OrderDiscountValue, header.OrderDiscountAmount,
            header.VatTreatmentId, header.VatTreatmentCode, header.VatTreatmentName, header.VatIsNotSubject,
            header.TaxRatePercent, header.TaxAmount, header.TotalAmount,
            items, header.DeletedAt);
    }

    public async Task<IReadOnlyList<SupplierPaymentListItemDto>> GetSupplierPaymentsAsync(
        SupplierPaymentListFilter filter,
        Guid[]? allowedWarehouseIds,
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

        if (allowedWarehouseIds is { Length: > 0 })
        {
            conditions.Add("""
                (
                    (gr.id IS NOT NULL AND gr.warehouse_id = ANY(@AllowedWarehouseIds))
                    OR (po.id IS NOT NULL AND po.warehouse_id = ANY(@AllowedWarehouseIds))
                )
                """);
            parameters.Add("AllowedWarehouseIds", allowedWarehouseIds);
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

    public async Task<IReadOnlyList<GrnPayableSourceRow>> GetGrnPayableSourceRowsAsync(
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND gr.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var sql = $"""
            WITH grn_totals AS (
                SELECT
                    gr.id AS GrnId,
                    gr.supplier_id AS SupplierId,
                    s.supplier_code AS SupplierCode,
                    s.supplier_name AS SupplierName,
                    s.payment_terms AS PaymentTerms,
                    gr.grn_number AS GrnNumber,
                    gr.receipt_date AS ReceiptDate,
                    CASE
                        WHEN gr.total_amount > 0 THEN gr.total_amount
                        ELSE COALESCE(SUM(gri.line_total), 0)
                    END AS GrnTotal
                FROM goods_receipts gr
                INNER JOIN suppliers s ON s.id = gr.supplier_id
                INNER JOIN goods_receipt_items gri ON gri.goods_receipt_id = gr.id
                WHERE gr.tenant_id = @TenantId
                  AND gr.status = @GrnCompleted
                  AND gr.deleted_at IS NULL
                  AND s.deleted_at IS NULL
                  AND COALESCE(s.is_placeholder, FALSE) = FALSE
                  {warehouseFilter}
                GROUP BY gr.id, gr.supplier_id, s.supplier_code, s.supplier_name, s.payment_terms,
                         gr.grn_number, gr.receipt_date, gr.total_amount
            ),
            grn_paid AS (
                SELECT goods_receipt_id AS GrnId, COALESCE(SUM(amount), 0) AS PaidAmount
                FROM supplier_payments
                WHERE tenant_id = @TenantId
                  AND status = @PaymentPosted
                  AND goods_receipt_id IS NOT NULL
                GROUP BY goods_receipt_id
            )
            SELECT
                g.SupplierId,
                g.SupplierCode,
                g.SupplierName,
                g.PaymentTerms,
                g.GrnId,
                g.GrnNumber,
                g.ReceiptDate,
                g.GrnTotal,
                COALESCE(p.PaidAmount, 0) AS GrnLinkedPaid
            FROM grn_totals g
            LEFT JOIN grn_paid p ON p.GrnId = g.GrnId
            ORDER BY g.SupplierName, g.ReceiptDate, g.GrnNumber
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<GrnPayableSourceRow>(sql, new
        {
            TenantId,
            GrnCompleted = GoodsReceiptStatuses.Completed,
            PaymentPosted = SupplierPaymentStatuses.Posted,
            AllowedWarehouseIds = allowedWarehouseIds,
        })).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetUnlinkedSupplierPaymentTotalsAsync(
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND po.id IS NOT NULL AND po.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;
        var sql = $"""
            SELECT sp.supplier_id AS SupplierId, COALESCE(SUM(sp.amount), 0) AS TotalPaid
            FROM supplier_payments sp
            LEFT JOIN purchase_orders po ON po.id = sp.purchase_order_id AND po.tenant_id = sp.tenant_id
            WHERE sp.tenant_id = @TenantId
              AND sp.status = @PaymentPosted
              AND sp.goods_receipt_id IS NULL
              {warehouseFilter}
            GROUP BY sp.supplier_id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid SupplierId, decimal TotalPaid)>(sql, new
        {
            TenantId,
            PaymentPosted = SupplierPaymentStatuses.Posted,
            AllowedWarehouseIds = allowedWarehouseIds,
        });
        return rows.ToDictionary(x => x.SupplierId, x => x.TotalPaid);
    }

    public async Task<IReadOnlyList<ProcurementVatTreatmentDto>> GetVatTreatmentsAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await EnsureDefaultVatTreatmentsAsync(conn, cancellationToken);

        var filter = activeOnly ? " AND is_active = true" : "";
        var sql = $"""
            SELECT
                {VatTreatmentSelectColumns}
            FROM procurement_vat_treatments
            WHERE tenant_id = @TenantId{filter}
            ORDER BY sort_order, treatment_name
            """;
        return (await conn.QueryAsync<ProcurementVatTreatmentDto>(sql, new { TenantId })).ToList();
    }

    private async Task EnsureDefaultVatTreatmentsAsync(
        System.Data.Common.DbConnection conn,
        CancellationToken cancellationToken)
    {
        const string countSql = """
            SELECT COUNT(*)::int FROM procurement_vat_treatments WHERE tenant_id = @TenantId
            """;
        var count = await conn.QuerySingleAsync<int>(countSql, new { TenantId });
        if (count > 0)
            return;

        const string seedSql = """
            INSERT INTO procurement_vat_treatments (
                tenant_id, treatment_code, treatment_name, rate_percent, is_not_subject, sort_order
            )
            VALUES
                (@TenantId, 'kct', 'Không chịu thuế GTGT (KCT)', 0, true, 0),
                (@TenantId, 'vat_0', 'Thuế suất 0%', 0, false, 1),
                (@TenantId, 'vat_5', 'Thuế suất 5%', 5, false, 2),
                (@TenantId, 'vat_8', 'Thuế suất 8%', 8, false, 3),
                (@TenantId, 'vat_10', 'Thuế suất 10%', 10, false, 4)
            ON CONFLICT (tenant_id, treatment_code) DO NOTHING
            """;
        await conn.ExecuteAsync(seedSql, new { TenantId });
    }

    public async Task<ProcurementVatTreatmentDto?> GetVatTreatmentAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                {VatTreatmentSelectColumns}
            FROM procurement_vat_treatments
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ProcurementVatTreatmentDto>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> DeleteVatTreatmentAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var item = await GetVatTreatmentAsync(id, cancellationToken);
        if (item is null)
            return false;

        if (!item.CanDelete)
            return false;

        const string sql = """
            DELETE FROM procurement_vat_treatments
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    private const string VatTreatmentSelectColumns = """
        id AS Id, treatment_code AS TreatmentCode, treatment_name AS TreatmentName,
        rate_percent AS RatePercent, is_not_subject AS IsNotSubject,
        sort_order AS SortOrder, is_active AS IsActive,
        (
            treatment_code NOT IN ('kct', 'vat_0', 'vat_5', 'vat_8', 'vat_10')
            AND NOT EXISTS (
                SELECT 1 FROM purchase_orders po
                WHERE po.vat_treatment_id = procurement_vat_treatments.id
                  AND po.tenant_id = @TenantId
            )
        ) AS CanDelete
        """;

    public async Task<bool> VatTreatmentCodeExistsAsync(string treatmentCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM procurement_vat_treatments
            WHERE tenant_id = @TenantId AND treatment_code = @TreatmentCode
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<int?>(sql, new { TenantId, TreatmentCode = treatmentCode }) is not null;
    }

    public async Task<Guid> CreateVatTreatmentAsync(
        CreateProcurementVatTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateVatTreatmentInput(request.TreatmentCode, request.RatePercent, request.IsNotSubject);
        const string sql = """
            INSERT INTO procurement_vat_treatments (
                tenant_id, treatment_code, treatment_name, rate_percent, is_not_subject, sort_order
            )
            VALUES (@TenantId, @TreatmentCode, @TreatmentName, @RatePercent, @IsNotSubject, @SortOrder)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            request.TreatmentCode,
            request.TreatmentName,
            request.RatePercent,
            request.IsNotSubject,
            request.SortOrder,
        });
    }

    public async Task<bool> UpdateVatTreatmentAsync(
        Guid id,
        UpdateProcurementVatTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateVatTreatmentInput(null, request.RatePercent, request.IsNotSubject);
        const string sql = """
            UPDATE procurement_vat_treatments SET
                treatment_name = @TreatmentName,
                rate_percent = @RatePercent,
                is_not_subject = @IsNotSubject,
                sort_order = @SortOrder,
                is_active = @IsActive,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            request.TreatmentName,
            request.RatePercent,
            request.IsNotSubject,
            request.SortOrder,
            request.IsActive,
        });
        return rows > 0;
    }

    public async Task<bool> WarehouseExistsAsync(Guid warehouseId, CancellationToken cancellationToken) =>
        await _inventory.WarehouseExistsAsync(warehouseId, cancellationToken);

    public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        await _inventory.ProductExistsAsync(productId, cancellationToken);

    private sealed class VatTreatmentCalcRow
    {
        public Guid Id { get; init; }
        public decimal RatePercent { get; init; }
        public bool IsNotSubject { get; init; }
        public bool IsActive { get; init; }
    }

    private async Task<VatTreatmentCalcRow> ResolveVatTreatmentAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid treatmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, rate_percent AS RatePercent, is_not_subject AS IsNotSubject, is_active AS IsActive
            FROM procurement_vat_treatments
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        var row = await conn.QuerySingleOrDefaultAsync<VatTreatmentCalcRow>(
            sql,
            new { Id = treatmentId, TenantId },
            tx);
        if (row is null)
            throw new InvalidOperationException("Loại thuế GTGT không tồn tại.");
        if (!row.IsActive)
            throw new InvalidOperationException("Loại thuế GTGT đã ngừng dùng.");
        return row;
    }

    private static (decimal TaxAmount, short RatePercent) ComputePoTax(decimal subtotal, VatTreatmentCalcRow treatment)
    {
        if (treatment.IsNotSubject || treatment.RatePercent <= 0)
            return (0, 0);

        var rate = (short)treatment.RatePercent;
        return (ProcurementVatTax.ComputeTaxAmount(subtotal, treatment.RatePercent), rate);
    }

    private static async Task<GrnDraftRow?> GetDraftGoodsReceiptForPoAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid purchaseOrderId,
        Guid tenantId)
    {
        const string sql = """
            SELECT id AS Id, grn_number AS GrnNumber
            FROM goods_receipts
            WHERE tenant_id = @TenantId AND purchase_order_id = @PurchaseOrderId
              AND status = @Draft AND deleted_at IS NULL
            LIMIT 1
            """;
        return await conn.QuerySingleOrDefaultAsync<GrnDraftRow>(
            sql,
            new { TenantId = tenantId, PurchaseOrderId = purchaseOrderId, Draft = GoodsReceiptStatuses.Draft },
            tx);
    }

    private static void ValidateVatTreatmentInput(string? treatmentCode, decimal ratePercent, bool isNotSubject)
    {
        if (treatmentCode is not null && string.IsNullOrWhiteSpace(treatmentCode))
            throw new InvalidOperationException("Mã loại thuế không hợp lệ.");
        if (ratePercent < 0 || ratePercent > 100)
            throw new InvalidOperationException("Thuế suất phải từ 0 đến 100%.");
        if (isNotSubject && ratePercent != 0)
            throw new InvalidOperationException("Không chịu thuế (KCT) phải có thuế suất 0%.");
    }

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
        public short TaxRatePercent { get; init; }
        public Guid VatTreatmentId { get; init; }
        public string VatTreatmentCode { get; init; } = "";
        public string VatTreatmentName { get; init; } = "";
        public bool VatIsNotSubject { get; init; }
        public decimal TotalAmount { get; init; }
        public string? Notes { get; init; }
        public DateTime? DeletedAt { get; init; }
    }

    private sealed class GrnDraftRow
    {
        public Guid Id { get; init; }
        public string GrnNumber { get; init; } = "";
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
        public decimal InventoryUnitCost { get; init; }
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
        public string? SupplierInvoiceNumber { get; init; }
        public DateTime? DeletedAt { get; init; }
        public decimal SubtotalGross { get; init; }
        public decimal LineDiscountTotal { get; init; }
        public decimal MerchandiseNet { get; init; }
        public short? OrderDiscountType { get; init; }
        public decimal OrderDiscountValue { get; init; }
        public decimal OrderDiscountAmount { get; init; }
        public Guid? VatTreatmentId { get; init; }
        public string? VatTreatmentCode { get; init; }
        public string? VatTreatmentName { get; init; }
        public bool VatIsNotSubject { get; init; }
        public short TaxRatePercent { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal TotalAmount { get; init; }
    }
}

internal sealed record PurchaseOrderPaymentLink(Guid SupplierId, short Status);

internal sealed record GoodsReceiptPaymentLink(Guid SupplierId, Guid? PurchaseOrderId, short Status);
