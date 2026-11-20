using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Viết tự động theo lịch (chỉ chạy cho thương hiệu đã bật trong "Cài đặt lịch Agent", mặc định tắt).
/// Mỗi vòng (~30 phút) gọi <see cref="IContentAutopilotService"/> cho từng thương hiệu đã bật và ghi log từng bước.
/// </summary>
internal sealed class ContentAutopilotWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly ILogger<ContentAutopilotWorker> _log;

    public ContentAutopilotWorker(IServiceScopeFactory scopes, IOptions<ContentOptions> options, ILogger<ContentAutopilotWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Content autopilot disabled (Content:WorkerEnabled=false).");
            return;
        }
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        _log.LogInformation("Content autopilot started (interval={Minutes} min).", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Content autopilot loop failed.");
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

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IContentPublishPlanStore>();
        var autopilot = scope.ServiceProvider.GetRequiredService<IContentAutopilotService>();
        var brands = await store.ListAutopilotBrandsAsync(ct);
        _log.LogInformation("Content autopilot tick: {Count} brand(s) enabled.", brands.Count);
        foreach (var brandId in brands)
        {
            try
            {
                var report = await autopilot.RunBrandAsync(brandId, ignoreDailyLimit: false, ct);
                _log.LogInformation("Content autopilot {Brand}: {Steps}", report.BrandCode, string.Join(" | ", report.Steps));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Content autopilot failed for brand {BrandId}.", brandId);
            }
        }
    }
}

/// <summary>
/// Một lượt Viết tự động cho một thương hiệu:
/// 1. Bài người đã duyệt mà chưa có lịch: Agent xếp giờ và duyệt website (astro, WordPress, Thái Nguyên Life).
/// 2. Bài Agent đã có web_long: xếp và duyệt website theo khung giờ.
/// 3. Bài viết Fanpage (không có file video): xếp và duyệt lúc 15:00. YouTube không tự duyệt.
/// 4. Tối đa 1 bài/ngày: dựng khung từ YouTube/Fanpage có lượt xem, rồi giao AI viết.
/// Xếp lịch lỗi không chặn việc viết.
/// </summary>
internal sealed class ContentAutopilotService : IContentAutopilotService
{
    private static readonly string[] OpenSeries = [ContentArticleSeriesStatuses.Active, ContentArticleSeriesStatuses.Planned];
    private static readonly string[] WritableEpisodes =
        [ContentArticleEpisodeStatuses.Planned, ContentArticleEpisodeStatuses.Briefing, ContentArticleEpisodeStatuses.Ready];

    private readonly IContentPublishPlanStore _store;
    private readonly ContentRepository _repo;
    private readonly IContentScheduleProposalService _proposals;
    private readonly IContentArticleSeriesService _series;

    public ContentAutopilotService(
        IContentPublishPlanStore store,
        ContentRepository repo,
        IContentScheduleProposalService proposals,
        IContentArticleSeriesService series)
    {
        _store = store;
        _repo = repo;
        _proposals = proposals;
        _series = series;
    }

