namespace PharmaCore.Application.CustomerApp;

public interface ICustomerOtpSender
{
    Task SendOtpAsync(
        string phone,
        string tenantCode,
        string code,
        int expireMinutes,
        CancellationToken cancellationToken = default);
}
