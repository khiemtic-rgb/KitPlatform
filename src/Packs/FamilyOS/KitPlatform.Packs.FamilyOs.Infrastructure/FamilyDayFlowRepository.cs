using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyDayFlowRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyDayFlowRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<DayFlowRow?> GetByDateAsync(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DayFlowRow>(
            """
            SELECT
                d.id AS Id,
                d.family_id AS FamilyId,
                d.routine_id AS RoutineId,
                r.display_name AS RoutineName,
                d.flow_date AS FlowDate,
                d.status AS Status
            FROM pack_family.day_flow d
            INNER JOIN pack_family.routine r ON r.id = d.routine_id
            WHERE d.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date = @FlowDate
              AND d.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, FlowDate = flowDate });
    }

    public async Task<RoutinePickRow?> PickRoutineForDateAsync(
        Guid familyId,
        DateOnly flowDate,
        Guid? preferredRoutineId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        if (preferredRoutineId is Guid rid)
        {
            return await conn.QuerySingleOrDefaultAsync<RoutinePickRow>(
                """
                SELECT id AS Id, display_name AS DisplayName
                FROM pack_family.routine
                WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @RoutineId
                  AND deleted_at IS NULL AND is_active
                """,
                new { TenantId, FamilyId = familyId, RoutineId = rid });
        }

        // ISO: Monday=1 … Sunday=7
        var isoDow = flowDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)flowDate.DayOfWeek;

        var matched = await conn.QuerySingleOrDefaultAsync<RoutinePickRow>(
            """
            SELECT id AS Id, display_name AS DisplayName
            FROM pack_family.routine
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND is_active
              AND @Dow = ANY (weekdays)
            ORDER BY sort_order, created_at
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, Dow = (short)isoDow });

        if (matched is not null) return matched;

        return await conn.QuerySingleOrDefaultAsync<RoutinePickRow>(
            """
            SELECT id AS Id, display_name AS DisplayName
            FROM pack_family.routine
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND is_active
            ORDER BY sort_order, created_at
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<(DayFlowRow Flow, bool Created)> CreateDayFlowWithCommitmentsAsync(
        Guid familyId,
        Guid routineId,
        string routineName,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var existing = await conn.QuerySingleOrDefaultAsync<DayFlowRow>(
            """
            SELECT
                d.id AS Id,
                d.family_id AS FamilyId,
                d.routine_id AS RoutineId,
                @RoutineName AS RoutineName,
                d.flow_date AS FlowDate,
                d.status AS Status
            FROM pack_family.day_flow d
            WHERE d.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date = @FlowDate
              AND d.deleted_at IS NULL
            FOR UPDATE
            """,
            new { TenantId, FamilyId = familyId, FlowDate = flowDate, RoutineName = routineName },
            tx);

        if (existing is not null)
        {
            await tx.CommitAsync(cancellationToken);
            return (existing with { RoutineName = routineName }, false);
        }

        var flowId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.day_flow (tenant_id, family_id, routine_id, flow_date)
            VALUES (@TenantId, @FamilyId, @RoutineId, @FlowDate)
            RETURNING id
            """,
            new { TenantId, FamilyId = familyId, RoutineId = routineId, FlowDate = flowDate },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.commitment (
                tenant_id, day_flow_id, template_id, member_id, title, description,
                window_start, window_end, sort_order,
                priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
                allow_early_complete, early_lead_minutes, on_time_grace_minutes, star_reward
            )
            SELECT
                @TenantId,
                @DayFlowId,
                t.id,
                t.member_id,
                t.title,
                t.description,
                t.window_start,
                t.window_end,
                t.sort_order,
                t.priority,
                t.expected_duration_minutes,
                t.context_anchor,
                t.depends_on_template_ids,
                t.allow_early_complete,
                t.early_lead_minutes,
                t.on_time_grace_minutes,
                t.star_reward
            FROM pack_family.commitment_template t
            WHERE t.tenant_id = @TenantId
              AND t.routine_id = @RoutineId
              AND t.deleted_at IS NULL
              AND t.is_active
            ORDER BY t.sort_order, t.created_at
            """,
            new { TenantId, DayFlowId = flowId, RoutineId = routineId },
            tx);

        await tx.CommitAsync(cancellationToken);

        return (
            new DayFlowRow
            {
                Id = flowId,
                FamilyId = familyId,
                RoutineId = routineId,
                RoutineName = routineName,
                FlowDate = flowDate,
                Status = "open",
            },
            true);
    }

    public async Task<IReadOnlyList<CommitmentRow>> ListCommitmentsAsync(
        Guid dayFlowId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CommitmentRow>(
            """
            SELECT
                c.id AS Id,
                c.day_flow_id AS DayFlowId,
                c.template_id AS TemplateId,
                c.member_id AS MemberId,
                m.display_name AS MemberName,
                c.title AS Title,
                c.description AS Description,
                c.window_start AS WindowStart,
                c.window_end AS WindowEnd,
                c.sort_order AS SortOrder,
                c.status AS Status,
                c.skip_reason AS SkipReason,
                c.completed_at AS CompletedAt,
                c.priority AS Priority,
                c.expected_duration_minutes AS ExpectedDurationMinutes,
                c.context_anchor AS ContextAnchor,
                c.depends_on_template_ids AS DependsOnTemplateIds,
                c.evidence_url AS EvidenceUrl,
                c.evidence_uploaded_at AS EvidenceUploadedAt,
                c.allow_early_complete AS AllowEarlyComplete,
                c.early_lead_minutes AS EarlyLeadMinutes,
                c.on_time_grace_minutes AS OnTimeGraceMinutes,
                c.star_reward AS StarReward,
                c.pending_star_delta AS PendingStarDelta,
                c.pending_star_tier AS PendingStarTier,
                c.pending_star_late_minutes AS PendingStarLateMinutes,
                c.star_computed_at AS StarComputedAt,
                c.star_posted_at AS StarPostedAt
            FROM pack_family.commitment c
            LEFT JOIN pack_family.membership m ON m.id = c.member_id
            WHERE c.tenant_id = @TenantId
              AND c.day_flow_id = @DayFlowId
              AND c.deleted_at IS NULL
            ORDER BY c.sort_order, c.created_at
            """,
            new { TenantId, DayFlowId = dayFlowId });
        return rows.AsList();
    }

    public async Task<CommitmentRow?> GetCommitmentForFamilyAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CommitmentRow>(
            """
            SELECT
                c.id AS Id,
                c.day_flow_id AS DayFlowId,
                c.template_id AS TemplateId,
                c.member_id AS MemberId,
                m.display_name AS MemberName,
                c.title AS Title,
                c.description AS Description,
                c.window_start AS WindowStart,
                c.window_end AS WindowEnd,
                c.sort_order AS SortOrder,
                c.status AS Status,
                c.skip_reason AS SkipReason,
                c.completed_at AS CompletedAt,
                d.flow_date AS FlowDate,
                c.priority AS Priority,
                c.expected_duration_minutes AS ExpectedDurationMinutes,
                c.context_anchor AS ContextAnchor,
                c.depends_on_template_ids AS DependsOnTemplateIds,
                c.evidence_url AS EvidenceUrl,
                c.evidence_uploaded_at AS EvidenceUploadedAt,
                c.allow_early_complete AS AllowEarlyComplete,
                c.early_lead_minutes AS EarlyLeadMinutes,
                c.on_time_grace_minutes AS OnTimeGraceMinutes,
                c.star_reward AS StarReward,
                c.pending_star_delta AS PendingStarDelta,
                c.pending_star_tier AS PendingStarTier,
                c.pending_star_late_minutes AS PendingStarLateMinutes,
                c.star_computed_at AS StarComputedAt,
                c.star_posted_at AS StarPostedAt
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d ON d.id = c.day_flow_id
            LEFT JOIN pack_family.membership m ON m.id = c.member_id
            WHERE c.tenant_id = @TenantId
              AND c.id = @CommitmentId
              AND d.family_id = @FamilyId
              AND c.deleted_at IS NULL
              AND d.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, CommitmentId = commitmentId });
    }

    public async Task<CommitmentRow?> UpdateCommitmentStatusAsync(
        Guid commitmentId,
        string status,
        string? skipReason,
        string? evidenceUrl,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CommitmentRow>(
            """
            UPDATE pack_family.commitment
            SET status = @Status,
                skip_reason = @SkipReason,
                completed_at = CASE
                    WHEN @Status = 'done' THEN NOW()
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE completed_at
                END,
                pending_star_delta = CASE
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE pending_star_delta
                END,
                pending_star_tier = CASE
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE pending_star_tier
                END,
                pending_star_late_minutes = CASE
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE pending_star_late_minutes
                END,
                star_computed_at = CASE
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE star_computed_at
                END,
                star_posted_at = CASE
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE star_posted_at
                END,
                evidence_url = CASE
                    WHEN @Status = 'done' AND @EvidenceUrl IS NOT NULL AND btrim(@EvidenceUrl) <> ''
                        THEN btrim(@EvidenceUrl)
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE evidence_url
                END,
                evidence_uploaded_at = CASE
                    WHEN @Status = 'done' AND @EvidenceUrl IS NOT NULL AND btrim(@EvidenceUrl) <> ''
                        THEN NOW()
                    WHEN @Status IN ('pending', 'in_progress', 'skipped') THEN NULL
                    ELSE evidence_uploaded_at
                END,
                updated_at = NOW()
            WHERE tenant_id = @TenantId AND id = @CommitmentId AND deleted_at IS NULL
            RETURNING
                id AS Id,
                day_flow_id AS DayFlowId,
                template_id AS TemplateId,
                member_id AS MemberId,
                NULL::text AS MemberName,
                title AS Title,
                description AS Description,
                window_start AS WindowStart,
                window_end AS WindowEnd,
                sort_order AS SortOrder,
                status AS Status,
                skip_reason AS SkipReason,
                completed_at AS CompletedAt,
                priority AS Priority,
                expected_duration_minutes AS ExpectedDurationMinutes,
                context_anchor AS ContextAnchor,
                depends_on_template_ids AS DependsOnTemplateIds,
                evidence_url AS EvidenceUrl,
                evidence_uploaded_at AS EvidenceUploadedAt,
                allow_early_complete AS AllowEarlyComplete,
                early_lead_minutes AS EarlyLeadMinutes,
                on_time_grace_minutes AS OnTimeGraceMinutes,
                star_reward AS StarReward,
                pending_star_delta AS PendingStarDelta,
                pending_star_tier AS PendingStarTier,
                pending_star_late_minutes AS PendingStarLateMinutes,
                star_computed_at AS StarComputedAt,
                star_posted_at AS StarPostedAt
            """,
            new
            {
                TenantId,
                CommitmentId = commitmentId,
                Status = status,
                SkipReason = skipReason,
                EvidenceUrl = evidenceUrl,
            });
    }

    public async Task SetPendingStarsAsync(
        Guid commitmentId,
        int delta,
        string tier,
        int? lateMinutes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment
            SET pending_star_delta = @Delta,
                pending_star_tier = @Tier,
                pending_star_late_minutes = @LateMinutes,
                star_computed_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId AND id = @CommitmentId AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                CommitmentId = commitmentId,
                Delta = delta,
                Tier = tier,
                LateMinutes = lateMinutes,
            });
    }

    public async Task MarkStarsPostedAsync(
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment
            SET star_posted_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId AND id = @CommitmentId AND deleted_at IS NULL
            """,
            new { TenantId, CommitmentId = commitmentId });
    }

    public async Task ClearPendingStarsAsync(
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment
            SET pending_star_delta = NULL,
                pending_star_tier = NULL,
                pending_star_late_minutes = NULL,
                star_computed_at = NULL,
                star_posted_at = NULL,
                updated_at = NOW()
            WHERE tenant_id = @TenantId AND id = @CommitmentId AND deleted_at IS NULL
            """,
            new { TenantId, CommitmentId = commitmentId });
    }

    internal sealed record DayFlowRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid RoutineId { get; init; }
        public string RoutineName { get; init; } = "";
        public DateOnly FlowDate { get; init; }
        public string Status { get; init; } = "";
    }

    internal sealed class RoutinePickRow
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = "";
    }

    internal sealed class CommitmentRow
    {
        public Guid Id { get; init; }
        public Guid DayFlowId { get; init; }
        public Guid? TemplateId { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public TimeOnly? WindowStart { get; init; }
        public TimeOnly? WindowEnd { get; init; }
        public int SortOrder { get; init; }
        public string Status { get; init; } = "";
        public string? SkipReason { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public DateOnly? FlowDate { get; init; }
        public string Priority { get; init; } = FamilyCommitmentPriorities.Normal;
        public int? ExpectedDurationMinutes { get; init; }
        public string? ContextAnchor { get; init; }
        public Guid[]? DependsOnTemplateIds { get; init; }
        public string? EvidenceUrl { get; init; }
        public DateTimeOffset? EvidenceUploadedAt { get; init; }
        public bool AllowEarlyComplete { get; init; }
        public int EarlyLeadMinutes { get; init; }
        public int OnTimeGraceMinutes { get; init; }
        public int StarReward { get; init; }
        public int? PendingStarDelta { get; init; }
        public string? PendingStarTier { get; init; }
        public int? PendingStarLateMinutes { get; init; }
        public DateTimeOffset? StarComputedAt { get; init; }
        public DateTimeOffset? StarPostedAt { get; init; }
    }
}
