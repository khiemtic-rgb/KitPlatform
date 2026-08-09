using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Inventory;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class InventoryService : IInventoryService
{
    private readonly InventoryRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly IAuditEngine _audit;
    private readonly IBranchAccessService _branchAccess;

    public InventoryService(
        InventoryRepository repository,
        ITenantContext tenant,
        IAuditEngine audit,
        IBranchAccessService branchAccess)
    {
        _repository = repository;
        _tenant = tenant;
        _audit = audit;
        _branchAccess = branchAccess;
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetWarehousesAsync(cancellationToken);
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        if (scope.Unrestricted)
            return all;
        return all.Where(w => scope.WarehouseIds.Contains(w.Id)).ToList();
    }

    public async Task<WarehouseDto?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(id, cancellationToken);
        return await _repository.GetWarehouseAsync(id, cancellationToken);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
            throw new InvalidOperationException("Mã kho không được để trống.");
        if (string.IsNullOrWhiteSpace(request.WarehouseName))
            throw new InvalidOperationException("Tên kho không được để trống.");
        if (!await _repository.BranchExistsAsync(request.BranchId, cancellationToken))
            throw new InvalidOperationException("Chi nhánh không tồn tại.");
        await _branchAccess.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        var id = await _repository.CreateWarehouseAsync(request, cancellationToken);
        return (await _repository.GetWarehouseAsync(id, cancellationToken))!;
    }

    public async Task<WarehouseDto?> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.WarehouseName))
            throw new InvalidOperationException("Tên kho không được để trống.");

        var updated = await _repository.UpdateWarehouseAsync(id, request, cancellationToken);
        return updated ? await _repository.GetWarehouseAsync(id, cancellationToken) : null;
    }

    public async Task<(bool Ok, string? Error)> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(id, cancellationToken);
        if (await _repository.CountBatchesInWarehouseAsync(id, cancellationToken) > 0)
            return (false, "Không xóa được: kho còn tồn hàng.");

        var deleted = await _repository.SoftDeleteWarehouseAsync(id, cancellationToken);
        return deleted ? (true, null) : (false, "Kho không tồn tại.");
    }

    public async Task<IReadOnlyList<BranchLookupDto>> GetBranchLookupsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetBranchLookupsAsync(cancellationToken);
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        if (scope.Unrestricted)
            return all;
        return all.Where(b => scope.BranchIds.Contains(b.Id)).ToList();
    }

    public async Task<PagedStockBatchesResult> GetStockBatchesAsync(
        Guid? warehouseId,
        Guid? productId,
        string? search,
        string? expiry,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var (items, total) = await _repository.GetStockBatchesAsync(
            warehouseId, allowed, productId, search, expiry, page, pageSize, cancellationToken);
        return new PagedStockBatchesResult(items, total, page, pageSize);
    }

    public async Task<PagedStockProductsResult> GetStockProductsAsync(
        Guid? warehouseId,
        string? search,
        string? expiry,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var (items, total) = await _repository.GetStockProductsAsync(
            warehouseId, allowed, search, expiry, page, pageSize, cancellationToken);
        return new PagedStockProductsResult(items, total, page, pageSize);
    }

    public async Task<OpeningBalanceResultDto> CreateOpeningBalanceAsync(
        CreateOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);
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

        await _audit.WriteAsync(
            "opening_balance",
            request.WarehouseId,
            "create",
            new { warehouseId = request.WarehouseId, lineCount = batchIds.Count },
            cancellationToken);

        return new OpeningBalanceResultDto(request.WarehouseId, batchIds.Count, batchIds);
    }

    public async Task<PagedOpeningBalanceBatchesResult> GetOpeningBalanceBatchesAsync(
        Guid? warehouseId,
        Guid? productId,
        string? search,
        string? status,
        string? expiry,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        var (items, total, summaryTotal, summaryVoidable) = await _repository.GetOpeningBalanceBatchesAsync(
            warehouseId, allowed, productId, search, status, expiry, page, pageSize, cancellationToken);
        return new PagedOpeningBalanceBatchesResult(items, total, page, pageSize, summaryTotal, summaryVoidable);
    }

    public Task VoidOpeningBalanceBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _repository.VoidOpeningBalanceBatchAsync(batchId, cancellationToken);

    public async Task<PagedTransfersResult> GetTransfersAsync(
        TransferListFilter filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new TransferListFilter();
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        Guid[]? allowed = scope.Unrestricted ? null : scope.WarehouseIds.ToArray();
        if (!scope.Unrestricted && (allowed is null || allowed.Length == 0))
            return new PagedTransfersResult([], 0, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 100));

        var (items, total) = await _repository.GetTransfersAsync(filter, allowed, cancellationToken);
        return new PagedTransfersResult(items, total, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 100));
    }

    public async Task<TransferDetailDto?> GetTransferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetTransferAsync(id, cancellationToken);
        if (item is null) return null;
        await EnsureAnyWarehouseAccessAsync(item.FromWarehouseId, item.ToWarehouseId, cancellationToken);
        return item;
    }

    public async Task<TransferDetailDto> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.FromWarehouseId, cancellationToken);
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

    public async Task<TransferDetailDto?> ShipTransferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetTransferAsync(id, cancellationToken);
        if (existing is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(existing.FromWarehouseId, cancellationToken);
        await _repository.ShipTransferAsync(id, _tenant.UserId, cancellationToken);
        return await _repository.GetTransferAsync(id, cancellationToken);
    }

    public async Task<TransferDetailDto?> ReceiveTransferAsync(
        Guid id,
        ReceiveTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetTransferAsync(id, cancellationToken);
        if (existing is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(existing.ToWarehouseId, cancellationToken);

        IReadOnlyDictionary<Guid, decimal>? byItem = null;
        if (request.Items is { Count: > 0 })
        {
            byItem = request.Items.ToDictionary(i => i.TransferItemId, i => i.ReceivedQuantity);
        }

        await _repository.ReceiveTransferAsync(id, _tenant.UserId, request.Notes, byItem, cancellationToken);
        return await _repository.GetTransferAsync(id, cancellationToken);
    }

    public async Task<TransferDetailDto?> CompleteTransferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetTransferAsync(id, cancellationToken);
        if (existing is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(existing.FromWarehouseId, cancellationToken);
        await _branchAccess.EnsureWarehouseAccessAsync(existing.ToWarehouseId, cancellationToken);
        await _repository.CompleteTransferAsync(id, _tenant.UserId, cancellationToken);
        return await _repository.GetTransferAsync(id, cancellationToken);
    }

    public async Task<TransferDetailDto?> CancelTransferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetTransferAsync(id, cancellationToken);
        if (existing is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(existing.FromWarehouseId, cancellationToken);
        await _repository.CancelTransferAsync(id, cancellationToken);
        return await _repository.GetTransferAsync(id, cancellationToken);
    }

    private async Task EnsureAnyWarehouseAccessAsync(
        Guid fromWarehouseId,
        Guid toWarehouseId,
        CancellationToken cancellationToken)
    {
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        if (scope.Unrestricted)
            return;
        var allowed = scope.WarehouseIds.ToHashSet();
        if (!allowed.Contains(fromWarehouseId) && !allowed.Contains(toWarehouseId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập kho của phiếu này.");
    }

    public async Task<PagedAdjustmentsResult> GetAdjustmentsAsync(
        AdjustmentListFilter filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new AdjustmentListFilter();
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        Guid[]? allowed = scope.Unrestricted ? null : scope.WarehouseIds.ToArray();
        if (!scope.Unrestricted && (allowed is null || allowed.Length == 0))
            return new PagedAdjustmentsResult([], 0, page, pageSize);

        if (filter.WarehouseId is Guid warehouseId && allowed is not null && !allowed.Contains(warehouseId))
            return new PagedAdjustmentsResult([], 0, page, pageSize);

        var (items, total) = await _repository.GetAdjustmentsAsync(
            filter with { Page = page, PageSize = pageSize },
            allowed,
            cancellationToken);
        return new PagedAdjustmentsResult(items, total, page, pageSize);
    }

    public async Task<AdjustmentDetailDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetAdjustmentAsync(id, cancellationToken);
        if (item is not null)
            await _branchAccess.EnsureWarehouseAccessAsync(item.WarehouseId, cancellationToken);
        return item;
    }

    public async Task<AdjustmentDetailDto> CreateAdjustmentAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng kiểm kê.");

        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");

        var adjustmentId = await _repository.CreateAdjustmentWithItemsAsync(
            request.WarehouseId, request.Reason, request.Items, cancellationToken);

        var created = (await _repository.GetAdjustmentAsync(adjustmentId, cancellationToken))!;
        await _audit.WriteAsync(
            "inventory_adjustment",
            adjustmentId,
            "create",
            new
            {
                created.AdjustmentNumber,
                created.WarehouseId,
                reason = request.Reason,
                itemCount = created.Items.Count,
            },
            cancellationToken);
        return created;
    }

    public async Task<AdjustmentDetailDto?> ApproveAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var before = await _repository.GetAdjustmentAsync(id, cancellationToken);
        if (before is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(before.WarehouseId, cancellationToken);
        await _repository.ApproveAdjustmentAsync(id, _tenant.UserId, cancellationToken);
        var detail = await _repository.GetAdjustmentAsync(id, cancellationToken);
        if (detail is not null)
        {
            await _audit.WriteAsync(
                "inventory_adjustment",
                id,
                "approve",
                new
                {
                    detail.AdjustmentNumber,
                    detail.WarehouseId,
                    reason = before.Reason,
                    itemCount = detail.Items.Count,
                },
                cancellationToken);
        }
        return detail;
    }

    public async Task<AdjustmentDetailDto?> CancelAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var before = await _repository.GetAdjustmentAsync(id, cancellationToken);
        if (before is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(before.WarehouseId, cancellationToken);
        await _repository.CancelAdjustmentAsync(id, cancellationToken);
        var detail = await _repository.GetAdjustmentAsync(id, cancellationToken);
        if (detail is not null)
        {
            await _audit.WriteAsync(
                "inventory_adjustment",
                id,
                "cancel",
                new
                {
                    detail.AdjustmentNumber,
                    detail.WarehouseId,
                    reason = before.Reason,
                    previousStatus = before.Status,
                },
                cancellationToken);
        }
        return detail;
    }

    public async Task<AdjustmentDetailDto> CreateCountingSessionAsync(
        CreateCountingSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);
        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho không tồn tại.");

        if (await _repository.HasActiveCountingSessionAsync(request.WarehouseId, cancellationToken))
            throw new InvalidOperationException("Kho đang có phiên kiểm kê chưa duyệt.");

        var adjustmentId = await _repository.CreateCountingAdjustmentAsync(
            request.WarehouseId, request.Reason, cancellationToken);

        return (await _repository.GetAdjustmentAsync(adjustmentId, cancellationToken))!;
    }

    public async Task<AdjustmentListItemDto?> GetActiveCountingSessionAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        return await _repository.GetActiveCountingSessionAsync(warehouseId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdjustmentCountEntryDto>> AddCountEntriesAsync(
        Guid adjustmentId,
        AddCountEntriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var adjustment = await _repository.GetAdjustmentAsync(adjustmentId, cancellationToken)
            ?? throw new InvalidOperationException("Phiên kiểm kê không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(adjustment.WarehouseId, cancellationToken);
        if (request.Entries.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một dòng đếm.");

        foreach (var entry in request.Entries)
        {
            if (entry.Quantity < 0)
                throw new InvalidOperationException("Số lượng đếm không được âm (0 = hết tồn thực tế).");
            if (entry.BatchId is null || entry.BatchId == Guid.Empty)
                throw new InvalidOperationException("Phải chọn lô khi ghi nhận đếm.");
        }

        return await _repository.AddCountEntriesAsync(adjustmentId, request.Entries, _tenant.UserId, cancellationToken);
    }

    public Task DeleteCountEntryAsync(Guid adjustmentId, Guid entryId, CancellationToken cancellationToken = default) =>
        _repository.DeleteCountEntryAsync(adjustmentId, entryId, cancellationToken);

    public Task<AdjustmentCountPreviewResultDto> GetCountPreviewAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken = default) =>
        _repository.GetCountPreviewAsync(adjustmentId, cancellationToken);

    public Task<IReadOnlyList<AdjustmentCountEntryDto>> GetCountEntriesAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken = default) =>
        _repository.GetCountEntriesAsync(adjustmentId, cancellationToken);

    public async Task<InventoryBarcodeResolveDto?> ResolveInventoryBarcodeAsync(
        Guid warehouseId,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        return await _repository.ResolveInventoryBarcodeAsync(warehouseId, barcode.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<LowStockProductDto>> GetLowStockProductsAsync(
        Guid? warehouseId,
        decimal defaultThreshold,
        CancellationToken cancellationToken = default)
    {
        var (scopedId, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(warehouseId, cancellationToken);
        return await _repository.GetLowStockProductsAsync(scopedId, allowed, defaultThreshold, cancellationToken);
    }
}
