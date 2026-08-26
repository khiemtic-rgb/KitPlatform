using System.Data;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class CsdlDuocSyncLogRepository
{
    private readonly IDbConnectionFactory _db;

    public CsdlDuocSyncLogRepository(IDbConnectionFactory db) => _db = db;

    public async Task<bool> TryBeginAsync(
        Guid tenantId,
        Guid documentId,
        string? documentNumber,
        CancellationToken cancellationToken,
        string direction = "stock-out")
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        const string sql = """
            INSERT INTO csdl_duoc_sync_log (
                tenant_id, sales_order_id, order_number, direction, status
            )
            VALUES (@TenantId, @DocumentId, @DocumentNumber, @Direction, 'pending')
            ON CONFLICT (tenant_id, sales_order_id, direction) DO UPDATE
            SET status = 'pending',
                error_message = NULL,
                updated_at = NOW()
            WHERE csdl_duoc_sync_log.status IN ('error', 'skipped', 'pending')
            RETURNING id
            """;
        var id = await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            DocumentId = documentId,
            DocumentNumber = documentNumber,
            Direction = direction,
        }, cancellationToken: cancellationToken));
        return id.HasValue;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid documentId,
        string status,
        string? remoteTransactionId,
        string? remoteStatus,
        int lineCount,
        int skippedLineCount,
        object? request,
        object? response,
        string? errorMessage,
        CancellationToken cancellationToken,
        string direction = "stock-out")
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        const string sql = """
            UPDATE csdl_duoc_sync_log
            SET status = @Status,
                remote_transaction_id = @RemoteTransactionId,
                remote_status = @RemoteStatus,
                line_count = @LineCount,
                skipped_line_count = @SkippedLineCount,
                request_json = CAST(@RequestJson AS jsonb),
                response_json = CAST(@ResponseJson AS jsonb),
                error_message = @ErrorMessage,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND sales_order_id = @DocumentId
              AND direction = @Direction
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            DocumentId = documentId,
            Direction = direction,
            Status = status,
            RemoteTransactionId = remoteTransactionId,
            RemoteStatus = remoteStatus,
            LineCount = lineCount,
            SkippedLineCount = skippedLineCount,
            RequestJson = request is null ? null : JsonSerializer.Serialize(request),
            ResponseJson = response is null ? null : (response is string s ? s : JsonSerializer.Serialize(response)),
            ErrorMessage = errorMessage,
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CsdlDuocSyncLogRow>> ListRecentAsync(
        Guid tenantId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        const string sql = """
            SELECT id, sales_order_id AS SalesOrderId, order_number AS OrderNumber,
                   direction, status, remote_transaction_id AS RemoteTransactionId,
                   remote_status AS RemoteStatus, line_count AS LineCount,
                   skipped_line_count AS SkippedLineCount, error_message AS ErrorMessage,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM csdl_duoc_sync_log
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT @Limit
            """;
        var rows = await conn.QueryAsync<CsdlDuocSyncLogRow>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, Limit = Math.Clamp(limit, 1, 200) },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CsdlDuocSaleLineForSync>> LoadSaleLinesAsync(
        Guid tenantId,
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        const string sql = """
            SELECT
                so.id AS SalesOrderId,
                so.order_number AS OrderNumber,
                so.order_date AS OrderDate,
                so.branch_id AS BranchId,
                b.retail_facility_code AS RetailFacilityCode,
                soi.id AS LineId,
                p.national_drug_id AS NationalDrugId,
                p.packaging AS Packaging,
                p.attributes->>'manufacturer' AS ManufacturerName,
                pb.country_code AS CountryCode,
                ib.batch_number AS BatchNumber,
                ib.expiry_date AS ExpiryDate,
                soi.quantity AS Quantity,
                soi.unit_price AS UnitPrice,
                COALESCE(base_u.unit_name, pu.unit_name) AS UnitName
            FROM sales_orders so
            INNER JOIN sales_order_items soi ON soi.sales_order_id = so.id
            INNER JOIN products p ON p.id = soi.product_id
            INNER JOIN product_units pu ON pu.id = soi.product_unit_id
            INNER JOIN inventory_batches ib ON ib.id = soi.batch_id
            INNER JOIN branches b ON b.id = so.branch_id
            LEFT JOIN product_brands pb ON pb.id = p.brand_id
            LEFT JOIN LATERAL (
                SELECT pu2.unit_name
                FROM product_units pu2
                WHERE pu2.product_id = p.id AND pu2.status = 1
                ORDER BY pu2.is_base_unit DESC, pu2.conversion_factor ASC
                LIMIT 1
            ) base_u ON TRUE
            WHERE so.tenant_id = @TenantId
              AND so.id = @SalesOrderId
              AND so.status = @Completed
            ORDER BY soi.id
            """;
        var rows = await conn.QueryAsync<CsdlDuocSaleLineForSync>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                SalesOrderId = salesOrderId,
                Completed = SalesOrderStatuses.Completed,
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CsdlDuocGrnLineForSync>> LoadGrnLinesAsync(
        Guid tenantId,
        Guid goodsReceiptId,
        CancellationToken cancellationToken)
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        const string sql = """
            SELECT
                gr.id AS GoodsReceiptId,
                gr.grn_number AS GrnNumber,
                gr.receipt_date AS ReceiptDate,
                gri.id AS LineId,
                p.national_drug_id AS NationalDrugId,
                p.packaging AS Packaging,
                p.attributes->>'manufacturer' AS ManufacturerName,
                pb.country_code AS CountryCode,
                gri.batch_number AS BatchNumber,
                gri.expiry_date AS ExpiryDate,
                gri.quantity AS Quantity,
                COALESCE(gri.inventory_unit_cost, gri.unit_cost, 0) AS UnitCost
            FROM goods_receipts gr
            INNER JOIN goods_receipt_items gri ON gri.goods_receipt_id = gr.id
            INNER JOIN products p ON p.id = gri.product_id
            LEFT JOIN product_brands pb ON pb.id = p.brand_id
            WHERE gr.tenant_id = @TenantId
              AND gr.id = @GoodsReceiptId
              AND gr.deleted_at IS NULL
              AND gr.status = @Completed
            ORDER BY gri.id
            """;
        var rows = await conn.QueryAsync<CsdlDuocGrnLineForSync>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                GoodsReceiptId = goodsReceiptId,
                Completed = KitPlatform.Packs.Pharmacy.Procurement.GoodsReceiptStatuses.Completed,
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private async Task<Npgsql.NpgsqlConnection> OpenTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                "SELECT set_config('app.tenant_id', @Value, false)",
                new { Value = tenantId.ToString() },
                cancellationToken: cancellationToken));
        return conn;
    }
}

internal sealed class CsdlDuocSyncLogRow
{
    public Guid Id { get; init; }
    public Guid SalesOrderId { get; init; }
    public string? OrderNumber { get; init; }
    public string Direction { get; init; } = "";
    public string Status { get; init; } = "";
    public string? RemoteTransactionId { get; init; }
    public string? RemoteStatus { get; init; }
    public int LineCount { get; init; }
    public int SkippedLineCount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

internal sealed class CsdlDuocSaleLineForSync
{
    public Guid SalesOrderId { get; init; }
    public string OrderNumber { get; init; } = "";
    public DateTime OrderDate { get; init; }
    public Guid BranchId { get; init; }
    public string? RetailFacilityCode { get; init; }
    public Guid LineId { get; init; }
    public string? NationalDrugId { get; init; }
    public string? Packaging { get; init; }
    public string? ManufacturerName { get; init; }
    public string? CountryCode { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string? UnitName { get; init; }
}

internal sealed class CsdlDuocGrnLineForSync
{
    public Guid GoodsReceiptId { get; init; }
    public string GrnNumber { get; init; } = "";
    public DateTime ReceiptDate { get; init; }
    public Guid LineId { get; init; }
    public string? NationalDrugId { get; init; }
    public string? Packaging { get; init; }
    public string? ManufacturerName { get; init; }
    public string? CountryCode { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
}
