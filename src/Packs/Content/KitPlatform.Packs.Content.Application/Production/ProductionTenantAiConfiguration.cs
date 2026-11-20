using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionTenantAiCapabilities
{
    public const string Text = "TEXT";
    public const string Image = "IMAGE";
    public const string Video = "VIDEO";
    public const string Voice = "VOICE";
    public const string Lipsync = "LIPSYNC";

    public static IReadOnlyList<string> All { get; } = [Text, Image, Video, Voice, Lipsync];

    public static string Require(string? capability)
    {
        var value = (capability ?? "").Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException(ProductionTenantAiReasons.UnknownCapability);
        return value;
    }
}

public static class ProductionTenantAiReasons
{
    public const string UnknownCapability = "TENANT_AI_UNKNOWN_CAPABILITY";
    public const string ConfigurationRequired = "TENANT_AI_CONFIGURATION_REQUIRED";
    public const string CapabilityDisabled = "TENANT_AI_CAPABILITY_DISABLED";
    public const string ModelNotExecutable = "TENANT_AI_MODEL_NOT_EXECUTABLE";
    public const string CredentialRequired = "TENANT_AI_CREDENTIAL_REQUIRED";
    public const string CredentialNotOwned = "TENANT_AI_CREDENTIAL_NOT_OWNED";
    public const string CredentialMismatch = "TENANT_AI_CREDENTIAL_MISMATCH";
}

public static class ProductionTenantAiSources
{
    public const string Shot = "SHOT";
    public const string Build = "BUILD";
    public const string Tenant = "TENANT";
}

/// <summary>
/// Build-level override row in production_ai_selection. Shot rows use the real shot code.
/// </summary>
public static class ProductionAiSelectionScopes
{
    public const string BuildShotCode = "*";
}

/// <summary>
/// Tenant default for one capability. Not org_settings. CredentialId must belong to TenantId.
/// </summary>
public sealed record ProductionTenantAiConfig(
    Guid TenantId,
    string Capability,
    string ProviderId,
    string ModelId,
    Guid? CredentialId,
    bool Enabled,
    string UpdatedBy,
    DateTimeOffset UpdatedAt);

public interface IProductionTenantAiConfigStore
{
    Task<IReadOnlyList<ProductionTenantAiConfig>> ListAsync(CancellationToken ct = default);
    Task<ProductionTenantAiConfig?> GetAsync(string capability, CancellationToken ct = default);
    Task<ProductionTenantAiConfig> UpsertAsync(ProductionTenantAiConfig row, CancellationToken ct = default);
    Task<bool> DeleteAsync(string capability, CancellationToken ct = default);
}

public sealed class InMemoryProductionTenantAiConfigStore : IProductionTenantAiConfigStore
{
    readonly IProductionCallerTenant _caller;
    readonly object _gate = new();
    readonly Dictionary<(Guid, string), ProductionTenantAiConfig> _rows = new();

    public InMemoryProductionTenantAiConfigStore(IProductionCallerTenant caller) => _caller = caller;

    public Task<IReadOnlyList<ProductionTenantAiConfig>> ListAsync(CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        lock (_gate)
            return Task.FromResult<IReadOnlyList<ProductionTenantAiConfig>>(
                _rows.Values.Where(r => r.TenantId == tenant).OrderBy(r => r.Capability, StringComparer.Ordinal).ToList());
    }

    public Task<ProductionTenantAiConfig?> GetAsync(string capability, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var key = (tenant, ProductionTenantAiCapabilities.Require(capability));
        lock (_gate)
            return Task.FromResult(_rows.TryGetValue(key, out var row) ? row : null);
    }

    public Task<ProductionTenantAiConfig> UpsertAsync(ProductionTenantAiConfig row, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(tenant, row.TenantId);
        var stored = row with { Capability = ProductionTenantAiCapabilities.Require(row.Capability) };
        lock (_gate)
            _rows[(tenant, stored.Capability)] = stored;
        return Task.FromResult(stored);
    }

