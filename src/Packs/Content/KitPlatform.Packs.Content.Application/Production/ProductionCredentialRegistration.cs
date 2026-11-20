namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Official registration of a tenant-owned production credential.
/// Identity comes from <see cref="IProductionCredentialStore"/>.
/// The secret enters only through <see cref="IProductionCredentialRotation"/>.
/// </summary>
public interface IProductionCredentialRegistration
{
    Task<ProductionCredentialRegistrationResult> RegisterAsync(
        RegisterProductionCredentialCommand command, CancellationToken ct = default);

    Task<ProductionCredentialRegistrationResult> AddVersionAsync(
        AddProductionCredentialVersionCommand command, CancellationToken ct = default);

    Task<ProductionCredentialRegistrationResult> RotateActiveAsync(
        RotateProductionCredentialCommand command, CancellationToken ct = default);

    Task<ProductionCredentialRegistrationResult> RevokeVersionAsync(
        Guid credentialVersionId, CancellationToken ct = default);

    Task<IReadOnlyList<ProductionCredentialCatalogEntry>> ListAsync(CancellationToken ct = default);
}

public sealed record RotateProductionCredentialCommand(
    string ProviderId,
    string ModelId,
    string Secret);

public sealed record RegisterProductionCredentialCommand(
    string ProviderId,
    string ModelId,
    string Secret,
    string? DisplayName);

public sealed record AddProductionCredentialVersionCommand(
    Guid CredentialId,
    string ProviderId,
    string ModelId,
    string Secret);

public sealed record ProductionCredentialRegistrationResult(
    Guid CredentialId,
    Guid CredentialVersionId,
    string ProviderId,
    string ModelId,
    string Status,
    int Version);

public static class ProductionCredentialRegistrationRules
{
    public const string ProviderRejected = "PRODUCTION_CREDENTIAL_PROVIDER_REJECTED";
    public const string ModelRejected = "PRODUCTION_CREDENTIAL_MODEL_REJECTED";
    public const string ProviderModelImmutable = "PRODUCTION_CREDENTIAL_PROVIDER_MODEL_IMMUTABLE";

    public static IReadOnlyList<ProductionMotionModel> ProductionCatalog { get; } =
    [
        .. ProductionMotionCatalog.All,
        .. ProductionVoiceCatalog.All,
        .. ProductionLipsyncCatalog.All,
        .. ProductionImageCatalog.All,
    ];

    public static (string ProviderId, string ModelId) RequireCatalogModel(string? providerId, string? modelId) =>
        RequireCatalogModel(providerId, modelId, ProductionCatalog);

