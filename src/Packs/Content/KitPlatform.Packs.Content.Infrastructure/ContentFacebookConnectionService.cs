using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentFacebookConnectionService : IContentFacebookConnectionService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ContentFacebookClient _fb;
    private readonly ContentRepository _repo;
    private readonly IContentBrandService _brands;

    public ContentFacebookConnectionService(
        ContentFacebookClient fb,
        ContentRepository repo,
        IContentBrandService brands)
    {
        _fb = fb;
        _repo = repo;
        _brands = brands;
    }

    public async Task<ContentFacebookStartDto> StartAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var brand = await _repo.GetBrandAsync(brandId, cancellationToken)
                    ?? throw new InvalidOperationException("Thương hiệu không tồn tại.");
        var cfg = await _fb.ResolveAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(cfg.AppId) || string.IsNullOrWhiteSpace(cfg.AppSecret))
            throw new InvalidOperationException("Chưa cấu hình Facebook App — Model AI → Facebook.");

        await _repo.PurgeOauthPendingAsync(cancellationToken);
        var state = Guid.NewGuid().ToString("N");
        await _repo.PutOauthPendingAsync(
            "st:" + state,
            "state",
            brand.Id,
            cfg.RedirectUri,
            "[]",
            DateTimeOffset.UtcNow.Add(Ttl),
            cancellationToken);

        var url =
            $"https://www.facebook.com/{ContentFacebookConfigParser.GraphVersion}/dialog/oauth" +
            $"?client_id={Uri.EscapeDataString(cfg.AppId)}" +
            $"&redirect_uri={Uri.EscapeDataString(cfg.RedirectUri)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&scope={Uri.EscapeDataString(ContentFacebookConfigParser.Scopes)}" +
            "&response_type=code";
        return new ContentFacebookStartDto(url, state);
    }

    public async Task<ContentFacebookPendingDto> CompleteAsync(
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("Thiếu mã OAuth từ Facebook.");
        var pending = await _repo.GetOauthPendingAsync("st:" + state.Trim(), cancellationToken);
        if (pending is null || pending.Kind != "state")
            throw new InvalidOperationException("Phiên Facebook hết hạn — bấm Kết nối lại.");
        await _repo.DeleteOauthPendingAsync(pending.Id, cancellationToken);

        var shortLived = await _fb.ExchangeCodeAsync(code.Trim(), pending.RedirectUri ?? "", cancellationToken);
        var longLived = await _fb.ExchangeLongLivedAsync(shortLived, cancellationToken);
        var pages = await _fb.ListPagesAsync(longLived, cancellationToken);
        if (pages.Count == 0)
        {
            throw new InvalidOperationException(
                "Facebook không trả Page nào. Tài khoản phải là quản trị Page, và app cần quyền pages_show_list + business_management.");
        }

        var sessionId = Guid.NewGuid().ToString("N");
        await _repo.PutOauthPendingAsync(
            "ss:" + sessionId,
            "session",
            pending.BrandId,
            pending.RedirectUri,
            JsonSerializer.Serialize(pages, JsonOpts),
            DateTimeOffset.UtcNow.Add(Ttl),
            cancellationToken);

        return new ContentFacebookPendingDto(
            sessionId,
            pending.BrandId,
            pages.Select(p => new ContentFacebookPageOptionDto(p.Id, p.Name)).ToList());
    }

    public async Task<ContentFacebookPendingDto?> GetPendingAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;
        var session = await _repo.GetOauthPendingAsync("ss:" + sessionId.Trim(), cancellationToken);
        if (session is null || session.Kind != "session")
            return null;
        var pages = ParsePages(session.PagesJson);
        return new ContentFacebookPendingDto(
            sessionId.Trim(),
            session.BrandId,
            pages.Select(p => new ContentFacebookPageOptionDto(p.Id, p.Name)).ToList());
    }

    public async Task<ContentChannelTargetDto> SelectPageAsync(
        string sessionId,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        var session = await _repo.GetOauthPendingAsync("ss:" + sessionId.Trim(), cancellationToken);
        if (session is null || session.Kind != "session")
            throw new InvalidOperationException("Phiên chọn Page hết hạn — Kết nối Facebook lại.");
        var pages = ParsePages(session.PagesJson);
        var page = pages.FirstOrDefault(p => p.Id == pageId.Trim())
                   ?? throw new InvalidOperationException("Page không nằm trong phiên vừa cấp quyền.");

        var channels = await _repo.ListChannelsAsync(session.BrandId, cancellationToken);
        var existing = channels.FirstOrDefault(c =>
                           c.ChannelType == "facebook_page"
                           && string.Equals(c.ExternalId, page.Id, StringComparison.Ordinal))
                       ?? channels.FirstOrDefault(c =>
                           c.ChannelType == "facebook_page"
                           && string.IsNullOrWhiteSpace(c.ExternalId));

        var code = existing?.Code ?? ("fb-" + page.Id);
        var link = new ContentFacebookLinkState
        {
            Status = "CONNECTED",
            PageId = page.Id,
            PageName = page.Name,
            Scopes = ContentFacebookConfigParser.Scopes.Split(',').ToList(),
            ConnectedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            LastError = null,
            TokenType = "page",
        };
        var configJson = ContentFacebookLink.Apply(existing?.ConfigJson, link);

        var saved = await _brands.UpsertChannelAsync(
            session.BrandId,
            new UpsertContentChannelTargetRequest(
                code,
                page.Name,
                "facebook_page",
                page.Id,
                configJson,
                existing?.SecretRef,
                page.AccessToken,
                true,
                existing?.SortOrder ?? 40),
            cancellationToken);
        await _repo.DeleteOauthPendingAsync(session.Id, cancellationToken);
        return saved;
    }

    public async Task<ContentFacebookVerifyDto> VerifyAsync(
        Guid channelId,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetChannelAsync(channelId, cancellationToken)
                  ?? throw new InvalidOperationException("Kênh không tồn tại.");
        if (row.ChannelType != "facebook_page")
            throw new InvalidOperationException("Chỉ kiểm tra được Fanpage.");

        var token = ContentTargetSecrets.ExtractStored(row.ConfigJson);
        var pageId = row.ExternalId?.Trim();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(pageId))
        {
            await WriteLinkAsync(row, status: "NEED_RECONNECT", error: "Chưa có Page token.", ct: cancellationToken);
            return new ContentFacebookVerifyDto(false, "NEED_RECONNECT", pageId, null, "Chưa kết nối Facebook.", null);
        }

        var inspect = await _fb.InspectPageAsync(pageId, token, cancellationToken);
        if (!inspect.Ok)
        {
            await WriteLinkAsync(row, status: "NEED_RECONNECT", error: inspect.Error, pageName: inspect.PageName, ct: cancellationToken);
            return new ContentFacebookVerifyDto(
                false, "NEED_RECONNECT", pageId, inspect.PageName,
                inspect.Error ?? "Token không còn hiệu lực — Kết nối lại Facebook.",
                DateTimeOffset.UtcNow);
        }

        await WriteLinkAsync(row, status: "CONNECTED", error: null, pageName: inspect.PageName, ct: cancellationToken);
        return new ContentFacebookVerifyDto(
            true, "CONNECTED", pageId, inspect.PageName, "Facebook còn quyền đăng bài.", DateTimeOffset.UtcNow);
    }

    public async Task<ContentChannelTargetDto> DisconnectAsync(
        Guid channelId,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetChannelAsync(channelId, cancellationToken)
                  ?? throw new InvalidOperationException("Kênh không tồn tại.");
        var link = ContentFacebookLink.Parse(row.ConfigJson);
        link.Status = "DISCONNECTED";
        link.LastError = null;
        var configJson = ContentFacebookLink.Apply(row.ConfigJson, link);
        return await _brands.UpsertChannelAsync(
            row.BrandId,
            new UpsertContentChannelTargetRequest(
                row.Code,
                row.Name,
                row.ChannelType,
                row.ExternalId,
                configJson,
                null,
                "",
                row.IsActive,
                row.SortOrder),
            cancellationToken);
    }

    public async Task MarkNeedReconnectAsync(
        Guid channelId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetChannelAsync(channelId, cancellationToken);
        if (row is null || row.ChannelType != "facebook_page") return;
        await WriteLinkAsync(row, status: "NEED_RECONNECT", error: error, ct: cancellationToken);
    }

    private async Task WriteLinkAsync(
        ContentRepository.ChannelRow row,
        string status,
        string? error,
        string? pageName = null,
        CancellationToken ct = default)
    {
        var link = ContentFacebookLink.Parse(row.ConfigJson);
        link.Status = status;
        link.LastError = error;
        link.LastVerifiedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(pageName))
            link.PageName = pageName;
        if (!string.IsNullOrWhiteSpace(row.ExternalId))
            link.PageId = row.ExternalId;
        row.ConfigJson = ContentFacebookLink.Apply(row.ConfigJson, link);
        await _repo.UpsertChannelAsync(row.BrandId, row, ct);
    }

    private static List<FacebookPageToken> ParsePages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<FacebookPageToken>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
