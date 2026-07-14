using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Infrastructure.Reports;

namespace KitPlatform.Infrastructure.Success;

internal sealed class LossPreventionService : ILossPreventionService
{
    private const string AttributionNotes =
        "Hủy HĐ: chỉ draft→cancelled; gán theo employee_id lúc tạo (không có cancelled_by). " +
        "Giảm giá: POS order+line trên đơn Completed; gán seller employee_id (không gồm loyalty/voucher). " +
        "Điều chỉnh tồn: approved adjustments; gán theo approved_by→employee; giá trị = |ΔSL|×unit_cost.";

    private const string AuditFeedNotes =
        "Nguồn: kit_audit.activity_log (dual-write từ audit_logs). " +
        "Tạo/sửa/hủy HĐ · trả hàng · điều chỉnh tồn (approve/create) có sẵn; giảm giá POS ghi action=discount khi có giảm. " +
        "Xuất nội bộ V0: phiếu điều chỉnh reason chứa 'nội bộ' / internal_issue. " +
        "Chi nhánh = JOIN chứng từ (audit không có branch_id).";

    private static readonly HashSet<string> AllowedAuditEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "order_create", "order_edit", "order_cancel", "discount", "return", "stock_adjust", "internal_issue",
    };

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

    public async Task<LossEmployeeReportsDto> GetEmployeeReportsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        if (branchId is Guid bid)
            await _branchAccess.EnsureBranchAccessAsync(bid, cancellationToken);

        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);

        var cancellations = await _repo.ListCancellationsByEmployeeAsync(from, to, branchId, allowed, cancellationToken);
        var discounts = await _repo.ListDiscountsByEmployeeAsync(from, to, branchId, allowed, cancellationToken);
        var adjustments = await _repo.ListAdjustmentsByEmployeeAsync(from, to, branchId, allowed, cancellationToken);

        return new LossEmployeeReportsDto(from, to, branchId, AttributionNotes, cancellations, discounts, adjustments);
    }

    public async Task<LossAuditFeedDto> GetAuditFeedAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? branchId = null,
        Guid? userId = null,
        string? eventType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (branchId is Guid bid)
            await _branchAccess.EnsureBranchAccessAsync(bid, cancellationToken);

        var normalizedType = string.IsNullOrWhiteSpace(eventType) ? null : eventType.Trim();
        if (normalizedType is not null && !AllowedAuditEventTypes.Contains(normalizedType))
            throw new ArgumentException($"eventType không hợp lệ: {normalizedType}");

        var (from, to) = ReportsDateHelper.ResolveRangeUtc(fromUtc, toUtc, DateTime.UtcNow);
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _repo.ListAuditFeedAsync(
            from, to, branchId, userId, normalizedType, allowed, page, pageSize, cancellationToken);

        return new LossAuditFeedDto(
            from, to, branchId, userId, normalizedType, AuditFeedNotes, total, page, pageSize, items);
    }

    private static decimal NormalizeThreshold(decimal? threshold)
    {
        if (threshold is null || threshold <= 0)
            return LossPreventionRepository.DefaultCashVarianceThreshold;
        return threshold.Value;
    }
}
