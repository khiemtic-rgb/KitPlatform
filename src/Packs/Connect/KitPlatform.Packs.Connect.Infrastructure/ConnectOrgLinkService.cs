using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectOrgLinkService : IConnectOrgLinkService
{
    private readonly ConnectOrgLinkRepository _repo;
    private readonly ConnectOrgProfileRepository _profiles;

    public ConnectOrgLinkService(ConnectOrgLinkRepository repo, ConnectOrgProfileRepository profiles)
    {
        _repo = repo;
        _profiles = profiles;
    }

    public Task<IReadOnlyList<ConnectOrgLinkDto>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default) =>
        _repo.ListForTenantAsync(status, cancellationToken);

    public Task<IReadOnlyList<ConnectOrgLinkDto>> ListPendingIncomingAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListPendingIncomingAsync(cancellationToken);

    public Task<IReadOnlyList<ConnectDirectoryEntryDto>> SearchDirectoryAsync(
        string? query,
        CancellationToken cancellationToken = default) =>
        _repo.SearchDirectoryAsync(query, cancellationToken);

    public async Task<ConnectOrgLinkDto> InviteAsync(
        InviteConnectOrgLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var ourProfile = await _profiles.GetMyProfileAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Tenant chưa có Connect org profile (pharmacy/clinic). Liên hệ vận hành.");

        var code = request.PartnerTenantCode?.Trim()
            ?? throw new InvalidOperationException("Mã tổ chức đối tác không được để trống.");

        var partner = await _repo.ResolveTenantByCodeAsync(code, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy tổ chức đối tác.");

        if (partner.Id == _repo.CurrentTenantId)
            throw new InvalidOperationException("Không thể liên kết với chính tổ chức của bạn.");

        var partnerKind = await _profiles.GetOrgKindAsync(partner.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                "Đối tác chưa có Connect org profile (pharmacy/clinic).");

        // Roles are derived from durable profiles — request fields must match or be omitted.
        var ourRole = ourProfile.OrgKind;
        var partnerRole = partnerKind;

        if (!string.IsNullOrWhiteSpace(request.OurOrgRole)
            && !string.Equals(request.OurOrgRole.Trim(), ourRole, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Vai trò của chúng ta phải là '{ourRole}' theo org profile Connect.");

        if (!string.IsNullOrWhiteSpace(request.PartnerOrgRole)
            && !string.Equals(request.PartnerOrgRole.Trim(), partnerRole, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Vai trò đối tác phải là '{partnerRole}' theo org profile Connect.");

        if (string.Equals(ourRole, partnerRole, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Connect chỉ liên kết Pharmacy ↔ Clinic (hai bên phải khác org_kind).");

        var linkId = await _repo.UpsertInviteAsync(
            partner.Id,
            ourRole,
            partnerRole,
            request.Notes,
            cancellationToken);

        return (await _repo.GetViewAsync(linkId, cancellationToken))!;
    }

    public Task<ConnectOrgLinkDto> RequestAsync(
        RequestConnectOrgLinkRequest request,
        CancellationToken cancellationToken = default) =>
        InviteAsync(
            new InviteConnectOrgLinkRequest(
                request.PartnerTenantCode,
                request.OurOrgRole,
                request.PartnerOrgRole,
                request.Notes),
            cancellationToken);

    public async Task<ConnectOrgLinkDto?> AcceptAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetRawAsync(linkId, cancellationToken);
        if (row is null) return null;

        if (row.LinkStatus != ConnectOrgLinkStatuses.PendingPartnerAccept)
            throw new InvalidOperationException("Liên kết không ở trạng thái chờ chấp nhận.");

        if (row.PartnerTenantId != _repo.CurrentTenantId)
            throw new InvalidOperationException("Chỉ tổ chức được mời mới được chấp nhận liên kết.");

        var ok = await _repo.UpdateStatusAsync(
            linkId,
            ConnectOrgLinkStatuses.PendingPartnerAccept,
            ConnectOrgLinkStatuses.Active,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(linkId, cancellationToken);
    }

    public Task<ConnectOrgLinkDto?> ApproveAsync(Guid linkId, CancellationToken cancellationToken = default) =>
        AcceptAsync(linkId, cancellationToken);

    public async Task<ConnectOrgLinkDto?> RejectAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetRawAsync(linkId, cancellationToken);
        if (row is null) return null;

        if (row.LinkStatus != ConnectOrgLinkStatuses.PendingPartnerAccept)
            throw new InvalidOperationException("Chỉ từ chối được liên kết đang chờ phản hồi.");

        var ok = await _repo.UpdateStatusAsync(
            linkId,
            ConnectOrgLinkStatuses.PendingPartnerAccept,
            ConnectOrgLinkStatuses.Rejected,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(linkId, cancellationToken);
    }

    public async Task<ConnectOrgLinkDto?> RevokeAsync(Guid linkId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetRawAsync(linkId, cancellationToken);
        if (row is null) return null;

        if (row.LinkStatus != ConnectOrgLinkStatuses.Active)
            throw new InvalidOperationException("Chỉ thu hồi liên kết đang active.");

        var ok = await _repo.UpdateStatusAsync(
            linkId,
            ConnectOrgLinkStatuses.Active,
            ConnectOrgLinkStatuses.Revoked,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetViewAsync(linkId, cancellationToken);
    }
}
