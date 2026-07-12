using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectStatusEventService : IConnectStatusEventService
{
    private readonly ConnectStatusEventRepository _repo;
    private readonly ConnectOrgProfileRepository _profiles;
    private readonly IConnectNotifyService _notify;
    private readonly ConnectRxHandoffRepository _handoffs;

    public ConnectStatusEventService(
        ConnectStatusEventRepository repo,
        ConnectOrgProfileRepository profiles,
        IConnectNotifyService notify,
        ConnectRxHandoffRepository handoffs)
    {
        _repo = repo;
        _profiles = profiles;
        _notify = notify;
        _handoffs = handoffs;
    }

    public Task<IReadOnlyList<ConnectStatusEventDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default) =>
        _repo.ListForTenantAsync(status, cancellationToken);

    public async Task<IReadOnlyList<ConnectStatusEventDto>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Pharmacy, cancellationToken);
        return await _repo.ListPendingForPharmacyAsync(cancellationToken);
    }

    public async Task<ConnectStatusEventDto> CreateManualReadyAsync(
        CreateConnectStatusEventRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Clinic, cancellationToken);

        if (request.PharmacyTenantId == Guid.Empty)
            throw new InvalidOperationException("PharmacyTenantId không hợp lệ.");

        var pharmacyKind = await _profiles.GetOrgKindAsync(request.PharmacyTenantId, cancellationToken);
        if (!string.Equals(pharmacyKind, ConnectOrgKinds.Pharmacy, StringComparison.Ordinal))
            throw new InvalidOperationException("Đích phải là org_kind=pharmacy.");

        if (!await _repo.HasActiveOrgLinkWithAsync(request.PharmacyTenantId, cancellationToken))
            throw new InvalidOperationException(
                "Chỉ gửi tín hiệu tới Pharmacy đã liên kết active.");

        var name = request.PatientDisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(name) && name.Length < 2)
            throw new InvalidOperationException("Tên bệnh nhân không hợp lệ.");

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.PatientPhone))
        {
            phone = new string(request.PatientPhone.Where(char.IsDigit).ToArray());
            if (phone.Length < 9)
                throw new InvalidOperationException("Số điện thoại không hợp lệ.");
        }

        var summary = string.IsNullOrWhiteSpace(request.Summary)
            ? "Clinic đánh dấu sẵn sàng lấy tại nhà thuốc (manual)."
            : request.Summary.Trim();

        var id = await _repo.TryInsertReadyAsync(
            request.PharmacyTenantId,
            _repo.CurrentTenantId,
            ConnectStatusSourceTypes.Manual,
            sourceId: null,
            string.IsNullOrWhiteSpace(name) ? null : name,
            phone,
            summary,
            cancellationToken);

        if (id is null)
            throw new InvalidOperationException("Không tạo được status event.");

        var view = (await _repo.GetViewAsync(id.Value, cancellationToken))!;
        await _notify.NotifyReadyToDispenseAsync(view, cancellationToken);
        return view;
    }

    public async Task<ConnectStatusEventDto?> ConsumeAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Pharmacy, cancellationToken);
        var row = await _repo.GetRawAsync(eventId, cancellationToken);
        if (row is null) return null;
        if (row.PharmacyTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Pharmacy nhận mới consume được event.");
        if (row.EventStatus != ConnectStatusEventStatuses.PendingPharmacy)
            throw new InvalidOperationException("Chỉ consume event đang pending.");

        var ok = await _repo.UpdateStatusAsync(
            eventId,
            ConnectStatusEventStatuses.PendingPharmacy,
            ConnectStatusEventStatuses.Consumed,
            cancellationToken);
        if (!ok) return null;

        // ClinicRx: «Đã nhận tín hiệu» chỉ ack hàng đợi Connect — KHÔNG đánh dấu
        // rx_handoff = consumed (đó là lúc POS hoàn tất bán và gắn connect_rx_handoff_id).
        // Trước đây cascade nhầm → handoff «đã xử lý» nhưng báo cáo SALES-05 trống.

        return await _repo.GetViewAsync(eventId, cancellationToken);
    }

    public async Task<ConnectStatusEventDto?> DismissAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Pharmacy, cancellationToken);
        var row = await _repo.GetRawAsync(eventId, cancellationToken);
        if (row is null) return null;
        if (row.PharmacyTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ Pharmacy nhận mới dismiss được event.");
        if (row.EventStatus != ConnectStatusEventStatuses.PendingPharmacy)
            throw new InvalidOperationException("Chỉ dismiss event đang pending.");

        var ok = await _repo.UpdateStatusAsync(
            eventId,
            ConnectStatusEventStatuses.PendingPharmacy,
            ConnectStatusEventStatuses.Dismissed,
            cancellationToken);
        if (!ok) return null;

        if (row.SourceType == ConnectStatusSourceTypes.ClinicRx)
        {
            await _handoffs.UpdateStatusByEventAsync(
                eventId,
                ConnectRxHandoffStatuses.Dismissed,
                cancellationToken);
            if (row.SourceId is Guid handoffId)
                await _handoffs.UpdateStatusByIdAsync(
                    handoffId,
                    ConnectRxHandoffStatuses.Dismissed,
                    cancellationToken);
        }

        return await _repo.GetViewAsync(eventId, cancellationToken);
    }

    public async Task EmitReadyFromSourceAsync(
        string sourceType,
        Guid sourceId,
        Guid pharmacyTenantId,
        Guid clinicTenantId,
        string? patientDisplayName,
        string? patientPhone,
        string summary,
        CancellationToken cancellationToken = default)
    {
        if (pharmacyTenantId == Guid.Empty || clinicTenantId == Guid.Empty || sourceId == Guid.Empty)
            return;

        if (sourceType is not (
            ConnectStatusSourceTypes.Referral
            or ConnectStatusSourceTypes.Booking
            or ConnectStatusSourceTypes.ClinicRx))
            return;

        var id = await _repo.TryInsertReadyAsync(
            pharmacyTenantId,
            clinicTenantId,
            sourceType,
            sourceId,
            patientDisplayName,
            patientPhone,
            summary,
            cancellationToken);

        if (id is null)
            return;

        var view = await _repo.GetViewAsync(id.Value, cancellationToken);
        if (view is not null)
            await _notify.NotifyReadyToDispenseAsync(view, cancellationToken);
    }

    private async Task EnsureKindAsync(string expectedKind, CancellationToken cancellationToken)
    {
        var kind = await _profiles.GetOrgKindAsync(_repo.CurrentTenantId, cancellationToken);
        if (!string.Equals(kind, expectedKind, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Thao tác này chỉ dành cho tổ chức Connect org_kind={expectedKind}.");
    }
}
