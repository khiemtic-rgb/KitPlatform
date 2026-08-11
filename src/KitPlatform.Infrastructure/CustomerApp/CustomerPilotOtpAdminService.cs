using Microsoft.Extensions.Options;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Customers;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerPilotOtpAdminService : ICustomerPilotOtpAdminService
{
    private readonly CustomerAppAuthRepository _otpRepo;
    private readonly ICustomerAdminService _customers;
    private readonly ITenantContext _tenant;
    private readonly CustomerAppAuthSettings _settings;

    public CustomerPilotOtpAdminService(
        CustomerAppAuthRepository otpRepo,
        ICustomerAdminService customers,
        ITenantContext tenant,
        IOptions<CustomerAppAuthSettings> settings)
    {
        _otpRepo = otpRepo;
        _customers = customers;
        _tenant = tenant;
        _settings = settings.Value;
    }

    public async Task<CustomerPilotOtpStatusDto?> GetStatusAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.ExposePilotOtpInAdmin)
            return new CustomerPilotOtpStatusDto(false, null, null, null);

        var customer = await _customers.GetAsync(customerId, cancellationToken);
        if (customer is null)
            return null;

        var phone = CustomerAppAuthRepository.NormalizePhone(customer.Phone);
        var otp = await _otpRepo.GetActivePilotOtpAsync(_tenant.TenantId, phone, cancellationToken);
        if (otp is null)
            return new CustomerPilotOtpStatusDto(true, null, null, null);

        return new CustomerPilotOtpStatusDto(
            true,
            otp.Code,
            new DateTimeOffset(DateTime.SpecifyKind(otp.ExpiresAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(otp.CreatedAt, DateTimeKind.Utc)));
    }

    public async Task<ActiveCounterOtpListDto> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_settings.ExposePilotOtpInAdmin)
            return new ActiveCounterOtpListDto(false, []);

        var rows = await _otpRepo.ListActivePilotOtpsAsync(_tenant.TenantId, 20, cancellationToken);
        var items = rows
            .Select(r => new ActiveCounterOtpDto(
                r.Phone,
                r.Code,
                new DateTimeOffset(DateTime.SpecifyKind(r.ExpiresAt, DateTimeKind.Utc)),
                new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)),
                r.CustomerId,
                r.CustomerName))
            .ToList();
        return new ActiveCounterOtpListDto(true, items);
    }
}
