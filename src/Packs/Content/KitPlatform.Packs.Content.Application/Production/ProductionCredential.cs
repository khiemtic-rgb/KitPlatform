namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// ADR-04 Gate A. Tenant owns the credential. CredentialId is not ProviderId,
/// ModelId, Workspace, Product Pack, or a secret.
/// </summary>
public sealed record ProductionCredential(
    Guid CredentialId,
    Guid TenantId,
    string DisplayName,
    string Status);

/// <summary>
/// Version identity under one credential. TenantId is the owning credential's tenant,
/// constrained to that credential. It is not a second owner and not a workspace.
/// ProviderId and ModelId are capability metadata.
/// </summary>
public sealed record ProductionCredentialVersion(
    Guid CredentialVersionId,
    Guid CredentialId,
    Guid TenantId,
    int Version,
    string? ProviderId,
    string? ModelId,
    string Status);

public static class ProductionCredentialStatuses
{
    public const string Active = "ACTIVE";
    public const string Superseded = "SUPERSEDED";
    public const string Revoked = "REVOKED";
}

public static class ProductionCredentialOwnership
{
    public const string IdRequired = "PRODUCTION_CREDENTIAL_ID_REQUIRED";
    public const string NameRequired = "PRODUCTION_CREDENTIAL_NAME_REQUIRED";
    public const string SecretForbidden = "PRODUCTION_CREDENTIAL_SECRET_FORBIDDEN";
    public const string IdentityExists = "PRODUCTION_CREDENTIAL_IDENTITY_EXISTS";
    public const string VersionIdRequired = "PRODUCTION_CREDENTIAL_VERSION_ID_REQUIRED";
    public const string VersionCredentialRequired = "PRODUCTION_CREDENTIAL_VERSION_CREDENTIAL_REQUIRED";
    public const string VersionNumberRequired = "PRODUCTION_CREDENTIAL_VERSION_NUMBER_REQUIRED";
    public const string VersionExists = "PRODUCTION_CREDENTIAL_VERSION_EXISTS";
    public const string VersionOwnerImmutable = "PRODUCTION_CREDENTIAL_VERSION_OWNER_IMMUTABLE";

    public static ProductionCredential Create(Guid credentialId, Guid tenantId, string displayName)
    {
        if (credentialId == Guid.Empty)
            throw new InvalidOperationException(IdRequired);
        var tenant = ProductionTenantBoundary.Require(tenantId);
        var name = RequireLabel(displayName);
        return new ProductionCredential(credentialId, tenant, name, ProductionCredentialStatuses.Active);
    }

    public static ProductionCredentialVersion CreateVersion(
        Guid credentialVersionId,
        Guid credentialId,
        Guid tenantId,
        int version,
        string? providerId,
        string? modelId)
    {
        if (credentialVersionId == Guid.Empty)
            throw new InvalidOperationException(VersionIdRequired);
        if (credentialId == Guid.Empty)
            throw new InvalidOperationException(VersionCredentialRequired);
        if (version < 1)
            throw new InvalidOperationException(VersionNumberRequired);
        var tenant = ProductionTenantBoundary.Require(tenantId);
        return new ProductionCredentialVersion(
            credentialVersionId,
            credentialId,
            tenant,
            version,
            RequireCapability(providerId),
            RequireCapability(modelId),
            ProductionCredentialStatuses.Active);
    }

