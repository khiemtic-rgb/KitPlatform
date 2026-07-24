using KitPlatform.Application.CustomerApp;
using KitPlatform.Packs.FamilyOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyOsParentReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyOsReminderOptions _options;
    private readonly CustomerAppPushOptions _pushOptions;
    private readonly ILogger<FamilyOsParentReminderWorker> _logger;

    public FamilyOsParentReminderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FamilyOsReminderOptions> options,
        IOptions<CustomerAppPushOptions> pushOptions,
        ILogger<FamilyOsParentReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _pushOptions = pushOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("FamilyOS parent reminder worker is disabled.");
            return;
        }

        if (!_pushOptions.Enabled
            || string.IsNullOrWhiteSpace(_pushOptions.PublicKey)
            || string.IsNullOrWhiteSpace(_pushOptions.PrivateKey))
        {
            _logger.LogWarning(
                "FamilyOS parent reminder worker skipped: configure CustomerAppPush keys.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(30, _options.PollIntervalSeconds));
        _logger.LogInformation(
            "FamilyOS parent reminder worker started (interval={IntervalSeconds}s, digestHour={Hour}).",
            delay.TotalSeconds,
            _options.EveningDigestHour);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var push = scope.ServiceProvider.GetRequiredService<IFamilyOsParentPushService>();
                var sent = await push.DispatchDueParentRemindersAsync(stoppingToken);
                if (sent > 0)
                    _logger.LogInformation("FamilyOS parent reminders sent: {Count}.", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FamilyOS parent reminder worker batch failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
