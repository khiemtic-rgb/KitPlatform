using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectDoctorMembershipService : IConnectDoctorMembershipService
{
    private readonly ConnectDoctorMembershipRepository _repo;
    private readonly ConnectOrgProfileRepository _profiles;

    public ConnectDoctorMembershipService(
        ConnectDoctorMembershipRepository repo,
        ConnectOrgProfileRepository profiles)
    {
        _repo = repo;
        _profiles = profiles;
    }

    public async Task<IReadOnlyList<ConnectDoctorMembershipDto>> ListForClinicAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        return await _repo.ListForClinicAsync(status, cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectDoctorMembershipDto>> ListPendingForClinicAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        return await _repo.ListPendingForClinicAsync(cancellationToken);
    }

    public async Task<ConnectDoctorMembershipDto> InviteAsync(
        InviteDoctorMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);

        var fullName = request.FullName?.Trim()
            ?? throw new InvalidOperationException("Họ tên bác sĩ không được để trống.");
        if (fullName.Length < 2)
            throw new InvalidOperationException("Họ tên bác sĩ không hợp lệ.");

        var phone = NormalizePhone(request.Phone);
        var role = NormalizeRole(request.MembershipRole);
        var license = string.IsNullOrWhiteSpace(request.LicenseNumber)
            ? null
            : request.LicenseNumber.Trim();
        var specialty = string.IsNullOrWhiteSpace(request.Specialty)
            ? null
            : request.Specialty.Trim();

        var doctorId = await _repo.UpsertDoctorAsync(
            fullName,
            phone,
            license,
            specialty,
            cancellationToken);

        var membershipId = await _repo.UpsertClinicInviteAsync(
            doctorId,
            role,
            request.Notes,
            cancellationToken);

        return (await _repo.GetViewAsync(membershipId, cancellationToken))!;
    }

    public async Task<ConnectDoctorMembershipDto?> ConfirmInviteAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(membershipId, cancellationToken);
        if (row is null) return null;

        if (row.MembershipStatus != ConnectMembershipStatuses.PendingDoctorAccept)
            throw new InvalidOperationException("Membership không ở trạng thái chờ bác sĩ chấp nhận.");

        var ok = await _repo.UpdateStatusAsync(
            membershipId,
            ConnectMembershipStatuses.PendingDoctorAccept,
            ConnectMembershipStatuses.Active,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(membershipId, cancellationToken);
    }

    public async Task<ConnectDoctorMembershipDto?> ApproveAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(membershipId, cancellationToken);
        if (row is null) return null;

        if (row.MembershipStatus != ConnectMembershipStatuses.PendingClinicApproval)
            throw new InvalidOperationException("Membership không ở trạng thái chờ phòng khám duyệt.");

        var ok = await _repo.UpdateStatusAsync(
            membershipId,
            ConnectMembershipStatuses.PendingClinicApproval,
            ConnectMembershipStatuses.Active,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(membershipId, cancellationToken);
    }

    public async Task<ConnectDoctorMembershipDto?> RejectAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(membershipId, cancellationToken);
        if (row is null) return null;

        var expected = row.MembershipStatus;
        if (expected is not (
            ConnectMembershipStatuses.PendingDoctorAccept
            or ConnectMembershipStatuses.PendingClinicApproval))
            throw new InvalidOperationException("Chỉ từ chối được membership đang chờ phản hồi.");

        var ok = await _repo.UpdateStatusAsync(
            membershipId,
            expected,
            ConnectMembershipStatuses.Rejected,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(membershipId, cancellationToken);
    }

    public async Task<ConnectDoctorMembershipDto?> RevokeAsync(
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(membershipId, cancellationToken);
        if (row is null) return null;

        if (row.MembershipStatus != ConnectMembershipStatuses.Active)
            throw new InvalidOperationException("Chỉ thu hồi membership đang active.");

        var ok = await _repo.UpdateStatusAsync(
            membershipId,
            ConnectMembershipStatuses.Active,
            ConnectMembershipStatuses.Revoked,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(membershipId, cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectDoctorDto>> ListPartnerClinicDoctorsAsync(
        Guid partnerClinicTenantId,
        CancellationToken cancellationToken = default)
    {
        if (partnerClinicTenantId == Guid.Empty)
            throw new InvalidOperationException("partnerClinicTenantId không hợp lệ.");

        var partnerKind = await _profiles.GetOrgKindAsync(partnerClinicTenantId, cancellationToken);
        if (!string.Equals(partnerKind, ConnectOrgKinds.Clinic, StringComparison.Ordinal))
            throw new InvalidOperationException("Đối tác phải là Clinic trong Connect org profile.");

        if (partnerClinicTenantId == _repo.CurrentTenantId)
        {
            await EnsureClinicAsync(cancellationToken);
            return await _repo.ListActiveDoctorsForClinicAsync(partnerClinicTenantId, cancellationToken);
        }

        var linked = await _repo.HasActiveOrgLinkWithAsync(partnerClinicTenantId, cancellationToken);
        if (!linked)
            throw new InvalidOperationException(
                "Chỉ xem được bác sĩ của phòng khám đã liên kết active trên Connect Network.");

        return await _repo.ListActiveDoctorsForClinicAsync(partnerClinicTenantId, cancellationToken);
    }

    private async Task EnsureClinicAsync(CancellationToken cancellationToken)
    {
        var kind = await _profiles.GetOrgKindAsync(_repo.CurrentTenantId, cancellationToken);
        if (!string.Equals(kind, ConnectOrgKinds.Clinic, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Chỉ tổ chức Clinic (Connect org profile) mới quản lý membership bác sĩ.");
    }

    private static string NormalizePhone(string? phone)
    {
        var value = phone?.Trim() ?? throw new InvalidOperationException("Số điện thoại không được để trống.");
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 9)
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");
        return digits;
    }

    private static string NormalizeRole(string? role)
    {
        var value = string.IsNullOrWhiteSpace(role)
            ? ConnectMembershipRoles.Attending
            : role.Trim().ToLowerInvariant();
        if (value is not (
            ConnectMembershipRoles.Attending
            or ConnectMembershipRoles.Consultant
            or ConnectMembershipRoles.Owner))
            throw new InvalidOperationException("Vai trò membership phải là attending, consultant hoặc owner.");
        return value;
    }
}
