using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.TestPack;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Packs.TestPack;
using KitPlatform.Packs.TestPack.Infrastructure;
using Xunit;

namespace KitPlatform.Tests;

public sealed class TestPackOnboardingTests
{
    [Fact]
    public void Test_pack_registers_sql_probe()
    {
        var services = new ServiceCollection();
        services.AddSingleton<KitPlatform.Infrastructure.Data.IDbConnectionFactory>(
            new KitPlatform.Infrastructure.Data.NpgsqlConnectionFactory("Host=localhost"));
        services.AddTestPack();
        services.AddSingleton<ITenantContext>(new FixedTenant(Guid.NewGuid(), Guid.NewGuid()));
        using var provider = services.BuildServiceProvider();

        var probe = provider.GetRequiredService<ITestPackProbe>();
        var controller = ActivatorUtilities.CreateInstance<TestPackController>(provider);

        Assert.Equal("SqlTestPackProbe", probe.GetType().Name);
        Assert.NotNull(controller);
        Assert.Equal("test_pack", TestPackDefinition.PackCode);
    }

    [Fact]
    public void Test_pack_application_does_not_reference_the_host_backward()
    {
        var application = typeof(TestPackDefinition).Assembly.GetReferencedAssemblies();
        var infrastructure = typeof(TestPackDependencyInjection).Assembly.GetReferencedAssemblies();
        var core = typeof(CoreAuthority).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(application, reference => reference.Name is "KitPlatform.Application" or "KitPlatform.Infrastructure" or "KitPlatform.Api");
        Assert.Contains(infrastructure, reference => reference.Name == "KitPlatform.Infrastructure");
        Assert.DoesNotContain(infrastructure, reference => reference.Name == "KitPlatform.Api");
        Assert.DoesNotContain(core, reference => reference.Name != null && reference.Name.Contains("TestPack", StringComparison.Ordinal));
    }

    [Fact]
    public void Marker_write_does_not_accept_a_request_tenant()
    {
        var write = typeof(TestPackMarkerWrite).GetProperties().Select(property => property.Name).ToArray();
        var ping = typeof(TestPackController).GetMethod(nameof(TestPackController.Ping))!;
        var remember = typeof(TestPackController).GetMethod(nameof(TestPackController.Remember))!;

        Assert.Equal(["Marker"], write);
        Assert.DoesNotContain(ping.GetParameters(), parameter => parameter.Name!.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(remember.GetParameters(), parameter => parameter.ParameterType == typeof(Guid));
    }

    [Fact]
    public async Task Missing_permission_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Assert.False((await Authorize(Principal(userId, tenantId))).Succeeded);
        Assert.True((await Authorize(Principal(userId, tenantId, TestPackDefinition.ReadPermission))).Succeeded);
        Assert.Throws<UnauthorizedAccessException>(() => CoreAuthority.RejectTenantSwitch(tenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Module_gate_follows_the_enabled_flag()
    {
        var tenantId = Guid.NewGuid();
        Assert.True(await ExecuteGate(enabled: true, tenantId));
        Assert.False(await ExecuteGate(enabled: false, tenantId));
    }

    private static async Task<AuthorizationResult> Authorize(ClaimsPrincipal principal)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddTestPackAuthorization());
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        return await authorization.AuthorizeAsync(principal, TestPackPolicies.Read);
    }

    private static async Task<bool> ExecuteGate(bool enabled, Guid tenantId)
    {
        var http = new DefaultHttpContext();
        http.User = Principal(Guid.NewGuid(), tenantId, TestPackDefinition.ReadPermission);
        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            null!);
        var executed = false;
        var filter = new PlatformModuleGateFilter(TestPackDefinition.PrimaryModuleCode, new ModuleFlag(enabled));
        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });
        return executed && context.Result is null;
    }

    private static ClaimsPrincipal Principal(Guid userId, Guid tenantId, string? permission = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "TestAuth");
        identity.AddClaim(new Claim(CoreAuthority.SubjectClaim, userId.ToString()));
        identity.AddClaim(new Claim(CoreAuthority.TenantIdClaim, tenantId.ToString()));
        if (permission is not null)
            identity.AddClaim(new Claim(CoreAuthority.PermissionClaim, permission));
        return new ClaimsPrincipal(identity);
    }

    private sealed class FixedTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    private sealed class ModuleFlag(bool enabled) : ITenantPlatformSettings
    {
        public Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(enabled && string.Equals(moduleCode, TestPackDefinition.PrimaryModuleCode, StringComparison.OrdinalIgnoreCase));

        public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(
            UpdateTenantPlatformSettingsRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsModuleEnabledForTenantCodeAsync(string tenantCode, string moduleCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsFeatureEnabledForTenantCodeAsync(string tenantCode, string featureCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(
            Guid? branchId = null,
            Guid? customerAccountId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
