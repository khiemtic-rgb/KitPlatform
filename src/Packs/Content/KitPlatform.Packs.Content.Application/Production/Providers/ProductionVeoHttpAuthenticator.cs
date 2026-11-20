using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Official google/veo-3.1 authenticator.
/// <see cref="GetAccessTokenAsync"/> stays closed: there is no unscoped token.
/// <see cref="AuthorizeFrozenAsync"/> mints a token only for one already-resolved
/// tenant credential whose secret is a service account. A missing token client
/// still fails as <see cref="ProductionFailureReasons.VertexExecutionClosed"/>
/// and does not call a provider. Revoked ciphertext is never opened here.
/// </summary>
public sealed class ProductionVeoHttpAuthenticator : IVeoHttpAuthenticator
{
    readonly VeoHttpCredentialContract? _contract;
    readonly IVeoJwtBearerTokenClient? _tokens;

    public ProductionVeoHttpAuthenticator(
        VeoHttpCredentialContract? contract = null,
        IVeoJwtBearerTokenClient? tokens = null)
    {
        _contract = contract;
        _tokens = tokens;
    }

    public VeoHttpCredentialContract? Contract => _contract;
    public int GetAccessTokenCalls { get; private set; }
    public bool TokenExchangeConfigured => _tokens is not null;
    public Guid? AcceptedCredentialId { get; private set; }
    public Guid? AcceptedCredentialVersionId { get; private set; }
    public string? AcceptedProjectId { get; private set; }

    public Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        GetAccessTokenCalls++;
        throw new InvalidOperationException(ProductionFailureReasons.VertexExecutionClosed);
    }

    public async Task<string> AuthorizeFrozenAsync(ProductionCredentialMaterial material, CancellationToken ct = default)
    {
        if (material.CredentialId == Guid.Empty
            || material.CredentialVersionId == Guid.Empty
            || string.IsNullOrWhiteSpace(material.Secret))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        if (!string.Equals(material.ProviderId, FamixaProviderIds.Google, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionCredentialResolver.ProviderMismatch);
        if (!string.Equals(material.ModelId, ProductionMotionCatalog.Veo31ModelId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionCredentialResolver.ModelMismatch);

        var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(material.Secret);
        if (!parsed.Ok || parsed.Contract is null)
            throw new InvalidOperationException(parsed.Code ?? ProductionFailureReasons.InvalidProviderResponse);
        if (_contract is not null
            && (!string.Equals(_contract.ProjectId, parsed.Contract.ProjectId, StringComparison.Ordinal)
                || !string.Equals(_contract.ClientEmail, parsed.Contract.ClientEmail, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(VeoFrozenCredentialCodes.ContractMismatch);

        var assertion = VeoJwtBearer.Sign(
            parsed.Contract.ClientEmail,
            material.Secret,
            parsed.Contract.TokenUri,
            DateTimeOffset.UtcNow);

        AcceptedCredentialId = material.CredentialId;
        AcceptedCredentialVersionId = material.CredentialVersionId;
        AcceptedProjectId = parsed.Contract.ProjectId;
        if (_tokens is null)
        {
            GetAccessTokenCalls++;
            throw new InvalidOperationException(ProductionFailureReasons.VertexExecutionClosed);
        }

        GetAccessTokenCalls++;
        var token = await _tokens.ExchangeAsync(parsed.Contract.TokenUri, assertion, ct);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);
        return token.Trim();
    }
}
