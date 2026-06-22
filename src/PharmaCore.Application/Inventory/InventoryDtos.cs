namespace PharmaCore.Application.Inventory;

public sealed record PagedStockBatchesResult(
    IReadOnlyList<StockBatchListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record PagedStockProductsResult(
    IReadOnlyList<StockProductSummaryDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record StockProductSummaryDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal TotalQuantity,
    int WarehouseCount,
    int BatchCount);

public sealed record StockBatchListItemDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal UnitCost,
    decimal QuantityAvailable,
    decimal QuantityReceived,
    short Status);

public sealed record WarehouseDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string WarehouseCode,
    string WarehouseName,
    short WarehouseType,
    bool IsDefault,
    string? Address,
    short Status);

public sealed record BranchLookupDto(Guid Id, string BranchCode, string BranchName);

public sealed record CreateWarehouseRequest(
    Guid BranchId,
    string WarehouseCode,
    string WarehouseName,
    short WarehouseType,
    bool IsDefault,
    string? Address);

public sealed record UpdateWarehouseRequest(
    string WarehouseName,
    short WarehouseType,
    bool IsDefault,
    string? Address,
    short Status);

public sealed record OpeningBalanceLineRequest(
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal UnitCost,
    decimal Quantity);

public sealed record CreateOpeningBalanceRequest(
    Guid WarehouseId,
    string? Notes,
    IReadOnlyList<OpeningBalanceLineRequest> Lines);

public sealed record OpeningBalanceResultDto(
    Guid WarehouseId,
    int LinesProcessed,
    IReadOnlyList<Guid> BatchIds);

public sealed record OpeningBalanceBatchListItemDto(
    Guid BatchId,
    Guid WarehouseId,
    string WarehouseName,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal UnitCost,
    decimal QuantityAvailable,
    decimal OpeningQuantity,
    DateTime FirstOpeningDate,
    bool CanVoid,
    string? VoidBlockReason);

public sealed record TransferListItemDto(
    Guid Id,
    string TransferNumber,
    Guid FromWarehouseId,
    string FromWarehouseName,
    Guid ToWarehouseId,
    string ToWarehouseName,
    short Status,
    DateTime TransferDate,
    int ItemCount);

public sealed record TransferItemDto(
    Guid Id,
    Guid BatchId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string BatchNumber,
    decimal Quantity);

public sealed record TransferDetailDto(
    Guid Id,
    string TransferNumber,
    Guid FromWarehouseId,
    string FromWarehouseName,
    Guid ToWarehouseId,
    string ToWarehouseName,
    short Status,
    DateTime TransferDate,
    string? Notes,
    IReadOnlyList<TransferItemDto> Items);

public sealed record CreateTransferItemRequest(Guid BatchId, decimal Quantity);

public sealed record CreateTransferRequest(
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    string? Notes,
    IReadOnlyList<CreateTransferItemRequest> Items);

public sealed record AdjustmentListItemDto(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    string WarehouseName,
    short Status,
    DateTime AdjustmentDate,
    int ItemCount);

public sealed record AdjustmentItemDto(
    Guid Id,
    Guid BatchId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string BatchNumber,
    decimal SystemQuantity,
    decimal ActualQuantity,
    decimal DifferenceQuantity,
    string? Note);

public sealed record AdjustmentDetailDto(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    string WarehouseName,
    short Status,
    DateTime AdjustmentDate,
    string? Reason,
    IReadOnlyList<AdjustmentItemDto> Items);

public sealed record CreateAdjustmentItemRequest(
    Guid BatchId,
    decimal ActualQuantity,
    string? Note);

public sealed record CreateAdjustmentRequest(
    Guid WarehouseId,
    string? Reason,
    IReadOnlyList<CreateAdjustmentItemRequest> Items);
