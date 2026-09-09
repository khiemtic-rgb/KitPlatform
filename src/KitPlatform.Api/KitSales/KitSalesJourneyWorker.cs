using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;

namespace KitPlatform.Api.KitSales;

public sealed class KitSalesJourneyWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<KitSalesJourneyWorker> _log;

    public KitSalesJourneyWorker(IServiceScopeFactory scopes, ILogger<KitSalesJourneyWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                using var scope = _scopes.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<KitSalesFacebookSettingsService>();
                var options = await settings.GetEffectiveAsync(stoppingToken);
                if (!options.Enabled || string.IsNullOrWhiteSpace(options.PageAccessToken))
                    continue;

                var chat = scope.ServiceProvider.GetRequiredService<IKitSalesChatService>();
                var sent = await chat.ProcessDueJourneysAsync(stoppingToken);
                if (sent > 0)
                    _log.LogInformation("KIT Sales journey sent {Count} due Facebook messages", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "KIT Sales journey worker tick failed");
            }
        }
    }
}
