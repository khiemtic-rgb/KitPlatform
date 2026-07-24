using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAgreementRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyAgreementRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectCols = """
        a.id AS Id, a.family_id AS FamilyId, a.proposed_by AS ProposedBy,
        m.display_name AS ProposedByName, a.title AS Title, a.proposal_body AS ProposalBody,
        a.target_type AS TargetType, a.target_id AS TargetId, a.status AS Status,
        a.terms::text AS TermsJson, a.decided_at AS DecidedAt, a.decided_by AS DecidedBy,
        a.decision_note AS DecisionNote, a.created_at AS CreatedAt,
        a.purpose AS Purpose, a.effective_on AS EffectiveOn,
        a.review_after_days AS ReviewAfterDays, a.applies_to_member_id AS AppliesToMemberId
        """;

    public async Task<IReadOnlyList<AgreementRow>> ListAsync(
        Guid familyId,
        string? status,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AgreementRow>(
            $"""
            SELECT {SelectCols}
            FROM pack_family.agreement a
            LEFT JOIN pack_family.membership m
              ON m.id = a.proposed_by AND m.tenant_id = a.tenant_id AND m.deleted_at IS NULL
            WHERE a.tenant_id = @TenantId
              AND a.family_id = @FamilyId
              AND a.deleted_at IS NULL
              AND (@Status IS NULL OR a.status = @Status)
            ORDER BY a.created_at DESC
            """,
            new { TenantId, FamilyId = familyId, Status = status });
        return rows.AsList();
    }

    public async Task<AgreementRow?> GetAsync(
        Guid familyId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AgreementRow>(
            $"""
            SELECT {SelectCols}
            FROM pack_family.agreement a
            LEFT JOIN pack_family.membership m
              ON m.id = a.proposed_by AND m.tenant_id = a.tenant_id AND m.deleted_at IS NULL
            WHERE a.tenant_id = @TenantId
              AND a.family_id = @FamilyId
              AND a.id = @AgreementId
              AND a.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, AgreementId = agreementId });
    }

    public async Task<AgreementRow> InsertAsync(
        Guid familyId,
        Guid proposedBy,
        string title,
        string proposalBody,
        string targetType,
        Guid? targetId,
        string termsJson,
        string? purpose,
        DateOnly? effectiveOn,
        int? reviewAfterDays,
        Guid? appliesToMemberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<AgreementRow>(
            """
            INSERT INTO pack_family.agreement (
                tenant_id, family_id, proposed_by, title, proposal_body,
                target_type, target_id, status, terms,
                purpose, effective_on, review_after_days, applies_to_member_id
            )
            VALUES (
                @TenantId, @FamilyId, @ProposedBy, @Title, @ProposalBody,
                @TargetType, @TargetId, 'proposed', CAST(@TermsJson AS jsonb),
                @Purpose, @EffectiveOn, @ReviewAfterDays, @AppliesToMemberId
            )
            RETURNING
                id AS Id, family_id AS FamilyId, proposed_by AS ProposedBy,
                NULL::text AS ProposedByName, title AS Title, proposal_body AS ProposalBody,
                target_type AS TargetType, target_id AS TargetId, status AS Status,
                terms::text AS TermsJson, decided_at AS DecidedAt, decided_by AS DecidedBy,
                decision_note AS DecisionNote, created_at AS CreatedAt,
                purpose AS Purpose, effective_on AS EffectiveOn,
                review_after_days AS ReviewAfterDays, applies_to_member_id AS AppliesToMemberId
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                ProposedBy = proposedBy,
                Title = title,
                ProposalBody = proposalBody,
                TargetType = targetType,
                TargetId = targetId,
                TermsJson = string.IsNullOrWhiteSpace(termsJson) ? "{}" : termsJson,
                Purpose = purpose,
                EffectiveOn = effectiveOn,
                ReviewAfterDays = reviewAfterDays,
                AppliesToMemberId = appliesToMemberId,
            });
    }

    public async Task<AgreementRow?> DecideAsync(
        Guid familyId,
        Guid agreementId,
        string status,
        Guid decidedBy,
        string? decisionNote,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<AgreementRow>(
            """
            UPDATE pack_family.agreement
            SET status = @Status,
                decided_by = @DecidedBy,
                decided_at = NOW(),
                decision_note = @DecisionNote,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @AgreementId
              AND deleted_at IS NULL
            RETURNING
                id AS Id, family_id AS FamilyId, proposed_by AS ProposedBy,
                NULL::text AS ProposedByName, title AS Title, proposal_body AS ProposalBody,
                target_type AS TargetType, target_id AS TargetId, status AS Status,
                terms::text AS TermsJson, decided_at AS DecidedAt, decided_by AS DecidedBy,
                decision_note AS DecisionNote, created_at AS CreatedAt,
                purpose AS Purpose, effective_on AS EffectiveOn,
                review_after_days AS ReviewAfterDays, applies_to_member_id AS AppliesToMemberId
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                AgreementId = agreementId,
                Status = status,
                DecidedBy = decidedBy,
                DecisionNote = decisionNote,
            });
    }

    internal sealed class AgreementRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid ProposedBy { get; init; }
        public string? ProposedByName { get; init; }
        public string Title { get; init; } = "";
        public string ProposalBody { get; init; } = "";
        public string TargetType { get; init; } = "";
        public Guid? TargetId { get; init; }
        public string Status { get; init; } = "";
        public string? TermsJson { get; init; }
        public DateTimeOffset? DecidedAt { get; init; }
        public Guid? DecidedBy { get; init; }
        public string? DecisionNote { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string? Purpose { get; init; }
        public DateOnly? EffectiveOn { get; init; }
        public int? ReviewAfterDays { get; init; }
        public Guid? AppliesToMemberId { get; init; }
    }
}
