namespace KitPlatform.Application.CustomerApp;

/// <summary>
/// Connect-aware surfaces for pharmacy customer app (Option 1).
/// Read-only inbox keyed by pharmacy CRM customer — not Clinic EMR.
/// </summary>
public static class CustomerConnectItemKinds
{
    public const string ReferralPending = "referral_pending";
    public const string ReferralAccepted = "referral_accepted";
    public const string BookingProposed = "booking_proposed";
    public const string BookingConfirmed = "booking_confirmed";
    public const string RxReady = "rx_ready";
}

public sealed record CustomerConnectInboxItemDto(
    string Kind,
    Guid SourceId,
    string? Detail,
    string? ClinicName,
    DateTime? OccurredAt,
    DateTime? ScheduledAt,
    string? PrescriptionCode,
    string CtaKey);

public sealed record CustomerConnectInboxDto(
    bool ConnectEnabled,
    IReadOnlyList<CustomerConnectInboxItemDto> Items);

public interface ICustomerAppConnectService
{
    Task<CustomerConnectInboxDto> GetInboxAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);
}
