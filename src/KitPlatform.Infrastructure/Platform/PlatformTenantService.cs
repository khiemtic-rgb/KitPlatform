using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Application.Platform;
using KitPlatform.Infrastructure.Kernel.Workspace;

namespace KitPlatform.Infrastructure.Platform;

internal sealed class PlatformTenantService : IPlatformTenantService
{
    private const int MinPasswordLength = 8;
    private const int MinProvisioningKeyLength = 16;
    private const int MaxAdditionalBranches = 20;

    private readonly PlatformTenantRepository _repository;
    private readonly PlatformSettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly WorkspacePackProvisioner _workspaceProvisioner;
    private readonly IKitAccountService _kitAccounts;

    public PlatformTenantService(
        PlatformTenantRepository repository,
        IOptions<PlatformSettings> settings,
        IHostEnvironment environment,
        WorkspacePackProvisioner workspaceProvisioner,
        IKitAccountService kitAccounts)
    {
        _repository = repository;
        _settings = settings.Value;
        _environment = environment;
        _workspaceProvisioner = workspaceProvisioner;
        _kitAccounts = kitAccounts;
    }

    public PlatformPublicConfigDto GetPublicConfig() =>
        new(
            _settings.BrandName,
            _settings.ProductName,
            _settings.AdminUrl,
            _settings.CustomerAppUrl,
            _settings.ApiUrl,
            "Nhập mã đơn vị do quản trị nền tảng cung cấp (ví dụ NT_A, PK_A, FM_A).");

