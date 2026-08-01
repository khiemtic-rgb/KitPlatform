using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyRelationshipTriggerStateRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyRelationshipTriggerStateRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private static readonly Guid NullToMemberKey = Guid.Empty;

    public sealed class StateRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid ViewerMemberId { get; init; }
        public DateOnly FlowDate { get; init; }
        public string TriggerCode { get; init; } = "";
        public Guid? ToMemberId { get; init; }
        public string State { get; init; } = "";
        public string? DraftBodyVi { get; init; }
        public string? TemplateCode { get; init; }
        public string? TitleVi { get; init; }
        public string? BodyVi { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private const string SelectColumns = """
        id AS Id,
        family_id AS FamilyId,
        viewer_member_id AS ViewerMemberId,
        flow_date AS FlowDate,
        trigger_code AS TriggerCode,
        to_member_id AS ToMemberId,
        state AS State,
        draft_body_vi AS DraftBodyVi,
        template_code AS TemplateCode,
        title_vi AS TitleVi,
        body_vi AS BodyVi,
        updated_at AS UpdatedAt
        """;

    public async Task<IReadOnlyList<StateRow>> ListAsync(
        Guid familyId,
        Guid viewerMemberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<StateRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.relationship_trigger_state
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND viewer_member_id = @ViewerMemberId
              AND flow_date = @FlowDate
              AND deleted_at IS NULL
            ORDER BY updated_at DESC
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ViewerMemberId = viewerMemberId,
                FlowDate = flowDate,
            });
        return rows.AsList();
    }

    public async Task<StateRow> UpsertAsync(
        Guid familyId,
        Guid viewerMemberId,
        DateOnly flowDate,
        string triggerCode,
        Guid? toMemberId,
        string state,
        string? draftBodyVi,
        string? templateCode,
        string? titleVi,
        string? bodyVi,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var toKey = toMemberId ?? NullToMemberKey;

        var existingId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id
            FROM pack_family.relationship_trigger_state
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND viewer_member_id = @ViewerMemberId
              AND flow_date = @FlowDate
              AND trigger_code = @TriggerCode
              AND COALESCE(to_member_id, '00000000-0000-0000-0000-000000000000'::uuid) = @ToKey
              AND deleted_at IS NULL
            LIMIT 1
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ViewerMemberId = viewerMemberId,
                FlowDate = flowDate,
                TriggerCode = triggerCode,
                ToKey = toKey,
            });

        Guid id;
        if (existingId is Guid found)
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_family.relationship_trigger_state
                SET state = @State,
                    draft_body_vi = COALESCE(@DraftBodyVi, draft_body_vi),
                    template_code = COALESCE(@TemplateCode, template_code),
                    title_vi = COALESCE(@TitleVi, title_vi),
                    body_vi = COALESCE(@BodyVi, body_vi),
                    updated_at = NOW()
                WHERE tenant_id = @TenantId
                  AND family_id = @FamilyId
                  AND id = @Id
                  AND deleted_at IS NULL
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    Id = found,
                    State = state,
                    DraftBodyVi = draftBodyVi,
                    TemplateCode = templateCode,
                    TitleVi = titleVi,
                    BodyVi = bodyVi,
                });
            id = found;
        }
        else
        {
            id = await conn.ExecuteScalarAsync<Guid>(
                """
                INSERT INTO pack_family.relationship_trigger_state (
                    tenant_id, family_id, viewer_member_id, flow_date,
                    trigger_code, to_member_id, state,
                    draft_body_vi, template_code, title_vi, body_vi
                )
                VALUES (
                    @TenantId, @FamilyId, @ViewerMemberId, @FlowDate,
                    @TriggerCode, @ToMemberId, @State,
                    @DraftBodyVi, @TemplateCode, @TitleVi, @BodyVi
                )
                RETURNING id
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    ViewerMemberId = viewerMemberId,
                    FlowDate = flowDate,
                    TriggerCode = triggerCode,
                    ToMemberId = toMemberId,
                    State = state,
                    DraftBodyVi = draftBodyVi,
                    TemplateCode = templateCode,
                    TitleVi = titleVi,
                    BodyVi = bodyVi,
                });
        }

        return (await conn.QuerySingleAsync<StateRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.relationship_trigger_state
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @Id
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, Id = id }))!;
    }
}
