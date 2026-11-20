using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Mỗi 5 phút: bài đã quá giờ lên sóng 2 phút thì kiểm tra công khai rồi bình luận. Tối đa 6 lần, cách 10 phút.</summary>
internal sealed class ContentFirstCommentService
{
    private readonly ContentFirstCommentStore _store;
    private readonly ContentRepository _repo;
    private readonly ContentYoutubeClient _youtube;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ContentFirstCommentService> _log;

    public ContentFirstCommentService(
        ContentFirstCommentStore store,
        ContentRepository repo,
        ContentYoutubeClient youtube,
        IHttpClientFactory http,
        ILogger<ContentFirstCommentService> log)
    {
        _store = store;
        _repo = repo;
        _youtube = youtube;
        _http = http;
        _log = log;
    }

    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<ContentFirstCommentRow> due;
        try
        {
            due = await _store.ListDueAsync(now, ct);
        }
        catch (Exception ex) when (ContentFirstCommentStore.IsMissingTable(ex))
        {
            _log.LogWarning("Hàng chờ bình luận chưa có bảng. Chạy migration 441.");
            return 0;
        }

        var done = 0;
        foreach (var row in due)
        {
            if (!ContentFirstCommentRules.IsDue(row.Status, row.Attempts, row.LiveAt, row.NextAttemptAt, now))
                continue;
            try
            {
                await PostOneAsync(row, now, ct);
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Bình luận chờ {Id} lỗi.", row.Id);
                await MissAsync(row, now, ex.Message, ct);
            }
        }
        return done;
    }

    private async Task PostOneAsync(ContentFirstCommentRow row, DateTimeOffset now, CancellationToken ct)
    {
        var channel = await _repo.GetChannelAsync(row.ChannelTargetId, ct);
        var token = channel is null ? null : ContentTargetSecrets.ExtractStored(channel.ConfigJson);
        if (channel is null || string.IsNullOrWhiteSpace(token))
        {
            await FinishAsync(row, ContentFirstCommentRules.FailedStatus, row.Attempts + 1, null,
                "Kênh không còn token.", ContentFirstCommentRules.FailedText("Kênh không còn token."), ct);
            return;
        }

        if (row.Connector == ContentFirstCommentRules.Youtube)
            await PostYoutubeAsync(row, channel, token, now, ct);
        else if (row.Connector == ContentFirstCommentRules.Facebook)
            await PostFacebookAsync(row, token, now, ct);
        else
            await FinishAsync(row, ContentFirstCommentRules.FailedStatus, row.Attempts + 1, null,
                "Kênh không bình luận được.", ContentFirstCommentRules.FailedText("Kênh không bình luận được."), ct);
    }

    private async Task PostYoutubeAsync(
        ContentFirstCommentRow row, ContentRepository.ChannelRow channel, string refresh, DateTimeOffset now, CancellationToken ct)
    {
        var (access, scope) = await _youtube.AccessTokenWithScopeAsync(refresh, ct);
        if (!ContentFirstCommentRules.ScopeAllowsComment(scope))
        {
            await MarkYoutubeReconnectAsync(channel, ct);
            await FinishAsync(row, ContentFirstCommentRules.FailedStatus, row.Attempts + 1, null,
                ContentFirstCommentRules.NeedReconnect, ContentFirstCommentRules.NeedReconnect, ct);
            return;
        }

        string? privacy;
        try
        {
            privacy = await _youtube.VideoPrivacyAsync(access, row.ExternalId, ct);
        }
        catch (ContentYoutubeException ex) when (ContentFirstCommentRules.IsMissingCommentScope(ex.StatusCode, ex.Reason))
        {
            await MarkYoutubeReconnectAsync(channel, ct);
            await FinishAsync(row, ContentFirstCommentRules.FailedStatus, row.Attempts + 1, null,
                ContentFirstCommentRules.NeedReconnect, ContentFirstCommentRules.NeedReconnect, ct);
            return;
        }

        if (!ContentFirstCommentRules.YoutubeIsPublic(privacy))
        {
            await MissAsync(row, now, "Video YouTube vẫn chưa công khai.", ct);
            return;
        }

        try
        {
            await _youtube.InsertCommentAsync(access, row.ExternalId, row.Body, ct);
        }
        catch (ContentYoutubeException ex) when (ContentFirstCommentRules.IsMissingCommentScope(ex.StatusCode, ex.Reason))
        {
            await MarkYoutubeReconnectAsync(channel, ct);
            await FinishAsync(row, ContentFirstCommentRules.FailedStatus, row.Attempts + 1, null,
                ContentFirstCommentRules.NeedReconnect, ContentFirstCommentRules.NeedReconnect, ct);
            return;
        }
        catch (ContentYoutubeException ex) when (ex.Reason == "commentsDisabled")
        {
            await FinishAsync(row, ContentFirstCommentRules.FailedStatus, row.Attempts + 1, null,
                "YouTube đang tắt bình luận trên video này.",
                ContentFirstCommentRules.FailedText("YouTube đang tắt bình luận trên video này."), ct);
            return;
        }

        await FinishAsync(row, ContentFirstCommentRules.PostedStatus, row.Attempts, null, null,
            ContentFirstCommentRules.Posted, ct);
        _log.LogInformation("Đã bình luận YouTube {VideoId} cho job {JobId}.", row.ExternalId, row.PublishJobId);
    }

    private async Task PostFacebookAsync(ContentFirstCommentRow row, string token, DateTimeOffset now, CancellationToken ct)
    {
        var http = _http.CreateClient("content-publish");
        var id = Uri.EscapeDataString(row.ExternalId);
        using var lookReq = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.facebook.com/{ContentFacebookConfigParser.GraphVersion}/{id}?fields=is_published,published,scheduled_publish_time");
        lookReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var look = await http.SendAsync(lookReq, ct);
        var lookBody = await look.Content.ReadAsStringAsync(ct);
        if (!look.IsSuccessStatusCode)
        {
            await MissAsync(row, now, "Chưa đọc được trạng thái bài Facebook.", ct);
            return;
        }

        if (!FacebookLive(lookBody, now))
        {
            await MissAsync(row, now, "Bài Facebook chưa lên sóng.", ct);
            return;
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["message"] = row.Body,
            ["access_token"] = token,
        });
        using var res = await http.PostAsync(
            $"https://graph.facebook.com/{ContentFacebookConfigParser.GraphVersion}/{id}/comments", form, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            await MissAsync(row, now, $"Facebook bình luận {(int)res.StatusCode}.", ct);
            return;
        }

        await FinishAsync(row, ContentFirstCommentRules.PostedStatus, row.Attempts, null, null,
            ContentFirstCommentRules.Posted, ct);
        _log.LogInformation("Đã bình luận Facebook {PostId} cho job {JobId}.", row.ExternalId, row.PublishJobId);
    }

    private static bool FacebookLive(string json, DateTimeOffset now)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            bool? Flag(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? v.GetBoolean()
                    : null;
            long? unix = root.TryGetProperty("scheduled_publish_time", out var s) && s.TryGetInt64(out var n) ? n : null;
            var published = Flag("is_published");
            var videoPublished = Flag("published");
            if (published == false || videoPublished == false) return false;
            if (unix is { } at && DateTimeOffset.FromUnixTimeSeconds(at) > now) return false;
            return published == true || videoPublished == true || unix is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task MarkYoutubeReconnectAsync(ContentRepository.ChannelRow channel, CancellationToken ct)
    {
        try
        {
            channel.ConfigJson = ContentYoutubeConnectionService.ApplyLink(
                channel.ConfigJson, "NEED_RECONNECT", null, ContentFirstCommentRules.NeedReconnect);
            await _repo.UpsertChannelAsync(channel.BrandId, channel, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Không ghi được NEED_RECONNECT cho kênh YouTube {ChannelId}.", channel.Id);
        }
    }

    private async Task MissAsync(ContentFirstCommentRow row, DateTimeOffset now, string error, CancellationToken ct)
    {
        var next = ContentFirstCommentRules.AfterMiss(row.Attempts, now, error);
        await FinishAsync(row, next.Status, next.Attempts, next.NextAttemptAt, next.LastError,
            next.Status == ContentFirstCommentRules.FailedStatus
                ? ContentFirstCommentRules.FailedText(next.LastError)
                : ContentFirstCommentRules.Waiting,
            ct);
    }

    private async Task FinishAsync(
        ContentFirstCommentRow row, string status, int attempts, DateTimeOffset? nextAt, string? lastError,
        string jobText, CancellationToken ct)
    {
        row.Status = status;
        row.Attempts = attempts;
        row.NextAttemptAt = nextAt;
        row.LastError = lastError;
        await _store.SaveAsync(row, ct);
        var current = await _store.ReadJobResultAsync(row.PublishJobId, ct);
        var merged = ContentFirstCommentRules.WriteResult(current, jobText, row.PinNote);
        await _store.WriteJobResultAsync(row.PublishJobId, merged, ct);
    }
}

internal sealed class ContentFirstCommentWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly ILogger<ContentFirstCommentWorker> _log;

    public ContentFirstCommentWorker(
        IServiceScopeFactory scopes, IOptions<ContentOptions> options, ILogger<ContentFirstCommentWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Content first-comment worker disabled.");
            return;
        }

        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        _log.LogInformation("Content first-comment worker started (interval=5 min).");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<ContentFirstCommentService>();
                await svc.ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Content first-comment worker loop failed.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
