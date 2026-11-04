using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Identity;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Identity;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Xunit;

namespace KitPlatform.Tests;

public sealed class ReservedPermissionsTests
{
    [Theory]
    [InlineData("platform.knowledge.publish", true)]
    [InlineData("PLATFORM.anything", true)]
    [InlineData(" pharmacy.knowledge.review ", true)]
    [InlineData("pharmacy.knowledge.verify-source", true)]
    [InlineData("pharmacy.knowledge.approve", false)]
    [InlineData("pharmacy.consultation.pharmacist", false)]
    [InlineData("platform_admin.read", false)]
    [InlineData("reports.read", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Reserved_codes_are_the_platform_operator_set(string? code, bool reserved) =>
        Assert.Equal(reserved, ReservedPermissions.IsReserved(code));

    [Fact]
    public void Pharmacy_platform_permissions_are_reserved()
    {
        Assert.True(ReservedPermissions.IsReserved(PharmacyKnowledgeApprovalAccess.PlatformPublishPermission));
        Assert.True(ReservedPermissions.IsReserved(PharmacyKnowledgeApprovalAccess.ReviewPermission));
        Assert.True(ReservedPermissions.IsReserved(PharmacyKnowledgeApprovalAccess.VerifySourcePermission));
    }

    [Fact]
    public void New_tenant_admin_copy_excludes_reserved_codes()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "KitPlatform.Infrastructure", "Platform", "PlatformTenantRepository.cs"));
        var inserts = source.Split("INSERT INTO role_permissions").Skip(1).ToList();
        Assert.NotEmpty(inserts);
        Assert.All(inserts, insert =>
        {
            var statement = insert[..insert.IndexOf("\"\"\"", StringComparison.Ordinal)];
            Assert.True(
                statement.Contains("ReservedPermissions.TenantAssignableSql", StringComparison.Ordinal)
                || statement.Contains("permission_code LIKE", StringComparison.Ordinal),
                "Every tenant bootstrap grant must filter permission codes.");
        });
    }

    [Fact]
    public async Task Tenant_admin_cannot_add_remove_or_see_reserved_permissions()
    {
        var db = new NpgsqlConnectionFactory(IsolatedDatabase.RequireConnection());
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var reservedCode = "platform.probe." + Guid.NewGuid().ToString("N")[..12];
        var otherReserved = "platform.probe." + Guid.NewGuid().ToString("N")[..12];
        await using var conn = await db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name, business_vertical)
            VALUES (@TenantId, @Code, 'Reserved permission probe', 'pharmacy');
            INSERT INTO roles (id, tenant_id, role_code, role_name, status)
            VALUES (@RoleId, @TenantId, 'ADMIN', 'Admin', 1);
            INSERT INTO permissions (permission_code, permission_name, module_name)
            VALUES (@Reserved, 'probe', 'probe'), (@OtherReserved, 'probe', 'probe');
            """,
            new
            {
                TenantId = tenantId,
                RoleId = roleId,
                Code = "RPX" + tenantId.ToString("N")[..12],
                Reserved = reservedCode,
                OtherReserved = otherReserved,
            });
        try
        {
            // Platform operator granted one reserved code directly.
            await conn.ExecuteAsync(
                """
                INSERT INTO role_permissions (role_id, permission_id)
                SELECT @RoleId, id FROM permissions WHERE permission_code = @Reserved
                """,
                new { RoleId = roleId, Reserved = reservedCode });

            var tenant = new FixedTenant(tenantId);
            var repo = new IdentityAdminRepository(db, tenant);
            var service = new IdentityAdminService(repo, null!, null!, tenant);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateRolePermissionsAsync(
                roleId, new UpdateRolePermissionsRequest(["sales.read", otherReserved])));

            var updated = await service.UpdateRolePermissionsAsync(
                roleId, new UpdateRolePermissionsRequest(["sales.read"]));
            Assert.NotNull(updated);
            Assert.Contains("sales.read", updated!.PermissionCodes);
            Assert.Contains(reservedCode, updated.PermissionCodes);
            Assert.DoesNotContain(otherReserved, updated.PermissionCodes);

            Assert.True(await repo.UpdateRolePermissionsAsync(roleId, [otherReserved], default));
            var afterRepo = await repo.GetRoleAsync(roleId, default);
            Assert.Contains(reservedCode, afterRepo!.PermissionCodes);
            Assert.DoesNotContain(otherReserved, afterRepo.PermissionCodes);
            Assert.DoesNotContain("sales.read", afterRepo.PermissionCodes);

            var picker = await repo.ListPermissionsAsync(default);
            Assert.DoesNotContain(picker, p => ReservedPermissions.IsReserved(p.PermissionCode));
            Assert.Contains(picker, p => p.PermissionCode == "sales.read");
        }
        finally
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM role_permissions WHERE role_id = @RoleId;
                DELETE FROM roles WHERE id = @RoleId;
                DELETE FROM permissions WHERE permission_code IN (@Reserved, @OtherReserved);
                DELETE FROM tenants WHERE id = @TenantId AND tenant_code LIKE 'RPX%';
                """,
                new { RoleId = roleId, TenantId = tenantId, Reserved = reservedCode, OtherReserved = otherReserved });
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
