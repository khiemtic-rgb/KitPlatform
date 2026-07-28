using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyValueRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyValueRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task UpsertHealthScoreAsync(
        Guid familyId,
        DateOnly scoreDate,
        int score,
        string? breakdownJson,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family_health_score (
                tenant_id, family_id, score_date, score, breakdown
            )
            VALUES (
                @TenantId, @FamilyId, @ScoreDate, @Score,
                CASE WHEN @BreakdownJson IS NULL OR btrim(@BreakdownJson) = '' THEN NULL
                     ELSE CAST(@BreakdownJson AS jsonb) END
            )
            ON CONFLICT (tenant_id, family_id, score_date)
            DO UPDATE SET
                score = EXCLUDED.score,
                breakdown = EXCLUDED.breakdown,
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ScoreDate = scoreDate,
                Score = score,
                BreakdownJson = breakdownJson,
            });
    }

    public async Task<IReadOnlyList<HealthScoreRow>> ListHealthScoresAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<HealthScoreRow>(
            """
            SELECT score_date AS ScoreDate, score AS Score
            FROM pack_family.family_health_score
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND score_date >= @From
              AND score_date <= @To
              AND deleted_at IS NULL
            ORDER BY score_date
            """,
            new { TenantId, FamilyId = familyId, From = from, To = to });
        return rows.AsList();
    }

    public async Task<int> IncrementNudgeAsync(
        Guid familyId,
        DateOnly nudgeDate,
        int increment,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO pack_family.parent_nudge_day (
                tenant_id, family_id, nudge_date, nudge_count
            )
            VALUES (@TenantId, @FamilyId, @NudgeDate, @Increment)
            ON CONFLICT (tenant_id, family_id, nudge_date)
            DO UPDATE SET
                nudge_count = pack_family.parent_nudge_day.nudge_count + EXCLUDED.nudge_count,
                updated_at = NOW(),
                deleted_at = NULL
            RETURNING nudge_count
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                NudgeDate = nudgeDate,
                Increment = increment,
            });
    }

    public async Task SetNudgeCountAsync(
        Guid familyId,
        DateOnly nudgeDate,
        int count,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.parent_nudge_day (
                tenant_id, family_id, nudge_date, nudge_count
            )
            VALUES (@TenantId, @FamilyId, @NudgeDate, @Count)
            ON CONFLICT (tenant_id, family_id, nudge_date)
            DO UPDATE SET
                nudge_count = GREATEST(pack_family.parent_nudge_day.nudge_count, EXCLUDED.nudge_count),
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                NudgeDate = nudgeDate,
                Count = count,
            });
    }

    public async Task<int> GetNudgeCountAsync(
        Guid familyId,
        DateOnly nudgeDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE((
                SELECT nudge_count
                FROM pack_family.parent_nudge_day
                WHERE tenant_id = @TenantId
                  AND family_id = @FamilyId
                  AND nudge_date = @NudgeDate
                  AND deleted_at IS NULL
            ), 0)
            """,
            new { TenantId, FamilyId = familyId, NudgeDate = nudgeDate });
    }

    public async Task<IReadOnlyList<NudgeDayRow>> ListNudgeDaysAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<NudgeDayRow>(
            """
            SELECT nudge_date AS NudgeDate, nudge_count AS NudgeCount
            FROM pack_family.parent_nudge_day
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND nudge_date >= @From
              AND nudge_date <= @To
              AND deleted_at IS NULL
            ORDER BY nudge_date
            """,
            new { TenantId, FamilyId = familyId, From = from, To = to });
        return rows.AsList();
    }

    public async Task UpsertOnboardingAsync(
        Guid familyId,
        string payloadJson,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.family_onboarding (
                tenant_id, family_id, payload, completed_at
            )
            VALUES (
                @TenantId, @FamilyId, CAST(@PayloadJson AS jsonb), @CompletedAt
            )
            ON CONFLICT (tenant_id, family_id)
            DO UPDATE SET
                payload = EXCLUDED.payload,
                completed_at = EXCLUDED.completed_at,
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                CompletedAt = completedAt,
            });
    }

    public async Task SoftDeleteOnboardingAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_onboarding
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<OnboardingRow?> GetOnboardingAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<OnboardingRow>(
            """
            SELECT payload::text AS PayloadJson, completed_at AS CompletedAt
            FROM pack_family.family_onboarding
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId });
    }

    internal sealed class HealthScoreRow
    {
        public DateOnly ScoreDate { get; init; }
        public int Score { get; init; }
    }

    internal sealed class NudgeDayRow
    {
        public DateOnly NudgeDate { get; init; }
        public int NudgeCount { get; init; }
    }

    internal sealed class OnboardingRow
    {
        public string PayloadJson { get; init; } = "{}";
        public DateTimeOffset CompletedAt { get; init; }
    }
}
