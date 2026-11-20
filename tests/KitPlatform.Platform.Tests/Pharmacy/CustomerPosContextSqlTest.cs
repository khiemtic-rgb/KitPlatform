using Xunit;
using Npgsql;
using Dapper;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// SQL validation test for GetRecentOrdersWithProductsAsync query.
/// Requires PostgreSQL test database with schema.
/// Run with: dotnet test --filter "FullyQualifiedName~CustomerPosContextSqlTest"
/// Skip if database unavailable.
/// </summary>
public class CustomerPosContextSqlTest
{
    private const string TestConnectionString = "Host=localhost;Port=5432;Database=kitplatform_test;Username=kitplatform;Password=kitplatform_dev_2026";

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(TestConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task GetRecentOrdersWithProducts_ValidatesQuery()
    {
        // ARRANGE: Test data setup
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var unitId1 = Guid.NewGuid();
        var unitId2 = Guid.NewGuid();
        var batchId1 = Guid.NewGuid();
        var batchId2 = Guid.NewGuid();

        if (!await CanConnectAsync())
        {
            Assert.Fail("Test database not available at: " + TestConnectionString);
            return;
        }

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        // Setup test data with required foreign keys
        var tenantCode = $"TEST-{tenantId.ToString().Substring(0, 8)}";
        await conn.ExecuteAsync("""
            -- Create tenant
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, 'Test Tenant', 1);
            
            -- Create branch
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @TenantCode, 'Test Branch', 1);
            
            -- Create warehouse
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseId, @TenantId, @BranchId, @TenantCode, 'Test Warehouse', 1, 1);
            
            -- Create customer
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
            VALUES (@CustomerId, @TenantId, @TenantCode, 'Test Customer', @TenantCode, 1);
            
            -- Create test products
            INSERT INTO products (id, tenant_id, product_code, product_name, status)
            VALUES 
                (@ProductId1, @TenantId, @TenantCode, 'Test Product 1', 1),
                (@ProductId2, @TenantId, CONCAT(@TenantCode, '-2'), 'Test Product 2', 1);
            
            -- Create product units
            INSERT INTO product_units (id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit, status)
            VALUES 
                (@UnitId1, @TenantId, @ProductId1, 'Unit', 1, TRUE, TRUE, 1),
                (@UnitId2, @TenantId, @ProductId2, 'Unit', 1, TRUE, TRUE, 1);
            
            -- Create inventory batches
            INSERT INTO inventory_batches (id, tenant_id, warehouse_id, product_id, batch_number, unit_cost, quantity_received, quantity_available, status)
            VALUES 
                (@BatchId1, @TenantId, @WarehouseId, @ProductId1, 'BATCH-001', 40.00, 100, 100, 1),
                (@BatchId2, @TenantId, @WarehouseId, @ProductId2, 'BATCH-002', 40.00, 100, 100, 1);
            """,
            new { TenantId = tenantId, TenantCode = tenantCode, BranchId = branchId, WarehouseId = warehouseId, CustomerId = customerId, ProductId1 = productId1, ProductId2 = productId2, UnitId1 = unitId1, UnitId2 = unitId2, BatchId1 = batchId1, BatchId2 = batchId2 });

        // Create test order with products
        var orderId = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO sales_orders (
                id, tenant_id, branch_id, order_number, customer_id, warehouse_id, 
                order_date, status, total_amount, created_at
            )
            VALUES (
                @OrderId, @TenantId, @BranchId, 'SO-TEST-001', @CustomerId, @WarehouseId,
                NOW() - INTERVAL '10 days', 2, 150.00, NOW()
            );
            
            INSERT INTO sales_order_items (sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total)
            VALUES 
                (@OrderId, @ProductId1, @UnitId1, @BatchId1, 2, 50.00, 100.00),
                (@OrderId, @ProductId2, @UnitId2, @BatchId2, 1, 50.00, 50.00);
            """,
            new { OrderId = orderId, TenantId = tenantId, BranchId = branchId, CustomerId = customerId, WarehouseId = warehouseId, ProductId1 = productId1, ProductId2 = productId2, UnitId1 = unitId1, UnitId2 = unitId2, BatchId1 = batchId1, BatchId2 = batchId2 });

        // ACT: Execute the actual query from GetRecentOrdersWithProductsAsync
        const string sql = """
            WITH order_products AS (
                SELECT
                    so.id AS order_id,
                    so.order_date,
                    so.order_number,
                    so.total_amount,
                    (SELECT COUNT(*)::int FROM sales_order_items WHERE sales_order_id = so.id) AS item_count,
                    soi.product_id,
                    p.product_name,
                    soi.quantity,
                    soi.line_total,
                    ROW_NUMBER() OVER (PARTITION BY so.id ORDER BY soi.line_total DESC) AS rn
                FROM sales_orders so
                LEFT JOIN sales_order_items soi ON soi.sales_order_id = so.id
                LEFT JOIN products p ON p.id = soi.product_id
                WHERE so.tenant_id = @TenantId
                  AND so.customer_id = @CustomerId
                  AND so.status = 2
                  AND so.order_date >= NOW() - INTERVAL '90 days'
            )
            SELECT
                order_id AS OrderId,
                order_date AS OrderDate,
                order_number AS OrderNumber,
                total_amount AS TotalAmount,
                item_count AS ItemCount,
                jsonb_agg(
                    jsonb_build_object('productName', product_name, 'quantity', quantity)
                    ORDER BY line_total DESC
                ) FILTER (WHERE product_name IS NOT NULL AND rn <= @TopProductsPerOrder) AS TopProductsJson
            FROM order_products
            GROUP BY order_id, order_date, order_number, total_amount, item_count
            ORDER BY order_date DESC
            LIMIT @Limit
            """;

        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId, Limit = 5, TopProductsPerOrder = 5 });

        // ASSERT
        var resultsList = results.ToList();
        Assert.Single(resultsList);

        var order = resultsList[0];
        Assert.Equal(orderId, (Guid)order.orderid);
        Assert.Equal("SO-TEST-001", (string)order.ordernumber);
        Assert.Equal(150.00m, (decimal)order.totalamount);
        Assert.Equal(2, (int)order.itemcount);
        Assert.NotNull(order.topproductsjson);

        // Cleanup in correct foreign key order
        await conn.ExecuteAsync("""
            DELETE FROM sales_order_items WHERE sales_order_id = @OrderId;
            DELETE FROM sales_orders WHERE id = @OrderId;
            DELETE FROM customers WHERE id = @CustomerId;
            DELETE FROM inventory_batches WHERE id IN (@BatchId1, @BatchId2);
            DELETE FROM product_units WHERE id IN (@UnitId1, @UnitId2);
            DELETE FROM products WHERE id IN (@ProductId1, @ProductId2);
            DELETE FROM warehouses WHERE id = @WarehouseId;
            DELETE FROM branches WHERE id = @BranchId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            new { OrderId = orderId, CustomerId = customerId, BatchId1 = batchId1, BatchId2 = batchId2, UnitId1 = unitId1, UnitId2 = unitId2, ProductId1 = productId1, ProductId2 = productId2, WarehouseId = warehouseId, BranchId = branchId, TenantId = tenantId });
    }

