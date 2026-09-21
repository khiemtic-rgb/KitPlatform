using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Application.Dashboard;

namespace KitPlatform.Infrastructure.Dashboard;

internal sealed class DashboardService : IDashboardService
{
    private readonly DashboardRepository _repository;
    private readonly IBranchAccessService _branchAccess;
    private readonly IPermissionEngine _permissions;

    public DashboardService(
        DashboardRepository repository,
        IBranchAccessService branchAccess,
        IPermissionEngine permissions)
    {
        _repository = repository;
        _branchAccess = branchAccess;
        _permissions = permissions;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(
        int expiryDays = 30,
        decimal lowStockThreshold = 10,
        CancellationToken cancellationToken = default)
    {
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        var overview = await _repository.GetOverviewAsync(expiryDays, lowStockThreshold, allowed, cancellationToken);

        // STAFF/POS: vẫn mở Tổng quan làm hub, nhưng không trả số liệu doanh thu cửa hàng.
        if (!CanViewStoreAnalytics())
        {
            overview = overview with
            {
                Sales = new DashboardSalesSnapshotDto(
                    TodayNetTotal: 0,
                    WeekNetTotal: 0,
                    TodayOrderCount: 0,
                    TodayCollected: 0,
                    TodayNewDebt: 0),
            };
        }

        return overview;
    }

    private bool CanViewStoreAnalytics() =>
        _permissions.IsAdmin()
        || _permissions.HasPermission("reports.read")
        || _permissions.HasPermission("reports.write")
        || _permissions.HasPermission("reports.export");
}