    public async Task<ContentAutopilotReportDto> RunBrandAsync(Guid brandId, bool ignoreDailyLimit, CancellationToken ct = default)
    {
        var steps = new List<string>();
        var brand = await _repo.GetBrandAsync(brandId, ct);
        if (brand is null)
            return new ContentAutopilotReportDto(brandId, "", ["Không thấy thương hiệu."], null);
        var settings = await _store.GetSettingsAsync(brandId, ct);
        if (!settings.AutopilotEnabled)
            return new ContentAutopilotReportDto(brandId, brand.Code, ["Viết tự động đang tắt."], null);
        if (!brand.IsActive)
            return new ContentAutopilotReportDto(brandId, brand.Code, ["Thương hiệu đang tắt."], null);
        if (settings.PublishPaused)
        {
            steps.Add("Đang tạm dừng đăng: không xếp lịch mới, các giờ đã hẹn giữ nguyên.");
        }
        else
        {
            try
            {
                steps.Add(await ScheduleApprovedAsync(brand, settings, ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                steps.Add("Xếp lịch bài đã duyệt lỗi: " + ex.Message);
            }

            try
            {
                var written = await _store.ListAutopilotTopicIdsAsync(brandId, ct);
                steps.Add(await _proposals.ScheduleAgentWebsitesAsync(brand.Code, written, ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                steps.Add("Xếp website lỗi: " + ex.Message);
            }

            try
            {
                var written = await _store.ListAutopilotTopicIdsAsync(brandId, ct);
                steps.Add(await _proposals.ScheduleAgentPagePostsAsync(brand.Code, written, ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                steps.Add("Xếp bài viết Fanpage lỗi: " + ex.Message);
            }
        }

        Guid? wrote = null;
        try
        {
            (var note, wrote) = await WriteNextEpisodeAsync(brandId, settings, ignoreDailyLimit, ct);
            steps.Add(note);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            steps.Add("Viết nháp lỗi: " + ex.Message);
        }
        return new ContentAutopilotReportDto(brandId, brand.Code, steps, wrote);
    }

    private async Task<string> ScheduleApprovedAsync(
        ContentRepository.BrandRow brand, ContentBrandScheduleSettingsDto settings, CancellationToken ct)
    {
        var approved = (await _repo.ListTopicsAsync(brand.Id, "Approved", ct)).Select(t => t.Id).ToHashSet();
        if (approved.Count == 0) return "Không có bài đã duyệt chờ xếp lịch.";

        var result = await _proposals.GenerateAsync(brand.Code, settings.Days, settings.MaxPerDayPerChannel, "Autopilot", ct);
        var mine = result.Proposals.Where(p =>
            p.Status == "Proposed"
            && approved.Contains(p.TopicId)
            && ContentScheduleRules.SiteAutoConnectors.Contains(p.ConnectorType, StringComparer.OrdinalIgnoreCase)).ToList();
        var done = 0;
        foreach (var p in mine)
        {
            try
            {
                await _proposals.ApproveAsync(p.Id, null, "Autopilot", ct);
                done++;
            }
            catch (InvalidOperationException)
            {
                // Lượt không duyệt được (ví dụ khung giờ vừa bị chiếm) để lại ở trạng thái Đề xuất cho người xem.
            }
        }
        return done > 0 ? $"Đã xếp lịch và duyệt giờ {done} lượt đăng." : "Bài đã duyệt đều đã có lịch.";
    }

    private async Task<(string Note, Guid? TopicId)> WriteNextEpisodeAsync(
        Guid brandId, ContentBrandScheduleSettingsDto settings, bool ignoreDailyLimit, CancellationToken ct)
    {
        var covered = await _repo.CountUnpostedReadyAsync(brandId, ct);
        var target = ContentPublishPlanRules.ReserveTarget(settings.AutopilotPostsPerWeek);
        if (!ignoreDailyLimit && covered >= target)
            return ($"Đã có {covered} bài chờ đăng, gồm bài dự phòng.", null);

        var series = (await _series.ListAsync(brandId, null, null, null, null, ct))
            .Where(s => OpenSeries.Contains(s.Status))
            .OrderBy(s => s.Status == ContentArticleSeriesStatuses.Active ? 0 : 1)
            .ToList();
        if (series.Count == 0) return ("Chưa có Series đang chạy (PLANNED/ACTIVE).", null);

        var skippedWithoutCard = 0;
        foreach (var s in series)
        {
            var (episode, skipped) = await NextReserveEpisodeAsync(s.Id, ct);
            skippedWithoutCard += skipped;
            if (episode is null) continue;

            try
            {
                var framework = ContentChannelFramework.Build(
                    await _repo.ListWatchedChannelPostsAsync(brandId, ct));
                var r = await _series.GenerateContentAsync(
                    s.Id, episode.Id, new GenerateContentRequest(ArticleFramework: framework), ct);
                await _store.AddAutopilotRunAsync(brandId, s.Id, episode.Id, r.Episode.ContentTopicId,
                    $"Tập {episode.EpisodeNo}: {episode.Title}", ct);
                return ($"Đã dựng khung từ YouTube/Fanpage và giao AI viết {s.Code} tập {episode.EpisodeNo}: {episode.Title}. {r.Message}", r.Episode.ContentTopicId);
            }
            catch (InvalidOperationException ex)
            {
                // Ghi lại để không thử lại liên tục trong ngày; topic_id rỗng không tính vào hạn mức tuần.
                await _store.AddAutopilotRunAsync(brandId, s.Id, episode.Id, null, "Lỗi: " + ex.Message, ct);
                return ($"Không viết được {s.Code} tập {episode.EpisodeNo}: {ex.Message}", null);
            }
        }
        return skippedWithoutCard > 0
            ? ("Các tập chưa có phiếu sự thật, chưa viết.", null)
            : ("Các Series đang chạy không còn tập chưa viết.", null);
    }

    private async Task<(ContentArticleEpisodeDto? Episode, int SkippedWithoutCard)> NextReserveEpisodeAsync(
        Guid seriesId, CancellationToken ct)
    {
        var skipped = 0;
        foreach (var episode in (await _series.ListEpisodesAsync(seriesId, ct)).OrderBy(e => e.EpisodeNo))
        {
            if (!ContentFactGate.HasCard(ContentFactGate.ReadCard(episode.Continuity)))
            {
                if (WritableEpisodes.Contains(episode.Status) || episode.Status == ContentArticleEpisodeStatuses.Generating)
                    skipped++;
                continue;
            }

            var fresh = episode.ContentTopicId is null && WritableEpisodes.Contains(episode.Status);
            var empty = episode.ContentTopicId is Guid topicId
                        && !await _repo.TopicHasBodyAsync(topicId, ct);
            if (fresh || empty) return (episode, skipped);
        }
        return (null, skipped);
    }
}
