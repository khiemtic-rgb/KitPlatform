using KitPlatform.Application.Customers;

namespace KitPlatform.Infrastructure.Customers;

internal sealed class CustomerAdminService : ICustomerAdminService
{
    private readonly CustomerAdminRepository _repository;
    private readonly CustomerGroupRepository _groups;

    public CustomerAdminService(CustomerAdminRepository repository, CustomerGroupRepository groups)
    {
        _repository = repository;
        _groups = groups;
    }

    public async Task<PagedCustomersResult> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default,
        string? pharmacyRelation = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repository.ListAsync(
            search,
            page,
            pageSize,
            pharmacyRelation,
            cancellationToken);
        return new PagedCustomersResult(items, total, page, pageSize);
    }

    public Task<CustomerPharmacyRelationSummaryDto> GetPharmacyRelationSummaryAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetPharmacyRelationSummaryAsync(cancellationToken);

    public Task<SimilarCustomerClustersResult> GetSimilarClustersAsync(
        double similarityThreshold = 0.8,
        CancellationToken cancellationToken = default) =>
        _repository.GetSimilarClustersAsync(similarityThreshold, cancellationToken);

    public Task<SimilarCustomerNamesResult> FindSimilarNamesAsync(
        string fullName,
        Guid? excludeCustomerId = null,
        double similarityThreshold = 0.8,
        CancellationToken cancellationToken = default) =>
        _repository.FindSimilarNamesAsync(fullName, excludeCustomerId, similarityThreshold, cancellationToken);

    public Task<CustomerDetailDto?> GetAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        _repository.GetAsync(customerId, cancellationToken);

    public async Task<PagedCustomerOrdersResult> GetOrdersAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var detail = await _repository.GetAsync(customerId, cancellationToken);
        if (detail is null)
            throw new InvalidOperationException("Khách hàng không tồn tại.");

        var (items, total) = await _repository.GetOrdersAsync(customerId, page, pageSize, cancellationToken);
        return new PagedCustomerOrdersResult(items, total, page, pageSize);
    }

    public async Task<CustomerDetailDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateNameAndPhone(request.FullName, request.Phone);

        var phone = CustomerPhoneNormalizer.Normalize(request.Phone);
        if (!CustomerPhoneNormalizer.IsValid(phone))
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");
        if (await _repository.PhoneExistsAsync(phone, excludeCustomerId: null, cancellationToken))
            throw new InvalidOperationException("Số điện thoại đã được dùng cho khách hàng khác.");

        var code = string.IsNullOrWhiteSpace(request.CustomerCode)
            ? await _repository.GenerateCustomerCodeAsync(cancellationToken)
            : NormalizeCustomerCode(request.CustomerCode);

        if (await _repository.CustomerCodeExistsAsync(code, excludeCustomerId: null, cancellationToken))
            throw new InvalidOperationException($"Mã khách hàng «{code}» đã tồn tại — chọn mã khác hoặc để trống để hệ thống tự sinh.");

        var groupId = await ResolveGroupIdAsync(request.CustomerGroupId, cancellationToken);
        var acquisition = ResolveAcquisitionSource(request.AcquisitionSource);
        var isAppSelf = acquisition == CustomerAcquisitionSources.AppSelf;
        var relation = isAppSelf ? CustomerPharmacyRelations.Prospect : CustomerPharmacyRelations.Member;
        var verifiedVia = isAppSelf ? null : CustomerPharmacyVerifiedVia.StaffMark;

        var id = await _repository.CreateAsync(
            code,
            request.FullName.Trim(),
            phone,
            NormalizeEmail(request.Email),
            request.DateOfBirth,
            request.Gender,
            NormalizeOptional(request.AddressLine),
            NormalizeOptional(request.IdNumber),
            NormalizeOptional(request.EmergencyContactName),
            NormalizeOptional(request.EmergencyContactPhone),
            NormalizeOptional(request.ClinicalNotes),
            groupId,
            acquisition,
            relation,
            verifiedVia,
            pharmacyVerifiedBy: null,
            cancellationToken);

        return (await _repository.GetAsync(id, cancellationToken))!;
    }

    public async Task<CustomerDetailDto?> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateNameAndPhone(request.FullName, request.Phone);

        if (await _repository.GetAsync(customerId, cancellationToken) is null)
            return null;

        var phone = CustomerPhoneNormalizer.Normalize(request.Phone);
        if (!CustomerPhoneNormalizer.IsValid(phone))
            throw new InvalidOperationException("Số điện thoại không hợp lệ.");
        if (await _repository.PhoneExistsAsync(phone, customerId, cancellationToken))
            throw new InvalidOperationException("Số điện thoại đã được dùng cho khách hàng khác.");

        var code = NormalizeCustomerCode(request.CustomerCode);
        if (await _repository.CustomerCodeExistsAsync(code, customerId, cancellationToken))
            throw new InvalidOperationException($"Mã khách hàng «{code}» đã tồn tại.");

        var groupId = await ResolveGroupIdAsync(request.CustomerGroupId, cancellationToken);

        var updated = await _repository.UpdateAsync(
            customerId,
            code,
            request.FullName.Trim(),
            phone,
            NormalizeEmail(request.Email),
            request.DateOfBirth,
            request.Gender,
            request.Status,
            request.AllowCredit,
            request.CreditLimit,
            NormalizeOptional(request.AddressLine),
            NormalizeOptional(request.IdNumber),
            NormalizeOptional(request.EmergencyContactName),
            NormalizeOptional(request.EmergencyContactPhone),
            NormalizeOptional(request.ClinicalNotes),
            groupId,
            cancellationToken);

        return updated ? await _repository.GetAsync(customerId, cancellationToken) : null;
    }

    public async Task<CustomerDetailDto?> MarkPharmacyMemberAsync(
        Guid customerId,
        string? verifiedVia,
        Guid? verifiedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetAsync(customerId, cancellationToken) is null)
            return null;

        var via = string.IsNullOrWhiteSpace(verifiedVia)
            ? CustomerPharmacyVerifiedVia.StaffMark
            : verifiedVia.Trim().ToLowerInvariant();
        if (via is not (
            CustomerPharmacyVerifiedVia.StaffMark
            or CustomerPharmacyVerifiedVia.QrScan
            or CustomerPharmacyVerifiedVia.FirstSale
            or CustomerPharmacyVerifiedVia.Invite))
        {
            throw new InvalidOperationException("Hình thức xác nhận khách nhà thuốc không hợp lệ.");
        }

        var ok = await _repository.MarkPharmacyMemberAsync(customerId, via, verifiedByUserId, cancellationToken);
        return ok ? await _repository.GetAsync(customerId, cancellationToken) : null;
    }

    public Task<string> GetNextCustomerCodeAsync(CancellationToken cancellationToken = default) =>
        _repository.GenerateCustomerCodeAsync(cancellationToken);

    private async Task<Guid?> ResolveGroupIdAsync(Guid? groupId, CancellationToken cancellationToken)
    {
        if (groupId is null)
            return null;
        if (!await _groups.ExistsActiveAsync(groupId.Value, cancellationToken))
            throw new InvalidOperationException("Nhóm khách không tồn tại hoặc đã ngưng.");
        return groupId;
    }

    private static void ValidateNameAndPhone(string fullName, string phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("Họ tên không được để trống.");
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Số điện thoại không được để trống.");
    }

    private static string ResolveAcquisitionSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return CustomerAcquisitionSources.Counter;
        var value = source.Trim().ToLowerInvariant();
        if (!CustomerAcquisitionSources.IsKnown(value))
            throw new InvalidOperationException("Nguồn tạo khách không hợp lệ.");
        return value;
    }

    private static string NormalizeCustomerCode(string customerCode) =>
        customerCode.Trim().ToUpperInvariant();

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
