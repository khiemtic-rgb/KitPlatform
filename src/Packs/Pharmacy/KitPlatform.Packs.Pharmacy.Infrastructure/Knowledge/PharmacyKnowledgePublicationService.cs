using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed class PharmacyKnowledgePublicationService : IPharmacyKnowledgePublicationService
{
    private const string VersionSelect = """
        SELECT
            v.rule_code AS RuleCode,
            v.version AS Version,
            v.lifecycle_status AS LifecycleStatus,
            v.source AS Source,
            v.effective_from AS EffectiveFrom,
            v.effective_to AS EffectiveTo,
            s.code AS SymptomCode,
            v.category_codes AS CategoryCodes,
            v.keywords AS Keywords,
            v.exclude_keywords AS ExcludeKeywords,
            v.reason_vi AS ReasonVi,
            v.priority AS Priority,
            v.reference_source AS ReferenceSource,
            v.source_identity AS SourceIdentity,
            v.source_url AS SourceUrl,
            v.document_ref AS DocumentRef,
            v.issuing_organization AS IssuingOrganization,
            v.source_published_on AS SourcePublishedOn,
            v.evidence_note AS EvidenceNote,
            v.source_verification_status AS SourceVerificationStatus,
            v.verified_by AS VerifiedBy,
            v.verified_at AS VerifiedAt
        FROM pharmacy_knowledge_rule_version v
        LEFT JOIN pharmacy_symptom s ON s.id = v.symptom_id
        """;

    private readonly IDbConnectionFactory _db;

    public PharmacyKnowledgePublicationService(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<PharmacyKnowledgeVersionDto>> ListVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<VersionRow>(
            VersionSelect + " ORDER BY v.rule_code, v.version");
        return rows.Select(Map).ToList();
    }

    public async Task<PharmacyKnowledgeVersionDto?> GetVersionAsync(
        string ruleCode,
        string version,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<VersionRow>(
            VersionSelect + " WHERE v.rule_code = @RuleCode AND v.version = @Version",
            new { RuleCode = Require(ruleCode), Version = Require(version) });
        return row is null ? null : Map(row);
    }

    public async Task<PharmacyTenantKnowledgeCatalogDto> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Thiếu nhà thuốc.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var versions = await conn.QueryAsync<VersionRow>(
            VersionSelect + """
             WHERE v.lifecycle_status = 'PUBLISHED'
             ORDER BY v.rule_code, v.version
            """);
        var pins = await conn.QueryAsync<AdoptionRow>(
            """
            SELECT
                a.rule_code AS RuleCode,
                a.rule_version AS RuleVersion,
                a.status AS Status,
                a.reference_source AS ReferenceSource,
                COALESCE(p.product_matching_enabled, TRUE) AS ProductMatchingEnabled,
                COALESCE(p.extra_exclude_keywords, '{}') AS ExtraExcludeKeywords,
                p.priority_override AS PriorityOverride
            FROM pharmacy_knowledge_adoption a
            LEFT JOIN pharmacy_tenant_knowledge_policy p
                ON p.adoption_id = a.id AND p.rule_version = a.rule_version
            WHERE a.tenant_id = @TenantId
            ORDER BY a.rule_code
            """,
            new { TenantId = tenantId });
        return new PharmacyTenantKnowledgeCatalogDto(
            versions.Select(Map).ToList(),
            pins.Select(MapPin).ToList());
    }

    public async Task<PharmacyKnowledgeVersionDto?> GetVisibleToTenantAsync(
        Guid tenantId,
        string ruleCode,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Thiếu nhà thuốc.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<VersionRow>(
            VersionSelect + """
             WHERE v.rule_code = @RuleCode
               AND v.version = @Version
               AND (
                    v.lifecycle_status = 'PUBLISHED'
                    OR EXISTS (
                        SELECT 1
                        FROM pharmacy_knowledge_adoption a
                        WHERE a.tenant_id = @TenantId
                          AND a.rule_code = v.rule_code
                          AND a.rule_version = v.version
                    )
               )
            """,
            new { TenantId = tenantId, RuleCode = Require(ruleCode), Version = Require(version) });
        return row is null ? null : Map(row);
    }

    public Task<PharmacyKnowledgeVersionDto> PublishAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(ruleCode, version, userId, PharmacyKnowledgeApproval.Published, cancellationToken);

    public Task<PharmacyKnowledgeVersionDto> RetireAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(ruleCode, version, userId, PharmacyKnowledgeApproval.Retired, cancellationToken);

    public Task<PharmacyKnowledgeVersionDto> SubmitReviewAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(ruleCode, version, userId, PharmacyKnowledgeApproval.InReview, cancellationToken);

    public Task<PharmacyKnowledgeVersionDto> ReturnToDraftAsync(
        string ruleCode,
        string version,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(ruleCode, version, userId, PharmacyKnowledgeApproval.Draft, cancellationToken);

    public async Task<PharmacyKnowledgeVersionDto> VerifySourceAsync(
        string ruleCode,
        string version,
        Guid userId,
        PharmacyKnowledgeSourceVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new InvalidOperationException("Thiếu nội dung nguồn.");
        if (userId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người kiểm chứng.");

        var status = PharmacyKnowledgeSourceRules.NormalizeStatus(request.Status);
        PharmacyKnowledgeSourceRules.EnsureVerificationRequest(
            status,
            request.ReferenceSource,
            request.SourceIdentity,
            request.SourceUrl,
            request.DocumentRef,
            request.IssuingOrganization,
            request.EvidenceNote);

        var code = Require(ruleCode);
        var ver = Require(version);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var lifecycle = await conn.QuerySingleOrDefaultAsync<string>(
            """
            SELECT lifecycle_status
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @RuleCode AND version = @Version
            FOR UPDATE
            """,
            new { RuleCode = code, Version = ver },
            tx);
        if (lifecycle is null)
            throw new InvalidOperationException("Không tìm thấy phiên bản.");
        PharmacyKnowledgeSourceRules.EnsureEditable(lifecycle);

        await conn.ExecuteAsync(
            """
            UPDATE pharmacy_knowledge_rule_version
            SET reference_source = @ReferenceSource,
                source_identity = @SourceIdentity,
                source_url = @SourceUrl,
                document_ref = @DocumentRef,
                issuing_organization = @IssuingOrganization,
                source_published_on = @SourcePublishedOn,
                evidence_note = @EvidenceNote,
                source_verification_status = @Status,
                verified_by = @VerifiedBy,
                verified_at = NOW(),
                updated_at = NOW()
            WHERE rule_code = @RuleCode AND version = @Version
            """,
            new
            {
                RuleCode = code,
                Version = ver,
                Status = status,
                ReferenceSource = Clean(request.ReferenceSource),
                SourceIdentity = Clean(request.SourceIdentity),
                SourceUrl = Clean(request.SourceUrl),
                DocumentRef = Clean(request.DocumentRef),
                IssuingOrganization = Clean(request.IssuingOrganization),
                SourcePublishedOn = request.SourcePublishedOn?.Date,
                EvidenceNote = Clean(request.EvidenceNote),
                VerifiedBy = userId.ToString(),
            },
            tx);
        var row = await conn.QuerySingleAsync<VersionRow>(
            VersionSelect + " WHERE v.rule_code = @RuleCode AND v.version = @Version",
            new { RuleCode = code, Version = ver },
            tx);
        await tx.CommitAsync(cancellationToken);
        return Map(row);
    }

    public async Task<PharmacyKnowledgeVersionDto> UpdateUnpublishedContentAsync(
        string ruleCode,
        string version,
        PharmacyKnowledgeContentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReasonVi))
            throw new InvalidOperationException("Thiếu nội dung phiên bản.");

        var code = Require(ruleCode);
        var ver = Require(version);
        var keywords = CleanList(request.Keywords);
        var excludes = CleanList(request.ExcludeKeywords);
        var categories = CleanList(request.CategoryCodes);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var current = await conn.QuerySingleOrDefaultAsync<ContentRow>(
            """
            SELECT
                lifecycle_status AS LifecycleStatus,
                reason_vi AS ReasonVi,
                symptom_id AS SymptomId,
                keywords AS Keywords,
                exclude_keywords AS ExcludeKeywords,
                category_codes AS CategoryCodes
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @RuleCode AND version = @Version
            FOR UPDATE
            """,
            new { RuleCode = code, Version = ver },
            tx);
        if (current is null)
            throw new InvalidOperationException("Không tìm thấy phiên bản.");
        PharmacyKnowledgeSourceRules.EnsureEditable(current.LifecycleStatus);

        var symptomId = PharmacyKnowledgeSourceRules.SymptomIdForContentEdit(request.SymptomId, current.SymptomId);
        var clears = PharmacyKnowledgeSourceRules.ClearsAcceptance(
            current.ReasonVi,
            request.ReasonVi.Trim(),
            current.SymptomId,
            symptomId,
            current.Keywords,
            keywords,
            current.ExcludeKeywords,
            excludes,
            current.CategoryCodes,
            categories);
        await conn.ExecuteAsync(
            """
            UPDATE pharmacy_knowledge_rule_version
            SET reason_vi = @ReasonVi,
                symptom_id = @SymptomId,
                keywords = @Keywords,
                exclude_keywords = @ExcludeKeywords,
                category_codes = @CategoryCodes,
                source_verification_status = CASE WHEN @Clears THEN 'UNVERIFIED' ELSE source_verification_status END,
                verified_by = CASE WHEN @Clears THEN NULL ELSE verified_by END,
                verified_at = CASE WHEN @Clears THEN NULL ELSE verified_at END,
                updated_at = NOW()
            WHERE rule_code = @RuleCode AND version = @Version
            """,
            new
            {
                RuleCode = code,
                Version = ver,
                ReasonVi = request.ReasonVi.Trim(),
                SymptomId = symptomId,
                Keywords = keywords,
                ExcludeKeywords = excludes,
                CategoryCodes = categories,
                Clears = clears,
            },
            tx);
        var row = await conn.QuerySingleAsync<VersionRow>(
            VersionSelect + " WHERE v.rule_code = @RuleCode AND v.version = @Version",
            new { RuleCode = code, Version = ver },
            tx);
        await tx.CommitAsync(cancellationToken);
        return Map(row);
    }

    public async Task<IReadOnlyList<PharmacyKnowledgePublicationEventDto>> ListHistoryAsync(
        string? ruleCode,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PharmacyKnowledgePublicationEventDto>(
            """
            SELECT
                rule_code AS RuleCode,
                rule_version AS Version,
                from_status AS FromStatus,
                to_status AS ToStatus,
                actor_user_id AS ActorUserId,
                created_at AS CreatedAt
            FROM pharmacy_knowledge_publication_event
            WHERE @RuleCode IS NULL OR rule_code = @RuleCode
            ORDER BY created_at
            """,
            new { RuleCode = string.IsNullOrWhiteSpace(ruleCode) ? null : Require(ruleCode) });
        return rows.AsList();
    }

    private async Task<PharmacyKnowledgeVersionDto> ChangeLifecycleAsync(
        string ruleCode,
        string version,
        Guid userId,
        string target,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người xuất bản.");

        var code = Require(ruleCode);
        var ver = Require(version);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var current = await conn.QuerySingleOrDefaultAsync<GateRow>(
            """
            SELECT
                lifecycle_status AS LifecycleStatus,
                source_verification_status AS SourceVerificationStatus,
                reference_source AS ReferenceSource,
                source_identity AS SourceIdentity,
                source_url AS SourceUrl,
                document_ref AS DocumentRef,
                issuing_organization AS IssuingOrganization,
                evidence_note AS EvidenceNote,
                verified_by AS VerifiedBy,
                verified_at AS VerifiedAt
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @RuleCode AND version = @Version
            FOR UPDATE
            """,
            new { RuleCode = code, Version = ver },
            tx);
        if (current is null)
            throw new InvalidOperationException("Không tìm thấy phiên bản.");

        var next = string.Equals(target, PharmacyKnowledgeApproval.Published, StringComparison.Ordinal)
            ? PharmacyKnowledgeSourceRules.EnsureMayPublish(
                code,
                current.LifecycleStatus,
                current.SourceVerificationStatus,
                current.ReferenceSource,
                current.SourceIdentity,
                current.SourceUrl,
                current.DocumentRef,
                current.IssuingOrganization,
                current.EvidenceNote,
                current.VerifiedBy,
                current.VerifiedAt)
            : PharmacyKnowledgeApproval.Transition(current.LifecycleStatus, target);
        await conn.ExecuteAsync(
            """
            UPDATE pharmacy_knowledge_rule_version
            SET lifecycle_status = @Lifecycle, updated_at = NOW()
            WHERE rule_code = @RuleCode AND version = @Version
            """,
            new { Lifecycle = next, RuleCode = code, Version = ver },
            tx);
        if (next is PharmacyKnowledgeApproval.Published or PharmacyKnowledgeApproval.Retired)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pharmacy_knowledge_publication_event (
                    rule_code, rule_version, from_status, to_status, actor_user_id)
                VALUES (@RuleCode, @Version, @FromStatus, @ToStatus, @Actor)
                """,
                new
                {
                    RuleCode = code,
                    Version = ver,
                    FromStatus = current.LifecycleStatus,
                    ToStatus = next,
                    Actor = userId.ToString(),
                },
                tx);
        }
        var row = await conn.QuerySingleAsync<VersionRow>(
            VersionSelect + " WHERE v.rule_code = @RuleCode AND v.version = @Version",
            new { RuleCode = code, Version = ver },
            tx);
        await tx.CommitAsync(cancellationToken);
        return Map(row);
    }

    private static PharmacyKnowledgeVersionDto Map(VersionRow row) =>
        new(
            row.RuleCode,
            row.Version,
            row.LifecycleStatus,
            row.Source,
            row.EffectiveFrom,
            row.EffectiveTo,
            row.SymptomCode,
            row.CategoryCodes ?? [],
            row.Keywords ?? [],
            row.ExcludeKeywords ?? [],
            row.ReasonVi,
            row.Priority,
            string.IsNullOrWhiteSpace(row.SourceVerificationStatus)
                ? PharmacyKnowledgeSourceRules.Unverified
                : row.SourceVerificationStatus,
            row.ReferenceSource,
            row.SourceIdentity,
            row.SourceUrl,
            row.DocumentRef,
            row.IssuingOrganization,
            row.SourcePublishedOn,
            row.EvidenceNote,
            row.VerifiedBy,
            row.VerifiedAt);

    private static PharmacyKnowledgeAdoptionViewDto MapPin(AdoptionRow row) =>
        new(
            row.RuleCode,
            row.RuleVersion,
            row.Status,
            row.ReferenceSource,
            row.ProductMatchingEnabled,
            row.ExtraExcludeKeywords ?? [],
            row.PriorityOverride);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] CleanList(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();

    private static string Require(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
            throw new InvalidOperationException("Thiếu mã.");
        return text;
    }

    private sealed class VersionRow
    {
        public string RuleCode { get; set; } = "";
        public string Version { get; set; } = "";
        public string LifecycleStatus { get; set; } = "";
        public string Source { get; set; } = "";
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? SymptomCode { get; set; }
        public string[]? CategoryCodes { get; set; }
        public string[]? Keywords { get; set; }
        public string[]? ExcludeKeywords { get; set; }
        public string ReasonVi { get; set; } = "";
        public int Priority { get; set; }
        public string? ReferenceSource { get; set; }
        public string? SourceIdentity { get; set; }
        public string? SourceUrl { get; set; }
        public string? DocumentRef { get; set; }
        public string? IssuingOrganization { get; set; }
        public DateTime? SourcePublishedOn { get; set; }
        public string? EvidenceNote { get; set; }
        public string SourceVerificationStatus { get; set; } = PharmacyKnowledgeSourceRules.Unverified;
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }

    private sealed class GateRow
    {
        public string LifecycleStatus { get; set; } = "";
        public string? SourceVerificationStatus { get; set; }
        public string? ReferenceSource { get; set; }
        public string? SourceIdentity { get; set; }
        public string? SourceUrl { get; set; }
        public string? DocumentRef { get; set; }
        public string? IssuingOrganization { get; set; }
        public string? EvidenceNote { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }

    private sealed class ContentRow
    {
        public string LifecycleStatus { get; set; } = "";
        public string ReasonVi { get; set; } = "";
        public Guid? SymptomId { get; set; }
        public string[]? Keywords { get; set; }
        public string[]? ExcludeKeywords { get; set; }
        public string[]? CategoryCodes { get; set; }
    }

    private sealed class AdoptionRow
    {
        public string RuleCode { get; set; } = "";
        public string RuleVersion { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ReferenceSource { get; set; }
        public bool ProductMatchingEnabled { get; set; } = true;
        public string[]? ExtraExcludeKeywords { get; set; }
        public int? PriorityOverride { get; set; }
    }
}
