using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public sealed class ExchangeBackfillWorker : BackgroundService
{
    readonly IServiceScopeFactory _scopes;
    readonly IOptions<ExchangeBackfillOptions> _options;
    readonly ILogger<ExchangeBackfillWorker> _log;

    public ExchangeBackfillWorker(IServiceScopeFactory scopes, IOptions<ExchangeBackfillOptions> options, ILogger<ExchangeBackfillWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _log.LogInformation("Exchange backfill worker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                using var scope = _scopes.CreateScope();
                var backfill = scope.ServiceProvider.GetRequiredService<IExchangeBackfill>();
                worked = await backfill.PumpAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning("Exchange backfill pump stopped on {ExceptionType}.", ex.GetType().Name);
            }

            try
            {
                await Task.Delay(worked ? TimeSpan.FromMilliseconds(Math.Max(50, _options.Value.DelayMilliseconds)) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
