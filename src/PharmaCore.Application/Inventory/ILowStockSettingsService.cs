namespace PharmaCore.Application.Inventory;

using PharmaCore.Application.Configuration;

public interface ILowStockSettingsService
{
    Task<LowStockSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<TenantDefaultMinStockDto> UpdateDefaultAsync(
        UpdateTenantDefaultMinStockRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplyLowStockResultDto> ApplyDefaultToProductsAsync(
        ApplyLowStockToProductsRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplyLowStockResultDto> ApplyCategoryToProductsAsync(
        Guid categoryId,
        ApplyLowStockToProductsRequest request,
        CancellationToken cancellationToken = default);

    Task<CategoryLowStockSettingDto?> UpdateCategoryMinStockAsync(
        Guid categoryId,
        decimal? minStockQty,
        CancellationToken cancellationToken = default);

    Task<WarehouseLowStockSettingDto?> UpdateWarehouseMinStockAsync(
        Guid warehouseId,
        decimal? minStockQty,
        CancellationToken cancellationToken = default);
}
