using PharmaCore.Application.Abstractions;

using PharmaCore.Application.Procurement;



namespace PharmaCore.Infrastructure.Procurement;



internal sealed class SupplierPaymentService : ISupplierPaymentService

{

    private readonly ProcurementRepository _repository;

    private readonly ITenantContext _tenant;

    private readonly IAuditLogService _audit;



    public SupplierPaymentService(

        ProcurementRepository repository,

        ITenantContext tenant,

        IAuditLogService audit)

    {

        _repository = repository;

        _tenant = tenant;

        _audit = audit;

    }



    public Task<IReadOnlyList<SupplierPaymentListItemDto>> GetAllAsync(

        SupplierPaymentListFilter? filter = null,

        CancellationToken cancellationToken = default) =>

        _repository.GetSupplierPaymentsAsync(filter ?? new SupplierPaymentListFilter(), cancellationToken);



    public Task<SupplierPaymentListItemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>

        _repository.GetSupplierPaymentAsync(id, cancellationToken);



    public async Task<SupplierPaymentListItemDto> CreateAsync(

        CreateSupplierPaymentRequest request,

        CancellationToken cancellationToken = default)

    {

        await ValidatePaymentRequestAsync(
            request.SupplierId,
            request.PurchaseOrderId,
            request.GoodsReceiptId,
            request.Amount,
            cancellationToken);

        var id = await _repository.CreateSupplierPaymentAsync(request, _tenant.UserId, cancellationToken);

        return (await _repository.GetSupplierPaymentAsync(id, cancellationToken))!;

    }



    public async Task<SupplierPaymentListItemDto?> UpdateAsync(

        Guid id,

        UpdateSupplierPaymentRequest request,

        CancellationToken cancellationToken = default)

    {

        var existing = await _repository.GetSupplierPaymentAsync(id, cancellationToken);

        if (existing is null) return null;

        if (existing.Status != SupplierPaymentStatuses.Draft)

            throw new InvalidOperationException("Chỉ sửa được phiếu thanh toán ở trạng thái Nháp.");



        await ValidatePaymentRequestAsync(
            request.SupplierId,
            request.PurchaseOrderId,
            request.GoodsReceiptId,
            request.Amount,
            cancellationToken);

        var updated = await _repository.UpdateSupplierPaymentAsync(id, request, cancellationToken);

        if (!updated)

            throw new InvalidOperationException("Không cập nhật được phiếu thanh toán.");

        return await _repository.GetSupplierPaymentAsync(id, cancellationToken);

    }



    public async Task<SupplierPaymentListItemDto?> PostAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var existing = await _repository.GetSupplierPaymentAsync(id, cancellationToken);

        if (existing is null) return null;

        if (existing.Status != SupplierPaymentStatuses.Draft)

            throw new InvalidOperationException("Chỉ ghi sổ được phiếu thanh toán ở trạng thái Nháp.");



        var posted = await _repository.PostSupplierPaymentAsync(id, _tenant.UserId, cancellationToken);

        if (!posted)

            throw new InvalidOperationException("Không ghi sổ được phiếu thanh toán.");



        await _audit.WriteAsync("supplier_payment", id, "post", new { paymentNumber = existing.PaymentNumber }, cancellationToken);

        return await _repository.GetSupplierPaymentAsync(id, cancellationToken);

    }



    public async Task<SupplierPaymentListItemDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var existing = await _repository.GetSupplierPaymentAsync(id, cancellationToken);

        if (existing is null) return null;

        if (existing.Status != SupplierPaymentStatuses.Draft)

            throw new InvalidOperationException("Chỉ hủy được phiếu thanh toán ở trạng thái Nháp.");



        var cancelled = await _repository.CancelSupplierPaymentAsync(id, _tenant.UserId, cancellationToken);

        if (!cancelled)

            throw new InvalidOperationException("Không hủy được phiếu thanh toán.");



        await _audit.WriteAsync("supplier_payment", id, "cancel", new { paymentNumber = existing.PaymentNumber }, cancellationToken);

        return await _repository.GetSupplierPaymentAsync(id, cancellationToken);

    }



    private async Task ValidatePaymentRequestAsync(
        Guid supplierId,
        Guid? purchaseOrderId,
        Guid? goodsReceiptId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Số tiền phải lớn hơn 0.");

        if (!await _repository.SupplierExistsAsync(supplierId, cancellationToken))
            throw new InvalidOperationException("NCC không tồn tại.");

        if (purchaseOrderId is Guid poId)
        {
            var po = await _repository.GetPurchaseOrderPaymentLinkAsync(poId, cancellationToken);
            if (po is null)
                throw new InvalidOperationException("Đơn đặt hàng không tồn tại.");
            if (po.SupplierId != supplierId)
                throw new InvalidOperationException("Đơn đặt hàng không thuộc NCC đã chọn.");
            if (po.Status is PurchaseOrderStatuses.Draft or PurchaseOrderStatuses.Cancelled)
                throw new InvalidOperationException("Đơn đặt hàng không hợp lệ để liên kết thanh toán.");
        }

        if (goodsReceiptId is Guid grnId)
        {
            var grn = await _repository.GetGoodsReceiptPaymentLinkAsync(grnId, cancellationToken);
            if (grn is null)
                throw new InvalidOperationException("Phiếu nhập không tồn tại.");
            if (grn.SupplierId != supplierId)
                throw new InvalidOperationException("Phiếu nhập không thuộc NCC đã chọn.");
            if (grn.Status != GoodsReceiptStatuses.Completed)
                throw new InvalidOperationException("Chỉ liên kết phiếu nhập đã hoàn tất.");
            if (purchaseOrderId is Guid linkedPoId
                && grn.PurchaseOrderId is Guid grnPoId
                && grnPoId != linkedPoId)
                throw new InvalidOperationException("Phiếu nhập không khớp đơn đặt hàng.");
        }
    }
}

