using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Đọc lịch Fanpage và YouTube (không sửa, không xoá bài trên nền tảng) rồi ghi pack_content.platform_post.
/// </summary>
internal sealed class ContentPlatformSyncService : IContentPlatformSyncService
{
    private readonly ContentRepository _repo;
    private readonly ContentPlatformPostStore _store;
    private readonly ContentYoutubeClient _youtube;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ContentPlatformSyncService> _log;

    public ContentPlatformSyncService(
        ContentRepository repo,
        ContentPlatformPostStore store,
        ContentYoutubeClient youtube,
        IHttpClientFactory http,
        ILogger<ContentPlatformSyncService> log)
    {
        _repo = repo;
        _store = store;
        _youtube = youtube;
        _http = http;
        _log = log;
    }

    public async Task<ContentPlatformSyncReportDto> SyncAllAsync(CancellationToken ct)
    {
        var brands = await _repo.ListBrandsAsync(true, ct);
        var channels = new List<ContentPlatformSyncChannelDto>();
        var warnings = new List<string>();
        var upserted = 0;
        var matched = 0;
        var unmatched = 0;
        foreach (var brand in brands)
        {
            var report = await SyncBrandAsync(brand.Id, ct);
            if (report.MigrationRequired) return report;
            channels.AddRange(report.ByChannel);
            warnings.AddRange(report.Warnings);
            upserted += report.Upserted;
            matched += report.Matched;
            unmatched += report.Unmatched;
        }
        return new ContentPlatformSyncReportDto(channels.Count, upserted, matched, unmatched, channels, warnings, false);
    }

