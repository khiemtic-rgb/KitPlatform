using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectBookingService : IConnectBookingService
{
    private readonly ConnectBookingRepository _repo;
    private readonly ConnectOrgProfileRepository _profiles;
    private readonly IConnectNotifyService _notify;
    private readonly IConnectStatusEventService _statusEvents;
    private readonly IConnectClinicCalendarBridge _calendar;

    public ConnectBookingService(
        ConnectBookingRepository repo,
        ConnectOrgProfileRepository profiles,
        IConnectNotifyService notify,
        IConnectStatusEventService statusEvents,
        IConnectClinicCalendarBridge calendar)
    {
        _repo = repo;
        _profiles = profiles;
        _notify = notify;
        _statusEvents = statusEvents;
        _calendar = calendar;
    }

    public Task<IReadOnlyList<ConnectBookingDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default) =>
        _repo.ListForTenantAsync(status, cancellationToken);

    public async Task<ConnectBookingDto> CreateAsync(
        CreateConnectBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);

        if (request.ScheduledAt == default)
            throw new InvalidOperationException("Thời gian hẹn không hợp lệ.");

        var duration = request.DurationMinutes <= 0 ? 30 : request.DurationMinutes;
        if (duration is < 5 or > 480)
            throw new InvalidOperationException("Thời lượng hẹn phải từ 5–480 phút.");

        Guid? pharmacyId = request.PharmacyTenantId is Guid p && p != Guid.Empty ? p : null;
        Guid? referralId = request.ReferralId is Guid r && r != Guid.Empty ? r : null;
        Guid? doctorId = request.DoctorId is Guid d && d != Guid.Empty ? d : null;
        Guid? pharmacyCustomerId = null;

        var patientName = request.PatientDisplayName?.Trim();
        var patientPhone = NormalizePhoneOptional(request.PatientPhone);

        if (referralId is Guid rid)
        {
            var referral = await _repo.GetReferralAsync(rid, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy referral.");

            if (referral.ClinicTenantId != _repo.CurrentTenantId)
                throw new InvalidOperationException("Referral không thuộc Clinic hiện tại.");

            if (referral.ReferralStatus is not (
                ConnectReferralStatuses.Accepted or ConnectReferralStatuses.Completed))
                throw new InvalidOperationException(
                    "Chỉ tạo booking từ referral đã accepted hoặc completed.");

            pharmacyId ??= referral.PharmacyTenantId;
            doctorId ??= referral.DoctorId;
            pharmacyCustomerId = referral.PharmacyCustomerId;
            if (string.IsNullOrWhiteSpace(patientName))
                patientName = referral.PatientDisplayName;
            patientPhone ??= NormalizePhoneOptional(referral.PatientPhone);

            // NT→PK: pharmacy trên booking phải trùng referral
            if (pharmacyId is Guid phFromReq && phFromReq != referral.PharmacyTenantId)
                throw new InvalidOperationException(
                    "pharmacyTenantId phải trùng nhà thuốc trên referral đã chọn.");
        }
        else if (pharmacyId is not null)
        {
            // Gate cứng: gắn NT = luồng giới thiệu → bắt buộc referral accepted
            throw new InvalidOperationException(
                "Booking gắn nhà thuốc (NT→PK) bắt buộc chọn referral đã accepted/completed. " +
                "Đặt lịch nội bộ PK: để trống nhà thuốc và referral.");
        }

        if (string.IsNullOrWhiteSpace(patientName) || patientName.Length < 2)
            throw new InvalidOperationException("Tên bệnh nhân không hợp lệ.");

        if (pharmacyId is Guid pharmId)
        {
            var kind = await _profiles.GetOrgKindAsync(pharmId, cancellationToken);
            if (!string.Equals(kind, ConnectOrgKinds.Pharmacy, StringComparison.Ordinal))
                throw new InvalidOperationException("pharmacyTenantId phải là org_kind=pharmacy.");

            if (!await _repo.HasActiveOrgLinkWithAsync(pharmId, cancellationToken))
                throw new InvalidOperationException(
                    "Pharmacy phải có org link active với Clinic.");
        }

        if (doctorId is Guid docId)
        {
            if (!await _repo.DoctorActiveAtClinicAsync(docId, cancellationToken))
                throw new InvalidOperationException(
                    "Bác sĩ phải có membership active tại Clinic.");
        }

        var modality = ConnectEncounterModalities.Normalize(request.EncounterModality);
        if (modality == ConnectEncounterModalities.RemoteVideo)
            throw new InvalidOperationException(
                "Khám video trong Novixa chưa bật. Chọn khám từ xa (gọi ngoài) hoặc tại phòng khám.");

        var id = await _repo.InsertAsync(
            pharmacyId,
            referralId,
            doctorId,
            pharmacyCustomerId,
            patientName,
            patientPhone,
            DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc),
            duration,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            modality,
            cancellationToken);

        var view = (await _repo.GetViewAsync(id, cancellationToken))!;
        await _notify.NotifyBookingProposedAsync(view, cancellationToken);
        await _repo.MarkNotifiedAsync(id, cancellationToken);
        return (await _repo.GetViewAsync(id, cancellationToken))!;
    }

    public async Task<ConnectBookingDto?> ConfirmAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(bookingId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic sở hữu mới xác nhận booking.");
        if (row.BookingStatus != ConnectBookingStatuses.Proposed)
            throw new InvalidOperationException("Chỉ xác nhận booking đang proposed.");

        var ok = await _repo.UpdateStatusAsync(
            bookingId,
            ConnectBookingStatuses.Proposed,
            ConnectBookingStatuses.Confirmed,
            cancellationToken);
        if (!ok) return null;

        var view = await _repo.GetViewAsync(bookingId, cancellationToken);
        if (view is not null)
        {
            await _notify.NotifyBookingConfirmedAsync(view, cancellationToken);
            await _repo.MarkNotifiedAsync(bookingId, cancellationToken);
            try
            {
                Guid? clinicCustomerId = null;
                if (view.ReferralId is Guid refId)
                {
                    var referral = await _repo.GetReferralAsync(refId, cancellationToken);
                    clinicCustomerId = referral?.ClinicCustomerId;
                }

                await _calendar.OnBookingConfirmedAsync(
                    view.Id,
                    view.PatientDisplayName,
                    view.PatientPhone,
                    view.ScheduledAt,
                    view.DurationMinutes,
                    view.Notes,
                    view.PharmacyCustomerId,
                    clinicCustomerId,
                    view.EncounterModality,
                    view.PharmacyTenantId,
                    view.ReferralId,
                    cancellationToken);
            }
            catch
            {
                // Bridge is best-effort — booking confirm must not fail if calendar sync fails
            }
            view = await _repo.GetViewAsync(bookingId, cancellationToken);
        }

        return view;
    }

    public async Task<ConnectBookingDto?> CancelAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(bookingId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic sở hữu mới hủy booking.");
        if (row.BookingStatus is not (ConnectBookingStatuses.Proposed or ConnectBookingStatuses.Confirmed))
            throw new InvalidOperationException("Chỉ hủy booking proposed/confirmed.");

        var ok = await _repo.UpdateStatusAsync(
            bookingId,
            row.BookingStatus,
            ConnectBookingStatuses.Cancelled,
            cancellationToken);
        if (!ok) return null;
        try { await _calendar.OnBookingCancelledAsync(bookingId, cancellationToken); } catch { /* best-effort */ }
        return await _repo.GetViewAsync(bookingId, cancellationToken);
    }

    public async Task<ConnectBookingDto?> CompleteAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(bookingId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic sở hữu mới hoàn tất booking.");
        if (row.BookingStatus != ConnectBookingStatuses.Confirmed)
            throw new InvalidOperationException("Chỉ hoàn tất booking đã confirmed.");

        var ok = await _repo.UpdateStatusAsync(
            bookingId,
            ConnectBookingStatuses.Confirmed,
            ConnectBookingStatuses.Completed,
            cancellationToken);
        if (!ok) return null;

        var view = await _repo.GetViewAsync(bookingId, cancellationToken);
        if (view?.PharmacyTenantId is Guid pharmacyId)
        {
            await _statusEvents.EmitReadyFromSourceAsync(
                ConnectStatusSourceTypes.Booking,
                bookingId,
                pharmacyId,
                view.ClinicTenantId,
                view.PatientDisplayName,
                view.PatientPhone,
                $"Booking hoàn tất — sẵn sàng hỗ trợ tại nhà thuốc ({view.PatientDisplayName}).",
                cancellationToken);
        }

        try { await _calendar.OnBookingCompletedAsync(bookingId, cancellationToken); } catch { /* best-effort */ }
        return view;
    }

    public async Task<ConnectBookingDto?> MarkNoShowAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClinicAsync(cancellationToken);
        var row = await _repo.GetRawAsync(bookingId, cancellationToken);
        if (row is null) return null;
        if (row.ClinicTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Clinic sở hữu mới đánh dấu no-show.");
        if (row.BookingStatus != ConnectBookingStatuses.Confirmed)
            throw new InvalidOperationException("Chỉ no-show trên booking đã confirmed.");

        var ok = await _repo.UpdateStatusAsync(
            bookingId,
            ConnectBookingStatuses.Confirmed,
            ConnectBookingStatuses.NoShow,
            cancellationToken);
        if (!ok) return null;
        try { await _calendar.OnBookingNoShowAsync(bookingId, cancellationToken); } catch { /* best-effort */ }
        return await _repo.GetViewAsync(bookingId, cancellationToken);
    }

    private async Task EnsureClinicAsync(CancellationToken cancellationToken)
    {
        var kind = await _profiles.GetOrgKindAsync(_repo.CurrentTenantId, cancellationToken);
        if (!string.Equals(kind, ConnectOrgKinds.Clinic, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Chỉ tổ chức Clinic mới quản lý booking Connect.");
    }

    private static string? NormalizePhoneOptional(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 9)
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");
        return digits;
    }
}
