using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;

namespace KitPlatform.Infrastructure.Success;

internal sealed class OwnerStaffTodayService : IOwnerStaffTodayService
{
    private const int CashVarianceWindowDays = 7;

    private readonly OwnerStaffTodayRepository _repo;
    private readonly LossPreventionRepository _loss;
    private readonly IBranchAccessService _branchAccess;

    public OwnerStaffTodayService(
        OwnerStaffTodayRepository repo,
        LossPreventionRepository loss,
        IBranchAccessService branchAccess)
    {
        _repo = repo;
        _loss = loss;
        _branchAccess = branchAccess;
    }

    public async Task<OwnerStaffTodayDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = VietnamBusinessCalendar.Today(now);
        var scope = await _branchAccess.GetScopeAsync(cancellationToken);
        var (_, allowedWarehouses) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);

        var branches = await _repo.ListActiveBranchesAsync(scope, cancellationToken);
        var checklistItems = await _repo.ListChecklistItemsAsync(today, scope, cancellationToken);
        var tasks = await _repo.ListOpenTasksAsync(scope, cancellationToken);

        var (fromUtc, toUtc) = VietnamBusinessCalendar.RollingDaysRangeUtc(now, CashVarianceWindowDays);
        var samples = await _loss.ListClosedShiftVarianceSamplesAsync(
            fromUtc, toUtc, branchId: null, allowedWarehouses, cancellationToken);

        var activeEmployees = await _repo.CountActiveEmployeesAsync(scope, cancellationToken);
        var withoutCredential = await _repo.CountEmployeesWithoutCredentialAsync(scope, cancellationToken);

        return new OwnerStaffTodayDto(
            today,
            OwnerStaffTodayRules.SummarizeChecklists(branches, checklistItems),
            OwnerStaffTodayRules.GroupTasksByAssignee(tasks, now),
            CashVarianceAccountability.GroupByCloser(samples),
            withoutCredential,
            activeEmployees);
    }
}
