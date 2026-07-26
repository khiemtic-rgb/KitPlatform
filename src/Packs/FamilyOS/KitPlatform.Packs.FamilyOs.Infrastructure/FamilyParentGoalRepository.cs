using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyParentGoalRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyParentGoalRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string GoalColumns = """
        g.id AS Id,
        g.family_id AS FamilyId,
        g.member_id AS MemberId,
        m.display_name AS MemberName,
        g.title AS Title,
        g.emoji AS Emoji,
        g.target_days_per_week AS TargetDaysPerWeek,
        g.share_with_family AS ShareWithFamily,
        g.is_active AS IsActive,
        g.sort_order AS SortOrder
        """;

    private const string GoalFromJoin = """
        FROM pack_family.parent_goal g
        INNER JOIN pack_family.membership m
            ON m.tenant_id = g.tenant_id
           AND m.id = g.member_id
           AND m.deleted_at IS NULL
        """;

    public async Task<IReadOnlyList<GoalRow>> ListForMemberAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<GoalRow>(
            $"""
            SELECT {GoalColumns}
            {GoalFromJoin}
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.member_id = @MemberId
              AND g.deleted_at IS NULL
            ORDER BY g.is_active DESC, g.sort_order, g.created_at
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<GoalRow>> ListSharedAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<GoalRow>(
            $"""
            SELECT {GoalColumns}
            {GoalFromJoin}
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.deleted_at IS NULL
              AND g.is_active = TRUE
              AND g.share_with_family = TRUE
            ORDER BY m.sort_order, m.display_name, g.sort_order
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<GoalRow?> GetAsync(
        Guid familyId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<GoalRow>(
            $"""
            SELECT {GoalColumns}
            {GoalFromJoin}
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.id = @GoalId
              AND g.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, GoalId = goalId });
    }

    public async Task<Guid> InsertAsync(
        Guid familyId,
        Guid memberId,
        string title,
        string? emoji,
        int targetDaysPerWeek,
        bool shareWithFamily,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.parent_goal (
                tenant_id, family_id, member_id, title, emoji,
                target_days_per_week, share_with_family,
                sort_order
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @Title, @Emoji,
                @TargetDaysPerWeek, @ShareWithFamily,
                COALESCE((
                    SELECT MAX(sort_order) + 1 FROM pack_family.parent_goal
                    WHERE tenant_id = @TenantId AND family_id = @FamilyId
                      AND member_id = @MemberId AND deleted_at IS NULL
                ), 0)
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                Title = title,
                Emoji = emoji,
                TargetDaysPerWeek = targetDaysPerWeek,
                ShareWithFamily = shareWithFamily,
            });
    }

    public async Task UpdateAsync(
        Guid familyId,
        Guid goalId,
        string? title,
        string? emoji,
        int? targetDaysPerWeek,
        bool? shareWithFamily,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.parent_goal
            SET title = COALESCE(NULLIF(TRIM(@Title), ''), title),
                emoji = COALESCE(@Emoji, emoji),
                target_days_per_week = COALESCE(@TargetDaysPerWeek, target_days_per_week),
                share_with_family = COALESCE(@ShareWithFamily, share_with_family),
                is_active = COALESCE(@IsActive, is_active),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @GoalId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                GoalId = goalId,
                Title = title,
                Emoji = emoji,
                TargetDaysPerWeek = targetDaysPerWeek,
                ShareWithFamily = shareWithFamily,
                IsActive = isActive,
            });
    }

    public async Task SoftDeleteAsync(
        Guid familyId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.parent_goal
            SET deleted_at = NOW(), is_active = FALSE, updated_at = NOW()
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @GoalId
            """,
            new { TenantId, FamilyId = familyId, GoalId = goalId });
    }

    public async Task UpsertCheckinAsync(
        Guid goalId,
        Guid memberId,
        Guid familyId,
        DateOnly date,
        string status,
        string? note,
        CancellationToken cancellationToken)
    {
        _ = familyId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.parent_goal_checkin (
                tenant_id, goal_id, member_id, checkin_date, status, note
            )
            VALUES (@TenantId, @GoalId, @MemberId, @Date, @Status, @Note)
            ON CONFLICT (goal_id, checkin_date)
            DO UPDATE SET
                status = EXCLUDED.status,
                note = EXCLUDED.note,
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                GoalId = goalId,
                MemberId = memberId,
                Date = date,
                Status = status,
                Note = note,
            });
    }

    public async Task ClearCheckinAsync(
        Guid goalId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.parent_goal_checkin
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId AND goal_id = @GoalId AND checkin_date = @Date
              AND deleted_at IS NULL
            """,
            new { TenantId, GoalId = goalId, Date = date });
    }

    public async Task<IReadOnlyList<CheckinRow>> ListCheckinsAsync(
        IReadOnlyCollection<Guid> goalIds,
        DateOnly fromDate,
        CancellationToken cancellationToken)
    {
        if (goalIds.Count == 0) return [];
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CheckinRow>(
            """
            SELECT goal_id AS GoalId, checkin_date AS CheckinDate, status AS Status
            FROM pack_family.parent_goal_checkin
            WHERE tenant_id = @TenantId
              AND goal_id = ANY(@GoalIds)
              AND checkin_date >= @FromDate
              AND deleted_at IS NULL
            ORDER BY checkin_date DESC
            """,
            new { TenantId, GoalIds = goalIds.ToArray(), FromDate = fromDate });
        return rows.AsList();
    }

    internal sealed class GoalRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Emoji { get; init; }
        public int TargetDaysPerWeek { get; init; }
        public bool ShareWithFamily { get; init; }
        public bool IsActive { get; init; }
        public int SortOrder { get; init; }
    }

    internal sealed class CheckinRow
    {
        public Guid GoalId { get; init; }
        public DateOnly CheckinDate { get; init; }
        public string Status { get; init; } = "";
    }
}
