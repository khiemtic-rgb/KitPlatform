using Dapper;
using System.Text.Json;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyParentSuccessRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyParentSuccessRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<PeriodEventCounts> CountEventsAsync(
        Guid familyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<PeriodEventCounts>(
            """
            SELECT
                COALESCE(SUM(CASE WHEN event_type = 'parent_nudge' THEN 1 ELSE 0 END), 0)::int AS ParentNudges,
                COALESCE(SUM(CASE WHEN event_type = 'self_start' THEN 1 ELSE 0 END), 0)::int AS SelfStarts,
                COALESCE(SUM(CASE WHEN event_type = 'reminder_fired' THEN 1 ELSE 0 END), 0)::int AS ReminderFired,
                COALESCE(SUM(CASE WHEN event_type = 'commitment_done' THEN 1 ELSE 0 END), 0)::int AS CommitmentDone,
                COALESCE(SUM(CASE WHEN event_type = 'commitment_skipped' THEN 1 ELSE 0 END), 0)::int AS CommitmentSkipped,
                COALESCE(SUM(CASE WHEN event_type = 'habit_stage_changed' THEN 1 ELSE 0 END), 0)::int AS HabitStageChanged,
                COALESCE(SUM(CASE WHEN event_type = 'reminder_suppressed' THEN 1 ELSE 0 END), 0)::int AS ReminderSuppressed,
                COALESCE(COUNT(*) FILTER (
                    WHERE event_type = 'habit_stage_changed'
                      AND (
                        payload_json->>'to' IN ('autonomous', 'maintained')
                        OR payload_json->>'stage' IN ('autonomous', 'maintained')
                        OR payload_json->>'toStage' IN ('autonomous', 'maintained')
                      )
                ), 0)::int AS HabitGraduations
            FROM pack_family.behavior_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND occurred_at >= @FromUtc
              AND occurred_at < @ToUtc
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
            });
    }

    public async Task<int> CountQualityMomentsAsync(
        Guid familyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        // Gratitude + family memory captures in window (quality-time proxies).
        var gratitude = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.child_gratitude
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND created_at >= @FromUtc
              AND created_at < @ToUtc
            """,
            new { TenantId, FamilyId = familyId, FromUtc = fromUtc, ToUtc = toUtc });

        var memories = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.family_memory
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND created_at >= @FromUtc
              AND created_at < @ToUtc
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, FromUtc = fromUtc, ToUtc = toUtc });

        return gratitude + memories;
    }

    public async Task<int> CountDistinctEventDaysAsync(
        Guid familyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string timezoneId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var pgTz = FamilyTimeZones.ToPostgresId(timezoneId);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT (occurred_at AT TIME ZONE @Tz)::date)::int
            FROM pack_family.behavior_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND occurred_at >= @FromUtc
              AND occurred_at < @ToUtc
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Tz = pgTz,
            });
    }

    public async Task<CheckinRow?> GetCheckinAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CheckinRow>(
            """
            SELECT id AS Id,
                   family_id AS FamilyId,
                   member_id AS MemberId,
                   flow_date AS FlowDate,
                   q_less_nudge AS QLessNudge,
                   q_less_tension AS QLessTension,
                   q_quality_time AS QQualityTime,
                   note AS Note,
                   updated_at AS UpdatedAt
            FROM pack_family.parent_success_checkin
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND flow_date = @FlowDate
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, FlowDate = flowDate });
    }

    public async Task<CheckinRow> UpsertCheckinAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        bool qLessNudge,
        bool qLessTension,
        bool qQualityTime,
        string? note,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<CheckinRow>(
            """
            INSERT INTO pack_family.parent_success_checkin (
                tenant_id, family_id, member_id, flow_date,
                q_less_nudge, q_less_tension, q_quality_time, note
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @FlowDate,
                @QLessNudge, @QLessTension, @QQualityTime, @Note
            )
            ON CONFLICT (tenant_id, family_id, member_id, flow_date)
            DO UPDATE SET
                q_less_nudge = EXCLUDED.q_less_nudge,
                q_less_tension = EXCLUDED.q_less_tension,
                q_quality_time = EXCLUDED.q_quality_time,
                note = EXCLUDED.note,
                updated_at = NOW(),
                deleted_at = NULL
            RETURNING id AS Id,
                      family_id AS FamilyId,
                      member_id AS MemberId,
                      flow_date AS FlowDate,
                      q_less_nudge AS QLessNudge,
                      q_less_tension AS QLessTension,
                      q_quality_time AS QQualityTime,
                      note AS Note,
                      updated_at AS UpdatedAt
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
                QLessNudge = qLessNudge,
                QLessTension = qLessTension,
                QQualityTime = qQualityTime,
                Note = note,
            });
    }

    public async Task<int> CountPositiveCheckinsAsync(
        Guid familyId,
        Guid? memberId,
        DateOnly from,
        DateOnly to,
        string column,
        CancellationToken cancellationToken)
    {
        // column must be one of the three boolean columns — validated by caller.
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var sql = $"""
            SELECT COUNT(*)::int
            FROM pack_family.parent_success_checkin
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND flow_date >= @From
              AND flow_date <= @To
              AND deleted_at IS NULL
              AND {column} = TRUE
              AND (@MemberId IS NULL OR member_id = @MemberId)
            """;
        return await conn.ExecuteScalarAsync<int>(
            sql,
            new { TenantId, FamilyId = familyId, MemberId = memberId, From = from, To = to });
    }

    public async Task<IReadOnlyList<string>> ListCoachActedTipIdsAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        string timezoneId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var pgTz = FamilyTimeZones.ToPostgresId(timezoneId);
        var rows = await conn.QueryAsync<string>(
            """
            SELECT DISTINCT NULLIF(TRIM(payload_json->>'tipId'), '')
            FROM pack_family.behavior_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND event_type = @EventType
              AND (
                    COALESCE(payload_json->>'flowDate', '') = @FlowDateText
                    OR (occurred_at AT TIME ZONE @Tz)::date = @FlowDate
                  )
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                EventType = FamilyBehaviorEventTypes.ParentCoachActed,
                FlowDate = flowDate,
                FlowDateText = flowDate.ToString("yyyy-MM-dd"),
                Tz = pgTz,
            });
        return rows.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()!;
    }

    public async Task InsertCoachActedAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        string tipId,
        string? tipSource,
        string? slot,
        string? titleVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            tipId,
            tipSource,
            slot,
            titleVi,
            flowDate = flowDate.ToString("yyyy-MM-dd"),
        });
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.behavior_event (
                tenant_id, family_id, member_id, event_type,
                commitment_id, template_id, payload_json
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @EventType,
                NULL, NULL, CAST(@Payload AS jsonb)
            )
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                EventType = FamilyBehaviorEventTypes.ParentCoachActed,
                Payload = payload,
            });
    }

    internal sealed class PeriodEventCounts
    {
        public int ParentNudges { get; init; }
        public int SelfStarts { get; init; }
        public int ReminderFired { get; init; }
        public int CommitmentDone { get; init; }
        public int CommitmentSkipped { get; init; }
        public int HabitStageChanged { get; init; }
        public int ReminderSuppressed { get; init; }
        public int HabitGraduations { get; init; }
    }

    internal sealed class CheckinRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid MemberId { get; init; }
        public DateOnly FlowDate { get; init; }
        public bool QLessNudge { get; init; }
        public bool QLessTension { get; init; }
        public bool QQualityTime { get; init; }
        public string? Note { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
