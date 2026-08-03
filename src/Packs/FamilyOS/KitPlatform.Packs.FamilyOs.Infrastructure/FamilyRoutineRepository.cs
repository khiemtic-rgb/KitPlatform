using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;
namespace KitPlatform.Packs.FamilyOs.Infrastructure;
internal sealed class FamilyRoutineRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    public FamilyRoutineRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }
    private Guid TenantId => _tenant.TenantId;
    private const string TemplateSelect = """
        id AS Id, routine_id AS RoutineId, member_id AS MemberId, title AS Title,
        description AS Description, window_start AS WindowStart, window_end AS WindowEnd,
        sort_order AS SortOrder, is_active AS IsActive,
        priority AS Priority, expected_duration_minutes AS ExpectedDurationMinutes,
        context_anchor AS ContextAnchor, depends_on_template_ids AS DependsOnTemplateIds,
        allow_early_complete AS AllowEarlyComplete,
        early_lead_minutes AS EarlyLeadMinutes,
        on_time_grace_minutes AS OnTimeGraceMinutes,
        star_reward AS StarReward,
        COALESCE(NULLIF(TRIM(commitment_kind), ''), 'chore') AS CommitmentKind
        """;
    public async Task<bool> FamilyExistsAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_family.family
                WHERE tenant_id = @TenantId AND id = @FamilyId AND deleted_at IS NULL
            )
            """,
            new { TenantId, FamilyId = familyId });
    }
    public async Task<IReadOnlyList<RoutineRow>> ListRoutinesAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<RoutineRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, code AS Code, display_name AS DisplayName,
                   kind AS Kind, weekdays::int[] AS Weekdays, is_active AS IsActive, sort_order AS SortOrder
            FROM pack_family.routine
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND deleted_at IS NULL
            ORDER BY sort_order, created_at
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }
    public async Task<RoutineRow?> GetRoutineAsync(
        Guid familyId,
        Guid routineId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<RoutineRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, code AS Code, display_name AS DisplayName,
                   kind AS Kind, weekdays::int[] AS Weekdays, is_active AS IsActive, sort_order AS SortOrder
            FROM pack_family.routine
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @RoutineId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, RoutineId = routineId });
    }
    public async Task<IReadOnlyList<TemplateRow>> ListTemplatesAsync(
        Guid routineId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<TemplateRow>(
            $"""
            SELECT {TemplateSelect}
            FROM pack_family.commitment_template
            WHERE tenant_id = @TenantId AND routine_id = @RoutineId AND deleted_at IS NULL
            ORDER BY sort_order, created_at
            """,
            new { TenantId, RoutineId = routineId });
        return rows.AsList();
    }
    public async Task<Guid> InsertRoutineAsync(
        Guid familyId,
        string code,
        string displayName,
        string kind,
        int[] weekdays,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.routine (
                tenant_id, family_id, code, display_name, kind, weekdays, sort_order
            )
            VALUES (
                @TenantId, @FamilyId, @Code, @DisplayName, @Kind, @Weekdays, @SortOrder
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Code = code,
                DisplayName = displayName,
                Kind = kind,
                Weekdays = weekdays,
                SortOrder = sortOrder,
            });
    }
    public async Task<TemplateRow> InsertTemplateAsync(
        Guid routineId,
        string title,
        string? description,
        Guid? memberId,
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        int sortOrder,
        string priority,
        int? expectedDurationMinutes,
        string? contextAnchor,
        Guid[] dependsOnTemplateIds,
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        int onTimeGraceMinutes,
        int starReward,
        string commitmentKind,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<TemplateRow>(
            $"""
            INSERT INTO pack_family.commitment_template (
                tenant_id, routine_id, member_id, title, description,
                window_start, window_end, sort_order,
                priority, expected_duration_minutes, context_anchor, depends_on_template_ids,
                allow_early_complete, early_lead_minutes, on_time_grace_minutes, star_reward,
                commitment_kind
            )
            VALUES (
                @TenantId, @RoutineId, @MemberId, @Title, @Description,
                @WindowStart, @WindowEnd, @SortOrder,
                @Priority, @ExpectedDurationMinutes, @ContextAnchor, @DependsOnTemplateIds,
                @AllowEarlyComplete, @EarlyLeadMinutes, @OnTimeGraceMinutes, @StarReward,
                @CommitmentKind
            )
            RETURNING {TemplateSelect}
            """,
            new
            {
                TenantId,
                RoutineId = routineId,
                MemberId = memberId,
                Title = title,
                Description = description,
                WindowStart = windowStart?.ToTimeSpan(),
                WindowEnd = windowEnd?.ToTimeSpan(),
                SortOrder = sortOrder,
                Priority = priority,
                ExpectedDurationMinutes = expectedDurationMinutes,
                ContextAnchor = contextAnchor,
                DependsOnTemplateIds = dependsOnTemplateIds,
                AllowEarlyComplete = allowEarlyComplete,
                EarlyLeadMinutes = earlyLeadMinutes,
                OnTimeGraceMinutes = onTimeGraceMinutes,
                StarReward = starReward,
                CommitmentKind = commitmentKind,
            });
    }
    public async Task<RoutineRow?> UpdateRoutineAsync(
        Guid familyId,
        Guid routineId,
        string displayName,
        string kind,
        int[] weekdays,
        bool isActive,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<RoutineRow>(
            """
            UPDATE pack_family.routine
            SET display_name = @DisplayName,
                kind = @Kind,
                weekdays = @Weekdays,
                is_active = @IsActive,
                sort_order = @SortOrder,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @RoutineId
              AND deleted_at IS NULL
            RETURNING
                id AS Id, family_id AS FamilyId, code AS Code, display_name AS DisplayName,
                kind AS Kind, weekdays::int[] AS Weekdays, is_active AS IsActive, sort_order AS SortOrder
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                RoutineId = routineId,
                DisplayName = displayName,
                Kind = kind,
                Weekdays = weekdays,
                IsActive = isActive,
                SortOrder = sortOrder,
            });
    }
    public async Task<TemplateRow?> GetTemplateAsync(
        Guid routineId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TemplateRow>(
            $"""
            SELECT {TemplateSelect}
            FROM pack_family.commitment_template
            WHERE tenant_id = @TenantId
              AND routine_id = @RoutineId
              AND id = @TemplateId
              AND deleted_at IS NULL
            """,
            new { TenantId, RoutineId = routineId, TemplateId = templateId });
    }
    public async Task<TemplateRow?> UpdateTemplateAsync(
        Guid routineId,
        Guid templateId,
        string title,
        string? description,
        Guid? memberId,
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        int sortOrder,
        bool isActive,
        string priority,
        int? expectedDurationMinutes,
        string? contextAnchor,
        Guid[] dependsOnTemplateIds,
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        int onTimeGraceMinutes,
        int starReward,
        string commitmentKind,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TemplateRow>(
            $"""
            UPDATE pack_family.commitment_template
            SET title = @Title,
                description = @Description,
                member_id = @MemberId,
                window_start = @WindowStart,
                window_end = @WindowEnd,
                sort_order = @SortOrder,
                is_active = @IsActive,
                priority = @Priority,
                expected_duration_minutes = @ExpectedDurationMinutes,
                context_anchor = @ContextAnchor,
                depends_on_template_ids = @DependsOnTemplateIds,
                allow_early_complete = @AllowEarlyComplete,
                early_lead_minutes = @EarlyLeadMinutes,
                on_time_grace_minutes = @OnTimeGraceMinutes,
                star_reward = @StarReward,
                commitment_kind = @CommitmentKind,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND routine_id = @RoutineId
              AND id = @TemplateId
              AND deleted_at IS NULL
            RETURNING {TemplateSelect}
            """,
            new
            {
                TenantId,
                RoutineId = routineId,
                TemplateId = templateId,
                Title = title,
                Description = description,
                MemberId = memberId,
                WindowStart = windowStart?.ToTimeSpan(),
                WindowEnd = windowEnd?.ToTimeSpan(),
                SortOrder = sortOrder,
                IsActive = isActive,
                Priority = priority,
                ExpectedDurationMinutes = expectedDurationMinutes,
                ContextAnchor = contextAnchor,
                DependsOnTemplateIds = dependsOnTemplateIds,
                AllowEarlyComplete = allowEarlyComplete,
                EarlyLeadMinutes = earlyLeadMinutes,
                OnTimeGraceMinutes = onTimeGraceMinutes,
                StarReward = starReward,
                CommitmentKind = commitmentKind,
            });
    }
    public async Task<bool> SoftDeleteTemplateAsync(
        Guid routineId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var affected = await conn.ExecuteAsync(
            """
            UPDATE pack_family.commitment_template
            SET deleted_at = NOW(),
                is_active = FALSE,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND routine_id = @RoutineId
              AND id = @TemplateId
              AND deleted_at IS NULL
            """,
            new { TenantId, RoutineId = routineId, TemplateId = templateId });
        return affected > 0;
    }
    public async Task<bool> MemberBelongsToFamilyAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_family.membership
                WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @MemberId
                  AND deleted_at IS NULL
            )
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
    }
    internal sealed class RoutineRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public string Code { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Kind { get; init; } = "";
        public int[]? Weekdays { get; init; }
        public bool IsActive { get; init; }
        public int SortOrder { get; init; }
    }
    internal sealed class TemplateRow
    {
        public Guid Id { get; init; }
        public Guid RoutineId { get; init; }
        public Guid? MemberId { get; init; }
        public string Title { get; init; } = "";
        public string? Description { get; init; }
        public TimeOnly? WindowStart { get; init; }
        public TimeOnly? WindowEnd { get; init; }
        public int SortOrder { get; init; }
        public bool IsActive { get; init; }
        public string Priority { get; init; } = FamilyCommitmentPriorities.Normal;
        public int? ExpectedDurationMinutes { get; init; }
        public string? ContextAnchor { get; init; }
        public Guid[]? DependsOnTemplateIds { get; init; }
        public bool AllowEarlyComplete { get; init; }
        public int EarlyLeadMinutes { get; init; }
        public int OnTimeGraceMinutes { get; init; }
        public int StarReward { get; init; }
        public string CommitmentKind { get; init; } = FamilyCommitmentKinds.Chore;
    }
}
