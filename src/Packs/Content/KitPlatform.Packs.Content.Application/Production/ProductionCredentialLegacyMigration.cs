namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// ADR-04 Gate F. Migrates a legacy credential only when Tenant ownership is already proven.
/// Does not read org_settings, environment keys, or frozen executions.
/// </summary>
public enum ProductionCredentialLegacyClass
{
    ProvablyTenantOwned,
    MultiTenantShared,
    UnscopedUnknown,
    NonProduction,
    AlreadyMigrated,
}

public sealed record ProductionCredentialLegacySource(
    string Source,
    string Field,
    string Provider,
    ProductionCredentialLegacyClass Class,
    string Evidence);

public sealed record ProductionCredentialLegacyEvidence(
    ProductionCredentialLegacyClass Class,
    Guid TenantId,
    Guid CredentialId,
    Guid CredentialVersionId,
    string ProviderId,
    string ModelId,
    string Source);

public interface IProductionCredentialLegacyMigration
{
    Task<ProductionCredentialVersion> MigrateAsync(
        ProductionCredentialLegacyEvidence evidence,
        string secret,
        CancellationToken ct = default);
}

public static class ProductionCredentialLegacyCatalog
{
    public const string NotEligible = "PRODUCTION_CREDENTIAL_LEGACY_NOT_ELIGIBLE";

