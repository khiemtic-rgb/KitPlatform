using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentOpsService : IContentOpsService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly ContentWorkRepository _work;
    private readonly ContentRepository _repo;
    private readonly IContentPackageService _packages;
    private readonly ContentFacebookClient _facebook;

    public ContentOpsService(
        ContentWorkRepository work,
        ContentRepository repo,
        IContentPackageService packages,
        ContentFacebookClient facebook)
    {
        _work = work;
        _repo = repo;
        _packages = packages;
        _facebook = facebook;
    }

    public async Task<ContentOpsSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = StartOfLocalDay(now);
        var monthStart = StartOfLocalMonth(now);
        var weekStart = StartOfLocalWeek(now);
        var weekEnd = weekStart.AddDays(7);

        var statusCounts = await _work.CountTopicStatusAsync(cancellationToken);
        int Count(string status) =>
            statusCounts.FirstOrDefault(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))?.Cnt ?? 0;

        var generatingTopics = Count("Generating");
        var activeWork = await _work.CountActiveWorkAsync(cancellationToken);
        var publishedToday = await _work.CountPublishedSinceAsync(todayStart, cancellationToken);
        var publishedWeek = await _work.CountPublishedSinceAsync(weekStart, cancellationToken);
        var errors = await _work.CountWorkErrorsAsync(todayStart, cancellationToken);
        var org = await _repo.GetOrgSettingsAsync(cancellationToken);
        var spend = await _repo.SumSpendAsync(null, monthStart, cancellationToken);
        var brands = await _work.ListBrandOpsAsync(monthStart, cancellationToken);
        var activeJobs = await _work.ListActiveAsync(cancellationToken);
        var failedJobs = await _work.ListFailedAsync(8, cancellationToken);
        var cores = await _packages.ListAsync(null, null, coresOnly: true, cancellationToken);
        var weekItems = await ListCalendarAsync(weekStart, weekEnd, null, cancellationToken);

        var featured = cores.Take(8).ToList();
        var unscored = cores.Count(c => c.BrandFits is not { Count: > 0 });
        var scheduledWeek = weekItems.Count(i =>
            string.Equals(i.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.Kind, "publish", StringComparison.OrdinalIgnoreCase));
        var fb = await _facebook.ResolveAsync(cancellationToken);
        var failedPublish = await _repo.ListFailedPublishJobsAsync(8, cancellationToken);

        return new ContentOpsSnapshotDto(
            ReviewCount: Count("Review"),
            GeneratingCount: Math.Max(generatingTopics, activeWork),
            ScheduledCount: Count("Scheduled"),
            PublishedTodayCount: publishedToday,
            ErrorCount: errors + failedPublish.Count,
            MonthSpendUsd: spend,
            MonthCeilingUsd: org.MonthlyCeilingUsd,
            Brands: brands.Select(b => new ContentOpsBrandRowDto(
                b.BrandId, b.BrandCode, b.BrandName,
                b.ReviewCount, b.ScheduledCount, b.PublishedMonthCount, b.SpendUsd)).ToList(),
            ActiveJobs: activeJobs.Select(ContentWorkQueueService.MapJob).ToList(),
            CoreIdeaCount: cores.Count,
            CoreDraftCount: cores.Count(c => string.Equals(c.Status, "Draft", StringComparison.OrdinalIgnoreCase)),
            CoreUnscoredCount: unscored,
            AdaptationCount: cores.Sum(c => c.AdaptationCount),
            ScheduledThisWeek: scheduledWeek,
            PublishedThisWeek: publishedWeek,
            CoreIdeas: featured,
            WeekItems: weekItems,
            RecentErrors: failedJobs.Select(ContentWorkQueueService.MapJob).ToList(),
            BudgetBlockedCount: Count("BudgetBlocked"),
            FacebookAppConfigured: !string.IsNullOrWhiteSpace(fb.AppId) && !string.IsNullOrWhiteSpace(fb.AppSecret),
            FailedPublishJobs: failedPublish.Select(j => new ContentOpsFailedPublishDto(
                j.Id, j.TopicId, j.TopicTitle, j.ConnectorType, j.LastError, j.UpdatedAt)).ToList());
    }

    public async Task<IReadOnlyList<ContentCalendarItemDto>> ListCalendarAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? brandId,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
            throw new InvalidOperationException("Khoảng lịch không hợp lệ.");
        if (toUtc - fromUtc > TimeSpan.FromDays(62))
            toUtc = fromUtc.AddDays(62);

        var rows = await _work.ListCalendarAsync(fromUtc, toUtc, brandId, cancellationToken);
        return rows.Select(r => new ContentCalendarItemDto(
            r.At, r.Kind, r.PackageId, r.TopicId, r.PublishJobId,
            r.BrandId, r.BrandCode, r.BrandName, r.Title, r.Channel, r.Status)).ToList();
    }

    private static DateTimeOffset StartOfLocalDay(DateTimeOffset utc)
    {
        var local = utc.ToOffset(VietnamOffset);
        return new DateTimeOffset(local.Date, VietnamOffset).ToUniversalTime();
    }

    private static DateTimeOffset StartOfLocalMonth(DateTimeOffset utc)
    {
        var local = utc.ToOffset(VietnamOffset);
        return new DateTimeOffset(new DateTime(local.Year, local.Month, 1), VietnamOffset).ToUniversalTime();
    }

    private static DateTimeOffset StartOfLocalWeek(DateTimeOffset utc)
    {
        var local = utc.ToOffset(VietnamOffset);
        var delta = ((int)local.DayOfWeek + 6) % 7;
        return new DateTimeOffset(local.Date.AddDays(-delta), VietnamOffset).ToUniversalTime();
    }
}
