using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Runtime tenant is the authenticated <see cref="ITenantContext"/> claim.
/// Request body, build id, and workspace are not consulted.
/// </summary>
internal sealed class HttpProductionCallerTenant : IProductionCallerTenant
{
    readonly ITenantContext _tenant;

    public HttpProductionCallerTenant(ITenantContext tenant) => _tenant = tenant;

    public Guid RequireTenant()
    {
        if (!_tenant.IsAuthenticated)
            throw new UnauthorizedAccessException(ProductionTenantBoundary.Required);
        return ProductionTenantBoundary.Require(_tenant.TenantId);
    }
}

internal sealed class ProductionMissingCallerTenant : IProductionCallerTenant
{
    public static readonly ProductionMissingCallerTenant Instance = new();

    public Guid RequireTenant() =>
        throw new UnauthorizedAccessException(ProductionTenantBoundary.Required);
}
