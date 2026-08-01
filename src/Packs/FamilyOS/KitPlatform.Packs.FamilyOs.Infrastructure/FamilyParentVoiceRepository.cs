using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyParentVoiceRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyParentVoiceRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        v.id AS Id,
        v.family_id AS FamilyId,
        v.from_member_id AS FromMemberId,
        fm.display_name AS FromMemberName,
        v.to_member_id AS ToMemberId,
        tm.display_name AS ToMemberName,
        v.flow_date AS FlowDate,
        v.template_code AS TemplateCode,
        v.body_vi AS BodyVi,
        v.status AS Status,
        v.sent_at AS SentAt,
        v.ack_at AS AckAt
        """;

    private const string FromJoin = """
        FROM pack_family.parent_voice_message v
        INNER JOIN pack_family.membership fm
            ON fm.tenant_id = v.tenant_id
           AND fm.id = v.from_member_id
           AND fm.deleted_at IS NULL
        INNER JOIN pack_family.membership tm
            ON tm.tenant_id = v.tenant_id
           AND tm.id = v.to_member_id
           AND tm.deleted_at IS NULL
        """;

    public async Task<IReadOnlyList<VoiceRow>> ListAsync(
        Guid familyId,
        Guid? forMemberId,
        Guid? fromMemberId,
        DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<VoiceRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE v.tenant_id = @TenantId
              AND v.family_id = @FamilyId
              AND v.deleted_at IS NULL
              AND (@FlowDate::date IS NULL OR v.flow_date = @FlowDate::date)
              AND (@ForMemberId::uuid IS NULL OR v.to_member_id = @ForMemberId::uuid)
              AND (@FromMemberId::uuid IS NULL OR v.from_member_id = @FromMemberId::uuid)
            ORDER BY v.sent_at DESC
            LIMIT 50
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FlowDate = flowDate,
                ForMemberId = forMemberId,
                FromMemberId = fromMemberId,
            });
        return rows.AsList();
    }

    public async Task<VoiceRow?> GetAsync(
        Guid familyId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<VoiceRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE v.tenant_id = @TenantId
              AND v.family_id = @FamilyId
              AND v.id = @MessageId
              AND v.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MessageId = messageId });
    }

    public async Task<bool> HasSentTodayAsync(
        Guid familyId,
        Guid fromMemberId,
        Guid toMemberId,
        DateOnly flowDate,
        string? templateCode,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.parent_voice_message v
            WHERE v.tenant_id = @TenantId
              AND v.family_id = @FamilyId
              AND v.from_member_id = @FromMemberId
              AND v.to_member_id = @ToMemberId
              AND v.flow_date = @FlowDate
              AND v.deleted_at IS NULL
              AND (@TemplateCode::text IS NULL OR v.template_code = @TemplateCode)
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                FlowDate = flowDate,
                TemplateCode = templateCode,
            });
        return n > 0;
    }

    public async Task<int> DaysSinceLastFromAsync(
        Guid familyId,
        Guid fromMemberId,
        Guid toMemberId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var last = await conn.ExecuteScalarAsync<DateOnly?>(
            """
            SELECT v.flow_date
            FROM pack_family.parent_voice_message v
            WHERE v.tenant_id = @TenantId
              AND v.family_id = @FamilyId
              AND v.from_member_id = @FromMemberId
              AND v.to_member_id = @ToMemberId
              AND v.deleted_at IS NULL
            ORDER BY v.flow_date DESC
            LIMIT 1
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
            });
        if (last is null) return 99;
        return Math.Max(0, today.DayNumber - last.Value.DayNumber);
    }

    public async Task<Guid> InsertAsync(
        Guid familyId,
        DateOnly flowDate,
        Guid fromMemberId,
        Guid toMemberId,
        string templateCode,
        string bodyVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.parent_voice_message (
                tenant_id, family_id, flow_date,
                from_member_id, to_member_id,
                template_code, body_vi, status, sent_at
            )
            VALUES (
                @TenantId, @FamilyId, @FlowDate,
                @FromMemberId, @ToMemberId,
                @TemplateCode, @BodyVi, 'sent', NOW()
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FlowDate = flowDate,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                TemplateCode = templateCode,
                BodyVi = bodyVi,
            });
    }

    public async Task<bool> AckAsync(
        Guid familyId,
        Guid messageId,
        string status,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_family.parent_voice_message
            SET status = @Status,
                ack_at = COALESCE(ack_at, NOW()),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @MessageId
              AND deleted_at IS NULL
              AND status = 'sent'
            """,
            new { TenantId, FamilyId = familyId, MessageId = messageId, Status = status });
        return n > 0;
    }

    /// <summary>Consecutive days ending yesterday/today where child had commitments and Open=0.</summary>
    public async Task<int> CountChildDoneStreakAsync(
        Guid familyId,
        Guid childMemberId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<(DateOnly FlowDate, int Total, int Open)>(
            """
            SELECT d.flow_date AS FlowDate,
                   COUNT(*)::int AS Total,
                   COUNT(*) FILTER (
                       WHERE c.status IS DISTINCT FROM 'done'
                         AND c.status IS DISTINCT FROM 'skipped'
                   )::int AS Open
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d
                ON d.id = c.day_flow_id
               AND d.tenant_id = c.tenant_id
               AND d.deleted_at IS NULL
            WHERE c.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND c.member_id = @ChildMemberId
              AND c.deleted_at IS NULL
              AND d.flow_date <= @Today
              AND d.flow_date >= (@Today::date - 21)
            GROUP BY d.flow_date
            ORDER BY d.flow_date DESC
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ChildMemberId = childMemberId,
                Today = today,
            })).AsList();

        var streak = 0;
        var expect = today;
        foreach (var row in rows)
        {
            if (row.FlowDate != expect)
            {
                if (streak == 0 && row.FlowDate == today.AddDays(-1))
                {
                    expect = today.AddDays(-1);
                    if (row.FlowDate != expect) break;
                }
                else break;
            }

            if (row.Total <= 0 || row.Open > 0) break;
            streak++;
            expect = expect.AddDays(-1);
        }

        return streak;
    }

    internal sealed class VoiceRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid FromMemberId { get; init; }
        public string FromMemberName { get; init; } = "";
        public Guid ToMemberId { get; init; }
        public string ToMemberName { get; init; } = "";
        public DateOnly FlowDate { get; init; }
        public string TemplateCode { get; init; } = "";
        public string BodyVi { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTimeOffset SentAt { get; init; }
        public DateTimeOffset? AckAt { get; init; }
    }
}
