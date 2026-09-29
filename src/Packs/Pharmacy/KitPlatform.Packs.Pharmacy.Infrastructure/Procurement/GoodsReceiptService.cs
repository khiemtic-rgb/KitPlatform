using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Procurement;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly ProcurementRepository _repository;
    private readonly IInventoryService _inventory;
    private readonly ITenantContext _tenant;
    private readonly IAuditEngine _audit;
    private readonly IBranchAccessService _branchAccess;
    private readonly ICsdlDuocStockInSyncService _csdlStockIn;
    private readonly ILogger<GoodsReceiptService> _logger;

    public GoodsReceiptService(
        ProcurementRepository repository,
        IInventoryService inventory,
        ITenantContext tenant,
        IAuditEngine audit,
        IBranchAccessService branchAccess,
        ICsdlDuocStockInSyncService csdlStockIn,
        ILogger<GoodsReceiptService> logger)
    {
        _repository = repository;
        _inventory = inventory;
        _tenant = tenant;
        _audit = audit;
        _branchAccess = branchAccess;
        _csdlStockIn = csdlStockIn;
        _logger = logger;
    }

    public async Task<ProcurementPagedListResult<GoodsReceiptListItemDto>> GetAllAsync(
        GoodsReceiptListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new GoodsReceiptListFilter();
        var (scopedWarehouseId, allowed) =
            await _branchAccess.ResolveWarehouseQueryAsync(filter.WarehouseId, cancellationToken);
        if (scopedWarehouseId is Guid warehouseId)
            filter = filter with { WarehouseId = warehouseId };
        var (items, total) = await _repository.GetGoodsReceiptsAsync(filter, allowed, cancellationToken);
        return new ProcurementPagedListResult<GoodsReceiptListItemDto>(
            items, total, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 100));
    }

    public async Task<GoodsReceiptDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grn = await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);
        if (grn is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(grn.WarehouseId, cancellationToken);
        return grn;
    }

    public async Task<GoodsReceiptDetailDto> CreateAsync(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng nhập.");

        if (!await _repository.SupplierExistsAsync(request.SupplierId, cancellationToken))
            throw new InvalidOperationException("NCC không tồn tại.");
        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.BatchNumber))
                throw new InvalidOperationException("Số lô không được để trống.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException("Số lượng nhập phải lớn hơn 0.");
            if (item.UnitCost < 0)
                throw new InvalidOperationException("Giá vốn không hợp lệ.");
            await EnsureProductReceivableAsync(item.ProductId, item.PurchaseOrderItemId, cancellationToken);
        }

        await EnsureIncomingLotsValidAsync(request.Items, cancellationToken);

        var id = await _repository.CreateGoodsReceiptAsync(request, _tenant.UserId, cancellationToken);
        return (await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken))!;
    }

    public async Task<GoodsReceiptDetailDto?> UpdateAsync(
        Guid id,
        UpdateGoodsReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await RequireGrnAccessAsync(id, cancellationToken);
        if (existing.Status != GoodsReceiptStatuses.Draft)
            throw new InvalidOperationException("Chỉ sửa được phiếu ở trạng thái chờ nhập kho.");

        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng nhập.");

        if (!await _repository.SupplierExistsAsync(request.SupplierId, cancellationToken))
            throw new InvalidOperationException("NCC không tồn tại.");
        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(
            existing.PurchaseOrderId is not null ? existing.WarehouseId : request.WarehouseId,
            cancellationToken);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.BatchNumber))
                throw new InvalidOperationException("Số lô không được để trống.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException("Số lượng nhập phải lớn hơn 0.");
            if (item.UnitCost < 0)
                throw new InvalidOperationException("Giá vốn không hợp lệ.");
            await EnsureProductReceivableAsync(item.ProductId, item.PurchaseOrderItemId, cancellationToken);
        }

        await EnsureIncomingLotsValidAsync(request.Items, cancellationToken);

        var updated = await _repository.UpdateDraftGoodsReceiptAsync(id, request, _tenant.UserId, cancellationToken);
        if (!updated) return null;
        await _audit.WriteAsync("goods_receipt", id, "update", new { grnNumber = existing.GrnNumber }, cancellationToken);
        return await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);
    }

    public async Task<GoodsReceiptDetailDto?> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grn = await RequireGrnAccessAsync(id, cancellationToken);
        await EnsureIncomingLotsValidAsync(
            grn.Items.Select(item => new CreateGoodsReceiptItemRequest(
                item.PurchaseOrderItemId,
                item.ProductId,
                item.ProductUnitId,
                item.BatchNumber,
                item.ManufactureDate,
                item.ExpiryDate,
                item.Quantity,
                item.UnitCost)),
            cancellationToken);
        await _repository.CompleteGoodsReceiptAsync(id, _tenant.UserId, cancellationToken);
        await _audit.WriteAsync("goods_receipt", id, "complete", cancellationToken: cancellationToken);
        var completed = await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);
        if (completed is not null)
        {
            try
            {
                await _csdlStockIn.SyncGoodsReceiptAsync(
                    _tenant.TenantId, completed.Id, completed.GrnNumber, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSDL stock-in sync threw for GRN {GrnId}", completed.Id);
            }
        }

        return completed;
    }

    public async Task<GoodsReceiptDetailDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grn = await RequireGrnAccessAsync(id, cancellationToken);
        await _repository.CancelGoodsReceiptAsync(id, _tenant.UserId, cancellationToken);
        await _audit.WriteAsync("goods_receipt", id, "cancel", new { grnNumber = grn.GrnNumber }, cancellationToken);
        return await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);
    }

    public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grn = await RequireGrnAccessAsync(id, cancellationToken);
        if (grn.Status != GoodsReceiptStatuses.Cancelled)
            throw new InvalidOperationException("Chỉ ẩn được phiếu nhập đã hủy.");
        if (grn.DeletedAt is not null)
            throw new InvalidOperationException("Phiếu nhập đã được ẩn.");

        var archived = await _repository.SoftDeleteGoodsReceiptAsync(id, _tenant.UserId, cancellationToken);
        if (archived)
        {
            await _audit.WriteAsync(
                "goods_receipt",
                id,
                "soft_delete",
                new { grnNumber = grn.GrnNumber, status = grn.Status },
                cancellationToken);
        }

        return archived;
    }

    public async Task<bool> PurgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grn = await _repository.GetGoodsReceiptAsync(id, includeArchived: true, cancellationToken);
        if (grn is null || grn.DeletedAt is null) return false;
        await _branchAccess.EnsureWarehouseAccessAsync(grn.WarehouseId, cancellationToken);

        var purged = await _repository.PurgeGoodsReceiptAsync(id, cancellationToken);
        if (purged)
        {
            await _audit.WriteAsync(
                "goods_receipt",
                id,
                "purge",
                new { grnNumber = grn.GrnNumber, status = grn.Status, deletedAt = grn.DeletedAt },
                cancellationToken);
        }

        return purged;
    }

    private async Task EnsureProductReceivableAsync(
        Guid productId,
        Guid? purchaseOrderItemId,
        CancellationToken cancellationToken)
    {
        if (await _repository.ProductExistsAsync(productId, includeHidden: true, cancellationToken))
            return;

        throw new InvalidOperationException(
            purchaseOrderItemId is not null
                ? "Sản phẩm trên đơn đặt hàng không còn trong hệ thống. Sửa PO hoặc khôi phục danh mục rồi nhập lại."
                : "Sản phẩm không tồn tại trong danh mục.");
    }

    private async Task EnsureIncomingLotsValidAsync(
        IEnumerable<CreateGoodsReceiptItemRequest> items,
        CancellationToken cancellationToken)
    {
        var lines = items
            .Select(item => (item.ProductId, item.BatchNumber, item.ManufactureDate, (DateOnly?)item.ExpiryDate))
            .ToList();
        InventoryLotRules.EnsureDocumentLotsConsistent(lines);
        foreach (var item in items)
        {
            var identity = await _inventory.FindLotIdentityAsync(item.ProductId, item.BatchNumber, cancellationToken);
            InventoryLotRules.Resolve(item.BatchNumber, item.ManufactureDate, item.ExpiryDate, identity);
        }
    }

    private async Task<GoodsReceiptDetailDto> RequireGrnAccessAsync(Guid id, CancellationToken cancellationToken)
    {
        var grn = await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Phiếu nhập không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(grn.WarehouseId, cancellationToken);
        return grn;
    }
}