    public static IReadOnlyList<ProductionCredentialLegacySource> Inventory() =>
    [
        new("pack_content.org_settings.ai_config_json", "geminiApiKey", "gemini",
            ProductionCredentialLegacyClass.MultiTenantShared,
            "Singleton row, no tenant_id. Read by Content AI, Local OS rewrite, and Pharmacy consultation fallback."),
        new("pack_content.org_settings.ai_config_json", "geminiApiKeySecretRef", "gemini",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Secret reference resolves process configuration and environment. No tenant row."),
        new("pack_content.org_settings.video_config_json", "runwayApiKey", "runway",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Singleton row, no tenant_id, no credential-to-tenant record."),
        new("pack_content.org_settings.video_config_json", "runwayApiKeySecretRef", "runway",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Environment and Content:Secrets fallback. No tenant row."),
        new("pack_content.org_settings.video_config_json", "falApiKey", "fal",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Singleton row, no tenant_id, no credential-to-tenant record."),
        new("pack_content.org_settings.video_config_json", "falApiKeySecretRef", "fal",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Environment and Content:Secrets fallback. No tenant row."),
        new("pack_content.org_settings.video_config_json", "elevenLabsApiKey", "elevenlabs",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Singleton row, no tenant_id, no credential-to-tenant record."),
        new("pack_content.org_settings.video_config_json", "elevenLabsApiKeySecretRef", "elevenlabs",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Environment and Content:Secrets fallback. No tenant row."),
        new("pack_content.org_settings.video_config_json", "veoJoinFile", "google",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Singleton join material, no tenant_id, no credential-to-tenant record."),
        new("pack_content.org_settings.video_config_json", "veoJoinSecretRef", "google",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Environment and Content:Secrets fallback. No tenant row."),
        new("pack_content.org_settings.video_config_json", "creatomateApiKey", "creatomate",
            ProductionCredentialLegacyClass.NonProduction,
            "Content Park video factory. Not a Production credential."),
        new("pack_content.org_settings.video_config_json", "creatomateApiKeySecretRef", "creatomate",
            ProductionCredentialLegacyClass.NonProduction,
            "Content Park video factory reference. Not a Production credential."),
        new("pack_content.org_settings.facebook_config_json", "appSecret", "facebook",
            ProductionCredentialLegacyClass.NonProduction,
            "Marketing OAuth app secret. Singleton, not a Production credential."),
        new("pack_content.org_settings.facebook_config_json", "appId", "facebook",
            ProductionCredentialLegacyClass.NonProduction,
            "Marketing OAuth app id. Not a Production credential."),
        new("pack_content.channel_target.config_json", "storedSecret", "facebook",
            ProductionCredentialLegacyClass.NonProduction,
            "Page token column. channel_target has no tenant_id. Not a Production credential."),
        new("pack_sales.ai_settings", "gemini_api_key", "gemini",
            ProductionCredentialLegacyClass.NonProduction,
            "KIT_SALES restyle key. tenant_id is present and is not Production ownership."),
        new("pharmacy tenant settings", "GeminiApiKey", "gemini",
            ProductionCredentialLegacyClass.NonProduction,
            "Pharmacy consultation. Not a Production credential."),
        new("process environment", "RUNWAY_API_KEY", "runway",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Process-global. No tenant record."),
        new("process environment", "FAL_KEY", "fal",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Process-global. No tenant record."),
        new("process environment", "ELEVENLABS_API_KEY", "elevenlabs",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Process-global. No tenant record."),
        new("process environment", "VEO_JOIN_FILE", "google",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Process-global. No tenant record."),
        new("process environment", "GEMINI_API_KEY", "gemini",
            ProductionCredentialLegacyClass.MultiTenantShared,
            "Process-global key read by Content, Local OS, and Pharmacy fallback."),
        new("configuration", "Content:Secrets", "mixed",
            ProductionCredentialLegacyClass.UnscopedUnknown,
            "Process configuration. No tenant record."),
        new("pack_content.production_credentials", "credential_id", "production",
            ProductionCredentialLegacyClass.AlreadyMigrated,
            "Tenant-owned credential table. Local count is zero. Nothing to copy."),
    ];

    public static int EligibleCount() =>
        Inventory().Count(row => row.Class == ProductionCredentialLegacyClass.ProvablyTenantOwned);
}

public static class ProductionCredentialLegacyMigration
{
    public static async Task<ProductionCredentialVersion> ApplyAsync(
        ProductionCredentialLegacyEvidence evidence,
        string secret,
        IProductionCredentialStore store,
        IProductionCredentialRotation rotation,
        IProductionCallerTenant caller,
        CancellationToken ct = default)
    {
        RequireEligible(evidence);
        var owner = caller.RequireTenant();
        ProductionCredentialOwnership.RequireOwned(owner, evidence.TenantId);
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretRequired);

        var existing = await store.GetAsync(evidence.CredentialId, ct);
        var version = await store.GetVersionAsync(evidence.CredentialVersionId, ct);
        if (existing is not null && version is not null && version.CredentialId == existing.CredentialId)
            return version;

        if (existing is null)
        {
            await store.CreateAsync(
                ProductionCredentialOwnership.Create(evidence.CredentialId, evidence.TenantId, Label(evidence)),
                ct);
        }

        return await rotation.RotateAsync(
            evidence.CredentialId,
            evidence.CredentialVersionId,
            evidence.ProviderId,
            evidence.ModelId,
            secret,
            ct);
    }

    public static void RequireEligible(ProductionCredentialLegacyEvidence evidence)
    {
        if (evidence.Class != ProductionCredentialLegacyClass.ProvablyTenantOwned)
            throw new InvalidOperationException(ProductionCredentialLegacyCatalog.NotEligible);
        ProductionTenantBoundary.Require(evidence.TenantId);
        if (evidence.CredentialId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialOwnership.IdRequired);
        if (evidence.CredentialVersionId == Guid.Empty)
            throw new InvalidOperationException(ProductionCredentialOwnership.VersionIdRequired);
        if (string.IsNullOrWhiteSpace(evidence.ProviderId) || string.IsNullOrWhiteSpace(evidence.ModelId))
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
    }

    static string Label(ProductionCredentialLegacyEvidence evidence)
    {
        var provider = evidence.ProviderId.Trim();
        return provider.Length == 0 ? "legacy" : "legacy-" + provider;
    }
}

public sealed class InMemoryProductionCredentialLegacyMigration : IProductionCredentialLegacyMigration
{
    readonly InMemoryProductionCredentialStore _store;
    readonly IProductionCredentialRotation _rotation;
    readonly IProductionCallerTenant _caller;

    public InMemoryProductionCredentialLegacyMigration(
        InMemoryProductionCredentialStore store,
        IProductionCredentialRotation rotation,
        IProductionCallerTenant caller)
    {
        _store = store;
        _rotation = rotation;
        _caller = caller;
    }

    public async Task<ProductionCredentialVersion> MigrateAsync(
        ProductionCredentialLegacyEvidence evidence,
        string secret,
        CancellationToken ct = default)
    {
        var before = await _store.GetAsync(evidence.CredentialId, ct);
        try
        {
            return await ProductionCredentialLegacyMigration.ApplyAsync(evidence, secret, _store, _rotation, _caller, ct);
        }
        catch
        {
            if (before is null)
                _store.DiscardCredential(evidence.CredentialId);
            throw;
        }
    }
}
