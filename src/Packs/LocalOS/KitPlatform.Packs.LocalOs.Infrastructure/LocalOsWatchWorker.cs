using KitPlatform.Packs.LocalOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

/// <summary>Once a day: official index → NEEDS_REVIEW. Never Facebook, never auto-publish.</summary>
internal sealed class LocalOsWatchWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Poll = TimeSpan.FromHours(6);
    private static readonly TimeSpan MinGap = TimeSpan.FromHours(20);

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
            await Task.Delay(StartupDelay, stoppingToken);
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
                var last = await watch.LastFinishedAtAsync(stoppingToken);
                if (last is null || DateTimeOffset.UtcNow - last.Value > MinGap)
                {
                    var run = await watch.RunAsync("scheduled", stoppingToken);
                    _log.LogInformation(
                        "Local OS watch: created={Created} existing={Existing} filtered={Filtered} errors={Errors}",
                        run.CreatedCount, run.SkippedExisting, run.SkippedFilter, run.ErrorCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Local OS watch scheduled run failed.");
            }

            try
            {
                await Task.Delay(Poll, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
