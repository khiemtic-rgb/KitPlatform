using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyMoodRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyMoodRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        e.id AS Id,
        e.family_id AS FamilyId,
        e.member_id AS MemberId,
        m.display_name AS MemberName,
        e.flow_date AS FlowDate,
        e.mood_code AS MoodCode,
        e.note AS Note,
        e.created_at AS CreatedAt,
        e.updated_at AS UpdatedAt
        """;

    private const string FromJoin = """
        FROM pack_family.member_mood_entry e
        INNER JOIN pack_family.membership m
            ON m.tenant_id = e.tenant_id
           AND m.id = e.member_id
           AND m.deleted_at IS NULL
        """;

    public async Task<IReadOnlyList<MoodRow>> ListByFamilyDayAsync(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MoodRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE e.tenant_id = @TenantId
              AND e.family_id = @FamilyId
              AND e.flow_date = @FlowDate
              AND e.deleted_at IS NULL
            ORDER BY m.sort_order, m.display_name
            """,
            new { TenantId, FamilyId = familyId, FlowDate = flowDate });
        return rows.AsList();
    }

    public async Task<MoodRow?> GetByMemberDayAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MoodRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE e.tenant_id = @TenantId
              AND e.family_id = @FamilyId
              AND e.member_id = @MemberId
              AND e.flow_date = @FlowDate
              AND e.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, FlowDate = flowDate });
    }

    public async Task<Guid> UpsertAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        string moodCode,
        string? note,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.member_mood_entry (
                tenant_id, family_id, member_id, flow_date, mood_code, note
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @FlowDate, @MoodCode, @Note
            )
            ON CONFLICT (tenant_id, family_id, member_id, flow_date)
            DO UPDATE SET
                mood_code = EXCLUDED.mood_code,
                note = EXCLUDED.note,
                updated_at = NOW(),
                deleted_at = NULL
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
                MoodCode = moodCode,
                Note = note,
            });
    }

    public async Task<MoodRow?> GetAsync(
        Guid familyId,
        Guid moodId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MoodRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE e.tenant_id = @TenantId
              AND e.family_id = @FamilyId
              AND e.id = @MoodId
              AND e.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MoodId = moodId });
    }

    internal sealed class MoodRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = "";
        public DateOnly FlowDate { get; init; }
        public string MoodCode { get; init; } = "";
        public string? Note { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
