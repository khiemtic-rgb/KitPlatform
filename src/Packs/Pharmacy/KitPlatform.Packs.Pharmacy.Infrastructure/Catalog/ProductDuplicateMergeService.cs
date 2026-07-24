using System.Data;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Packs.Pharmacy.Inventory;
using Npgsql;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class ProductDuplicateMergeService : IProductDuplicateMergeService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly InventoryRepository _inventory;
    private readonly CatalogRepository _catalog;

    public ProductDuplicateMergeService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        InventoryRepository inventory,
        CatalogRepository catalog)
    {
        _db = db;
        _tenant = tenant;
        _inventory = inventory;
        _catalog = catalog;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<DuplicateProductClustersResult> GetDuplicateClustersAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH active AS (
                SELECT
                    p.id AS Id,
                    p.product_code AS ProductCode,
                    p.product_name AS ProductName,
                    COALESCE(NULLIF(TRIM(p.product_name_normalized), ''), lower(trim(p.product_name))) AS NormalizedName,
                    COALESCE((
                        SELECT u.unit_name
                        FROM product_units u
                        WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                        ORDER BY u.is_base_unit DESC, u.unit_name
                        LIMIT 1
                    ), '') AS UnitName,
                    COALESCE((
                        SELECT SUM(b.quantity_available)
                        FROM inventory_batches b
                        WHERE b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.quantity_available > 0
                    ), 0) AS TotalQuantity,
                    COALESCE((
                        SELECT COUNT(DISTINCT b.warehouse_id)::int
                        FROM inventory_batches b
                        WHERE b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.quantity_available > 0
                    ), 0) AS WarehouseCount
                FROM products p
                WHERE p.tenant_id = @TenantId
                  AND p.deleted_at IS NULL
                  AND p.status = 1
            )
            SELECT *
            FROM active a
            WHERE EXISTS (
                SELECT 1 FROM active b
                WHERE b.NormalizedName = a.NormalizedName AND b.Id <> a.Id
            )
            ORDER BY a.NormalizedName, a.ProductCode
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<ClusterMemberRow>(sql, new { TenantId })).ToList();
        if (rows.Count == 0)
            return new DuplicateProductClustersResult([], 0, 0);

        var clusters = new List<DuplicateProductClusterDto>();
        foreach (var group in rows.GroupBy(r => r.NormalizedName, StringComparer.Ordinal))
        {
            var members = group.ToList();
            var keeperId = SuggestKeeperId(members);
            var products = members
                .Select(m => new DuplicateProductMemberDto(
                    m.Id,
                    m.ProductCode,
                    m.ProductName,
                    m.UnitName,
                    m.TotalQuantity,
                    m.WarehouseCount,
                    m.Id == keeperId))
                .ToList();

            clusters.Add(new DuplicateProductClusterDto(
                group.Key,
                members[0].ProductName,
                products));
        }

        return new DuplicateProductClustersResult(
            clusters,
            clusters.Count,
            rows.Count,
            SimilarityThreshold: null);
    }

    public async Task<DuplicateProductClustersResult> GetSimilarClustersAsync(
        double similarityThreshold = 0.8,
        CancellationToken cancellationToken = default)
    {
        var threshold = Math.Clamp(similarityThreshold, 0.5, 0.99);

        // Self-join via pg_trgm `%` + GIN (ix_products_name_normalized_trgm).
        // Full nested CTE cross-join is O(n²) and times out (~8k SKUs → 30s+).
        const string pairSql = """
            SELECT
                a.id AS IdA,
                b.id AS IdB,
                similarity(a.product_name_normalized, b.product_name_normalized)::float8 AS Score
            FROM products a
            INNER JOIN products b
              ON a.tenant_id = b.tenant_id
             AND a.id < b.id
             AND a.product_name_normalized % b.product_name_normalized
            WHERE a.tenant_id = @TenantId
              AND a.deleted_at IS NULL AND a.status = 1
              AND b.deleted_at IS NULL AND b.status = 1
              AND a.product_name_normalized IS NOT NULL
              AND b.product_name_normalized IS NOT NULL
              AND length(a.product_name_normalized) >= 3
              AND length(b.product_name_normalized) >= 3
              AND a.product_name_normalized <> b.product_name_normalized
              AND similarity(a.product_name_normalized, b.product_name_normalized) >= @Threshold
            ORDER BY Score DESC
            LIMIT 3000
            """;

        const string productByIdsSql = """
            SELECT
                p.id AS Id,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE(NULLIF(TRIM(p.product_name_normalized), ''), lower(trim(p.product_name))) AS NormalizedName,
                COALESCE((
                    SELECT u.unit_name
                    FROM product_units u
                    WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                    ORDER BY u.is_base_unit DESC, u.unit_name
                    LIMIT 1
                ), '') AS UnitName,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.quantity_available > 0
                ), 0) AS TotalQuantity,
                COALESCE((
                    SELECT COUNT(DISTINCT b.warehouse_id)::int
                    FROM inventory_batches b
                    WHERE b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.quantity_available > 0
                ), 0) AS WarehouseCount
            FROM products p
            WHERE p.tenant_id = @TenantId
              AND p.deleted_at IS NULL
              AND p.id = ANY(@Ids)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        List<SimilarPairRow> pairs;
        try
        {
            await conn.ExecuteAsync(
                "SELECT set_config('pg_trgm.similarity_threshold', @Th, true)",
                new { Th = threshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) });

            pairs = (await conn.QueryAsync<SimilarPairRow>(
                    pairSql,
                    new { TenantId, Threshold = threshold }))
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState is "42883" or "42704")
        {
            // pg_trgm missing / unavailable — no fuzzy clusters
            return new DuplicateProductClustersResult([], 0, 0, threshold);
        }

        if (pairs.Count == 0)
            return new DuplicateProductClustersResult([], 0, 0, threshold);

        var parent = new Dictionary<Guid, Guid>();
        Guid Find(Guid x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        void Union(Guid a, Guid b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) parent[rb] = ra;
        }

        var maxPairScore = new Dictionary<Guid, double>();
        foreach (var pair in pairs)
        {
            Union(pair.IdA, pair.IdB);
            maxPairScore[pair.IdA] = Math.Max(maxPairScore.GetValueOrDefault(pair.IdA), pair.Score);
            maxPairScore[pair.IdB] = Math.Max(maxPairScore.GetValueOrDefault(pair.IdB), pair.Score);
        }

        var involvedIds = parent.Keys.ToArray();
        var products = (await conn.QueryAsync<ClusterMemberRow>(
                productByIdsSql,
                new { TenantId, Ids = involvedIds }))
            .ToDictionary(p => p.Id);

        var groups = new Dictionary<Guid, List<ClusterMemberRow>>();
        var groupMaxScore = new Dictionary<Guid, double>();
        foreach (var id in involvedIds)
        {
            if (!products.TryGetValue(id, out var row)) continue;
            var root = Find(id);
            if (!groups.TryGetValue(root, out var list))
            {
                list = [];
                groups[root] = list;
            }
            list.Add(row);
            groupMaxScore[root] = Math.Max(
                groupMaxScore.GetValueOrDefault(root),
                maxPairScore.GetValueOrDefault(id));
        }

        var clusters = new List<DuplicateProductClusterDto>();
        foreach (var (root, members) in groups.Where(g => g.Value.Count >= 2))
        {
            members.Sort((a, b) =>
                b.TotalQuantity.CompareTo(a.TotalQuantity) != 0
                    ? b.TotalQuantity.CompareTo(a.TotalQuantity)
                    : string.Compare(a.ProductCode, b.ProductCode, StringComparison.Ordinal));
            var keeperId = SuggestKeeperId(members);
            var display = members.FirstOrDefault(m => m.Id == keeperId)?.ProductName
                ?? members[0].ProductName;
            var key = $"sim:{root:N}";
            clusters.Add(new DuplicateProductClusterDto(
                key,
                display,
                members.Select(m => new DuplicateProductMemberDto(
                    m.Id,
                    m.ProductCode,
                    m.ProductName,
                    m.UnitName,
                    m.TotalQuantity,
                    m.WarehouseCount,
                    m.Id == keeperId)).ToList(),
                MaxSimilarity: Math.Round(groupMaxScore.GetValueOrDefault(root), 4)));
        }

        clusters = clusters
            .OrderByDescending(c => c.MaxSimilarity ?? 0)
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(150)
            .ToList();

        return new DuplicateProductClustersResult(
            clusters,
            clusters.Count,
            clusters.Sum(c => c.Products.Count),
            threshold);
    }

    public async Task<MergeDuplicateProductStockResult> MergeStockAsync(
        MergeDuplicateProductStockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.KeeperProductId == request.SourceProductId)
            throw new InvalidOperationException("Mã giữ và mã nguồn phải khác nhau.");
        if (request.ConversionFactor <= 0)
            throw new InvalidOperationException("Hệ số quy đổi phải lớn hơn 0.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var keeper = await LoadProductAsync(conn, tx, request.KeeperProductId);
        var source = await LoadProductAsync(conn, tx, request.SourceProductId);
        if (keeper is null)
            throw new InvalidOperationException("Sản phẩm giữ không tồn tại hoặc đã xóa.");
        if (source is null)
            throw new InvalidOperationException("Sản phẩm nguồn không tồn tại hoặc đã xóa.");

        const string batchesSql = """
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
            WHERE tenant_id = @TenantId
              AND product_id = @ProductId
              AND quantity_available > 0
            ORDER BY warehouse_id, expiry_date NULLS LAST, batch_number
            FOR UPDATE
            """;

        var sourceBatches = (await conn.QueryAsync<MergeBatchRow>(
            batchesSql,
            new { TenantId, ProductId = request.SourceProductId },
            tx)).ToList();

        var warehouseNames = await LoadWarehouseNamesAsync(conn, tx, sourceBatches.Select(b => b.WarehouseId).Distinct());

        var mergeId = Guid.NewGuid();
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? $"Gộp tồn {source.ProductCode} → {keeper.ProductCode} (×{request.ConversionFactor})"
            : request.Reason.Trim();

        var lines = new List<MergeDuplicateStockLineDto>();
        decimal totalSource = 0;
        decimal totalAdded = 0;

        foreach (var batch in sourceBatches)
        {
            var qty = batch.QuantityAvailable;
            if (qty <= 0) continue;

            var added = RoundQty(qty * request.ConversionFactor);
            if (added <= 0)
                throw new InvalidOperationException(
                    $"Hệ số quy đổi quá nhỏ cho lô {batch.BatchNumber} (tồn {qty}).");

            var unitCostForKeeper = request.ConversionFactor == 0
                ? batch.UnitCost
                : RoundMoney(batch.UnitCost / request.ConversionFactor);

            await _inventory.DecreaseBatchQuantityAsync(conn, tx, batch.Id, qty, cancellationToken);
            await _inventory.InsertMovementAsync(
                conn, tx, batch.WarehouseId, batch.Id, batch.ProductId,
                StockMovementTypes.Out, StockReferenceTypes.ProductMerge, mergeId,
                qty, batch.UnitCost, reason, cancellationToken);

            var keeperBatchId = await _inventory.FindBatchIdByKeyAsync(
                conn, tx, batch.WarehouseId, request.KeeperProductId, batch.BatchNumber, cancellationToken);

            if (keeperBatchId is null)
            {
                keeperBatchId = await _inventory.InsertBatchAsync(
                    conn, tx,
                    batch.WarehouseId,
                    request.KeeperProductId,
                    batch.BatchNumber,
                    batch.ManufactureDate,
                    batch.ExpiryDate,
                    unitCostForKeeper,
                    added,
                    cancellationToken);
            }
            else
            {
                await _inventory.IncreaseBatchQuantityAsync(conn, tx, keeperBatchId.Value, added, cancellationToken);
            }

            await _inventory.InsertMovementAsync(
                conn, tx, batch.WarehouseId, keeperBatchId.Value, request.KeeperProductId,
                StockMovementTypes.In, StockReferenceTypes.ProductMerge, mergeId,
                added, unitCostForKeeper, reason, cancellationToken);

            warehouseNames.TryGetValue(batch.WarehouseId, out var whName);
            lines.Add(new MergeDuplicateStockLineDto(
                batch.WarehouseId,
                whName ?? batch.WarehouseId.ToString(),
                batch.Id,
                batch.BatchNumber,
                keeperBatchId.Value,
                qty,
                added));

            totalSource += qty;
            totalAdded += added;
        }

        var softDeleted = false;
        if (request.SoftDeleteSource)
        {
            softDeleted = await _catalog.SoftDeleteProductAsync(conn, tx, request.SourceProductId, cancellationToken);
            if (!softDeleted)
                throw new InvalidOperationException("Không ẩn được sản phẩm nguồn sau khi gộp tồn.");
        }

        await tx.CommitAsync(cancellationToken);

        return new MergeDuplicateProductStockResult(
            request.KeeperProductId,
            request.SourceProductId,
            request.ConversionFactor,
            softDeleted,
            totalSource,
            totalAdded,
            lines);
    }

    public async Task<ProductMergeHistoryResult> GetMergeHistoryAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        const string sql = """
            WITH merge_ids AS (
                SELECT m.reference_id AS MergeId,
                       MIN(m.created_at) AS MergedAt,
                       MAX(m.notes) AS Notes
                FROM stock_movements m
                WHERE m.tenant_id = @TenantId
                  AND m.reference_type = @RefType
                GROUP BY m.reference_id
                ORDER BY MIN(m.created_at) DESC
                LIMIT @Limit
            ),
            src AS (
                SELECT m.reference_id AS MergeId,
                       m.product_id AS ProductId,
                       SUM(m.quantity) AS Qty
                FROM stock_movements m
                INNER JOIN merge_ids x ON x.MergeId = m.reference_id
                WHERE m.tenant_id = @TenantId
                  AND m.reference_type = @RefType
                  AND m.movement_type = @OutType
                GROUP BY m.reference_id, m.product_id
            ),
            dst AS (
                SELECT m.reference_id AS MergeId,
                       m.product_id AS ProductId,
                       SUM(m.quantity) AS Qty
                FROM stock_movements m
                INNER JOIN merge_ids x ON x.MergeId = m.reference_id
                WHERE m.tenant_id = @TenantId
                  AND m.reference_type = @RefType
                  AND m.movement_type = @InType
                GROUP BY m.reference_id, m.product_id
            )
            SELECT
                x.MergeId,
                x.MergedAt,
                s.ProductId AS SourceProductId,
                COALESCE(sp.product_code, '') AS SourceProductCode,
                COALESCE(sp.product_name, '') AS SourceProductName,
                d.ProductId AS KeeperProductId,
                COALESCE(kp.product_code, '') AS KeeperProductCode,
                COALESCE(kp.product_name, '') AS KeeperProductName,
                COALESCE(s.Qty, 0) AS SourceQuantity,
                COALESCE(d.Qty, 0) AS KeeperQuantityAdded,
                x.Notes,
                (sp.id IS NOT NULL AND sp.deleted_at IS NOT NULL) AS SourceStillHidden
            FROM merge_ids x
            LEFT JOIN src s ON s.MergeId = x.MergeId
            LEFT JOIN dst d ON d.MergeId = x.MergeId
            LEFT JOIN products sp ON sp.id = s.ProductId AND sp.tenant_id = @TenantId
            LEFT JOIN products kp ON kp.id = d.ProductId AND kp.tenant_id = @TenantId
            ORDER BY x.MergedAt DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<ProductMergeHistoryRow>(sql, new
        {
            TenantId,
            RefType = StockReferenceTypes.ProductMerge,
            OutType = StockMovementTypes.Out,
            InType = StockMovementTypes.In,
            Limit = limit,
        })).ToList();

        var items = rows.Select(r => new ProductMergeHistoryItemDto(
            r.MergeId,
            r.MergedAt,
            r.SourceProductId,
            r.SourceProductCode ?? string.Empty,
            r.SourceProductName ?? string.Empty,
            r.KeeperProductId,
            r.KeeperProductCode ?? string.Empty,
            r.KeeperProductName ?? string.Empty,
            r.SourceQuantity,
            r.KeeperQuantityAdded,
            r.Notes,
            r.SourceStillHidden)).ToList();

        return new ProductMergeHistoryResult(items, items.Count);
    }

    public async Task<HiddenProductsResult> GetHiddenProductsAsync(
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        const string sql = """
            SELECT
                p.id AS Id,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE((
                    SELECT u.unit_name FROM product_units u
                    WHERE u.product_id = p.id AND u.is_sale_unit = TRUE AND u.status = 1
                    ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1
                ), '') AS UnitName,
                p.deleted_at AS DeletedAt,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.quantity_available > 0
                ), 0) AS RemainingStock,
                EXISTS (
                    SELECT 1 FROM stock_movements m
                    WHERE m.tenant_id = p.tenant_id
                      AND m.product_id = p.id
                      AND m.reference_type = @RefType
                      AND m.movement_type = @OutType
                ) AS MergedAway
            FROM products p
            WHERE p.tenant_id = @TenantId
              AND p.deleted_at IS NOT NULL
            ORDER BY p.deleted_at DESC NULLS LAST, p.product_code
            LIMIT @Limit
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<HiddenProductRow>(sql, new
        {
            TenantId,
            RefType = StockReferenceTypes.ProductMerge,
            OutType = StockMovementTypes.Out,
            Limit = limit,
        })).ToList();

        var items = new List<HiddenProductItemDto>();
        foreach (var row in rows)
        {
            var (canHardDelete, reasons) = await EvaluateHardDeleteAsync(conn, row.Id, row.RemainingStock, cancellationToken);
            items.Add(new HiddenProductItemDto(
                row.Id,
                row.ProductCode ?? string.Empty,
                row.ProductName ?? string.Empty,
                row.UnitName ?? string.Empty,
                row.DeletedAt,
                row.RemainingStock,
                canHardDelete,
                reasons,
                row.MergedAway));
        }

        return new HiddenProductsResult(items, items.Count);
    }

    public async Task<bool> RestoreHiddenProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        // Unique code may collide with an active product
        const string conflictSql = """
            SELECT EXISTS(
                SELECT 1
                FROM products hidden
                INNER JOIN products active
                  ON active.tenant_id = hidden.tenant_id
                 AND UPPER(active.product_code) = UPPER(hidden.product_code)
                 AND active.deleted_at IS NULL
                 AND active.id <> hidden.id
                WHERE hidden.id = @Id AND hidden.tenant_id = @TenantId AND hidden.deleted_at IS NOT NULL
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var conflict = await conn.QuerySingleAsync<bool>(conflictSql, new { Id = productId, TenantId });
        if (conflict)
            throw new InvalidOperationException("Không khôi phục được: đã có sản phẩm đang bán cùng mã.");

        const string sql = """
            UPDATE products SET deleted_at = NULL, status = 1, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NOT NULL
            """;
        return await conn.ExecuteAsync(sql, new { Id = productId, TenantId }) > 0;
    }

    public async Task<HardDeleteProductResult> HardDeleteHiddenProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string loadSql = """
            SELECT id AS Id,
                   COALESCE((
                       SELECT SUM(b.quantity_available)
                       FROM inventory_batches b
                       WHERE b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.quantity_available > 0
                   ), 0) AS RemainingStock
            FROM products p
            WHERE p.id = @Id AND p.tenant_id = @TenantId AND p.deleted_at IS NOT NULL
            FOR UPDATE
            """;
        var row = await conn.QuerySingleOrDefaultAsync<(Guid Id, decimal RemainingStock)>(
            loadSql, new { Id = productId, TenantId }, tx);
        if (row.Id == Guid.Empty)
            throw new InvalidOperationException("Sản phẩm không tồn tại trong danh sách đã ẩn.");

        var (canHardDelete, reasons) = await EvaluateHardDeleteAsync(conn, productId, row.RemainingStock, cancellationToken, tx);
        if (!canHardDelete)
            throw new InvalidOperationException(string.Join(" ", reasons));

        // Remove empty batches (no stock) so CASCADE path is cleaner; movements already checked = 0
        await conn.ExecuteAsync(
            """
            DELETE FROM inventory_batches
            WHERE tenant_id = @TenantId AND product_id = @Id AND quantity_available <= 0
            """,
            new { TenantId, Id = productId }, tx);

        var deleted = await conn.ExecuteAsync(
            "DELETE FROM products WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NOT NULL",
            new { Id = productId, TenantId }, tx);

        if (deleted == 0)
            throw new InvalidOperationException("Không xóa được sản phẩm.");

        await tx.CommitAsync(cancellationToken);
        return new HardDeleteProductResult(productId, true);
    }

    private async Task<(bool CanHardDelete, IReadOnlyList<string> Reasons)> EvaluateHardDeleteAsync(
        IDbConnection conn,
        Guid productId,
        decimal remainingStock,
        CancellationToken cancellationToken,
        IDbTransaction? tx = null)
    {
        var reasons = new List<string>();
        if (remainingStock > 0)
            reasons.Add($"Còn tồn {remainingStock:0.####} — cần gộp/điều chỉnh về 0 trước.");

        async Task<bool> Exists(string sql) =>
            await conn.QuerySingleAsync<bool>(sql, new { TenantId, ProductId = productId }, tx);

        if (await Exists("""
            SELECT EXISTS(
                SELECT 1 FROM stock_movements
                WHERE tenant_id = @TenantId AND product_id = @ProductId
            )
            """))
            reasons.Add("Còn lịch sử kho (xuất/nhập/gộp) — không xóa cứng được.");

        if (await Exists("""
            SELECT EXISTS(
                SELECT 1 FROM sales_order_items
                WHERE product_id = @ProductId
            )
            """))
            reasons.Add("Còn dòng bán hàng gắn mã này.");

        if (await Exists("""
            SELECT EXISTS(
                SELECT 1 FROM purchase_order_items
                WHERE product_id = @ProductId
            )
            """))
            reasons.Add("Còn dòng đơn mua gắn mã này.");

        if (await Exists("""
            SELECT EXISTS(
                SELECT 1 FROM goods_receipt_items
                WHERE product_id = @ProductId
            )
            """))
            reasons.Add("Còn dòng phiếu nhập gắn mã này.");

        if (await Exists("""
            SELECT EXISTS(
                SELECT 1 FROM inventory_batches
                WHERE tenant_id = @TenantId AND product_id = @ProductId AND quantity_available > 0
            )
            """))
        {
            // already covered by remainingStock; keep single message
        }

        return (reasons.Count == 0, reasons);
    }

    private async Task<ProductLite?> LoadProductAsync(IDbConnection conn, IDbTransaction tx, Guid id)
    {
        const string sql = """
            SELECT id AS Id, product_code AS ProductCode, product_name AS ProductName
            FROM products
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            FOR UPDATE
            """;
        return await conn.QuerySingleOrDefaultAsync<ProductLite>(sql, new { Id = id, TenantId }, tx);
    }

    private async Task<Dictionary<Guid, string>> LoadWarehouseNamesAsync(
        IDbConnection conn,
        IDbTransaction tx,
        IEnumerable<Guid> warehouseIds)
    {
        var ids = warehouseIds.ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, string>();

        const string sql = """
            SELECT id AS Id, warehouse_name AS Name
            FROM warehouses
            WHERE tenant_id = @TenantId AND id = ANY(@Ids)
            """;
        var rows = await conn.QueryAsync<(Guid Id, string Name)>(sql, new { TenantId, Ids = ids }, tx);
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    private static Guid SuggestKeeperId(IReadOnlyList<ClusterMemberRow> members)
    {
        return members
            .OrderBy(m => UnitSizeRank(m.UnitName))
            .ThenByDescending(m => m.TotalQuantity)
            .ThenBy(m => m.ProductCode, StringComparer.OrdinalIgnoreCase)
            .First()
            .Id;
    }

    /// <summary>ĐVT nhỏ hơn → rank thấp hơn → ưu tiên làm mã giữ.</summary>
    private static int UnitSizeRank(string? unitName)
    {
        var u = (unitName ?? string.Empty).Trim().ToLowerInvariant();
        if (u.Length == 0) return 50;

        if (ContainsAny(u, "thùng", "thung", "lốc", "loc", "két", "ket", "carton"))
            return 90;
        if (ContainsAny(u, "hộp", "hop", "box"))
            return 80;
        if (ContainsAny(u, "lọ", "lo", "chai", "tub", "tuýp", "tuyp", "tube"))
            return 40;
        if (ContainsAny(u, "vỉ", "vi", "blister"))
            return 20;
        if (ContainsAny(u, "viên", "vien", "gói", "goi", "ml", "g"))
            return 10;
        return 50;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

    private static decimal RoundQty(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class ProductMergeHistoryRow
    {
        public Guid MergeId { get; init; }
        public DateTimeOffset MergedAt { get; init; }
        public Guid? SourceProductId { get; init; }
        public string? SourceProductCode { get; init; }
        public string? SourceProductName { get; init; }
        public Guid? KeeperProductId { get; init; }
        public string? KeeperProductCode { get; init; }
        public string? KeeperProductName { get; init; }
        public decimal SourceQuantity { get; init; }
        public decimal KeeperQuantityAdded { get; init; }
        public string? Notes { get; init; }
        public bool SourceStillHidden { get; init; }
    }

    private sealed record ClusterMemberRow(
        Guid Id,
        string ProductCode,
        string ProductName,
        string NormalizedName,
        string UnitName,
        decimal TotalQuantity,
        int WarehouseCount);

    private sealed record SimilarPairRow(Guid IdA, Guid IdB, double Score);

    private sealed record ProductLite(Guid Id, string ProductCode, string ProductName);

    private sealed class HiddenProductRow
    {
        public Guid Id { get; init; }
        public string? ProductCode { get; init; }
        public string? ProductName { get; init; }
        public string? UnitName { get; init; }
        public DateTimeOffset? DeletedAt { get; init; }
        public decimal RemainingStock { get; init; }
        public bool MergedAway { get; init; }
    }

    private sealed record MergeBatchRow(
        Guid Id,
        Guid WarehouseId,
        Guid ProductId,
        string BatchNumber,
        DateOnly? ManufactureDate,
        DateOnly? ExpiryDate,
        decimal UnitCost,
        decimal QuantityAvailable,
        decimal QuantityReceived);
}
