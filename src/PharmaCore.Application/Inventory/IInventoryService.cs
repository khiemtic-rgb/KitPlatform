namespace PharmaCore.Application.Inventory;

public interface IInventoryService
{
    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<WarehouseDto?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<WarehouseDto?> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> DeleteWarehouseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchLookupDto>> GetBranchLookupsAsync(CancellationToken cancellationToken = default);

    Task<PagedStockBatchesResult> GetStockBatchesAsync(
        Guid? warehouseId,
        Guid? productId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedStockProductsResult> GetStockProductsAsync(
        Guid? warehouseId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceResultDto> CreateOpeningBalanceAsync(
        CreateOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpeningBalanceBatchListItemDto>> GetOpeningBalanceBatchesAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task VoidOpeningBalanceBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransferListItemDto>> GetTransfersAsync(CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> GetTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransferDetailDto> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> CompleteTransferAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjustmentListItemDto>> GetAdjustmentsAsync(CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto> CreateAdjustmentAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto?> ApproveAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);
}
