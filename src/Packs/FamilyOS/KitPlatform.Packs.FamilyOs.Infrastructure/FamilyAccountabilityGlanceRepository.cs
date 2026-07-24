using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAccountabilityGlanceRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyAccountabilityGlanceRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<DayAggRow>> ListDayAggregatesAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        string? timezoneId,
        CancellationToken cancellationToken)
    {
        var tz = FamilyTimeZones.ToPostgresId(timezoneId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<DayAggRow>(
            """
            WITH days AS (
                SELECT d.id AS day_flow_id, d.flow_date
                FROM pack_family.day_flow d
                WHERE d.tenant_id = @TenantId
                  AND d.family_id = @FamilyId
                  AND d.deleted_at IS NULL
                  AND d.flow_date >= @FromDate
                  AND d.flow_date <= @ToDate
            ),
            child_commitments AS (
                SELECT
                    days.flow_date,
                    c.status,
                    CASE
                        WHEN c.status = 'done'
                         AND c.window_end IS NOT NULL
                         AND c.completed_at IS NOT NULL
                         AND (
                             (timezone(@Tz, c.completed_at))::date > days.flow_date
                             OR (
                                 (timezone(@Tz, c.completed_at))::date = days.flow_date
                                 AND (timezone(@Tz, c.completed_at))::time > c.window_end
                             )
                         )
                        THEN 1 ELSE 0
                    END AS is_late_done
                FROM days
                INNER JOIN pack_family.commitment c
                  ON c.day_flow_id = days.day_flow_id
                 AND c.tenant_id = @TenantId
                 AND c.deleted_at IS NULL
                INNER JOIN pack_family.membership m
                  ON m.id = c.member_id
                 AND m.tenant_id = @TenantId
                 AND m.deleted_at IS NULL
                 AND m.role_code = 'child'
            ),
            consequences AS (
                SELECT e.flow_date, COUNT(*)::int AS applied_count
                FROM pack_family.consequence_event e
                WHERE e.tenant_id = @TenantId
                  AND e.family_id = @FamilyId
                  AND e.deleted_at IS NULL
                  AND e.status = 'applied'
                  AND e.flow_date >= @FromDate
                  AND e.flow_date <= @ToDate
                GROUP BY e.flow_date
            )
            SELECT
                d.flow_date AS FlowDate,
                COALESCE(SUM(CASE WHEN cc.status = 'done' THEN 1 ELSE 0 END), 0)::int AS ChildDone,
                COALESCE(SUM(CASE WHEN cc.status = 'skipped' THEN 1 ELSE 0 END), 0)::int AS ChildSkipped,
                COALESCE(SUM(CASE WHEN cc.status IN ('pending', 'in_progress') THEN 1 ELSE 0 END), 0)::int AS ChildOpen,
                COALESCE(SUM(cc.is_late_done), 0)::int AS ChildLateDone,
                COALESCE(MAX(con.applied_count), 0)::int AS AppliedConsequences,
                COUNT(cc.status)::int AS ChildTotal
            FROM days d
            LEFT JOIN child_commitments cc ON cc.flow_date = d.flow_date
            LEFT JOIN consequences con ON con.flow_date = d.flow_date
            GROUP BY d.flow_date
            ORDER BY d.flow_date
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromDate = from,
                ToDate = to,
                Tz = tz,
            });
        return rows.AsList();
    }

    internal sealed class DayAggRow
    {
        public DateOnly FlowDate { get; init; }
        public int ChildDone { get; init; }
        public int ChildSkipped { get; init; }
        public int ChildOpen { get; init; }
        public int ChildLateDone { get; init; }
        public int AppliedConsequences { get; init; }
        public int ChildTotal { get; init; }
    }
}
