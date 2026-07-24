using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyConsequenceRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyConsequenceRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<AgreementMatchRow>> ListAcceptedAccountabilityRulesAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<AgreementMatchRow>(
            """
            SELECT id AS Id, title AS Title, target_id AS TargetId, terms::text AS TermsJson
            FROM pack_family.agreement
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND status = 'accepted'
              AND target_type IN ('accountability', 'accountability_rule')
            ORDER BY decided_at DESC NULLS LAST, created_at DESC
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<string?> ResolveConsequenceLabelAsync(
        Guid familyId,
        string code,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<string?>(
            """
            SELECT label_vi
            FROM pack_family.accountability_option
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND kind = 'consequence'
              AND code = @Code
              AND status = 'active'
              AND deleted_at IS NULL
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, Code = code });
    }

    public async Task InsertPendingIfAbsentAsync(
        Guid familyId,
        Guid dayFlowId,
        Guid commitmentId,
        Guid agreementId,
        Guid? memberId,
        DateOnly flowDate,
        string consequenceCode,
        string labelVi,
        string? triggerSkipReason,
        string commitmentTitle,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.consequence_event (
                tenant_id, family_id, day_flow_id, commitment_id, agreement_id,
                member_id, flow_date, consequence_code, label_vi,
                trigger_skip_reason, commitment_title, status
            )
            SELECT
                @TenantId, @FamilyId, @DayFlowId, @CommitmentId, @AgreementId,
                @MemberId, @FlowDate, @ConsequenceCode, @LabelVi,
                @TriggerSkipReason, @CommitmentTitle, 'pending_confirm'
            WHERE NOT EXISTS (
                SELECT 1
                FROM pack_family.consequence_event e
                WHERE e.tenant_id = @TenantId
                  AND e.commitment_id = @CommitmentId
                  AND e.agreement_id = @AgreementId
                  AND e.deleted_at IS NULL
            )
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                DayFlowId = dayFlowId,
                CommitmentId = commitmentId,
                AgreementId = agreementId,
                MemberId = memberId,
                FlowDate = flowDate,
                ConsequenceCode = consequenceCode,
                LabelVi = labelVi,
                TriggerSkipReason = triggerSkipReason,
                CommitmentTitle = commitmentTitle,
            });
    }

    public async Task<IReadOnlyList<EventRow>> ListAsync(
        Guid familyId,
        DateOnly? flowDate,
        string? status,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<EventRow>(
            """
            SELECT e.id AS Id, e.family_id AS FamilyId, e.day_flow_id AS DayFlowId,
                   e.commitment_id AS CommitmentId, e.agreement_id AS AgreementId,
                   e.member_id AS MemberId, m.display_name AS MemberName,
                   e.flow_date AS FlowDate, e.consequence_code AS ConsequenceCode,
                   e.label_vi AS LabelVi, e.trigger_skip_reason AS TriggerSkipReason,
                   e.commitment_title AS CommitmentTitle, e.status AS Status,
                   e.decided_by AS DecidedBy, e.decided_at AS DecidedAt,
                   e.decision_note AS DecisionNote, e.created_at AS CreatedAt
            FROM pack_family.consequence_event e
            LEFT JOIN pack_family.membership m
              ON m.id = e.member_id AND m.tenant_id = e.tenant_id AND m.deleted_at IS NULL
            WHERE e.tenant_id = @TenantId
              AND e.family_id = @FamilyId
              AND e.deleted_at IS NULL
              AND (@FlowDate::date IS NULL OR e.flow_date = @FlowDate::date)
              AND (@Status::varchar IS NULL OR e.status = @Status::varchar)
            ORDER BY
              CASE e.status WHEN 'pending_confirm' THEN 0 WHEN 'applied' THEN 1 ELSE 2 END,
              e.created_at DESC
            """,
            new { TenantId, FamilyId = familyId, FlowDate = flowDate, Status = status });
        return rows.AsList();
    }

    public async Task<EventRow?> GetAsync(
        Guid familyId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<EventRow>(
            """
            SELECT e.id AS Id, e.family_id AS FamilyId, e.day_flow_id AS DayFlowId,
                   e.commitment_id AS CommitmentId, e.agreement_id AS AgreementId,
                   e.member_id AS MemberId, m.display_name AS MemberName,
                   e.flow_date AS FlowDate, e.consequence_code AS ConsequenceCode,
                   e.label_vi AS LabelVi, e.trigger_skip_reason AS TriggerSkipReason,
                   e.commitment_title AS CommitmentTitle, e.status AS Status,
                   e.decided_by AS DecidedBy, e.decided_at AS DecidedAt,
                   e.decision_note AS DecisionNote, e.created_at AS CreatedAt
            FROM pack_family.consequence_event e
            LEFT JOIN pack_family.membership m
              ON m.id = e.member_id AND m.tenant_id = e.tenant_id AND m.deleted_at IS NULL
            WHERE e.tenant_id = @TenantId
              AND e.family_id = @FamilyId
              AND e.id = @EventId
              AND e.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, EventId = eventId });
    }

    public async Task<EventRow?> DecideAsync(
        Guid familyId,
        Guid eventId,
        string status,
        Guid decidedBy,
        string? decisionNote,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<EventRow>(
            """
            UPDATE pack_family.consequence_event
            SET status = @Status,
                decided_by = @DecidedBy,
                decided_at = NOW(),
                decision_note = @DecisionNote,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @EventId
              AND deleted_at IS NULL
              AND status = 'pending_confirm'
            RETURNING
                id AS Id, family_id AS FamilyId, day_flow_id AS DayFlowId,
                commitment_id AS CommitmentId, agreement_id AS AgreementId,
                member_id AS MemberId, NULL::text AS MemberName,
                flow_date AS FlowDate, consequence_code AS ConsequenceCode,
                label_vi AS LabelVi, trigger_skip_reason AS TriggerSkipReason,
                commitment_title AS CommitmentTitle, status AS Status,
                decided_by AS DecidedBy, decided_at AS DecidedAt,
                decision_note AS DecisionNote, created_at AS CreatedAt
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                EventId = eventId,
                Status = status,
                DecidedBy = decidedBy,
                DecisionNote = decisionNote,
            });
    }

    internal sealed class AgreementMatchRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = "";
        public Guid? TargetId { get; init; }
        public string TermsJson { get; init; } = "{}";
    }

    internal sealed class EventRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid DayFlowId { get; init; }
        public Guid CommitmentId { get; init; }
        public Guid AgreementId { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public DateOnly FlowDate { get; init; }
        public string ConsequenceCode { get; init; } = "";
        public string LabelVi { get; init; } = "";
        public string? TriggerSkipReason { get; init; }
        public string CommitmentTitle { get; init; } = "";
        public string Status { get; init; } = "";
        public Guid? DecidedBy { get; init; }
        public DateTimeOffset? DecidedAt { get; init; }
        public string? DecisionNote { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    internal static (Guid? TemplateId, Guid? MemberId, string? ConsequenceCode) ParseTerms(
        string? termsJson,
        Guid? targetId)
    {
        Guid? templateId = targetId;
        Guid? memberId = null;
        string? code = null;

        if (string.IsNullOrWhiteSpace(termsJson))
            return (templateId, memberId, code);

        try
        {
            using var doc = JsonDocument.Parse(termsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (templateId, memberId, code);

            if (doc.RootElement.TryGetProperty("consequenceCode", out var codeEl))
                code = codeEl.GetString();

            if (doc.RootElement.TryGetProperty("triggerCommitmentTemplateId", out var tplEl) &&
                Guid.TryParse(tplEl.GetString(), out var tpl))
                templateId = tpl;

            if (doc.RootElement.TryGetProperty("appliesToMemberId", out var memEl) &&
                Guid.TryParse(memEl.GetString(), out var mem))
                memberId = mem;
        }
        catch (JsonException)
        {
            // ignore malformed terms for matching
        }

        return (templateId, memberId, code);
    }
}