    [Fact]
    public async Task GetRecentOrdersWithProducts_HandlesEmptyOrders()
    {
        // Test that orders with no items return empty product list
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        if (!await CanConnectAsync())
        {
            Assert.Fail("Test database not available");
            return;
        }

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        // Setup minimal test data
        var tenantCode = $"TEST-{tenantId.ToString().Substring(0, 8)}";
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, 'Test Tenant', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @TenantCode, 'Test Branch', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseId, @TenantId, @BranchId, @TenantCode, 'Test Warehouse', 1, 1);
            
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
            VALUES (@CustomerId, @TenantId, @TenantCode, 'Test Customer', @TenantCode, 1);
            """,
            new { TenantId = tenantId, TenantCode = tenantCode, BranchId = branchId, WarehouseId = warehouseId, CustomerId = customerId });

        // Create order with NO items
        var orderId = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO sales_orders (
                id, tenant_id, branch_id, order_number, customer_id, warehouse_id,
                order_date, status, total_amount, created_at
            )
            VALUES (
                @OrderId, @TenantId, @BranchId, 'SO-EMPTY-001', @CustomerId, @WarehouseId,
                NOW(), 2, 0, NOW()
            );
            """,
            new { OrderId = orderId, TenantId = tenantId, BranchId = branchId, CustomerId = customerId, WarehouseId = warehouseId });

        // Execute query
        const string sql = """
            WITH order_products AS (
                SELECT
                    so.id AS order_id,
                    so.order_date,
                    so.order_number,
                    so.total_amount,
                    (SELECT COUNT(*)::int FROM sales_order_items WHERE sales_order_id = so.id) AS item_count,
                    soi.product_id,
                    p.product_name,
                    soi.quantity,
                    soi.line_total,
                    ROW_NUMBER() OVER (PARTITION BY so.id ORDER BY soi.line_total DESC) AS rn
                FROM sales_orders so
                LEFT JOIN sales_order_items soi ON soi.sales_order_id = so.id
                LEFT JOIN products p ON p.id = soi.product_id
                WHERE so.tenant_id = @TenantId
                  AND so.customer_id = @CustomerId
                  AND so.status = 2
                  AND so.order_date >= NOW() - INTERVAL '90 days'
            )
            SELECT
                order_id AS OrderId,
                order_date AS OrderDate,
                order_number AS OrderNumber,
                total_amount AS TotalAmount,
                item_count AS ItemCount,
                jsonb_agg(
                    jsonb_build_object('productName', product_name, 'quantity', quantity)
                    ORDER BY line_total DESC
                ) FILTER (WHERE product_name IS NOT NULL AND rn <= @TopProductsPerOrder) AS TopProductsJson
            FROM order_products
            GROUP BY order_id, order_date, order_number, total_amount, item_count
            ORDER BY order_date DESC
            LIMIT @Limit
            """;

        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId, Limit = 5, TopProductsPerOrder = 5 });

        // ASSERT: Should return order with 0 items and NULL or empty products JSON
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.Equal(0, (int)resultsList[0].itemcount);

        // Cleanup
        await conn.ExecuteAsync("""
            DELETE FROM sales_orders WHERE id = @OrderId;
            DELETE FROM customers WHERE id = @CustomerId;
            DELETE FROM warehouses WHERE id = @WarehouseId;
            DELETE FROM branches WHERE id = @BranchId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            new { OrderId = orderId, CustomerId = customerId, WarehouseId = warehouseId, BranchId = branchId, TenantId = tenantId });
    }

    [Fact]
    public async Task GetRecentOrdersWithProducts_RespectsTopProductsLimit()
    {
        // Test that TopProductsPerOrder parameter limits products per order
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productIds = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToList();
        var unitIds = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToList();
        var batchIds = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToList();

        if (!await CanConnectAsync())
        {
            Assert.Fail("Test database not available");
            return;
        }

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        // Setup tenant, branch, warehouse, customer
        var tenantCode = $"TEST-{tenantId.ToString().Substring(0, 8)}";
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, 'Test Tenant', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @TenantCode, 'Test Branch', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseId, @TenantId, @BranchId, @TenantCode, 'Test Warehouse', 1, 1);
            
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
            VALUES (@CustomerId, @TenantId, @TenantCode, 'Test Customer', @TenantCode, 1);
            """,
            new { TenantId = tenantId, TenantCode = tenantCode, BranchId = branchId, WarehouseId = warehouseId, CustomerId = customerId });

        // Create 10 products with units and batches
        for (int index = 0; index < 10; index++)
        {
            var productId = productIds[index];
            var unitId = unitIds[index];
            var batchId = batchIds[index];
            
            await conn.ExecuteAsync("""
                INSERT INTO products (id, tenant_id, product_code, product_name, status)
                VALUES (@ProductId, @TenantId, @Code, @Name, 1);
                
                INSERT INTO product_units (id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit, status)
                VALUES (@UnitId, @TenantId, @ProductId, 'Unit', 1, TRUE, TRUE, 1);
                
                INSERT INTO inventory_batches (id, tenant_id, warehouse_id, product_id, batch_number, unit_cost, quantity_received, quantity_available, status)
                VALUES (@BatchId, @TenantId, @WarehouseId, @ProductId, @BatchNum, 40.00, 100, 100, 1);
                """,
                new { ProductId = productId, UnitId = unitId, BatchId = batchId, TenantId = tenantId, WarehouseId = warehouseId, Code = $"{tenantCode}-{index}", Name = $"Product {index}", BatchNum = $"{tenantCode}-{index}" });
        }

        // Create order with 10 products
        var orderId = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO sales_orders (
                id, tenant_id, branch_id, order_number, customer_id, warehouse_id,
                order_date, status, total_amount, created_at
            )
            VALUES (
                @OrderId, @TenantId, @BranchId, 'SO-LIMIT-001', @CustomerId, @WarehouseId,
                NOW(), 2, 500.00, NOW()
            );
            """,
            new { OrderId = orderId, TenantId = tenantId, BranchId = branchId, CustomerId = customerId, WarehouseId = warehouseId });

        for (int index = 0; index < 10; index++)
        {
            var productId = productIds[index];
            var unitId = unitIds[index];
            var batchId = batchIds[index];
            
            await conn.ExecuteAsync("""
                INSERT INTO sales_order_items (sales_order_id, product_id, product_unit_id, batch_id, quantity, unit_price, line_total)
                VALUES (@OrderId, @ProductId, @UnitId, @BatchId, 1, @Price, @Price)
                """,
                new { OrderId = orderId, ProductId = productId, UnitId = unitId, BatchId = batchId, Price = (10 - index) * 10m }); // Decreasing prices
        }

        // Execute query with TopProductsPerOrder = 3
        const string sql = """
            WITH order_products AS (
                SELECT
                    so.id AS order_id,
                    so.order_date,
                    so.order_number,
                    so.total_amount,
                    (SELECT COUNT(*)::int FROM sales_order_items WHERE sales_order_id = so.id) AS item_count,
                    soi.product_id,
                    p.product_name,
                    soi.quantity,
                    soi.line_total,
                    ROW_NUMBER() OVER (PARTITION BY so.id ORDER BY soi.line_total DESC) AS rn
                FROM sales_orders so
                LEFT JOIN sales_order_items soi ON soi.sales_order_id = so.id
                LEFT JOIN products p ON p.id = soi.product_id
                WHERE so.tenant_id = @TenantId
                  AND so.customer_id = @CustomerId
                  AND so.status = 2
                  AND so.order_date >= NOW() - INTERVAL '90 days'
            )
            SELECT
                order_id AS OrderId,
                order_date AS OrderDate,
                order_number AS OrderNumber,
                total_amount AS TotalAmount,
                item_count AS ItemCount,
                jsonb_agg(
                    jsonb_build_object('productName', product_name, 'quantity', quantity)
                    ORDER BY line_total DESC
                ) FILTER (WHERE product_name IS NOT NULL AND rn <= @TopProductsPerOrder) AS TopProductsJson
            FROM order_products
            GROUP BY order_id, order_date, order_number, total_amount, item_count
            ORDER BY order_date DESC
            LIMIT @Limit
            """;

        var results = await conn.QueryAsync<dynamic>(
            sql,
            new { TenantId = tenantId, CustomerId = customerId, Limit = 5, TopProductsPerOrder = 3 });

        // ASSERT: Should return 10 item_count but only 3 products in JSON
        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.Equal(10, (int)resultsList[0].itemcount);

        var jsonStr = (string)resultsList[0].topproductsjson;
        var products = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(jsonStr);
        Assert.Equal(3, products!.Count); // Should limit to top 3

        // Cleanup in correct foreign key order
        await conn.ExecuteAsync("""
            DELETE FROM sales_order_items WHERE sales_order_id = @OrderId;
            DELETE FROM sales_orders WHERE id = @OrderId;
            """,
            new { OrderId = orderId });

        for (int i = 0; i < 10; i++)
        {
            await conn.ExecuteAsync("""
                DELETE FROM inventory_batches WHERE id = @BatchId;
                DELETE FROM product_units WHERE id = @UnitId;
                DELETE FROM products WHERE id = @ProductId;
                """,
                new { ProductId = productIds[i], UnitId = unitIds[i], BatchId = batchIds[i] });
        }

        await conn.ExecuteAsync("""
            DELETE FROM customers WHERE id = @CustomerId;
            DELETE FROM warehouses WHERE id = @WarehouseId;
            DELETE FROM branches WHERE id = @BranchId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            new { CustomerId = customerId, WarehouseId = warehouseId, BranchId = branchId, TenantId = tenantId });
    }
}
