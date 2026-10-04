using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Workflow;
using KitPlatform.Packs.Pharmacy.Procurement;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ProcurementRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly IAuditEngine _audit;
    private readonly IBranchAccessService _branchAccess;
    private readonly IWorkflowEngine _workflow;
    private readonly ICurrentUserAccessor _user;

    public PurchaseOrderService(
        ProcurementRepository repository,
        ITenantContext tenant,
        IAuditEngine audit,
        IBranchAccessService branchAccess,
        IWorkflowEngine workflow,
        ICurrentUserAccessor user)
    {
        _repository = repository;
        _tenant = tenant;
        _audit = audit;
        _branchAccess = branchAccess;
        _workflow = workflow;
        _user = user;
    }

    public async Task<ProcurementPagedListResult<PurchaseOrderListItemDto>> GetAllAsync(
        PurchaseOrderListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new PurchaseOrderListFilter();
        var (scopedWarehouseId, allowed) =
            await _branchAccess.ResolveWarehouseQueryAsync(filter.WarehouseId, cancellationToken);
        if (scopedWarehouseId is Guid warehouseId)
            filter = filter with { WarehouseId = warehouseId };
        var (items, total) = await _repository.GetPurchaseOrdersAsync(filter, allowed, cancellationToken);
        return new ProcurementPagedListResult<PurchaseOrderListItemDto>(
            items, total, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 100));
    }

    public async Task<PurchaseOrderDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var po = await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);
        if (po is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(po.WarehouseId, cancellationToken);
        return po;
    }

    public async Task<PurchaseOrderDetailDto> CreateAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng hàng.");

        if (!await _repository.SupplierExistsAsync(request.SupplierId, cancellationToken))
            throw new InvalidOperationException("NCC không tồn tại.");
        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);

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
        await _branchAccess.EnsureWarehouseAccessAsync(po.WarehouseId, cancellationToken);

        if (po.DeletedAt is not null)
            throw new InvalidOperationException("Không sửa được đơn đã ẩn.");

        if (po.Status is not (
            PurchaseOrderStatuses.Draft or
            PurchaseOrderStatuses.Approved or
            PurchaseOrderStatuses.PartiallyReceived))
            throw new InvalidOperationException("Không sửa được đơn ở trạng thái này.");

        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng hàng.");

        if (po.Status == PurchaseOrderStatuses.Draft &&
            request.SupplierId is Guid supplierId &&
            supplierId != po.SupplierId)
        {
            if (!await _repository.SupplierExistsAsync(supplierId, cancellationToken))
                throw new InvalidOperationException("NCC không tồn tại.");
        }

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

    public async Task<PurchaseOrderDetailDto?> ApproveAsync(
        Guid id,
        ApprovePurchaseOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!_user.IsInRole("ADMIN"))
            throw new InvalidOperationException("Chỉ quản trị viên mới duyệt trực tiếp PO. Hãy gửi duyệt qua workflow.");

        return await ApproveInternalAsync(id, request, cancellationToken);
    }

    public async Task<SubmitPurchaseOrderApprovalResult> SubmitForApprovalAsync(
        Guid id,
        ApprovePurchaseOrderRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var po = await RequirePoAccessAsync(id, cancellationToken);
        if (po.Status != PurchaseOrderStatuses.Draft)
            throw new InvalidOperationException("Chỉ gửi duyệt PO ở trạng thái Nháp.");

        await EnsureSupplierReadyForApprovalAsync(po, request?.SupplierId, cancellationToken);

        if (request?.SupplierId is Guid newSupplierId && newSupplierId != po.SupplierId)
            await _repository.SetPurchaseOrderSupplierAsync(id, newSupplierId, cancellationToken);

        var taskId = await _workflow.SubmitPurchaseOrderApprovalAsync(
            po.Id,
            po.PoNumber,
            po.SupplierName,
            po.TotalAmount,
            cancellationToken);

        await _audit.WriteAsync(
            "purchase_order",
            id,
            "submit_for_approval",
            new { poNumber = po.PoNumber, workflowTaskId = taskId },
            cancellationToken);

        return new SubmitPurchaseOrderApprovalResult(taskId);
    }

    public async Task<PoWorkflowDecisionDto> DecideApprovalWorkflowAsync(
        Guid taskId,
        bool approved,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (!_user.IsInRole("ADMIN"))
            throw new InvalidOperationException("Chỉ quản trị viên mới xử lý phê duyệt PO.");

        var purchaseOrderId = await _workflow.GetPurchaseOrderIdForPendingTaskAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow task không thuộc PO.");

        var decision = await _workflow.DecidePurchaseOrderTaskAsync(taskId, approved, notes, cancellationToken);

        if (approved)
        {
            await ApproveInternalAsync(purchaseOrderId, null, cancellationToken);
        }
        else
        {
            await _audit.WriteAsync(
                "purchase_order",
                purchaseOrderId,
                "approval_rejected",
                new { workflowTaskId = taskId, notes },
                cancellationToken);
        }

        return new PoWorkflowDecisionDto(
            decision.TaskId,
            decision.TaskStatus,
            decision.Decision,
            decision.CompletedAt,
            purchaseOrderId);
    }

    private async Task<PurchaseOrderDetailDto?> ApproveInternalAsync(
        Guid id,
        ApprovePurchaseOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var po = await RequirePoAccessAsync(id, cancellationToken);
        if (po.Status != PurchaseOrderStatuses.Draft)
            throw new InvalidOperationException("Chỉ duyệt được PO ở trạng thái Nháp.");

        await EnsureSupplierReadyForApprovalAsync(po, request?.SupplierId, cancellationToken);

        if (request?.SupplierId is Guid newSupplierId && newSupplierId != po.SupplierId)
            await _repository.SetPurchaseOrderSupplierAsync(id, newSupplierId, cancellationToken);

        var updated = await _repository.TransitionPurchaseOrderStatusAsync(
            id, PurchaseOrderStatuses.Draft, PurchaseOrderStatuses.Approved, _tenant.UserId, cancellationToken);
        if (!updated)
            throw new InvalidOperationException("Không duyệt được PO (chỉ duyệt từ Nháp).");

        await _audit.WriteAsync("purchase_order", id, "approve", cancellationToken: cancellationToken);
        return await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken);
    }

    private async Task EnsureSupplierReadyForApprovalAsync(
        PurchaseOrderDetailDto po,
        Guid? supplierOverrideId,
        CancellationToken cancellationToken)
    {
        if (await _repository.IsSupplierPlaceholderAsync(po.SupplierId, cancellationToken))
        {
            if (supplierOverrideId is not Guid newSupplierId)
                throw new InvalidOperationException("Chọn NCC thật trước khi duyệt PO.");
            if (!await _repository.SupplierExistsAsync(newSupplierId, cancellationToken))
                throw new InvalidOperationException("NCC không tồn tại.");
            if (await _repository.IsSupplierPlaceholderAsync(newSupplierId, cancellationToken))
                throw new InvalidOperationException("Không duyệt PO với NCC Chưa xác định.");
        }
    }

    public async Task<PurchaseOrderDetailDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var po = await RequirePoAccessAsync(id, cancellationToken);

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
        var po = await RequirePoAccessAsync(id, cancellationToken);

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
        var po = await RequirePoAccessAsync(id, cancellationToken);
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
        await _branchAccess.EnsureWarehouseAccessAsync(po.WarehouseId, cancellationToken);

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

    public async Task<LastPurchasePriceHintDto> GetLastPurchasePriceHintAsync(
        Guid supplierId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        return await _repository.GetLastPurchasePriceHintAsync(supplierId, productId, allowed, cancellationToken);
    }

    private async Task<PurchaseOrderDetailDto> RequirePoAccessAsync(Guid id, CancellationToken cancellationToken)
    {
        var po = await _repository.GetPurchaseOrderAsync(id, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("PO không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(po.WarehouseId, cancellationToken);
        return po;
    }
}
