using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Trạng thái giữ giữa các vòng của worker (singleton theo worker).</summary>
internal sealed class ContentTelegramLoopState
{
    public string? LowStockDoneForVnDate { get; set; }
    public bool DraftTableMissingLogged { get; set; }
}

/// <summary>
/// Quét (chỉ đọc) publish_job / autopilot_run / schedule_proposal để xếp tin vào telegram_outbox, rồi gửi tin đến hạn.
/// Không nằm trên đường đăng bài: lỗi Telegram không đổi trạng thái publish_job.
/// </summary>
internal sealed class ContentTelegramNotifier
{
    private const int DrainBatch = 5;
    private static readonly TimeSpan SameChatGap = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NoTokenGiveUpAfter = TimeSpan.FromHours(24);

    private readonly ContentTelegramStore _store;
    private readonly ContentTelegramSender _sender;
    private readonly IOptions<ContentTelegramOptions> _options;
    private readonly ILogger<ContentTelegramNotifier> _log;

    public ContentTelegramNotifier(
        ContentTelegramStore store,
        ContentTelegramSender sender,
        IOptions<ContentTelegramOptions> options,
        ILogger<ContentTelegramNotifier> log)
    {
        _store = store;
        _sender = sender;
        _options = options;
        _log = log;
    }

    // ---------------- Discover ----------------

    public async Task<int> DiscoverAsync(ContentTelegramLoopState state, CancellationToken ct)
    {
        var brands = await _store.ListActiveBrandsAsync(ct);
        var queued = 0;
        queued += await DiscoverPublishAsync(brands, ct);
        queued += await DiscoverDraftsAsync(brands, state, ct);
        queued += await DiscoverProposalsAsync(brands, ct);
        queued += await DiscoverLowStockAsync(brands, state, ct);
        return queued;
    }

    private async Task<int> DiscoverPublishAsync(Dictionary<Guid, ContentTelegramBrandRow> brands, CancellationToken ct)
    {
        var wm = await _store.GetCursorAsync(ContentTelegramStore.CursorPublish, ct);
        var rows = await _store.ListPublishAfterAsync(wm, ct);
        if (rows.Count == 0) return 0;

        var n = 0;
        foreach (var r in rows)
        {
            if (!brands.TryGetValue(r.BrandId, out var s) || !s.CanQueue) continue;

            if (r.Status == "Succeeded" && s.NotifySuccess)
            {
                var result = ContentTelegramPublishResult.Parse(r.ResultJson, r.ConnectorType);
                var html = ContentTelegramTemplates.Published(
                    r.BrandName, r.TargetName, r.ConnectorType, r.TopicTitle, result, r.UpdatedAt, r.PublishAt);
                if (await _store.InsertAsync(r.BrandId, ContentTelegramDefaults.KindSuccess,
                        ContentTelegramDedupe.Success(r.Id), null, s.ChatId!, html, ContentTelegramDefaults.StatusPending, ct))
                    n++;
            }
            else if (r.Status == "Failed" && s.NotifyFailure)
            {
                var key = ContentTelegramDedupe.Failure(r.Id);
                if (await _store.ExistsAsync(key, ct)) continue;

                var (cls, hint) = ContentTelegramHints.For(r.LastError, r.BrandId, r.ConnectorType);
                var recent = await _store.ClassSentSinceAsync(r.BrandId, cls, ContentTelegramDefaults.FailureClassWindow, ct);
                var status = ContentTelegramDedupe.Decide(recent) == ContentTelegramDedupeDecision.Suppress
                    ? ContentTelegramDefaults.StatusSuppressed
                    : ContentTelegramDefaults.StatusPending;
                var html = ContentTelegramTemplates.Failure(
                    r.BrandName, r.TargetName, r.ConnectorType, r.TopicTitle, r.LastError, hint, r.UpdatedAt);
                if (await _store.InsertAsync(r.BrandId, ContentTelegramDefaults.KindFailure, key, cls, s.ChatId!, html, status, ct)
                    && status == ContentTelegramDefaults.StatusPending)
                    n++;
            }
        }

        // Nhích cả qua hàng bị bỏ vì brand tắt cờ, để không quét lại mãi.
        await _store.SetCursorAsync(ContentTelegramStore.CursorPublish, rows.Max(x => x.UpdatedAt), ct);
        return n;
    }

