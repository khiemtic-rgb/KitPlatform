using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Packs.Pharmacy.Rx;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class PrescriberLinkService : IPrescriberLinkService
{
    private readonly PrescriberPortalRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IPlatformEventWriter _platformEvents;

    public PrescriberLinkService(
        PrescriberPortalRepository repo,
        ITenantContext tenant,
        IPlatformEventWriter platformEvents)
    {
        _repo = repo;
        _tenant = tenant;
        _platformEvents = platformEvents;
    }

    public Task<IReadOnlyList<PrescriberTenantLinkDto>> ListTenantLinksAsync(
        string? status = null,
        CancellationToken cancellationToken = default) =>
        _repo.ListLinksForTenantAsync(_tenant.TenantId, status, cancellationToken);

    public Task<IReadOnlyList<PrescriberTenantLinkDto>> ListPendingApprovalAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListLinksForTenantAsync(
            _tenant.TenantId,
            PrescriberLinkStatuses.PendingNtApproval,
            cancellationToken);

    public async Task<PrescriberTenantLinkDto> InvitePrescriberAsync(
        InvitePrescriberLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Tên bác sĩ không được để trống.");
        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new InvalidOperationException("Số CCHN (chứng chỉ hành nghề) là bắt buộc.");

        var phone = PrescriberPortalRepository.NormalizePhone(request.Phone);
        if (phone.Length < 9)
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");

        var prescriberId = await _repo.EnsurePrescriberAsync(
            phone,
            request.FullName,
            request.LicenseNumber,
            request.Specialty,
            cancellationToken);

        var linkId = await _repo.UpsertTenantInviteLinkAsync(
            _tenant.TenantId,
            prescriberId,
            _tenant.UserId,
            request.Notes,
            cancellationToken);

        return (await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken))!;
    }

    public async Task<PrescriberTenantLinkDto?> ApproveLinkAsync(
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken);
        if (link is null || link.LinkStatus != PrescriberLinkStatuses.PendingNtApproval)
            return null;
        if (string.IsNullOrWhiteSpace(link.PrescriberLicenseNumber))
            throw new InvalidOperationException(
                "Không thể duyệt: bác sĩ chưa có số CCHN. Yêu cầu bổ sung chứng chỉ hành nghề trước khi kích hoạt.");

        var ok = await _repo.UpdateLinkStatusAsync(
            linkId,
            PrescriberLinkStatuses.PendingNtApproval,
            PrescriberLinkStatuses.Active,
            _tenant.UserId,
            cancellationToken);
        if (!ok)
            return null;

        var activated = await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken);
        if (activated is not null)
        {
            await _platformEvents.PublishForTenantAsync(
                _tenant.TenantId,
                PlatformEventTypes.HealthcarePrescriberLinkActive,
                PlatformEventAggregateTypes.PrescriberTenantLink,
                linkId,
                new
                {
                    linkId,
                    prescriberId = activated.PrescriberId,
                    tenantId = activated.TenantId,
                    initiatedBy = activated.InitiatedBy,
                },
                actorUserId: _tenant.UserId,
                source: PlatformEventSources.HealthcareNetwork,
                cancellationToken: cancellationToken);
        }

        return activated;
    }

    public async Task<PrescriberTenantLinkDto?> RejectLinkAsync(
        Guid linkId,
        RejectPrescriberLinkRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var link = await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken);
        if (link is null)
            return null;

        if (link.LinkStatus != PrescriberLinkStatuses.PendingNtApproval)
            throw new InvalidOperationException("Liên kết không ở trạng thái chờ duyệt.");

        var ok = await _repo.UpdateLinkStatusAsync(
            linkId,
            PrescriberLinkStatuses.PendingNtApproval,
            PrescriberLinkStatuses.Rejected,
            _tenant.UserId,
            cancellationToken);
        return ok ? await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken) : null;
    }

    public async Task<PrescriberTenantLinkDto?> RevokeLinkAsync(
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken);
        if (link is null)
            return null;

        if (link.LinkStatus != PrescriberLinkStatuses.Active)
            throw new InvalidOperationException("Chỉ thu hồi liên kết đang active.");

        var ok = await _repo.RevokeLinkAsync(linkId, _tenant.UserId, cancellationToken);
        return ok ? await _repo.GetLinkForTenantAsync(_tenant.TenantId, linkId, cancellationToken) : null;
    }

    public Task<IReadOnlyList<PrescriberTenantLinkDto>> ListMyPharmaciesAsync(
        Guid prescriberId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default) =>
        _repo.ListLinksForPrescriberAsync(
            prescriberId,
            activeOnly ? PrescriberLinkStatuses.Active : null,
            cancellationToken);

    public Task<IReadOnlyList<PrescriberTenantLinkDto>> ListPendingInvitesAsync(
        Guid prescriberId,
        CancellationToken cancellationToken = default) =>
        _repo.ListLinksForPrescriberAsync(
            prescriberId,
            PrescriberLinkStatuses.PendingNtInvite,
            cancellationToken);

    public Task<IReadOnlyList<PharmacyDirectoryItemDto>> SearchPharmacyDirectoryAsync(
        string? query,
        CancellationToken cancellationToken = default) =>
        _repo.SearchDirectoryAsync(query, cancellationToken);

    public async Task<PrescriberTenantLinkDto> RequestPharmacyLinkAsync(
        Guid prescriberId,
        RequestPharmacyLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantCode = request.TenantCode.Trim();
        if (string.IsNullOrWhiteSpace(tenantCode))
            throw new InvalidOperationException("Mã nhà thuốc không được để trống.");

        var tenant = await _repo.ResolveTenantByCodeAsync(tenantCode, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy nhà thuốc.");

        var linkId = await _repo.UpsertPrescriberRequestLinkAsync(tenant.Id, prescriberId, cancellationToken);
        return (await _repo.GetLinkForPrescriberAsync(prescriberId, linkId, cancellationToken))!;
    }

    public async Task<PrescriberTenantLinkDto?> AcceptInviteAsync(
        Guid prescriberId,
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await _repo.GetLinkForPrescriberAsync(prescriberId, linkId, cancellationToken);
        if (link is null)
            return null;

        if (link.LinkStatus != PrescriberLinkStatuses.PendingNtInvite)
            throw new InvalidOperationException("Lời mời không còn hiệu lực.");

        var ok = await _repo.UpdateLinkStatusAsync(
            linkId,
            PrescriberLinkStatuses.PendingNtInvite,
            PrescriberLinkStatuses.Active,
            prescriberId,
            cancellationToken);
        if (!ok)
            return null;

        var activated = await _repo.GetLinkForPrescriberAsync(prescriberId, linkId, cancellationToken);
        if (activated is not null)
        {
            await _platformEvents.PublishForTenantAsync(
                activated.TenantId,
                PlatformEventTypes.HealthcarePrescriberLinkActive,
                PlatformEventAggregateTypes.PrescriberTenantLink,
                linkId,
                new
                {
                    linkId,
                    prescriberId = activated.PrescriberId,
                    tenantId = activated.TenantId,
                    initiatedBy = activated.InitiatedBy,
                },
                actorUserId: prescriberId,
                source: PlatformEventSources.HealthcareNetwork,
                cancellationToken: cancellationToken);
        }

        return activated;
    }

    public async Task<PrescriberTenantLinkDto?> DeclineInviteAsync(
        Guid prescriberId,
        Guid linkId,
        RejectPrescriberLinkRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var link = await _repo.GetLinkForPrescriberAsync(prescriberId, linkId, cancellationToken);
        if (link is null)
            return null;

        if (link.LinkStatus != PrescriberLinkStatuses.PendingNtInvite)
            throw new InvalidOperationException("Lời mời không còn hiệu lực.");

        var ok = await _repo.UpdateLinkStatusAsync(
            linkId,
            PrescriberLinkStatuses.PendingNtInvite,
            PrescriberLinkStatuses.Rejected,
            prescriberId,
            cancellationToken);
        return ok ? await _repo.GetLinkForPrescriberAsync(prescriberId, linkId, cancellationToken) : null;
    }
}
