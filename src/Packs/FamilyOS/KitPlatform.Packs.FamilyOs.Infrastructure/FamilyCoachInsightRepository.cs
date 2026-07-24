using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCoachInsightRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyCoachInsightRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<HistoryRow>> ListHistoryAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        string? timezoneId,
        CancellationToken cancellationToken)
    {
        var tz = FamilyTimeZones.ToPostgresId(timezoneId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<HistoryRow>(
            """
            SELECT
                d.flow_date AS FlowDate,
                c.template_id AS TemplateId,
                c.title AS Title,
                c.member_id AS MemberId,
                m.display_name AS MemberName,
                c.status AS Status,
                c.skip_reason AS SkipReason,
                c.context_anchor AS ContextAnchor,
                c.priority AS Priority,
                c.window_end AS WindowEnd,
                c.completed_at AS CompletedAt,
                CASE
                    WHEN c.status = 'done'
                     AND c.window_end IS NOT NULL
                     AND c.completed_at IS NOT NULL
                     AND (
                         (timezone(@Tz, c.completed_at))::date > d.flow_date
                         OR (
                             (timezone(@Tz, c.completed_at))::date = d.flow_date
                             AND (timezone(@Tz, c.completed_at))::time > c.window_end
                         )
                     )
                    THEN TRUE ELSE FALSE
                END AS IsLateDone
            FROM pack_family.day_flow d
            INNER JOIN pack_family.commitment c
              ON c.day_flow_id = d.id
             AND c.tenant_id = @TenantId
             AND c.deleted_at IS NULL
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id
             AND m.tenant_id = @TenantId
            WHERE d.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.deleted_at IS NULL
              AND d.flow_date >= @FromDate
              AND d.flow_date <= @ToDate
            ORDER BY d.flow_date, c.sort_order
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

    internal sealed class HistoryRow
    {
        public DateOnly FlowDate { get; init; }
        public Guid? TemplateId { get; init; }
        public string Title { get; init; } = "";
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public string Status { get; init; } = "";
        public string? SkipReason { get; init; }
        public string? ContextAnchor { get; init; }
        public string? Priority { get; init; }
        public TimeOnly? WindowEnd { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public bool IsLateDone { get; init; }
    }
}
