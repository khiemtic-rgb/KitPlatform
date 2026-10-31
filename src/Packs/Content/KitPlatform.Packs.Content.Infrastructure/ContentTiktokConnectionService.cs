using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentTiktokConnectionService : IContentTiktokConnectionService
{
    internal const string ChannelType = "tiktok_channel";
    internal const string LinkKey = "tiktok";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly ContentTiktokClient _tt;
    private readonly ContentRepository _repo;
    private readonly IContentBrandService _brands;

    public ContentTiktokConnectionService(ContentTiktokClient tt, ContentRepository repo, IContentBrandService brands)
    {
        _tt = tt;
        _repo = repo;
        _brands = brands;
    }

    public async Task<ContentTiktokStartDto> StartAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await _repo.GetBrandAsync(brandId, ct)
                    ?? throw new InvalidOperationException("Thương hiệu không tồn tại.");
        var cfg = _tt.Resolve();
        if (string.IsNullOrWhiteSpace(cfg.ClientKey) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new InvalidOperationException(
                "Chưa cấu hình TikTok — đặt TIKTOK_CLIENT_KEY và TIKTOK_CLIENT_SECRET rồi khởi động lại API.");
        await _repo.PurgeOauthPendingAsync(ct);
        var state = Guid.NewGuid().ToString("N");
        await _repo.PutOauthPendingAsync("tt:" + state, "state", brand.Id, cfg.RedirectUri, "[]",
            DateTimeOffset.UtcNow.Add(Ttl), ct);
        return new ContentTiktokStartDto(_tt.BuildAuthUrl(cfg, state), state);
    }

    public async Task<ContentChannelTargetDto> CompleteAsync(string code, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("Thiếu mã OAuth từ TikTok.");
        var pending = await _repo.GetOauthPendingAsync("tt:" + state.Trim(), ct);
        if (pending is null || pending.Kind != "state")
            throw new InvalidOperationException("Phiên TikTok hết hạn — bấm Kết nối lại.");
        await _repo.DeleteOauthPendingAsync(pending.Id, ct);

        var tokens = await _tt.ExchangeCodeAsync(code.Trim(), pending.RedirectUri ?? "", ct);
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            throw new InvalidOperationException("TikTok không trả refresh token.");
        string? name = null;
        try { name = await _tt.GetDisplayNameAsync(tokens.AccessToken, ct); }
        catch (InvalidOperationException) { /* tên chỉ để hiển thị */ }
        var title = string.IsNullOrWhiteSpace(name) ? "TikTok" : name!;

        var channels = await _repo.ListChannelsAsync(pending.BrandId, ct);
        var existing = channels.FirstOrDefault(c => c.ChannelType == ChannelType && c.ExternalId == tokens.OpenId)
                       ?? channels.FirstOrDefault(c => c.ChannelType == ChannelType && string.IsNullOrWhiteSpace(c.ExternalId));
        var configJson = ApplyLink(existing?.ConfigJson, "CONNECTED", title, null);
        return await _brands.UpsertChannelAsync(
            pending.BrandId,
            new UpsertContentChannelTargetRequest(
                existing?.Code ?? ("tt-" + tokens.OpenId),
                title,
                ChannelType,
                tokens.OpenId,
                configJson,
                existing?.SecretRef,
                tokens.RefreshToken,
                true,
                existing?.SortOrder ?? 60),
            ct);
    }

    public async Task<ContentTiktokVerifyDto> VerifyAsync(Guid channelId, CancellationToken ct = default)
    {
        var row = await _repo.GetChannelAsync(channelId, ct)
                  ?? throw new InvalidOperationException("Kênh không tồn tại.");
        if (row.ChannelType != ChannelType)
            throw new InvalidOperationException("Chỉ kiểm tra được kênh TikTok.");
        var refresh = ContentTargetSecrets.ExtractStored(row.ConfigJson);
        if (string.IsNullOrWhiteSpace(refresh))
            return await Mark(row, "NEED_RECONNECT", "Chưa kết nối TikTok.", null, ct);
        try
        {
            var tokens = await _tt.RefreshAsync(refresh, ct);
            if (!string.IsNullOrWhiteSpace(tokens.RefreshToken) && tokens.RefreshToken != refresh)
                row.ConfigJson = ContentTargetSecrets.MergeConfig(row.ConfigJson, row.ConfigJson, tokens.RefreshToken);
            string? name = null;
            try { name = await _tt.GetDisplayNameAsync(tokens.AccessToken, ct); }
            catch (InvalidOperationException) { /* bỏ qua */ }
            return await Mark(row, "CONNECTED", null, name, ct, tokens.OpenId);
        }
        catch (InvalidOperationException ex)
        {
            return await Mark(row, "NEED_RECONNECT", ex.Message, null, ct);
        }
    }

    private async Task<ContentTiktokVerifyDto> Mark(
        ContentRepository.ChannelRow row, string status, string? error, string? title,
        CancellationToken ct, string? openId = null)
    {
        row.ConfigJson = ApplyLink(row.ConfigJson, status, title, error);
        if (!string.IsNullOrWhiteSpace(title)) row.Name = title;
        await _repo.UpsertChannelAsync(row.BrandId, row, ct);
        var ok = status == "CONNECTED";
        return new ContentTiktokVerifyDto(
            ok, status, openId ?? row.ExternalId, title,
            ok ? "TikTok còn quyền gửi video vào hộp nháp." : error ?? "Cần kết nối lại TikTok.",
            DateTimeOffset.UtcNow);
    }

    internal static string ApplyLink(string? configJson, string status, string? title, string? error)
    {
        JsonObject node;
        try { node = JsonNode.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson) as JsonObject ?? new(); }
        catch { node = new(); }
        var link = node[LinkKey] as JsonObject ?? new JsonObject();
        link["status"] = status;
        link["displayName"] = title ?? link["displayName"]?.GetValue<string?>();
        link["lastError"] = error;
        link["lastVerifiedAt"] = DateTimeOffset.UtcNow.ToString("O");
        if (status == "CONNECTED" && link["connectedAt"] is null)
            link["connectedAt"] = DateTimeOffset.UtcNow.ToString("O");
        node[LinkKey] = link;
        return node.ToJsonString();
    }
}
