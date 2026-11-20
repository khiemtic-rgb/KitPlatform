using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.Pharmacy;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Sales;
using Xunit;

namespace KitPlatform.Tests;

public sealed class PharmacyPackOnboardingTests
{
    private static string ConnectionString => IsolatedDatabase.RequireConnection();

    [Fact]
    public void Pharmacy_registers_on_the_host_without_an_application_reference()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(ConnectionString));
        services.AddScoped<ITenantContext>(_ => new FixedTenant(Guid.NewGuid(), Guid.NewGuid()));
        services.AddPharmacyPack(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICatalogService>());
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IInventoryService));
        Assert.Equal("pharmacy", PharmacyPackDefinition.PackCode);
        Assert.Equal("novixa_pharmacy", PharmacyPackDefinition.TenantPackageCode);
        Assert.Contains("inventory", PharmacyPackDefinition.DefaultEnabledModules);
        Assert.Contains("sales", PharmacyPackDefinition.DefaultEnabledModules);
        Assert.DoesNotContain(PlatformModuleCodes.All, code => code.Equals("pharmacy", StringComparison.OrdinalIgnoreCase));

        var application = typeof(CoreAuthority).Assembly.GetReferencedAssemblies();
        var packApplication = typeof(PharmacyPackDefinition).Assembly.GetReferencedAssemblies();
        var infrastructure = typeof(TenantPlatformSettingsService).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(application, reference => reference.Name != null && reference.Name.Contains("Pharmacy", StringComparison.Ordinal));
        Assert.DoesNotContain(packApplication, reference => reference.Name is "KitPlatform.Api" or "KitPlatform.Application" or "KitPlatform.Infrastructure");
        Assert.Contains(infrastructure, reference => reference.Name == "KitPlatform.Packs.Pharmacy.Application");
    }

    [Fact]
    public async Task Catalog_permission_is_required_apart_from_the_module_gate()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddCatalogAuthorization());
        var auth = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();

        Assert.False((await auth.AuthorizeAsync(Principal(permission: null, role: null), CatalogPolicies.Read)).Succeeded);
        Assert.True((await auth.AuthorizeAsync(Principal(permission: "catalog.read", role: null), CatalogPolicies.Read)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(Principal(permission: "catalog.read", role: null), CatalogPolicies.Write)).Succeeded);
    }

    [Fact]
    public async Task Sales_module_gate_follows_persisted_settings_per_tenant()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var conn = await db.CreateOpenConnectionAsync();
        await InsertTenantAsync(conn, tenantA, "PHX" + tenantA.ToString("N")[..12]);
        await InsertTenantAsync(conn, tenantB, "PHX" + tenantB.ToString("N")[..12]);
        try
        {
            var settingsA = Settings(db, tenantA);
            var enabled = await settingsA.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "pharmacy",
                ["inventory", "sales"]));
            Assert.Contains("sales", enabled.Settings.EnabledModules);
            Assert.True(await ExecuteGate(settingsA, tenantA, "sales"));

            var disabled = await settingsA.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "pharmacy",
                ["inventory"]));
            Assert.DoesNotContain("sales", disabled.Settings.EnabledModules);
            Assert.False(await ExecuteGate(settingsA, tenantA, "sales"));
            Assert.True(await ExecuteGate(settingsA, tenantA, "inventory"));

            var settingsB = Settings(db, tenantB);
            var enabledB = await settingsB.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "pharmacy",
                ["inventory", "sales"]));
            Assert.Contains("sales", enabledB.Settings.EnabledModules);
            Assert.True(await ExecuteGate(settingsB, tenantB, "sales"));
            Assert.False(await ExecuteGate(settingsA, tenantA, "sales"));
        }
        finally
        {
            await DeleteProbeAsync(conn, tenantA);
            await DeleteProbeAsync(conn, tenantB);
        }
    }

    [Fact]
    public async Task Tenant_b_cannot_read_or_mutate_tenant_a_pharmacy_rows()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var marker = "PHX" + productId.ToString("N")[..12];
        await using var conn = await db.CreateOpenConnectionAsync();
        await InsertTenantAsync(conn, tenantA, "PHX" + tenantA.ToString("N")[..12]);
        await InsertTenantAsync(conn, tenantB, "PHX" + tenantB.ToString("N")[..12]);
        try
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO products (id, tenant_id, product_code, product_name)
                VALUES (@Id, @TenantId, @Code, @Name)
                """,
                new { Id = productId, TenantId = tenantA, Code = marker, Name = "Probe " + marker });
            await conn.ExecuteAsync(
                """
                INSERT INTO customers (id, tenant_id, customer_code, full_name, phone)
                VALUES (@Id, @TenantId, @Code, @Name, @Phone)
                """,
                new { Id = customerId, TenantId = tenantA, Code = marker, Name = "Khach " + marker, Phone = "090" + marker[^7..] });
            await conn.ExecuteAsync(
                """
                INSERT INTO branches (id, tenant_id, branch_code, branch_name)
                VALUES (@Id, @TenantId, 'HQ', 'Probe')
                """,
                new { Id = branchId, TenantId = tenantA });
            await conn.ExecuteAsync(
                """
                INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type)
                VALUES (@Id, @TenantId, @BranchId, 'WH', 'Probe', 1)
                """,
                new { Id = warehouseId, TenantId = tenantA, BranchId = branchId });
            await conn.ExecuteAsync(
                """
                INSERT INTO sales_orders (id, tenant_id, order_number, branch_id, warehouse_id, status)
                VALUES (@Id, @TenantId, @Number, @BranchId, @WarehouseId, @Status)
                """,
                new
                {
                    Id = orderId,
                    TenantId = tenantA,
                    Number = marker,
                    BranchId = branchId,
                    WarehouseId = warehouseId,
                    Status = SalesOrderStatuses.Draft,
                });
            await conn.ExecuteAsync(
                """
                INSERT INTO inventory_batches (
                    id, tenant_id, warehouse_id, product_id, batch_number, unit_cost, quantity_received, quantity_available)
                VALUES (@Id, @TenantId, @WarehouseId, @ProductId, @Batch, 1000, 1, 1)
                """,
                new { Id = batchId, TenantId = tenantA, WarehouseId = warehouseId, ProductId = productId, Batch = marker });

            var catalogA = new CatalogRepository(db, new FixedTenant(tenantA, Guid.NewGuid()));
            var catalogB = new CatalogRepository(db, new FixedTenant(tenantB, Guid.NewGuid()));
            Assert.Equal(productId, await catalogA.ProductExistsAsync(productId, CancellationToken.None));
            Assert.Null(await catalogB.ProductExistsAsync(productId, CancellationToken.None));
            Assert.False(await catalogB.UpdateProductAsync(productId, new UpdateProductRequest
            {
                ProductName = "Stolen " + marker,
                DrugType = 1,
                Status = 1,
            }, CancellationToken.None));
            var name = await conn.QuerySingleAsync<string>(
                "SELECT product_name FROM products WHERE id = @Id", new { Id = productId });
            Assert.Equal("Probe " + marker, name);
            Assert.False(await catalogB.SoftDeleteProductAsync(productId, CancellationToken.None));
            Assert.Equal(productId, await catalogA.ProductExistsAsync(productId, CancellationToken.None));

            var salesA = Sales(db, tenantA);
            var salesB = Sales(db, tenantB);
            var seenByA = await salesA.SearchCustomersAsync(marker, 1, 20, CancellationToken.None);
            var seenByB = await salesB.SearchCustomersAsync(marker, 1, 20, CancellationToken.None);
            Assert.Contains(seenByA.Items, item => item.Id == customerId);
            Assert.DoesNotContain(seenByB.Items, item => item.Id == customerId);
            var orderA = await salesA.GetSalesOrderAsync(orderId, CancellationToken.None);
            Assert.Equal(marker, orderA?.OrderNumber);
            Assert.Null(await salesB.GetSalesOrderAsync(orderId, CancellationToken.None));
            Assert.False(await salesB.CancelDraftSaleAsync(orderId, CancellationToken.None));
            var status = await conn.QuerySingleAsync<short>(
                "SELECT status FROM sales_orders WHERE id = @Id", new { Id = orderId });
            Assert.Equal(SalesOrderStatuses.Draft, status);

            var stockA = new InventoryRepository(db, new FixedTenant(tenantA, Guid.NewGuid()));
            var stockB = new InventoryRepository(db, new FixedTenant(tenantB, Guid.NewGuid()));
            Assert.Equal(marker, (await stockA.GetStockBatchAsync(batchId, CancellationToken.None))?.BatchNumber);
            Assert.Null(await stockB.GetStockBatchAsync(batchId, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => stockB.RevalueBatchUnitCostAsync(
                batchId, warehouseId, productId, 1000, 5, "probe", CancellationToken.None));
            var cost = await conn.QuerySingleAsync<decimal>(
                "SELECT unit_cost FROM inventory_batches WHERE id = @Id", new { Id = batchId });
            Assert.Equal(1000m, cost);
        }
        finally
        {
            await conn.ExecuteAsync("DELETE FROM sales_orders WHERE id = @Id", new { Id = orderId });
            await conn.ExecuteAsync("DELETE FROM inventory_batches WHERE id = @Id", new { Id = batchId });
            await conn.ExecuteAsync("DELETE FROM warehouses WHERE id = @Id", new { Id = warehouseId });
            await conn.ExecuteAsync("DELETE FROM branches WHERE id = @Id", new { Id = branchId });
            await conn.ExecuteAsync("DELETE FROM customers WHERE id = @Id", new { Id = customerId });
            await conn.ExecuteAsync("DELETE FROM products WHERE id = @Id", new { Id = productId });
            await DeleteProbeAsync(conn, tenantA);
            await DeleteProbeAsync(conn, tenantB);
        }
    }

    private static SalesRepository Sales(IDbConnectionFactory db, Guid tenantId) =>
        new(db, new FixedTenant(tenantId, Guid.NewGuid()), null!, null!, null!, null!, null!, null!, null!, null!, null!);

    private static TenantPlatformSettingsService Settings(IDbConnectionFactory db, Guid tenantId) =>
        new(db, new FixedTenant(tenantId, Guid.NewGuid()), new WorkspacePackProvisioner(db));

    private static ClaimsPrincipal Principal(string? permission, string? role)
    {
        var identity = new ClaimsIdentity(authenticationType: "TestAuth", nameType: "name", roleType: "role");
        if (permission is not null)
            identity.AddClaim(new Claim("permission", permission));
        if (role is not null)
            identity.AddClaim(new Claim("role", role));
        return new ClaimsPrincipal(identity);
    }

    private static async Task<bool> ExecuteGate(ITenantPlatformSettings settings, Guid tenantId, string moduleCode)
    {
        var http = new DefaultHttpContext();
        var identity = new ClaimsIdentity(authenticationType: "TestAuth");
        identity.AddClaim(new Claim(CoreAuthority.TenantIdClaim, tenantId.ToString()));
        http.User = new ClaimsPrincipal(identity);
        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            null!);
        var executed = false;
        await new PlatformModuleGateFilter(moduleCode, settings).OnActionExecutionAsync(
            context,
            () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });
        return executed && context.Result is null;
    }

    private static Task InsertTenantAsync(Npgsql.NpgsqlConnection conn, Guid id, string code) =>
        conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, business_vertical, settings)
            VALUES (
                @Id, @Code, 'Pharmacy onboarding probe', 'pharmacy',
                '{"platform":{"schema_version":1,"vertical":"pharmacy","enabled_modules":["inventory"],"allowed_modules":["inventory","sales"]}}'::jsonb)
            """,
            new { Id = id, Code = code });

    private static Task DeleteProbeAsync(Npgsql.NpgsqlConnection conn, Guid tenantId) =>
        conn.ExecuteAsync(
            """
            DELETE FROM kit_workspace.workspace_member WHERE tenant_id = @Id;
            DELETE FROM kit_workspace.workspace_setting WHERE tenant_id = @Id;
            DELETE FROM kit_tenant.tenant_subscription WHERE tenant_id = @Id;
            DELETE FROM kit_workspace.workspace_workspace WHERE tenant_id = @Id;
            DELETE FROM tenants WHERE id = @Id AND tenant_code LIKE 'PHX%';
            """,
            new { Id = tenantId });

    private sealed class FixedTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
