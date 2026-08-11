namespace KitPlatform.Application.CustomerApp;

public static class CustomerAppOtpChannels
{
    public const string Counter = "counter";
    public const string Remote = "remote";
}

public static class CustomerAppOtpResponseStatuses
{
    public const string OtpSent = "otp_sent";
    public const string PendingApproval = "pending_approval";
    /// <summary>Counter channel needs staff PIN (first-time / non-member). Client should show PIN field.</summary>
    public const string CounterPinRequired = "counter_pin_required";
}

public static class CustomerAppLoginRequestStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Consumed = "consumed";
    public const string Expired = "expired";
}

public sealed record RequestCustomerOtpRequest(
    string Phone,
    string? TenantCode = null,
    string? Channel = null,
    string? CounterPin = null,
    string? InviteCode = null);

public sealed record VerifyCustomerOtpRequest(string Phone, string Code, string? TenantCode = null);

public sealed record CustomerOtpSentResponse(
    int ExpiresInSeconds,
    int CooldownSeconds,
    string Message,
    string? PilotCode = null,
    string Status = CustomerAppOtpResponseStatuses.OtpSent);

public sealed record CustomerProfileDto(
    Guid AccountId,
    Guid CustomerId,
    Guid TenantId,
    string TenantCode,
    string FullName,
    string Phone,
    string? PreferredLocale,
    string? PharmacyRelation = null,
    string? AcquisitionSource = null,
    string? AvatarUrl = null);

public sealed record UpdateCustomerPreferredLocaleRequest(string PreferredLocale);

public sealed record UpdateCustomerProfileRequest(string? FullName = null, string? AvatarUrl = null);

public sealed record ConfirmCustomerPharmacyLinkRequest(
    string VerifiedVia = "qr_scan",
    string? TenantCode = null);

public sealed record CustomerLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    CustomerProfileDto Profile);

public sealed record CustomerRefreshTokenRequest(string RefreshToken);

public sealed record CustomerAppLoginRequestDto(
    Guid Id,
    string Phone,
    Guid? CustomerId,
    string? CustomerName,
    string Channel,
    string Status,
    string? ReferralCodeUsed,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectReason);

public sealed record RejectCustomerAppLoginRequest(string? Reason = null);

public sealed record ApproveCustomerAppLoginResult(
    Guid RequestId,
    Guid CustomerId,
    string Phone,
    string? PilotCode,
    DateTimeOffset? ExpiresAt,
    string Message);

public sealed record CustomerAppAuthSettingsDto(
    bool HasCounterPin,
    bool HasInviteCode,
    string? InviteCodeHint);

public sealed record UpdateCustomerAppAuthSettingsRequest(
    string? CounterPin = null,
    string? InviteCode = null,
    bool ClearCounterPin = false,
    bool ClearInviteCode = false);

public sealed record IssueCounterPilotOtpRequest(string Phone, string? FullName = null);

public sealed record IssueCounterPilotOtpResult(
    Guid CustomerId,
    string Phone,
    string? PilotCode,
    DateTimeOffset? ExpiresAt,
    string Message);