    public Task<bool> DeleteAsync(string capability, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var key = (tenant, ProductionTenantAiCapabilities.Require(capability));
        lock (_gate)
            return Task.FromResult(_rows.Remove(key));
    }
}

public sealed record ProductionTenantAiCatalogEntry(
    string Capability,
    string ProviderId,
    string ModelId,
    string DisplayName,
    bool IsPaid,
    bool RequiresCredential);

public interface IProductionTenantAiCatalog
{
    IReadOnlyList<ProductionTenantAiCatalogEntry> List(string capability);
}

/// <summary>
/// System catalog filtered to what a tenant-owned build can execute today.
/// VIDEO needs a registered motion adapter. LIPSYNC needs the Fal adapter.
/// IMAGE needs the tenant-credential Gemini adapter. VOICE needs the ElevenLabs adapter.
/// TEXT has no tenant path, so it lists nothing. Fixture models are never listed.
/// </summary>
public sealed class ProductionTenantAiCatalog : IProductionTenantAiCatalog
{
    readonly IProductionMotionAdapterResolver _motion;
    readonly ILipSyncExecutionAdapter? _lipsync;
    readonly TenantGeminiImageExecutionAdapter? _image;
    readonly ElevenLabsVoiceExecutionAdapter? _voice;
    readonly FalLipSyncExecutionAdapter? _falLipsync;

    public ProductionTenantAiCatalog(
        IProductionMotionAdapterResolver motion,
        ILipSyncExecutionAdapter? lipsync = null,
        TenantGeminiImageExecutionAdapter? image = null,
        ElevenLabsVoiceExecutionAdapter? voice = null,
        FalLipSyncExecutionAdapter? falLipsync = null)
    {
        _motion = motion;
        _lipsync = lipsync;
        _image = image;
        _voice = voice;
        _falLipsync = falLipsync;
    }

