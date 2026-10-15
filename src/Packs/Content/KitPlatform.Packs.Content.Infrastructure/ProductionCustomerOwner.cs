using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ProductionCustomerOwner
{
    public static Guid Require(ITenantContext tenant)
    {
        if (!tenant.IsAuthenticated)
            throw new UnauthorizedAccessException("Tenant id claim missing.");
        var id = tenant.TenantId;
        if (id == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant id claim missing.");
        return id;
    }
}

/// <summary>
/// Used only when the host has not registered a request tenant. Production reads and writes fail closed.
/// </summary>
internal sealed class UnauthenticatedTenantContext : ITenantContext
{
    public bool IsAuthenticated => false;
    public Guid? WorkspaceId => null;
    public Guid TenantId => throw new UnauthorizedAccessException("Tenant id claim missing.");
    public Guid UserId => throw new UnauthorizedAccessException("User id claim missing.");
}