    public async Task<ContentPlatformSyncReportDto> SyncBrandAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.ListSnapshotsAsync(brandId, cancellationToken);
        }
        catch (Exception ex) when (ContentPlatformPostStore.IsMissingTable(ex))
        {
            return new ContentPlatformSyncReportDto(
                0, 0, 0, 0, [], [ContentPlatformRules.MigrationHint], true);
        }

        var channels = (await _repo.ListChannelsAsync(brandId, cancellationToken))
            .Select(c =>
            {
                c.ChannelType = (c.ChannelType ?? "").Trim().ToLowerInvariant();
                return c;
            })
            .Where(c => c.IsActive && ContentPlatformRules.Applies(c.ChannelType))
            .ToList();
        var rows = new List<ContentPlatformSyncChannelDto>();
        var warnings = new List<string>();
        if (channels.Count == 0)
            warnings.Add("Chưa có Fanpage hoặc YouTube đang bật.");

        var upserted = 0;
        var matched = 0;
        var unmatched = 0;
        foreach (var channel in channels)
        {
            var one = await SyncChannelAsync(brandId, channel, cancellationToken);
            rows.Add(one);
            upserted += one.Upserted;
            matched += one.Matched;
            unmatched += one.Unmatched;
            if (!string.IsNullOrWhiteSpace(one.Warning)) warnings.Add(one.Warning);
            _log.LogInformation(
                "Đồng bộ lịch kênh {ChannelId} ({Connector}): {Upserted} bài, {Matched} đã ghép, {Unmatched} chưa ghép.",
                channel.Id, channel.ChannelType, one.Upserted, one.Matched, one.Unmatched);
        }
        return new ContentPlatformSyncReportDto(rows.Count, upserted, matched, unmatched, rows, warnings, false);
    }

    public async Task<IReadOnlyList<ContentPlatformPostDto>> ListAsync(
        Guid brandId,
        bool unmatchedOnly,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _store.ListAsync(brandId, unmatchedOnly, cancellationToken);
        }
        catch (Exception ex) when (ContentPlatformPostStore.IsMissingTable(ex))
        {
            throw new InvalidOperationException(ContentPlatformRules.MigrationHint);
        }
    }

    public async Task<ContentPlatformPostDto> MatchAsync(Guid id, Guid? topicId, CancellationToken cancellationToken = default)
    {
        ContentPlatformPostDto? row;
        try
        {
            row = await _store.GetAsync(id, cancellationToken);
        }
        catch (Exception ex) when (ContentPlatformPostStore.IsMissingTable(ex))
        {
            throw new InvalidOperationException(ContentPlatformRules.MigrationHint);
        }
        if (row is null) throw new InvalidOperationException("Không thấy bài nền tảng.");
        if (topicId is Guid tid)
        {
            var topic = await _repo.GetTopicAsync(tid, cancellationToken)
                        ?? throw new InvalidOperationException("Không thấy bài viết.");
            if (topic.BrandId != row.BrandId)
                throw new InvalidOperationException("Bài viết không thuộc thương hiệu của kênh này.");
        }
        await _store.SetMatchAsync(id, topicId, cancellationToken);
        return await _store.GetAsync(id, cancellationToken) ?? row;
    }

    public async Task<ContentPlatformPostDto> MarkOutsideAsync(
        Guid topicId,
        Guid channelId,
        DateTimeOffset publishAt,
        string? kind,
        CancellationToken cancellationToken = default)
    {
        if (topicId == Guid.Empty || channelId == Guid.Empty)
            throw new InvalidOperationException("Thiếu bài viết hoặc kênh.");
        try
        {
            return await _store.MarkOutsideAsync(topicId, channelId, publishAt, kind, cancellationToken);
        }
        catch (Exception ex) when (ContentPlatformPostStore.IsMissingTable(ex))
        {
            throw new InvalidOperationException(ContentPlatformRules.MigrationHint);
        }
    }

    private async Task<ContentPlatformSyncChannelDto> SyncChannelAsync(
        Guid brandId,
        ContentRepository.ChannelRow channel,
        CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(channel.Name) ? channel.Code : channel.Name;
        var token = ContentTargetSecrets.ExtractStored(channel.ConfigJson);
        if (string.IsNullOrWhiteSpace(token))
            return new ContentPlatformSyncChannelDto(name, channel.ChannelType, 0, 0, 0, $"Kênh «{name}» chưa có token để đọc lịch.");

        var now = DateTimeOffset.UtcNow;
        List<ContentPlatformIncoming> items;
        bool complete;
        string? error;
        if (channel.ChannelType == "youtube_channel")
            (items, complete, error) = await ReadYoutubeAsync(token, now, ct);
        else
        {
            var pageId = channel.ExternalId?.Trim();
            if (string.IsNullOrWhiteSpace(pageId) || pageId.Any(c => !char.IsDigit(c)))
                return new ContentPlatformSyncChannelDto(name, channel.ChannelType, 0, 0, 0, $"Fanpage «{name}» chưa có Page ID.");
            (items, complete, error) = await ReadFacebookAsync(pageId, token, now, ct);
        }

        if (items.Count == 0 && !complete)
            return new ContentPlatformSyncChannelDto(name, channel.ChannelType, 0, 0, 0, $"«{name}»: {error}");

        IReadOnlyList<ContentPlatformMatchCandidate> candidates;
        try
        {
            candidates = await _store.LoadCandidatesAsync(brandId, channel.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ContentPlatformSyncChannelDto(name, channel.ChannelType, 0, 0, 0, Safe(ex.Message));
        }

        var matched = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var topicId = ContentPlatformRules.Match(item.ExternalId, item.Title, item.Caption, candidates);
            if (topicId is Guid id) matched[item.ExternalId] = id;
        }

        try
        {
            var (total, matchedCount) = await _store.ReplaceChannelAsync(
                channel.Id, channel.ChannelType, items, matched, complete, ct);
            var warning = complete ? null : $"«{name}»: đọc chưa đủ ({error}). Giữ các bài cũ.";
            return new ContentPlatformSyncChannelDto(
                name, channel.ChannelType, items.Count, matchedCount, total - matchedCount, warning);
        }
        catch (Exception ex) when (ContentPlatformPostStore.IsMissingTable(ex))
        {
            return new ContentPlatformSyncChannelDto(name, channel.ChannelType, 0, 0, 0, ContentPlatformRules.MigrationHint);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ContentPlatformSyncChannelDto(name, channel.ChannelType, 0, 0, 0, Safe(ex.Message));
        }
    }

    private async Task<(List<ContentPlatformIncoming> Items, bool Complete, string? Error)> ReadFacebookAsync(
        string pageId,
        string token,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var since = now.AddDays(-30);
        var sinceUnix = since.ToUnixTimeSeconds().ToString();
        var found = new List<ContentPlatformIncoming>();
        var complete = true;
        string? error = null;

        async Task Pull(string edge, string fields, string? extra)
        {
            try
            {
                var pages = await GetGraphPagesAsync(pageId, edge, fields, extra, token, ct);
                foreach (var body in pages)
                    found.AddRange(ContentPlatformReaders.ParseFacebookEdge(edge, body, now, since));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                complete = false;
                error ??= Safe(ex.Message);
            }
        }

        await Pull("scheduled_posts", "id,message,scheduled_publish_time,permalink_url,created_time,is_published,published", null);
        await Pull("published_posts", "id,message,created_time,permalink_url,is_published", "since=" + sinceUnix);
        await Pull("video_reels", "id,description,title,permalink_url,created_time,scheduled_publish_time,published,is_published", null);
        await Pull("videos", "id,title,description,permalink_url,created_time,published,is_published,scheduled_publish_time,length", null);
        return (ContentPlatformReaders.Merge(found).ToList(), complete, error);
    }

    private async Task<List<string>> GetGraphPagesAsync(
        string pageId,
        string edge,
        string fields,
        string? extra,
        string token,
        CancellationToken ct)
    {
        var pages = new List<string>();
        var url =
            $"https://graph.facebook.com/{ContentFacebookConfigParser.GraphVersion}/{Uri.EscapeDataString(pageId)}/{edge}" +
            $"?fields={Uri.EscapeDataString(fields)}&limit=50&access_token={Uri.EscapeDataString(token)}";
        if (!string.IsNullOrWhiteSpace(extra)) url += "&" + extra;
        string? next = url;
        var client = _http.CreateClient("content-facebook");
        for (var i = 0; i < 5 && !string.IsNullOrWhiteSpace(next); i++)
        {
            using var res = await client.GetAsync(next, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(GraphMessage(body) ?? $"Meta trả {(int)res.StatusCode}.");
            pages.Add(body);
            next = ContentPlatformReaders.NextGraphPage(body);
        }
        return pages;
    }

    private async Task<(List<ContentPlatformIncoming> Items, bool Complete, string? Error)> ReadYoutubeAsync(
        string refresh,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            var access = await _youtube.AccessTokenAsync(refresh, ct);
            var channelJson = await _youtube.GetJsonAsync(access, "channels?part=contentDetails&mine=true", ct);
            var playlist = ContentPlatformReaders.ParseUploadsPlaylistId(channelJson);
            if (string.IsNullOrWhiteSpace(playlist))
                return ([], true, "Kênh YouTube chưa có playlist uploads.");

            var ids = new List<string>();
            string? page = null;
            for (var i = 0; i < 4; i++)
            {
                var query = $"playlistItems?part=contentDetails&maxResults=50&playlistId={Uri.EscapeDataString(playlist)}"
                            + (page is null ? "" : $"&pageToken={Uri.EscapeDataString(page)}");
                var json = await _youtube.GetJsonAsync(access, query, ct);
                ids.AddRange(ContentPlatformReaders.ParsePlaylistVideoIds(json));
                page = ContentPlatformReaders.NextPageToken(json);
                if (string.IsNullOrWhiteSpace(page)) break;
            }

            var since = now.AddDays(-30);
            var items = new List<ContentPlatformIncoming>();
            foreach (var chunk in ids.Distinct(StringComparer.Ordinal).Chunk(50))
            {
                var query = "videos?part=status,snippet&id=" + string.Join(",", chunk.Select(Uri.EscapeDataString));
                var json = await _youtube.GetJsonAsync(access, query, ct);
                items.AddRange(ContentPlatformReaders.ParseYoutubeVideos(json, now, since));
            }
            return (items, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ([], false, Safe(ex.Message));
        }
    }

    private static string? GraphMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var m)
                && m.ValueKind == JsonValueKind.String)
                return m.GetString();
        }
        catch
        {
            /* không có JSON */
        }
        return null;
    }

    internal static string Safe(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Không đọc được nền tảng.";
        var cut = message;
        var i = cut.IndexOf("access_token", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) cut = cut[..i] + "…";
        i = cut.IndexOf("refresh_token", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) cut = cut[..i] + "…";
        return cut.Length > 240 ? cut[..240] : cut;
    }
}

/// <summary>Mỗi 30 phút đọc lịch Fanpage và YouTube. Không đăng, không sửa bài trên nền tảng.</summary>
internal sealed class ContentPlatformSyncWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly ILogger<ContentPlatformSyncWorker> _log;

    public ContentPlatformSyncWorker(
        IServiceScopeFactory scopes,
        IOptions<ContentOptions> options,
        ILogger<ContentPlatformSyncWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Worker đồng bộ lịch nền tảng tắt cùng worker Content.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var sync = scope.ServiceProvider.GetRequiredService<IContentPlatformSyncService>();
                var report = await sync.SyncAllAsync(stoppingToken);
                if (report.MigrationRequired)
                    _log.LogWarning("{Hint}", ContentPlatformRules.MigrationHint);
                else
                    _log.LogInformation(
                        "Đồng bộ lịch nền tảng: {Channels} kênh, {Upserted} bài, {Matched} đã ghép, {Unmatched} chưa ghép.",
                        report.Channels, report.Upserted, report.Matched, report.Unmatched);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Vòng đồng bộ lịch nền tảng lỗi.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
