using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class CustomerPaymentService : ICustomerPaymentService
{
    private readonly SalesRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly IAuditEngine _audit;
    private readonly IBranchAccessService _branchAccess;

    public CustomerPaymentService(
        SalesRepository repository,
        ITenantContext tenant,
        IAuditEngine audit,
        IBranchAccessService branchAccess)
    {
        _repository = repository;
        _tenant = tenant;
        _audit = audit;
        _branchAccess = branchAccess;
    }

    public async Task<IReadOnlyList<CustomerPaymentListItemDto>> GetAllAsync(
        CustomerPaymentListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new CustomerPaymentListFilter();
        var (scopedId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(filter.WarehouseId, cancellationToken);
        var effective = filter with { WarehouseId = scopedId ?? filter.WarehouseId };
        return await _repository.GetCustomerPaymentsAsync(effective, allowed, cancellationToken);
    }

    public async Task<CustomerPaymentListItemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _repository.GetCustomerPaymentAsync(id, cancellationToken);
        if (payment is null) return null;
        await EnsurePaymentAccessAsync(payment.SalesOrderId, cancellationToken);
        return payment;
    }

    public async Task<CustomerPaymentListItemDto> CreateAsync(
        CreateCustomerPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidatePaymentRequestAsync(
            request.CustomerId,
            request.SalesOrderId,
            request.Amount,
            cancellationToken);

        var id = await _repository.CreateCustomerPaymentAsync(request, _tenant.UserId, cancellationToken);
        return (await _repository.GetCustomerPaymentAsync(id, cancellationToken))!;
    }

    public async Task<CustomerPaymentListItemDto?> UpdateAsync(
        Guid id,
        UpdateCustomerPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCustomerPaymentAsync(id, cancellationToken);
        if (existing is null) return null;
        if (existing.Status != CustomerPaymentStatuses.Draft)
            throw new InvalidOperationException("Chỉ sửa được phiếu thu nợ ở trạng thái chờ ghi sổ.");

        await EnsurePaymentAccessAsync(existing.SalesOrderId, cancellationToken);
        await ValidatePaymentRequestAsync(
            request.CustomerId,
            request.SalesOrderId,
            request.Amount,
            cancellationToken);

        var updated = await _repository.UpdateCustomerPaymentAsync(id, request, cancellationToken);
        if (!updated)
            throw new InvalidOperationException("Không cập nhật được phiếu thu nợ.");

        return await _repository.GetCustomerPaymentAsync(id, cancellationToken);
    }

    public async Task<CustomerPaymentListItemDto?> PostAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCustomerPaymentAsync(id, cancellationToken);
        if (existing is null) return null;
        if (existing.Status != CustomerPaymentStatuses.Draft)
            throw new InvalidOperationException("Chỉ ghi sổ được phiếu thu nợ ở trạng thái chờ ghi sổ.");

        await EnsurePaymentAccessAsync(existing.SalesOrderId, cancellationToken);

        var posted = await _repository.PostCustomerPaymentAsync(id, _tenant.UserId, cancellationToken);
        if (!posted)
            throw new InvalidOperationException("Không ghi sổ được phiếu thu nợ.");

        await _audit.WriteAsync("customer_payment", id, "post", new { paymentNumber = existing.PaymentNumber }, cancellationToken);
        return await _repository.GetCustomerPaymentAsync(id, cancellationToken);
    }

    public async Task<CustomerPaymentListItemDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetCustomerPaymentAsync(id, cancellationToken);
        if (existing is null) return null;
        if (existing.Status != CustomerPaymentStatuses.Draft)
            throw new InvalidOperationException("Chỉ hủy được phiếu thu nợ ở trạng thái chờ ghi sổ.");

        await EnsurePaymentAccessAsync(existing.SalesOrderId, cancellationToken);

        var cancelled = await _repository.CancelCustomerPaymentAsync(id, _tenant.UserId, cancellationToken);
        if (!cancelled)
            throw new InvalidOperationException("Không hủy được phiếu thu nợ.");

        await _audit.WriteAsync("customer_payment", id, "cancel", new { paymentNumber = existing.PaymentNumber }, cancellationToken);
        return await _repository.GetCustomerPaymentAsync(id, cancellationToken);
    }

    private async Task ValidatePaymentRequestAsync(
        Guid customerId,
        Guid? salesOrderId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Số tiền phải lớn hơn 0.");

        if (!await _repository.CustomerExistsAsync(customerId, cancellationToken))
            throw new InvalidOperationException("Khách hàng không tồn tại.");

        await EnsurePaymentAccessAsync(salesOrderId, cancellationToken);

        if (salesOrderId is Guid orderId)
        {
            var order = await _repository.GetSalesOrderPaymentLinkAsync(orderId, cancellationToken);
            if (order is null)
                throw new InvalidOperationException("Đơn bán không tồn tại.");
            if (order.CustomerId != customerId)
                throw new InvalidOperationException("Đơn bán không thuộc khách hàng đã chọn.");
            if (order.Status != SalesOrderStatuses.Completed)
                throw new InvalidOperationException("Chỉ liên kết đơn bán đã hoàn tất.");
            if (order.Outstanding <= 0.009m)
                throw new InvalidOperationException("Đơn bán không còn nợ.");
            if (amount > order.Outstanding + 0.009m)
                throw new InvalidOperationException("Số tiền thu vượt còn nợ của đơn bán.");
        }
    }

    private async Task EnsurePaymentAccessAsync(Guid? salesOrderId, CancellationToken cancellationToken)
    {
        if (salesOrderId is Guid orderId)
        {
            var order = await _repository.GetSalesOrderPaymentLinkAsync(orderId, cancellationToken)
                ?? throw new InvalidOperationException("Đơn bán không tồn tại.");
            await _branchAccess.EnsureWarehouseAccessAsync(order.WarehouseId, cancellationToken);
            return;
        }

        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        if (!scope.Unrestricted)
            throw new UnauthorizedAccessException("Phiếu thu nợ phải liên kết đơn bán thuộc chi nhánh được phép.");
    }
}
