using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyTeamNudgeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyTeamNudgeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public sealed class NudgeRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public DateOnly FlowDate { get; init; }
        public Guid FromMemberId { get; init; }
        public string FromName { get; init; } = "";
        public Guid ToMemberId { get; init; }
        public string ToName { get; init; } = "";
        public Guid? CommitmentId { get; init; }
        public string TemplateCode { get; init; } = "";
        public string MessageVi { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTimeOffset? SentAt { get; init; }
        public DateTimeOffset? AckAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    private const string SelectSql = """
        SELECT
            n.id AS Id, n.family_id AS FamilyId, n.flow_date AS FlowDate,
            n.from_member_id AS FromMemberId, COALESCE(mf.display_name, '') AS FromName,
            n.to_member_id AS ToMemberId, COALESCE(mt.display_name, '') AS ToName,
            n.commitment_id AS CommitmentId, n.template_code AS TemplateCode,
            n.message_vi AS MessageVi, n.status AS Status,
            n.sent_at AS SentAt, n.ack_at AS AckAt, n.created_at AS CreatedAt
        FROM pack_family.team_nudge n
        LEFT JOIN pack_family.membership mf
            ON mf.id = n.from_member_id AND mf.deleted_at IS NULL
        LEFT JOIN pack_family.membership mt
            ON mt.id = n.to_member_id AND mt.deleted_at IS NULL
        """ + "\n";

    public async Task<NudgeRow?> GetAsync(
        Guid familyId,
        Guid nudgeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<NudgeRow>(
            SelectSql + """
            WHERE n.tenant_id = @TenantId
              AND n.family_id = @FamilyId
              AND n.id = @NudgeId
              AND n.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, NudgeId = nudgeId });
    }

    public async Task<IReadOnlyList<NudgeRow>> ListAsync(
        Guid familyId,
        DateOnly? flowDate,
        Guid? forMemberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<NudgeRow>(
            SelectSql + """
            WHERE n.tenant_id = @TenantId
              AND n.family_id = @FamilyId
              AND n.deleted_at IS NULL
              AND (@FlowDate::date IS NULL OR n.flow_date = @FlowDate::date)
              AND (
                    @ForMemberId::uuid IS NULL
                    OR n.to_member_id = @ForMemberId
                    OR n.from_member_id = @ForMemberId
                  )
            ORDER BY n.flow_date DESC, n.created_at DESC
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FlowDate = flowDate,
                ForMemberId = forMemberId,
            });
        return rows.AsList();
    }

    public async Task<int> CountSentTodayAsync(
        Guid familyId,
        Guid fromMemberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.team_nudge
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND from_member_id = @FromMemberId
              AND flow_date = @FlowDate
              AND status = 'sent'
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                FromMemberId = fromMemberId,
                FlowDate = flowDate,
            });
    }

    public async Task<NudgeRow> InsertDraftAsync(
        Guid familyId,
        DateOnly flowDate,
        Guid fromMemberId,
        Guid toMemberId,
        Guid? commitmentId,
        string templateCode,
        string messageVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var id = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.team_nudge (
                tenant_id, family_id, flow_date,
                from_member_id, to_member_id, commitment_id,
                template_code, message_vi, status
            )
            VALUES (
                @TenantId, @FamilyId, @FlowDate,
                @FromMemberId, @ToMemberId, @CommitmentId,
                @TemplateCode, @MessageVi, 'draft'
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
                CommitmentId = commitmentId,
                TemplateCode = templateCode,
                MessageVi = messageVi,
            });

        return (await GetAsync(familyId, id, cancellationToken))!;
    }

    public async Task<NudgeRow?> MarkSentAsync(
        Guid familyId,
        Guid nudgeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.team_nudge
            SET status = 'sent',
                sent_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @NudgeId
              AND deleted_at IS NULL
              AND status = 'draft'
            """,
            new { TenantId, FamilyId = familyId, NudgeId = nudgeId });
        return await GetAsync(familyId, nudgeId, cancellationToken);
    }

    public async Task<NudgeRow?> AckAsync(
        Guid familyId,
        Guid nudgeId,
        string status,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.team_nudge
            SET status = @Status,
                ack_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @NudgeId
              AND deleted_at IS NULL
              AND status IN ('sent', 'seen')
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                NudgeId = nudgeId,
                Status = status,
            });
        return await GetAsync(familyId, nudgeId, cancellationToken);
    }

    public async Task<int> CountSentInRangeAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.team_nudge
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND flow_date >= @From
              AND flow_date <= @To
              AND status IN ('sent', 'seen', 'thanks', 'deferred')
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, From = from, To = to });
    }

    public async Task<int> CountThanksInRangeAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.team_nudge
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND flow_date >= @From
              AND flow_date <= @To
              AND status = 'thanks'
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, From = from, To = to });
    }
}
