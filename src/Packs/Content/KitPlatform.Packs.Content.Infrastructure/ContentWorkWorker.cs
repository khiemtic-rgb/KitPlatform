using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Polls <c>pack_content.work_job</c> and due publish jobs. Isolated from Pharmacy workers.</summary>
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
                if (!processed)
                    await queue.ProcessDuePublishJobsAsync(3, stoppingToken);
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
