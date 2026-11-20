using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Security;
using Xunit;

namespace KitPlatform.Tests;

public sealed class CoreBoundaryRuntimeTests
{
    [Fact]
    public void Tenant_context_reads_the_jwt_tenant_and_does_not_require_workspace()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var http = new DefaultHttpContext();
        http.User = Principal(userId, tenantA, "system.read");
        var tenant = new TenantContext(new Accessor(http));

        Assert.True(tenant.IsAuthenticated);
        Assert.Equal(userId, tenant.UserId);
        Assert.Equal(tenantA, tenant.TenantId);
        Assert.Null(tenant.WorkspaceId);

        var thrown = Assert.Throws<UnauthorizedAccessException>(() =>
            CoreAuthority.RejectTenantSwitch(tenant.TenantId, tenantB));
        Assert.Contains("cannot switch tenant", thrown.Message, StringComparison.Ordinal);
        Assert.Equal(tenantA, tenant.TenantId);

        http.Items[WorkspaceResolutionMiddleware.WorkspaceIdItemKey] = Guid.NewGuid();
        Assert.Equal(tenantA, tenant.TenantId);
        Assert.NotNull(tenant.WorkspaceId);
        Assert.False(CoreAuthority.WorkspaceRequiredForTenantAuthorization);
    }

    [Fact]
    public async Task Permission_refresh_rejects_a_user_from_another_tenant()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var auth = new RecordingAuth(User(userId, tenantB, "system.read"));

        var result = await Transform(auth, Principal(userId, tenantA, "stale.permission", "ADMIN"));

        Assert.False(result.IsInRole("ADMIN"));
        Assert.DoesNotContain(result.Claims, claim => claim.Type == CoreAuthority.PermissionClaim);
        Assert.Equal((userId, tenantA), auth.LastCall);
    }

    [Fact]
    public async Task Permission_refresh_accepts_the_caller_tenant_user()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var auth = new RecordingAuth(User(userId, tenantA, "system.read"));

        var result = await Transform(auth, Principal(userId, tenantA, "stale.permission"));

        Assert.Contains(
            result.Claims,
            claim => claim.Type == CoreAuthority.PermissionClaim && claim.Value == "system.read");
        Assert.DoesNotContain(
            result.Claims,
            claim => claim.Type == CoreAuthority.PermissionClaim && claim.Value == "stale.permission");
        Assert.True(result.IsInRole("ADMIN"));
        Assert.Equal((userId, tenantA), auth.LastCall);
    }

    [Fact]
    public async Task Permission_refresh_strips_roles_and_permissions_when_user_is_inactive()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var auth = new RecordingAuth(User(userId, tenantA, "sales.read", status: 2, role: "ADMIN"));

        var result = await Transform(auth, Principal(userId, tenantA, "sales.read", "ADMIN"));

        Assert.False(result.IsInRole("ADMIN"));
        Assert.False(result.IsInRole("MANAGER"));
        Assert.DoesNotContain(result.Claims, claim => claim.Type == CoreAuthority.PermissionClaim);
    }

    [Fact]
    public async Task Permission_refresh_strips_permissions_when_user_is_missing()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var auth = new RecordingAuth(null);

        var result = await Transform(auth, Principal(userId, tenantA, "sales.read", "ADMIN"));

        Assert.False(result.IsInRole("ADMIN"));
        Assert.DoesNotContain(result.Claims, claim => claim.Type == CoreAuthority.PermissionClaim);
    }

    [Fact]
    public async Task Permission_refresh_strips_admin_when_inactive_user_had_admin_on_the_token()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var auth = new RecordingAuth(User(userId, tenantA, "reports.read", status: 2, role: "ADMIN"));

        var result = await Transform(auth, Principal(userId, tenantA, "reports.read", "ADMIN"));

        Assert.False(result.IsInRole("ADMIN"));
        Assert.DoesNotContain(
            result.Claims,
            claim => claim.Type == CoreAuthority.PermissionClaim && claim.Value == "reports.read");
    }

    [Fact]
    public async Task Permission_refresh_strips_sales_permission_when_staff_user_is_inactive()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var auth = new RecordingAuth(User(userId, tenantA, "sales.read", status: 2, role: "STAFF"));

        var result = await Transform(auth, Principal(userId, tenantA, "sales.read", "STAFF"));

        Assert.False(result.IsInRole("STAFF"));
        Assert.DoesNotContain(
            result.Claims,
            claim => claim.Type == CoreAuthority.PermissionClaim && claim.Value == "sales.read");
    }

    [Fact]
    public async Task Identity_policy_rejects_without_permission_and_accepts_with_permission()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddIdentityAuthorization());
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var denied = await authorization.AuthorizeAsync(Principal(userId, tenantId), IdentityPolicies.Read);
        var allowed = await authorization.AuthorizeAsync(
            Principal(userId, tenantId, "system.read"),
            IdentityPolicies.Read);

        Assert.False(denied.Succeeded);
        Assert.True(allowed.Succeeded);
        Assert.False(CoreAuthority.WorkspaceRequiredForTenantAuthorization);
    }

    [Fact]
    public async Task Module_gate_ignores_workspace_and_follows_the_enabled_flag()
    {
        var tenantId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        Assert.True(await ExecuteModuleGate(enabled: true, tenantId, workspaceId, PlatformModuleCodes.Learning));
        Assert.False(await ExecuteModuleGate(enabled: false, tenantId, workspaceId, PlatformModuleCodes.Learning));
    }

    private static async Task<bool> ExecuteModuleGate(
        bool enabled,
        Guid tenantId,
        Guid workspaceId,
        string moduleCode)
    {
        var http = new DefaultHttpContext();
        http.User = Principal(Guid.NewGuid(), tenantId);
        http.Items[WorkspaceResolutionMiddleware.WorkspaceIdItemKey] = workspaceId;
        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            null!);
        var executed = false;
        var filter = new PlatformModuleGateFilter(moduleCode, new ModuleFlag(enabled));
        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                executed = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });
        return executed && context.Result is null;
    }

    private static Task<ClaimsPrincipal> Transform(IAuthService auth, ClaimsPrincipal principal)
    {
        var transformation = new DbPermissionsClaimsTransformation(auth, new MemoryCache(new MemoryCacheOptions()));
        return transformation.TransformAsync(principal);
    }

    private static ClaimsPrincipal Principal(Guid userId, Guid tenantId, string? permission = null, string? role = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "TestAuth");
        identity.AddClaim(new Claim(CoreAuthority.SubjectClaim, userId.ToString()));
        identity.AddClaim(new Claim(CoreAuthority.TenantIdClaim, tenantId.ToString()));
        if (permission is not null)
            identity.AddClaim(new Claim(CoreAuthority.PermissionClaim, permission));
        if (role is not null)
            identity.AddClaim(new Claim(identity.RoleClaimType, role));
        return new ClaimsPrincipal(identity);
    }

    private static AuthUserDto User(Guid userId, Guid tenantId, string permission, short status = 1, string role = "ADMIN") =>
        new(userId, tenantId, "TENANT", "ada", "ada@example.com", [role], [permission], status);

    private sealed class Accessor(HttpContext http) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = http;
    }

    private sealed class RecordingAuth(AuthUserDto? user) : IAuthService
    {
        public (Guid UserId, Guid CallerTenantId)? LastCall { get; private set; }

        public Task<AuthUserDto?> GetUserAsync(Guid userId, Guid callerTenantId, CancellationToken cancellationToken = default)
        {
            LastCall = (userId, callerTenantId);
            return Task.FromResult(user);
        }

        public Task<AuthLoginResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LoginResponse?> SelectWorkspaceAsync(SelectWorkspaceRequest request, string? ipAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AuthWorkspaceDto>> ListWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ModuleFlag(bool enabled) : ITenantPlatformSettings
    {
        public Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(enabled);

        public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(UpdateTenantPlatformSettingsRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsModuleEnabledForTenantCodeAsync(string tenantCode, string moduleCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsFeatureEnabledForTenantCodeAsync(string tenantCode, string featureCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(Guid? branchId = null, Guid? customerAccountId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
