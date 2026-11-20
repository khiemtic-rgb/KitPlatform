using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Explicit credential identity. ProviderId and ModelId do not select this.
/// </summary>
public sealed record ProductionCredentialReference(Guid CredentialId, Guid CredentialVersionId);

/// <summary>
/// Secret material for one authorized provider call. ToString does not include the secret.
/// </summary>
public sealed class ProductionCredentialMaterial
{
    public Guid CredentialId { get; }
    public Guid CredentialVersionId { get; }
    public string ProviderId { get; }
    public string ModelId { get; }
    readonly string _secret;

    public ProductionCredentialMaterial(
        Guid credentialId, Guid credentialVersionId, string providerId, string modelId, string secret)
    {
        CredentialId = credentialId;
        CredentialVersionId = credentialVersionId;
        ProviderId = providerId;
        ModelId = modelId;
        _secret = secret;
    }

    public string Secret => _secret;

    public override string ToString() =>
        FormattableString.Invariant($"ProductionCredentialMaterial {CredentialId:N} {CredentialVersionId:N}");
}

public interface IProductionCredentialResolver
{
    Task<ProductionCredentialMaterial> ResolveAsync(
        Guid credentialId,
        Guid credentialVersionId,
        string providerId,
        string modelId,
        CancellationToken ct = default);

    Task<ProductionCredentialMaterial> ResolveActiveAsync(
        Guid tenantId,
        string providerId,
        string modelId,
        CancellationToken ct = default)
        => throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);

    Task<ProductionCredentialMaterial> ResolveActiveAsync(
        string providerId,
        string modelId,
        CancellationToken ct = default)
        => throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
}

/// <summary>
/// Gate C. Caller tenant must own the credential. The version id is explicit.
/// There is no latest-version rule and no legacy settings fallback.
/// </summary>
public sealed class ProductionCredentialResolver : IProductionCredentialResolver
{
    public const string Unresolved = "PRODUCTION_CREDENTIAL_UNRESOLVED";
    public const string Ambiguous = "PRODUCTION_CREDENTIAL_AMBIGUOUS";
    public const string Revoked = "PRODUCTION_CREDENTIAL_REVOKED";
    public const string ProviderMismatch = "PRODUCTION_CREDENTIAL_PROVIDER_MISMATCH";
    public const string ModelMismatch = "PRODUCTION_CREDENTIAL_MODEL_MISMATCH";

    readonly IProductionCredentialStore _identities;
    readonly IProductionCredentialSecretStore _secrets;
    readonly IProductionCallerTenant _caller;

    public ProductionCredentialResolver(
        IProductionCredentialStore identities,
        IProductionCredentialSecretStore secrets,
        IProductionCallerTenant caller)
    {
        _identities = identities;
        _secrets = secrets;
        _caller = caller;
    }

    public async Task<ProductionCredentialMaterial> ResolveAsync(
        Guid credentialId,
        Guid credentialVersionId,
        string providerId,
        string modelId,
        CancellationToken ct = default)
    {
        _caller.RequireTenant();
        if (credentialId == Guid.Empty || credentialVersionId == Guid.Empty)
            throw new InvalidOperationException(Unresolved);
        var requiredProvider = (providerId ?? "").Trim();
        var requiredModel = (modelId ?? "").Trim();
        if (requiredProvider.Length == 0 || requiredModel.Length == 0)
            throw new InvalidOperationException(Unresolved);

        var credential = await _identities.GetAsync(credentialId, ct);
        if (credential is null)
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
        if (!string.Equals(credential.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException(Revoked);

        var version = await _identities.GetVersionAsync(credentialVersionId, ct);
        if (version is null || version.CredentialId != credential.CredentialId)
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
        if (string.Equals(version.Status, ProductionCredentialStatuses.Revoked, StringComparison.Ordinal))
            throw new InvalidOperationException(Revoked);
        if (!string.Equals(version.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal)
            && !string.Equals(version.Status, ProductionCredentialStatuses.Superseded, StringComparison.Ordinal))
            throw new InvalidOperationException(Unresolved);
        if (!string.Equals(version.ProviderId, requiredProvider, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProviderMismatch);
        if (!string.Equals(version.ModelId, requiredModel, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ModelMismatch);

        var secret = await _secrets.OpenAsync(version.CredentialVersionId, ct);
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
        return new ProductionCredentialMaterial(
            credential.CredentialId, version.CredentialVersionId, requiredProvider, requiredModel, secret);
    }

    public Task<ProductionCredentialMaterial> ResolveActiveAsync(
        string providerId, string modelId, CancellationToken ct = default)
        => ResolveActiveAsync(_caller.RequireTenant(), providerId, modelId, ct);

    public async Task<ProductionCredentialMaterial> ResolveActiveAsync(
        Guid tenantId, string providerId, string modelId, CancellationToken ct = default)
    {
        var caller = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(caller, tenantId);
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        if (provider.Length == 0 || model.Length == 0)
            throw new InvalidOperationException(Unresolved);
        var matches = await _identities.ListEligibleAsync(provider, model, ct);
        if (matches.Count == 0)
            throw new InvalidOperationException(Unresolved);
        if (matches.Count != 1)
            throw new InvalidOperationException(Ambiguous);
        var only = matches[0];
        return await ResolveAsync(only.CredentialId, only.CredentialVersionId, provider, model, ct);
    }

    public static async Task<ProductionCredentialMaterial> RequireAsync(
        IProductionCredentialResolver resolver,
        ProductionCredentialReference? reference,
        string providerId,
        string modelId,
        CancellationToken ct = default)
    {
        if (resolver is null || reference is null
            || reference.CredentialId == Guid.Empty
            || reference.CredentialVersionId == Guid.Empty)
            throw new InvalidOperationException(Unresolved);
        return await resolver.ResolveAsync(
            reference.CredentialId, reference.CredentialVersionId, providerId, modelId, ct);
    }
}

/// <summary>
/// Non-secret credential reference pinned on a frozen execution. Not a secret and not a latest-version rule.
/// </summary>
public static class ProductionFrozenCredential
{
    public static bool RequiresPin(string? providerId)
    {
        var id = (providerId ?? "").Trim();
        return id.Equals(FamixaProviderIds.Runway, StringComparison.OrdinalIgnoreCase)
            || id.Equals(FamixaProviderIds.Fal, StringComparison.OrdinalIgnoreCase)
            || id.Equals(FamixaProviderIds.ElevenLabs, StringComparison.OrdinalIgnoreCase)
            || id.Equals(FamixaProviderIds.Google, StringComparison.OrdinalIgnoreCase);
    }

    public static ProductionCredentialReference? FromSnapshot(ProductionTaskInputSnapshot snapshot)
    {
        if (snapshot.CredentialId is not Guid credentialId
            || snapshot.CredentialVersionId is not Guid credentialVersionId
            || credentialId == Guid.Empty
            || credentialVersionId == Guid.Empty)
            return null;
        return new ProductionCredentialReference(credentialId, credentialVersionId);
    }
}
