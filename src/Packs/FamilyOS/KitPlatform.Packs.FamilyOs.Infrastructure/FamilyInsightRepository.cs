using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyInsightRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyInsightRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    /// <summary>
    /// One commitment row per (day, commitment) across the full span (previous +
    /// current window), including the star delta actually posted for that item.
    /// </summary>
    public async Task<IReadOnlyList<InsightRow>> ListCommitmentsAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        string? timezoneId,
        CancellationToken cancellationToken)
    {
        var tz = FamilyTimeZones.ToPostgresId(timezoneId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<InsightRow>(
            """
            SELECT
                d.flow_date AS FlowDate,
                c.member_id AS MemberId,
                m.display_name AS MemberName,
                m.role_code AS RoleCode,
                c.template_id AS TemplateId,
                c.title AS Title,
                c.status AS Status,
                c.skip_reason AS SkipReason,
                c.priority AS Priority,
                COALESCE(sl.delta, 0) AS StarDelta,
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
                END AS IsLateDone,
                COALESCE(NULLIF(TRIM(c.commitment_kind), ''), 'chore') AS CommitmentKind,
                c.evidence_satisfied_at AS EvidenceSatisfiedAt
            FROM pack_family.day_flow d
            INNER JOIN pack_family.commitment c
              ON c.day_flow_id = d.id
             AND c.tenant_id = @TenantId
             AND c.deleted_at IS NULL
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id
             AND m.tenant_id = @TenantId
            LEFT JOIN pack_family.star_ledger sl
              ON sl.commitment_id = c.id
             AND sl.tenant_id = @TenantId
            WHERE d.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.deleted_at IS NULL
              AND d.flow_date >= @FromDate
              AND d.flow_date <= @ToDate
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

    /// <summary>Distinct flow dates that actually exist in the span (data provenance).</summary>
    public async Task<IReadOnlyList<DateOnly>> ListDataDaysAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<DateOnly>(
            """
            SELECT DISTINCT flow_date
            FROM pack_family.day_flow
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND flow_date >= @FromDate
              AND flow_date <= @ToDate
            ORDER BY flow_date
            """,
            new { TenantId, FamilyId = familyId, FromDate = from, ToDate = to });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<ReminderDayRow>> ListReminderCountsAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ReminderDayRow>(
            """
            SELECT flow_date AS FlowDate, COUNT(*)::int AS Count
            FROM pack_family.reminder_dispatch
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND flow_date >= @FromDate
              AND flow_date <= @ToDate
            GROUP BY flow_date
            """,
            new { TenantId, FamilyId = familyId, FromDate = from, ToDate = to });
        return rows.AsList();
    }

    /// <summary>Has this family ever produced reminder dispatch rows? Distinguishes
    /// "0 reminders (great!)" from "reminders not tracked yet (older family)".</summary>
    public async Task<bool> HasAnyReminderHistoryAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_family.reminder_dispatch
                WHERE tenant_id = @TenantId AND family_id = @FamilyId
            )
            """,
            new { TenantId, FamilyId = familyId });
    }

    /// <summary>
    /// Opt-in shared parent goals + done check-ins in [from,to]. Privacy: only
    /// <c>share_with_family = TRUE</c> rows are returned.
    /// </summary>
    public async Task<IReadOnlyList<ParentGoalPeriodRow>> ListSharedParentGoalStatsAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ParentGoalPeriodRow>(
            """
            SELECT
                g.id AS GoalId,
                g.member_id AS MemberId,
                m.display_name AS MemberName,
                g.title AS Title,
                g.emoji AS Emoji,
                g.target_days_per_week AS TargetDaysPerWeek,
                (
                    SELECT COUNT(*)::int
                    FROM pack_family.parent_goal_checkin c
                    WHERE c.tenant_id = g.tenant_id
                      AND c.goal_id = g.id
                      AND c.deleted_at IS NULL
                      AND c.status = 'done'
                      AND c.checkin_date >= @FromDate
                      AND c.checkin_date <= @ToDate
                ) AS DoneDays,
                EXISTS(
                    SELECT 1
                    FROM pack_family.parent_goal_checkin c
                    WHERE c.tenant_id = g.tenant_id
                      AND c.goal_id = g.id
                      AND c.deleted_at IS NULL
                      AND c.status = 'done'
                      AND c.checkin_date = @Today
                ) AS TodayDone
            FROM pack_family.parent_goal g
            INNER JOIN pack_family.membership m
                ON m.tenant_id = g.tenant_id
               AND m.id = g.member_id
               AND m.deleted_at IS NULL
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.deleted_at IS NULL
              AND g.is_active = TRUE
              AND g.share_with_family = TRUE
            ORDER BY m.sort_order, m.display_name, g.sort_order
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromDate = from,
                ToDate = to,
                Today = today,
            });
        return rows.AsList();
    }

    public async Task<int> CountTeamUnlocksConfirmedAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.team_unlock_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND status = 'confirmed'
              AND flow_date >= @FromDate
              AND flow_date <= @ToDate
            """,
            new { TenantId, FamilyId = familyId, FromDate = from, ToDate = to });
    }

    internal sealed class InsightRow
    {
        public DateOnly FlowDate { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public string? RoleCode { get; init; }
        public Guid? TemplateId { get; init; }
        public string Title { get; init; } = "";
        public string Status { get; init; } = "";
        public string? SkipReason { get; init; }
        public string? Priority { get; init; }
        public int StarDelta { get; init; }
        public bool IsLateDone { get; init; }
        public string CommitmentKind { get; init; } = FamilyCommitmentKinds.Chore;
        public DateTimeOffset? EvidenceSatisfiedAt { get; init; }
    }

    internal sealed class ReminderDayRow
    {
        public DateOnly FlowDate { get; init; }
        public int Count { get; init; }
    }

    internal sealed class ParentGoalPeriodRow
    {
        public Guid GoalId { get; init; }
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Emoji { get; init; }
        public int TargetDaysPerWeek { get; init; }
        public int DoneDays { get; init; }
        public bool TodayDone { get; init; }
    }
}
