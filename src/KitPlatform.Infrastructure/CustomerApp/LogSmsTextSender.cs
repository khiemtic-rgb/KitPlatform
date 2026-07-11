using Microsoft.Extensions.Logging;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class LogSmsTextSender : ISmsTextSender
{
    private readonly ILogger<LogSmsTextSender> _logger;

    public LogSmsTextSender(ILogger<LogSmsTextSender> logger) => _logger = logger;

    public Task SendTextAsync(
        string phone,
        string tenantCode,
        string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SMS text (log-only) → {Phone} tenant {Tenant}: {Message}",
            phone,
            tenantCode,
            message);
        return Task.CompletedTask;
    }
}