    public static void RequireOwned(Guid callerTenantId, Guid ownerTenantId)
    {
        ProductionTenantBoundary.Require(callerTenantId);
        ProductionTenantBoundary.Require(ownerTenantId);
        if (callerTenantId != ownerTenantId)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    public static void RejectOwnerChange(Guid currentTenantId, Guid proposedTenantId)
    {
        ProductionTenantBoundary.Require(currentTenantId);
        if (proposedTenantId != currentTenantId)
            throw new InvalidOperationException(ProductionTenantBoundary.Immutable);
    }

    public static void RejectVersionReassignment(Guid currentCredentialId, Guid proposedCredentialId)
    {
        if (currentCredentialId == Guid.Empty || proposedCredentialId == Guid.Empty)
            throw new InvalidOperationException(VersionCredentialRequired);
        if (currentCredentialId != proposedCredentialId)
            throw new InvalidOperationException(VersionOwnerImmutable);
    }

    static string RequireLabel(string? value)
    {
        var name = (value ?? "").Trim();
        if (name.Length == 0 || name.Length > 200)
            throw new InvalidOperationException(NameRequired);
        RejectSecretMaterial(name);
        return name;
    }

    static string? RequireCapability(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        RejectSecretMaterial(text);
        return text;
    }

    public static void RejectSecretMaterial(string text)
    {
        if (text.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
            || text.Contains("BEGIN ", StringComparison.OrdinalIgnoreCase)
            || text.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(SecretForbidden);
    }
}

/// <summary>
/// One ACTIVE version the caller tenant can pin. No secret field.
/// </summary>
public sealed record ProductionCredentialActiveMatch(Guid CredentialId, Guid CredentialVersionId);

/// <summary>
/// Metadata for the credential center. No secret, ciphertext, nonce, or tag.
/// </summary>
public sealed record ProductionCredentialCatalogEntry(
    Guid CredentialId,
    Guid CredentialVersionId,
    string DisplayName,
    string IdentityStatus,
    int Version,
    string ProviderId,
    string ModelId,
    string Status,
    DateTimeOffset CreatedAt);

public interface IProductionCredentialStore
{
    Task<ProductionCredential> CreateAsync(ProductionCredential credential, CancellationToken ct = default);
    Task<ProductionCredential?> GetAsync(Guid credentialId, CancellationToken ct = default);
    Task<ProductionCredentialVersion> AddVersionAsync(ProductionCredentialVersion version, CancellationToken ct = default);
    Task<ProductionCredentialVersion?> GetVersionAsync(Guid credentialVersionId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionCredentialActiveMatch>> ListEligibleAsync(
        string providerId, string modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionCredentialCatalogEntry>> ListCatalogAsync(CancellationToken ct = default);
}

/// <summary>
/// Gate A ownership store. No secret field. No workspace. No product pack.
/// Reads are limited to the caller tenant from <see cref="IProductionCallerTenant"/>.
/// </summary>
public sealed class InMemoryProductionCredentialStore : IProductionCredentialStore
{
    readonly IProductionCallerTenant _caller;
    readonly Dictionary<Guid, ProductionCredential> _credentials = new();
    readonly Dictionary<Guid, ProductionCredentialVersion> _versions = new();
    readonly Dictionary<Guid, DateTimeOffset> _versionCreated = new();

    public InMemoryProductionCredentialStore(IProductionCallerTenant caller) => _caller = caller;

    public Task<ProductionCredential> CreateAsync(ProductionCredential credential, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(caller, credential.TenantId);
        if (_credentials.TryGetValue(credential.CredentialId, out var existing))
        {
            ProductionCredentialOwnership.RejectOwnerChange(existing.TenantId, credential.TenantId);
            throw new InvalidOperationException(ProductionCredentialOwnership.IdentityExists);
        }

        _credentials.Add(credential.CredentialId, credential);
        return Task.FromResult(credential);
    }

    public Task<ProductionCredential?> GetAsync(Guid credentialId, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        if (!_credentials.TryGetValue(credentialId, out var credential))
            return Task.FromResult<ProductionCredential?>(null);
        if (credential.TenantId != caller)
            return Task.FromResult<ProductionCredential?>(null);
        return Task.FromResult<ProductionCredential?>(credential);
    }

    public Task<ProductionCredentialVersion> AddVersionAsync(ProductionCredentialVersion version, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(caller, version.TenantId);
        if (!_credentials.TryGetValue(version.CredentialId, out var credential) || credential.TenantId != caller)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionCredentialRequired);
        if (credential.TenantId != version.TenantId)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);

        if (_versions.TryGetValue(version.CredentialVersionId, out var existing))
        {
            ProductionCredentialOwnership.RejectVersionReassignment(existing.CredentialId, version.CredentialId);
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionExists);
        }

        if (_versions.Values.Any(row => row.CredentialId == version.CredentialId && row.Version == version.Version))
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionExists);

        _versions.Add(version.CredentialVersionId, version);
        _versionCreated[version.CredentialVersionId] = DateTimeOffset.UtcNow;
        return Task.FromResult(version);
    }

