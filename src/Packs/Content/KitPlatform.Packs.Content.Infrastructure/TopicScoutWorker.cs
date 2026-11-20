using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Quét chủ đề theo lịch khi Content:TopicScout:WorkerEnabled. Chỉ ghi phiếu chờ.
/// Không đưa vào pool, không viết bài, không dựng phim, không đăng.
/// </summary>
internal sealed class TopicScoutWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<TopicScoutOptions> _options;
    private readonly ILogger<TopicScoutWorker> _log;

    public TopicScoutWorker(IServiceScopeFactory scopes, IOptions<TopicScoutOptions> options, ILogger<TopicScoutWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Topic scout automatic scan disabled.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(_options.Value.IntervalHours, 1, 24));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _log.LogInformation("Topic scout automatic scan started (interval={Hours} h).", interval.TotalHours);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var scout = scope.ServiceProvider.GetRequiredService<ITopicScoutService>();
                var run = await scout.RunAsync(stoppingToken);
                _log.LogInformation("Topic scout scan saved vi={Vi} en={En}.", run.Vietnamese, run.English);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Topic scout automatic scan failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
