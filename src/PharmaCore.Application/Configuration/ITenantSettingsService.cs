namespace PharmaCore.Application.Configuration;

public interface ITenantSettingsService
{
    Task<TenantBatchMode> GetBatchModeAsync(CancellationToken cancellationToken = default);

    Task<TenantReceiptSettingsDto> GetReceiptSettingsAsync(CancellationToken cancellationToken = default);

    Task<TenantReceiptSettingsDto> UpdateReceiptSettingsAsync(
        UpdateTenantReceiptSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantBatchModeSettingsDto> GetBatchModeSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<TenantBatchModeSettingsDto> UpdateBatchModeAsync(
        UpdateTenantBatchModeRequest request,
        CancellationToken cancellationToken = default);
}
