namespace KitPlatform.Packs.Pharmacy.Catalog;

/// <summary>Effective CSDL API credentials for a tenant (own link or platform fallback).</summary>
public sealed record CsdlDuocEffectiveCredentials(
    Guid TenantId,
    string Source,
    string Mode,
    string BaseUrl,
    string Username,
    string Password,
    bool PasswordIsBase64,
    string? PracticeLicenseCode,
    bool EnableStockOutSync,
    bool EnableStockInSync,
    string? LinkStatus)
{
    public bool IsTenantLinked =>
        string.Equals(Source, "tenant", StringComparison.OrdinalIgnoreCase);

    public bool CanSyncStockOut =>
        EnableStockOutSync
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password)
        && NationalDrugCatalogSettings.NormalizeMode(Mode) is "sandbox" or "live";
}

public sealed record TenantCsdlDuocLinkDto(
    bool Enabled,
    string Environment,
    string? Username,
    bool PasswordConfigured,
    string? PracticeLicenseCode,
    bool EnableStockOutSync,
    bool EnableStockInSync,
    string Status,
    DateTime? LastCheckAt,
    string? LastError,
    DateTime? ConnectedAt,
    string ActiveAccountSource,
    string? ActiveAccountUsername,
    string? ActiveAccountLabel);

public sealed record UpdateTenantCsdlDuocLinkRequest(
    bool Enabled,
    string Environment,
    string? Username,
    /// <summary>null = keep; empty = clear; non-empty = replace.</summary>
    string? Password,
    string? PracticeLicenseCode,
    bool EnableStockOutSync,
    bool EnableStockInSync);

public interface ICsdlDuocCredentialResolver
{
    Task<CsdlDuocEffectiveCredentials> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface ITenantCsdlDuocLinkService
{
    Task<TenantCsdlDuocLinkDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantCsdlDuocLinkDto> UpdateAsync(
        Guid tenantId,
        UpdateTenantCsdlDuocLinkRequest request,
        Guid? updatedBy,
        CancellationToken cancellationToken = default);

    Task<TenantCsdlDuocLinkDto> TestConnectionAsync(
        Guid tenantId,
        Guid? updatedBy,
        CancellationToken cancellationToken = default);
}
