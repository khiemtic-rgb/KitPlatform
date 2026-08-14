using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class TenantCsdlDuocLinkService : ITenantCsdlDuocLinkService
{
    private readonly TenantCsdlDuocLinkRepository _repo;
    private readonly ICsdlDuocCredentialResolver _resolver;
    private readonly CsdlDuocTokenProvider _tokens;
    private readonly IOptionsMonitor<NationalDrugCatalogSettings> _platform;
    private readonly ILogger<TenantCsdlDuocLinkService> _logger;

    public TenantCsdlDuocLinkService(
        TenantCsdlDuocLinkRepository repo,
        ICsdlDuocCredentialResolver resolver,
        CsdlDuocTokenProvider tokens,
        IOptionsMonitor<NationalDrugCatalogSettings> platform,
        ILogger<TenantCsdlDuocLinkService> logger)
    {
        _repo = repo;
        _resolver = resolver;
        _tokens = tokens;
        _platform = platform;
        _logger = logger;
    }

    public async Task<TenantCsdlDuocLinkDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAsync(tenantId, cancellationToken);
        var effective = await _resolver.ResolveAsync(tenantId, cancellationToken);
        return ToDto(row, effective);
    }

    public async Task<TenantCsdlDuocLinkDto> UpdateAsync(
        Guid tenantId,
        UpdateTenantCsdlDuocLinkRequest request,
        Guid? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var env = NationalDrugCatalogSettings.NormalizeMode(request.Environment) switch
        {
            "live" => "live",
            _ => "sandbox",
        };

        var existing = await _repo.GetAsync(tenantId, cancellationToken);
        var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        string? password = existing?.Password;
        if (request.Password is not null)
            password = string.IsNullOrWhiteSpace(request.Password) ? null : request.Password;

        var hasCreds = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
        string status;
        if (!request.Enabled)
            status = "Disabled";
        else if (!hasCreds)
            status = "NotConfigured";
        else if (existing is not null
                 && string.Equals(existing.Status, "Connected", StringComparison.OrdinalIgnoreCase)
                 && string.Equals(existing.Username, username, StringComparison.Ordinal)
                 && request.Password is null)
            status = "Connected";
        else
            status = "Configured";

        var row = new TenantCsdlDuocLinkRow
        {
            TenantId = tenantId,
            Enabled = request.Enabled,
            Environment = env,
            Username = username,
            Password = password,
            PracticeLicenseCode = string.IsNullOrWhiteSpace(request.PracticeLicenseCode)
                ? null
                : request.PracticeLicenseCode.Trim(),
            EnableStockOutSync = request.EnableStockOutSync,
            EnableStockInSync = request.EnableStockInSync,
            Status = status,
            LastCheckAt = existing?.LastCheckAt,
            LastError = status is "Configured" or "NotConfigured" or "Disabled" ? null : existing?.LastError,
            ConnectedAt = status == "Connected" ? existing?.ConnectedAt : null,
            UpdatedBy = updatedBy,
        };

        if (!string.Equals(existing?.Username, username, StringComparison.Ordinal)
            || request.Password is not null)
        {
            _tokens.Invalidate(username);
            _tokens.Invalidate(existing?.Username);
        }

        await _repo.UpsertAsync(row, cancellationToken);
        return await GetAsync(tenantId, cancellationToken);
    }

    public async Task<TenantCsdlDuocLinkDto> TestConnectionAsync(
        Guid tenantId,
        Guid? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Chưa lưu cấu hình liên thông. Hãy lưu tài khoản trước khi kiểm tra.");

        if (string.IsNullOrWhiteSpace(existing.Username) || string.IsNullOrWhiteSpace(existing.Password))
            throw new InvalidOperationException("Thiếu username/password để kiểm tra kết nối.");

        var mode = NationalDrugCatalogSettings.NormalizeMode(existing.Environment);
        var baseUrl = mode == "live"
            ? "https://api.csdlduoc.com.vn/v2"
            : "https://api-sandbox.csdlduoc.com.vn/v2";

        var probe = new CsdlDuocEffectiveCredentials(
            tenantId,
            Source: "tenant",
            Mode: mode,
            BaseUrl: baseUrl,
            Username: existing.Username.Trim(),
            Password: existing.Password,
            PasswordIsBase64: false,
            PracticeLicenseCode: existing.PracticeLicenseCode,
            EnableStockOutSync: existing.EnableStockOutSync,
            EnableStockInSync: existing.EnableStockInSync,
            LinkStatus: existing.Status);

        string status;
        string? error = null;
        DateTime? connectedAt = existing.ConnectedAt;
        try
        {
            _tokens.Invalidate(probe.Username);
            var client = await _tokens.CreateAuthorizedClientAsync(probe, cancellationToken);
            using var response = await client.GetAsync("master/drugs?page=1&page_size=1", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                status = "Error";
                error = $"Đăng nhập OK nhưng GET /master/drugs HTTP {(int)response.StatusCode}.";
            }
            else
            {
                status = "Connected";
                connectedAt ??= DateTime.UtcNow;
                var page = await response.Content.ReadFromJsonAsync<CsdlDuocPagedDrugsResponse>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
                error = null;
                _logger.LogInformation(
                    "Tenant {TenantId} CSDL link Connected — catalog ~{Total}",
                    tenantId,
                    page?.Total ?? 0);
            }
        }
        catch (Exception ex)
        {
            status = "Error";
            error = ex.Message;
            _logger.LogWarning(ex, "Tenant {TenantId} CSDL link test failed", tenantId);
        }

        var updated = new TenantCsdlDuocLinkRow
        {
            TenantId = tenantId,
            Enabled = existing.Enabled || status == "Connected",
            Environment = existing.Environment,
            Username = existing.Username,
            Password = existing.Password,
            PracticeLicenseCode = existing.PracticeLicenseCode,
            EnableStockOutSync = existing.EnableStockOutSync,
            EnableStockInSync = existing.EnableStockInSync,
            Status = status,
            LastCheckAt = DateTime.UtcNow,
            LastError = error,
            ConnectedAt = connectedAt,
            UpdatedBy = updatedBy,
        };
        await _repo.UpsertAsync(updated, cancellationToken);
        return await GetAsync(tenantId, cancellationToken);
    }

    private TenantCsdlDuocLinkDto ToDto(TenantCsdlDuocLinkRow? row, CsdlDuocEffectiveCredentials effective)
    {
        var platform = _platform.CurrentValue;
        var sourceLabel = effective.IsTenantLinked
            ? (effective.Mode == "live" ? "Tài khoản nhà thuốc (live)" : "Tài khoản nhà thuốc (sandbox)")
            : (NationalDrugCatalogSettings.NormalizeMode(platform.Mode) == "mock"
                ? "Mock nội bộ"
                : "Sandbox chung (platform)");

        return new TenantCsdlDuocLinkDto(
            Enabled: row?.Enabled ?? false,
            Environment: row?.Environment ?? "sandbox",
            Username: row?.Username,
            PasswordConfigured: !string.IsNullOrWhiteSpace(row?.Password),
            PracticeLicenseCode: row?.PracticeLicenseCode,
            EnableStockOutSync: row?.EnableStockOutSync ?? false,
            EnableStockInSync: row?.EnableStockInSync ?? false,
            Status: row?.Status ?? "NotConfigured",
            LastCheckAt: row?.LastCheckAt,
            LastError: row?.LastError,
            ConnectedAt: row?.ConnectedAt,
            ActiveAccountSource: effective.Source,
            ActiveAccountUsername: string.IsNullOrWhiteSpace(effective.Username) ? null : effective.Username,
            ActiveAccountLabel: sourceLabel);
    }
}
