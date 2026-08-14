using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

/// <summary>
/// Live/Sandbox client for TTYQG CSDL dược API v2 (QĐ-TTYQG API 1.1).
/// </summary>
internal sealed class CsdlDuocNationalDrugCatalogService : INationalDrugCatalogService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly CsdlDuocTokenProvider _tokens;
    private readonly ICsdlDuocCredentialResolver _credentials;
    private readonly ITenantContext _tenant;
    private readonly IOptionsMonitor<NationalDrugCatalogSettings> _options;
    private readonly ILogger<CsdlDuocNationalDrugCatalogService> _logger;

    public CsdlDuocNationalDrugCatalogService(
        CsdlDuocTokenProvider tokens,
        ICsdlDuocCredentialResolver credentials,
        ITenantContext tenant,
        IOptionsMonitor<NationalDrugCatalogSettings> options,
        ILogger<CsdlDuocNationalDrugCatalogService> logger)
    {
        _tokens = tokens;
        _credentials = credentials;
        _tenant = tenant;
        _options = options;
        _logger = logger;
    }

    public async Task<NationalDrugConnectionStatusDto> GetConnectionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var creds = await _credentials.ResolveAsync(_tenant.TenantId, cancellationToken);
        var mode = NationalDrugCatalogSettings.NormalizeMode(creds.Mode);
        var (label, isLive) = mode switch
        {
            "live" => ("Liên thông thật (CSDL dược)", true),
            "sandbox" => ("Sandbox CSDL dược", false),
            _ => ("Mock (nội bộ)", false),
        };
        if (creds.IsTenantLinked)
            label = mode == "live"
                ? "Tài khoản nhà thuốc (live)"
                : "Tài khoản nhà thuốc (sandbox)";
        else if (mode == "sandbox")
            label = "Sandbox chung (platform)";

        try
        {
            var client = await _tokens.CreateAuthorizedClientAsync(creds, cancellationToken);
            using var response = await client.GetAsync("master/drugs?page=1&page_size=1", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new NationalDrugConnectionStatusDto(
                    mode,
                    label,
                    isLive,
                    $"Đã đăng nhập nhưng GET /master/drugs lỗi HTTP {(int)response.StatusCode}.",
                    creds.Source,
                    MaskUser(creds.Username),
                    creds.LinkStatus);
            }

            var page = await response.Content.ReadFromJsonAsync<CsdlDuocPagedDrugsResponse>(JsonOpts, cancellationToken);
            return new NationalDrugConnectionStatusDto(
                mode,
                label,
                isLive,
                $"Kết nối OK — danh mục ~{page?.Total ?? 0:N0} thuốc. Nguồn: {label}.",
                creds.Source,
                MaskUser(creds.Username),
                creds.LinkStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CSDL dược connection check failed");
            return new NationalDrugConnectionStatusDto(
                mode,
                label,
                false,
                $"Chưa kết nối: {ex.Message}",
                creds.Source,
                MaskUser(creds.Username),
                creds.LinkStatus);
        }
    }

    public IReadOnlyList<NationalDrugFieldMapDto> GetFieldMap() => NationalDrugCatalogFieldMap.Items;

    public async Task<PagedNationalDrugListResult> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var q = search?.Trim();

        if (string.IsNullOrWhiteSpace(q))
            return await BrowseAsync(page, pageSize, cancellationToken);

        // Exact / near-exact id or registration lookup.
        var byId = await TryGetDrugAsync(q, cancellationToken);
        if (byId is not null)
        {
            return new PagedNationalDrugListResult(
                [CsdlDuocDrugMapper.ToListItem(byId)],
                1,
                1,
                pageSize);
        }

        return await ScanFilterAsync(q, page, pageSize, cancellationToken);
    }

    public async Task<NationalDrugDetailDto?> GetAsync(
        string drugId,
        CancellationToken cancellationToken = default)
    {
        var drug = await TryGetDrugAsync(drugId?.Trim() ?? string.Empty, cancellationToken);
        return drug is null ? null : CsdlDuocDrugMapper.ToDetail(drug);
    }

    public async Task<NationalDrugProductPrefillDto?> BuildProductPrefillAsync(
        string drugId,
        CancellationToken cancellationToken = default)
    {
        var drug = await TryGetDrugAsync(drugId?.Trim() ?? string.Empty, cancellationToken);
        return drug is null ? null : CsdlDuocDrugMapper.ToPrefill(drug);
    }

    private async Task<PagedNationalDrugListResult> BrowseAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var url = $"master/drugs?page={page}&page_size={pageSize}";
        using var response = await GetWithAuthRetryAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CsdlDuocPagedDrugsResponse>(JsonOpts, cancellationToken)
            ?? new CsdlDuocPagedDrugsResponse();
        var items = (payload.Data ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Id))
            .Select(CsdlDuocDrugMapper.ToListItem)
            .ToList();
        return new PagedNationalDrugListResult(items, payload.Total, page, pageSize);
    }

    private async Task<PagedNationalDrugListResult> ScanFilterAsync(
        string q,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var settings = _options.CurrentValue;
        var maxPages = Math.Clamp(settings.MaxSearchScanPages, 1, 200);
        var matches = new List<CsdlDuocDrugDto>();
        var client = await AuthorizedClientAsync(cancellationToken);

        for (var apiPage = 1; apiPage <= maxPages; apiPage++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"master/drugs?page={apiPage}&page_size=50";
            using var response = await client.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var creds = await _credentials.ResolveAsync(_tenant.TenantId, cancellationToken);
                _tokens.Invalidate(creds.Username);
                client = await AuthorizedClientAsync(cancellationToken);
                using var retry = await client.GetAsync(url, cancellationToken);
                retry.EnsureSuccessStatusCode();
                var retryPayload = await retry.Content.ReadFromJsonAsync<CsdlDuocPagedDrugsResponse>(JsonOpts, cancellationToken);
                AppendMatches(retryPayload, q, matches);
                if (IsLastPage(retryPayload, apiPage, 50)) break;
                continue;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<CsdlDuocPagedDrugsResponse>(JsonOpts, cancellationToken);
            AppendMatches(payload, q, matches);
            if (IsLastPage(payload, apiPage, 50)) break;
        }

        var total = matches.Count;
        var items = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CsdlDuocDrugMapper.ToListItem)
            .ToList();
        return new PagedNationalDrugListResult(items, total, page, pageSize);
    }

    private static void AppendMatches(
        CsdlDuocPagedDrugsResponse? payload,
        string q,
        List<CsdlDuocDrugDto> matches)
    {
        foreach (var d in payload?.Data ?? [])
        {
            if (string.IsNullOrWhiteSpace(d.Id)) continue;
            if (CsdlDuocDrugMapper.MatchesSearch(d, q))
                matches.Add(d);
        }
    }

    private static bool IsLastPage(CsdlDuocPagedDrugsResponse? payload, int apiPage, int pageSize)
    {
        var count = payload?.Data?.Count ?? 0;
        if (count < pageSize) return true;
        if (payload?.Total > 0 && apiPage * pageSize >= payload.Total) return true;
        return false;
    }

    private async Task<CsdlDuocDrugDto?> TryGetDrugAsync(string drugId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(drugId)) return null;
        // Avoid path abuse / odd queries that always 500 on sandbox.
        if (drugId.Length > 40 || drugId.Contains('/') || drugId.Contains('?'))
            return null;

        var client = await AuthorizedClientAsync(cancellationToken);
        var url = $"master/drugs/{Uri.EscapeDataString(drugId)}";
        using var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            return null;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var creds = await _credentials.ResolveAsync(_tenant.TenantId, cancellationToken);
            _tokens.Invalidate(creds.Username);
            client = await AuthorizedClientAsync(cancellationToken);
            using var retry = await client.GetAsync(url, cancellationToken);
            if (!retry.IsSuccessStatusCode) return null;
            return await retry.Content.ReadFromJsonAsync<CsdlDuocDrugDto>(JsonOpts, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("CSDL dược get {DrugId} → HTTP {Status}", drugId, (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CsdlDuocDrugDto>(JsonOpts, cancellationToken);
    }

    private async Task<HttpClient> AuthorizedClientAsync(CancellationToken cancellationToken)
    {
        var creds = await _credentials.ResolveAsync(_tenant.TenantId, cancellationToken);
        return await _tokens.CreateAuthorizedClientAsync(creds, cancellationToken);
    }

    private async Task<HttpResponseMessage> GetWithAuthRetryAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = await AuthorizedClientAsync(cancellationToken);
        var response = await client.GetAsync(relativeUrl, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        var creds = await _credentials.ResolveAsync(_tenant.TenantId, cancellationToken);
        _tokens.Invalidate(creds.Username);
        client = await AuthorizedClientAsync(cancellationToken);
        return await client.GetAsync(relativeUrl, cancellationToken);
    }

    private static string? MaskUser(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var u = username.Trim();
        if (u.Length <= 4) return "****";
        return u[..2] + new string('*', Math.Min(6, u.Length - 4)) + u[^2..];
    }
}