    public async Task<PlatformSetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken = default)
    {
        var count = await _repository.CountTenantsAsync(cancellationToken);
        return new PlatformSetupStatusDto(
            count,
            SetupRequired: count == 0,
            ProvisioningKeyRequired: count > 0,
            _settings.BrandName,
            _settings.ProductName);
    }

    public Task<IReadOnlyList<PlatformTenantListItemDto>> ListTenantsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListTenantsAsync(cancellationToken);

    public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(
        CancellationToken cancellationToken = default) =>
        _repository.ListModulesAsync(cancellationToken);

    public Task<PlatformTenantEntitlementDto> GetTenantEntitlementAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        _repository.GetTenantEntitlementAsync(tenantId, cancellationToken);

    public async Task<PlatformTenantEntitlementDto> UpdateTenantEntitlementAsync(
        Guid tenantId,
        UpdatePlatformTenantEntitlementRequest request,
        string? provisioningKey,
        CancellationToken cancellationToken = default)
    {
        var tenantCount = await _repository.CountTenantsAsync(cancellationToken);
        EnsureProvisioningAuthorized(tenantCount, provisioningKey);

        var registry = await _repository.ListModulesAsync(cancellationToken);
        var registryCodes = registry.Select(m => m.ModuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vertical = TenantPlatformSettingsValidator.NormalizeVertical(request.Vertical);
        var allowed = TenantPlatformSettingsValidator.NormalizeAllowedModules(
            request.AllowedModules,
            registryCodes);

        var maxBranches = TenantPlatformSettingsValidator.NormalizeMaxBranches(request.MaxBranches);

        var current = await _repository.GetTenantEntitlementAsync(tenantId, cancellationToken);
        var enabled = request.SyncEnabledModules
            ? allowed.ToList()
            : TenantPlatformSettingsValidator.ClampEnabledToAllowed(current.EnabledModules, allowed);

        var updated = await _repository.UpdateTenantEntitlementAsync(
            tenantId,
            vertical,
            allowed,
            enabled,
            maxBranches,
            cancellationToken);

        await _workspaceProvisioner.EnsurePacksForTenantAsync(tenantId, enabled, cancellationToken);
        return updated;
    }

    public async Task EnsureCanManageTenantsAsync(string? provisioningKey, CancellationToken cancellationToken = default)
    {
        var tenantCount = await _repository.CountTenantsAsync(cancellationToken);
        EnsureProvisioningAuthorized(tenantCount, provisioningKey);
    }

    public async Task<CreatePlatformTenantResponse> CreateTenantAsync(
        CreatePlatformTenantRequest request,
        string? provisioningKey,
        CancellationToken cancellationToken = default)
    {
        var tenantCount = await _repository.CountTenantsAsync(cancellationToken);
        EnsureProvisioningAuthorized(tenantCount, provisioningKey);

        var vertical = string.IsNullOrWhiteSpace(request.Vertical)
            ? PlatformVerticalCodes.Pharmacy
            : TenantPlatformSettingsValidator.NormalizeVertical(request.Vertical);

        var isFamily = vertical.Equals(PlatformVerticalCodes.Family, StringComparison.OrdinalIgnoreCase);
        var isClinic = vertical.Equals(PlatformVerticalCodes.Clinic, StringComparison.OrdinalIgnoreCase);
        var codeLabel = isFamily ? "Mã đơn vị" : isClinic ? "Mã phòng khám" : "Mã nhà thuốc";
        var nameLabel = isFamily ? "Tên gia đình" : isClinic ? "Tên phòng khám" : "Tên nhà thuốc";

        var tenantCode = NormalizeCode(request.TenantCode, codeLabel);
        var branchCode = NormalizeCode(
            string.IsNullOrWhiteSpace(request.BranchCode) && isFamily ? "HOME" : request.BranchCode,
            isFamily ? "Mã nhà" : "Mã chi nhánh");

        var warehouseCode = isFamily
            ? NormalizeCode(
                string.IsNullOrWhiteSpace(request.WarehouseCode) ? "WH_HOME" : request.WarehouseCode,
                "Mã kho")
            : NormalizeCode(request.WarehouseCode, "Mã kho");

        if (string.IsNullOrWhiteSpace(request.TenantName))
            throw new InvalidOperationException($"{nameLabel} là bắt buộc.");

        var branchName = string.IsNullOrWhiteSpace(request.BranchName)
            ? (isFamily ? "Nhà" : isClinic ? "Cơ sở chính" : "Quầy chính")
            : request.BranchName.Trim();

        var warehouseName = string.IsNullOrWhiteSpace(request.WarehouseName)
            ? (isFamily ? "Kho nhà" : "Kho chính")
            : request.WarehouseName.Trim();

        ValidateAdmin(request);

        if (await _repository.TenantCodeExistsAsync(tenantCode, cancellationToken))
            throw new InvalidOperationException($"{codeLabel} «{tenantCode}» đã tồn tại.");

        var additional = isFamily
            ? Array.Empty<CreatePlatformBranchRequest>()
            : NormalizeAdditionalBranches(request.AdditionalBranches);
        ValidateUniqueBranchCodes(branchCode, additional);
        if (!isFamily)
            ValidateUniqueWarehouseCodes(warehouseCode, additional);

        var maxBranches = TenantPlatformSettingsValidator.NormalizeMaxBranches(request.MaxBranches);
        var plannedBranchCount = 1 + additional.Count;
        TenantPlatformSettingsValidator.EnsureWithinBranchQuota(plannedBranchCount, maxBranches);

        var normalized = request with
        {
            Vertical = vertical,
            TenantCode = tenantCode,
            TenantName = request.TenantName.Trim(),
            BranchCode = branchCode,
            BranchName = branchName,
            WarehouseCode = warehouseCode,
            WarehouseName = warehouseName,
            AdminUsername = request.AdminUsername.Trim(),
            AdminEmail = request.AdminEmail.Trim().ToLowerInvariant(),
            AdminFullName = string.IsNullOrWhiteSpace(request.AdminFullName)
                ? (isFamily ? "Phụ huynh" : "Quản trị viên")
                : request.AdminFullName.Trim(),
            LoyaltyEnabled = isFamily ? false : request.LoyaltyEnabled,
            AdditionalBranches = additional,
            MaxBranches = maxBranches,
        };

        await _kitAccounts.AssertEmailPasswordCompatibleAsync(
            normalized.AdminEmail,
            request.AdminPassword,
            cancellationToken);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword);
        var created = await _repository.CreateTenantAsync(normalized, passwordHash, cancellationToken);

        await _kitAccounts.EnsureAccountForUserAsync(
            created.UserId,
            created.TenantId,
            normalized.AdminEmail,
            request.AdminPassword,
            passwordHash,
            normalized.AdminFullName,
            connection: null,
            transaction: null,
            cancellationToken);

        var modules = PlatformTenantRepository.DefaultModulesForVertical(vertical);
        await _workspaceProvisioner.EnsurePacksForTenantAsync(created.TenantId, modules, cancellationToken);

        return created;
    }

    private void EnsureProvisioningAuthorized(int tenantCount, string? provisioningKey)
    {
        if (tenantCount == 0)
            return;

        var configured = _settings.ProvisioningKey?.Trim() ?? "";
        if (configured.Length < MinProvisioningKeyLength)
        {
            throw new InvalidOperationException(
                "Đã có đơn vị trên hệ thống — cần cấu hình Platform:ProvisioningKey (≥ 16 ký tự) trên server.");
        }

        var provided = provisioningKey?.Trim() ?? "";
        if (provided.Length == 0 || !CryptographicEquals(provided, configured))
            throw new UnauthorizedAccessException("Mã thiết lập nền tảng không đúng.");
    }

    private static void ValidateAdmin(CreatePlatformTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AdminUsername))
            throw new InvalidOperationException("Tên đăng nhập admin là bắt buộc.");

        if (string.IsNullOrWhiteSpace(request.AdminEmail) || !request.AdminEmail.Contains('@'))
            throw new InvalidOperationException("Email admin không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < MinPasswordLength)
            throw new InvalidOperationException($"Mật khẩu admin tối thiểu {MinPasswordLength} ký tự.");
    }

    private static string NormalizeCode(string value, string label)
    {
        var code = value.Trim().ToUpperInvariant();
        if (code.Length < 2 || code.Length > 30)
            throw new InvalidOperationException($"{label} phải từ 2–30 ký tự.");

        foreach (var ch in code)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')
                continue;
            throw new InvalidOperationException($"{label} chỉ gồm chữ, số, gạch dưới hoặc gạch ngang.");
        }

        return code.Replace('-', '_');
    }

    private static IReadOnlyList<CreatePlatformBranchRequest> NormalizeAdditionalBranches(
        IReadOnlyList<CreatePlatformBranchRequest>? branches)
    {
        if (branches is null || branches.Count == 0)
            return Array.Empty<CreatePlatformBranchRequest>();

        if (branches.Count > MaxAdditionalBranches)
            throw new InvalidOperationException($"Tối đa {MaxAdditionalBranches} chi nhánh bổ sung.");

        return branches.Select(b =>
        {
            var branchCode = NormalizeCode(b.BranchCode, "Mã chi nhánh bổ sung");
            var warehouseCode = NormalizeCode(b.WarehouseCode, "Mã kho bổ sung");

            if (string.IsNullOrWhiteSpace(b.BranchName))
                throw new InvalidOperationException("Tên chi nhánh bổ sung là bắt buộc.");

            if (string.IsNullOrWhiteSpace(b.WarehouseName))
                throw new InvalidOperationException("Tên kho bổ sung là bắt buộc.");

            return new CreatePlatformBranchRequest(
                branchCode,
                b.BranchName.Trim(),
                string.IsNullOrWhiteSpace(b.BranchAddress) ? null : b.BranchAddress.Trim(),
                string.IsNullOrWhiteSpace(b.BranchPhone) ? null : b.BranchPhone.Trim(),
                warehouseCode,
                b.WarehouseName.Trim());
        }).ToList();
    }

    private static void ValidateUniqueBranchCodes(
        string primaryBranchCode,
        IReadOnlyList<CreatePlatformBranchRequest> additional)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryBranchCode };
        foreach (var branch in additional)
        {
            if (!codes.Add(branch.BranchCode))
                throw new InvalidOperationException($"Mã chi nhánh «{branch.BranchCode}» bị trùng trong form.");
        }
    }

    private static void ValidateUniqueWarehouseCodes(
        string primaryWarehouseCode,
        IReadOnlyList<CreatePlatformBranchRequest> additional)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryWarehouseCode };
        foreach (var branch in additional)
        {
            if (!codes.Add(branch.WarehouseCode))
                throw new InvalidOperationException($"Mã kho «{branch.WarehouseCode}» bị trùng trong form.");
        }
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}
