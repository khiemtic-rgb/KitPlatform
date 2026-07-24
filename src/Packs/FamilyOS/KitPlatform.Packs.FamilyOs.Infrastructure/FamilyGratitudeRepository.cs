using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyGratitudeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyGratitudeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        g.id AS Id,
        g.family_id AS FamilyId,
        g.from_member_id AS FromMemberId,
        fm.display_name AS FromMemberName,
        g.to_member_id AS ToMemberId,
        tm.display_name AS ToMemberName,
        g.flow_date AS FlowDate,
        g.message_vi AS MessageVi,
        g.praise_context AS PraiseContext,
        g.created_at AS CreatedAt,
        g.read_at AS ReadAt
        """;

    private const string FromJoin = """
        FROM pack_family.child_gratitude g
        INNER JOIN pack_family.membership fm
            ON fm.tenant_id = g.tenant_id
           AND fm.id = g.from_member_id
           AND fm.deleted_at IS NULL
        LEFT JOIN pack_family.membership tm
            ON tm.tenant_id = g.tenant_id
           AND tm.id = g.to_member_id
           AND tm.deleted_at IS NULL
        """;

    public async Task<IReadOnlyList<GratitudeRow>> ListAsync(
        Guid familyId,
        DateOnly? flowDate,
        Guid? fromMemberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<GratitudeRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.deleted_at IS NULL
              AND (@FlowDate::date IS NULL OR g.flow_date = @FlowDate::date)
              AND (@FromMemberId::uuid IS NULL OR g.from_member_id = @FromMemberId::uuid)
            ORDER BY g.created_at DESC
            LIMIT 50
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FlowDate = flowDate,
                FromMemberId = fromMemberId,
            });
        return rows.AsList();
    }

    public async Task<GratitudeRow?> GetAsync(
        Guid familyId,
        Guid gratitudeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<GratitudeRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.id = @GratitudeId
              AND g.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, GratitudeId = gratitudeId });
    }

    public async Task<GratitudeRow?> GetByChildDayAsync(
        Guid familyId,
        Guid fromMemberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<GratitudeRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE g.tenant_id = @TenantId
              AND g.family_id = @FamilyId
              AND g.from_member_id = @FromMemberId
              AND g.flow_date = @FlowDate
              AND g.deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromMemberId = fromMemberId,
                FlowDate = flowDate,
            });
    }

    public async Task<Guid> InsertAsync(
        Guid familyId,
        Guid fromMemberId,
        Guid? toMemberId,
        DateOnly flowDate,
        string messageVi,
        string? praiseContext,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.child_gratitude (
                tenant_id, family_id, from_member_id, to_member_id,
                flow_date, message_vi, praise_context
            )
            VALUES (
                @TenantId, @FamilyId, @FromMemberId, @ToMemberId,
                @FlowDate, @MessageVi, @PraiseContext
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                FlowDate = flowDate,
                MessageVi = messageVi,
                PraiseContext = praiseContext,
            });
    }

    public async Task<bool> MarkReadAsync(
        Guid familyId,
        Guid gratitudeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_family.child_gratitude
            SET read_at = COALESCE(read_at, NOW()),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @GratitudeId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, GratitudeId = gratitudeId });
        return n > 0;
    }

    internal sealed class GratitudeRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid FromMemberId { get; init; }
        public string FromMemberName { get; init; } = "";
        public Guid? ToMemberId { get; init; }
        public string? ToMemberName { get; init; }
        public DateOnly FlowDate { get; init; }
        public string MessageVi { get; init; } = "";
        public string? PraiseContext { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? ReadAt { get; init; }
    }
}
