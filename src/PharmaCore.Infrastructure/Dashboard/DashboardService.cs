using PharmaCore.Application.Dashboard;

namespace PharmaCore.Infrastructure.Dashboard;

internal sealed class DashboardService : IDashboardService
{
    private readonly DashboardRepository _repository;

    public DashboardService(DashboardRepository repository) => _repository = repository;

    public Task<DashboardOverviewDto> GetOverviewAsync(
        int expiryDays = 30,
        decimal lowStockThreshold = 10,
        CancellationToken cancellationToken = default) =>
        _repository.GetOverviewAsync(expiryDays, lowStockThreshold, cancellationToken);
}
