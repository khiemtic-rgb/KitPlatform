using KitPlatform.Packs.LocalOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

/// <summary>08:00 Asia/Ho_Chi_Minh daily. Official watch → ACTIVE. Never Facebook.</summary>
internal sealed class LocalOsWatchWorker : BackgroundService
{
    private static readonly TimeSpan InFlightMax = TimeSpan.FromMinutes(3);
    private static readonly TimeZoneInfo Vietnam = ResolveVietnam();

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LocalOsWatchWorker> _log;

    public LocalOsWatchWorker(IServiceScopeFactory scopes, ILogger<LocalOsWatchWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
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
                var watch = scope.ServiceProvider.GetRequiredService<ILocalOsWatchService>();
                if (await watch.HasInFlightAsync(InFlightMax, stoppingToken))
                {
                    _log.LogInformation("Local OS watch: đang chạy, chờ.");
                    await DelayUntil(DateTimeOffset.UtcNow.AddMinutes(20), stoppingToken);
                    continue;
                }

                var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Vietnam);
                var last = await watch.LastScheduledFinishedAtAsync(stoppingToken);
                var lastVn = last is DateTimeOffset at
                    ? TimeZoneInfo.ConvertTimeFromUtc(at.UtcDateTime, Vietnam)
                    : (DateTime?)null;
                var ranToday = lastVn is DateTime done && done.Date == nowVn.Date;

                if (ranToday || nowVn.Hour < 8)
                {
                    await DelayUntil(NextEightAmUtc(nowVn), stoppingToken);
                    continue;
                }

                var run = await watch.RunAsync("scheduled", stoppingToken);
                _log.LogInformation(
                    "Local OS watch 8h: created={Created} existing={Existing} filtered={Filtered} errors={Errors}",
                    run.CreatedCount, run.SkippedExisting, run.SkippedFilter, run.ErrorCount);
                await DelayUntil(NextEightAmUtc(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Vietnam)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Local OS watch 8h failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(20), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static async Task DelayUntil(DateTimeOffset utc, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var left = utc - DateTimeOffset.UtcNow;
            if (left <= TimeSpan.Zero) return;
            var slice = left > TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : left;
            await Task.Delay(slice, stoppingToken);
        }
    }

    private static DateTimeOffset NextEightAmUtc(DateTime vnNow)
    {
        var day = vnNow.Hour < 8 ? vnNow.Date : vnNow.Date.AddDays(1);
        var local = DateTime.SpecifyKind(day.AddHours(8), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Vietnam), TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveVietnam()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                /* try next */
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN");
    }
}
