using Microsoft.Extensions.Logging;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class LogCustomerOtpSender : ICustomerOtpSender
{
    private readonly ILogger<LogCustomerOtpSender> _logger;

    public LogCustomerOtpSender(ILogger<LogCustomerOtpSender> logger) => _logger = logger;

    public Task SendOtpAsync(
        string phone,
        string tenantCode,
        string code,
        int expireMinutes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "OTP SMS (log-only) → {Phone} tenant {Tenant}: {Code} (expires {Minutes}m)",
            phone,
            tenantCode,
            code,
            expireMinutes);
        return Task.CompletedTask;
    }
}