    private async Task<int> DiscoverDraftsAsync(
        Dictionary<Guid, ContentTelegramBrandRow> brands, ContentTelegramLoopState state, CancellationToken ct)
    {
        var wm = await _store.GetCursorAsync(ContentTelegramStore.CursorDraft, ct);
        IReadOnlyList<ContentTelegramDraftRow> rows;
        try
        {
            rows = await _store.ListDraftsAfterAsync(wm, ct);
        }
        catch (Exception ex) when (ContentFirstCommentStore.IsMissingTable(ex))
        {
            // autopilot_run tạo lazy trong ContentPublishPlanStore; DB cũ có thể chưa có.
            if (!state.DraftTableMissingLogged)
            {
                _log.LogInformation("Telegram: chưa có bảng autopilot_run, bỏ qua thông báo nháp Agent.");
                state.DraftTableMissingLogged = true;
            }
            return 0;
        }
        if (rows.Count == 0) return 0;

        var adminBase = _options.Value.AdminBaseUrl;
        var n = 0;
        foreach (var r in rows)
        {
            if (!brands.TryGetValue(r.BrandId, out var s) || !s.CanQueue || !s.NotifyProposal) continue;
            var html = ContentTelegramTemplates.Draft(r.BrandName, r.Title, r.TopicId, adminBase);
            if (await _store.InsertAsync(r.BrandId, ContentTelegramDefaults.KindProposal,
                    ContentTelegramDedupe.Draft(r.TopicId), null, s.ChatId!, html, ContentTelegramDefaults.StatusPending, ct))
                n++;
        }
        await _store.SetCursorAsync(ContentTelegramStore.CursorDraft, rows.Max(x => x.CreatedAt), ct);
        return n;
    }

    private async Task<int> DiscoverProposalsAsync(Dictionary<Guid, ContentTelegramBrandRow> brands, CancellationToken ct)
    {
        var wm = await _store.GetCursorAsync(ContentTelegramStore.CursorProposal, ct);
        // SQL chỉ trả hàng đã đủ 2 phút, nên max(created_at) luôn nhỏ hơn mọi hàng chưa đủ tuổi.
        var rows = await _store.ListProposalsAfterAsync(wm, ct);
        if (rows.Count == 0) return 0;

        var adminBase = _options.Value.AdminBaseUrl;
        var n = 0;
        foreach (var r in rows)
        {
            if (!brands.TryGetValue(r.BrandId, out var s) || !s.CanQueue || !s.NotifyProposal) continue;
            var html = ContentTelegramTemplates.Proposal(r.BrandName, r.Title, r.TargetLabel, r.PublishAt, adminBase);
            if (await _store.InsertAsync(r.BrandId, ContentTelegramDefaults.KindProposal,
                    ContentTelegramDedupe.Proposal(r.Id), null, s.ChatId!, html, ContentTelegramDefaults.StatusPending, ct))
                n++;
        }
        await _store.SetCursorAsync(ContentTelegramStore.CursorProposal, rows.Max(x => x.CreatedAt), ct);
        return n;
    }

    private async Task<int> DiscoverLowStockAsync(
        Dictionary<Guid, ContentTelegramBrandRow> brands, ContentTelegramLoopState state, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var today = ContentTelegramQuiet.DateKeyVn(now);
        if (state.LowStockDoneForVnDate == today || !ContentTelegramLowStock.IsScanTime(now)) return 0;
        if (!brands.Values.Any(b => b.CanQueue && b.NotifyLowStock))
        {
            state.LowStockDoneForVnDate = today;
            return 0;
        }

        var rows = await _store.ListLowStockCandidatesAsync(ct);
        var n = 0;
        foreach (var r in rows)
        {
            if (!brands.TryGetValue(r.BrandId, out var s) || !s.CanQueue || !s.NotifyLowStock) continue;
            if (!ContentTelegramLowStock.ShouldAlert(r.Remaining, r.NextAt, now, s.LowStockRemaining, s.LowStockEmptyDays)) continue;

            var nextShown = r.NextAt is { } nx && nx <= now.AddDays(s.LowStockEmptyDays) ? r.NextAt : null;
            var html = ContentTelegramTemplates.LowStock(
                r.BrandName, r.TargetName, r.Connector, r.Remaining, nextShown, s.LowStockEmptyDays);
            if (await _store.InsertAsync(r.BrandId, ContentTelegramDefaults.KindLowStock,
                    ContentTelegramDedupe.LowStock(r.BrandId, r.TargetId, now), null, s.ChatId!, html,
                    ContentTelegramDefaults.StatusPending, ct))
                n++;
        }
        state.LowStockDoneForVnDate = today;
        return n;
    }

    // ---------------- Drain ----------------