    public IReadOnlyList<ProductionTenantAiCatalogEntry> List(string capability)
    {
        var cap = ProductionTenantAiCapabilities.Require(capability);
        return cap switch
        {
            ProductionTenantAiCapabilities.Video => ProductionMotionCatalog.All
                .Where(m => m.Enabled
                    && !string.Equals(m.ProviderId, ProductionMotionCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
                    && HasMotionAdapter(m.ProviderId, m.ModelId))
                .Select(m => Entry(cap, m))
                .ToList(),
            ProductionTenantAiCapabilities.Lipsync => ProductionLipsyncCatalog.All
                .Where(m => m.Enabled
                    && !string.Equals(m.ProviderId, ProductionLipsyncCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
                    && HasLipsyncAdapter(m.ProviderId, m.ModelId))
                .Select(m => Entry(cap, m))
                .ToList(),
            ProductionTenantAiCapabilities.Image => ProductionImageCatalog.All
                .Where(m => m.Enabled
                    && _image is not null
                    && string.Equals(_image.ProviderId, m.ProviderId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_image.ModelId, m.ModelId, StringComparison.OrdinalIgnoreCase))
                .Select(m => Entry(cap, m))
                .ToList(),
            ProductionTenantAiCapabilities.Voice => ProductionVoiceCatalog.All
                .Where(m => m.Enabled
                    && !string.Equals(m.ProviderId, ProductionVoiceCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
                    && _voice is not null
                    && string.Equals(_voice.ProviderId, m.ProviderId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_voice.ModelId, m.ModelId, StringComparison.OrdinalIgnoreCase))
                .Select(m => Entry(cap, m))
                .ToList(),
            _ => [],
        };
    }

    bool HasLipsyncAdapter(string providerId, string modelId) =>
        AdapterMatches(_lipsync, providerId, modelId) || AdapterMatches(_falLipsync, providerId, modelId);

    static bool AdapterMatches(ILipSyncExecutionAdapter? adapter, string providerId, string modelId) =>
        adapter is not null
        && string.Equals(adapter.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(adapter.ModelId, modelId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Tenant IMAGE always runs on a tenant credential; there is no global key path.</summary>
    public static bool RequiresCredential(string capability, string providerId) =>
        capability == ProductionTenantAiCapabilities.Image || ProductionFrozenCredential.RequiresPin(providerId);

    bool HasMotionAdapter(string providerId, string modelId)
    {
        try
        {
            _motion.Resolve(providerId, modelId);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    static ProductionTenantAiCatalogEntry Entry(string capability, ProductionMotionModel m) =>
        new(capability, m.ProviderId, m.ModelId, m.DisplayName, m.IsPaid, RequiresCredential(capability, m.ProviderId));

    public static ProductionTenantAiCatalogEntry? Find(IProductionTenantAiCatalog catalog, string capability, string? providerId, string? modelId)
    {
        var provider = (providerId ?? "").Trim();
        var model = (modelId ?? "").Trim();
        return catalog.List(capability).FirstOrDefault(e =>
            string.Equals(e.ProviderId, provider, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.ModelId, model, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record SaveProductionTenantAiConfigCommand(
    string Capability,
    string ProviderId,
    string ModelId,
    Guid? CredentialId,
    bool Enabled,
    string UpdatedBy);

public sealed record ProductionTenantAiCapabilityView(
    string Capability,
    ProductionTenantAiConfig? Config,
    IReadOnlyList<ProductionTenantAiCatalogEntry> Catalog,
    IReadOnlyList<ProductionCredentialCatalogEntry> Credentials);

public interface IProductionTenantAiSettings
{
    Task<IReadOnlyList<ProductionTenantAiCapabilityView>> GetAsync(CancellationToken ct = default);
    Task<ProductionTenantAiConfig> SaveAsync(SaveProductionTenantAiConfigCommand command, CancellationToken ct = default);
    Task<bool> ClearAsync(string capability, CancellationToken ct = default);
    void RequireSelectableOverride(Guid buildId, string stage, string providerId, string modelId);
}

public sealed class ProductionTenantAiSettingsService : IProductionTenantAiSettings
{
    readonly IProductionCallerTenant _caller;
    readonly IProductionTenantAiConfigStore _configs;
    readonly IProductionTenantAiCatalog _catalog;
    readonly IProductionCredentialStore _credentials;

    public ProductionTenantAiSettingsService(
        IProductionCallerTenant caller,
        IProductionTenantAiConfigStore configs,
        IProductionTenantAiCatalog catalog,
        IProductionCredentialStore credentials)
    {
        _caller = caller;
        _configs = configs;
        _catalog = catalog;
        _credentials = credentials;
    }

    public async Task<IReadOnlyList<ProductionTenantAiCapabilityView>> GetAsync(CancellationToken ct = default)
    {
        _caller.RequireTenant();
        var rows = await _configs.ListAsync(ct);
        var credentials = (await _credentials.ListCatalogAsync(ct))
            .Where(c => string.Equals(c.IdentityStatus, ProductionCredentialStatuses.Active, StringComparison.Ordinal)
                && string.Equals(c.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
            .ToList();
        return ProductionTenantAiCapabilities.All.Select(cap =>
        {
            var catalog = _catalog.List(cap);
            var usable = credentials.Where(c => catalog.Any(e =>
                    e.RequiresCredential
                    && string.Equals(e.ProviderId, c.ProviderId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.ModelId, c.ModelId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return new ProductionTenantAiCapabilityView(
                cap, rows.FirstOrDefault(r => r.Capability == cap), catalog, usable);
        }).ToList();
    }

    public async Task<ProductionTenantAiConfig> SaveAsync(SaveProductionTenantAiConfigCommand command, CancellationToken ct = default)
    {
        var tenant = _caller.RequireTenant();
        var cap = ProductionTenantAiCapabilities.Require(command.Capability);
        var entry = ProductionTenantAiCatalog.Find(_catalog, cap, command.ProviderId, command.ModelId)
            ?? throw new InvalidOperationException(ProductionTenantAiReasons.ModelNotExecutable);
        Guid? credentialId = null;
        if (entry.RequiresCredential)
        {
            if (command.CredentialId is not { } id || id == Guid.Empty)
                throw new InvalidOperationException(ProductionTenantAiReasons.CredentialRequired);
            await ProductionTenantAiCredentials.RequireOwnedActiveAsync(_credentials, id, entry.ProviderId, entry.ModelId, ct);
            credentialId = id;
        }
        else if (command.CredentialId is { } unexpected && unexpected != Guid.Empty)
            throw new InvalidOperationException(ProductionTenantAiReasons.CredentialMismatch);

        return await _configs.UpsertAsync(new ProductionTenantAiConfig(
            tenant, cap, entry.ProviderId, entry.ModelId, credentialId, command.Enabled,
            (command.UpdatedBy ?? "").Trim(), DateTimeOffset.UtcNow), ct);
    }

    public Task<bool> ClearAsync(string capability, CancellationToken ct = default)
    {
        _caller.RequireTenant();
        return _configs.DeleteAsync(ProductionTenantAiCapabilities.Require(capability), ct);
    }

    /// <summary>Official-film builds keep their own selection regime.</summary>
    public void RequireSelectableOverride(Guid buildId, string stage, string providerId, string modelId)
    {
        if (ProductionScopeAnchorRules.IsOfficialFilm(buildId))
            return;
        var cap = ProductionTenantAiCapabilities.Require(stage);
        if (ProductionTenantAiCatalog.Find(_catalog, cap, providerId, modelId) is null)
            throw new InvalidOperationException(ProductionTenantAiReasons.ModelNotExecutable);
    }
}

public static class ProductionTenantAiCredentials
{
    /// <summary>
    /// The credential must be visible to the caller tenant, ACTIVE, and have exactly one ACTIVE version for provider/model.
    /// </summary>
    public static async Task<ProductionCredentialReference> RequireOwnedActiveAsync(
        IProductionCredentialStore store, Guid credentialId, string providerId, string modelId, CancellationToken ct)
    {
        var credential = await store.GetAsync(credentialId, ct)
            ?? throw new InvalidOperationException(ProductionTenantAiReasons.CredentialNotOwned);
        if (!string.Equals(credential.Status, ProductionCredentialStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionCredentialResolver.Revoked);
        var matches = (await store.ListEligibleAsync(providerId, modelId, ct))
            .Where(m => m.CredentialId == credentialId)
            .ToList();
        if (matches.Count == 0)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        if (matches.Count != 1)
            throw new InvalidOperationException(ProductionCredentialResolver.Ambiguous);
        return new ProductionCredentialReference(matches[0].CredentialId, matches[0].CredentialVersionId);
    }
}

public sealed record ProductionResolvedAi(
    string Capability,
    string ProviderId,
    string ModelId,
    ProductionCredentialReference? Credential,
    string Source);

public interface IProductionTenantAiResolution
{
    /// <summary>True for tenant-owned production builds. Official-film and Test scopes are not governed.</summary>
    Task<bool> GovernsAsync(ProductionScope scope, CancellationToken ct = default);

    /// <summary>Shot override, then build override, then tenant default. Otherwise blocks.</summary>
    Task<ProductionResolvedAi> ResolveAsync(ProductionScope scope, string capability, CancellationToken ct = default);
}

public sealed class ProductionTenantAiResolution : IProductionTenantAiResolution
{
    readonly IProductionTenantAiConfigStore _configs;
    readonly IProductionTenantAiCatalog _catalog;
    readonly IProductionCredentialStore _credentials;
    readonly IProductionAiSelectionStore? _selections;
    readonly IProductionScopeFoundationStore? _scopes;

    public ProductionTenantAiResolution(
        IProductionTenantAiConfigStore configs,
        IProductionTenantAiCatalog catalog,
        IProductionCredentialStore credentials,
        IProductionAiSelectionStore? selections = null,
        IProductionScopeFoundationStore? scopes = null)
    {
        _configs = configs;
        _catalog = catalog;
        _credentials = credentials;
        _selections = selections;
        _scopes = scopes;
    }

    public async Task<bool> GovernsAsync(ProductionScope scope, CancellationToken ct = default)
    {
        if (ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId))
            return false;
        if (_scopes is null)
            return true;
        var kind = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(scope.BuildId, ct), scope.BuildId);
        return kind != ProductionScopeKind.Test;
    }

    public async Task<ProductionResolvedAi> ResolveAsync(ProductionScope scope, string capability, CancellationToken ct = default)
    {
        var cap = ProductionTenantAiCapabilities.Require(capability);
        var tenantDefault = await _configs.GetAsync(cap, ct);

        string provider;
        string model;
        string source;
        var shot = await SelectionAsync(scope.BuildId, scope.ShotCode, cap, ct);
        var build = shot is null
            ? await SelectionAsync(scope.BuildId, ProductionAiSelectionScopes.BuildShotCode, cap, ct)
            : null;
        if (shot is not null)
            (provider, model, source) = (shot.ProviderId.Trim(), shot.ModelId.Trim(), ProductionTenantAiSources.Shot);
        else if (build is not null)
            (provider, model, source) = (build.ProviderId.Trim(), build.ModelId.Trim(), ProductionTenantAiSources.Build);
        else if (tenantDefault is null)
            throw new InvalidOperationException(ProductionTenantAiReasons.ConfigurationRequired);
        else if (!tenantDefault.Enabled)
            throw new InvalidOperationException(ProductionTenantAiReasons.CapabilityDisabled);
        else
            (provider, model, source) = (tenantDefault.ProviderId, tenantDefault.ModelId, ProductionTenantAiSources.Tenant);

        var entry = ProductionTenantAiCatalog.Find(_catalog, cap, provider, model)
            ?? throw new InvalidOperationException(ProductionTenantAiReasons.ModelNotExecutable);

        ProductionCredentialReference? credential = null;
        if (entry.RequiresCredential)
        {
            var configured = tenantDefault is { CredentialId: { } id }
                && string.Equals(tenantDefault.ProviderId, entry.ProviderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(tenantDefault.ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase)
                    ? id
                    : (Guid?)null;
            credential = configured is { } credentialId
                ? await ProductionTenantAiCredentials.RequireOwnedActiveAsync(
                    _credentials, credentialId, entry.ProviderId, entry.ModelId, ct)
                : await SingleActiveAsync(entry.ProviderId, entry.ModelId, ct);
        }
        return new ProductionResolvedAi(cap, entry.ProviderId, entry.ModelId, credential, source);
    }

    async Task<ProductionAiSelection?> SelectionAsync(Guid buildId, string shotCode, string capability, CancellationToken ct)
    {
        if (_selections is null || capability is ProductionTenantAiCapabilities.Text)
            return null;
        var row = await _selections.GetActiveAsync(buildId, shotCode, capability, ct);
        return row is not null
            && CreateVideoActionContractGate.IsPresent(row.ProviderId)
            && CreateVideoActionContractGate.IsPresent(row.ModelId)
                ? row
                : null;
    }

    async Task<ProductionCredentialReference> SingleActiveAsync(string providerId, string modelId, CancellationToken ct)
    {
        var matches = await _credentials.ListEligibleAsync(providerId, modelId, ct);
        if (matches.Count == 0)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        if (matches.Count != 1)
            throw new InvalidOperationException(ProductionCredentialResolver.Ambiguous);
        return new ProductionCredentialReference(matches[0].CredentialId, matches[0].CredentialVersionId);
    }
}
