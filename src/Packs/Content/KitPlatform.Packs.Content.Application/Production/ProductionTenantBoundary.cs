namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-TENANT-BOUNDARY-01. Caller tenant comes from ITenantContext via
/// <see cref="IProductionCallerTenant"/>. BuildId, artifact id, and StorageKey are not authority.
/// </summary>
public static class ProductionTenantBoundary
{
    public const string Required = "PRODUCTION_TENANT_REQUIRED";
    public const string Forbidden = "PRODUCTION_TENANT_FORBIDDEN";
    public const string Unattributed = "PRODUCTION_TENANT_UNATTRIBUTED";
    public const string Immutable = "PRODUCTION_TENANT_IMMUTABLE";

    public static Guid Require(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(Required);
        return tenantId;
    }

    public static string Prefix(Guid tenantId) => Require(tenantId).ToString("N");

    public static bool OwnsStorageKey(Guid tenantId, string? storageKey)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(storageKey))
            return false;
        var normalized = storageKey.Trim().Replace('\\', '/');
        return normalized.StartsWith(Prefix(tenantId) + "/", StringComparison.OrdinalIgnoreCase);
    }
}

public static class ProductionBuildTenantGate
{
    public static void Require(Guid caller, Guid? buildTenant)
    {
        ProductionTenantBoundary.Require(caller);
        if (buildTenant is null || buildTenant.Value == Guid.Empty)
            throw new InvalidOperationException(ProductionTenantBoundary.Unattributed);
        if (buildTenant.Value != caller)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }
}

public interface IProductionCallerTenant
{
    Guid RequireTenant();
}
