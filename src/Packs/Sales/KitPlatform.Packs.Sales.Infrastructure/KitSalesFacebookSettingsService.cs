using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Sales.Infrastructure;

public sealed class KitSalesFacebookSettingsService : IKitSalesFacebookSettings
{
    private static readonly Regex PsidPattern = new(@"^\d{5,128}$", RegexOptions.Compiled);
    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;
    private readonly KitSalesFacebookOptions _file;
    private KitSalesFacebookOptions? _cached;

    public KitSalesFacebookSettingsService(
        IDbConnectionFactory db,
        IHttpClientFactory http,
        IOptions<KitSalesFacebookOptions> options)
    {
        _db = db;
        _http = http;
        _file = options.Value;
    }

    public async Task<KitSalesFacebookOptions> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
            return _cached;

        var row = await LoadRowAsync(cancellationToken);
        _cached = Merge(_file, row);
        return _cached;
    }

    public async Task<KitSalesFacebookSettingsDto> GetMaskedAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectiveAsync(cancellationToken);
        return Mask(effective, webhookUrl);
    }

    public async Task<KitSalesFacebookSettingsDto> SaveAsync(
        SaveKitSalesFacebookSettingsRequest request,
        string webhookUrl,
        CancellationToken cancellationToken = default)
    {
        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<SettingsRow>(
            """
            SELECT tenant_id AS TenantId, enabled AS Enabled, page_id AS PageId,
                   page_access_token AS PageAccessToken, verify_token AS VerifyToken,
                   app_secret AS AppSecret, me_link AS MeLink, page_url AS PageUrl,
                   phc_url AS PhcUrl
            FROM pack_sales.facebook_settings
            WHERE tenant_id = @TenantId
            """,
            new { TenantId = tenantId });

        var token = request.ClearPageToken
            ? null
            : FirstNonEmpty(request.PageAccessToken, existing?.PageAccessToken);
        var verify = request.ClearVerifyToken
            ? null
            : FirstNonEmpty(request.VerifyToken, existing?.VerifyToken);
        var secret = request.ClearAppSecret
            ? null
            : FirstNonEmpty(request.AppSecret, existing?.AppSecret);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.facebook_settings (
                tenant_id, enabled, page_id, page_access_token, verify_token,
                app_secret, me_link, page_url, phc_url, updated_at
            )
            VALUES (
                @TenantId, @Enabled, @PageId, @PageAccessToken, @VerifyToken,
                @AppSecret, @MeLink, @PageUrl, @PhcUrl, NOW()
            )
            ON CONFLICT (tenant_id) DO UPDATE SET
                enabled = EXCLUDED.enabled,
                page_id = EXCLUDED.page_id,
                page_access_token = EXCLUDED.page_access_token,
                verify_token = EXCLUDED.verify_token,
                app_secret = EXCLUDED.app_secret,
                me_link = EXCLUDED.me_link,
                page_url = EXCLUDED.page_url,
                phc_url = EXCLUDED.phc_url,
                updated_at = NOW()
            """,
            new
            {
                TenantId = tenantId,
                request.Enabled,
                PageId = TrimOrNull(request.PageId),
                PageAccessToken = token,
                VerifyToken = verify,
                AppSecret = secret,
                MeLink = TrimOrNull(request.MeLink),
                PageUrl = TrimOrNull(request.PageUrl),
                PhcUrl = TrimOrNull(request.PhcUrl),
            });

        _cached = null;
        return await GetMaskedAsync(webhookUrl, cancellationToken);
    }

    public async Task<KitSalesFacebookTestDto> TestAsync(CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectiveAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(effective.PageAccessToken))
            return new KitSalesFacebookTestDto(false, null, null, "Chưa có Page Access Token.");

        var client = _http.CreateClient("kit-sales-facebook");
        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            "https://graph.facebook.com/v21.0/me?fields=id,name");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + effective.PageAccessToken);
        using var res = await client.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var msg))
            {
                return new KitSalesFacebookTestDto(false, null, null, msg.GetString());
            }

            if (!res.IsSuccessStatusCode)
                return new KitSalesFacebookTestDto(false, null, null, $"Graph trả {(int)res.StatusCode}.");

            return new KitSalesFacebookTestDto(
                true,
                root.TryGetProperty("id", out var id) ? id.GetString() : null,
                root.TryGetProperty("name", out var name) ? name.GetString() : null,
                null);
        }
        catch (JsonException)
        {
            return new KitSalesFacebookTestDto(false, null, null, "Graph trả payload không đọc được.");
        }
    }

    public async Task<IReadOnlyList<KitSalesFacebookPsidDto>> ListPsidsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PsidRow>(
            """
            SELECT l.id AS LeadId, l.business_id AS BusinessId, b.name AS BusinessName,
                   i.username AS Psid, src.url AS FacebookUrl, i.created_at AS CreatedAt
            FROM pack_sales.business_identity i
            INNER JOIN pack_sales.lead l
                ON l.business_id = i.business_id AND l.tenant_id = i.tenant_id
            INNER JOIN pack_sales.business b
                ON b.id = l.business_id AND b.tenant_id = l.tenant_id
            LEFT JOIN pack_sales.business_identity src
                ON src.business_id = i.business_id
               AND src.tenant_id = i.tenant_id
               AND src.identity_type = 'facebook'
               AND src.platform IN ('profile', 'page')
            WHERE i.tenant_id = @TenantId
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
              AND i.username IS NOT NULL
            ORDER BY i.created_at DESC
            """,
            new { TenantId = SalesPackDefinition.DedicatedTenantId });
        return rows.Select(MapPsid).ToList();
    }

    public async Task<KitSalesFacebookPsidDto?> GetPsidAsync(
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<PsidRow>(
            """
            SELECT l.id AS LeadId, l.business_id AS BusinessId, b.name AS BusinessName,
                   i.username AS Psid, src.url AS FacebookUrl, i.created_at AS CreatedAt
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business b
                ON b.id = l.business_id AND b.tenant_id = l.tenant_id
            INNER JOIN pack_sales.business_identity i
                ON i.business_id = l.business_id AND i.tenant_id = l.tenant_id
            LEFT JOIN pack_sales.business_identity src
                ON src.business_id = l.business_id
               AND src.tenant_id = l.tenant_id
               AND src.identity_type = 'facebook'
               AND src.platform IN ('profile', 'page')
            WHERE l.id = @LeadId
              AND l.tenant_id = @TenantId
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
              AND i.username IS NOT NULL
            LIMIT 1
            """,
            new { LeadId = leadId, TenantId = SalesPackDefinition.DedicatedTenantId });
        return row is null ? null : MapPsid(row);
    }

    public async Task<KitSalesFacebookPsidDto> AttachPsidAsync(
        Guid leadId,
        AttachKitSalesPsidRequest request,
        CancellationToken cancellationToken = default)
    {
        var psid = request.Psid?.Trim() ?? "";
        if (!PsidPattern.IsMatch(psid))
            throw new ArgumentException("PSID phải là dãy số Page-Scoped ID (sau khi họ nhắn Fanpage Novixa).");

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var lead = await conn.QuerySingleOrDefaultAsync<LeadLookup>(
            """
            SELECT l.id AS LeadId, l.business_id AS BusinessId, b.name AS BusinessName
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business b
                ON b.id = l.business_id AND b.tenant_id = l.tenant_id
            WHERE l.id = @LeadId AND l.tenant_id = @TenantId
            """,
            new { LeadId = leadId, TenantId = tenantId });
        if (lead is null)
            throw new InvalidOperationException("Không tìm thấy lead.");

        var conflict = await conn.QuerySingleOrDefaultAsync<LeadLookup>(
            """
            SELECT l.id AS LeadId, l.business_id AS BusinessId, b.name AS BusinessName
            FROM pack_sales.business_identity i
            INNER JOIN pack_sales.lead l
                ON l.business_id = i.business_id AND l.tenant_id = i.tenant_id
            INNER JOIN pack_sales.business b
                ON b.id = l.business_id AND b.tenant_id = l.tenant_id
            WHERE i.tenant_id = @TenantId
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
              AND i.username = @Psid
              AND l.id <> @LeadId
            LIMIT 1
            """,
            new { TenantId = tenantId, Psid = psid, LeadId = leadId });
        if (conflict is not null)
            throw new InvalidOperationException($"PSID đã gắn lead {conflict.BusinessName}.");

        var existingId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id
            FROM pack_sales.business_identity
            WHERE tenant_id = @TenantId
              AND business_id = @BusinessId
              AND identity_type = 'facebook'
              AND platform = 'messenger'
            ORDER BY created_at DESC
            LIMIT 1
            """,
            new { TenantId = tenantId, BusinessId = lead.BusinessId });

        if (existingId is null)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_sales.business_identity (
                    tenant_id, business_id, identity_type, platform, username, source
                )
                VALUES (
                    @TenantId, @BusinessId, 'facebook', 'messenger', @Psid, 'staff_psid'
                )
                """,
                new { TenantId = tenantId, BusinessId = lead.BusinessId, Psid = psid });
        }
        else
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_sales.business_identity
                SET username = @Psid, source = COALESCE(source, 'staff_psid')
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = existingId.Value, TenantId = tenantId, Psid = psid });
        }

        return await GetPsidAsync(leadId, cancellationToken)
            ?? throw new InvalidOperationException("Gắn PSID xong nhưng không đọc lại được.");
    }

    public async Task DetachPsidAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            DELETE FROM pack_sales.business_identity i
            USING pack_sales.lead l
            WHERE l.id = @LeadId
              AND l.tenant_id = @TenantId
              AND i.tenant_id = l.tenant_id
              AND i.business_id = l.business_id
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
            """,
            new { LeadId = leadId, TenantId = SalesPackDefinition.DedicatedTenantId });
    }

    private async Task<SettingsRow?> LoadRowAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SettingsRow>(
            """
            SELECT tenant_id AS TenantId, enabled AS Enabled, page_id AS PageId,
                   page_access_token AS PageAccessToken, verify_token AS VerifyToken,
                   app_secret AS AppSecret, me_link AS MeLink, page_url AS PageUrl,
                   phc_url AS PhcUrl
            FROM pack_sales.facebook_settings
            WHERE tenant_id = @TenantId
            """,
            new { TenantId = SalesPackDefinition.DedicatedTenantId });
    }

    private static KitSalesFacebookOptions Merge(KitSalesFacebookOptions file, SettingsRow? row)
    {
        var next = Clone(file);
        if (row is null)
            return next;

        next.Enabled = row.Enabled;
        next.PageId = FirstNonEmpty(row.PageId, file.PageId);
        next.PageAccessToken = FirstNonEmpty(row.PageAccessToken, file.PageAccessToken);
        next.VerifyToken = FirstNonEmpty(row.VerifyToken, file.VerifyToken);
        next.AppSecret = FirstNonEmpty(row.AppSecret, file.AppSecret);
        next.MeLink = FirstNonEmpty(row.MeLink, file.MeLink) ?? next.MeLink;
        next.PageUrl = FirstNonEmpty(row.PageUrl, file.PageUrl) ?? next.PageUrl;
        next.PhcUrl = FirstNonEmpty(row.PhcUrl, file.PhcUrl) ?? next.PhcUrl;
        return next;
    }

    private static KitSalesFacebookSettingsDto Mask(KitSalesFacebookOptions o, string webhookUrl)
    {
        var token = o.PageAccessToken?.Trim();
        var last4 = string.IsNullOrWhiteSpace(token) || token.Length < 4
            ? null
            : token[^4..];
        var sendReady = o.Enabled && !string.IsNullOrWhiteSpace(token);
        return new KitSalesFacebookSettingsDto(
            o.Enabled,
            !string.IsNullOrWhiteSpace(token),
            last4,
            !string.IsNullOrWhiteSpace(o.VerifyToken),
            !string.IsNullOrWhiteSpace(o.AppSecret),
            TrimOrNull(o.PageId),
            o.MeLink,
            o.PageUrl,
            o.PhcUrl,
            webhookUrl,
            sendReady);
    }

    private static KitSalesFacebookOptions Clone(KitSalesFacebookOptions src) =>
        new()
        {
            Enabled = src.Enabled,
            PageId = src.PageId,
            PageAccessToken = src.PageAccessToken,
            VerifyToken = src.VerifyToken,
            AppSecret = src.AppSecret,
            MeLink = src.MeLink,
            PageUrl = src.PageUrl,
            PhcUrl = src.PhcUrl,
        };

    private static KitSalesFacebookPsidDto MapPsid(PsidRow row) =>
        new(
            row.LeadId,
            row.BusinessId,
            row.BusinessName,
            row.Psid,
            row.FacebookUrl,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)));

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a.Trim() : TrimOrNull(b);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class LeadLookup
    {
        public Guid LeadId { get; init; }
        public Guid BusinessId { get; init; }
        public string BusinessName { get; init; } = "";
    }

    private sealed class SettingsRow
    {
        public Guid TenantId { get; init; }
        public bool Enabled { get; init; }
        public string? PageId { get; init; }
        public string? PageAccessToken { get; init; }
        public string? VerifyToken { get; init; }
        public string? AppSecret { get; init; }
        public string? MeLink { get; init; }
        public string? PageUrl { get; init; }
        public string? PhcUrl { get; init; }
    }

    private sealed class PsidRow
    {
        public Guid LeadId { get; init; }
        public Guid BusinessId { get; init; }
        public string BusinessName { get; init; } = "";
        public string Psid { get; init; } = "";
        public string? FacebookUrl { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
