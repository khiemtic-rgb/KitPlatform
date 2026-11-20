namespace KitPlatform.Packs.Pharmacy.Inventory;

public interface ILowStockSettingsService
{
    Task<LowStockSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<LowStockDefaultDto> UpdateDefaultAsync(
        UpdateLowStockDefaultRequest request,
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

    Task UpdateNotificationEmailAsync(
        UpdateLowStockNotificationEmailRequest request,
        CancellationToken cancellationToken = default);
}
