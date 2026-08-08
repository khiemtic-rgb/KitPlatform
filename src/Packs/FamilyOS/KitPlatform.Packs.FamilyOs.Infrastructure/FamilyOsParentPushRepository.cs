using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;
using Npgsql;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyOsParentPushRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyOsParentPushRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task UpsertSubscriptionAsync(
        Guid familyId,
        Guid membershipId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.parent_push_subscription (
                tenant_id, family_id, membership_id, endpoint, p256dh, auth, user_agent
            )
            VALUES (@TenantId, @FamilyId, @MembershipId, @Endpoint, @P256dh, @Auth, @UserAgent)
            ON CONFLICT (tenant_id, endpoint)
            DO UPDATE SET
                family_id = EXCLUDED.family_id,
                membership_id = EXCLUDED.membership_id,
                p256dh = EXCLUDED.p256dh,
                auth = EXCLUDED.auth,
                user_agent = EXCLUDED.user_agent,
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MembershipId = membershipId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
            });
    }

    public async Task SoftDeleteSubscriptionAsync(
        Guid familyId,
        Guid membershipId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.parent_push_subscription
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND membership_id = @MembershipId
              AND endpoint = @Endpoint
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MembershipId = membershipId, Endpoint = endpoint });
    }

    public async Task<bool> HasSubscriptionAsync(
        Guid familyId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_family.parent_push_subscription
                WHERE tenant_id = @TenantId
                  AND family_id = @FamilyId
                  AND membership_id = @MembershipId
                  AND deleted_at IS NULL
            )
            """,
            new { TenantId, FamilyId = familyId, MembershipId = membershipId });
    }

    public async Task SoftDeleteByEndpointAsync(string endpoint, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.parent_push_subscription
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE endpoint = @Endpoint AND deleted_at IS NULL
            """,
            new { Endpoint = endpoint });
    }

    public async Task<IReadOnlyList<SubscriptionRow>> ListAllActiveSubscriptionsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SubscriptionRow>(
            """
            SELECT
                s.id AS Id,
                s.tenant_id AS TenantId,
                s.family_id AS FamilyId,
                s.membership_id AS MembershipId,
                s.endpoint AS Endpoint,
                s.p256dh AS P256dh,
                s.auth AS Auth
            FROM pack_family.parent_push_subscription s
            WHERE s.deleted_at IS NULL
            """);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<OpenCommitmentRow>> ListOpenCommitmentsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<OpenCommitmentRow>(
            """
            SELECT
                c.id AS CommitmentId,
                c.tenant_id AS TenantId,
                d.family_id AS FamilyId,
                d.flow_date AS FlowDate,
                f.timezone AS Timezone,
                f.display_name AS FamilyName,
                c.title AS Title,
                c.status AS Status,
                c.window_start AS WindowStart,
                c.window_end AS WindowEnd,
                c.completed_at AS CompletedAt,
                c.member_id AS MemberId,
                m.display_name AS MemberName,
                m.role_code AS MemberRole,
                COALESCE(t.habit_stage, c.habit_stage, 'new') AS HabitStage,
                COALESCE(t.habit_streak_days, 0) AS HabitStreakDays,
                COALESCE(t.reminder_suppressed, c.reminder_suppressed, FALSE) AS ReminderSuppressed
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d
              ON d.id = c.day_flow_id AND d.tenant_id = c.tenant_id AND d.deleted_at IS NULL
            INNER JOIN pack_family.family f
              ON f.id = d.family_id AND f.tenant_id = c.tenant_id AND f.deleted_at IS NULL
             AND f.status = 'active'
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id AND m.tenant_id = c.tenant_id AND m.deleted_at IS NULL
            LEFT JOIN pack_family.commitment_template t
              ON t.id = c.template_id AND t.tenant_id = c.tenant_id AND t.deleted_at IS NULL
            WHERE c.deleted_at IS NULL
              AND c.status IN ('pending', 'in_progress')
            """);
        return rows.AsList();
    }

    /// <summary>SCH-01c — load blueprint layers for quiet-hour filter (cross-tenant worker).</summary>
    public async Task<string?> GetBlueprintLayersJsonAsync(
        Guid tenantId,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT layers_json::text
            FROM pack_family.family_blueprint
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
            """,
            new { TenantId = tenantId, FamilyId = familyId });
    }

    public async Task<IReadOnlyList<DigestItemRow>> ListDigestCandidatesAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<DigestItemRow>(
            """
            SELECT
                c.id AS CommitmentId,
                c.title AS Title,
                c.status AS Status,
                c.window_start AS WindowStart,
                c.window_end AS WindowEnd,
                c.completed_at AS CompletedAt,
                m.display_name AS MemberName
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d
              ON d.id = c.day_flow_id AND d.tenant_id = c.tenant_id AND d.deleted_at IS NULL
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id AND m.tenant_id = c.tenant_id AND m.deleted_at IS NULL
            WHERE c.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date = @FlowDate
              AND c.deleted_at IS NULL
              AND (
                    c.status IN ('pending', 'in_progress')
                 OR c.status = 'done'
              )
            ORDER BY c.sort_order
            """,
            new { TenantId = tenantId, FamilyId = familyId, FlowDate = flowDate });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<FamilyClockRow>> ListActiveFamiliesAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<FamilyClockRow>(
            """
            SELECT id AS FamilyId, tenant_id AS TenantId, timezone AS Timezone, display_name AS DisplayName
            FROM pack_family.family
            WHERE deleted_at IS NULL AND status = 'active'
            """);
        return rows.AsList();
    }

    /// <summary>Done items that may still need parent star/evidence approval.</summary>
    public async Task<IReadOnlyList<PendingApprovalRow>> ListPendingApprovalsAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PendingApprovalRow>(
            """
            SELECT
                c.id AS CommitmentId,
                c.title AS Title,
                c.evidence_url AS EvidenceUrl,
                c.pending_star_delta AS PendingStarDelta,
                m.display_name AS MemberName
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d
              ON d.id = c.day_flow_id AND d.tenant_id = c.tenant_id AND d.deleted_at IS NULL
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id AND m.tenant_id = c.tenant_id AND m.deleted_at IS NULL
            WHERE c.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date = @FlowDate
              AND c.deleted_at IS NULL
              AND c.status = 'done'
              AND c.star_posted_at IS NULL
              AND c.pending_star_delta IS NOT NULL
            ORDER BY c.sort_order, c.completed_at NULLS LAST
            """,
            new { TenantId = tenantId, FamilyId = familyId, FlowDate = flowDate });
        return rows.AsList();
    }

    public async Task<DayAggRow?> GetDayAggregateAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DayAggRow>(
            """
            SELECT
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child' AND c.status = 'done'
                )::int AS ChildDone,
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child' AND c.status = 'skipped'
                )::int AS ChildSkipped,
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child' AND c.status IN ('pending', 'in_progress')
                )::int AS ChildOpen,
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child'
                      AND c.status = 'done'
                      AND c.completed_at IS NOT NULL
                      AND c.window_end IS NOT NULL
                      AND (c.completed_at AT TIME ZONE f.timezone)::time > c.window_end
                )::int AS ChildLateDone,
                (
                    SELECT COUNT(*)::int
                    FROM pack_family.consequence_event ce
                    WHERE ce.tenant_id = @TenantId
                      AND ce.family_id = @FamilyId
                      AND ce.flow_date = @FlowDate
                      AND ce.deleted_at IS NULL
                      AND ce.status = 'applied'
                ) AS AppliedConsequences
            FROM pack_family.day_flow d
            INNER JOIN pack_family.family f
              ON f.id = d.family_id AND f.tenant_id = d.tenant_id
            LEFT JOIN pack_family.commitment c
              ON c.day_flow_id = d.id AND c.tenant_id = d.tenant_id AND c.deleted_at IS NULL
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id AND m.tenant_id = c.tenant_id AND m.deleted_at IS NULL
            WHERE d.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date = @FlowDate
              AND d.deleted_at IS NULL
            GROUP BY f.timezone
            """,
            new { TenantId = tenantId, FamilyId = familyId, FlowDate = flowDate });
    }

    public async Task<int> CountBeautifulDayStreakAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly today,
        string timezone,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var from = today.AddDays(-60);
        var rows = await conn.QueryAsync<StreakDayRow>(
            """
            SELECT
                d.flow_date AS FlowDate,
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child'
                )::int AS ChildTotal,
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child' AND c.status IN ('pending', 'in_progress')
                )::int AS ChildOpen,
                COUNT(*) FILTER (
                    WHERE m.role_code = 'child'
                      AND c.status = 'done'
                      AND c.completed_at IS NOT NULL
                      AND c.window_end IS NOT NULL
                      AND (c.completed_at AT TIME ZONE @Timezone)::time > c.window_end
                )::int AS ChildLateDone,
                (
                    SELECT COUNT(*)::int
                    FROM pack_family.consequence_event ce
                    WHERE ce.tenant_id = d.tenant_id
                      AND ce.family_id = d.family_id
                      AND ce.flow_date = d.flow_date
                      AND ce.deleted_at IS NULL
                      AND ce.status = 'applied'
                ) AS AppliedConsequences
            FROM pack_family.day_flow d
            LEFT JOIN pack_family.commitment c
              ON c.day_flow_id = d.id AND c.tenant_id = d.tenant_id AND c.deleted_at IS NULL
            LEFT JOIN pack_family.membership m
              ON m.id = c.member_id AND m.tenant_id = c.tenant_id AND m.deleted_at IS NULL
            WHERE d.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date BETWEEN @From AND @Today
              AND d.deleted_at IS NULL
            GROUP BY d.tenant_id, d.family_id, d.flow_date
            ORDER BY d.flow_date DESC
            """,
            new
            {
                TenantId = tenantId,
                FamilyId = familyId,
                From = from,
                Today = today,
                Timezone = timezone,
            });

        var streak = 0;
        var byDate = rows.ToDictionary(r => r.FlowDate);
        for (var d = today; d >= from; d = d.AddDays(-1))
        {
            if (!byDate.TryGetValue(d, out var row) || row.ChildTotal <= 0)
            {
                if (d == today)
                    continue;
                continue;
            }

            var beautiful = row.ChildOpen == 0
                && row.AppliedConsequences == 0
                && row.ChildLateDone == 0;
            if (!beautiful)
                break;
            streak++;
        }

        return streak;
    }

    public async Task<IReadOnlyList<SubscriptionRow>> ListSubscriptionsForFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SubscriptionRow>(
            """
            SELECT
                s.id AS Id,
                s.tenant_id AS TenantId,
                s.family_id AS FamilyId,
                s.membership_id AS MembershipId,
                s.endpoint AS Endpoint,
                s.p256dh AS P256dh,
                s.auth AS Auth
            FROM pack_family.parent_push_subscription s
            WHERE s.family_id = @FamilyId
              AND s.deleted_at IS NULL
            """,
            new { FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<bool> TryInsertDispatchAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        string kind,
        Guid? commitmentId,
        string? summary,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            var rows = await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.reminder_dispatch (
                    tenant_id, family_id, flow_date, kind, commitment_id, payload_summary
                )
                VALUES (@TenantId, @FamilyId, @FlowDate, @Kind, @CommitmentId, @Summary)
                """,
                new
                {
                    TenantId = tenantId,
                    FamilyId = familyId,
                    FlowDate = flowDate,
                    Kind = kind,
                    CommitmentId = commitmentId,
                    Summary = summary,
                });
            return rows > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return false;
        }
    }

    /// <summary>
    /// True if family already got an alert-class push today (cap ≈ 1/day: voice / due / digest).
    /// Surprises (gratitude, milestones) are excluded.
    /// </summary>
    public async Task<bool> HasAlertDispatchTodayAsync(
        Guid tenantId,
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_family.reminder_dispatch d
                WHERE d.tenant_id = @TenantId
                  AND d.family_id = @FamilyId
                  AND d.flow_date = @FlowDate
                  AND d.kind IN (
                      'due_now',
                      'overdue',
                      'relationship_voice',
                      'approval_digest',
                      'evening_digest'
                  )
            )
            """,
            new { TenantId = tenantId, FamilyId = familyId, FlowDate = flowDate });
    }

    /// <summary>
    /// Unread human voice (status=sent) for any recipient — parent↔parent hoặc bố mẹ→con.
    /// Cap 1 alert/family/day still applied by DispatchRelationshipVoiceAsync.
    /// </summary>
    public async Task<IReadOnlyList<UnreadVoiceRow>> ListUnreadParentVoicesAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<UnreadVoiceRow>(
            """
            SELECT
                v.id AS MessageId,
                v.tenant_id AS TenantId,
                v.family_id AS FamilyId,
                v.flow_date AS FlowDate,
                f.timezone AS Timezone,
                v.to_member_id AS ToMemberId,
                fm.display_name AS FromMemberName,
                LEFT(v.body_vi, 140) AS BodyPreview
            FROM pack_family.parent_voice_message v
            INNER JOIN pack_family.family f
              ON f.id = v.family_id AND f.tenant_id = v.tenant_id
             AND f.deleted_at IS NULL AND f.status = 'active'
            INNER JOIN pack_family.membership fm
              ON fm.id = v.from_member_id AND fm.tenant_id = v.tenant_id AND fm.deleted_at IS NULL
            INNER JOIN pack_family.membership tm
              ON tm.id = v.to_member_id AND tm.tenant_id = v.tenant_id AND tm.deleted_at IS NULL
            WHERE v.deleted_at IS NULL
              AND v.status = 'sent'
              AND tm.role_code IN ('guardian', 'caregiver', 'child')
            ORDER BY v.sent_at DESC
            LIMIT 200
            """);
        return rows.AsList();
    }

    internal sealed class UnreadVoiceRow
    {
        public Guid MessageId { get; init; }
        public Guid TenantId { get; init; }
        public Guid FamilyId { get; init; }
        public DateOnly FlowDate { get; init; }
        public string Timezone { get; init; } = "Asia/Ho_Chi_Minh";
        public Guid ToMemberId { get; init; }
        public string? FromMemberName { get; init; }
        public string BodyPreview { get; init; } = "";
    }

    internal sealed class SubscriptionRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid FamilyId { get; init; }
        public Guid MembershipId { get; init; }
        public string Endpoint { get; init; } = "";
        public string P256dh { get; init; } = "";
        public string Auth { get; init; } = "";
    }

    internal sealed class OpenCommitmentRow
    {
        public Guid CommitmentId { get; init; }
        public Guid TenantId { get; init; }
        public Guid FamilyId { get; init; }
        public DateOnly FlowDate { get; init; }
        public string Timezone { get; init; } = "Asia/Ho_Chi_Minh";
        public string FamilyName { get; init; } = "";
        public string Title { get; init; } = "";
        public string Status { get; init; } = "";
        public TimeOnly? WindowStart { get; init; }
        public TimeOnly? WindowEnd { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public string? MemberRole { get; init; }
        public string? HabitStage { get; init; }
        public int HabitStreakDays { get; init; }
        public bool ReminderSuppressed { get; init; }
    }

    internal sealed class DigestItemRow
    {
        public Guid CommitmentId { get; init; }
        public string Title { get; init; } = "";
        public string Status { get; init; } = "";
        public TimeOnly? WindowStart { get; init; }
        public TimeOnly? WindowEnd { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public string? MemberName { get; init; }
    }

    internal sealed class FamilyClockRow
    {
        public Guid FamilyId { get; init; }
        public Guid TenantId { get; init; }
        public string Timezone { get; init; } = "Asia/Ho_Chi_Minh";
        public string DisplayName { get; init; } = "";
    }

    internal sealed class PendingApprovalRow
    {
        public Guid CommitmentId { get; init; }
        public string Title { get; init; } = "";
        public string? EvidenceUrl { get; init; }
        public int? PendingStarDelta { get; init; }
        public string? MemberName { get; init; }
    }

    internal sealed class DayAggRow
    {
        public int ChildDone { get; init; }
        public int ChildSkipped { get; init; }
        public int ChildOpen { get; init; }
        public int ChildLateDone { get; init; }
        public int AppliedConsequences { get; init; }
        public int ChildTotal => ChildDone + ChildSkipped + ChildOpen;
    }

    internal sealed class StreakDayRow
    {
        public DateOnly FlowDate { get; init; }
        public int ChildTotal { get; init; }
        public int ChildOpen { get; init; }
        public int ChildLateDone { get; init; }
        public int AppliedConsequences { get; init; }
    }
}
