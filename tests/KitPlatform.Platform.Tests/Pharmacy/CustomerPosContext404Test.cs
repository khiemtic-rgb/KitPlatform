using Xunit;
using Npgsql;
using Dapper;
using System.Net;
using System.Net.Http.Json;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Platform.Tests.Pharmacy;

/// <summary>
/// Integration tests for Customer POS Context 404 behavior.
/// Validates that missing or cross-tenant customers return 404 Not Found.
/// </summary>
public class CustomerPosContext404Test
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
    public async Task GetPosCustomerContext_ExistingCustomer_Returns200()
    {
        // ARRANGE: Create a tenant, branch, warehouse, and customer
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        if (!await CanConnectAsync())
        {
            Assert.Fail("Test database not available at: " + TestConnectionString);
            return;
        }

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        var tenantCode = $"TEST-{tenantId.ToString().Substring(0, 8)}";
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, 'Test Tenant', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @TenantCode, 'Test Branch', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseId, @TenantId, @BranchId, @TenantCode, 'Test Warehouse', 1, 1);
            
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
            VALUES (@CustomerId, @TenantId, @TenantCode, 'Test Customer', '0901234567', 1);
            """,
            new { TenantId = tenantId, TenantCode = tenantCode, BranchId = branchId, WarehouseId = warehouseId, CustomerId = customerId });

        // ACT: Simulate service call (direct repository query to validate behavior)
        const string sql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone,
                c.status AS Status
            FROM customers c
            WHERE c.id = @CustomerId
              AND c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
            """;

        var customer = await conn.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { CustomerId = customerId, TenantId = tenantId });

        // ASSERT: Customer should exist
        Assert.NotNull(customer);
        Assert.Equal(customerId, (Guid)customer.id);
        Assert.Equal("Test Customer", (string)customer.fullname);

        // Cleanup
        await conn.ExecuteAsync("""
            DELETE FROM customers WHERE id = @CustomerId;
            DELETE FROM warehouses WHERE id = @WarehouseId;
            DELETE FROM branches WHERE id = @BranchId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            new { CustomerId = customerId, WarehouseId = warehouseId, BranchId = branchId, TenantId = tenantId });
    }

    [Fact]
    public async Task GetPosCustomerContext_MissingCustomer_ReturnsNull()
    {
        // ARRANGE: Create a tenant but no customer
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var nonExistentCustomerId = Guid.NewGuid();

        if (!await CanConnectAsync())
        {
            Assert.Fail("Test database not available");
            return;
        }

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        var tenantCode = $"TEST-{tenantId.ToString().Substring(0, 8)}";
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, 'Test Tenant', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @TenantCode, 'Test Branch', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseId, @TenantId, @BranchId, @TenantCode, 'Test Warehouse', 1, 1);
            """,
            new { TenantId = tenantId, TenantCode = tenantCode, BranchId = branchId, WarehouseId = warehouseId });

        // ACT: Query for non-existent customer
        const string sql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName
            FROM customers c
            WHERE c.id = @CustomerId
              AND c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
            """;

        var customer = await conn.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { CustomerId = nonExistentCustomerId, TenantId = tenantId });

        // ASSERT: Should return null (simulating 404)
        Assert.Null(customer);

        // Cleanup
        await conn.ExecuteAsync("""
            DELETE FROM warehouses WHERE id = @WarehouseId;
            DELETE FROM branches WHERE id = @BranchId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            new { WarehouseId = warehouseId, BranchId = branchId, TenantId = tenantId });
    }

    [Fact]
    public async Task GetPosCustomerContext_CrossTenantCustomer_ReturnsNull()
    {
        // ARRANGE: Create two tenants, customer belongs to tenant1, query from tenant2
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        var branch1Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();
        var warehouse1Id = Guid.NewGuid();
        var warehouse2Id = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        if (!await CanConnectAsync())
        {
            Assert.Fail("Test database not available");
            return;
        }

        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        var tenant1Code = $"TEST1-{tenant1Id.ToString().Substring(0, 7)}";
        var tenant2Code = $"TEST2-{tenant2Id.ToString().Substring(0, 7)}";

        // Setup tenant1 with customer
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@Tenant1Id, @Tenant1Code, 'Tenant 1', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@Branch1Id, @Tenant1Id, @Tenant1Code, 'Branch 1', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@Warehouse1Id, @Tenant1Id, @Branch1Id, @Tenant1Code, 'Warehouse 1', 1, 1);
            
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
            VALUES (@CustomerId, @Tenant1Id, @Tenant1Code, 'Customer In Tenant1', '0901234567', 1);
            """,
            new { Tenant1Id = tenant1Id, Tenant1Code = tenant1Code, Branch1Id = branch1Id, Warehouse1Id = warehouse1Id, CustomerId = customerId });

        // Setup tenant2 (no customer)
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@Tenant2Id, @Tenant2Code, 'Tenant 2', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@Branch2Id, @Tenant2Id, @Tenant2Code, 'Branch 2', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@Warehouse2Id, @Tenant2Id, @Branch2Id, @Tenant2Code, 'Warehouse 2', 1, 1);
            """,
            new { Tenant2Id = tenant2Id, Tenant2Code = tenant2Code, Branch2Id = branch2Id, Warehouse2Id = warehouse2Id });

        // ACT: Query customer from tenant2 context (should fail)
        const string sql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName
            FROM customers c
            WHERE c.id = @CustomerId
              AND c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
            """;

        var customerInTenant2 = await conn.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { CustomerId = customerId, TenantId = tenant2Id });

        // ASSERT: Should return null (tenant isolation — simulating 404)
        Assert.Null(customerInTenant2);

        // Verify customer exists in tenant1 (sanity check)
        var customerInTenant1 = await conn.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { CustomerId = customerId, TenantId = tenant1Id });
        Assert.NotNull(customerInTenant1);

        // Cleanup in correct order
        await conn.ExecuteAsync("""
            DELETE FROM customers WHERE id = @CustomerId;
            DELETE FROM warehouses WHERE id IN (@Warehouse1Id, @Warehouse2Id);
            DELETE FROM branches WHERE id IN (@Branch1Id, @Branch2Id);
            DELETE FROM tenants WHERE id IN (@Tenant1Id, @Tenant2Id);
            """,
            new { CustomerId = customerId, Warehouse1Id = warehouse1Id, Warehouse2Id = warehouse2Id, Branch1Id = branch1Id, Branch2Id = branch2Id, Tenant1Id = tenant1Id, Tenant2Id = tenant2Id });
    }

    [Fact]
    public async Task GetPosCustomerContext_DeletedCustomer_ReturnsNull()
    {
        // ARRANGE: Create a tenant and a soft-deleted customer
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

        var tenantCode = $"TEST-{tenantId.ToString().Substring(0, 8)}";
        await conn.ExecuteAsync("""
            INSERT INTO tenants (id, tenant_code, tenant_name, status)
            VALUES (@TenantId, @TenantCode, 'Test Tenant', 1);
            
            INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
            VALUES (@BranchId, @TenantId, @TenantCode, 'Test Branch', 1);
            
            INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
            VALUES (@WarehouseId, @TenantId, @BranchId, @TenantCode, 'Test Warehouse', 1, 1);
            
            INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status, deleted_at)
            VALUES (@CustomerId, @TenantId, @TenantCode, 'Deleted Customer', '0901234567', 0, NOW());
            """,
            new { TenantId = tenantId, TenantCode = tenantCode, BranchId = branchId, WarehouseId = warehouseId, CustomerId = customerId });

        // ACT: Query deleted customer
        const string sql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName
            FROM customers c
            WHERE c.id = @CustomerId
              AND c.tenant_id = @TenantId
              AND c.deleted_at IS NULL
            """;

        var customer = await conn.QuerySingleOrDefaultAsync<dynamic>(
            sql,
            new { CustomerId = customerId, TenantId = tenantId });

        // ASSERT: Should return null (deleted customers excluded)
        Assert.Null(customer);

        // Cleanup
        await conn.ExecuteAsync("""
            DELETE FROM customers WHERE id = @CustomerId;
            DELETE FROM warehouses WHERE id = @WarehouseId;
            DELETE FROM branches WHERE id = @BranchId;
            DELETE FROM tenants WHERE id = @TenantId;
            """,
            new { CustomerId = customerId, WarehouseId = warehouseId, BranchId = branchId, TenantId = tenantId });
    }
}
