using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyBehaviorRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyBehaviorRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<TemplateHabitRow?> GetTemplateHabitAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TemplateHabitRow>(
            """
            SELECT
                id AS Id,
                habit_stage AS HabitStage,
                habit_streak_days AS HabitStreakDays,
                habit_last_done_date AS HabitLastDoneDate,
                reminder_suppressed AS ReminderSuppressed,
                habit_stage_changed_at AS HabitStageChangedAt
            FROM pack_family.commitment_template
            WHERE tenant_id = @TenantId
              AND id = @TemplateId
              AND deleted_at IS NULL
            """,
            new { TenantId, TemplateId = templateId });
    }

    public async Task UpdateTemplateHabitAsync(
        Guid templateId,
        string habitStage,
        int streakDays,
        DateOnly? lastDoneDate,
        bool reminderSuppressed,
        DateTimeOffset? stageChangedAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment_template
            SET habit_stage = @HabitStage,
                habit_streak_days = @StreakDays,
                habit_last_done_date = @LastDoneDate,
                reminder_suppressed = @ReminderSuppressed,
                habit_stage_changed_at = COALESCE(@StageChangedAt, habit_stage_changed_at),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND id = @TemplateId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                TemplateId = templateId,
                HabitStage = habitStage,
                StreakDays = streakDays,
                LastDoneDate = lastDoneDate,
                ReminderSuppressed = reminderSuppressed,
                StageChangedAt = stageChangedAt,
            });
    }

    public async Task SyncCommitmentHabitSnapshotAsync(
        Guid commitmentId,
        string habitStage,
        bool reminderSuppressed,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment
            SET habit_stage = @HabitStage,
                reminder_suppressed = @ReminderSuppressed,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND id = @CommitmentId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                CommitmentId = commitmentId,
                HabitStage = habitStage,
                ReminderSuppressed = reminderSuppressed,
            });
    }

    public async Task InsertBehaviorEventAsync(
        Guid familyId,
        Guid? memberId,
        string eventType,
        Guid? commitmentId,
        Guid? templateId,
        object? payload,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var json = payload is null
            ? "{}"
            : JsonSerializer.Serialize(payload);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.behavior_event (
                tenant_id, family_id, member_id, event_type,
                commitment_id, template_id, payload_json
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @EventType,
                @CommitmentId, @TemplateId, CAST(@Payload AS jsonb)
            )
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                EventType = eventType,
                CommitmentId = commitmentId,
                TemplateId = templateId,
                Payload = json,
            });
    }

    public async Task<ReflectionRow?> GetReflectionAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ReflectionRow>(
            """
            SELECT
                id AS Id,
                family_id AS FamilyId,
                commitment_id AS CommitmentId,
                member_id AS MemberId,
                prompt_code AS PromptCode,
                answer_text AS AnswerText,
                created_at AS CreatedAt
            FROM pack_family.commitment_reflection
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND commitment_id = @CommitmentId
            """,
            new { TenantId, FamilyId = familyId, CommitmentId = commitmentId });
    }

    public async Task<ReflectionRow> InsertReflectionAsync(
        Guid familyId,
        Guid commitmentId,
        Guid? memberId,
        string promptCode,
        string answerText,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<ReflectionRow>(
            """
            INSERT INTO pack_family.commitment_reflection (
                tenant_id, family_id, commitment_id, member_id, prompt_code, answer_text
            )
            VALUES (
                @TenantId, @FamilyId, @CommitmentId, @MemberId, @PromptCode, @AnswerText
            )
            ON CONFLICT (commitment_id) DO UPDATE SET
                prompt_code = EXCLUDED.prompt_code,
                answer_text = EXCLUDED.answer_text
            RETURNING
                id AS Id,
                family_id AS FamilyId,
                commitment_id AS CommitmentId,
                member_id AS MemberId,
                prompt_code AS PromptCode,
                answer_text AS AnswerText,
                created_at AS CreatedAt
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                CommitmentId = commitmentId,
                MemberId = memberId,
                PromptCode = promptCode,
                AnswerText = answerText,
            });
    }

    public async Task<CommitmentContextRow?> GetCommitmentContextAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CommitmentContextRow>(
            """
            SELECT
                c.id AS CommitmentId,
                c.template_id AS TemplateId,
                c.member_id AS MemberId,
                c.status AS Status,
                c.title AS Title,
                c.evidence_url AS EvidenceUrl,
                c.evidence_level AS EvidenceLevel,
                c.confidence_score AS ConfidenceScore,
                d.flow_date AS FlowDate,
                COALESCE(t.habit_stage, c.habit_stage, 'new') AS HabitStage,
                COALESCE(t.habit_streak_days, 0) AS HabitStreakDays,
                COALESCE(t.reminder_suppressed, c.reminder_suppressed, FALSE) AS ReminderSuppressed,
                (r.id IS NOT NULL) AS HasReflection,
                (q.id IS NOT NULL) AS HasRetrievalCheck,
                q.method_answer AS MethodAnswer,
                q.recall_answer AS RecallAnswer,
                COALESCE(q.illusion_risk, FALSE) AS IllusionRisk
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d ON d.id = c.day_flow_id
            LEFT JOIN pack_family.commitment_template t ON t.id = c.template_id
            LEFT JOIN pack_family.commitment_reflection r ON r.commitment_id = c.id
            LEFT JOIN pack_family.commitment_retrieval_check q ON q.commitment_id = c.id
            WHERE c.tenant_id = @TenantId
              AND c.id = @CommitmentId
              AND d.family_id = @FamilyId
              AND c.deleted_at IS NULL
              AND d.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, CommitmentId = commitmentId });
    }

    public async Task UpdateCommitmentConfidenceAsync(
        Guid commitmentId,
        int evidenceLevel,
        int confidenceScore,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment
            SET evidence_level = @EvidenceLevel,
                confidence_score = @ConfidenceScore,
                confidence_updated_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND id = @CommitmentId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                CommitmentId = commitmentId,
                EvidenceLevel = evidenceLevel,
                ConfidenceScore = confidenceScore,
            });
    }

    public async Task<RetrievalCheckRow?> GetRetrievalCheckAsync(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<RetrievalCheckRow>(
            """
            SELECT
                id AS Id,
                family_id AS FamilyId,
                commitment_id AS CommitmentId,
                member_id AS MemberId,
                method_answer AS MethodAnswer,
                recall_answer AS RecallAnswer,
                illusion_risk AS IllusionRisk,
                created_at AS CreatedAt
            FROM pack_family.commitment_retrieval_check
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND commitment_id = @CommitmentId
            """,
            new { TenantId, FamilyId = familyId, CommitmentId = commitmentId });
    }

    public async Task<RetrievalCheckRow> InsertRetrievalCheckAsync(
        Guid familyId,
        Guid commitmentId,
        Guid? memberId,
        string methodAnswer,
        string recallAnswer,
        bool illusionRisk,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<RetrievalCheckRow>(
            """
            INSERT INTO pack_family.commitment_retrieval_check (
                tenant_id, family_id, commitment_id, member_id,
                method_answer, recall_answer, illusion_risk
            )
            VALUES (
                @TenantId, @FamilyId, @CommitmentId, @MemberId,
                @MethodAnswer, @RecallAnswer, @IllusionRisk
            )
            ON CONFLICT (commitment_id) DO UPDATE SET
                method_answer = EXCLUDED.method_answer,
                recall_answer = EXCLUDED.recall_answer,
                illusion_risk = EXCLUDED.illusion_risk
            RETURNING
                id AS Id,
                family_id AS FamilyId,
                commitment_id AS CommitmentId,
                member_id AS MemberId,
                method_answer AS MethodAnswer,
                recall_answer AS RecallAnswer,
                illusion_risk AS IllusionRisk,
                created_at AS CreatedAt
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                CommitmentId = commitmentId,
                MemberId = memberId,
                MethodAnswer = methodAnswer,
                RecallAnswer = recallAnswer,
                IllusionRisk = illusionRisk,
            });
    }

    public async Task<MemberWindowStatsRow> GetMemberWindowStatsAsync(
        Guid familyId,
        Guid memberId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<MemberWindowStatsRow>(
            """
            SELECT
                COALESCE(SUM(CASE WHEN c.status IN ('pending','in_progress') THEN 1 ELSE 0 END), 0)::int AS OpenCount,
                COALESCE(SUM(CASE WHEN c.status = 'done' THEN 1 ELSE 0 END), 0)::int AS DoneCount,
                COALESCE(SUM(CASE WHEN c.status = 'skipped' THEN 1 ELSE 0 END), 0)::int AS SkippedCount,
                COALESCE(SUM(CASE WHEN c.status = 'done' AND c.confidence_score IS NOT NULL THEN c.confidence_score ELSE 0 END), 0)::float AS ConfidenceSum,
                COALESCE(SUM(CASE WHEN c.status = 'done' AND c.confidence_score IS NOT NULL THEN 1 ELSE 0 END), 0)::int AS ConfidenceN,
                COALESCE(SUM(CASE
                    WHEN c.status = 'skipped'
                     AND COALESCE(c.window_end, c.window_start) IS NOT NULL
                     AND EXTRACT(HOUR FROM COALESCE(c.window_end, c.window_start)) >= 17
                    THEN 1 ELSE 0 END), 0)::int AS EveningSkipCount,
                COALESCE(SUM(CASE
                    WHEN c.status IN ('pending','in_progress')
                     AND COALESCE(c.window_end, c.window_start) IS NOT NULL
                     AND EXTRACT(HOUR FROM COALESCE(c.window_end, c.window_start)) >= 17
                    THEN 1 ELSE 0 END), 0)::int AS EveningOpenCount,
                COALESCE(MAX(COALESCE(t.habit_streak_days, 0)), 0)::int AS MaxHabitStreak,
                COALESCE(BOOL_OR(COALESCE(t.habit_stage, c.habit_stage, 'new') IN ('autonomous','maintained')), FALSE) AS AnyAutonomous
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d ON d.id = c.day_flow_id
            LEFT JOIN pack_family.commitment_template t ON t.id = c.template_id
            WHERE c.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND c.member_id = @MemberId
              AND d.flow_date >= @FromDate
              AND d.flow_date <= @ToDate
              AND c.deleted_at IS NULL
              AND d.deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FromDate = fromDate,
                ToDate = toDate,
            });
    }

    public async Task<EventCountsRow> CountMemberEventsAsync(
        Guid familyId,
        Guid memberId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<EventCountsRow>(
            """
            SELECT
                COALESCE(SUM(CASE WHEN event_type = 'self_start' THEN 1 ELSE 0 END), 0)::int AS SelfStartCount,
                COALESCE(SUM(CASE WHEN event_type = 'reflection_submitted' THEN 1 ELSE 0 END), 0)::int AS ReflectionCount,
                COALESCE(SUM(CASE WHEN event_type = 'retrieval_submitted' THEN 1 ELSE 0 END), 0)::int AS RetrievalCount,
                COALESCE(SUM(CASE WHEN event_type = 'parent_nudge' THEN 1 ELSE 0 END), 0)::int AS ParentNudgeCount
            FROM pack_family.behavior_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND occurred_at >= @FromUtc
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FromUtc = fromUtc,
            });
    }

    public async Task UpsertTwinSnapshotAsync(
        Guid familyId,
        Guid memberId,
        DateOnly snapshotDate,
        int overallScore,
        string overallLabel,
        string dimensionsJson,
        string? eveningRiskBand,
        string eveningReasonsJson,
        string disclaimerVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.behavior_twin_snapshot (
                tenant_id, family_id, member_id, snapshot_date,
                overall_score, overall_label, dimensions_json,
                evening_risk_band, evening_reasons_json, disclaimer_vi
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @SnapshotDate,
                @OverallScore, @OverallLabel, CAST(@DimensionsJson AS jsonb),
                @EveningRiskBand, CAST(@EveningReasonsJson AS jsonb), @DisclaimerVi
            )
            ON CONFLICT (family_id, member_id, snapshot_date) DO UPDATE SET
                overall_score = EXCLUDED.overall_score,
                overall_label = EXCLUDED.overall_label,
                dimensions_json = EXCLUDED.dimensions_json,
                evening_risk_band = EXCLUDED.evening_risk_band,
                evening_reasons_json = EXCLUDED.evening_reasons_json,
                disclaimer_vi = EXCLUDED.disclaimer_vi,
                updated_at = NOW()
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                SnapshotDate = snapshotDate,
                OverallScore = overallScore,
                OverallLabel = overallLabel,
                DimensionsJson = dimensionsJson,
                EveningRiskBand = eveningRiskBand,
                EveningReasonsJson = eveningReasonsJson,
                DisclaimerVi = disclaimerVi,
            });
    }

    public async Task<RetirementPolicyRow?> GetRetirementPolicyAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<RetirementPolicyRow>(
            """
            SELECT
                observe_only AS ObserveOnly,
                retirement_stage AS RetirementStage,
                parent_nudge_budget AS ParentNudgeBudget,
                notes_vi AS NotesVi,
                updated_at AS UpdatedAt
            FROM pack_family.behavior_retirement_policy
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<RetirementPolicyRow> UpsertRetirementPolicyAsync(
        Guid familyId,
        bool observeOnly,
        string? retirementStage,
        int? parentNudgeBudget,
        string? notesVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<RetirementPolicyRow>(
            """
            INSERT INTO pack_family.behavior_retirement_policy (
                tenant_id, family_id, observe_only, retirement_stage,
                parent_nudge_budget, notes_vi
            )
            VALUES (
                @TenantId, @FamilyId, @ObserveOnly, @RetirementStage,
                @ParentNudgeBudget, @NotesVi
            )
            ON CONFLICT (family_id) DO UPDATE SET
                observe_only = EXCLUDED.observe_only,
                retirement_stage = COALESCE(EXCLUDED.retirement_stage, pack_family.behavior_retirement_policy.retirement_stage),
                parent_nudge_budget = EXCLUDED.parent_nudge_budget,
                notes_vi = EXCLUDED.notes_vi,
                updated_at = NOW()
            RETURNING
                observe_only AS ObserveOnly,
                retirement_stage AS RetirementStage,
                parent_nudge_budget AS ParentNudgeBudget,
                notes_vi AS NotesVi,
                updated_at AS UpdatedAt
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ObserveOnly = observeOnly,
                RetirementStage = retirementStage,
                ParentNudgeBudget = parentNudgeBudget,
                NotesVi = notesVi,
            });
    }

    public async Task<int> CountFamilyParentNudgesAsync(
        Guid familyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        return await CountFamilyEventsAsync(
            familyId, FamilyBehaviorEventTypes.ParentNudge, fromUtc, toUtc, cancellationToken);
    }

    public async Task<int> CountFamilyEventsAsync(
        Guid familyId,
        string eventType,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(COUNT(*), 0)::int
            FROM pack_family.behavior_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND event_type = @EventType
              AND occurred_at >= @FromUtc
              AND occurred_at < @ToUtc
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                EventType = eventType,
                FromUtc = fromUtc,
                ToUtc = toUtc,
            });
    }

    public async Task<WeekPlaybookRow?> GetWeekPlaybookAsync(
        Guid familyId,
        Guid? memberId,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<WeekPlaybookRow>(
            """
            SELECT
                id AS Id,
                family_id AS FamilyId,
                member_id AS MemberId,
                week_start AS WeekStart,
                pattern_code AS PatternCode,
                tactic_code AS TacticCode,
                last_failed_tactic AS LastFailedTactic,
                parent_strategy_tip_vi AS ParentStrategyTipVi,
                child_voice_json::text AS ChildVoiceJson,
                child_voice_at AS ChildVoiceAt,
                updated_at AS UpdatedAt
            FROM pack_family.behavior_week_playbook
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND week_start = @WeekStart
              AND (
                    (@MemberId IS NULL AND member_id IS NULL)
                    OR member_id = @MemberId
                  )
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, WeekStart = weekStart });
    }

    public async Task<WeekPlaybookRow> UpsertWeekPlaybookAsync(
        Guid familyId,
        Guid? memberId,
        DateOnly weekStart,
        string? patternCode,
        string? tacticCode,
        string? lastFailedTactic,
        string? parentStrategyTipVi,
        string? childVoiceJson,
        DateTimeOffset? childVoiceAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<WeekPlaybookRow>(
            """
            SELECT
                id AS Id,
                family_id AS FamilyId,
                member_id AS MemberId,
                week_start AS WeekStart,
                pattern_code AS PatternCode,
                tactic_code AS TacticCode,
                last_failed_tactic AS LastFailedTactic,
                parent_strategy_tip_vi AS ParentStrategyTipVi,
                child_voice_json::text AS ChildVoiceJson,
                child_voice_at AS ChildVoiceAt,
                updated_at AS UpdatedAt
            FROM pack_family.behavior_week_playbook
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND week_start = @WeekStart
              AND (
                    (@MemberId IS NULL AND member_id IS NULL)
                    OR member_id = @MemberId
                  )
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, WeekStart = weekStart });

        if (existing is null)
        {
            return await conn.QuerySingleAsync<WeekPlaybookRow>(
                """
                INSERT INTO pack_family.behavior_week_playbook (
                    tenant_id, family_id, member_id, week_start,
                    pattern_code, tactic_code, last_failed_tactic,
                    parent_strategy_tip_vi, child_voice_json, child_voice_at
                )
                VALUES (
                    @TenantId, @FamilyId, @MemberId, @WeekStart,
                    @PatternCode, @TacticCode, @LastFailedTactic,
                    @ParentStrategyTipVi,
                    CAST(COALESCE(@ChildVoiceJson, '{}') AS jsonb),
                    @ChildVoiceAt
                )
                RETURNING
                    id AS Id,
                    family_id AS FamilyId,
                    member_id AS MemberId,
                    week_start AS WeekStart,
                    pattern_code AS PatternCode,
                    tactic_code AS TacticCode,
                    last_failed_tactic AS LastFailedTactic,
                    parent_strategy_tip_vi AS ParentStrategyTipVi,
                    child_voice_json::text AS ChildVoiceJson,
                    child_voice_at AS ChildVoiceAt,
                    updated_at AS UpdatedAt
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    MemberId = memberId,
                    WeekStart = weekStart,
                    PatternCode = patternCode,
                    TacticCode = tacticCode,
                    LastFailedTactic = lastFailedTactic,
                    ParentStrategyTipVi = parentStrategyTipVi,
                    ChildVoiceJson = childVoiceJson,
                    ChildVoiceAt = childVoiceAt,
                });
        }

        return await conn.QuerySingleAsync<WeekPlaybookRow>(
            """
            UPDATE pack_family.behavior_week_playbook
            SET
                pattern_code = COALESCE(@PatternCode, pattern_code),
                tactic_code = COALESCE(@TacticCode, tactic_code),
                last_failed_tactic = COALESCE(@LastFailedTactic, last_failed_tactic),
                parent_strategy_tip_vi = COALESCE(@ParentStrategyTipVi, parent_strategy_tip_vi),
                child_voice_json = CASE
                    WHEN @ChildVoiceJson IS NULL THEN child_voice_json
                    ELSE CAST(@ChildVoiceJson AS jsonb)
                END,
                child_voice_at = COALESCE(@ChildVoiceAt, child_voice_at),
                updated_at = NOW()
            WHERE id = @Id
            RETURNING
                id AS Id,
                family_id AS FamilyId,
                member_id AS MemberId,
                week_start AS WeekStart,
                pattern_code AS PatternCode,
                tactic_code AS TacticCode,
                last_failed_tactic AS LastFailedTactic,
                parent_strategy_tip_vi AS ParentStrategyTipVi,
                child_voice_json::text AS ChildVoiceJson,
                child_voice_at AS ChildVoiceAt,
                updated_at AS UpdatedAt
            """,
            new
            {
                existing.Id,
                PatternCode = patternCode,
                TacticCode = tacticCode,
                LastFailedTactic = lastFailedTactic,
                ParentStrategyTipVi = parentStrategyTipVi,
                ChildVoiceJson = childVoiceJson,
                ChildVoiceAt = childVoiceAt,
            });
    }

    internal sealed class WeekPlaybookRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid? MemberId { get; init; }
        public DateOnly WeekStart { get; init; }
        public string? PatternCode { get; init; }
        public string? TacticCode { get; init; }
        public string? LastFailedTactic { get; init; }
        public string? ParentStrategyTipVi { get; init; }
        public string? ChildVoiceJson { get; init; }
        public DateTimeOffset? ChildVoiceAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    internal sealed class TemplateHabitRow
    {
        public Guid Id { get; init; }
        public string HabitStage { get; init; } = FamilyHabitStages.New;
        public int HabitStreakDays { get; init; }
        public DateOnly? HabitLastDoneDate { get; init; }
        public bool ReminderSuppressed { get; init; }
        public DateTimeOffset? HabitStageChangedAt { get; init; }
    }

    internal sealed class ReflectionRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid CommitmentId { get; init; }
        public Guid? MemberId { get; init; }
        public string PromptCode { get; init; } = "";
        public string AnswerText { get; init; } = "";
        public DateTimeOffset CreatedAt { get; init; }
    }

    internal sealed class RetrievalCheckRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid CommitmentId { get; init; }
        public Guid? MemberId { get; init; }
        public string MethodAnswer { get; init; } = "";
        public string RecallAnswer { get; init; } = "";
        public bool IllusionRisk { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    internal sealed class MemberWindowStatsRow
    {
        public int OpenCount { get; init; }
        public int DoneCount { get; init; }
        public int SkippedCount { get; init; }
        public double ConfidenceSum { get; init; }
        public int ConfidenceN { get; init; }
        public int EveningSkipCount { get; init; }
        public int EveningOpenCount { get; init; }
        public int MaxHabitStreak { get; init; }
        public bool AnyAutonomous { get; init; }
    }

    internal sealed class EventCountsRow
    {
        public int SelfStartCount { get; init; }
        public int ReflectionCount { get; init; }
        public int RetrievalCount { get; init; }
        public int ParentNudgeCount { get; init; }
    }

    internal sealed class RetirementPolicyRow
    {
        public bool ObserveOnly { get; init; }
        public string? RetirementStage { get; init; }
        public int? ParentNudgeBudget { get; init; }
        public string? NotesVi { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    internal sealed class CommitmentContextRow
    {
        public Guid CommitmentId { get; init; }
        public Guid? TemplateId { get; init; }
        public Guid? MemberId { get; init; }
        public string Status { get; init; } = "";
        public string Title { get; init; } = "";
        public string? EvidenceUrl { get; init; }
        public int EvidenceLevel { get; init; }
        public int? ConfidenceScore { get; init; }
        public DateOnly FlowDate { get; init; }
        public string HabitStage { get; init; } = FamilyHabitStages.New;
        public int HabitStreakDays { get; init; }
        public bool ReminderSuppressed { get; init; }
        public bool HasReflection { get; init; }
        public bool HasRetrievalCheck { get; init; }
        public string? MethodAnswer { get; init; }
        public string? RecallAnswer { get; init; }
        public bool IllusionRisk { get; init; }
    }
}
