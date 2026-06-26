using System.Data;
using Dapper;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed record ResolvedReservationProduct(
    Guid ProductId,
    Guid ProductUnitId,
    string ProductCode,
    string ProductName,
    string UnitName);

internal sealed class CustomerReservationRepository
{
    private readonly IDbConnectionFactory _db;

    public CustomerReservationRepository(IDbConnectionFactory db) => _db = db;

    public async Task<string> NextReservationNumberAsync(
        Guid tenantId,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int + 1 FROM customer_reservations WHERE tenant_id = @TenantId
            """;
        var seq = await conn.QuerySingleAsync<int>(sql, new { TenantId = tenantId }, tx);
        return $"CRV-{seq:D6}";
    }

    public async Task<ResolvedReservationProduct?> ResolveProductAsync(
        Guid tenantId,
        Guid productId,
        IDbConnection conn,
        IDbTransaction? tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS ProductId,
                u.id AS ProductUnitId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                u.unit_name AS UnitName
            FROM products p
            INNER JOIN product_units u ON u.product_id = p.id
            WHERE p.id = @ProductId
              AND p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND p.status = 1
            ORDER BY u.is_sale_unit DESC, u.is_base_unit DESC, u.unit_name
            LIMIT 1
            """;
        return await conn.QuerySingleOrDefaultAsync<ResolvedReservationProduct>(
            sql,
            new { ProductId = productId, TenantId = tenantId },
            tx);
    }

    public async Task<string?> ResolveAddressSummaryAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        IDbConnection conn,
        IDbTransaction? tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                TRIM(BOTH ', ' FROM CONCAT_WS(', ',
                    NULLIF(a.recipient_name, ''),
                    NULLIF(a.address_line, ''),
                    NULLIF(a.ward, ''),
                    NULLIF(a.district, ''),
                    NULLIF(a.province, '')))
            FROM customer_addresses a
            INNER JOIN customers c ON c.id = a.customer_id
            WHERE a.id = @AddressId AND a.customer_id = @CustomerId AND c.tenant_id = @TenantId
            """;
        return await conn.QuerySingleOrDefaultAsync<string?>(
            sql,
            new { AddressId = addressId, CustomerId = customerId, TenantId = tenantId },
            tx);
    }

    public async Task<bool> AddressBelongsToCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid addressId,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM customer_addresses a
                INNER JOIN customers c ON c.id = a.customer_id
                WHERE a.id = @AddressId AND a.customer_id = @CustomerId AND c.tenant_id = @TenantId
            )
            """;
        return await conn.QuerySingleAsync<bool>(
            sql,
            new { AddressId = addressId, CustomerId = customerId, TenantId = tenantId },
            tx);
    }

    public async Task<Guid> InsertReservationAsync(
        Guid tenantId,
        Guid customerId,
        string reservationNumber,
        short fulfillmentType,
        Guid? addressId,
        string? notes,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_reservations (
                tenant_id, customer_id, reservation_number, status, fulfillment_type, address_id, notes
            ) VALUES (
                @TenantId, @CustomerId, @ReservationNumber, @Status, @FulfillmentType, @AddressId, @Notes
            )
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
            ReservationNumber = reservationNumber,
            Status = CustomerReservationStatuses.Pending,
            FulfillmentType = fulfillmentType,
            AddressId = addressId,
            Notes = notes,
        }, tx);
    }

    public async Task InsertItemsAsync(
        Guid reservationId,
        IReadOnlyList<(CustomerReservationLineRequest Request, ResolvedReservationProduct Product)> items,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO customer_reservation_items (
                reservation_id, line_number, product_id, product_unit_id, product_code, product_name,
                unit_name, quantity, customer_note
            ) VALUES (
                @ReservationId, @LineNumber, @ProductId, @ProductUnitId, @ProductCode, @ProductName,
                @UnitName, @Quantity, @CustomerNote
            )
            """;
        var lineNumber = 1;
        foreach (var (request, product) in items)
        {
            await conn.ExecuteAsync(sql, new
            {
                ReservationId = reservationId,
                LineNumber = lineNumber++,
                product.ProductId,
                product.ProductUnitId,
                product.ProductCode,
                product.ProductName,
                product.UnitName,
                Quantity = request.Quantity,
                CustomerNote = request.CustomerNote,
            }, tx);
        }
    }

    public async Task<IReadOnlyList<CustomerReservationListItemDto>> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id,
                r.reservation_number AS ReservationNumber,
                r.status AS Status,
                r.fulfillment_type AS FulfillmentType,
                (SELECT COUNT(*)::int FROM customer_reservation_items i WHERE i.reservation_id = r.id) AS ItemCount,
                r.submitted_at AS SubmittedAt,
                r.ready_at AS ReadyAt
            FROM customer_reservations r
            INNER JOIN customers c ON c.id = r.customer_id
            WHERE r.customer_id = @CustomerId AND c.tenant_id = @TenantId
            ORDER BY r.submitted_at DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ReservationListRow>(sql, new
        {
            CustomerId = customerId,
            TenantId = tenantId,
        });
        return rows.Select(MapListItem).ToList();
    }

    public async Task<IReadOnlyList<CustomerReservationStaffListItemDto>> ListForStaffAsync(
        Guid tenantId,
        short[]? statuses,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                r.id AS Id,
                r.reservation_number AS ReservationNumber,
                r.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                c.phone AS CustomerPhone,
                r.status AS Status,
                r.fulfillment_type AS FulfillmentType,
                (SELECT COUNT(*)::int FROM customer_reservation_items i WHERE i.reservation_id = r.id) AS ItemCount,
                r.submitted_at AS SubmittedAt,
                r.ready_at AS ReadyAt
            FROM customer_reservations r
            INNER JOIN customers c ON c.id = r.customer_id
            WHERE r.tenant_id = @TenantId
            """;
        if (statuses is { Length: > 0 })
            sql += " AND r.status = ANY(@Statuses)";
        sql += " ORDER BY r.submitted_at DESC";

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ReservationStaffListRow>(sql, new
        {
            TenantId = tenantId,
            Statuses = statuses,
        });
        return rows.Select(MapStaffListItem).ToList();
    }

    public async Task<CustomerReservationDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                r.id AS Id,
                r.reservation_number AS ReservationNumber,
                r.status AS Status,
                r.fulfillment_type AS FulfillmentType,
                r.address_id AS AddressId,
                TRIM(BOTH ', ' FROM CONCAT_WS(', ',
                    NULLIF(a.recipient_name, ''),
                    NULLIF(a.address_line, ''),
                    NULLIF(a.ward, ''),
                    NULLIF(a.district, ''),
                    NULLIF(a.province, ''))) AS AddressSummary,
                r.notes AS Notes,
                r.staff_notes AS StaffNotes,
                r.submitted_at AS SubmittedAt,
                r.confirmed_at AS ConfirmedAt,
                r.ready_at AS ReadyAt,
                r.collected_at AS CollectedAt,
                r.sales_order_id AS SalesOrderId,
                so.order_number AS SalesOrderNumber
            FROM customer_reservations r
            INNER JOIN customers c ON c.id = r.customer_id
            LEFT JOIN customer_addresses a ON a.id = r.address_id
            LEFT JOIN sales_orders so ON so.id = r.sales_order_id
            WHERE r.id = @ReservationId AND r.customer_id = @CustomerId AND c.tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<ReservationHeaderRow>(headerSql, new
        {
            ReservationId = reservationId,
            CustomerId = customerId,
            TenantId = tenantId,
        });
        if (header is null) return null;

        var items = await LoadItemsAsync(conn, reservationId, cancellationToken);
        return MapDetail(header, items);
    }

    public async Task<CustomerReservationDto?> GetForStaffAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                r.id AS Id,
                r.reservation_number AS ReservationNumber,
                r.status AS Status,
                r.fulfillment_type AS FulfillmentType,
                r.address_id AS AddressId,
                TRIM(BOTH ', ' FROM CONCAT_WS(', ',
                    NULLIF(a.recipient_name, ''),
                    NULLIF(a.address_line, ''),
                    NULLIF(a.ward, ''),
                    NULLIF(a.district, ''),
                    NULLIF(a.province, ''))) AS AddressSummary,
                r.notes AS Notes,
                r.staff_notes AS StaffNotes,
                r.submitted_at AS SubmittedAt,
                r.confirmed_at AS ConfirmedAt,
                r.ready_at AS ReadyAt,
                r.collected_at AS CollectedAt,
                r.sales_order_id AS SalesOrderId,
                so.order_number AS SalesOrderNumber
            FROM customer_reservations r
            LEFT JOIN customer_addresses a ON a.id = r.address_id
            LEFT JOIN sales_orders so ON so.id = r.sales_order_id
            WHERE r.id = @ReservationId AND r.tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<ReservationHeaderRow>(headerSql, new
        {
            ReservationId = reservationId,
            TenantId = tenantId,
        });
        if (header is null) return null;

        var items = await LoadItemsAsync(conn, reservationId, cancellationToken);
        return MapDetail(header, items);
    }

    public async Task<short?> GetStatusAsync(
        Guid tenantId,
        Guid reservationId,
        Guid? customerId,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT status FROM customer_reservations
            WHERE id = @ReservationId AND tenant_id = @TenantId
            """;
        if (customerId is not null)
            sql += " AND customer_id = @CustomerId";
        sql += " FOR UPDATE";

        return await conn.QuerySingleOrDefaultAsync<short?>(
            sql,
            new { ReservationId = reservationId, TenantId = tenantId, CustomerId = customerId },
            tx);
    }

    public async Task UpdateStatusAsync(
        Guid tenantId,
        Guid reservationId,
        short status,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var timestampColumn = status switch
        {
            CustomerReservationStatuses.Confirmed => "confirmed_at",
            CustomerReservationStatuses.Ready => "ready_at",
            CustomerReservationStatuses.Collected => "collected_at",
            CustomerReservationStatuses.Cancelled => "cancelled_at",
            CustomerReservationStatuses.Rejected => "rejected_at",
            _ => null,
        };

        var sql = timestampColumn is null
            ? """
              UPDATE customer_reservations
              SET status = @Status
              WHERE id = @ReservationId AND tenant_id = @TenantId
              """
            : $"""
              UPDATE customer_reservations
              SET status = @Status, {timestampColumn} = NOW()
              WHERE id = @ReservationId AND tenant_id = @TenantId
              """;

        await conn.ExecuteAsync(sql, new
        {
            Status = status,
            ReservationId = reservationId,
            TenantId = tenantId,
        }, tx);
    }

    public async Task UpdateStaffNotesAsync(
        Guid tenantId,
        Guid reservationId,
        string? staffNotes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE customer_reservations
            SET staff_notes = @StaffNotes
            WHERE id = @ReservationId AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            StaffNotes = staffNotes,
            ReservationId = reservationId,
            TenantId = tenantId,
        });
    }

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

    public async Task<ReservationHeaderRow?> GetHeaderAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id,
                r.reservation_number AS ReservationNumber,
                r.customer_id AS CustomerId,
                r.status AS Status,
                r.fulfillment_type AS FulfillmentType,
                r.address_id AS AddressId,
                r.notes AS Notes,
                r.staff_notes AS StaffNotes,
                r.submitted_at AS SubmittedAt,
                r.confirmed_at AS ConfirmedAt,
                r.ready_at AS ReadyAt,
                r.collected_at AS CollectedAt,
                r.sales_order_id AS SalesOrderId,
                so.order_number AS SalesOrderNumber
            FROM customer_reservations r
            LEFT JOIN sales_orders so ON so.id = r.sales_order_id
            WHERE r.id = @ReservationId AND r.tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ReservationHeaderRow>(sql, new
        {
            ReservationId = reservationId,
            TenantId = tenantId,
        });
    }

    public async Task<bool> LinkSalesOrderAsync(
        Guid tenantId,
        Guid reservationId,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync("""
            UPDATE customer_reservations r SET
                status = @Collected,
                collected_at = COALESCE(r.collected_at, NOW()),
                sales_order_id = @SalesOrderId
            WHERE r.id = @ReservationId AND r.tenant_id = @TenantId
              AND r.sales_order_id IS NULL
              AND r.status IN (@Confirmed, @Ready, @Collected)
              AND EXISTS (
                  SELECT 1 FROM sales_orders so
                  WHERE so.id = @SalesOrderId AND so.tenant_id = @TenantId
                    AND so.customer_id = r.customer_id
              )
            """, new
        {
            ReservationId = reservationId,
            TenantId = tenantId,
            SalesOrderId = salesOrderId,
            Confirmed = CustomerReservationStatuses.Confirmed,
            Ready = CustomerReservationStatuses.Ready,
            Collected = CustomerReservationStatuses.Collected,
        });
        return rows > 0;
    }

    private static async Task<IReadOnlyList<ReservationLineRow>> LoadItemsInternalAsync(
        IDbConnection conn,
        Guid reservationId)
    {
        const string sql = """
            SELECT
                i.id AS Id,
                i.line_number AS LineNumber,
                i.product_id AS ProductId,
                i.product_code AS ProductCode,
                i.product_name AS ProductName,
                i.product_unit_id AS ProductUnitId,
                i.unit_name AS UnitName,
                i.quantity AS Quantity,
                i.customer_note AS CustomerNote
            FROM customer_reservation_items i
            WHERE i.reservation_id = @ReservationId
            ORDER BY i.line_number
            """;
        return (await conn.QueryAsync<ReservationLineRow>(sql, new { ReservationId = reservationId }))
            .ToList();
    }

    public async Task<IReadOnlyList<ReservationLineRow>> ListItemsAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await LoadItemsInternalAsync(conn, reservationId);
    }

    private static async Task<IReadOnlyList<CustomerReservationLineDto>> LoadItemsAsync(
        IDbConnection conn,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadItemsInternalAsync(conn, reservationId);
        return rows.Select(MapLine).ToList();
    }

    private static CustomerReservationListItemDto MapListItem(ReservationListRow row) =>
        new(
            row.Id,
            row.ReservationNumber,
            row.Status,
            row.FulfillmentType,
            row.ItemCount,
            ToOffset(row.SubmittedAt) ?? DateTimeOffset.UtcNow,
            ToOffset(row.ReadyAt));

    private static CustomerReservationStaffListItemDto MapStaffListItem(ReservationStaffListRow row) =>
        new(
            row.Id,
            row.ReservationNumber,
            row.CustomerId,
            row.CustomerName,
            row.CustomerPhone,
            row.Status,
            row.FulfillmentType,
            row.ItemCount,
            ToOffset(row.SubmittedAt) ?? DateTimeOffset.UtcNow,
            ToOffset(row.ReadyAt));

    private static CustomerReservationDto MapDetail(
        ReservationHeaderRow header,
        IReadOnlyList<CustomerReservationLineDto> items) =>
        new(
            header.Id,
            header.ReservationNumber,
            header.Status,
            header.FulfillmentType,
            header.AddressId,
            header.AddressSummary,
            header.Notes,
            header.StaffNotes,
            ToOffset(header.SubmittedAt) ?? DateTimeOffset.UtcNow,
            ToOffset(header.ConfirmedAt),
            ToOffset(header.ReadyAt),
            ToOffset(header.CollectedAt),
            header.SalesOrderId,
            header.SalesOrderNumber,
            items);

    private static CustomerReservationLineDto MapLine(ReservationLineRow row) =>
        new(
            row.Id,
            row.LineNumber,
            row.ProductId,
            row.ProductCode,
            row.ProductName,
            row.UnitName,
            row.Quantity,
            row.CustomerNote);

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}

internal sealed record ReservationListRow
{
    public Guid Id { get; init; }
    public string ReservationNumber { get; init; } = "";
    public short Status { get; init; }
    public short FulfillmentType { get; init; }
    public int ItemCount { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReadyAt { get; init; }
}

internal sealed record ReservationStaffListRow
{
    public Guid Id { get; init; }
    public string ReservationNumber { get; init; } = "";
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public string? CustomerPhone { get; init; }
    public short Status { get; init; }
    public short FulfillmentType { get; init; }
    public int ItemCount { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReadyAt { get; init; }
}

internal sealed record ReservationHeaderRow
{
    public Guid Id { get; init; }
    public string ReservationNumber { get; init; } = "";
    public Guid CustomerId { get; init; }
    public short Status { get; init; }
    public short FulfillmentType { get; init; }
    public Guid? AddressId { get; init; }
    public string? AddressSummary { get; init; }
    public string? Notes { get; init; }
    public string? StaffNotes { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? ReadyAt { get; init; }
    public DateTime? CollectedAt { get; init; }
    public Guid? SalesOrderId { get; init; }
    public string? SalesOrderNumber { get; init; }
}

internal sealed record ReservationLineRow
{
    public Guid Id { get; init; }
    public int LineNumber { get; init; }
    public Guid ProductId { get; init; }
    public Guid ProductUnitId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string UnitName { get; init; } = "";
    public decimal Quantity { get; init; }
    public string? CustomerNote { get; init; }
}
