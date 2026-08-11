namespace KitPlatform.Application.CustomerApp;

public interface ICustomerPilotOtpAdminService
{
    Task<CustomerPilotOtpStatusDto?> GetStatusAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>Live list of unconsumed staff-read OTPs for the current tenant.</summary>
    Task<ActiveCounterOtpListDto> ListActiveAsync(CancellationToken cancellationToken = default);
}
