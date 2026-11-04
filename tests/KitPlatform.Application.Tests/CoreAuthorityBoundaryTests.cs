using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KitPlatform.Application.Core;
using KitPlatform.Application.Configuration;
using KitPlatform.Domain.Common;
using KitPlatform.Infrastructure.Auth;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Inventory;
using Microsoft.Extensions.Options;
using Xunit;

namespace KitPlatform.Tests;

public sealed class CoreAuthorityBoundaryTests
{
    [Fact]
    public void Access_token_resolves_the_user_row_identity_and_tenant()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var jwt = new JwtTokenService(Options.Create(new JwtSettings
        {
            Secret = new string('k', 48),
            Issuer = "KitPlatform",
            Audience = "KitPlatform.Client",
            AccessTokenExpireMinutes = 30,
        }));

        var (token, _) = jwt.CreateAccessToken(new UserRecord
        {
            Id = userId,
            TenantId = tenantId,
            TenantCode = "TENANT_A",
            Username = "ada",
            Email = "ada@example.com",
            Roles = ["ADMIN"],
            Permissions = ["system.read"],
        });

        var issued = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(issued.Claims, authenticationType: "TestAuth"));

        Assert.True(CoreAuthority.TryResolveCaller(principal, out var caller));
        Assert.Equal(userId, caller.UserId);
        Assert.Equal(tenantId, caller.TenantId);
        Assert.Equal(tenantId.ToString(), issued.Claims.Single(claim => claim.Type == CoreAuthority.TenantIdClaim).Value);
        Assert.True(CoreAuthority.HasPermission(principal, "system.read"));
        Assert.False(CoreAuthority.HasPermission(principal, "system.write"));
        Assert.False(CoreAuthority.WorkspaceRequiredForTenantAuthorization);
    }

    [Fact]
    public void Caller_cannot_silently_switch_tenant()
    {
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var principal = Caller(Guid.NewGuid(), tenantA);

        Assert.True(CoreAuthority.TryResolveCaller(principal, out var caller));
        Assert.Equal(tenantA, caller.TenantId);
        Assert.Throws<UnauthorizedAccessException>(() => CoreAuthority.RejectTenantSwitch(caller.TenantId, tenantB));
        Assert.True(CoreAuthority.TryResolveCaller(principal, out var again));
        Assert.Equal(tenantA, again.TenantId);
    }

    [Fact]
    public void Tenant_A_cannot_read_a_tenant_B_user()
    {
        var tenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.True(CoreAuthority.TryResolveCaller(Caller(Guid.NewGuid(), tenantA), out var caller));

        Assert.False(AuthService.MayExposeUser(caller.TenantId, tenantB));
        Assert.True(AuthService.MayExposeUser(caller.TenantId, tenantA));
        Assert.False(AuthService.MayExposeUser(Guid.Empty, tenantA));
    }

    [Fact]
    public void Module_membership_follows_the_enabled_list_without_workspace()
    {
        var enabled = new[] { PlatformModuleCodes.KitContent };
        Assert.True(CoreAuthority.IsModuleEnabled(enabled, PlatformModuleCodes.KitContent));
        Assert.False(CoreAuthority.IsModuleEnabled(enabled, PlatformModuleCodes.FamilyOs));
        Assert.False(CoreAuthority.IsModuleEnabled(enabled, " "));
        Assert.False(CoreAuthority.WorkspaceRequiredForTenantAuthorization);
    }

    [Fact]
    public void Application_does_not_define_a_permission_engine()
    {
        var names = typeof(CoreAuthority).Assembly.GetTypes().Select(type => type.Name).ToHashSet();
        Assert.DoesNotContain("IPermissionEngine", names);
        Assert.DoesNotContain("PermissionEngine", names);
        Assert.DoesNotContain("IAuthorizationEngine", names);
        Assert.DoesNotContain("IPermissionService", names);
        Assert.DoesNotContain("IPolicyEngine", names);
    }

    [Fact]
    public void Application_does_not_define_an_inventory_engine()
    {
        var names = typeof(CoreAuthority).Assembly.GetTypes().Select(type => type.Name).ToHashSet();
        Assert.DoesNotContain("IInventoryEngine", names);
        Assert.DoesNotContain("InventoryEngine", names);
        Assert.True(typeof(IBatchResolver).IsAssignableFrom(typeof(BatchResolver)));
    }

    [Fact]
    public void Application_does_not_define_a_pricing_engine()
    {
        var names = typeof(CoreAuthority).Assembly.GetTypes().Select(type => type.Name).ToHashSet();
        Assert.DoesNotContain("IPricingEngine", names);
        Assert.DoesNotContain("PricingEngine", names);
        Assert.DoesNotContain("ICorePricingEngine", names);
    }

    [Fact]
    public void Application_does_not_define_a_workflow_engine()
    {
        var names = typeof(CoreAuthority).Assembly.GetTypes().Select(type => type.Name).ToHashSet();
        Assert.DoesNotContain("IWorkflowEngine", names);
        Assert.DoesNotContain("WorkflowEngine", names);
        Assert.DoesNotContain("DiscountApprovalRequiredException", names);
        Assert.DoesNotContain("WorkflowTaskListItemDto", names);
        Assert.DoesNotContain("WorkflowTaskDecisionDto", names);
    }

    [Fact]
    public void Core_assemblies_do_not_reference_famixa_production_types()
    {
        var forbidden = new[]
        {
            "ProductionTask",
            "ProductionTaskInputSnapshot",
            "ProductionAttempt",
            "ProductionArtifact",
            "ProductionBinding",
            "FilmAssembly",
            "production_task_events",
            "video_generation_execution_event",
        };

        foreach (var assembly in new[] { typeof(CoreAuthority).Assembly, typeof(KernelEntity).Assembly })
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                name => name.Name?.Contains("Content", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var type in assembly.GetTypes())
            {
                var fullName = type.FullName ?? "";
                foreach (var token in forbidden)
                    Assert.DoesNotContain(token, fullName, StringComparison.Ordinal);
            }
        }
    }

    private static ClaimsPrincipal Caller(Guid userId, Guid tenantId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(CoreAuthority.SubjectClaim, userId.ToString()),
                new Claim(CoreAuthority.TenantIdClaim, tenantId.ToString()),
            ],
            authenticationType: "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
