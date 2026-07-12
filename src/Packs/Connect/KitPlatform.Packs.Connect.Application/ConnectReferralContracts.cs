namespace KitPlatform.Packs.Connect;

public static class ConnectReferralStatuses
{
    public const string PendingClinicAccept = "pending_clinic_accept";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public sealed record ConnectReferralDto(
    Guid Id,
    Guid PharmacyTenantId,
    string PharmacyTenantCode,
    string PharmacyTenantName,
    Guid ClinicTenantId,
    string ClinicTenantCode,
    string ClinicTenantName,
    Guid? DoctorId,
    string? DoctorFullName,
    string PatientDisplayName,
    string? PatientPhone,
    Guid? PharmacyCustomerId,
    Guid? ClinicCustomerId,
    string? Reason,
    string? Notes,
    string ReferralStatus,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? CompletedAt);

public sealed record CreateConnectReferralRequest(
    Guid ClinicTenantId,
    Guid PharmacyCustomerId,
    string? PatientDisplayName = null,
    string? PatientPhone = null,
    string? Reason = null,
    string? Notes = null,
    Guid? DoctorId = null);

public interface IConnectReferralService
{
    Task<IReadOnlyList<ConnectReferralDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectReferralDto>> ListInboxAsync(
        CancellationToken cancellationToken = default);

    Task<ConnectReferralDto> CreateAsync(
        CreateConnectReferralRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectReferralDto?> AcceptAsync(Guid referralId, CancellationToken cancellationToken = default);

    Task<ConnectReferralDto?> RejectAsync(Guid referralId, CancellationToken cancellationToken = default);

    Task<ConnectReferralDto?> CompleteAsync(Guid referralId, CancellationToken cancellationToken = default);

    Task<ConnectReferralDto?> CancelAsync(Guid referralId, CancellationToken cancellationToken = default);
}
