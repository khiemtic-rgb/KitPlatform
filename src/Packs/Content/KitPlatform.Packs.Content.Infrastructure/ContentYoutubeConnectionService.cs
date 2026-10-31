using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentYoutubeConnectionService : IContentYoutubeConnectionService
{
    internal const string ChannelType = "youtube_channel";
    internal const string LinkKey = "youtube";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private static readonly string[] VideoExt = [".mp4", ".mov", ".webm", ".m4v"];

    private readonly ContentYoutubeClient _yt;
    private readonly ContentRepository _repo;
    private readonly IContentBrandService _brands;
    private readonly ContentOptions _options;
    private readonly IHostEnvironment _env;

    public ContentYoutubeConnectionService(
        ContentYoutubeClient yt,
        ContentRepository repo,
        IContentBrandService brands,
        IOptions<ContentOptions> options,
        IHostEnvironment env)
    {
        _yt = yt;
        _repo = repo;
        _brands = brands;
        _options = options.Value;
        _env = env;
    }

    public async Task<ContentYoutubeStartDto> StartAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await _repo.GetBrandAsync(brandId, ct)
                    ?? throw new InvalidOperationException("Thương hiệu không tồn tại.");
        var cfg = _yt.Resolve();
        if (string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            throw new InvalidOperationException(
                "Chưa cấu hình Google OAuth — đặt YOUTUBE_CLIENT_ID và YOUTUBE_CLIENT_SECRET rồi khởi động lại API.");
        await _repo.PurgeOauthPendingAsync(ct);
        var state = Guid.NewGuid().ToString("N");
        await _repo.PutOauthPendingAsync("yt:" + state, "state", brand.Id, cfg.RedirectUri, "[]",
            DateTimeOffset.UtcNow.Add(Ttl), ct);
        return new ContentYoutubeStartDto(_yt.BuildAuthUrl(cfg, state), state);
    }

    public async Task<ContentChannelTargetDto> CompleteAsync(string code, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            throw new InvalidOperationException("Thiếu mã OAuth từ Google.");
        var pending = await _repo.GetOauthPendingAsync("yt:" + state.Trim(), ct);
        if (pending is null || pending.Kind != "state")
            throw new InvalidOperationException("Phiên YouTube hết hạn — bấm Kết nối lại.");
        await _repo.DeleteOauthPendingAsync(pending.Id, ct);

        var refresh = await _yt.ExchangeCodeAsync(code.Trim(), pending.RedirectUri ?? "", ct);
        var access = await _yt.AccessTokenAsync(refresh, ct);
        var (channelId, title) = await _yt.GetMyChannelAsync(access, ct);

        var channels = await _repo.ListChannelsAsync(pending.BrandId, ct);
        var existing = channels.FirstOrDefault(c => c.ChannelType == ChannelType && c.ExternalId == channelId)
                       ?? channels.FirstOrDefault(c => c.ChannelType == ChannelType && string.IsNullOrWhiteSpace(c.ExternalId));
        var configJson = ApplyLink(existing?.ConfigJson, "CONNECTED", title, null);
        return await _brands.UpsertChannelAsync(
            pending.BrandId,
            new UpsertContentChannelTargetRequest(
                existing?.Code ?? ("yt-" + channelId),
                title,
                ChannelType,
                channelId,
                configJson,
                existing?.SecretRef,
                refresh,
                true,
                existing?.SortOrder ?? 50),
            ct);
    }

    public async Task<ContentYoutubeVerifyDto> VerifyAsync(Guid channelId, CancellationToken ct = default)
    {
        var row = await _repo.GetChannelAsync(channelId, ct)
                  ?? throw new InvalidOperationException("Kênh không tồn tại.");
        if (row.ChannelType != ChannelType)
            throw new InvalidOperationException("Chỉ kiểm tra được kênh YouTube.");
        var refresh = ContentTargetSecrets.ExtractStored(row.ConfigJson);
        if (string.IsNullOrWhiteSpace(refresh))
            return await Mark(row, "NEED_RECONNECT", "Chưa kết nối YouTube.", null, ct);
        try
        {
            var access = await _yt.AccessTokenAsync(refresh, ct);
            var (id, title) = await _yt.GetMyChannelAsync(access, ct);
            return await Mark(row, "CONNECTED", null, title, ct, id);
        }
        catch (InvalidOperationException ex)
        {
            return await Mark(row, "NEED_RECONNECT", ex.Message, null, ct);
        }
    }

    public async Task<ContentYoutubeVideoDto?> UploadVideoAsync(
        Guid topicId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var topic = await _repo.GetTopicAsync(topicId, ct);
        if (topic is null) return null;
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (!VideoExt.Contains(ext))
            throw new InvalidOperationException("Chỉ nhận video MP4 / MOV / WEBM.");
        var safe = $"{Guid.NewGuid():N}{ext}";
        var rel = $"video/{topicId:N}/{safe}";
        var path = Path.Combine(AssetRoot(), rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        long size;
        await using (var fs = File.Create(path))
        {
            await content.CopyToAsync(fs, ct);
            size = fs.Length;
        }

        var ctype = ext switch { ".mov" => "video/quicktime", ".webm" => "video/webm", _ => "video/mp4" };
        var id = await _repo.InsertAssetAsync(new ContentRepository.AssetRow
        {
            TopicId = topicId,
            Kind = "video",
            FileName = Path.GetFileName(fileName ?? safe),
            ContentType = ctype,
            StoragePath = rel,
            IsSelected = false,
            MetaJson = JsonSerializer.Serialize(new { sizeBytes = size, source = "manual_upload" }),
        }, ct);
        return new ContentYoutubeVideoDto(id, topicId, Path.GetFileName(fileName ?? safe), size);
    }

    private async Task<ContentYoutubeVerifyDto> Mark(
        ContentRepository.ChannelRow row, string status, string? error, string? title,
        CancellationToken ct, string? channelId = null)
    {
        row.ConfigJson = ApplyLink(row.ConfigJson, status, title, error);
        if (!string.IsNullOrWhiteSpace(title)) row.Name = title;
        await _repo.UpsertChannelAsync(row.BrandId, row, ct);
        var ok = status == "CONNECTED";
        return new ContentYoutubeVerifyDto(
            ok, status, channelId ?? row.ExternalId, title,
            ok ? "YouTube còn quyền đăng video." : error ?? "Cần kết nối lại YouTube.",
            DateTimeOffset.UtcNow);
    }

    internal static string ApplyLink(string? configJson, string status, string? title, string? error)
    {
        JsonObject node;
        try { node = JsonNode.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson) as JsonObject ?? new(); }
        catch { node = new(); }
        var link = node[LinkKey] as JsonObject ?? new JsonObject();
        link["status"] = status;
        link["channelTitle"] = title ?? link["channelTitle"]?.GetValue<string?>();
        link["lastError"] = error;
        link["lastVerifiedAt"] = DateTimeOffset.UtcNow.ToString("O");
        if (status == "CONNECTED" && link["connectedAt"] is null)
            link["connectedAt"] = DateTimeOffset.UtcNow.ToString("O");
        node[LinkKey] = link;
        return node.ToJsonString();
    }

    private string AssetRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.AssetRoot) ? "App_Data/content-assets" : _options.AssetRoot;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }
}
