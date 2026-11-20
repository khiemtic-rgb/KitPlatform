namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// ADR-04 Gate E. Creates a new credential version under an existing tenant-owned credential.
/// The new version number is insert identity only. It is not an execution selection rule.
/// </summary>
public interface IProductionCredentialRotation
{
    Task<ProductionCredentialVersion> RotateAsync(
        Guid credentialId,
        Guid credentialVersionId,
        string providerId,
        string modelId,
        string secret,
        CancellationToken ct = default);

    Task<ProductionCredentialVersion> RevokeVersionAsync(
        Guid credentialVersionId,
        CancellationToken ct = default);
}

public sealed class InMemoryProductionCredentialRotation : IProductionCredentialRotation
{
    readonly InMemoryProductionCredentialStore _identities;
    readonly InMemoryProductionCredentialSecretStore _secrets;
    readonly IProductionCredentialKeyRing _keys;

    public InMemoryProductionCredentialRotation(
        InMemoryProductionCredentialStore identities,
        InMemoryProductionCredentialSecretStore secrets,
        IProductionCredentialKeyRing keys)
    {
        _identities = identities;
        _secrets = secrets;
        _keys = keys;
    }

    public async Task<ProductionCredentialVersion> RotateAsync(
        Guid credentialId,
        Guid credentialVersionId,
        string providerId,
        string modelId,
        string secret,
        CancellationToken ct = default)
    {
        var credential = await RequireActiveCredential(credentialId, ct);
        if (credentialVersionId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionIdRequired);
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length == 0 || model.Length == 0)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);

        var envelope = ProductionCredentialSecretProtector.Encrypt(secret, _keys.RequireCurrent());
        var version = ProductionCredentialOwnership.CreateVersion(
            credentialVersionId,
            credential.CredentialId,
            credential.TenantId,
            _identities.AllocateVersionNumber(credential.CredentialId),
            provider,
            model);

        var superseded = _identities.SupersedeActiveVersions(credential.CredentialId);
        ProductionCredentialVersion stored;
        var inserted = false;
        try
        {
            stored = await _identities.AddVersionAsync(version, ct);
            inserted = true;
            _secrets.RetainPrepared(stored.CredentialVersionId, envelope);
        }
        catch
        {
            if (inserted)
                _identities.DiscardVersion(version.CredentialVersionId);
            _secrets.DiscardPrepared(version.CredentialVersionId);
            _identities.RestoreActiveVersions(superseded);
            throw;
        }

        return stored;
    }

    public Task<ProductionCredentialVersion> RevokeVersionAsync(
        Guid credentialVersionId, CancellationToken ct = default)
        => _identities.RevokeVersionAsync(credentialVersionId, ct);

    async Task<ProductionCredential> RequireActiveCredential(Guid credentialId, CancellationToken ct)
    {
        var credential = await _identities.GetAsync(credentialId, ct);
        if (credential is null)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionCredentialRequired);
        if (!string.Equals(credential.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionCredentialResolver.Revoked);
        return credential;
    }
}
