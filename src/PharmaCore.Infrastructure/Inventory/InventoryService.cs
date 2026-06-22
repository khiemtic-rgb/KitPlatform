using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Infrastructure.Inventory;

internal sealed class InventoryService : IInventoryService
{
    private readonly InventoryRepository _repository;
    private readonly ITenantContext _tenant;

    public InventoryService(InventoryRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        _repository.GetWarehousesAsync(cancellationToken);

    public Task<WarehouseDto?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetWarehouseAsync(id, cancellationToken);

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
            throw new InvalidOperationException("Mã kho không được để trống.");
        if (string.IsNullOrWhiteSpace(request.WarehouseName))
            throw new InvalidOperationException("Tên kho không được để trống.");
        if (!await _repository.BranchExistsAsync(request.BranchId, cancellationToken))
            throw new InvalidOperationException("Chi nhánh không tồn tại.");

        var id = await _repository.CreateWarehouseAsync(request, cancellationToken);
        return (await _repository.GetWarehouseAsync(id, cancellationToken))!;
    }

    public async Task<WarehouseDto?> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WarehouseName))
            throw new InvalidOperationException("Tên kho không được để trống.");

        var updated = await _repository.UpdateWarehouseAsync(id, request, cancellationToken);
        return updated ? await _repository.GetWarehouseAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await _repository.CountBatchesInWarehouseAsync(id, cancellationToken) > 0)
            return (false, "Không xóa được: kho còn tồn hàng.");

        var deleted = await _repository.SoftDeleteWarehouseAsync(id, cancellationToken);
        return deleted ? (true, null) : (false, "Kho không tồn tại.");
    }

    public Task<IReadOnlyList<BranchLookupDto>> GetBranchLookupsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetBranchLookupsAsync(cancellationToken);

    public Task<PagedStockBatchesResult> GetStockBatchesAsync(
        Guid? warehouseId,
        Guid? productId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return GetStockBatchesInternalAsync(warehouseId, productId, search, page, pageSize, cancellationToken);
    }

    private async Task<PagedStockBatchesResult> GetStockBatchesInternalAsync(
        Guid? warehouseId,
        Guid? productId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetStockBatchesAsync(
            warehouseId, productId, search, page, pageSize, cancellationToken);
        return new PagedStockBatchesResult(items, total, page, pageSize);
    }

    public Task<PagedStockProductsResult> GetStockProductsAsync(
        Guid? warehouseId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return GetStockProductsInternalAsync(warehouseId, search, page, pageSize, cancellationToken);
    }

    private async Task<PagedStockProductsResult> GetStockProductsInternalAsync(
        Guid? warehouseId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetStockProductsAsync(
            warehouseId, search, page, pageSize, cancellationToken);
        return new PagedStockProductsResult(items, total, page, pageSize);
    }

    public async Task<OpeningBalanceResultDto> CreateOpeningBalanceAsync(
        CreateOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng nhập tồn.");

        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.BatchNumber))
                throw new InvalidOperationException("Số lô không được để trống.");
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Số lượng phải lớn hơn 0.");
            if (line.UnitCost < 0)
                throw new InvalidOperationException("Giá vốn không hợp lệ.");
            if (!await _repository.ProductExistsAsync(line.ProductId, cancellationToken))
                throw new InvalidOperationException($"Sản phẩm không tồn tại: {line.ProductId}");
        }

        var batchIds = await _repository.ProcessOpeningBalanceAsync(
            request.WarehouseId, request.Notes, request.Lines, cancellationToken);

        return new OpeningBalanceResultDto(request.WarehouseId, batchIds.Count, batchIds);
    }

    public Task<IReadOnlyList<OpeningBalanceBatchListItemDto>> GetOpeningBalanceBatchesAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default) =>
        _repository.GetOpeningBalanceBatchesAsync(warehouseId, cancellationToken);

    public Task VoidOpeningBalanceBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _repository.VoidOpeningBalanceBatchAsync(batchId, cancellationToken);

    public Task<IReadOnlyList<TransferListItemDto>> GetTransfersAsync(CancellationToken cancellationToken = default) =>
        _repository.GetTransfersAsync(cancellationToken);

    public Task<TransferDetailDto?> GetTransferAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetTransferAsync(id, cancellationToken);

    public async Task<TransferDetailDto> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new InvalidOperationException("Kho xuất và kho nhận phải khác nhau.");
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng điều chuyển.");

        if (!await _repository.WarehouseExistsAsync(request.FromWarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho xuất không tồn tại.");
        if (!await _repository.WarehouseExistsAsync(request.ToWarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho nhận không tồn tại.");

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new InvalidOperationException("Số lượng chuyển phải lớn hơn 0.");
        }

        var transferId = await _repository.CreateTransferWithItemsAsync(
            request.FromWarehouseId, request.ToWarehouseId, request.Notes, request.Items, cancellationToken);

        return (await _repository.GetTransferAsync(transferId, cancellationToken))!;
    }

    public async Task<TransferDetailDto?> CompleteTransferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _repository.CompleteTransferAsync(id, _tenant.UserId, cancellationToken);
        return await _repository.GetTransferAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<AdjustmentListItemDto>> GetAdjustmentsAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAdjustmentsAsync(cancellationToken);

    public Task<AdjustmentDetailDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetAdjustmentAsync(id, cancellationToken);

    public async Task<AdjustmentDetailDto> CreateAdjustmentAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng kiểm kê.");

        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");

        var adjustmentId = await _repository.CreateAdjustmentWithItemsAsync(
            request.WarehouseId, request.Reason, request.Items, cancellationToken);

        return (await _repository.GetAdjustmentAsync(adjustmentId, cancellationToken))!;
    }

    public async Task<AdjustmentDetailDto?> ApproveAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _repository.ApproveAdjustmentAsync(id, _tenant.UserId, cancellationToken);
        return await _repository.GetAdjustmentAsync(id, cancellationToken);
    }
}
