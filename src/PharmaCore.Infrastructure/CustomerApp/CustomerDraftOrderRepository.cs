using System.Data;
using Dapper;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerDraftOrderRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerDraftOrderRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Guid?> ResolveDefaultWarehouseIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id FROM warehouses
            WHERE tenant_id = @TenantId AND deleted_at IS NULL AND status = 1
            ORDER BY is_default DESC, warehouse_name
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TenantId = tenantId });
    }

    public async Task<Guid?> ResolveChatThreadIdAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id FROM customer_chat_threads
            WHERE tenant_id = @TenantId AND customer_id = @CustomerId
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TenantId = tenantId, CustomerId = customerId });
    }

    public async Task<string> NextDraftNumberAsync(
        Guid tenantId,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int + 1 FROM customer_draft_orders WHERE tenant_id = @TenantId
            """;
        var seq = await conn.QuerySingleAsync<int>(sql, new { TenantId = tenantId }, tx);
        return $"CDO-{seq:D6}";
    }

    public async Task<Guid> InsertDraftAsync(
        Guid tenantId,
        Guid userId,
        string draftNumber,
        UpsertCustomerDraftOrderRequest request,
        Guid warehouseId,
        short status,
        decimal subtotal,
        decimal discountAmount,
        decimal totalAmount,
        DateTimeOffset? expiresAt,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_draft_orders (
                tenant_id, customer_id, draft_number, chat_thread_id, warehouse_id, price_type, status,
                subtotal, discount_amount, total_amount, order_discount_type, order_discount_value,
                notes, expires_at, created_by, sent_at
            ) VALUES (
                @TenantId, @CustomerId, @DraftNumber, @ChatThreadId, @WarehouseId, @PriceType, @Status,
                @Subtotal, @DiscountAmount, @TotalAmount, @OrderDiscountType, @OrderDiscountValue,
                @Notes, @ExpiresAt, @CreatedBy, @SentAt
            )
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = tenantId,
            request.CustomerId,
            DraftNumber = draftNumber,
            ChatThreadId = request.ChatThreadId,
            WarehouseId = warehouseId,
            request.PriceType,
            Status = status,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            TotalAmount = totalAmount,
            request.OrderDiscountType,
            OrderDiscountValue = request.OrderDiscountValue ?? 0,
            request.Notes,
            ExpiresAt = expiresAt?.UtcDateTime,
            CreatedBy = userId,
            SentAt = status == CustomerDraftOrderStatuses.Sent ? DateTime.UtcNow : (DateTime?)null,
        }, tx);
    }

    public async Task InsertItemsAsync(
        Guid draftOrderId,
        IReadOnlyList<(CustomerDraftOrderLineRequest Request, PricedDraftLine Line)> items,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_draft_order_items (
                draft_order_id, line_number, product_id, product_unit_id, product_code, product_name,
                unit_name, quantity, unit_price, line_discount_type, line_discount_value, line_amount, dosage_note
            ) VALUES (
                @DraftOrderId, @LineNumber, @ProductId, @ProductUnitId, @ProductCode, @ProductName,
                @UnitName, @Quantity, @UnitPrice, @LineDiscountType, @LineDiscountValue, @LineAmount, @DosageNote
            )
            """;
        var lineNumber = 1;
        foreach (var (request, line) in items)
        {
            await conn.ExecuteAsync(sql, new
            {
                DraftOrderId = draftOrderId,
                LineNumber = lineNumber++,
                line.ProductId,
                line.ProductUnitId,
                line.ProductCode,
                line.ProductName,
                line.UnitName,
                Quantity = request.Quantity,
                line.UnitPrice,
                LineDiscountType = request.DiscountType,
                LineDiscountValue = request.DiscountValue ?? 0,
                LineAmount = line.LineTotal,
                DosageNote = request.DosageNote,
            }, tx);
        }
    }

    public async Task ReplaceDraftAsync(
        Guid tenantId,
        Guid draftOrderId,
        UpsertCustomerDraftOrderRequest request,
        decimal subtotal,
        decimal discountAmount,
        decimal totalAmount,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                chat_thread_id = COALESCE(@ChatThreadId, chat_thread_id),
                price_type = @PriceType,
                subtotal = @Subtotal,
                discount_amount = @DiscountAmount,
                total_amount = @TotalAmount,
                order_discount_type = @OrderDiscountType,
                order_discount_value = @OrderDiscountValue,
                notes = @Notes
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            request.ChatThreadId,
            request.PriceType,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            TotalAmount = totalAmount,
            request.OrderDiscountType,
            OrderDiscountValue = request.OrderDiscountValue ?? 0,
            request.Notes,
            Draft = CustomerDraftOrderStatuses.Draft,
        }, tx);

        await conn.ExecuteAsync(
            "DELETE FROM customer_draft_order_items WHERE draft_order_id = @DraftOrderId",
            new { DraftOrderId = draftOrderId },
            tx);
    }

    public async Task<bool> MarkSentAsync(
        Guid tenantId,
        Guid draftOrderId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                status = @Sent,
                sent_at = NOW(),
                expires_at = @ExpiresAt
            WHERE id = @Id AND tenant_id = @TenantId AND status = @Draft
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            Sent = CustomerDraftOrderStatuses.Sent,
            Draft = CustomerDraftOrderStatuses.Draft,
            ExpiresAt = expiresAt.UtcDateTime,
        });
        return rows > 0;
    }

    public async Task<bool> MarkCancelledAsync(Guid tenantId, Guid draftOrderId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                status = @Cancelled,
                cancelled_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
              AND status IN (@Draft, @Sent, @Confirmed)
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            Draft = CustomerDraftOrderStatuses.Draft,
            Sent = CustomerDraftOrderStatuses.Sent,
            Confirmed = CustomerDraftOrderStatuses.Confirmed,
            Cancelled = CustomerDraftOrderStatuses.Cancelled,
        });
        return rows > 0;
    }

    public async Task<bool> MarkCancelledByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken)
    {
        await ExpireStaleAsync(tenantId, customerId, cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                status = @Cancelled,
                cancelled_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND customer_id = @CustomerId
              AND status IN (@Sent, @Confirmed)
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            CustomerId = customerId,
            Sent = CustomerDraftOrderStatuses.Sent,
            Confirmed = CustomerDraftOrderStatuses.Confirmed,
            Cancelled = CustomerDraftOrderStatuses.Cancelled,
        });
        return rows > 0;
    }

    public async Task<bool> MarkConfirmedAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken)
    {
        await ExpireStaleAsync(tenantId, customerId, cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                status = @Confirmed,
                confirmed_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND customer_id = @CustomerId
              AND status = @Sent
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            CustomerId = customerId,
            Sent = CustomerDraftOrderStatuses.Sent,
            Confirmed = CustomerDraftOrderStatuses.Confirmed,
        });
        return rows > 0;
    }

    public async Task<bool> MarkCompletedAsync(
        Guid tenantId,
        Guid draftOrderId,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                status = @Completed,
                completed_at = NOW(),
                sales_order_id = @SalesOrderId
            WHERE id = @Id AND tenant_id = @TenantId
              AND status IN (@Sent, @Confirmed)
              AND sales_order_id IS NULL
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            SalesOrderId = salesOrderId,
            Sent = CustomerDraftOrderStatuses.Sent,
            Confirmed = CustomerDraftOrderStatuses.Confirmed,
            Completed = CustomerDraftOrderStatuses.Completed,
        });
        return rows > 0;
    }

    public async Task ExpireStaleAsync(Guid tenantId, Guid? customerId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET status = @Expired
            WHERE tenant_id = @TenantId
              AND status IN (@Sent, @Confirmed)
              AND expires_at IS NOT NULL
              AND expires_at < NOW()
              AND (@CustomerId IS NULL OR customer_id = @CustomerId)
            """, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            Sent = CustomerDraftOrderStatuses.Sent,
            Confirmed = CustomerDraftOrderStatuses.Confirmed,
            Expired = CustomerDraftOrderStatuses.Expired,
        });
    }

    public async Task<DraftOrderHeaderRow?> GetHeaderAsync(
        Guid tenantId,
        Guid draftOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DraftOrderHeaderRow>("""
            SELECT
                d.id AS Id,
                d.draft_number AS DraftNumber,
                d.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                c.phone AS CustomerPhone,
                d.chat_thread_id AS ChatThreadId,
                d.warehouse_id AS WarehouseId,
                d.price_type AS PriceType,
                d.status AS Status,
                d.subtotal AS Subtotal,
                d.discount_amount AS DiscountAmount,
                d.total_amount AS TotalAmount,
                d.order_discount_type AS OrderDiscountType,
                d.order_discount_value AS OrderDiscountValue,
                d.notes AS Notes,
                d.sent_at AS SentAt,
                d.confirmed_at AS ConfirmedAt,
                d.completed_at AS CompletedAt,
                d.expires_at AS ExpiresAt,
                d.sales_order_id AS SalesOrderId,
                so.order_number AS SalesOrderNumber,
                d.hidden_by_customer_at AS HiddenByCustomerAt
            FROM customer_draft_orders d
            JOIN customers c ON c.id = d.customer_id
            LEFT JOIN sales_orders so ON so.id = d.sales_order_id
            WHERE d.id = @Id AND d.tenant_id = @TenantId
            """, new { Id = draftOrderId, TenantId = tenantId });
    }

    public async Task<IReadOnlyList<DraftOrderItemRow>> ListItemsAsync(
        Guid draftOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<DraftOrderItemRow>("""
            SELECT
                id AS Id,
                line_number AS LineNumber,
                product_id AS ProductId,
                product_unit_id AS ProductUnitId,
                product_code AS ProductCode,
                product_name AS ProductName,
                unit_name AS UnitName,
                quantity AS Quantity,
                unit_price AS UnitPrice,
                line_discount_type AS LineDiscountType,
                line_discount_value AS LineDiscountValue,
                line_amount AS LineAmount,
                dosage_note AS DosageNote
            FROM customer_draft_order_items
            WHERE draft_order_id = @DraftOrderId
            ORDER BY line_number
            """, new { DraftOrderId = draftOrderId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DraftOrderListRow>> ListAsync(
        Guid tenantId,
        Guid? customerId,
        short[]? statuses,
        bool excludeHiddenByCustomer = false,
        CancellationToken cancellationToken = default)
    {
        if (customerId is Guid cid)
            await ExpireStaleAsync(tenantId, cid, cancellationToken);
        else
            await ExpireStaleAsync(tenantId, null, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var statusFilter = statuses is { Length: > 0 } ? statuses : null;
        var sql = """
            SELECT
                d.id AS Id,
                d.draft_number AS DraftNumber,
                d.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                d.status AS Status,
                d.total_amount AS TotalAmount,
                (SELECT COUNT(*)::int FROM customer_draft_order_items i WHERE i.draft_order_id = d.id) AS ItemCount,
                d.sent_at AS SentAt,
                d.confirmed_at AS ConfirmedAt,
                d.expires_at AS ExpiresAt,
                d.hidden_by_customer_at AS HiddenByCustomerAt
            FROM customer_draft_orders d
            JOIN customers c ON c.id = d.customer_id
            WHERE d.tenant_id = @TenantId
              AND (@CustomerId IS NULL OR d.customer_id = @CustomerId)
            """ + (statusFilter != null ? " AND d.status = ANY(@Statuses)" : "") +
            (excludeHiddenByCustomer ? " AND d.hidden_by_customer_at IS NULL" : "") +
            " ORDER BY d.created_at DESC";
        var rows = await conn.QueryAsync<DraftOrderListRow>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            Statuses = statusFilter,
        });
        return rows.ToList();
    }

    public async Task<bool> MarkHiddenByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_draft_orders SET
                hidden_by_customer_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND customer_id = @CustomerId
              AND hidden_by_customer_at IS NULL
            """, new
        {
            Id = draftOrderId,
            TenantId = tenantId,
            CustomerId = customerId,
        });
        return rows > 0;
    }

    public async Task<IReadOnlyList<ProductMetaRow>> LoadProductMetaAsync(
        Guid tenantId,
        IReadOnlyList<CustomerDraftOrderLineRequest> items,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var result = new List<ProductMetaRow>();
        foreach (var item in items)
        {
            var row = await conn.QuerySingleOrDefaultAsync<ProductMetaRow>("""
                SELECT
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    u.unit_name AS UnitName
                FROM products p
                JOIN product_units u ON u.id = @ProductUnitId AND u.product_id = p.id
                WHERE p.id = @ProductId AND p.tenant_id = @TenantId AND p.deleted_at IS NULL
                """, new
            {
                TenantId = tenantId,
                item.ProductId,
                item.ProductUnitId,
            }, tx) ?? throw new InvalidOperationException("Sản phẩm không hợp lệ.");
            result.Add(row with { ProductId = item.ProductId, ProductUnitId = item.ProductUnitId });
        }

        return result;
    }
}

