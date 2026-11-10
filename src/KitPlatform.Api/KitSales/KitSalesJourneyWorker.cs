using KitPlatform.Packs.Sales;
using Microsoft.Extensions.DependencyInjection;

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
            using var scope = _scopes.CreateScope();
            try
            {
                var sweep = scope.ServiceProvider.GetRequiredService<IKitSalesAgentSweep>();
                var result = await sweep.SweepAsync(stoppingToken);
                _log.LogInformation(
                    "KIT Sales agent sweep planned {Planned}, skipped {Skipped}, schema {SchemaReady}, outbound unknown {OutboundUnknown}. Outbound remains draft-only.",
                    result.Planned,
                    result.Skipped,
                    result.SchemaReady,
                    result.OutboundUnknown);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "KIT Sales agent sweep failed; no outbound was sent");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
