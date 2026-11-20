using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Reports;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Procurement;
using Npgsql;
using Xunit;

namespace KitPlatform.Tests;

/// <summary>
/// XNT on the isolated staff-acceptance database. Refuses shared and production database names.
/// </summary>
public sealed class InventoryMovementSummaryTests
{
    private const string ConnectionEnvironmentVariable = "POS_IDEMPOTENCY_TEST_CONNECTION";

    private static readonly HashSet<string> DeniedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitplatform",
        "kitplatform_test",
        "postgres",
        "novixa_prod",
        "pharmacore",
    };

    [Fact]
    public async Task Movement_summary_reconciles_opening_in_out_adjustment_and_closing()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Fail($"Set {ConnectionEnvironmentVariable} to the isolated database before this test.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (DeniedDatabases.Contains(builder.Database ?? "")
            || (builder.Database ?? "").Contains("prod", StringComparison.OrdinalIgnoreCase))
            Assert.Fail($"Refusing database '{builder.Database}'.");

        var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseA = Guid.NewGuid();
        var warehouseB = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var deletedProductId = Guid.NewGuid();
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();
        var deletedBatch = Guid.NewGuid();
        var code = "XNT" + suffix;

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
        var port = await conn.ExecuteScalarAsync<int>("SELECT inet_server_port()");
        if (!string.Equals(database, "pos_idem_staff_accept", StringComparison.Ordinal) || port != 5433)
            Assert.Fail($"Refusing database '{database}' on port {port}.");

        var forbidden = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM tenants
            WHERE tenant_code IN ('DEMO_PHARMACY', 'NT_XUANHOA', 'KIT_MKT')
            """);
        if (forbidden != 0)
            Assert.Fail("Refusing database that already contains a real tenant code.");

        try
        {
            await SeedAsync(conn, tenantId, otherTenantId, branchId, warehouseA, warehouseB, productId, deletedProductId, batchA, batchB, deletedBatch, code, from, to);

            var repository = new ReportsRepository(new NpgsqlConnectionFactory(builder.ConnectionString), new FixedTenant(tenantId));
            var rows = await repository.GetInventoryMovementSummaryAsync(from, to, null, null, code, CancellationToken.None);

            var rowA = One(rows, "Kho A");
            var rowB = One(rows, "Kho B");
            Assert.Equal(2, rows.Count);
            Assert.Equal(productId, Assert.IsType<Guid>(rowA["productId"]));
            Assert.Equal(warehouseA, Assert.IsType<Guid>(rowA["warehouseId"]));
            Assert.Equal(90m, Qty(rowA, "openingQty"));
            Assert.Equal(37m, Qty(rowA, "inQty"));
            Assert.Equal(14m, Qty(rowA, "outQty"));
            Assert.Equal(2m, Qty(rowA, "adjustmentInQty"));
            Assert.Equal(1m, Qty(rowA, "adjustmentOutQty"));
            Assert.Equal(113m, Qty(rowA, "closingQty"));
            Assert.Equal(Qty(rowA, "openingQty") + Qty(rowA, "inQty") - Qty(rowA, "outQty"), Qty(rowA, "closingQty"));

            Assert.Equal(20m, Qty(rowB, "openingQty"));
            Assert.Equal(4m, Qty(rowB, "inQty"));
            Assert.Equal(5m, Qty(rowB, "outQty"));
            Assert.Equal(0m, Qty(rowB, "adjustmentInQty"));
            Assert.Equal(0m, Qty(rowB, "adjustmentOutQty"));
            Assert.Equal(19m, Qty(rowB, "closingQty"));

            var onlyA = await repository.GetInventoryMovementSummaryAsync(from, to, warehouseA, null, code, CancellationToken.None);
            Assert.Single(onlyA);
            Assert.Equal("Kho A", onlyA[0]["warehouseName"]);

            var onlyB = await repository.GetInventoryMovementSummaryAsync(from, to, null, [warehouseB], code, CancellationToken.None);
            Assert.Single(onlyB);
            Assert.Equal("Kho B", onlyB[0]["warehouseName"]);

            var stockA = await conn.ExecuteScalarAsync<decimal>(
                "SELECT quantity_available FROM inventory_batches WHERE id = @Id",
                new { Id = batchA });
            var stockB = await conn.ExecuteScalarAsync<decimal>(
                "SELECT quantity_available FROM inventory_batches WHERE id = @Id",
                new { Id = batchB });
            Assert.Equal(stockA, Qty(rowA, "closingQty"));
            Assert.Equal(stockB, Qty(rowB, "closingQty"));

            var deleted = await repository.GetInventoryMovementSummaryAsync(from, to, null, null, "DEL" + suffix, CancellationToken.None);
            Assert.Empty(deleted);
        }
        finally
        {
            await CleanupAsync(conn, tenantId, otherTenantId);
        }
    }

    [Fact]
    public async Task Movement_lines_match_summary_cell_and_keep_document_codes_inside_the_tenant()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Fail($"Set {ConnectionEnvironmentVariable} to the isolated database before this test.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (DeniedDatabases.Contains(builder.Database ?? "")
            || (builder.Database ?? "").Contains("prod", StringComparison.OrdinalIgnoreCase))
            Assert.Fail($"Refusing database '{builder.Database}'.");

        var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var warehouseA = Guid.NewGuid();
        var warehouseB = Guid.NewGuid();
        var otherWarehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchA = Guid.NewGuid();
        var code = "XNT" + suffix;
        var grnId = Guid.NewGuid();
        var returnOrderId = Guid.NewGuid();
        var saleOrderId = Guid.NewGuid();
        var leakedOrderId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var transferInId = Guid.NewGuid();
        var transferOutId = Guid.NewGuid();
        var adjustmentUpId = Guid.NewGuid();
        var adjustmentDownId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
        var port = await conn.ExecuteScalarAsync<int>("SELECT inet_server_port()");
        if (!string.Equals(database, "pos_idem_staff_accept", StringComparison.Ordinal) || port != 5433)
            Assert.Fail($"Refusing database '{database}' on port {port}.");

        var forbidden = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int FROM tenants
            WHERE tenant_code IN ('DEMO_PHARMACY', 'NT_XUANHOA', 'KIT_MKT')
            """);
        if (forbidden != 0)
            Assert.Fail("Refusing database that already contains a real tenant code.");

        try
        {
            await SeedLineFixturesAsync(
                conn, tenantId, otherTenantId, branchId, otherBranchId, warehouseA, warehouseB, otherWarehouseId,
                productId, batchA, supplierId, grnId, returnOrderId, saleOrderId, leakedOrderId, returnId,
                transferInId, transferOutId, adjustmentUpId, adjustmentDownId, code);

            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, grnId, 100m, from.AddDays(-10));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, grnId, 20m, from);
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.SalesReturn, returnId, 3m, from.AddDays(1));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.InventoryTransfer, transferInId, 5m, from.AddDays(2));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.InventoryAdjustment, adjustmentUpId, 2m, from.AddDays(3));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.OpeningBalance, Guid.NewGuid(), 7m, from.AddDays(4));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, Guid.NewGuid(), 4m, from.AddDays(5));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, saleOrderId, 8m, from.AddDays(6));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.InventoryTransfer, transferOutId, 4m, from.AddDays(7));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.InventoryAdjustment, adjustmentDownId, 1m, from.AddDays(8));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.ProductMerge, Guid.NewGuid(), 2m, from.AddDays(9));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, "counter_issue", Guid.NewGuid(), 3m, from.AddDays(10));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, leakedOrderId, 1m, from.AddDays(11));
            await MoveRef(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, saleOrderId, 100m, to);
            await MoveRef(conn, tenantId, warehouseB, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.InventoryTransfer, transferInId, 5m, from.AddDays(2));
            await MoveRef(conn, tenantId, warehouseB, batchA, productId, StockMovementTypes.In, StockReferenceTypes.InventoryTransfer, transferOutId, 4m, from.AddDays(7));
            await MoveRef(conn, otherTenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, grnId, 50m, from.AddDays(2));

            var repository = new ReportsRepository(new NpgsqlConnectionFactory(builder.ConnectionString), new FixedTenant(tenantId));
            var rows = await repository.GetInventoryMovementSummaryAsync(from, to, warehouseA, null, code, CancellationToken.None);
            var rowA = One(rows, "Kho A");
            Assert.Equal(100m, Qty(rowA, "openingQty"));
            Assert.Equal(41m, Qty(rowA, "inQty"));
            Assert.Equal(19m, Qty(rowA, "outQty"));
            Assert.Equal(2m, Qty(rowA, "adjustmentInQty"));
            Assert.Equal(1m, Qty(rowA, "adjustmentOutQty"));
            Assert.Equal(122m, Qty(rowA, "closingQty"));
            Assert.Equal(Qty(rowA, "openingQty") + Qty(rowA, "inQty") - Qty(rowA, "outQty"), Qty(rowA, "closingQty"));

            var branch = new AllowOneWarehouse(warehouseA);
            var service = new ReportsService(repository, new UnusedPayables(), branch);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RunInventoryMovementLinesAsync(from, to, Guid.Empty, warehouseA, "in", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RunInventoryMovementLinesAsync(from, to, productId, warehouseA, "sale", CancellationToken.None));
            Assert.Equal(0, branch.Calls);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.RunInventoryMovementLinesAsync(from, to, productId, warehouseB, "out", CancellationToken.None));

            var inbound = await service.RunInventoryMovementLinesAsync(from, to, productId, warehouseA, "in", CancellationToken.None);
            var outbound = await service.RunInventoryMovementLinesAsync(from, to, productId, warehouseA, "out", CancellationToken.None);
            Assert.Equal(41m, Convert.ToDecimal(inbound.Totals!["quantity"]));
            Assert.Equal(19m, Convert.ToDecimal(outbound.Totals!["quantity"]));
            Assert.Equal(Qty(rowA, "inQty"), inbound.Rows.Sum(row => Convert.ToDecimal(row["quantity"])));
            Assert.Equal(Qty(rowA, "outQty"), outbound.Rows.Sum(row => Convert.ToDecimal(row["quantity"])));
            Assert.Equal(
                new[] { "movementAt", "productName", "movementKind", "referenceCode", "warehouseName", "quantity" },
                inbound.Columns.Select(column => column.Key).ToArray());
            Assert.All(inbound.Rows, row => Assert.Equal("Kho A", row["warehouseName"]));
            Assert.All(outbound.Rows, row => Assert.Equal("Kho A", row["warehouseName"]));
            Assert.Contains(inbound.Rows, row => row["movementAt"]?.ToString() == "01/03/2026 07:00" && row["referenceCode"]?.ToString() == "GRN-1");
            Assert.DoesNotContain(outbound.Rows, row => Convert.ToDecimal(row["quantity"]) == 100m);
            Assert.DoesNotContain(inbound.Rows.Concat(outbound.Rows), row => row["referenceCode"]?.ToString() == "SO-LEAK");
            Assert.Contains(inbound.Rows, row => row["movementKind"]?.ToString() == "Nhập mua hàng" && row["referenceCode"]?.ToString() == "GRN-1");
            Assert.Contains(inbound.Rows, row => row["movementKind"]?.ToString() == "Nhập mua hàng" && row["referenceCode"] is null && Convert.ToDecimal(row["quantity"]) == 4m);
            Assert.Contains(inbound.Rows, row => row["movementKind"]?.ToString() == "Hàng trả" && row["referenceCode"]?.ToString() == "RT-1");
            Assert.Contains(inbound.Rows, row => row["movementKind"]?.ToString() == "Chuyển kho đến" && row["referenceCode"]?.ToString() == "TR-IN");
            Assert.Contains(inbound.Rows, row => row["movementKind"]?.ToString() == "Kiểm kê tăng" && row["referenceCode"]?.ToString() == "ADJ-UP");
            Assert.Contains(inbound.Rows, row => row["movementKind"]?.ToString() == "Tồn đầu kỳ" && row["referenceCode"] is null);
            Assert.Contains(outbound.Rows, row => row["movementKind"]?.ToString() == "Bán hàng" && row["referenceCode"]?.ToString() == "SO-1");
            Assert.Contains(outbound.Rows, row => row["movementKind"]?.ToString() == "Bán hàng" && row["referenceCode"] is null && Convert.ToDecimal(row["quantity"]) == 1m);
            Assert.Contains(outbound.Rows, row => row["movementKind"]?.ToString() == "Chuyển kho đi" && row["referenceCode"]?.ToString() == "TR-OUT");
            Assert.Contains(outbound.Rows, row => row["movementKind"]?.ToString() == "Kiểm kê giảm" && row["referenceCode"]?.ToString() == "ADJ-DOWN");
            Assert.Contains(outbound.Rows, row => row["movementKind"]?.ToString() == "Gộp sản phẩm — xuất" && row["referenceCode"] is null);
            Assert.Contains(outbound.Rows, row => row["movementKind"]?.ToString() == "Xuất khác" && row["referenceCode"] is null);
            Assert.DoesNotContain(outbound.Rows, row => row["movementKind"]?.ToString() == "Bán hàng" && Convert.ToDecimal(row["quantity"]) == 3m);

            var otherWarehouse = await repository.GetInventoryMovementLinesAsync(
                from, to, productId, warehouseB, StockMovementTypes.Out, CancellationToken.None);
            var otherLine = Assert.Single(otherWarehouse);
            Assert.Equal("Chuyển kho đi", otherLine["movementKind"]);
            Assert.Equal("TR-IN", otherLine["referenceCode"]);
            Assert.Equal("Kho B", otherLine["warehouseName"]);
        }
        finally
        {
            await CleanupAsync(conn, tenantId, otherTenantId);
        }
    }

    private static Dictionary<string, object?> One(IReadOnlyList<Dictionary<string, object?>> rows, string warehouse) =>
        Assert.Single(rows, row => string.Equals(row["warehouseName"]?.ToString(), warehouse, StringComparison.Ordinal));

    private static decimal Qty(Dictionary<string, object?> row, string key) =>
        Convert.ToDecimal(row[key]);

    private static async Task SeedAsync(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid otherTenantId,
        Guid branchId,
        Guid warehouseA,
        Guid warehouseB,
        Guid productId,
        Guid deletedProductId,
        Guid batchA,
        Guid batchB,
        Guid deletedBatch,
        string code,
        DateTime from,
        DateTime to)
    {
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @Code, @Code, 1), (@OtherTenantId, @OtherCode, @OtherCode, 1);
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @Code, @Code, 1);
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES
                (@WarehouseA, @TenantId, @BranchId, @CodeA, 'Kho A', 1, 1),
                (@WarehouseB, @TenantId, @BranchId, @CodeB, 'Kho B', 1, 1);
            INSERT INTO products (id, tenant_id, product_code, product_name, status)
            VALUES
                (@ProductId, @TenantId, @Code, @Code, 1),
                (@DeletedProductId, @TenantId, @DeletedCode, @DeletedCode, 1);
            UPDATE products SET deleted_at = NOW() WHERE id = @DeletedProductId;
            INSERT INTO inventory_batches (
                id, tenant_id, warehouse_id, product_id, batch_number,
                unit_cost, quantity_received, quantity_available, status)
            VALUES
                (@BatchA, @TenantId, @WarehouseA, @ProductId, 'A', 100, 113, 113, 1),
                (@BatchB, @TenantId, @WarehouseB, @ProductId, 'B', 100, 19, 19, 1),
                (@DeletedBatch, @TenantId, @WarehouseA, @DeletedProductId, 'D', 100, 1, 1, 1);
            """, new
        {
            TenantId = tenantId,
            OtherTenantId = otherTenantId,
            OtherCode = code + "-OTHER",
            BranchId = branchId,
            WarehouseA = warehouseA,
            WarehouseB = warehouseB,
            Code = code,
            CodeA = code + "A",
            CodeB = code + "B",
            ProductId = productId,
            DeletedProductId = deletedProductId,
            DeletedCode = "DEL" + code[3..],
            BatchA = batchA,
            BatchB = batchB,
            DeletedBatch = deletedBatch,
        });

        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, 100m, from.AddDays(-28));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, 10m, from.AddDays(-14));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, 1m, from);
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, 20m, from.AddDays(9));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.SalesReturn, 3m, from.AddDays(10));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.InventoryTransfer, 5m, from.AddDays(11));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.InventoryAdjustment, 2m, from.AddDays(12));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, 8m, from.AddDays(13));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.InventoryTransfer, 4m, from.AddDays(14));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.InventoryAdjustment, 1m, from.AddDays(15));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.OpeningBalance, 7m, from.AddDays(16));
        await Move(conn, tenantId, warehouseA, batchA, productId, StockMovementTypes.Out, StockReferenceTypes.SalesOrder, 100m, to);

        await Move(conn, tenantId, warehouseB, batchB, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, 20m, from.AddDays(-10));
        await Move(conn, tenantId, warehouseB, batchB, productId, StockMovementTypes.Out, StockReferenceTypes.InventoryTransfer, 5m, from.AddDays(11));
        await Move(conn, tenantId, warehouseB, batchB, productId, StockMovementTypes.In, StockReferenceTypes.InventoryTransfer, 4m, from.AddDays(14));

        await Move(conn, tenantId, warehouseA, deletedBatch, deletedProductId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, 9m, from.AddDays(1));
        await Move(conn, otherTenantId, warehouseA, batchA, productId, StockMovementTypes.In, StockReferenceTypes.GoodsReceipt, 50m, from.AddDays(2));
    }

    private static Task Move(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid warehouseId,
        Guid batchId,
        Guid productId,
        short movementType,
        string referenceType,
        decimal quantity,
        DateTime movementDate) =>
        conn.ExecuteAsync("""
            INSERT INTO stock_movements (
                tenant_id, warehouse_id, batch_id, product_id,
                movement_type, reference_type, reference_id, quantity, movement_date)
            VALUES (
                @TenantId, @WarehouseId, @BatchId, @ProductId,
                @MovementType, @ReferenceType, @ReferenceId, @Quantity, @MovementDate)
            """, new
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            ProductId = productId,
            MovementType = movementType,
            ReferenceType = referenceType,
            ReferenceId = Guid.NewGuid(),
            Quantity = quantity,
            MovementDate = movementDate,
        });

    private static Task MoveRef(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid warehouseId,
        Guid batchId,
        Guid productId,
        short movementType,
        string referenceType,
        Guid referenceId,
        decimal quantity,
        DateTime movementDate) =>
        conn.ExecuteAsync("""
            INSERT INTO stock_movements (
                tenant_id, warehouse_id, batch_id, product_id,
                movement_type, reference_type, reference_id, quantity, movement_date)
            VALUES (
                @TenantId, @WarehouseId, @BatchId, @ProductId,
                @MovementType, @ReferenceType, @ReferenceId, @Quantity, @MovementDate)
            """, new
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            ProductId = productId,
            MovementType = movementType,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Quantity = quantity,
            MovementDate = movementDate,
        });

    private static Task SeedLineFixturesAsync(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid otherTenantId,
        Guid branchId,
        Guid otherBranchId,
        Guid warehouseA,
        Guid warehouseB,
        Guid otherWarehouseId,
        Guid productId,
        Guid batchA,
        Guid supplierId,
        Guid grnId,
        Guid returnOrderId,
        Guid saleOrderId,
        Guid leakedOrderId,
        Guid returnId,
        Guid transferInId,
        Guid transferOutId,
        Guid adjustmentUpId,
        Guid adjustmentDownId,
        string code) =>
        conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @Code, @Code, 1), (@OtherTenantId, @OtherCode, @OtherCode, 1);
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES
                (@BranchId, @TenantId, @Code, @Code, 1),
                (@OtherBranchId, @OtherTenantId, @OtherCode, @OtherCode, 1);
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES
                (@WarehouseA, @TenantId, @BranchId, @CodeA, 'Kho A', 1, 1),
                (@WarehouseB, @TenantId, @BranchId, @CodeB, 'Kho B', 1, 1),
                (@OtherWarehouseId, @OtherTenantId, @OtherBranchId, @OtherCode, 'Kho ngoài', 1, 1);
            INSERT INTO products (id, tenant_id, product_code, product_name, status)
            VALUES (@ProductId, @TenantId, @Code, @Code, 1);
            INSERT INTO inventory_batches (
                id, tenant_id, warehouse_id, product_id, batch_number,
                unit_cost, quantity_received, quantity_available, status)
            VALUES (@BatchA, @TenantId, @WarehouseA, @ProductId, 'A', 100, 122, 122, 1);
            INSERT INTO suppliers (id, tenant_id, supplier_code, supplier_name)
            VALUES (@SupplierId, @TenantId, @Code, @Code);
            INSERT INTO goods_receipts (id, tenant_id, grn_number, supplier_id, warehouse_id)
            VALUES (@GrnId, @TenantId, 'GRN-1', @SupplierId, @WarehouseA);
            INSERT INTO sales_orders (id, tenant_id, order_number, branch_id, warehouse_id)
            VALUES
                (@ReturnOrderId, @TenantId, 'SO-RET', @BranchId, @WarehouseA),
                (@SaleOrderId, @TenantId, 'SO-1', @BranchId, @WarehouseA),
                (@LeakedOrderId, @OtherTenantId, 'SO-LEAK', @OtherBranchId, @OtherWarehouseId);
            INSERT INTO sales_returns (id, tenant_id, return_number, sales_order_id)
            VALUES (@ReturnId, @TenantId, 'RT-1', @ReturnOrderId);
            INSERT INTO inventory_transfers (id, tenant_id, transfer_number, from_warehouse_id, to_warehouse_id)
            VALUES
                (@TransferInId, @TenantId, 'TR-IN', @WarehouseB, @WarehouseA),
                (@TransferOutId, @TenantId, 'TR-OUT', @WarehouseA, @WarehouseB);
            INSERT INTO inventory_adjustments (id, tenant_id, warehouse_id, adjustment_number)
            VALUES
                (@AdjustmentUpId, @TenantId, @WarehouseA, 'ADJ-UP'),
                (@AdjustmentDownId, @TenantId, @WarehouseA, 'ADJ-DOWN');
            """, new
        {
            TenantId = tenantId,
            OtherTenantId = otherTenantId,
            OtherCode = code + "-OTHER",
            BranchId = branchId,
            OtherBranchId = otherBranchId,
            WarehouseA = warehouseA,
            WarehouseB = warehouseB,
            OtherWarehouseId = otherWarehouseId,
            Code = code,
            CodeA = code + "A",
            CodeB = code + "B",
            ProductId = productId,
            BatchA = batchA,
            SupplierId = supplierId,
            GrnId = grnId,
            ReturnOrderId = returnOrderId,
            SaleOrderId = saleOrderId,
            LeakedOrderId = leakedOrderId,
            ReturnId = returnId,
            TransferInId = transferInId,
            TransferOutId = transferOutId,
            AdjustmentUpId = adjustmentUpId,
            AdjustmentDownId = adjustmentDownId,
        });

    private static async Task CleanupAsync(NpgsqlConnection conn, Guid tenantId, Guid otherTenantId)
    {
        await conn.ExecuteAsync("""
            DELETE FROM stock_movements WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM sales_returns WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM sales_orders WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM goods_receipts WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM inventory_transfers WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM inventory_adjustments WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM suppliers WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM inventory_batches WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM products WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM warehouses WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM branches WHERE tenant_id IN (@TenantId, @OtherTenantId);
            DELETE FROM tenants WHERE id IN (@TenantId, @OtherTenantId);
            """, new { TenantId = tenantId, OtherTenantId = otherTenantId });
    }

    private sealed class AllowOneWarehouse(Guid allowed) : IBranchAccessService
    {
        public int Calls { get; private set; }

        public Task<(Guid? WarehouseId, Guid[]? AllowedWarehouseIds)> ResolveWarehouseQueryAsync(
            Guid? requestedWarehouseId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (requestedWarehouseId != allowed)
                throw new UnauthorizedAccessException("Bạn không có quyền xem dữ liệu kho này.");
            return Task.FromResult<(Guid? WarehouseId, Guid[]? AllowedWarehouseIds)>((requestedWarehouseId, null));
        }

        public Task<BranchAccessScope> GetScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(BranchAccessScope.EmptyScope());

        public Task EnsureWarehouseAccessAsync(Guid warehouseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task EnsureBranchAccessAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class UnusedPayables : ISupplierPayablesService
    {
        public Task<IReadOnlyList<SupplierPayablesRowDto>> GetSummaryAsync(
            Guid? warehouseId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SupplierPayablesDetailDto?> GetDetailAsync(
            Guid supplierId,
            Guid? warehouseId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
