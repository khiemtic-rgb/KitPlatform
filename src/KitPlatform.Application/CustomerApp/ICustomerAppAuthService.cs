namespace KitPlatform.Application.CustomerApp;

public interface ICustomerAppAuthService
{
    Task<CustomerOtpSentResponse> RequestOtpAsync(
        RequestCustomerOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerLoginResponse?> VerifyOtpAsync(
        VerifyCustomerOtpRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task<CustomerLoginResponse?> RefreshAsync(
        CustomerRefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileDto?> GetProfileAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileDto?> UpdatePreferredLocaleAsync(
        Guid accountId,
        string preferredLocale,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileDto?> UpdateProfileAsync(
        Guid accountId,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileDto?> UpdateAvatarUrlAsync(
        Guid accountId,
        string avatarUrl,
        CancellationToken cancellationToken = default);

    Task<CustomerProfileDto?> ConfirmPharmacyLinkAsync(
        Guid accountId,
        ConfirmCustomerPharmacyLinkRequest request,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
