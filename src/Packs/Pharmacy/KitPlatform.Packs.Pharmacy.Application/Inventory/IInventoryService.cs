namespace KitPlatform.Packs.Pharmacy.Inventory;

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
        string? expiry,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedStockProductsResult> GetStockProductsAsync(
        Guid? warehouseId,
        string? search,
        string? expiry,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OpeningBalanceResultDto> CreateOpeningBalanceAsync(
        CreateOpeningBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedOpeningBalanceBatchesResult> GetOpeningBalanceBatchesAsync(
        Guid? warehouseId,
        Guid? productId,
        string? search,
        string? status,
        string? expiry,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task VoidOpeningBalanceBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<PagedTransfersResult> GetTransfersAsync(TransferListFilter filter, CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> GetTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransferDetailDto> CreateTransferAsync(CreateTransferRequest request, CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> ShipTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> ReceiveTransferAsync(Guid id, ReceiveTransferRequest request, CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> CompleteTransferAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransferDetailDto?> CancelTransferAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedAdjustmentsResult> GetAdjustmentsAsync(AdjustmentListFilter filter, CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto> CreateAdjustmentAsync(CreateAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto?> ApproveAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdjustmentDetailDto?> CancelAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AdjustmentDetailDto> CreateCountingSessionAsync(
        CreateCountingSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<AdjustmentListItemDto?> GetActiveCountingSessionAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjustmentCountEntryDto>> AddCountEntriesAsync(
        Guid adjustmentId,
        AddCountEntriesRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteCountEntryAsync(Guid adjustmentId, Guid entryId, CancellationToken cancellationToken = default);

    Task<AdjustmentCountPreviewResultDto> GetCountPreviewAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjustmentCountEntryDto>> GetCountEntriesAsync(
        Guid adjustmentId,
        CancellationToken cancellationToken = default);

    Task<InventoryBarcodeResolveDto?> ResolveInventoryBarcodeAsync(
        Guid warehouseId,
        string barcode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LowStockProductDto>> GetLowStockProductsAsync(
        Guid? warehouseId,
        decimal defaultThreshold,
        CancellationToken cancellationToken = default);
}
