using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectRxHandoffService : IConnectRxHandoffService
{
    private readonly ConnectRxHandoffRepository _repo;
    private readonly ConnectStatusEventRepository _events;
    private readonly ConnectOrgProfileRepository _profiles;
    private readonly IConnectNotifyService _notify;

    public ConnectRxHandoffService(
        ConnectRxHandoffRepository repo,
        ConnectStatusEventRepository events,
        ConnectOrgProfileRepository profiles,
        IConnectNotifyService notify)
    {
        _repo = repo;
        _events = events;
        _profiles = profiles;
        _notify = notify;
    }

    public async Task<ConnectRxHandoffDto> CreateFromClinicAsync(
        CreateConnectRxHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKindAsync(ConnectOrgKinds.Clinic, cancellationToken);

        if (request.PharmacyTenantId == Guid.Empty)
            throw new InvalidOperationException("PharmacyTenantId không hợp lệ.");
        if (request.ClinicPrescriptionId == Guid.Empty)
            throw new InvalidOperationException("ClinicPrescriptionId không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.PrescriptionCode))
            throw new InvalidOperationException("PrescriptionCode bắt buộc.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("Đơn handoff phải có ít nhất một dòng thuốc.");

        var pharmacyKind = await _profiles.GetOrgKindAsync(request.PharmacyTenantId, cancellationToken);
        if (!string.Equals(pharmacyKind, ConnectOrgKinds.Pharmacy, StringComparison.Ordinal))
            throw new InvalidOperationException("Đích phải là org_kind=pharmacy.");

        if (!await _events.HasActiveOrgLinkWithAsync(request.PharmacyTenantId, cancellationToken))
            throw new InvalidOperationException(
                "Chỉ gửi đơn tới Pharmacy đã liên kết Connect (active).");

        var existing = await _repo.GetByClinicPrescriptionAsync(
            _repo.CurrentTenantId,
            request.ClinicPrescriptionId,
            cancellationToken);
        if (existing is not null)
            return ConnectRxHandoffRepository.ToDto(existing);

        var handoffId = await _repo.InsertAsync(
            request.PharmacyTenantId,
            request.ClinicPrescriptionId,
            request.PrescriptionCode.Trim(),
            string.IsNullOrWhiteSpace(request.PatientDisplayName)
                ? null
                : request.PatientDisplayName.Trim(),
            NormalizePhone(request.PatientPhone),
            string.IsNullOrWhiteSpace(request.ProviderDisplayName)
                ? null
                : request.ProviderDisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.DiagnosisText) ? null : request.DiagnosisText.Trim(),
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            request.PdfSha256,
            request.Lines,
            cancellationToken);

        var summary =
            $"Đơn PK {request.PrescriptionCode.Trim()} — {request.Lines.Count} dòng · sẵn sàng lấy tại NT";

        var eventId = await _events.TryInsertReadyAsync(
            request.PharmacyTenantId,
            _repo.CurrentTenantId,
            ConnectStatusSourceTypes.ClinicRx,
            handoffId,
            string.IsNullOrWhiteSpace(request.PatientDisplayName)
                ? null
                : request.PatientDisplayName.Trim(),
            NormalizePhone(request.PatientPhone),
            summary,
            cancellationToken);

        if (eventId is Guid eid)
        {
            await _repo.SetStatusEventIdAsync(handoffId, eid, cancellationToken);
            var view = await _events.GetViewAsync(eid, cancellationToken);
            if (view is not null)
                await _notify.NotifyReadyToDispenseAsync(view, cancellationToken);
        }

        var row = await _repo.GetAsync(handoffId, cancellationToken)
            ?? throw new InvalidOperationException("Không đọc được handoff vừa tạo.");
        return ConnectRxHandoffRepository.ToDto(row);
    }

    public async Task<IReadOnlyList<ConnectRxHandoffDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListForTenantAsync(status, cancellationToken);
        return rows.Select(ConnectRxHandoffRepository.ToDto).ToList();
    }

    public async Task<ConnectRxHandoffDto?> GetAsync(
        Guid handoffId,
        CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetAsync(handoffId, cancellationToken);
        return row is null ? null : ConnectRxHandoffRepository.ToDto(row);
    }

    public async Task MarkFromStatusEventAsync(
        Guid statusEventId,
        string handoffStatus,
        CancellationToken cancellationToken = default)
    {
        if (handoffStatus is not (
            ConnectRxHandoffStatuses.Consumed or ConnectRxHandoffStatuses.Dismissed))
            return;

        await _repo.UpdateStatusByEventAsync(statusEventId, handoffStatus, cancellationToken);
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 9 ? digits : null;
    }

    private async Task EnsureKindAsync(string expectedKind, CancellationToken cancellationToken)
    {
        var kind = await _profiles.GetOrgKindAsync(_repo.CurrentTenantId, cancellationToken);
        if (!string.Equals(kind, expectedKind, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Thao tác này chỉ dành cho tổ chức Connect org_kind={expectedKind}.");
    }
}
