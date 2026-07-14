using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;

namespace KitPlatform.Infrastructure.Success;

internal sealed class LossPreventionService : ILossPreventionService
{
    private readonly LossPreventionRepository _repo;
    private readonly IBranchAccessService _branchAccess;

    public LossPreventionService(LossPreventionRepository repo, IBranchAccessService branchAccess)
    {
        _repo = repo;
        _branchAccess = branchAccess;
    }

    public async Task<LossCashVarianceTodayDto> GetCashVarianceTodayAsync(
        decimal? threshold = null,
        CancellationToken cancellationToken = default)
    {
        var t = NormalizeThreshold(threshold);
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        var shifts = await _repo.ListTodayShiftsAsync(t, allowed, cancellationToken);
        var closed = shifts.Count(s => s.Status == "closed");
        var open = shifts.Count(s => s.Status == "open");
        var alerts = shifts.Where(s => s.IsAlert).ToList();
        return new LossCashVarianceTodayDto(
            VietnamBusinessCalendar.Today(DateTime.UtcNow),
            t,
            closed,
            open,
            alerts.Count,
            shifts.Where(s => s.Status == "closed").Select(s => s.AbsCashVariance).DefaultIfEmpty(0).Max(),
            shifts);
    }

    public async Task<OwnerCockpitRiskStripDto> GetRiskStripAsync(
        decimal? cashVarianceThreshold = null,
        CancellationToken cancellationToken = default)
    {
        var today = await GetCashVarianceTodayAsync(cashVarianceThreshold, cancellationToken);
        var top = today.Shifts.FirstOrDefault(s => s.IsAlert);
        return new OwnerCockpitRiskStripDto(
            today.Threshold,
            today.ClosedShiftCount,
            today.OpenShiftCount,
            today.AlertCount,
            today.MaxAbsVariance,
            top?.ShiftNumber);
    }

    private static decimal NormalizeThreshold(decimal? threshold)
    {
        if (threshold is null || threshold <= 0)
            return LossPreventionRepository.DefaultCashVarianceThreshold;
        return threshold.Value;
    }
}
