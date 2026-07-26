using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAiProposalRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyAiProposalRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        p.id AS Id,
        p.family_id AS FamilyId,
        p.member_id AS MemberId,
        m.display_name AS MemberName,
        p.kind AS Kind,
        p.title_vi AS TitleVi,
        p.body_vi AS BodyVi,
        p.payload_json::text AS PayloadJson,
        p.status AS Status,
        p.created_at AS CreatedAt,
        p.decided_at AS DecidedAt
        """;

    public async Task<IReadOnlyList<AiProposalRow>> ListPendingAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AiProposalRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.ai_proposal p
            LEFT JOIN pack_family.membership m
                ON m.tenant_id = p.tenant_id AND m.id = p.member_id AND m.deleted_at IS NULL
            WHERE p.tenant_id = @TenantId
              AND p.family_id = @FamilyId
              AND p.deleted_at IS NULL
              AND p.status = 'pending'
            ORDER BY p.created_at DESC
            LIMIT 40
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<AiProposalRow?> GetAsync(
        Guid familyId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AiProposalRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.ai_proposal p
            LEFT JOIN pack_family.membership m
                ON m.tenant_id = p.tenant_id AND m.id = p.member_id AND m.deleted_at IS NULL
            WHERE p.tenant_id = @TenantId
              AND p.family_id = @FamilyId
              AND p.id = @ProposalId
              AND p.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, ProposalId = proposalId });
    }

    public async Task<Guid?> FindPendingBySourceAsync(
        Guid familyId,
        string kind,
        string sourceRef,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id
            FROM pack_family.ai_proposal
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND kind = @Kind
              AND source_ref = @SourceRef
              AND status = 'pending'
              AND deleted_at IS NULL
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, Kind = kind, SourceRef = sourceRef });
    }

    public async Task<Guid?> TryInsertAsync(
        Guid familyId,
        Guid? memberId,
        string kind,
        string titleVi,
        string bodyVi,
        string? payloadJson,
        string sourceRef,
        CancellationToken cancellationToken)
    {
        var existing = await FindPendingBySourceAsync(familyId, kind, sourceRef, cancellationToken);
        if (existing is not null) return null;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            return await conn.ExecuteScalarAsync<Guid>(
                """
                INSERT INTO pack_family.ai_proposal (
                    tenant_id, family_id, member_id, kind, title_vi, body_vi,
                    payload_json, status, source_ref
                )
                VALUES (
                    @TenantId, @FamilyId, @MemberId, @Kind, @TitleVi, @BodyVi,
                    CASE WHEN @PayloadJson::text IS NULL THEN NULL ELSE @PayloadJson::jsonb END,
                    'pending', @SourceRef
                )
                RETURNING id
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    MemberId = memberId,
                    Kind = kind,
                    TitleVi = titleVi,
                    BodyVi = bodyVi,
                    PayloadJson = payloadJson,
                    SourceRef = sourceRef,
                });
        }
        catch
        {
            return null;
        }
    }

    public async Task DecideAsync(
        Guid familyId,
        Guid proposalId,
        string status,
        Guid decidedBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.ai_proposal
            SET status = @Status,
                decided_by = @DecidedBy,
                decided_at = NOW(),
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @ProposalId
              AND deleted_at IS NULL
              AND status = 'pending'
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ProposalId = proposalId,
                Status = status,
                DecidedBy = decidedBy,
            });
    }

    internal sealed class AiProposalRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public string Kind { get; init; } = "";
        public string TitleVi { get; init; } = "";
        public string BodyVi { get; init; } = "";
        public string? PayloadJson { get; init; }
        public string Status { get; init; } = "";
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? DecidedAt { get; init; }
    }
}