internal sealed record PricedDraftLine(
    Guid ProductId,
    Guid ProductUnitId,
    string ProductCode,
    string ProductName,
    string UnitName,
    decimal UnitPrice,
    decimal LineTotal);

internal sealed record DraftOrderHeaderRow
{
    public Guid Id { get; init; }
    public string DraftNumber { get; init; } = "";
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public string? CustomerPhone { get; init; }
    public Guid? ChatThreadId { get; init; }
    public Guid WarehouseId { get; init; }
    public short PriceType { get; init; }
    public short Status { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public short? OrderDiscountType { get; init; }
    public decimal? OrderDiscountValue { get; init; }
    public string? Notes { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? SalesOrderId { get; init; }
    public string? SalesOrderNumber { get; init; }
    public DateTime? HiddenByCustomerAt { get; init; }
}

internal sealed record DraftOrderItemRow
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public Guid ProductUnitId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string UnitName { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public short? LineDiscountType { get; init; }
    public decimal? LineDiscountValue { get; init; }
    public decimal LineAmount { get; init; }
    public string? DosageNote { get; init; }
}

internal sealed record DraftOrderListRow
{
    public Guid Id { get; init; }
    public string DraftNumber { get; init; } = "";
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public short Status { get; init; }
    public decimal TotalAmount { get; init; }
    public int ItemCount { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? HiddenByCustomerAt { get; init; }
}

internal sealed record ProductMetaRow
{
    public Guid ProductId { get; init; }
    public Guid ProductUnitId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string UnitName { get; init; } = "";
}
