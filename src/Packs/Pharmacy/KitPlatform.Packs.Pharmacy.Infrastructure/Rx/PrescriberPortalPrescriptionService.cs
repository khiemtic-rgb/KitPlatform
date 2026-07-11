using KitPlatform.Application.Configuration;
using KitPlatform.Packs.Pharmacy.Rx;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class PrescriberPortalPrescriptionService : IPrescriberPortalPrescriptionService
{
    private readonly PrescriberPortalPrescriptionRepository _repo;
    private readonly PrescriberPortalRepository _portal;
    private readonly PlatformSettings _platform;
    private readonly IRxNotifyService _notify;

    public PrescriberPortalPrescriptionService(
        PrescriberPortalPrescriptionRepository repo,
        PrescriberPortalRepository portal,
        IOptions<PlatformSettings> platform,
        IRxNotifyService notify)
    {
        _repo = repo;
        _portal = portal;
        _platform = platform.Value;
        _notify = notify;
    }

    public async Task<IReadOnlyList<PortalCustomerSearchItemDto>> SearchCustomersAsync(
        Guid prescriberId,
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveLinkAsync(prescriberId, tenantId, cancellationToken);
        return await _repo.SearchCustomersAsync(tenantId, query, cancellationToken);
    }

    public async Task<IReadOnlyList<PortalProductSearchItemDto>> SearchProductsAsync(
        Guid prescriberId,
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveLinkAsync(prescriberId, tenantId, cancellationToken);
        return await _repo.SearchProductsAsync(tenantId, query, cancellationToken);
    }

    public async Task<PortalPrescriptionDetailDto> CreateSignedPrescriptionAsync(
        Guid prescriberId,
        PortalCreatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var link = await EnsureActiveLinkAsync(prescriberId, request.TenantId, cancellationToken);

        var customer = await ValidateCustomerAsync(request.TenantId, request.CustomerId, cancellationToken);

        var createRequest = request with
        {
            PatientName = request.PatientName?.Trim() ?? customer.FullName,
            PatientPhone = request.PatientPhone?.Trim() ?? customer.Phone,
        };

        var prescriptionId = await _repo.CreateSignedPrescriptionAsync(
            request.TenantId,
            prescriberId,
            link.LinkedPrescriberId,
            createRequest,
            cancellationToken);

        var detail = WithPosDeepLink(
            (await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken))!);

        var profile = await _portal.FindPrescriberByIdAsync(prescriberId, cancellationToken);
        await _notify.NotifyPharmacyNewPrescriptionAsync(
            detail.TenantCode,
            detail.TenantName,
            detail.PrescriptionCode,
            detail.Id,
            detail.PatientName,
            profile?.FullName,
            cancellationToken);

        return detail;
    }

    public Task<IReadOnlyList<PortalPrescriptionSummaryDto>> ListMyPrescriptionsAsync(
        Guid prescriberId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default) =>
        _repo.ListForPrescriberAsync(prescriberId, tenantId, cancellationToken);

    public async Task<PortalPrescriptionDetailDto?> GetPrescriptionAsync(
        Guid prescriberId,
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var item = await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken);
        return item is null ? null : WithPosDeepLink(item);
    }

    public async Task<PortalPrescriptionDetailDto> AmendSignedPrescriptionAsync(
        Guid prescriberId,
        Guid prescriptionId,
        PortalAmendPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy đơn thuốc.");

        await EnsureActiveLinkAsync(prescriberId, existing.TenantId, cancellationToken);
        await _repo.AmendSignedPrescriptionAsync(prescriberId, prescriptionId, request, cancellationToken);

        return WithPosDeepLink(
            (await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken))!);
    }

    public async Task<PortalPrescriptionDetailDto?> CancelPrescriptionAsync(
        Guid prescriberId,
        Guid prescriptionId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken);
        if (existing is null)
            return null;

        await _repo.CancelForPrescriberAsync(prescriberId, prescriptionId, reason, cancellationToken);
        return WithPosDeepLink(
            (await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken))!);
    }

    public Task<PortalPrescriberDashboardDto> GetDashboardAsync(
        Guid prescriberId,
        CancellationToken cancellationToken = default) =>
        _repo.GetDashboardAsync(prescriberId, cancellationToken);

    public async Task<PortalPrescriptionShareDto?> GetPrescriptionShareAsync(
        Guid prescriberId,
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var item = await _repo.GetForPrescriberAsync(prescriberId, prescriptionId, cancellationToken);
        if (item is null)
            return null;

        return new PortalPrescriptionShareDto(
            item.Id,
            item.PrescriptionCode,
            BuildPosDeepLink(item.Id));
    }

    private PortalPrescriptionDetailDto WithPosDeepLink(PortalPrescriptionDetailDto item) =>
        item with { PosDeepLink = BuildPosDeepLink(item.Id) };

    private string BuildPosDeepLink(Guid prescriptionId)
    {
        var adminUrl = _platform.AdminUrl?.TrimEnd('/') ?? "https://admin.novixa.vn";
        return $"{adminUrl}/rx/prescriptions?rx={prescriptionId:D}";
    }

    private async Task<PrescriberPortalPrescriptionRepository.ActivePrescriberLinkRow> EnsureActiveLinkAsync(
        Guid prescriberId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var profile = await _portal.FindPrescriberByIdAsync(prescriberId, cancellationToken)
            ?? throw new InvalidOperationException("Bác sĩ không tồn tại.");

        if (string.IsNullOrWhiteSpace(profile.LicenseNumber))
            throw new InvalidOperationException(
                "Tài khoản bác sĩ chưa có số CCHN. Liên hệ nhà thuốc để bổ sung chứng chỉ hành nghề trước khi kê đơn.");

        if (profile.Status != "active")
            throw new InvalidOperationException("Tài khoản bác sĩ chưa được xác minh hoặc đã bị tạm khóa.");

        var link = await _repo.GetActiveLinkAsync(prescriberId, tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Chưa liên kết active với nhà thuốc này.");

        return link;
    }

    private async Task<PortalCustomerSearchItemDto> ValidateCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var match = await _repo.GetCustomerAsync(tenantId, customerId, cancellationToken);
        return match ?? throw new InvalidOperationException("Khách hàng không thuộc nhà thuốc đích.");
    }
}
