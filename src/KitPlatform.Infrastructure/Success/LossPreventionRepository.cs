using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Success;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Infrastructure.Success;

internal sealed class LossPreventionRepository
{
    public const decimal DefaultCashVarianceThreshold = 10_000m;

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public LossPreventionRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<LossCashVarianceShiftDto>> ListTodayShiftsAsync(
        decimal threshold,
        Guid[]? allowedWarehouseIds,
        CancellationToken cancellationToken)
    {
        var (dayStart, dayEnd) = VietnamBusinessCalendar.TodayRangeUtc(DateTime.UtcNow);
        var warehouseFilter = allowedWarehouseIds is { Length: > 0 }
            ? "AND s.warehouse_id = ANY(@AllowedWarehouseIds)"
            : string.Empty;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid ShiftId,
            string ShiftNumber,
            Guid WarehouseId,
            string WarehouseName,
            Guid BranchId,
            string BranchName,
            short Status,
            decimal OpeningCash,
            decimal? ClosingCash,
            decimal? ExpectedCash,
            decimal? CashVariance,
            DateTime OpenedAt,
            DateTime? ClosedAt)>(
            $"""
            SELECT
                s.id AS ShiftId,
                s.shift_number AS ShiftNumber,
                s.warehouse_id AS WarehouseId,
                w.warehouse_name AS WarehouseName,
                w.branch_id AS BranchId,
                b.branch_name AS BranchName,
                s.status AS Status,
                s.opening_cash AS OpeningCash,
                s.closing_cash AS ClosingCash,
                s.expected_cash AS ExpectedCash,
                s.cash_variance AS CashVariance,
                s.opened_at AS OpenedAt,
                s.closed_at AS ClosedAt
            FROM sales_shifts s
            INNER JOIN warehouses w ON w.id = s.warehouse_id AND w.tenant_id = s.tenant_id
            INNER JOIN branches b ON b.id = w.branch_id AND b.tenant_id = s.tenant_id
            WHERE s.tenant_id = @TenantId
              AND (
                    s.status = @Open
                 OR (s.status = @Closed AND s.closed_at >= @DayStart AND s.closed_at < @DayEnd)
              )
              {warehouseFilter}
            ORDER BY
                CASE WHEN s.cash_variance IS NULL THEN 1 ELSE 0 END,
                ABS(COALESCE(s.cash_variance, 0)) DESC,
                s.opened_at DESC
            """,
            new
            {
                TenantId,
                DayStart = dayStart,
                DayEnd = dayEnd,
                Open = SalesShiftStatuses.Open,
                Closed = SalesShiftStatuses.Closed,
                AllowedWarehouseIds = allowedWarehouseIds,
            });

        return rows.Select(r =>
        {
            var abs = Math.Abs(r.CashVariance ?? 0);
            var status = r.Status == SalesShiftStatuses.Open ? "open" : "closed";
            var isAlert = r.Status == SalesShiftStatuses.Closed
                          && r.CashVariance is not null
                          && abs > threshold;
            return new LossCashVarianceShiftDto(
                r.ShiftId,
                r.ShiftNumber,
                r.WarehouseId,
                r.WarehouseName,
                r.BranchId,
                r.BranchName,
                status,
                r.OpeningCash,
                r.ClosingCash,
                r.ExpectedCash,
                r.CashVariance,
                abs,
                isAlert,
                r.OpenedAt,
                r.ClosedAt);
        }).ToList();
    }
}
