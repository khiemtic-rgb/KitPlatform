using PharmaCore.Application.Abstractions;

using PharmaCore.Application.Procurement;



namespace PharmaCore.Infrastructure.Procurement;



internal sealed class PurchaseOrderService : IPurchaseOrderService

{

    private readonly ProcurementRepository _repository;

    private readonly ITenantContext _tenant;

    private readonly IAuditLogService _audit;



    public PurchaseOrderService(

        ProcurementRepository repository,

        ITenantContext tenant,

        IAuditLogService audit)

    {

        _repository = repository;

        _tenant = tenant;

        _audit = audit;

    }



    public Task<IReadOnlyList<PurchaseOrderListItemDto>> GetAllAsync(

        PurchaseOrderListFilter? filter = null,

        CancellationToken cancellationToken = default) =>

        _repository.GetPurchaseOrdersAsync(filter ?? new PurchaseOrderListFilter(), cancellationToken);



    public Task<PurchaseOrderDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>

        _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);



    public async Task<PurchaseOrderDetailDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)

    {

        if (request.Items.Count == 0)

            throw new InvalidOperationException("Thêm ít nhất một dòng hàng.");



        if (!await _repository.SupplierExistsAsync(request.SupplierId, cancellationToken))

            throw new InvalidOperationException("NCC không tồn tại.");

        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))

            throw new InvalidOperationException("Kho không tồn tại.");



        foreach (var item in request.Items)

        {

            if (item.OrderedQty <= 0)

                throw new InvalidOperationException("Số lượng đặt phải lớn hơn 0.");

            if (item.UnitPrice < 0)

                throw new InvalidOperationException("Đơn giá không hợp lệ.");

            if (!await _repository.ProductExistsAsync(item.ProductId, cancellationToken))

                throw new InvalidOperationException($"Sản phẩm không tồn tại: {item.ProductId}");

        }



        var id = await _repository.CreatePurchaseOrderAsync(request, _tenant.UserId, cancellationToken);

        return (await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken))!;

    }



    public async Task<PurchaseOrderDetailDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var po = await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("PO không tồn tại.");

        if (po.DeletedAt is not null)
            throw new InvalidOperationException("Không sửa được đơn đã ẩn.");

        if (po.Status is not (
            PurchaseOrderStatuses.Draft or
            PurchaseOrderStatuses.Approved or
            PurchaseOrderStatuses.PartiallyReceived))
            throw new InvalidOperationException("Không sửa được đơn ở trạng thái này.");

        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng hàng.");

        foreach (var item in request.Items)
        {
            if (item.OrderedQty <= 0)
                throw new InvalidOperationException("Số lượng đặt phải lớn hơn 0.");
            if (item.UnitPrice < 0)
                throw new InvalidOperationException("Đơn giá không hợp lệ.");
            if (!await _repository.ProductExistsAsync(item.ProductId, cancellationToken))
                throw new InvalidOperationException($"Sản phẩm không tồn tại: {item.ProductId}");
        }

        var updated = await _repository.UpdatePurchaseOrderAsync(id, request, cancellationToken);
        if (!updated)
            throw new InvalidOperationException("Không cập nhật được đơn đặt hàng.");

        await _audit.WriteAsync(
            "purchase_order",
            id,
            "update",
            new { poNumber = po.PoNumber },
            cancellationToken);

        return await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);
    }



    public async Task<PurchaseOrderDetailDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var updated = await _repository.TransitionPurchaseOrderStatusAsync(

            id, PurchaseOrderStatuses.Draft, PurchaseOrderStatuses.Approved, _tenant.UserId, cancellationToken);

        if (!updated)

            throw new InvalidOperationException("Không duyệt được PO (chỉ duyệt từ Nháp).");



        await _audit.WriteAsync("purchase_order", id, "approve", cancellationToken: cancellationToken);

        return await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);

    }



    public async Task<PurchaseOrderDetailDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var po = await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken)

            ?? throw new InvalidOperationException("PO không tồn tại.");



        if (po.Status is PurchaseOrderStatuses.Received or PurchaseOrderStatuses.PartiallyReceived)

            throw new InvalidOperationException("Không hủy PO đã nhận hàng.");



        if (po.Status == PurchaseOrderStatuses.Cancelled)

            throw new InvalidOperationException("PO đã hủy.");



        var updated = await _repository.TransitionPurchaseOrderStatusAsync(

            id, po.Status, PurchaseOrderStatuses.Cancelled, _tenant.UserId, cancellationToken);

        if (!updated)

            throw new InvalidOperationException("Không hủy được PO.");



        await _audit.WriteAsync("purchase_order", id, "cancel", new { poNumber = po.PoNumber }, cancellationToken);

        return await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);

    }



    public async Task<PurchaseOrderDetailDto?> CloseAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var po = await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken)

            ?? throw new InvalidOperationException("Đơn đặt hàng không tồn tại.");



        if (po.Status != PurchaseOrderStatuses.Received)

            throw new InvalidOperationException("Chỉ đóng đơn đã nhận đủ hàng.");



        var updated = await _repository.TransitionPurchaseOrderStatusAsync(

            id, PurchaseOrderStatuses.Received, PurchaseOrderStatuses.Closed, _tenant.UserId, cancellationToken);

        if (!updated)

            throw new InvalidOperationException("Không đóng được đơn đặt hàng.");



        await _audit.WriteAsync("purchase_order", id, "close", new { poNumber = po.PoNumber }, cancellationToken);

        return await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);

    }



    public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var po = await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);

        if (po is null) return false;

        if (po.Status != PurchaseOrderStatuses.Cancelled)

            throw new InvalidOperationException("Chỉ ẩn được đơn đặt hàng đã hủy.");

        if (po.DeletedAt is not null)

            throw new InvalidOperationException("Đơn đặt hàng đã được ẩn.");



        var archived = await _repository.SoftDeletePurchaseOrderAsync(id, _tenant.UserId, cancellationToken);

        if (archived)

        {

            await _audit.WriteAsync(

                "purchase_order",

                id,

                "soft_delete",

                new { poNumber = po.PoNumber, status = po.Status },

                cancellationToken);

        }



        return archived;

    }



    public async Task<bool> PurgeAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var po = await _repository.GetPurchaseOrderAsync(id, includeArchived: true, cancellationToken);

        if (po is null || po.DeletedAt is null) return false;



        var purged = await _repository.PurgePurchaseOrderAsync(id, cancellationToken);

        if (purged)

        {

            await _audit.WriteAsync(

                "purchase_order",

                id,

                "purge",

                new { poNumber = po.PoNumber, status = po.Status, deletedAt = po.DeletedAt },

                cancellationToken);

        }



        return purged;

    }



    public Task<LastPurchasePriceHintDto> GetLastPurchasePriceHintAsync(

        Guid supplierId,

        Guid productId,

        CancellationToken cancellationToken = default) =>

        _repository.GetLastPurchasePriceHintAsync(supplierId, productId, cancellationToken);

}

