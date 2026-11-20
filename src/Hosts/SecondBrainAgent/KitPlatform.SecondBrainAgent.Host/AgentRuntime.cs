using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.SecondBrainAgent.Host;

public sealed class AgentTenantContext : ITenantContext
{
    readonly IHttpContextAccessor _http;

    public AgentTenantContext(IHttpContextAccessor http) => _http = http;

    public bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId => CoreAuthority.RequireUserId(Principal);

    public Guid TenantId => CoreAuthority.RequireTenantId(Principal);

    public Guid? WorkspaceId => null;

    System.Security.Claims.ClaimsPrincipal? Principal => _http.HttpContext?.User;
}

public sealed class AgentPlatformSettings : ITenantPlatformSettings
{
    readonly IDbConnectionFactory _db;
    readonly ITenantContext _tenant;

    public AgentPlatformSettings(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The isolated Second Brain host does not edit platform settings.");

    public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlatformModuleRegistryItemDto>>(
        [
            new PlatformModuleRegistryItemDto(
                PlatformModuleCodes.PersonalIntelligence,
                "Personal Intelligence",
                "Second Brain Trading Agent V1",
                ["hybrid"],
                210),
        ]);

    public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(
        UpdateTenantPlatformSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The isolated Second Brain host does not edit platform settings.");

    public async Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleCode) || !_tenant.IsAuthenticated)
            return false;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var settings = await conn.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT settings::text FROM public.tenants WHERE id = @TenantId AND deleted_at IS NULL",
                new { TenantId = _tenant.TenantId },
                cancellationToken: cancellationToken));
        return Enabled(settings, moduleCode);
    }

    public async Task<bool> IsModuleEnabledForTenantCodeAsync(
        string tenantCode,
        string moduleCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(moduleCode))
            return false;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var settings = await conn.ExecuteScalarAsync<string>(
            new CommandDefinition(
                "SELECT settings::text FROM public.tenants WHERE tenant_code = @TenantCode AND deleted_at IS NULL AND status = 1",
                new { TenantCode = tenantCode.Trim() },
                cancellationToken: cancellationToken));
        return Enabled(settings, moduleCode);
    }

    public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> IsFeatureEnabledForTenantCodeAsync(
        string tenantCode,
        string featureCode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(
        Guid? branchId = null,
        Guid? customerAccountId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ResolvedTenantLocaleDto("vi-VN", "vi-VN", null, null));

    static bool Enabled(string? settings, string moduleCode)
    {
        if (string.IsNullOrWhiteSpace(settings))
            return false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(settings);
            if (!doc.RootElement.TryGetProperty("platform", out var platform)
                || !platform.TryGetProperty("enabled_modules", out var modules)
                || modules.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;
            return modules.EnumerateArray().Any(item =>
                item.ValueKind == System.Text.Json.JsonValueKind.String
                && string.Equals(item.GetString(), moduleCode, StringComparison.OrdinalIgnoreCase));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}

public sealed record AgentRuntimeReport(
    string Status,
    string Service,
    string Database,
    bool WorkerEnabled,
    bool BackfillWorkerRegistered,
    IReadOnlyList<string> HostedServices);
