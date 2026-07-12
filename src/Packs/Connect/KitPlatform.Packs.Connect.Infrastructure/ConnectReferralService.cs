using KitPlatform.Application.Customers;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectReferralService : IConnectReferralService
{
    private readonly ConnectReferralRepository _repo;
    private readonly ConnectOrgProfileRepository _profiles;
    private readonly IConnectStatusEventService _statusEvents;
    private readonly ICustomerAdminService _customers;

    public ConnectReferralService(
        ConnectReferralRepository repo,
        ConnectOrgProfileRepository profiles,
        IConnectStatusEventService statusEvents,
        ICustomerAdminService customers)
    {
        _repo = repo;
        _profiles = profiles;
        _statusEvents = statusEvents;
        _customers = customers;
    }

    public Task<IReadOnlyList<ConnectReferralDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default) =>
        _repo.ListForTenantAsync(status, cancellationToken);

    public async Task<IReadOnlyList<ConnectReferralDto>> ListInboxAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Clinic, cancellationToken);
        return await _repo.ListInboxAsync(cancellationToken);
    }

    public async Task<ConnectReferralDto> CreateAsync(
        CreateConnectReferralRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Pharmacy, cancellationToken);

        if (request.ClinicTenantId == Guid.Empty)
            throw new InvalidOperationException("ClinicTenantId không hợp lệ.");

        var clinicKind = await _profiles.GetOrgKindAsync(request.ClinicTenantId, cancellationToken);
        if (!string.Equals(clinicKind, ConnectOrgKinds.Clinic, StringComparison.Ordinal))
            throw new InvalidOperationException("Đích referral phải là Clinic trong Connect org profile.");

        if (!await _repo.HasActiveOrgLinkWithAsync(request.ClinicTenantId, cancellationToken))
            throw new InvalidOperationException(
                "Chỉ gửi referral tới Clinic đã liên kết active trên Connect Network.");

        if (request.PharmacyCustomerId == Guid.Empty)
            throw new InvalidOperationException("Chọn khách hàng CRM nhà thuốc (không nhập tay tách CRM).");

        var customer = await _repo.GetOwnCustomerAsync(request.PharmacyCustomerId, cancellationToken)
            ?? throw new InvalidOperationException("Khách hàng không thuộc nhà thuốc hiện tại.");

        var name = string.IsNullOrWhiteSpace(request.PatientDisplayName)
            ? customer.FullName.Trim()
            : request.PatientDisplayName.Trim();
        if (name.Length < 2)
            throw new InvalidOperationException("Tên bệnh nhân không hợp lệ.");

        string? phone = NormalizePhoneOptional(customer.Phone);
        if (!string.IsNullOrWhiteSpace(request.PatientPhone))
            phone = NormalizePhoneOptional(request.PatientPhone) ?? phone;
        if (phone is null)
            throw new InvalidOperationException(
                "Khách hàng cần có SĐT hợp lệ để giới thiệu / đồng bộ sau.");

        Guid? doctorId = request.DoctorId;
        if (doctorId is Guid docId && docId != Guid.Empty)
        {
            if (!await _repo.DoctorActiveAtClinicAsync(docId, request.ClinicTenantId, cancellationToken))
                throw new InvalidOperationException(
                    "Bác sĩ phải có membership active tại Clinic nhận referral.");
        }
        else
        {
            doctorId = null;
        }

        var id = await _repo.InsertAsync(
            request.ClinicTenantId,
            doctorId,
            request.PharmacyCustomerId,
            name,
            phone,
            string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            cancellationToken);

        return (await _repo.GetViewAsync(id, cancellationToken))!;
    }

    private static string? NormalizePhoneOptional(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 9 ? digits : null;
    }

    public async Task<ConnectReferralDto?> AcceptAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Clinic, cancellationToken);
        var row = await _repo.GetRawAsync(referralId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic nhận mới được chấp nhận referral.");
        if (row.ReferralStatus != ConnectReferralStatuses.PendingClinicAccept)
            throw new InvalidOperationException("Referral không ở trạng thái chờ chấp nhận.");

        var ok = await _repo.UpdateStatusAsync(
            referralId,
            ConnectReferralStatuses.PendingClinicAccept,
            ConnectReferralStatuses.Accepted,
            cancellationToken);
        if (!ok) return null;

        var view = await _repo.GetViewAsync(referralId, cancellationToken);
        if (view is null) return null;

        // Continuity NT→PK: provision BN on clinic CRM so they appear under Clinic → Bệnh nhân
        try
        {
            var clinicCustomerId = await EnsureClinicCustomerAsync(
                view.PatientDisplayName,
                view.PatientPhone,
                cancellationToken);
            await _repo.SetClinicCustomerIdAsync(referralId, clinicCustomerId, cancellationToken);
            view = await _repo.GetViewAsync(referralId, cancellationToken);
        }
        catch (Exception)
        {
            // Keep accepted even if CRM provision fails — booking confirm can still create BN
        }

        return view;
    }

    private async Task<Guid> EnsureClinicCustomerAsync(
        string patientDisplayName,
        string? patientPhone,
        CancellationToken cancellationToken)
    {
        var phone = NormalizePhoneOptional(patientPhone)
            ?? throw new InvalidOperationException("Referral thiếu SĐT — không tạo được BN trên PK.");

        var found = await _customers.ListAsync(phone, 1, 5, cancellationToken);
        var hit = found.Items.FirstOrDefault(c =>
            NormalizePhoneOptional(c.Phone) == phone);
        if (hit is not null) return hit.Id;

        var name = patientDisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
            name = $"BN {phone}";

        try
        {
            var created = await _customers.CreateAsync(
                new CreateCustomerRequest(name, phone),
                cancellationToken);
            return created.Id;
        }
        catch (InvalidOperationException)
        {
            var again = await _customers.ListAsync(phone, 1, 5, cancellationToken);
            var retry = again.Items.FirstOrDefault(c => NormalizePhoneOptional(c.Phone) == phone);
            if (retry is not null) return retry.Id;
            throw;
        }
    }

    public async Task<ConnectReferralDto?> RejectAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Clinic, cancellationToken);
        var row = await _repo.GetRawAsync(referralId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic nhận mới được từ chối referral.");
        if (row.ReferralStatus != ConnectReferralStatuses.PendingClinicAccept)
            throw new InvalidOperationException("Chỉ từ chối được referral đang chờ.");

        var ok = await _repo.UpdateStatusAsync(
            referralId,
            ConnectReferralStatuses.PendingClinicAccept,
            ConnectReferralStatuses.Rejected,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(referralId, cancellationToken);
    }

    public async Task<ConnectReferralDto?> CompleteAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Clinic, cancellationToken);
        var row = await _repo.GetRawAsync(referralId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic nhận mới được hoàn tất referral.");
        if (row.ReferralStatus != ConnectReferralStatuses.Accepted)
            throw new InvalidOperationException("Chỉ hoàn tất referral đã accepted.");

        var ok = await _repo.UpdateStatusAsync(
            referralId,
            ConnectReferralStatuses.Accepted,
            ConnectReferralStatuses.Completed,
            cancellationToken);
        if (!ok) return null;

        var view = await _repo.GetViewAsync(referralId, cancellationToken);
        if (view is not null)
        {
            await _statusEvents.EmitReadyFromSourceAsync(
                ConnectStatusSourceTypes.Referral,
                referralId,
                view.PharmacyTenantId,
                view.ClinicTenantId,
                view.PatientDisplayName,
                view.PatientPhone,
                $"Referral hoàn tất — sẵn sàng hỗ trợ tại nhà thuốc ({view.PatientDisplayName}).",
                cancellationToken);
        }

        return view;
    }

    public async Task<ConnectReferralDto?> CancelAsync(
        Guid referralId,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Pharmacy, cancellationToken);
        var row = await _repo.GetRawAsync(referralId, cancellationToken);
        if (row is null) return null;
        if (row.PharmacyTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Pharmacy gửi mới được hủy referral.");
        if (row.ReferralStatus != ConnectReferralStatuses.PendingClinicAccept)
            throw new InvalidOperationException("Chỉ hủy được referral đang chờ Clinic.");

        var ok = await _repo.UpdateStatusAsync(
            referralId,
            ConnectReferralStatuses.PendingClinicAccept,
            ConnectReferralStatuses.Cancelled,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(referralId, cancellationToken);
    }

    private async Task EnsureKindAsync(string expectedKind, CancellationToken cancellationToken)
    {
        var kind = await _profiles.GetOrgKindAsync(_repo.CurrentTenantId, cancellationToken);
        if (!string.Equals(kind, expectedKind, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Thao tác này chỉ dành cho tổ chức Connect org_kind={expectedKind}.");
    }
}
