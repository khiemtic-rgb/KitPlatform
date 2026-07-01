namespace PharmaCore.Application.Reports;

public interface IReportsService
{
    IReadOnlyList<ReportCatalogItemDto> GetCatalog();

    Task<ReportTableResultDto> RunSalesRevenueByPeriodAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunSalesRevenueByPaymentMethodAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunSalesShiftsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunSalesRevenueByCategoryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunProcurementGrnValueAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string groupBy,
        Guid? supplierId,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunProcurementPayablesSnapshotAsync(
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunInventoryStockSnapshotAsync(
        Guid? warehouseId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ReportTableResultDto> RunInventoryNearExpiryAsync(
        Guid? warehouseId,
        int expiryDays,
        CancellationToken cancellationToken = default);
}
