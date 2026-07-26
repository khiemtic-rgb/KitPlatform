namespace KitPlatform.Application.Platform;

public sealed record PlatformPublicConfigDto(
    string BrandName,
    string ProductName,
    string AdminUrl,
    string CustomerAppUrl,
    string ApiUrl,
    string LoginHint);

public sealed record PlatformSetupStatusDto(
    int TenantsCount,
    bool SetupRequired,
    bool ProvisioningKeyRequired,
    string BrandName,
    string ProductName);

public sealed record PlatformTenantListItemDto(
    Guid Id,
    string TenantCode,
    string TenantName,
    DateTimeOffset CreatedAt,
    short Status,
    string Vertical,
    int AllowedModuleCount,
    int EnabledModuleCount);

public sealed record PlatformTenantEntitlementDto(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    string Vertical,
    IReadOnlyList<string> AllowedModules,
    IReadOnlyList<string> EnabledModules,
    /// <summary>Core ceiling for active branches. Null = unlimited.</summary>
    int? MaxBranches);

public sealed record UpdatePlatformTenantEntitlementRequest(
    string Vertical,
    IReadOnlyList<string> AllowedModules,
    /// <summary>When true (default), clamp enabled_modules to allowed and fill if empty.</summary>
    bool SyncEnabledModules = true,
    /// <summary>Null = unlimited. Lowering below current count does not delete branches.</summary>
    int? MaxBranches = null);

public sealed record CreatePlatformBranchRequest(
    string BranchCode,
    string BranchName,
    string? BranchAddress,
    string? BranchPhone,
    string WarehouseCode,
    string WarehouseName);

public sealed record CreatePlatformTenantRequest(
    string TenantCode,
    string TenantName,
    string BranchCode,
    string BranchName,
    string? BranchAddress,
    string? BranchPhone,
    string WarehouseCode,
    string WarehouseName,
    string AdminUsername,
    string AdminEmail,
    string AdminFullName,
    string AdminPassword,
    bool LoyaltyEnabled,
    IReadOnlyList<CreatePlatformBranchRequest>? AdditionalBranches = null,
    /// <summary>Optional Core ceiling at provision time. Null = unlimited.</summary>
    int? MaxBranches = null,
    /// <summary>
    /// Commercial shell: <c>pharmacy</c> | <c>clinic</c> | <c>family</c>.
    /// Defaults to pharmacy for backward compatibility with older clients.
    /// </summary>
    string? Vertical = null);

public sealed record CreatePlatformTenantResponse(
    Guid TenantId,
    string TenantCode,
    string TenantName,
    Guid BranchId,
    Guid UserId,
    string AdminUsername,
    int BranchCount);