    public static (string ProviderId, string ModelId) RequireCatalogModel(
        string? providerId, string? modelId, IReadOnlyList<ProductionMotionModel> catalog)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        var candidates = catalog
            .Where(m => !IsFixture(m)
                && string.Equals(m.ProviderId, provider, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (provider.Length == 0 || candidates.Count == 0)
            throw new InvalidOperationException(ProviderRejected);
        var entry = candidates.FirstOrDefault(m => string.Equals(m.ModelId, model, StringComparison.OrdinalIgnoreCase));
        if (entry is null || !entry.Enabled)
            throw new InvalidOperationException(ModelRejected);
        return (entry.ProviderId, entry.ModelId);
    }

    public static bool IsCatalogModel(string? providerId, string? modelId)
    {
        try
        {
            RequireCatalogModel(providerId, modelId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    static bool IsFixture(ProductionMotionModel model) =>
        string.Equals(model.ProviderId, ProductionMotionCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(model.ProviderId, ProductionVoiceCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(model.ProviderId, ProductionLipsyncCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase);
}

public sealed class ProductionCredentialRegistrationService : IProductionCredentialRegistration
{
    readonly IProductionCallerTenant _caller;
    readonly IProductionCredentialStore _store;
    readonly IProductionCredentialRotation _rotation;

    public ProductionCredentialRegistrationService(
        IProductionCallerTenant caller,
        IProductionCredentialStore store,
        IProductionCredentialRotation rotation)
    {
        _caller = caller;
        _store = store;
        _rotation = rotation;
    }

    public async Task<ProductionCredentialRegistrationResult> RegisterAsync(
        RegisterProductionCredentialCommand command, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var (provider, model) = ProductionCredentialRegistrationRules.RequireCatalogModel(
            command.ProviderId, command.ModelId);
        RequireSecret(command.Secret);
        var name = string.IsNullOrWhiteSpace(command.DisplayName) ? provider : command.DisplayName.Trim();
        RejectSecretInLabel(name, command.Secret);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenant, name);
        await _store.CreateAsync(credential, ct);
        var version = await _rotation.RotateAsync(
            credential.CredentialId, Guid.NewGuid(), provider, model, command.Secret, ct);
        return Result(credential.CredentialId, version);
    }

    public async Task<ProductionCredentialRegistrationResult> AddVersionAsync(
        AddProductionCredentialVersionCommand command, CancellationToken ct = default)
    {
        _caller.RequireTenant();
        var (provider, model) = ProductionCredentialRegistrationRules.RequireCatalogModel(
            command.ProviderId, command.ModelId);
        RequireSecret(command.Secret);
        if (command.CredentialId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialOwnership.IdRequired);
        await RequireSameProviderModelAsync(command.CredentialId, provider, model, ct);
        var version = await _rotation.RotateAsync(
            command.CredentialId, Guid.NewGuid(), provider, model, command.Secret, ct);
        return Result(command.CredentialId, version);
    }

    public async Task<ProductionCredentialRegistrationResult> RotateActiveAsync(
        RotateProductionCredentialCommand command, CancellationToken ct = default)
    {
        _caller.RequireTenant();
        var (provider, model) = ProductionCredentialRegistrationRules.RequireCatalogModel(
            command.ProviderId, command.ModelId);
        RequireSecret(command.Secret);
        var matches = await _store.ListEligibleAsync(provider, model, ct);
        if (matches.Count == 0)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        if (matches.Count > 1)
            throw new InvalidOperationException(ProductionCredentialResolver.Ambiguous);
        var credentialId = matches[0].CredentialId;
        await RequireSameProviderModelAsync(credentialId, provider, model, ct);
        var version = await _rotation.RotateAsync(
            credentialId, Guid.NewGuid(), provider, model, command.Secret, ct);
        return Result(credentialId, version);
    }

    public async Task<ProductionCredentialRegistrationResult> RevokeVersionAsync(
        Guid credentialVersionId, CancellationToken ct = default)
    {
        _caller.RequireTenant();
        if (credentialVersionId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionIdRequired);
        var version = await _rotation.RevokeVersionAsync(credentialVersionId, ct);
        return Result(version.CredentialId, version);
    }

    public async Task<IReadOnlyList<ProductionCredentialCatalogEntry>> ListAsync(CancellationToken ct = default)
    {
        _caller.RequireTenant();
        var rows = await _store.ListCatalogAsync(ct);
        return rows.Where(row => ProductionCredentialRegistrationRules.IsCatalogModel(row.ProviderId, row.ModelId)).ToList();
    }

    async Task RequireSameProviderModelAsync(Guid credentialId, string provider, string model, CancellationToken ct)
    {
        var versions = (await _store.ListCatalogAsync(ct))
            .Where(row => row.CredentialId == credentialId)
            .ToList();
        if (versions.Count == 0)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionCredentialRequired);
        var current = versions.FirstOrDefault(row =>
                string.Equals(row.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
            ?? versions.MaxBy(row => row.Version)!;
        if (!string.Equals(current.ProviderId, provider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.ModelId, model, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionCredentialRegistrationRules.ProviderModelImmutable);
    }

    static ProductionCredentialRegistrationResult Result(Guid credentialId, ProductionCredentialVersion version) =>
        new(
            credentialId,
            version.CredentialVersionId,
            version.ProviderId ?? "",
            version.ModelId ?? "",
            version.Status,
            version.Version);

    static void RequireSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretRequired);
    }

    static void RejectSecretInLabel(string label, string secret)
    {
        if (label.Contains(secret, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionCredentialOwnership.SecretForbidden);
    }
}
