namespace KitPlatform.Application.CustomerApp;

/// <summary>
/// Free-text SMS (invite, Rx alerts). Shares CustomerAppSms gateway settings with OTP.
/// </summary>
public interface ISmsTextSender
{
    Task SendTextAsync(
        string phone,
        string tenantCode,
        string message,
        CancellationToken cancellationToken = default);
}
