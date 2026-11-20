namespace KitPlatform.Packs.Content.Production;

public interface IProductionArtifactFileStore
{
    string Write(string folder, string fileId, byte[] bytes);
    byte[]? Read(string storageKey);
}

public sealed class InMemoryProductionArtifactFileStore : IProductionArtifactFileStore
{
    readonly IProductionCallerTenant? _caller;
    readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryProductionArtifactFileStore(IProductionCallerTenant? caller = null) => _caller = caller;

    public string Write(string folder, string fileId, byte[] bytes)
    {
        var key = StorageKey(folder, fileId);
        if (_files.TryGetValue(key, out var existing))
        {
            ProductionArtifactPhysicalWrite.RejectReplacement(existing, bytes);
            return key;
        }

        _files[key] = (byte[])bytes.Clone();
        return key;
    }

    /// <summary>
    /// Out-of-band disk tamper for integrity tests. Not an official Production write.
    /// </summary>
    public void PlantExternalBytes(string storageKey, byte[] bytes) =>
        _files[storageKey] = (byte[])bytes.Clone();

    string StorageKey(string folder, string fileId)
    {
        if (_caller is null)
            return $"{folder}/{fileId}";
        var tenant = _caller.RequireTenant();
        var scoped = ProductionTenantBoundary.Prefix(tenant) + "/" + (folder ?? "").Trim().Trim('/') + "/" + fileId;
        if (!ProductionTenantBoundary.OwnsStorageKey(tenant, scoped))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        return scoped;
    }

    public byte[]? Read(string storageKey)
    {
        if (_caller is not null && !ProductionTenantBoundary.OwnsStorageKey(_caller.RequireTenant(), storageKey))
            return null;
        return _files.TryGetValue(storageKey, out var b) ? b : null;
    }
}
