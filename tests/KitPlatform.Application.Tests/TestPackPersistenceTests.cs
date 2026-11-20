using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.TestPack;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.TestPack;
using KitPlatform.Packs.TestPack.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KitPlatform.Tests;

public sealed class TestPackPersistenceTests
{
    private static string ConnectionString => IsolatedDatabase.RequireConnection();

    [Fact]
    public async Task Registry_row_and_marker_table_exist()
    {
        await using var conn = await OpenAsync();
        var module = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT module_code FROM platform_module_registry WHERE module_code = 'test_pack' AND status = 1");
        var column = await conn.QuerySingleAsync<(string Name, string Nullable)>(
            """
            SELECT column_name, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'pack_test' AND table_name = 'marker' AND column_name = 'tenant_id'
            """);
        var index = await conn.QuerySingleOrDefaultAsync<string>(
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'pack_test' AND tablename = 'marker' AND indexname = 'uq_pack_test_marker_tenant'
            """);

        Assert.Equal("test_pack", module);
        Assert.Equal("tenant_id", column.Name);
        Assert.Equal("NO", column.Nullable);
        Assert.Equal("uq_pack_test_marker_tenant", index);
    }

    [Fact]
    public async Task Settings_update_persists_and_the_module_gate_follows_it()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        var tenantId = Guid.NewGuid();
        var code = "TPC" + tenantId.ToString("N")[..12];
        await using var conn = await db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, business_vertical, settings)
            VALUES (
                @Id, @Code, 'KIT Test Pack Canary', 'hybrid',
                '{"platform":{"schema_version":1,"vertical":"hybrid","enabled_modules":["inventory"],"allowed_modules":["inventory","test_pack"]}}'::jsonb)
            """,
            new { Id = tenantId, Code = code });

        try
        {
            var settings = new TenantPlatformSettingsService(db, new FixedTenant(tenantId, Guid.NewGuid()), new WorkspacePackProvisioner(db));
            var enabled = await settings.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "hybrid",
                ["inventory", TestPackDefinition.PrimaryModuleCode]));

            Assert.DoesNotContain(TestPackDefinition.PrimaryModuleCode, enabled.IgnoredModuleCodes);
            Assert.Contains(TestPackDefinition.PrimaryModuleCode, enabled.Settings.EnabledModules);
            Assert.True(await ExecuteGate(settings, tenantId));

            var disabled = await settings.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "hybrid",
                ["inventory"]));

            Assert.DoesNotContain(TestPackDefinition.PrimaryModuleCode, disabled.Settings.EnabledModules);
            Assert.False(await ExecuteGate(settings, tenantId));

            var again = await settings.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "hybrid",
                ["inventory", TestPackDefinition.PrimaryModuleCode]));
            Assert.Contains(TestPackDefinition.PrimaryModuleCode, again.Settings.EnabledModules);
            Assert.True(await ExecuteGate(settings, tenantId));
        }
        finally
        {
            await DeleteCanaryTenantAsync(conn, tenantId);
        }
    }

    [Fact]
    public async Task Sql_marker_is_isolated_by_caller_tenant()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        var services = new ServiceCollection();
        services.AddSingleton<IDbConnectionFactory>(db);
        services.AddTestPack();
        var probe = services.BuildServiceProvider().GetRequiredService<ITestPackProbe>();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var user = Guid.NewGuid();
        try
        {
            await new TestPackController(new FixedTenant(tenantA, user), probe)
                .Remember(new TestPackMarkerWrite("marker-a"), CancellationToken.None);
            await new TestPackController(new FixedTenant(tenantB, user), probe)
                .Remember(new TestPackMarkerWrite("marker-b"), CancellationToken.None);

            var seenByA = await Ping(probe, tenantA, user);
            var seenByB = await Ping(probe, tenantB, user);

            Assert.Equal(tenantA, seenByA.TenantId);
            Assert.Equal("marker-a", seenByA.Marker);
            Assert.Equal(TestPackDefinition.PackCode, seenByA.Pack);
            Assert.Equal(tenantB, seenByB.TenantId);
            Assert.Equal("marker-b", seenByB.Marker);
            Assert.NotEqual(seenByA.Marker, seenByB.Marker);
        }
        finally
        {
            await using var conn = await db.CreateOpenConnectionAsync();
            await conn.ExecuteAsync(
                "DELETE FROM pack_test.marker WHERE tenant_id = ANY(@Ids)",
                new { Ids = new[] { tenantA, tenantB } });
        }
    }

    private static async Task<TestPackPing> Ping(ITestPackProbe probe, Guid tenantId, Guid userId)
    {
        var result = await new TestPackController(new FixedTenant(tenantId, userId), probe)
            .Ping(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<TestPackPing>(ok.Value);
    }

    private static async Task<bool> ExecuteGate(ITenantPlatformSettings settings, Guid tenantId)
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
        var filter = new PlatformModuleGateFilter(TestPackDefinition.PrimaryModuleCode, settings);
        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });
        return executed && context.Result is null;
    }

    private static async Task DeleteCanaryTenantAsync(Npgsql.NpgsqlConnection conn, Guid tenantId)
    {
        await conn.ExecuteAsync(
            """
            DELETE FROM kit_workspace.workspace_member WHERE tenant_id = @Id;
            DELETE FROM kit_workspace.workspace_setting WHERE tenant_id = @Id;
            DELETE FROM kit_tenant.tenant_subscription WHERE tenant_id = @Id;
            DELETE FROM kit_workspace.workspace_workspace WHERE tenant_id = @Id;
            DELETE FROM tenants WHERE id = @Id AND tenant_code LIKE 'TPC%';
            """,
            new { Id = tenantId });
    }

    private static async Task<Npgsql.NpgsqlConnection> OpenAsync()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        return await db.CreateOpenConnectionAsync();
    }

    private sealed class FixedTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
