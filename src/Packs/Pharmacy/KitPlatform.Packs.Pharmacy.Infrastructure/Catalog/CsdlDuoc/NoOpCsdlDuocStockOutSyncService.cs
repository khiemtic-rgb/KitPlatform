namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal sealed class NoOpCsdlDuocStockOutSyncService : ICsdlDuocStockOutSyncService
{
    public Task SyncSalesOrderAsync(
        Guid tenantId,
        Guid salesOrderId,
        string? orderNumber,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<CsdlDuocSyncLogDto>> ListRecentAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CsdlDuocSyncLogDto>>([]);
}