    public Task<IReadOnlyList<ProductionCredentialActiveMatch>> ListEligibleAsync(
        string providerId, string modelId, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        var matches = new List<ProductionCredentialActiveMatch>();
        foreach (var version in _versions.Values.OrderBy(row => row.CredentialVersionId))
        {
            if (version.TenantId != caller)
                continue;
            if (!string.Equals(version.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
                continue;
            if (!string.Equals(version.ProviderId, provider, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(version.ModelId, model, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!_credentials.TryGetValue(version.CredentialId, out var credential)
                || credential.TenantId != caller
                || !string.Equals(credential.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
                continue;
            matches.Add(new ProductionCredentialActiveMatch(version.CredentialId, version.CredentialVersionId));
        }
        return Task.FromResult<IReadOnlyList<ProductionCredentialActiveMatch>>(matches);
    }

    public Task<IReadOnlyList<ProductionCredentialCatalogEntry>> ListCatalogAsync(CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        var rows = new List<ProductionCredentialCatalogEntry>();
        foreach (var version in _versions.Values.OrderBy(row => row.CredentialVersionId))
        {
            if (version.TenantId != caller)
                continue;
            if (!_credentials.TryGetValue(version.CredentialId, out var credential) || credential.TenantId != caller)
                continue;
            rows.Add(new ProductionCredentialCatalogEntry(
                credential.CredentialId,
                version.CredentialVersionId,
                credential.DisplayName,
                credential.Status,
                version.Version,
                version.ProviderId ?? "",
                version.ModelId ?? "",
                version.Status,
                _versionCreated.TryGetValue(version.CredentialVersionId, out var created)
                    ? created
                    : DateTimeOffset.UnixEpoch));
        }
        return Task.FromResult<IReadOnlyList<ProductionCredentialCatalogEntry>>(rows);
    }

    public int AllocateVersionNumber(Guid credentialId)
    {
        var caller = _caller.RequireTenant();
        var next = 0;
        foreach (var row in _versions.Values)
        {
            if (row.CredentialId == credentialId && row.TenantId == caller && row.Version > next)
                next = row.Version;
        }
        return next + 1;
    }

    public void DiscardVersion(Guid credentialVersionId)
    {
        _versions.Remove(credentialVersionId);
        _versionCreated.Remove(credentialVersionId);
    }

    public void DiscardCredential(Guid credentialId)
    {
        _credentials.Remove(credentialId);
        foreach (var id in _versions.Where(pair => pair.Value.CredentialId == credentialId).Select(pair => pair.Key).ToList())
        {
            _versions.Remove(id);
            _versionCreated.Remove(id);
        }
    }

    public int CredentialCount => _credentials.Count;

    public int VersionCount => _versions.Count;

    public IReadOnlyList<Guid> SupersedeActiveVersions(Guid credentialId)
    {
        var caller = _caller.RequireTenant();
        var superseded = new List<Guid>();
        foreach (var version in _versions.Values.ToList())
        {
            if (version.CredentialId != credentialId || version.TenantId != caller)
                continue;
            if (!string.Equals(version.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
                continue;
            superseded.Add(version.CredentialVersionId);
            _versions[version.CredentialVersionId] = version with { Status = ProductionCredentialStatuses.Superseded };
        }
        return superseded;
    }

    public void RestoreActiveVersions(IReadOnlyList<Guid> versionIds)
    {
        foreach (var id in versionIds)
        {
            if (_versions.TryGetValue(id, out var version))
                _versions[id] = version with { Status = ProductionCredentialStatuses.Active };
        }
    }

    public Task<ProductionCredentialVersion> RevokeVersionAsync(Guid credentialVersionId, CancellationToken ct = default)
    {
        var version = GetVersionAsync(credentialVersionId, ct).GetAwaiter().GetResult();
        if (version is null)
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
        if (string.Equals(version.Status, ProductionCredentialStatuses.Revoked, StringComparison.Ordinal))
            return Task.FromResult(version);
        var revoked = version with { Status = ProductionCredentialStatuses.Revoked };
        _versions[credentialVersionId] = revoked;
        return Task.FromResult(revoked);
    }

    public Task<ProductionCredentialVersion?> GetVersionAsync(Guid credentialVersionId, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        if (!_versions.TryGetValue(credentialVersionId, out var version) || version.TenantId != caller)
            return Task.FromResult<ProductionCredentialVersion?>(null);
        return Task.FromResult<ProductionCredentialVersion?>(version);
    }
}
