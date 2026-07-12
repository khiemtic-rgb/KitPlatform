namespace KitPlatform.Packs.Connect;

public static class ConnectDoctorStatuses
{
    public const string PendingVerification = "pending_verification";
    public const string Active = "active";
    public const string Suspended = "suspended";
}

public static class ConnectMembershipRoles
{
    public const string Attending = "attending";
    public const string Consultant = "consultant";
    public const string Owner = "owner";
}

public static class ConnectMembershipStatuses
{
    /// <summary>Clinic invited doctor; waiting for doctor accept (or clinic confirm in C2.0 pilot).</summary>
    public const string PendingDoctorAccept = "pending_doctor_accept";

    /// <summary>Doctor requested; waiting for clinic approve.</summary>
    public const string PendingClinicApproval = "pending_clinic_approval";

    /// <summary>POV: pending incoming for clinic (maps from pending_clinic_approval).</summary>
    public const string PendingOurApproval = "pending_our_approval";

    public const string Active = "active";
    public const string Rejected = "rejected";
    public const string Revoked = "revoked";
}

public static class ConnectMembershipInitiators
{
    public const string Clinic = "clinic";
    public const string Doctor = "doctor";
    public const string System = "system";
}

public sealed record ConnectDoctorDto(
    Guid Id,
    string FullName,
    string Phone,
    string? LicenseNumber,
    string? Specialty,
    string Status,
    DateTime CreatedAt);

public sealed record ConnectDoctorMembershipDto(
    Guid Id,
    Guid DoctorId,
    string DoctorFullName,
    string DoctorPhone,
    string? DoctorLicenseNumber,
    string? DoctorSpecialty,
    Guid ClinicTenantId,
    string ClinicTenantCode,
    string ClinicTenantName,
    string MembershipRole,
    string MembershipStatus,
    string InitiatedBy,
    string? Notes,
    DateTime InvitedAt,
    DateTime? RespondedAt,
    DateTime CreatedAt);

public sealed record InviteDoctorMembershipRequest(
    string FullName,
    string Phone,
    string? LicenseNumber = null,
    string? Specialty = null,
    string MembershipRole = ConnectMembershipRoles.Attending,
    string? Notes = null);

public sealed record RequestDoctorMembershipRequest(
    Guid DoctorId,
    Guid ClinicTenantId,
    string MembershipRole = ConnectMembershipRoles.Attending,
    string? Notes = null);

public interface IConnectDoctorMembershipService
{
    Task<IReadOnlyList<ConnectDoctorMembershipDto>> ListForClinicAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectDoctorMembershipDto>> ListPendingForClinicAsync(
        CancellationToken cancellationToken = default);

    Task<ConnectDoctorMembershipDto> InviteAsync(
        InviteDoctorMembershipRequest request,
        CancellationToken cancellationToken = default);

    Task<ConnectDoctorMembershipDto?> ConfirmInviteAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default);

    Task<ConnectDoctorMembershipDto?> ApproveAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default);

    Task<ConnectDoctorMembershipDto?> RejectAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default);

    Task<ConnectDoctorMembershipDto?> RevokeAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pharmacy read: active doctors of a clinic partner, only if C1 org link is active.
    /// </summary>
    Task<IReadOnlyList<ConnectDoctorDto>> ListPartnerClinicDoctorsAsync(
        Guid partnerClinicTenantId,
        CancellationToken cancellationToken = default);
}
