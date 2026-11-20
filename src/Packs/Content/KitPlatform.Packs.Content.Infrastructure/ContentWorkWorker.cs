using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Polls <c>pack_content.work_job</c>. Isolated from Pharmacy workers. Job đăng do <see cref="ContentPublishDueWorker"/> chạy.</summary>
internal sealed class ContentWorkWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly ILogger<ContentWorkWorker> _log;

    public ContentWorkWorker(
        IServiceScopeFactory scopes,
        IOptions<ContentOptions> options,
        ILogger<ContentWorkWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Content work worker disabled.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _log.LogInformation("Content work worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<IContentWorkQueueService>();
                processed = await queue.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Content work worker loop failed.");
            }

            if (processed) continue;

            var seconds = Math.Clamp(_options.Value.WorkerPollSeconds, 1, 30);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

/// <summary>
/// Chạy các job đăng đã tới giờ, kể cả job quá giờ vì máy tắt (đăng ngay khi máy bật lại).
/// Tách khỏi <see cref="ContentWorkWorker"/> để một lượt dựng video dài không chặn việc đăng bài.
/// Mỗi vòng cũng chuyển job kẹt ở Running (máy tắt giữa lúc đăng) sang Failed để người dùng bấm Đăng lại.
/// </summary>
internal sealed class ContentPublishDueWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly ILogger<ContentPublishDueWorker> _log;

    public ContentPublishDueWorker(
        IServiceScopeFactory scopes,
        IOptions<ContentOptions> options,
        ILogger<ContentPublishDueWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Content publish worker disabled.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _log.LogInformation("Content publish worker started (interval={Seconds}s).", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var ran = 0;
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<IContentWorkQueueService>();
                ran = await queue.ProcessDuePublishJobsAsync(3, stoppingToken);
                if (ran > 0)
                    _log.LogInformation("Content publish worker ran {Count} due job(s).", ran);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Content publish worker loop failed.");
            }

            if (ran > 0) continue; // còn job quá giờ thì quét tiếp ngay

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
