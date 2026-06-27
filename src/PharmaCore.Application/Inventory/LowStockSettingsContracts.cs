namespace PharmaCore.Application.Inventory;

public sealed record CategoryLowStockSettingDto(
    Guid Id,
    string CategoryCode,
    string CategoryName,
    decimal? MinStockQty,
    int ProductCount);

public sealed record WarehouseLowStockSettingDto(
    Guid Id,
    string WarehouseCode,
    string WarehouseName,
    Guid? BranchId,
    string? BranchName,
    decimal? MinStockQty,
    bool IsDefault);

public sealed record LowStockSettingsDto(
    decimal? DefaultMinStockQty,
    decimal SystemFallbackQty,
    IReadOnlyList<CategoryLowStockSettingDto> Categories,
    IReadOnlyList<WarehouseLowStockSettingDto> Warehouses);

public sealed record ApplyLowStockToProductsRequest(
    decimal? MinStockQty,
    bool OnlyUnset = true);

public sealed record ApplyLowStockResultDto(int UpdatedCount);
