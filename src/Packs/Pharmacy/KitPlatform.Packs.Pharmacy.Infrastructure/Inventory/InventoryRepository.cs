using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class InventoryRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public InventoryRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<BranchLookupDto>> GetBranchLookupsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, branch_code AS BranchCode, branch_name AS BranchName
            FROM branches
            WHERE tenant_id = @TenantId AND deleted_at IS NULL AND status = 1
            ORDER BY branch_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<BranchLookupDto>(sql, new { TenantId })).ToList();
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                w.id AS Id,
                w.branch_id AS BranchId,
                b.branch_name AS BranchName,
                w.warehouse_code AS WarehouseCode,
                w.warehouse_name AS WarehouseName,
                w.warehouse_type AS WarehouseType,
                w.is_default AS IsDefault,
                w.address AS Address,
                w.status AS Status
            FROM warehouses w
            INNER JOIN branches b ON b.id = w.branch_id
            WHERE w.tenant_id = @TenantId AND w.deleted_at IS NULL
            ORDER BY w.is_default DESC, w.warehouse_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<WarehouseDto>(sql, new { TenantId })).ToList();
    }

    public async Task<WarehouseDto?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                w.id AS Id,
                w.branch_id AS BranchId,
                b.branch_name AS BranchName,
                w.warehouse_code AS WarehouseCode,
                w.warehouse_name AS WarehouseName,
                w.warehouse_type AS WarehouseType,
                w.is_default AS IsDefault,
                w.address AS Address,
                w.status AS Status
            FROM warehouses w
            INNER JOIN branches b ON b.id = w.branch_id
            WHERE w.id = @Id AND w.tenant_id = @TenantId AND w.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<WarehouseDto>(sql, new { Id = id, TenantId });
    }

    public async Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM branches
                WHERE id = @BranchId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { BranchId = branchId, TenantId });
    }

    public async Task<Guid> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        if (request.IsDefault)
        {
            await conn.ExecuteAsync(
                "UPDATE warehouses SET is_default = FALSE, updated_at = NOW() WHERE tenant_id = @TenantId AND deleted_at IS NULL",
                new { TenantId },
                tx);
        }

        const string sql = """
            INSERT INTO warehouses (tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, is_default, address)
            VALUES (@TenantId, @BranchId, @WarehouseCode, @WarehouseName, @WarehouseType, @IsDefault, @Address)
            RETURNING id
            """;
        var id = await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            request.BranchId,
            WarehouseCode = request.WarehouseCode.Trim(),
            WarehouseName = request.WarehouseName.Trim(),
            request.WarehouseType,
            request.IsDefault,
            request.Address,
        }, tx);

        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<bool> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        if (request.IsDefault)
        {
            await conn.ExecuteAsync(
                "UPDATE warehouses SET is_default = FALSE, updated_at = NOW() WHERE tenant_id = @TenantId AND deleted_at IS NULL AND id <> @Id",
                new { TenantId, Id = id },
                tx);
        }

        const string sql = """
            UPDATE warehouses SET
                warehouse_name = @WarehouseName,
                warehouse_type = @WarehouseType,
                is_default = @IsDefault,
                address = @Address,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        var updated = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            WarehouseName = request.WarehouseName.Trim(),
            request.WarehouseType,
            request.IsDefault,
            request.Address,
            request.Status,
        }, tx) > 0;

        await tx.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<int> CountBatchesInWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int FROM inventory_batches
            WHERE warehouse_id = @WarehouseId AND tenant_id = @TenantId AND quantity_available > 0
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<int>(sql, new { WarehouseId = warehouseId, TenantId });
    }

    public async Task<bool> SoftDeleteWarehouseAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE warehouses SET deleted_at = NOW(), status = 2, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    public async Task<(IReadOnlyList<StockBatchListItemDto> Items, int Total)> GetStockBatchesAsync(
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        Guid? productId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        var extra = new List<string> { "b.quantity_available > 0" };
        if (warehouseId is not null) extra.Add("b.warehouse_id = @WarehouseId");
        else if (allowedWarehouseIds is { Length: > 0 }) extra.Add("b.warehouse_id = ANY(@AllowedWarehouseIds)");
        if (productId is not null) extra.Add("b.product_id = @ProductId");
        var searchClause = BuildProductStockSearchClause(search, out var searchParams);
        if (searchClause is not null) extra.Add(searchClause);

        var whereExtra = " AND " + string.Join(" AND ", extra);

        var countSql = $"""
            SELECT COUNT(*)::int
            FROM inventory_batches b
            INNER JOIN products p ON p.id = b.product_id
            INNER JOIN warehouses w ON w.id = b.warehouse_id
            WHERE b.tenant_id = @TenantId AND p.deleted_at IS NULL AND w.deleted_at IS NULL
            {whereExtra}
            """;

        var sql = $"""
            SELECT
                b.id AS Id,
                b.warehouse_id AS WarehouseId,
                w.warehouse_code AS WarehouseCode,
                w.warehouse_name AS WarehouseName,
                b.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                (SELECT u.unit_name FROM product_units u
                 WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                 ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                b.unit_cost AS UnitCost,
                b.quantity_available AS QuantityAvailable,
                b.quantity_received AS QuantityReceived,
                b.status AS Status
            FROM inventory_batches b
            INNER JOIN products p ON p.id = b.product_id
            INNER JOIN warehouses w ON w.id = b.warehouse_id
            WHERE b.tenant_id = @TenantId AND p.deleted_at IS NULL AND w.deleted_at IS NULL
            {whereExtra}
            ORDER BY b.expiry_date NULLS LAST, p.product_name, b.batch_number
            LIMIT @PageSize OFFSET @Offset
            """;

        var param = new DynamicParameters(new
        {
            TenantId,
            WarehouseId = warehouseId,
            AllowedWarehouseIds = allowedWarehouseIds,
            ProductId = productId,
            PageSize = pageSize,
            Offset = offset,
        });
        foreach (var (key, value) in searchParams)
            param.Add(key, value);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.QuerySingleAsync<int>(countSql, param);
        var items = (await conn.QueryAsync<StockBatchListItemDto>(sql, param)).ToList();
        return (items, total);
    }

    public async Task<(IReadOnlyList<StockProductSummaryDto> Items, int Total)> GetStockProductsAsync(
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var searchClause = BuildProductStockSearchClause(search, out var searchParams, includeBatchNumber: !hasSearch);

        // Khi đang tìm theo tên/mã: hiện cả SP tồn 0 (để biết SP có trong danh mục nhưng chưa nhập / chưa Hoàn tất).
        // Không tìm: chỉ SP còn tồn > 0 (giữ hiệu năng danh sách lớn).
        string warehouseFilter;
        if (warehouseId is not null)
            warehouseFilter = "AND b.warehouse_id = @WarehouseId";
        else if (allowedWarehouseIds is { Length: > 0 })
            warehouseFilter = "AND b.warehouse_id = ANY(@AllowedWarehouseIds)";
        else
            warehouseFilter = "";

        string countSql;
        string sql;
        if (hasSearch && searchClause is not null)
        {
            countSql = $"""
                SELECT COUNT(*)::int
                FROM products p
                WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL
                  AND {searchClause}
                """;

            sql = $"""
                SELECT
                    p.id AS ProductId,
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    (SELECT u.unit_name FROM product_units u
                     WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                     ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName,
                    COALESCE((
                        SELECT SUM(b.quantity_available)
                        FROM inventory_batches b
                        INNER JOIN warehouses w ON w.id = b.warehouse_id AND w.deleted_at IS NULL
                        WHERE b.product_id = p.id AND b.tenant_id = @TenantId
                          AND b.quantity_available > 0
                          {warehouseFilter}
                    ), 0) AS TotalQuantity,
                    COALESCE((
                        SELECT COUNT(DISTINCT b.warehouse_id)::int
                        FROM inventory_batches b
                        INNER JOIN warehouses w ON w.id = b.warehouse_id AND w.deleted_at IS NULL
                        WHERE b.product_id = p.id AND b.tenant_id = @TenantId
                          AND b.quantity_available > 0
                          {warehouseFilter}
                    ), 0) AS WarehouseCount,
                    COALESCE((
                        SELECT COUNT(*)::int
                        FROM inventory_batches b
                        INNER JOIN warehouses w ON w.id = b.warehouse_id AND w.deleted_at IS NULL
                        WHERE b.product_id = p.id AND b.tenant_id = @TenantId
                          AND b.quantity_available > 0
                          {warehouseFilter}
                    ), 0) AS BatchCount
                FROM products p
                WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL
                  AND {searchClause}
                ORDER BY
                    CASE WHEN COALESCE((
                        SELECT SUM(b.quantity_available)
                        FROM inventory_batches b
                        INNER JOIN warehouses w ON w.id = b.warehouse_id AND w.deleted_at IS NULL
                        WHERE b.product_id = p.id AND b.tenant_id = @TenantId
                          AND b.quantity_available > 0
                          {warehouseFilter}
                    ), 0) > 0 THEN 0 ELSE 1 END,
                    p.product_name
                LIMIT @PageSize OFFSET @Offset
                """;
        }
        else
        {
            var extra = new List<string> { "b.quantity_available > 0" };
            if (warehouseId is not null) extra.Add("b.warehouse_id = @WarehouseId");
            else if (allowedWarehouseIds is { Length: > 0 }) extra.Add("b.warehouse_id = ANY(@AllowedWarehouseIds)");
            if (searchClause is not null) extra.Add(searchClause);
            var whereExtra = " AND " + string.Join(" AND ", extra);

            countSql = $"""
                SELECT COUNT(*)::int FROM (
                    SELECT p.id
                    FROM inventory_batches b
                    INNER JOIN products p ON p.id = b.product_id
                    INNER JOIN warehouses w ON w.id = b.warehouse_id
                    WHERE b.tenant_id = @TenantId AND p.deleted_at IS NULL AND w.deleted_at IS NULL
                    {whereExtra}
                    GROUP BY p.id
                ) grouped
                """;

            sql = $"""
                SELECT
                    p.id AS ProductId,
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    (SELECT u.unit_name FROM product_units u
                     WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                     ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName,
                    SUM(b.quantity_available) AS TotalQuantity,
                    COUNT(DISTINCT b.warehouse_id)::int AS WarehouseCount,
                    COUNT(*)::int AS BatchCount
                FROM inventory_batches b
                INNER JOIN products p ON p.id = b.product_id
                INNER JOIN warehouses w ON w.id = b.warehouse_id
                WHERE b.tenant_id = @TenantId AND p.deleted_at IS NULL AND w.deleted_at IS NULL
                {whereExtra}
                GROUP BY p.id, p.product_code, p.product_name
                ORDER BY p.product_name
                LIMIT @PageSize OFFSET @Offset
                """;
        }

        var param = new DynamicParameters(new
        {
            TenantId,
            WarehouseId = warehouseId,
            AllowedWarehouseIds = allowedWarehouseIds,
            PageSize = pageSize,
            Offset = offset,
        });
        foreach (var (key, value) in searchParams)
            param.Add(key, value);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.QuerySingleAsync<int>(countSql, param);
        var items = (await conn.QueryAsync<StockProductSummaryDto>(sql, param)).ToList();
        return (items, total);
    }

    /// <summary>
    /// Tìm theo tên SP (ưu tiên), mã, số lô. Bỏ phần trong ngoặc (vd. strength) và khớp từng từ
    /// để "Paracetamol 500mg (500 mg)" vẫn ra "Paracetamol 500mg - Vỉ".
    /// </summary>
    private static string? BuildProductStockSearchClause(
        string? search,
        out List<(string Key, object Value)> searchParams,
        bool includeBatchNumber = true)
    {
        searchParams = [];
        if (string.IsNullOrWhiteSpace(search)) return null;

        var cleaned = Regex.Replace(search.Trim(), @"\([^)]*\)", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (cleaned.Length == 0) return null;

        searchParams.Add(("SearchPattern", $"%{cleaned}%"));

        var tokens = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2)
            .Take(5)
            .ToArray();

        var batchPart = includeBatchNumber ? "OR b.batch_number ILIKE @SearchPattern" : "";

        if (tokens.Length == 0)
        {
            return $"(p.product_name ILIKE @SearchPattern OR p.product_code ILIKE @SearchPattern {batchPart})";
        }

        var nameParts = new List<string>();
        for (var i = 0; i < tokens.Length; i++)
        {
            var key = $"SearchToken{i}";
            searchParams.Add((key, $"%{tokens[i]}%"));
            nameParts.Add($"p.product_name ILIKE @{key}");
        }

        return $"""
            (
                p.product_code ILIKE @SearchPattern
                {batchPart}
                OR p.product_name ILIKE @SearchPattern
                OR ({string.Join(" AND ", nameParts)})
            )
            """;
    }

    public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM products WHERE id = @ProductId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { ProductId = productId, TenantId });
    }

    public async Task<bool> WarehouseExistsAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM warehouses WHERE id = @WarehouseId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { WarehouseId = warehouseId, TenantId });
    }

    internal sealed record BatchRow(
        Guid Id,
        Guid WarehouseId,
        Guid ProductId,
        string BatchNumber,
        DateOnly? ManufactureDate,
        DateOnly? ExpiryDate,
        decimal UnitCost,
        decimal QuantityAvailable,
        decimal QuantityReceived);

    public async Task<BatchRow?> GetBatchForUpdateAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                warehouse_id AS WarehouseId,
                product_id AS ProductId,
                batch_number AS BatchNumber,
                manufacture_date AS ManufactureDate,
                expiry_date AS ExpiryDate,
                unit_cost AS UnitCost,
                quantity_available AS QuantityAvailable,
                quantity_received AS QuantityReceived
            FROM inventory_batches
            WHERE id = @BatchId AND tenant_id = @TenantId
            FOR UPDATE
            """;
        return await conn.QuerySingleOrDefaultAsync<BatchRow>(sql, new { BatchId = batchId, TenantId }, tx);
    }

    public async Task<Guid?> FindBatchIdByKeyAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid productId,
        string batchNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id FROM inventory_batches
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId
              AND product_id = @ProductId AND batch_number = @BatchNumber
            """;
        var id = await conn.QuerySingleOrDefaultAsync<Guid>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            ProductId = productId,
            BatchNumber = batchNumber,
        }, tx);
        return id == Guid.Empty ? null : id;
    }

    public async Task<Guid> InsertBatchAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid productId,
        string batchNumber,
        DateOnly? manufactureDate,
        DateOnly? expiryDate,
        decimal unitCost,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory_batches (
                tenant_id, warehouse_id, product_id, batch_number,
                manufacture_date, expiry_date, unit_cost, quantity_received, quantity_available
            )
            VALUES (
                @TenantId, @WarehouseId, @ProductId, @BatchNumber,
                @ManufactureDate, @ExpiryDate, @UnitCost, @Quantity, @Quantity
            )
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            ProductId = productId,
            BatchNumber = batchNumber,
            ManufactureDate = manufactureDate,
            ExpiryDate = expiryDate,
            UnitCost = unitCost,
            Quantity = quantity,
        }, tx);
    }

    public async Task IncreaseBatchQuantityAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid batchId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE inventory_batches SET
                quantity_available = quantity_available + @Quantity,
                quantity_received = quantity_received + @Quantity,
                updated_at = NOW()
            WHERE id = @BatchId AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(sql, new { BatchId = batchId, Quantity = quantity, TenantId }, tx);
    }

    public async Task DecreaseBatchQuantityAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid batchId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE inventory_batches SET
                quantity_available = quantity_available - @Quantity,
                updated_at = NOW()
            WHERE id = @BatchId AND tenant_id = @TenantId
              AND quantity_available >= @Quantity
            """;
        var rows = await conn.ExecuteAsync(sql, new { BatchId = batchId, Quantity = quantity, TenantId }, tx);
        if (rows == 0)
            throw new InvalidOperationException("Không đủ tồn lô để xuất.");
    }

    public async Task InsertMovementAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid batchId,
        Guid productId,
        short movementType,
        string referenceType,
        Guid referenceId,
        decimal quantity,
        decimal? unitCost,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO stock_movements (
                tenant_id, warehouse_id, batch_id, product_id,
                movement_type, reference_type, reference_id, quantity, unit_cost, notes
            )
            VALUES (
                @TenantId, @WarehouseId, @BatchId, @ProductId,
                @MovementType, @ReferenceType, @ReferenceId, @Quantity, @UnitCost, @Notes
            )
            """;
        await conn.ExecuteAsync(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            ProductId = productId,
            MovementType = movementType,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Quantity = quantity,
            UnitCost = unitCost,
            Notes = notes,
        }, tx);
    }

    public async Task<string> NextDocumentNumberAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string prefix,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COUNT(*)::int + 1 FROM {tableName} WHERE tenant_id = @TenantId
            """;
        var seq = await conn.QuerySingleAsync<int>(sql, new { TenantId }, tx);
        return $"{prefix}-{seq:D6}";
    }

    public async Task<IReadOnlyList<Guid>> ProcessOpeningBalanceAsync(
        Guid warehouseId,
        string? notes,
        IReadOnlyList<OpeningBalanceLineRequest> lines,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var referenceId = Guid.NewGuid();
        var batchIds = new List<Guid>();

        foreach (var line in lines)
        {
            var batchNumber = line.BatchNumber.Trim();
            var qty = line.Quantity;
            if (qty <= 0) throw new InvalidOperationException("Số lượng phải lớn hơn 0.");

            var existingId = await FindBatchIdByKeyAsync(conn, tx, warehouseId, line.ProductId, batchNumber, cancellationToken);
            Guid batchId;
            if (existingId is Guid id)
            {
                batchId = id;
                await IncreaseBatchQuantityAsync(conn, tx, batchId, qty, cancellationToken);
            }
            else
            {
                batchId = await InsertBatchAsync(
                    conn, tx, warehouseId, line.ProductId, batchNumber,
                    line.ManufactureDate, line.ExpiryDate, line.UnitCost, qty, cancellationToken);
            }

            batchIds.Add(batchId);
            await InsertMovementAsync(
                conn, tx, warehouseId, batchId, line.ProductId,
                StockMovementTypes.In, StockReferenceTypes.OpeningBalance, referenceId,
                qty, line.UnitCost, notes, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return batchIds;
    }

    public async Task<(IReadOnlyList<OpeningBalanceBatchListItemDto> Items, int Total, int SummaryTotal, int SummaryVoidableCount)>
        GetOpeningBalanceBatchesAsync(
            Guid? warehouseId,
            Guid[]? allowedWarehouseIds,
            Guid? productId,
            string? search,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;
        var scopeExtra = new List<string>();
        var extra = new List<string>();
        if (warehouseId is not null)
        {
            scopeExtra.Add("b.warehouse_id = @WarehouseId");
            extra.Add("b.warehouse_id = @WarehouseId");
        }
        else if (allowedWarehouseIds is { Length: > 0 })
        {
            scopeExtra.Add("b.warehouse_id = ANY(@AllowedWarehouseIds)");
            extra.Add("b.warehouse_id = ANY(@AllowedWarehouseIds)");
        }
        if (productId is not null)
            extra.Add("b.product_id = @ProductId");
        if (!string.IsNullOrWhiteSpace(search))
            extra.Add("(p.product_code ILIKE @SearchPattern OR p.product_name ILIKE @SearchPattern OR b.batch_number ILIKE @SearchPattern)");

        const string canVoidSql = """
            (
                b.quantity_available > 0
                AND NOT EXISTS (
                    SELECT 1 FROM stock_movements m
                    WHERE m.batch_id = b.id
                      AND m.reference_type NOT IN (@OpeningRef, @VoidRef)
                )
                AND NOT EXISTS (
                    SELECT 1 FROM inventory_transfer_items ti
                    INNER JOIN inventory_transfers t ON t.id = ti.transfer_id
                    WHERE ti.batch_id = b.id AND t.status <> @TransferCancelled
                )
                AND NOT EXISTS (
                    SELECT 1 FROM inventory_adjustment_items ai
                    INNER JOIN inventory_adjustments a ON a.id = ai.adjustment_id
                    WHERE ai.batch_id = b.id AND a.status <> @AdjustmentCancelled
                )
                AND b.quantity_available = b.quantity_received
            )
            """;

        if (string.Equals(status, "voidable", StringComparison.OrdinalIgnoreCase))
            extra.Add(canVoidSql);
        else if (string.Equals(status, "locked", StringComparison.OrdinalIgnoreCase))
            extra.Add($"NOT {canVoidSql}");

        var whereExtra = extra.Count > 0 ? " AND " + string.Join(" AND ", extra) : "";
        var scopeWhereExtra = scopeExtra.Count > 0 ? " AND " + string.Join(" AND ", scopeExtra) : "";
        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        const string baseFrom = """
            FROM inventory_batches b
            INNER JOIN warehouses w ON w.id = b.warehouse_id
            INNER JOIN products p ON p.id = b.product_id
            INNER JOIN LATERAL (
                SELECT
                    COALESCE(SUM(sm.quantity), 0) AS opening_qty,
                    MIN(sm.movement_date) AS first_opening_date
                FROM stock_movements sm
                WHERE sm.batch_id = b.id AND sm.reference_type = @OpeningRef
            ) ob ON TRUE
            WHERE b.tenant_id = @TenantId
              AND ob.opening_qty > 0
              AND b.quantity_available > 0
            """;

        var countSql = $"""
            SELECT COUNT(*)::int
            {baseFrom}
            {whereExtra}
            """;

        var summaryCountSql = $"""
            SELECT
                COUNT(*)::int AS SummaryTotal,
                COUNT(*) FILTER (WHERE {canVoidSql})::int AS SummaryVoidableCount
            {baseFrom}
            {scopeWhereExtra}
            """;

        var sql = $"""
            SELECT
                b.id AS BatchId,
                b.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                b.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                (SELECT u.unit_name FROM product_units u
                 WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                 ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName,
                b.batch_number AS BatchNumber,
                b.expiry_date AS ExpiryDate,
                b.unit_cost AS UnitCost,
                b.quantity_available AS QuantityAvailable,
                ob.opening_qty AS OpeningQuantity,
                ob.first_opening_date AS FirstOpeningDate,
                {canVoidSql} AS CanVoid,
                CASE
                    WHEN b.quantity_available <= 0 THEN 'Lô đã hết tồn'
                    WHEN EXISTS (
                        SELECT 1 FROM stock_movements m
                        WHERE m.batch_id = b.id
                          AND m.reference_type NOT IN (@OpeningRef, @VoidRef)
                    ) THEN 'Đã phát sinh giao dịch kho'
                    WHEN EXISTS (
                        SELECT 1 FROM inventory_transfer_items ti
                        INNER JOIN inventory_transfers t ON t.id = ti.transfer_id
                        WHERE ti.batch_id = b.id AND t.status <> @TransferCancelled
                    ) THEN 'Lô đang hoặc đã nằm trên phiếu điều chuyển'
                    WHEN EXISTS (
                        SELECT 1 FROM inventory_adjustment_items ai
                        INNER JOIN inventory_adjustments a ON a.id = ai.adjustment_id
                        WHERE ai.batch_id = b.id AND a.status <> @AdjustmentCancelled
                    ) THEN 'Lô đang hoặc đã nằm trên phiếu kiểm kê'
                    WHEN b.quantity_available < b.quantity_received THEN 'Tồn đã thay đổi so với lúc nhập'
                    ELSE NULL
                END AS VoidBlockReason
            {baseFrom}
            {whereExtra}
            ORDER BY b.expiry_date NULLS LAST, p.product_name, b.batch_number
            LIMIT @PageSize OFFSET @Offset
            """;

        var param = new
        {
            TenantId,
            WarehouseId = warehouseId,
            AllowedWarehouseIds = allowedWarehouseIds,
            ProductId = productId,
            SearchPattern = searchPattern,
            OpeningRef = StockReferenceTypes.OpeningBalance,
            VoidRef = StockReferenceTypes.OpeningBalanceVoid,
            TransferCancelled = TransferStatuses.Cancelled,
            AdjustmentCancelled = AdjustmentStatuses.Cancelled,
            PageSize = pageSize,
            Offset = offset,
        };

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.QuerySingleAsync<int>(countSql, param);
        var summary = await conn.QuerySingleAsync<(int SummaryTotal, int SummaryVoidableCount)>(summaryCountSql, param);
        var items = (await conn.QueryAsync<OpeningBalanceBatchListItemDto>(sql, param)).ToList();
        return (items, total, summary.SummaryTotal, summary.SummaryVoidableCount);
    }

    public async Task VoidOpeningBalanceBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string batchSql = """
            SELECT
                id AS Id,
                warehouse_id AS WarehouseId,
                product_id AS ProductId,
                batch_number AS BatchNumber,
                manufacture_date AS ManufactureDate,
                expiry_date AS ExpiryDate,
                unit_cost AS UnitCost,
                quantity_available AS QuantityAvailable,
                quantity_received AS QuantityReceived
            FROM inventory_batches
            WHERE id = @BatchId AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var batch = await conn.QuerySingleOrDefaultAsync<BatchRow>(batchSql, new { BatchId = batchId, TenantId }, tx);
        if (batch is null)
            throw new InvalidOperationException("Lô không tồn tại.");

        const string openingCheckSql = """
            SELECT EXISTS(
                SELECT 1 FROM stock_movements
                WHERE batch_id = @BatchId AND tenant_id = @TenantId
                  AND reference_type = @OpeningRef
            )
            """;
        var hasOpening = await conn.QuerySingleAsync<bool>(openingCheckSql, new
        {
            BatchId = batchId,
            TenantId,
            OpeningRef = StockReferenceTypes.OpeningBalance,
        }, tx);
        if (!hasOpening)
            throw new InvalidOperationException("Lô không phải tồn đầu kỳ.");

        if (batch.QuantityAvailable <= 0)
            throw new InvalidOperationException("Lô đã hết tồn.");

        const string canVoidSql = """
            SELECT (
                @QuantityAvailable > 0
                AND NOT EXISTS (
                    SELECT 1 FROM stock_movements m
                    WHERE m.batch_id = @BatchId
                      AND m.reference_type NOT IN (@OpeningRef, @VoidRef)
                )
                AND NOT EXISTS (
                    SELECT 1 FROM inventory_transfer_items ti
                    INNER JOIN inventory_transfers t ON t.id = ti.transfer_id
                    WHERE ti.batch_id = @BatchId AND t.status <> @TransferCancelled
                )
                AND NOT EXISTS (
                    SELECT 1 FROM inventory_adjustment_items ai
                    INNER JOIN inventory_adjustments a ON a.id = ai.adjustment_id
                    WHERE ai.batch_id = @BatchId AND a.status <> @AdjustmentCancelled
                )
                AND @QuantityAvailable = @QuantityReceived
            )
            """;
        var canVoid = await conn.QuerySingleAsync<bool>(canVoidSql, new
        {
            BatchId = batchId,
            QuantityAvailable = batch.QuantityAvailable,
            QuantityReceived = batch.QuantityReceived,
            OpeningRef = StockReferenceTypes.OpeningBalance,
            VoidRef = StockReferenceTypes.OpeningBalanceVoid,
            TransferCancelled = TransferStatuses.Cancelled,
            AdjustmentCancelled = AdjustmentStatuses.Cancelled,
        }, tx);

        if (!canVoid)
            throw new InvalidOperationException("Không thể xóa: lô đã phát sinh giao dịch. Dùng Kiểm kê để điều chỉnh tồn.");

        var voidReferenceId = Guid.NewGuid();
        var qty = batch.QuantityAvailable;

        await DecreaseBatchQuantityAsync(conn, tx, batch.Id, qty, cancellationToken);
        await InsertMovementAsync(
            conn, tx, batch.WarehouseId, batch.Id, batch.ProductId,
            StockMovementTypes.Out, StockReferenceTypes.OpeningBalanceVoid, voidReferenceId,
            qty, batch.UnitCost, null, cancellationToken);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<Guid> CreateTransferWithItemsAsync(
        Guid fromWarehouseId,
        Guid toWarehouseId,
        string? notes,
        IReadOnlyList<CreateTransferItemRequest> items,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var transferNumber = await NextDocumentNumberAsync(conn, tx, "TR", "inventory_transfers", cancellationToken);
        var transferId = await InsertTransferAsync(conn, tx, transferNumber, fromWarehouseId, toWarehouseId, notes, cancellationToken);

        foreach (var item in items)
        {
            var batch = await GetBatchForUpdateAsync(conn, tx, item.BatchId, cancellationToken)
                ?? throw new InvalidOperationException("Lô không tồn tại.");

            if (batch.WarehouseId != fromWarehouseId)
                throw new InvalidOperationException("Lô không thuộc kho xuất.");
            if (batch.QuantityAvailable < item.Quantity)
                throw new InvalidOperationException($"Không đủ tồn lô {batch.BatchNumber}.");

            await InsertTransferItemAsync(conn, tx, transferId, item.BatchId, batch.ProductId, item.Quantity, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return transferId;
    }

    public async Task<Guid> CreateAdjustmentWithItemsAsync(
        Guid warehouseId,
        string? reason,
        IReadOnlyList<CreateAdjustmentItemRequest> items,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var adjustmentNumber = await NextDocumentNumberAsync(conn, tx, "ADJ", "inventory_adjustments", cancellationToken);
        var adjustmentId = await InsertAdjustmentAsync(
            conn, tx, adjustmentNumber, warehouseId, reason, AdjustmentStatuses.Draft, cancellationToken);

        foreach (var item in items)
        {
            var batch = await GetBatchForUpdateAsync(conn, tx, item.BatchId, cancellationToken)
                ?? throw new InvalidOperationException("Lô không tồn tại.");

            if (batch.WarehouseId != warehouseId)
                throw new InvalidOperationException("Lô không thuộc kho kiểm kê.");

            var diff = item.ActualQuantity - batch.QuantityAvailable;
            await InsertAdjustmentItemAsync(
                conn, tx, adjustmentId, item.BatchId, batch.ProductId,
                batch.QuantityAvailable, item.ActualQuantity, diff, item.Note, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return adjustmentId;
    }

    public async Task<Guid> InsertTransferAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string transferNumber,
        Guid fromWarehouseId,
        Guid toWarehouseId,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory_transfers (
                tenant_id, transfer_number, from_warehouse_id, to_warehouse_id, status, notes
            )
            VALUES (@TenantId, @TransferNumber, @FromWarehouseId, @ToWarehouseId, @Status, @Notes)
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            TransferNumber = transferNumber,
            FromWarehouseId = fromWarehouseId,
            ToWarehouseId = toWarehouseId,
            Status = TransferStatuses.Draft,
            Notes = notes,
        }, tx);
    }

    public async Task InsertTransferItemAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid transferId,
        Guid batchId,
        Guid productId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory_transfer_items (transfer_id, batch_id, product_id, quantity)
            VALUES (@TransferId, @BatchId, @ProductId, @Quantity)
            """;
        await conn.ExecuteAsync(sql, new { TransferId = transferId, BatchId = batchId, ProductId = productId, Quantity = quantity }, tx);
    }

    public async Task CompleteTransferAsync(Guid transferId, Guid approvedBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string headerSql = """
            SELECT id AS Id, from_warehouse_id AS FromWarehouseId, to_warehouse_id AS ToWarehouseId, status AS Status
            FROM inventory_transfers
            WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var header = await conn.QuerySingleOrDefaultAsync<(Guid Id, Guid FromWarehouseId, Guid ToWarehouseId, short Status)>(
            headerSql, new { Id = transferId, TenantId }, tx);
        if (header.Id == Guid.Empty)
            throw new InvalidOperationException("Phiếu điều chuyển không tồn tại.");

        if (header.Status == TransferStatuses.Completed)
            throw new InvalidOperationException("Phiếu đã hoàn tất.");
        if (header.Status == TransferStatuses.Cancelled)
            throw new InvalidOperationException("Phiếu đã hủy.");

        const string itemsSql = """
            SELECT batch_id AS BatchId, product_id AS ProductId, quantity AS Quantity
            FROM inventory_transfer_items WHERE transfer_id = @TransferId
            """;
        var items = (await conn.QueryAsync<(Guid BatchId, Guid ProductId, decimal Quantity)>(itemsSql, new { TransferId = transferId }, tx)).ToList();
        if (items.Count == 0)
            throw new InvalidOperationException("Phiếu không có dòng hàng.");

        foreach (var item in items)
        {
            var source = await GetBatchForUpdateAsync(conn, tx, item.BatchId, cancellationToken)
                ?? throw new InvalidOperationException("Lô nguồn không tồn tại.");

            if (source.WarehouseId != header.FromWarehouseId)
                throw new InvalidOperationException("Lô không thuộc kho xuất.");

            await DecreaseBatchQuantityAsync(conn, tx, source.Id, item.Quantity, cancellationToken);

            var destBatchId = await FindBatchIdByKeyAsync(
                conn, tx, header.ToWarehouseId, source.ProductId, source.BatchNumber, cancellationToken);

            if (destBatchId is Guid existingDest)
            {
                await IncreaseBatchQuantityAsync(conn, tx, existingDest, item.Quantity, cancellationToken);
            }
            else
            {
                destBatchId = await InsertBatchAsync(
                    conn, tx, header.ToWarehouseId, source.ProductId, source.BatchNumber,
                    source.ManufactureDate, source.ExpiryDate, source.UnitCost, item.Quantity, cancellationToken);
            }

            await InsertMovementAsync(
                conn, tx, header.FromWarehouseId, source.Id, source.ProductId,
                StockMovementTypes.Out, StockReferenceTypes.InventoryTransfer, transferId,
                item.Quantity, source.UnitCost, null, cancellationToken);

            await InsertMovementAsync(
                conn, tx, header.ToWarehouseId, destBatchId!.Value, source.ProductId,
                StockMovementTypes.In, StockReferenceTypes.InventoryTransfer, transferId,
                item.Quantity, source.UnitCost, null, cancellationToken);
        }

        const string updateSql = """
            UPDATE inventory_transfers SET
                status = @Status, approved_by = @ApprovedBy, approved_at = NOW(), updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(updateSql, new
        {
            Id = transferId,
            TenantId,
            Status = TransferStatuses.Completed,
            ApprovedBy = approvedBy,
        }, tx);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task CancelTransferAsync(Guid transferId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string headerSql = """
            SELECT id AS Id, status AS Status
            FROM inventory_transfers
            WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var header = await conn.QuerySingleOrDefaultAsync<(Guid Id, short Status)>(
            headerSql, new { Id = transferId, TenantId }, tx);
        if (header.Id == Guid.Empty)
            throw new InvalidOperationException("Phiếu điều chuyển không tồn tại.");

        if (header.Status == TransferStatuses.Completed)
            throw new InvalidOperationException("Phiếu đã hoàn tất — không thể hủy.");
        if (header.Status == TransferStatuses.Cancelled)
            throw new InvalidOperationException("Phiếu đã hủy.");

        const string updateSql = """
            UPDATE inventory_transfers SET
                status = @Status, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(updateSql, new
        {
            Id = transferId,
            TenantId,
            Status = TransferStatuses.Cancelled,
        }, tx);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransferListItemDto>> GetTransfersAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                t.id AS Id,
                t.transfer_number AS TransferNumber,
                t.from_warehouse_id AS FromWarehouseId,
                fw.warehouse_name AS FromWarehouseName,
                t.to_warehouse_id AS ToWarehouseId,
                tw.warehouse_name AS ToWarehouseName,
                t.status AS Status,
                t.transfer_date AS TransferDate,
                (SELECT COUNT(*)::int FROM inventory_transfer_items i WHERE i.transfer_id = t.id) AS ItemCount
            FROM inventory_transfers t
            INNER JOIN warehouses fw ON fw.id = t.from_warehouse_id
            INNER JOIN warehouses tw ON tw.id = t.to_warehouse_id
            WHERE t.tenant_id = @TenantId
            ORDER BY t.transfer_date DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<TransferListItemDto>(sql, new { TenantId })).ToList();
    }

    public async Task<TransferDetailDto?> GetTransferAsync(Guid id, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                t.id AS Id,
                t.transfer_number AS TransferNumber,
                t.from_warehouse_id AS FromWarehouseId,
                fw.warehouse_name AS FromWarehouseName,
                t.to_warehouse_id AS ToWarehouseId,
                tw.warehouse_name AS ToWarehouseName,
                t.status AS Status,
                t.transfer_date AS TransferDate,
                t.notes AS Notes
            FROM inventory_transfers t
            INNER JOIN warehouses fw ON fw.id = t.from_warehouse_id
            INNER JOIN warehouses tw ON tw.id = t.to_warehouse_id
            WHERE t.id = @Id AND t.tenant_id = @TenantId
            """;
        const string itemsSql = """
            SELECT
                i.id AS Id,
                i.batch_id AS BatchId,
                i.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                b.batch_number AS BatchNumber,
                i.quantity AS Quantity
            FROM inventory_transfer_items i
            INNER JOIN products p ON p.id = i.product_id
            INNER JOIN inventory_batches b ON b.id = i.batch_id
            WHERE i.transfer_id = @TransferId
            ORDER BY p.product_name
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<TransferHeaderRow>(headerSql, new { Id = id, TenantId });
        if (header is null) return null;

        var items = (await conn.QueryAsync<TransferItemDto>(itemsSql, new { TransferId = id })).ToList();
        return new TransferDetailDto(
            header.Id,
            header.TransferNumber,
            header.FromWarehouseId,
            header.FromWarehouseName,
            header.ToWarehouseId,
            header.ToWarehouseName,
            header.Status,
            header.TransferDate,
            header.Notes,
            items);
    }

    public async Task<Guid> InsertAdjustmentAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string adjustmentNumber,
        Guid warehouseId,
        string? reason,
        short status,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory_adjustments (tenant_id, warehouse_id, adjustment_number, reason, status)
            VALUES (@TenantId, @WarehouseId, @AdjustmentNumber, @Reason, @Status)
            RETURNING id
            """;
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            AdjustmentNumber = adjustmentNumber,
            Reason = reason,
            Status = status,
        }, tx);
    }

    public async Task<bool> HasActiveCountingSessionAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        return await GetActiveCountingSessionAsync(warehouseId, cancellationToken) is not null;
    }

    public async Task<AdjustmentListItemDto?> GetActiveCountingSessionAsync(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                a.id AS Id,
                a.adjustment_number AS AdjustmentNumber,
                a.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                a.status AS Status,
                a.adjustment_date AS AdjustmentDate,
                (SELECT COUNT(*)::int FROM inventory_adjustment_count_entries e WHERE e.adjustment_id = a.id) AS ItemCount
            FROM inventory_adjustments a
            INNER JOIN warehouses w ON w.id = a.warehouse_id
            WHERE a.tenant_id = @TenantId
              AND a.warehouse_id = @WarehouseId
              AND a.status = @Counting
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AdjustmentListItemDto>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            Counting = AdjustmentStatuses.Counting,
        });
    }

    public async Task<Guid> CreateCountingAdjustmentAsync(
        Guid warehouseId,
        string? reason,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var adjustmentNumber = await NextDocumentNumberAsync(conn, tx, "ADJ", "inventory_adjustments", cancellationToken);
        var adjustmentId = await InsertAdjustmentAsync(
            conn, tx, adjustmentNumber, warehouseId, reason, AdjustmentStatuses.Counting, cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return adjustmentId;
    }

    public async Task<InventoryBarcodeResolveDto?> ResolveInventoryBarcodeAsync(
        Guid warehouseId,
        string barcode,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        const string productSql = """
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                (SELECT u.unit_name FROM product_units u
                 WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                 ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName
            FROM product_barcodes bc
            INNER JOIN products p ON p.id = bc.product_id
            WHERE bc.tenant_id = @TenantId AND bc.barcode = @Barcode
              AND bc.status = 1 AND p.deleted_at IS NULL
            LIMIT 1
            """;
        var product = await conn.QuerySingleOrDefaultAsync<(Guid ProductId, string ProductCode, string ProductName, string? SaleUnitName)?>(
            productSql, new { TenantId, Barcode = barcode });
        if (product is null || product.Value.ProductId == Guid.Empty)
            return null;

        var suggested = await GetFefoBatchSuggestionAsync(conn, warehouseId, product.Value.ProductId, cancellationToken);
        return new InventoryBarcodeResolveDto(
            product.Value.ProductId,
            product.Value.ProductCode,
            product.Value.ProductName,
            product.Value.SaleUnitName,
            suggested?.BatchId,
            suggested?.BatchNumber);
    }

    private sealed record FefoBatchSuggestion(Guid BatchId, string BatchNumber);

    private async Task<FefoBatchSuggestion?> GetFefoBatchSuggestionAsync(
        IDbConnection conn,
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS BatchId, batch_number AS BatchNumber
            FROM inventory_batches
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND product_id = @ProductId
            ORDER BY
                CASE WHEN quantity_available > 0 THEN 0 ELSE 1 END,
                expiry_date ASC NULLS LAST,
                batch_number,
                id
            LIMIT 1
            """;
        return await conn.QuerySingleOrDefaultAsync<FefoBatchSuggestion>(
            sql, new { TenantId, WarehouseId = warehouseId, ProductId = productId });
    }

    public async Task<Guid?> ResolveProductIdByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await ResolveProductIdByBarcodeOnConnectionAsync(conn, barcode, cancellationToken);
    }

    private async Task<Guid?> ResolveProductIdByBarcodeOnConnectionAsync(
        IDbConnection conn,
        string barcode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT bc.product_id
            FROM product_barcodes bc
            INNER JOIN products p ON p.id = bc.product_id
            WHERE bc.tenant_id = @TenantId AND bc.barcode = @Barcode
              AND bc.status = 1 AND p.deleted_at IS NULL
            LIMIT 1
            """;
        var id = await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TenantId, Barcode = barcode });
        return id is null || id == Guid.Empty ? null : id;
    }

    private async Task<bool> ProductExistsOnConnectionAsync(
        IDbConnection conn,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM products WHERE id = @ProductId AND tenant_id = @TenantId AND deleted_at IS NULL
            )
            """;
        return await conn.QuerySingleAsync<bool>(sql, new { ProductId = productId, TenantId });
    }

    private sealed record BatchInfoRow(decimal QuantityAvailable, string BatchNumber);

    private async Task<BatchInfoRow?> GetBatchInfoAsync(
        IDbConnection conn,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT quantity_available AS QuantityAvailable, batch_number AS BatchNumber
            FROM inventory_batches
            WHERE id = @BatchId AND tenant_id = @TenantId
            """;
        return await conn.QuerySingleOrDefaultAsync<BatchInfoRow>(sql, new { BatchId = batchId, TenantId });
    }

    public async Task InsertAdjustmentItemAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid adjustmentId,
        Guid batchId,
        Guid productId,
        decimal systemQty,
        decimal actualQty,
        decimal diffQty,
        string? note,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO inventory_adjustment_items (
                adjustment_id, product_id, batch_id, system_quantity, actual_quantity, difference_quantity, note
            )
            VALUES (@AdjustmentId, @ProductId, @BatchId, @SystemQuantity, @ActualQuantity, @DifferenceQuantity, @Note)
            """;
        await conn.ExecuteAsync(sql, new
        {
            AdjustmentId = adjustmentId,
            ProductId = productId,
            BatchId = batchId,
            SystemQuantity = systemQty,
            ActualQuantity = actualQty,
            DifferenceQuantity = diffQty,
            Note = note,
        }, tx);
    }

    public async Task ApproveAdjustmentAsync(Guid adjustmentId, Guid approvedBy, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string headerSql = """
            SELECT id AS Id, warehouse_id AS WarehouseId, status AS Status
            FROM inventory_adjustments
            WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var header = await conn.QuerySingleOrDefaultAsync<(Guid Id, Guid WarehouseId, short Status)>(
            headerSql, new { Id = adjustmentId, TenantId }, tx);
        if (header.Id == Guid.Empty)
            throw new InvalidOperationException("Phiếu kiểm kê không tồn tại.");

        if (header.Status == AdjustmentStatuses.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt.");
        if (header.Status == AdjustmentStatuses.Cancelled)
            throw new InvalidOperationException("Phiếu đã hủy.");

        List<(Guid BatchId, Guid ProductId, decimal SystemQuantity, decimal ActualQuantity, decimal DifferenceQuantity)> items;

        if (header.Status == AdjustmentStatuses.Counting)
        {
            var entryCount = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*)::int FROM inventory_adjustment_count_entries WHERE adjustment_id = @AdjustmentId",
                new { AdjustmentId = adjustmentId }, tx);
            if (entryCount == 0)
                throw new InvalidOperationException("Phiên kiểm kê chưa có dòng đếm.");

            var missingBatchCount = await conn.QuerySingleAsync<int>(
                """
                SELECT COUNT(*)::int FROM inventory_adjustment_count_entries
                WHERE adjustment_id = @AdjustmentId AND batch_id IS NULL
                """,
                new { AdjustmentId = adjustmentId }, tx);
            if (missingBatchCount > 0)
                throw new InvalidOperationException(
                    $"Còn {missingBatchCount} dòng đếm chưa có lô. Xóa các dòng cũ (nhập trước khi bắt buộc chọn lô) rồi thử lại.");

            items = await BuildAdjustmentItemsFromCountEntriesAsync(
                conn, tx, adjustmentId, header.WarehouseId, cancellationToken);

            foreach (var item in items)
            {
                await InsertAdjustmentItemAsync(
                    conn, tx, adjustmentId, item.BatchId, item.ProductId,
                    item.SystemQuantity, item.ActualQuantity, item.DifferenceQuantity, null, cancellationToken);
            }
        }
        else if (header.Status == AdjustmentStatuses.Draft)
        {
            const string itemsSql = """
                SELECT batch_id AS BatchId, product_id AS ProductId,
                       system_quantity AS SystemQuantity, actual_quantity AS ActualQuantity,
                       difference_quantity AS DifferenceQuantity
                FROM inventory_adjustment_items WHERE adjustment_id = @AdjustmentId
                """;
            items = (await conn.QueryAsync<(Guid BatchId, Guid ProductId, decimal SystemQuantity, decimal ActualQuantity, decimal DifferenceQuantity)>(
                itemsSql, new { AdjustmentId = adjustmentId }, tx)).ToList();
        }
        else
        {
            throw new InvalidOperationException("Trạng thái phiếu không cho phép duyệt.");
        }

        foreach (var item in items)
        {
            if (item.DifferenceQuantity == 0) continue;

            var batch = await GetBatchForUpdateAsync(conn, tx, item.BatchId, cancellationToken)
                ?? throw new InvalidOperationException("Lô không tồn tại.");

            if (batch.WarehouseId != header.WarehouseId)
                throw new InvalidOperationException("Lô không thuộc kho kiểm kê.");

            if (item.DifferenceQuantity > 0)
            {
                await IncreaseBatchQuantityAsync(conn, tx, batch.Id, item.DifferenceQuantity, cancellationToken);
                await InsertMovementAsync(
                    conn, tx, header.WarehouseId, batch.Id, batch.ProductId,
                    StockMovementTypes.In, StockReferenceTypes.InventoryAdjustment, adjustmentId,
                    item.DifferenceQuantity, batch.UnitCost, null, cancellationToken);
            }
            else
            {
                var outQty = Math.Abs(item.DifferenceQuantity);
                await DecreaseBatchQuantityAsync(conn, tx, batch.Id, outQty, cancellationToken);
                await InsertMovementAsync(
                    conn, tx, header.WarehouseId, batch.Id, batch.ProductId,
                    StockMovementTypes.Out, StockReferenceTypes.InventoryAdjustment, adjustmentId,
                    outQty, batch.UnitCost, null, cancellationToken);
            }
        }

        const string updateSql = """
            UPDATE inventory_adjustments SET
                status = @Status, approved_by = @ApprovedBy, approved_at = NOW(), updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(updateSql, new
        {
            Id = adjustmentId,
            TenantId,
            Status = AdjustmentStatuses.Approved,
            ApprovedBy = approvedBy,
        }, tx);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdjustmentCountEntryDto>> AddCountEntriesAsync(
        Guid adjustmentId,
        IReadOnlyList<AddCountEntryRequest> entries,
        Guid counterUserId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var header = await GetAdjustmentHeaderForUpdateAsync(conn, tx, adjustmentId, cancellationToken)
            ?? throw new InvalidOperationException("Phiếu kiểm kê không tồn tại.");
        if (header.Status != AdjustmentStatuses.Counting)
            throw new InvalidOperationException("Chỉ thêm dòng đếm khi phiếu đang kiểm.");

        foreach (var entry in entries)
        {
            var productId = entry.ProductId;
            if (productId is null || productId == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(entry.ScannedBarcode))
                    throw new InvalidOperationException("Cần productId hoặc barcode.");
                productId = await ResolveProductIdByBarcodeOnConnectionAsync(conn, entry.ScannedBarcode.Trim(), cancellationToken)
                    ?? throw new InvalidOperationException($"Không tìm thấy sản phẩm theo barcode: {entry.ScannedBarcode}");
            }

            if (!await ProductExistsOnConnectionAsync(conn, productId.Value, cancellationToken))
                throw new InvalidOperationException("Sản phẩm không tồn tại.");

            if (entry.BatchId is not Guid batchId || batchId == Guid.Empty)
                throw new InvalidOperationException("Phải chọn lô khi ghi nhận đếm.");

            var batch = await GetBatchForUpdateAsync(conn, tx, batchId, cancellationToken)
                ?? throw new InvalidOperationException("Lô không tồn tại.");
            if (batch.WarehouseId != header.WarehouseId)
                throw new InvalidOperationException("Lô không thuộc kho kiểm kê.");
            if (batch.ProductId != productId.Value)
                throw new InvalidOperationException("Lô không thuộc sản phẩm đã chọn.");

            const string insertSql = """
                INSERT INTO inventory_adjustment_count_entries (
                    adjustment_id, product_id, batch_id, quantity, counter_user_id,
                    zone, scanned_barcode, note
                )
                VALUES (
                    @AdjustmentId, @ProductId, @BatchId, @Quantity, @CounterUserId,
                    @Zone, @ScannedBarcode, @Note
                )
                """;
            await conn.ExecuteAsync(insertSql, new
            {
                AdjustmentId = adjustmentId,
                ProductId = productId.Value,
                entry.BatchId,
                entry.Quantity,
                CounterUserId = counterUserId,
                entry.Zone,
                ScannedBarcode = entry.ScannedBarcode?.Trim(),
                entry.Note,
            }, tx);
        }

        await tx.CommitAsync(cancellationToken);
        return await GetCountEntriesAsync(adjustmentId, cancellationToken);
    }

    public async Task DeleteCountEntryAsync(Guid adjustmentId, Guid entryId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var header = await GetAdjustmentHeaderForUpdateAsync(conn, tx, adjustmentId, cancellationToken)
            ?? throw new InvalidOperationException("Phiếu kiểm kê không tồn tại.");
        if (header.Status != AdjustmentStatuses.Counting)
            throw new InvalidOperationException("Chỉ xóa dòng đếm khi phiếu đang kiểm.");

        const string deleteSql = """
            DELETE FROM inventory_adjustment_count_entries
            WHERE id = @EntryId AND adjustment_id = @AdjustmentId
            """;
        var rows = await conn.ExecuteAsync(deleteSql, new { EntryId = entryId, AdjustmentId = adjustmentId }, tx);
        if (rows == 0)
            throw new InvalidOperationException("Dòng đếm không tồn tại.");

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<AdjustmentCountPreviewResultDto> GetCountPreviewAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<(Guid WarehouseId, short Status)>(
            """
            SELECT warehouse_id AS WarehouseId, status AS Status
            FROM inventory_adjustments
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new { Id = adjustmentId, TenantId });
        if (header.WarehouseId == Guid.Empty)
            throw new InvalidOperationException("Phiếu kiểm kê không tồn tại.");

        var groups = await GetCountEntryGroupsAsync(conn, adjustmentId, cancellationToken);
        var byBatch = new List<AdjustmentCountPreviewLineDto>();
        var byProductMap = new Dictionary<Guid, (decimal Counted, int EntryCount)>();

        foreach (var group in groups)
        {
            if (group.BatchId is not Guid batchId)
                continue;

            var productInfo = await GetProductInfoAsync(conn, group.ProductId, cancellationToken);
            var batchInfo = await GetBatchInfoAsync(conn, batchId, cancellationToken);
            var systemQty = batchInfo?.QuantityAvailable ?? 0;
            var diff = group.CountedQuantity - systemQty;
            byBatch.Add(new AdjustmentCountPreviewLineDto(
                group.ProductId,
                productInfo.Code,
                productInfo.Name,
                batchId,
                batchInfo?.BatchNumber,
                group.CountedQuantity,
                systemQty,
                diff,
                group.EntryCount));

            if (byProductMap.TryGetValue(group.ProductId, out var existing))
                byProductMap[group.ProductId] = (existing.Counted + group.CountedQuantity, existing.EntryCount + group.EntryCount);
            else
                byProductMap[group.ProductId] = (group.CountedQuantity, group.EntryCount);
        }

        var byProduct = new List<AdjustmentCountPreviewLineDto>();
        foreach (var (productId, totals) in byProductMap)
        {
            var productInfo = await GetProductInfoAsync(conn, productId, cancellationToken);
            var systemQty = await GetProductSystemQuantityAsync(conn, header.WarehouseId, productId, cancellationToken);
            byProduct.Add(new AdjustmentCountPreviewLineDto(
                productId,
                productInfo.Code,
                productInfo.Name,
                null,
                null,
                totals.Counted,
                systemQty,
                totals.Counted - systemQty,
                totals.EntryCount));
        }

        return new AdjustmentCountPreviewResultDto(
            byBatch.OrderBy(p => p.ProductName).ThenBy(p => p.BatchNumber).ToList(),
            byProduct.OrderBy(p => p.ProductName).ToList());
    }

    public async Task<IReadOnlyList<AdjustmentCountEntryDto>> GetCountEntriesAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                e.id AS Id,
                e.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                e.batch_id AS BatchId,
                b.batch_number AS BatchNumber,
                e.quantity AS Quantity,
                e.counter_user_id AS CounterUserId,
                COALESCE(emp.full_name, u.username) AS CounterUserName,
                e.zone AS Zone,
                e.scanned_barcode AS ScannedBarcode,
                e.note AS Note,
                e.created_at AS CreatedAt
            FROM inventory_adjustment_count_entries e
            INNER JOIN products p ON p.id = e.product_id
            LEFT JOIN inventory_batches b ON b.id = e.batch_id
            LEFT JOIN users u ON u.id = e.counter_user_id
            LEFT JOIN employees emp ON emp.id = u.employee_id
            INNER JOIN inventory_adjustments a ON a.id = e.adjustment_id
            WHERE e.adjustment_id = @AdjustmentId AND a.tenant_id = @TenantId
            ORDER BY e.created_at DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<AdjustmentCountEntryDto>(sql, new { AdjustmentId = adjustmentId, TenantId })).ToList();
    }

    private async Task<(Guid WarehouseId, short Status)?> GetAdjustmentHeaderForUpdateAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT warehouse_id AS WarehouseId, status AS Status
            FROM inventory_adjustments
            WHERE id = @Id AND tenant_id = @TenantId
            FOR UPDATE
            """;
        var row = await conn.QuerySingleOrDefaultAsync<(Guid WarehouseId, short Status)>(
            sql, new { Id = adjustmentId, TenantId }, tx);
        return row.WarehouseId == Guid.Empty ? null : row;
    }

    private sealed record CountEntryGroup(Guid ProductId, Guid? BatchId, decimal CountedQuantity, int EntryCount);

    private async Task<IReadOnlyList<CountEntryGroup>> GetCountEntryGroupsAsync(
        IDbConnection conn,
        Guid adjustmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                product_id AS ProductId,
                batch_id AS BatchId,
                SUM(quantity) AS CountedQuantity,
                COUNT(*)::int AS EntryCount
            FROM inventory_adjustment_count_entries
            WHERE adjustment_id = @AdjustmentId
            GROUP BY product_id, batch_id
            """;
        return (await conn.QueryAsync<CountEntryGroup>(sql, new { AdjustmentId = adjustmentId })).ToList();
    }

    private async Task<(string Code, string Name)> GetProductInfoAsync(
        IDbConnection conn,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT product_code AS Code, product_name AS Name
            FROM products WHERE id = @ProductId AND tenant_id = @TenantId
            """;
        var row = await conn.QuerySingleAsync<(string Code, string Name)>(sql, new { ProductId = productId, TenantId });
        return row;
    }

    private async Task<decimal> GetProductSystemQuantityAsync(
        IDbConnection conn,
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(quantity_available), 0)
            FROM inventory_batches
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND product_id = @ProductId
            """;
        return await conn.QuerySingleAsync<decimal>(sql, new { TenantId, WarehouseId = warehouseId, ProductId = productId });
    }

    private async Task<IReadOnlyList<BatchRow>> GetBatchesFefoAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid warehouseId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                warehouse_id AS WarehouseId,
                product_id AS ProductId,
                batch_number AS BatchNumber,
                manufacture_date AS ManufactureDate,
                expiry_date AS ExpiryDate,
                unit_cost AS UnitCost,
                quantity_available AS QuantityAvailable,
                quantity_received AS QuantityReceived
            FROM inventory_batches
            WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId AND product_id = @ProductId
            ORDER BY expiry_date ASC NULLS LAST, batch_number, id
            """;
        return (await conn.QueryAsync<BatchRow>(sql, new { TenantId, WarehouseId = warehouseId, ProductId = productId }, tx)).ToList();
    }

    private async Task<List<(Guid BatchId, Guid ProductId, decimal SystemQuantity, decimal ActualQuantity, decimal DifferenceQuantity)>>
        BuildAdjustmentItemsFromCountEntriesAsync(
            IDbConnection conn,
            IDbTransaction tx,
            Guid adjustmentId,
            Guid warehouseId,
            CancellationToken cancellationToken)
    {
        var groups = await GetCountEntryGroupsAsync(conn, adjustmentId, cancellationToken);
        var items = new List<(Guid, Guid, decimal, decimal, decimal)>();

        foreach (var group in groups)
        {
            if (group.BatchId is not Guid batchId)
                throw new InvalidOperationException("Mỗi dòng đếm phải có lô. Vui lòng xóa dòng cũ không có lô hoặc thêm lại.");

            var batch = await GetBatchForUpdateAsync(conn, tx, batchId, cancellationToken)
                ?? throw new InvalidOperationException("Lô không tồn tại.");
            if (batch.WarehouseId != warehouseId)
                throw new InvalidOperationException("Lô không thuộc kho kiểm kê.");

            var diff = group.CountedQuantity - batch.QuantityAvailable;
            if (diff != 0)
            {
                items.Add((batchId, group.ProductId, batch.QuantityAvailable, group.CountedQuantity, diff));
            }
        }

        return items;
    }

    private static List<(Guid BatchId, Guid ProductId, decimal SystemQuantity, decimal ActualQuantity, decimal DifferenceQuantity)>
        AllocateDifferenceFefo(Guid productId, decimal difference, IReadOnlyList<BatchRow> batches)
    {
        var result = new List<(Guid, Guid, decimal, decimal, decimal)>();

        if (difference > 0)
        {
            var head = batches.FirstOrDefault()
                ?? throw new InvalidOperationException("Sản phẩm không có lô tại kho để ghi tăng.");
            result.Add((head.Id, productId, head.QuantityAvailable, head.QuantityAvailable + difference, difference));
            return result;
        }

        var remaining = Math.Abs(difference);
        foreach (var batch in batches.Where(b => b.QuantityAvailable > 0))
        {
            if (remaining <= 0) break;
            var take = Math.Min(batch.QuantityAvailable, remaining);
            result.Add((batch.Id, productId, batch.QuantityAvailable, batch.QuantityAvailable - take, -take));
            remaining -= take;
        }

        if (remaining > 0)
            throw new InvalidOperationException("Không đủ tồn lô để ghi giảm theo FEFO.");

        return result;
    }

    public async Task<IReadOnlyList<LowStockProductDto>> GetLowStockProductsAsync(
        Guid? warehouseId,
        Guid[]? allowedWarehouseIds,
        decimal defaultThreshold,
        CancellationToken cancellationToken)
    {
        var threshold = defaultThreshold < 0 ? LowStockThresholdSql.SystemFallback : defaultThreshold;
        var warehouseFilter = warehouseId.HasValue
            ? "AND w.id = @WarehouseId"
            : allowedWarehouseIds is { Length: > 0 }
                ? "AND w.id = ANY(@AllowedWarehouseIds)"
                : string.Empty;
        var effective = LowStockThresholdSql.EffectiveMinStockExpr;
        var sql = $"""
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                (SELECT u.unit_name FROM product_units u
                 WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                 ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName,
                w.id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                br.id AS BranchId,
                br.branch_name AS BranchName,
                COALESCE(SUM(b.quantity_available), 0) AS TotalQuantity,
                {effective} AS MinStockQty,
                COUNT(b.id)::int AS BatchCount
            FROM products p
            INNER JOIN warehouses w
              ON w.tenant_id = p.tenant_id AND w.deleted_at IS NULL AND w.status = 1
            LEFT JOIN branches br
              ON br.id = w.branch_id AND br.tenant_id = p.tenant_id AND br.deleted_at IS NULL
            LEFT JOIN product_categories c
              ON c.id = p.category_id AND c.tenant_id = p.tenant_id AND c.deleted_at IS NULL
            LEFT JOIN inventory_batches b
              ON b.product_id = p.id AND b.warehouse_id = w.id AND b.tenant_id = p.tenant_id
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL AND p.status = 1
              {warehouseFilter}
            GROUP BY
                p.id, p.product_code, p.product_name, p.min_stock_qty,
                w.id, w.warehouse_name, w.min_stock_qty,
                br.id, br.branch_name,
                c.min_stock_qty
            HAVING COALESCE(SUM(b.quantity_available), 0) <= {effective}
            ORDER BY TotalQuantity ASC, w.warehouse_name, p.product_name
            LIMIT 500
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<LowStockProductDto>(sql, new
        {
            TenantId,
            WarehouseId = warehouseId,
            AllowedWarehouseIds = allowedWarehouseIds,
            FallbackThreshold = threshold,
        })).ToList();
    }

    public async Task<bool> UpdateWarehouseMinStockAsync(
        Guid warehouseId,
        decimal? minStockQty,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE warehouses
            SET min_stock_qty = @MinStockQty, updated_at = NOW()
            WHERE id = @WarehouseId AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            WarehouseId = warehouseId,
            TenantId,
            MinStockQty = minStockQty,
        }) > 0;
    }

    public async Task<IReadOnlyList<AdjustmentListItemDto>> GetAdjustmentsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                a.id AS Id,
                a.adjustment_number AS AdjustmentNumber,
                a.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                a.status AS Status,
                a.adjustment_date AS AdjustmentDate,
                (CASE WHEN a.status = @Counting THEN
                    (SELECT COUNT(*)::int FROM inventory_adjustment_count_entries e WHERE e.adjustment_id = a.id)
                 ELSE
                    (SELECT COUNT(*)::int FROM inventory_adjustment_items i WHERE i.adjustment_id = a.id)
                 END) AS ItemCount
            FROM inventory_adjustments a
            INNER JOIN warehouses w ON w.id = a.warehouse_id
            WHERE a.tenant_id = @TenantId
            ORDER BY a.adjustment_date DESC
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<AdjustmentListItemDto>(sql, new { TenantId, Counting = AdjustmentStatuses.Counting })).ToList();
    }

    public async Task<AdjustmentDetailDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                a.id AS Id,
                a.adjustment_number AS AdjustmentNumber,
                a.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                a.status AS Status,
                a.adjustment_date AS AdjustmentDate,
                a.reason AS Reason
            FROM inventory_adjustments a
            INNER JOIN warehouses w ON w.id = a.warehouse_id
            WHERE a.id = @Id AND a.tenant_id = @TenantId
            """;
        const string itemsSql = """
            SELECT
                i.id AS Id,
                i.batch_id AS BatchId,
                i.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                b.batch_number AS BatchNumber,
                i.system_quantity AS SystemQuantity,
                i.actual_quantity AS ActualQuantity,
                i.difference_quantity AS DifferenceQuantity,
                i.note AS Note
            FROM inventory_adjustment_items i
            INNER JOIN products p ON p.id = i.product_id
            INNER JOIN inventory_batches b ON b.id = i.batch_id
            WHERE i.adjustment_id = @AdjustmentId
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<AdjustmentHeaderRow>(headerSql, new { Id = id, TenantId });
        if (header is null) return null;

        var items = (await conn.QueryAsync<AdjustmentItemDto>(itemsSql, new { AdjustmentId = id })).ToList();
        return new AdjustmentDetailDto(
            header.Id,
            header.AdjustmentNumber,
            header.WarehouseId,
            header.WarehouseName,
            header.Status,
            header.AdjustmentDate,
            header.Reason,
            items);
    }

    private sealed class TransferHeaderRow
    {
        public Guid Id { get; init; }
        public string TransferNumber { get; init; } = "";
        public Guid FromWarehouseId { get; init; }
        public string FromWarehouseName { get; init; } = "";
        public Guid ToWarehouseId { get; init; }
        public string ToWarehouseName { get; init; } = "";
        public short Status { get; init; }
        public DateTime TransferDate { get; init; }
        public string? Notes { get; init; }
    }

    private sealed class AdjustmentHeaderRow
    {
        public Guid Id { get; init; }
        public string AdjustmentNumber { get; init; } = "";
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public short Status { get; init; }
        public DateTime AdjustmentDate { get; init; }
        public string? Reason { get; init; }
    }
}
