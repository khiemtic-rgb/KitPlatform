using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyChallengeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyChallengeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<ChallengeRow?> GetByWeekAsync(
        Guid familyId,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ChallengeRow>(
            """
            SELECT
                id AS Id, family_id AS FamilyId, week_start AS WeekStart,
                status AS Status, title AS Title, reward_code AS RewardCode,
                reward_label AS RewardLabel, accepted_by AS AcceptedBy,
                completed_at AS CompletedAt, unlock_id AS UnlockId
            FROM pack_family.family_challenge
            WHERE tenant_id = @TenantId AND family_id = @FamilyId
              AND week_start = @WeekStart AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, WeekStart = weekStart });
    }

    public async Task<ChallengeRow?> GetAsync(
        Guid familyId,
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ChallengeRow>(
            """
            SELECT
                id AS Id, family_id AS FamilyId, week_start AS WeekStart,
                status AS Status, title AS Title, reward_code AS RewardCode,
                reward_label AS RewardLabel, accepted_by AS AcceptedBy,
                completed_at AS CompletedAt, unlock_id AS UnlockId
            FROM pack_family.family_challenge
            WHERE tenant_id = @TenantId AND family_id = @FamilyId
              AND id = @ChallengeId AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, ChallengeId = challengeId });
    }

    public async Task<Guid> InsertChallengeAsync(
        Guid familyId,
        DateOnly weekStart,
        string title,
        string rewardCode,
        string rewardLabel,
        Guid acceptedBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.family_challenge (
                tenant_id, family_id, week_start, status, title,
                reward_code, reward_label, accepted_by
            )
            VALUES (
                @TenantId, @FamilyId, @WeekStart, 'active', @Title,
                @RewardCode, @RewardLabel, @AcceptedBy
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                WeekStart = weekStart,
                Title = title,
                RewardCode = rewardCode,
                RewardLabel = rewardLabel,
                AcceptedBy = acceptedBy,
            });
    }

    public async Task InsertLegAsync(
        Guid challengeId,
        Guid? memberId,
        string legKind,
        string title,
        string? emoji,
        int targetDays,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family_challenge_leg (
                tenant_id, challenge_id, member_id, leg_kind, title, emoji,
                target_days, sort_order
            )
            VALUES (
                @TenantId, @ChallengeId, @MemberId, @LegKind, @Title, @Emoji,
                @TargetDays, @SortOrder
            )
            """,
            new
            {
                TenantId,
                ChallengeId = challengeId,
                MemberId = memberId,
                LegKind = legKind,
                Title = title,
                Emoji = emoji,
                TargetDays = targetDays,
                SortOrder = sortOrder,
            });
    }

    public async Task<IReadOnlyList<LegRow>> ListLegsAsync(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LegRow>(
            """
            SELECT
                l.id AS Id,
                l.challenge_id AS ChallengeId,
                l.member_id AS MemberId,
                m.display_name AS MemberName,
                l.leg_kind AS LegKind,
                l.title AS Title,
                l.emoji AS Emoji,
                l.target_days AS TargetDays,
                l.done_days AS DoneDays,
                l.sort_order AS SortOrder
            FROM pack_family.family_challenge_leg l
            LEFT JOIN pack_family.membership m
                ON m.id = l.member_id AND m.tenant_id = l.tenant_id AND m.deleted_at IS NULL
            WHERE l.tenant_id = @TenantId
              AND l.challenge_id = @ChallengeId
              AND l.deleted_at IS NULL
            ORDER BY l.sort_order, l.created_at
            """,
            new { TenantId, ChallengeId = challengeId });
        return rows.AsList();
    }

    public async Task<LegRow?> GetLegAsync(
        Guid challengeId,
        Guid legId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<LegRow>(
            """
            SELECT
                l.id AS Id,
                l.challenge_id AS ChallengeId,
                l.member_id AS MemberId,
                m.display_name AS MemberName,
                l.leg_kind AS LegKind,
                l.title AS Title,
                l.emoji AS Emoji,
                l.target_days AS TargetDays,
                l.done_days AS DoneDays,
                l.sort_order AS SortOrder
            FROM pack_family.family_challenge_leg l
            LEFT JOIN pack_family.membership m
                ON m.id = l.member_id AND m.tenant_id = l.tenant_id AND m.deleted_at IS NULL
            WHERE l.tenant_id = @TenantId
              AND l.challenge_id = @ChallengeId
              AND l.id = @LegId
              AND l.deleted_at IS NULL
            """,
            new { TenantId, ChallengeId = challengeId, LegId = legId });
    }

    public async Task UpsertCheckinAsync(
        Guid legId,
        DateOnly date,
        string status,
        Guid? checkedBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family_challenge_checkin (
                tenant_id, leg_id, checkin_date, status, checked_by
            )
            VALUES (@TenantId, @LegId, @Date, @Status, @CheckedBy)
            ON CONFLICT (leg_id, checkin_date)
            DO UPDATE SET
                status = EXCLUDED.status,
                checked_by = EXCLUDED.checked_by,
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                LegId = legId,
                Date = date,
                Status = status,
                CheckedBy = checkedBy,
            });
    }

    public async Task ClearCheckinAsync(
        Guid legId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_challenge_checkin
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId AND leg_id = @LegId
              AND checkin_date = @Date AND deleted_at IS NULL
            """,
            new { TenantId, LegId = legId, Date = date });
    }

    public async Task<IReadOnlyList<CheckinRow>> ListCheckinsAsync(
        IReadOnlyCollection<Guid> legIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (legIds.Count == 0) return [];
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CheckinRow>(
            """
            SELECT leg_id AS LegId, checkin_date AS CheckinDate, status AS Status
            FROM pack_family.family_challenge_checkin
            WHERE tenant_id = @TenantId
              AND leg_id = ANY(@LegIds)
              AND checkin_date >= @FromDate
              AND checkin_date <= @ToDate
              AND deleted_at IS NULL
            """,
            new { TenantId, LegIds = legIds.ToArray(), FromDate = from, ToDate = to });
        return rows.AsList();
    }

    public async Task SyncDoneDaysAsync(
        Guid legId,
        int doneDays,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_challenge_leg
            SET done_days = @DoneDays, updated_at = NOW()
            WHERE tenant_id = @TenantId AND id = @LegId AND deleted_at IS NULL
            """,
            new { TenantId, LegId = legId, DoneDays = doneDays });
    }

    public async Task MarkCompletedAsync(
        Guid challengeId,
        Guid? unlockId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_challenge
            SET status = 'completed',
                completed_at = NOW(),
                unlock_id = COALESCE(@UnlockId, unlock_id),
                updated_at = NOW()
            WHERE tenant_id = @TenantId AND id = @ChallengeId AND deleted_at IS NULL
            """,
            new { TenantId, ChallengeId = challengeId, UnlockId = unlockId });
    }

    public async Task<string?> FirstActiveParentGoalTitleAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<string?>(
            """
            SELECT title FROM pack_family.parent_goal
            WHERE tenant_id = @TenantId AND family_id = @FamilyId
              AND member_id = @MemberId AND deleted_at IS NULL AND is_active = TRUE
            ORDER BY sort_order, created_at
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
    }

    internal sealed class ChallengeRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public DateOnly WeekStart { get; init; }
        public string Status { get; init; } = "";
        public string Title { get; init; } = "";
        public string RewardCode { get; init; } = "";
        public string RewardLabel { get; init; } = "";
        public Guid? AcceptedBy { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public Guid? UnlockId { get; init; }
    }

    internal sealed class LegRow
    {
        public Guid Id { get; init; }
        public Guid ChallengeId { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public string LegKind { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Emoji { get; init; }
        public int TargetDays { get; init; }
        public int DoneDays { get; init; }
        public int SortOrder { get; init; }
    }

    internal sealed class CheckinRow
    {
        public Guid LegId { get; init; }
        public DateOnly CheckinDate { get; init; }
        public string Status { get; init; } = "";
    }
}
