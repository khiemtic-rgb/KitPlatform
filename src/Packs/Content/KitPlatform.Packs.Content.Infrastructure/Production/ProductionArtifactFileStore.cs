using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionArtifactFileStore : IProductionArtifactFileStore
{
    readonly KitVideoVideoStore _videos;
    readonly IProductionCallerTenant _caller;

    public ProductionArtifactFileStore(KitVideoVideoStore videos, IProductionCallerTenant? caller = null)
    {
        _videos = videos;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    public string Write(string folder, string fileId, byte[] bytes)
    {
        var tenant = _caller.RequireTenant();
        var scoped = ProductionTenantBoundary.Prefix(tenant) + "/" + (folder ?? "").Trim().Trim('/');
        _videos.PersistNamed(scoped, fileId, bytes);
        return $"{scoped}/{fileId}";
    }

    public byte[]? Read(string storageKey)
    {
        var tenant = _caller.RequireTenant();
        if (!ProductionTenantBoundary.OwnsStorageKey(tenant, storageKey))
            return null;
        var raw = (storageKey ?? "").Trim().Replace('\\', '/');
        var i = raw.LastIndexOf('/');
        if (i <= 0 || i >= raw.Length - 1) return null;
        return _videos.TryReadNamed(raw[..i], raw[(i + 1)..]);
    }
}
