using System.Security.Claims;

namespace KitPlatform.Application.Core;

/// <summary>
/// Boundary over the existing Identity, Tenant, permission, and module authorities.
/// It does not create a store. User rows stay in <c>public.users</c>. Tenant rows stay in <c>public.tenants</c>.
/// The caller tenant is the JWT <c>tenant_id</c> claim. Workspace is not part of this check.
/// </summary>
public static class CoreAuthority
{
    public const string TenantIdClaim = "tenant_id";
    public const string SubjectClaim = "sub";
    public const string PermissionClaim = "permission";

    public static bool WorkspaceRequiredForTenantAuthorization => false;

    public static Guid RequireUserId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal?.FindFirst(SubjectClaim)?.Value;
        if (!Guid.TryParse(value, out var userId))
            throw new UnauthorizedAccessException("User id claim missing.");
        return userId;
    }

    public static Guid RequireTenantId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(TenantIdClaim)?.Value;
        if (!Guid.TryParse(value, out var tenantId))
            throw new UnauthorizedAccessException("Tenant id claim missing.");
        return tenantId;
    }

    public static bool TryResolveCaller(ClaimsPrincipal? principal, out CoreCaller caller)
    {
        caller = default;
        var userValue = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal?.FindFirst(SubjectClaim)?.Value;
        var tenantValue = principal?.FindFirst(TenantIdClaim)?.Value;
        if (!Guid.TryParse(userValue, out var userId) || !Guid.TryParse(tenantValue, out var tenantId))
            return false;

        caller = new CoreCaller(userId, tenantId);
        return true;
    }

    public static bool AllowsSameTenant(Guid callerTenantId, Guid resourceTenantId) =>
        callerTenantId != Guid.Empty && callerTenantId == resourceTenantId;

    public static void RejectTenantSwitch(Guid boundTenantId, Guid requestedTenantId)
    {
        if (!AllowsSameTenant(boundTenantId, requestedTenantId))
            throw new UnauthorizedAccessException("Tenant context cannot switch tenant.");
    }

    public static bool HasPermission(ClaimsPrincipal? principal, string permissionCode)
    {
        if (principal is null || string.IsNullOrWhiteSpace(permissionCode))
            return false;

        return principal.Claims.Any(claim =>
            claim.Type == PermissionClaim && claim.Value == permissionCode);
    }

    public static bool IsModuleEnabled(IEnumerable<string>? enabledModules, string? moduleCode)
    {
        if (enabledModules is null || string.IsNullOrWhiteSpace(moduleCode))
            return false;

        return enabledModules.Contains(moduleCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}

public readonly record struct CoreCaller(Guid UserId, Guid TenantId);