    public async Task<int> DrainAsync(CancellationToken ct)
    {
        var claimed = await _store.ClaimDueAsync(DrainBatch, ct);
        if (claimed.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        var token = await _sender.ResolveTokenAsync(ct);
        if (token is null)
        {
            foreach (var r in claimed)
            {
                if (now - r.CreatedAt > NoTokenGiveUpAfter)
                    await _store.MarkFailedAsync([r.Id], "Chưa có bot token.", ct);
                else
                    await _store.MarkRetryAsync([r.Id], now.AddMinutes(10), "Chưa có bot token.", refundAttempt: true, ct);
            }
            _log.LogWarning("Telegram: chưa có bot token, hoãn {Count} tin.", claimed.Count);
            return 0;
        }

        var lastSent = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var done = new HashSet<Guid>();
        var sent = 0;

        foreach (var row in claimed)
        {
            if (!done.Add(row.Id)) continue;
            now = DateTimeOffset.UtcNow;
            var from = row.QuietFromHour ?? ContentTelegramDefaults.QuietFromHour;
            var to = row.QuietToHour ?? ContentTelegramDefaults.QuietToHour;
            var quiet = ContentTelegramQuiet.IsQuiet(now, from, to);

            List<ContentTelegramOutboxRow> group = [row];
            string html;
            var silent = false;

            if (row.Kind == ContentTelegramDefaults.KindFailure)
            {
                // Tin lỗi vẫn gửi trong giờ im lặng, nhưng không rung/chuông.
                html = row.Html;
                silent = quiet;
            }
            else if (quiet)
            {
                await _store.MarkDigestAsync(row.Id, ContentTelegramQuiet.NextSendUtc(now, from, to), ct);
                continue;
            }
            else if (row.Digest)
            {
                foreach (var other in claimed)
                    if (other.Digest && other.Kind != ContentTelegramDefaults.KindFailure
                        && other.ChatId == row.ChatId && done.Add(other.Id))
                        group.Add(other);
                foreach (var extra in await _store.ClaimDigestForChatAsync(row.ChatId, 40, ct))
                    if (done.Add(extra.Id))
                        group.Add(extra);
                group.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
                html = ContentTelegramTemplates.Digest(group.Select(g => g.Html).ToList());
            }
            else
            {
                html = row.Html;
            }

            if (lastSent.TryGetValue(row.ChatId, out var prev))
            {
                var wait = prev + SameChatGap - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            }

            var result = await _sender.SendHtmlAsync(token, row.ChatId, html, silent, ct);
            lastSent[row.ChatId] = DateTimeOffset.UtcNow;

            var ids = group.Select(g => g.Id).ToList();
            var attempts = group.Max(g => g.Attempts);
            var (outcome, nextAt, refund) = ContentTelegramRetry.Decide(result, attempts, DateTimeOffset.UtcNow);
            switch (outcome)
            {
                case ContentTelegramSendOutcome.Sent:
                    await _store.MarkSentAsync(ids, ct);
                    sent += ids.Count;
                    break;
                case ContentTelegramSendOutcome.Retry:
                    await _store.MarkRetryAsync(ids, nextAt!.Value, result.Description, refund, ct);
                    break;
                default:
                    await _store.MarkFailedAsync(ids, result.Description, ct);
                    break;
            }

            _log.LogInformation(
                "Telegram {Outcome}: brand={BrandId} kind={Kind} key={DedupeKey} items={Count} status={Status} silent={Silent}",
                outcome, row.BrandId, row.Kind, row.DedupeKey, ids.Count, result.StatusCode, silent);
        }

        return sent;
    }
}

/// <summary>
/// Vòng 15 giây: gửi tin đến hạn rồi quét việc mới. Mặc định tắt cùng cờ <c>Content:WorkerEnabled</c> với worker đăng;
/// <c>Content:Telegram:WorkerEnabled</c> (nếu đặt) ghi đè riêng cho worker này.
/// Mẫu vòng lặp theo <see cref="ContentPublishDueWorker"/>.
/// </summary>
internal sealed class ContentTelegramWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MissingTableBackoff = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly IOptions<ContentTelegramOptions> _telegram;
    private readonly ILogger<ContentTelegramWorker> _log;
    private readonly ContentTelegramLoopState _state = new();

    public ContentTelegramWorker(
        IServiceScopeFactory scopes,
        IOptions<ContentOptions> options,
        IOptions<ContentTelegramOptions> telegram,
        ILogger<ContentTelegramWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _telegram = telegram;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ContentTelegramOptions.IsWorkerEnabled(_telegram.Value.WorkerEnabled, _options.Value.WorkerEnabled))
        {
            _log.LogInformation("Content Telegram worker disabled.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _log.LogInformation("Content Telegram worker started (interval={Seconds}s).", Interval.TotalSeconds);
        var missingLogged = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = Interval;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<ContentTelegramNotifier>();
                await notifier.DrainAsync(stoppingToken);
                var queued = await notifier.DiscoverAsync(_state, stoppingToken);
                if (queued > 0)
                    _log.LogInformation("Content Telegram worker queued {Count} message(s).", queued);
                missingLogged = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ContentFirstCommentStore.IsMissingTable(ex))
            {
                if (!missingLogged)
                {
                    _log.LogWarning("Content Telegram: chưa có bảng Telegram. Chạy migration 443.");
                    missingLogged = true;
                }
                delay = MissingTableBackoff;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Content Telegram worker loop failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
