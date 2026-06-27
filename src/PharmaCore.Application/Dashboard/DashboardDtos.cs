namespace PharmaCore.Application.Dashboard;

public sealed record DashboardOverviewDto(
    DashboardSalesSnapshotDto Sales,
    DashboardCatalogSnapshotDto Catalog,
    DashboardInventorySnapshotDto Inventory,
    DashboardProcurementSnapshotDto Procurement,
    DashboardO2oSnapshotDto O2o);

public sealed record DashboardSalesSnapshotDto(
    decimal TodayNetTotal,
    decimal WeekNetTotal,
    int TodayOrderCount);

public sealed record DashboardCatalogSnapshotDto(
    int ProductCount,
    int CustomerCount);

public sealed record DashboardInventorySnapshotDto(
    int ActiveBatchCount,
    int NearExpiryBatchCount,
    int LowStockBatchCount,
    int LowStockProductCount,
    int ExpiryDays);

public sealed record DashboardProcurementSnapshotDto(
    int PendingReceiptCount);

public sealed record DashboardO2oSnapshotDto(
    int DraftOrdersAwaitingCount,
    int ReservationsAwaitingCount,
    int ChatUnreadCount);
