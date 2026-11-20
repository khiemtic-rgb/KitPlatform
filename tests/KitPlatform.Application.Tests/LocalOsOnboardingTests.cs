using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.LocalOs;
using KitPlatform.Api.Controllers.Public;
using KitPlatform.Api.LocalOs;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.LocalOs;
using KitPlatform.Packs.LocalOs.Infrastructure;
using Xunit;

namespace KitPlatform.Tests;

public sealed class LocalOsOnboardingTests
{
    private static string ConnectionString => IsolatedDatabase.RequireConnection();

    [Fact]
    public void Local_os_registers_listing_service_and_watch_worker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(ConnectionString));
        services.AddLocalOsPack();
        using var provider = services.BuildServiceProvider();

        var listings = provider.GetRequiredService<ILocalOsListingService>();
        var sources = provider.GetRequiredService<ILocalOsSourceService>();
        var worker = provider.GetServices<IHostedService>().Single(service => service.GetType().Name == "LocalOsWatchWorker");

        Assert.DoesNotContain(
            worker.GetType().GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ITenantContext));
        Assert.Equal("LocalOsListingService", listings.GetType().Name);
        Assert.Equal("LocalOsSourceService", sources.GetType().Name);
        Assert.Equal("local_os", LocalOsPackDefinition.PackCode);
        Assert.Equal(PlatformModuleCodes.LocalOs, LocalOsPackDefinition.PrimaryModuleCode);
        Assert.NotNull(worker);
    }

    [Fact]
    public void Local_os_project_boundary_matches_pack_to_platform_direction()
    {
        var application = typeof(LocalOsPackDefinition).Assembly.GetReferencedAssemblies();
        var infrastructure = typeof(LocalOsPackDependencyInjection).Assembly.GetReferencedAssemblies();
        var core = typeof(CoreAuthority).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(application, reference => reference.Name is "KitPlatform.Application" or "KitPlatform.Infrastructure" or "KitPlatform.Api");
        Assert.Contains(infrastructure, reference => reference.Name == "KitPlatform.Infrastructure");
        Assert.DoesNotContain(infrastructure, reference => reference.Name is "KitPlatform.Application" or "KitPlatform.Api");
        Assert.DoesNotContain(core, reference => reference.Name != null && reference.Name.Contains("LocalOs", StringComparison.Ordinal));
    }

    [Fact]
    public void Admin_endpoint_requires_admin_role_and_does_not_read_a_request_tenant()
    {
        var authorize = typeof(LocalOsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        var gate = typeof(LocalOsController).GetCustomAttributes(typeof(RequirePlatformModuleAttribute), inherit: true)
            .Cast<RequirePlatformModuleAttribute>()
            .Single();
        var queryNames = typeof(LocalListingQuery).GetProperties().Select(property => property.Name).ToArray();
        var list = typeof(LocalOsController).GetMethod(nameof(LocalOsController.List))!;
        var sources = typeof(LocalOsController).GetMethod(nameof(LocalOsController.ListSources))!;

        Assert.Equal("ADMIN", authorize.Roles);
        Assert.Null(authorize.Policy);
        Assert.Equal(LocalOsPackDefinition.PrimaryModuleCode, gate.Arguments![0]);
        Assert.NotNull(typeof(LocalOsController).GetCustomAttributes(typeof(RequireLocalOsOperatorAttribute), inherit: true).Single());
        Assert.DoesNotContain(queryNames, name => name.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(list.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
        Assert.DoesNotContain(sources.GetParameters(), parameter => parameter.Name!.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(LocalOsController).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ITenantContext));
    }

    [Fact]
    public async Task Admin_role_authorizes_and_local_os_permission_alone_does_not()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        var auth = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        IAuthorizationRequirement requirement = new RolesAuthorizationRequirement(["ADMIN"]);

        Assert.False((await auth.AuthorizeAsync(Principal(role: null, permission: "local_os.read"), null, [requirement])).Succeeded);
        Assert.True((await auth.AuthorizeAsync(Principal(role: "ADMIN", permission: null), null, [requirement])).Succeeded);
    }

    [Fact]
    public void Public_browse_is_anonymous_and_has_no_module_gate()
    {
        Assert.NotNull(typeof(PublicLocalOsController).GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Single());
        Assert.Empty(typeof(PublicLocalOsController).GetCustomAttributes(typeof(RequirePlatformModuleAttribute), inherit: true));
        Assert.Empty(typeof(PublicLocalOsController).GetCustomAttributes(typeof(RequireLocalOsOperatorAttribute), inherit: true));
    }

    [Fact]
    public async Task Registry_row_exists_and_listing_has_no_tenant_column()
    {
        await using var conn = await OpenAsync();
        var module = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT module_code FROM platform_module_registry WHERE module_code = 'local_os' AND status = 1");
        var tenantColumns = await conn.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'pack_local'
              AND table_name = 'listing'
              AND column_name = 'tenant_id'
            """);
        var index = await conn.QuerySingleOrDefaultAsync<string>(
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'pack_local' AND tablename = 'listing' AND indexname = 'ix_local_listing_public'
            """);

        Assert.Equal("local_os", module);
        Assert.Equal(0, tenantColumns);
        Assert.Equal("ix_local_listing_public", index);
    }

    [Fact]
    public void Operator_codes_match_the_settings_allowlist()
    {
        foreach (var code in new[] { "KIT_LOCAL", "kit_mkt", "DEMO_PHARMACY", "NT_XUANHOA", "RLO", "", null })
            Assert.Equal(LocalOsTenantBoundary.IsAdminOperator(code), LocalOsDedicatedOperators.Allows(code));
    }

    [Fact]
    public void Entitlement_rejects_local_os_for_any_other_tenant()
    {
        LocalOsDedicatedOperators.RejectEntitlement("KIT_LOCAL", ["local_os"], ["local_os"]);
        LocalOsDedicatedOperators.RejectEntitlement("KIT_MKT", ["kit_content", "local_os"], ["kit_content", "local_os"]);
        Assert.Throws<InvalidOperationException>(() =>
            LocalOsDedicatedOperators.RejectEntitlement("DEMO_PHARMACY", ["inventory", "local_os"], ["inventory"]));
        LocalOsDedicatedOperators.RejectEntitlement("NT_XUANHOA", ["inventory", "sales"], ["inventory", "sales"]);
    }

    [Fact]
    public async Task Settings_update_drops_local_os_for_any_other_tenant()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        var tenantId = Guid.NewGuid();
        var code = "RLO" + tenantId.ToString("N")[..12];
        await using var conn = await db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, business_vertical, settings)
            VALUES (
                @Id, @Code, 'Local OS onboarding probe', 'hybrid',
                '{"platform":{"schema_version":1,"vertical":"hybrid","enabled_modules":["inventory"],"allowed_modules":["inventory","local_os"]}}'::jsonb)
            """,
            new { Id = tenantId, Code = code });

        try
        {
            var settings = new TenantPlatformSettingsService(
                db, new FixedTenant(tenantId, Guid.NewGuid()), new WorkspacePackProvisioner(db));
            var updated = await settings.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "hybrid",
                ["inventory", LocalOsPackDefinition.PrimaryModuleCode]));

            Assert.Contains(LocalOsPackDefinition.PrimaryModuleCode, updated.IgnoredModuleCodes);
            Assert.DoesNotContain(LocalOsPackDefinition.PrimaryModuleCode, updated.Settings.EnabledModules);
            Assert.False(await ExecuteGate(settings, tenantId));
            Assert.False(await OperatorAllowsAsync(db, tenantId));
        }
        finally
        {
            await DeleteProbeTenantAsync(conn, tenantId);
        }
    }

    [Fact]
    public async Task Kit_local_keeps_local_os_and_another_tenant_cannot_open_admin_data()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        await using var conn = await db.CreateOpenConnectionAsync();
        var owner = await conn.QuerySingleAsync<(Guid Id, string Settings)>(
            "SELECT id, settings::text FROM tenants WHERE tenant_code = 'KIT_LOCAL' AND deleted_at IS NULL");
        var probeId = Guid.NewGuid();
        var probeCode = "RLO" + probeId.ToString("N")[..12];
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, business_vertical, settings)
            VALUES (
                @Id, @Code, 'Local OS boundary probe', 'hybrid',
                '{"platform":{"schema_version":1,"vertical":"hybrid","enabled_modules":["inventory","local_os"],"allowed_modules":["inventory","local_os"]}}'::jsonb)
            """,
            new { Id = probeId, Code = probeCode });

        try
        {
            var settings = new TenantPlatformSettingsService(
                db, new FixedTenant(owner.Id, Guid.NewGuid()), new WorkspacePackProvisioner(db));
            var kept = await settings.UpdateAsync(new UpdateTenantPlatformSettingsRequest(
                "local",
                [LocalOsPackDefinition.PrimaryModuleCode]));

            Assert.DoesNotContain(LocalOsPackDefinition.PrimaryModuleCode, kept.IgnoredModuleCodes);
            Assert.Contains(LocalOsPackDefinition.PrimaryModuleCode, kept.Settings.EnabledModules);
            Assert.True(await ExecuteGate(settings, owner.Id));
            Assert.True(await OperatorAllowsAsync(db, owner.Id));

            var sources = new ServiceCollection();
            sources.AddLogging();
            sources.AddSingleton<IDbConnectionFactory>(db);
            sources.AddLocalOsPack();
            var provider = sources.BuildServiceProvider();
            var sourceService = provider.GetRequiredService<ILocalOsSourceService>();
            var opened = false;
            Assert.False(await RunOperatorFilter(db, probeId, () => opened = true));
            Assert.False(opened);

            Assert.True(await RunOperatorFilter(db, owner.Id, () => opened = true));
            var result = await new LocalOsController(
                null!, null!, sourceService, null!, null!, null!, null!, null!, null!,
                provider.GetRequiredService<ILogger<LocalOsController>>())
                .ListSources(CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var rows = Assert.IsAssignableFrom<IReadOnlyList<LocalSourceDto>>(ok.Value);
            var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM pack_local.source");
            Assert.Equal(count, rows.Count);
            Assert.True(opened);
        }
        finally
        {
            await conn.ExecuteAsync(
                "UPDATE tenants SET settings = @Settings::jsonb WHERE id = @Id AND tenant_code = 'KIT_LOCAL'",
                new { owner.Id, owner.Settings });
            await DeleteProbeTenantAsync(conn, probeId);
        }
    }

    [Fact]
    public async Task Content_publish_into_the_park_rejects_any_other_tenant()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        await using var conn = await db.CreateOpenConnectionAsync();
        var other = await conn.QuerySingleAsync<Guid>(
            "SELECT id FROM tenants WHERE tenant_code = 'DEMO_PHARMACY' AND deleted_at IS NULL");
        var publisher = new ContentLocalOsPublisher(
            null!,
            null!,
            new FixedTenant(other, Guid.NewGuid()),
            db,
            null!,
            NullLogger<ContentLocalOsPublisher>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishArticleAsync(
                new ContentLocalOsPublishRequest(Guid.NewGuid(), "Tin thử", "Nội dung", null, null, null),
                CancellationToken.None));

        Assert.Contains("KIT_LOCAL", ex.Message);
    }

    [Fact]
    public async Task Sources_endpoint_reads_pack_local_without_a_tenant_predicate()
    {
        var db = new NpgsqlConnectionFactory(ConnectionString);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDbConnectionFactory>(db);
        services.AddLocalOsPack();
        var sources = services.BuildServiceProvider().GetRequiredService<ILocalOsSourceService>();
        var controller = new LocalOsController(
            null!, null!, sources, null!, null!, null!, null!, null!, null!,
            services.BuildServiceProvider().GetRequiredService<ILogger<LocalOsController>>());

        var result = await controller.ListSources(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<LocalSourceDto>>(ok.Value);

        await using var conn = await db.CreateOpenConnectionAsync();
        var count = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM pack_local.source");
        Assert.Equal(count, rows.Count);
        Assert.DoesNotContain(
            typeof(ILocalOsSourceService).GetMethod(nameof(ILocalOsSourceService.ListAsync))!.GetParameters(),
            parameter => parameter.ParameterType == typeof(Guid));
    }

    private static ClaimsPrincipal Principal(string? role, string? permission)
    {
        var identity = new ClaimsIdentity(authenticationType: "TestAuth", nameType: "name", roleType: "role");
        if (role is not null)
            identity.AddClaim(new Claim("role", role));
        if (permission is not null)
            identity.AddClaim(new Claim(CoreAuthority.PermissionClaim, permission));
        return new ClaimsPrincipal(identity);
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
        var filter = new PlatformModuleGateFilter(LocalOsPackDefinition.PrimaryModuleCode, settings);
        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });
        return executed && context.Result is null;
    }

    private static async Task<bool> OperatorAllowsAsync(IDbConnectionFactory db, Guid tenantId)
    {
        await using var conn = await db.CreateOpenConnectionAsync();
        var code = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT tenant_code FROM tenants WHERE id = @Id",
            new { Id = tenantId });
        return LocalOsTenantBoundary.IsAdminOperator(code);
    }

    private static async Task<bool> RunOperatorFilter(IDbConnectionFactory db, Guid tenantId, Action onAllowed)
    {
        var http = new DefaultHttpContext();
        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            null!);
        var executed = false;
        var filter = new LocalOsOperatorFilter(new FixedTenant(tenantId, Guid.NewGuid()), db);
        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                executed = true;
                onAllowed();
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });
        return executed && context.Result is null;
    }

    private static async Task DeleteProbeTenantAsync(Npgsql.NpgsqlConnection conn, Guid tenantId)
    {
        await conn.ExecuteAsync(
            """
            DELETE FROM kit_workspace.workspace_member WHERE tenant_id = @Id;
            DELETE FROM kit_workspace.workspace_setting WHERE tenant_id = @Id;
            DELETE FROM kit_tenant.tenant_subscription WHERE tenant_id = @Id;
            DELETE FROM kit_workspace.workspace_workspace WHERE tenant_id = @Id;
            DELETE FROM tenants WHERE id = @Id AND tenant_code LIKE 'RLO%';
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
