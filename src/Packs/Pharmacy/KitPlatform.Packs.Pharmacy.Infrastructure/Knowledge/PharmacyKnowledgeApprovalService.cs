using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Npgsql;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed class PharmacyKnowledgeApprovalService : IPharmacyKnowledgeApprovalService
{
    private readonly IDbConnectionFactory _db;

    public PharmacyKnowledgeApprovalService(IDbConnectionFactory db) => _db = db;

    public async Task<PharmacyKnowledgeRuleApprovalDto?> TransitionAsync(
        string ruleCode,
        PharmacyKnowledgeApprovalRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new InvalidOperationException("Thiếu nội dung duyệt.");
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            throw new InvalidOperationException("Thiếu nhà thuốc hoặc người duyệt.");
        if (request.AddsKeywords || request.AddsCategories)
            throw new InvalidOperationException("Chính sách nhà thuốc không được mở rộng nội dung quy tắc.");

        var code = RequireCode(ruleCode);
        var target = PharmacyKnowledgeApproval.NormalizeAdoption(request.Status);
        var version = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim();
        if (target == PharmacyKnowledgeApproval.Approved && version is null)
            throw new InvalidOperationException("Thiếu phiên bản cần ghim.");

        var referenceSource = string.IsNullOrWhiteSpace(request.ReferenceSource)
            ? null
            : request.ReferenceSource.Trim();
        if (referenceSource is { Length: > 500 })
            throw new InvalidOperationException("Nguồn tham chiếu vượt quá 500 ký tự.");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            try
            {
                var dto = await PinAsync(conn, tx, code, version, target, referenceSource, tenantId, userId);
                await tx.CommitAsync(cancellationToken);
                return dto;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && attempt == 0)
            {
                await tx.RollbackAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException("Pin vừa đổi. Tải lại.");
    }

    public async Task<PharmacyKnowledgePolicyDto> SavePolicyAsync(
        string ruleCode,
        PharmacyKnowledgePolicyRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new InvalidOperationException("Thiếu chính sách.");
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            throw new InvalidOperationException("Thiếu nhà thuốc hoặc người duyệt.");

        var code = RequireCode(ruleCode);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var pin = await conn.QuerySingleOrDefaultAsync<PinRow>(
            """
            SELECT id AS Id, rule_version AS RuleVersion, status AS Status
            FROM pharmacy_knowledge_adoption
            WHERE tenant_id = @TenantId AND rule_code = @RuleCode
            FOR UPDATE
            """,
            new { TenantId = tenantId, RuleCode = code },
            tx);
        if (pin is null || pin.Status != PharmacyKnowledgeApproval.Approved)
            throw new InvalidOperationException("Nhà thuốc chưa ghim phiên bản này.");

        var version = await conn.QuerySingleOrDefaultAsync<VersionRow>(
            """
            SELECT lifecycle_status AS LifecycleStatus, priority AS Priority
            FROM pharmacy_knowledge_rule_version
            WHERE rule_code = @RuleCode AND version = @Version
            """,
            new { RuleCode = code, Version = pin.RuleVersion },
            tx) ?? throw new InvalidOperationException("Phiên bản đã ghim không còn tồn tại.");

        PharmacyKnowledgeApproval.RejectPolicyExpansion(
            request.AddsKeywords,
            request.AddsCategories,
            version.Priority,
            request.PriorityOverride);

        var excludes = (request.ExtraExcludeKeywords ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var saved = await conn.QuerySingleAsync<PolicyRow>(
            """
            INSERT INTO pharmacy_tenant_knowledge_policy (
                adoption_id, rule_version, product_matching_enabled, extra_exclude_keywords, priority_override)
            VALUES (@AdoptionId, @RuleVersion, @Enabled, @Excludes, @PriorityOverride)
            ON CONFLICT (adoption_id) DO UPDATE
            SET rule_version = EXCLUDED.rule_version,
                product_matching_enabled = EXCLUDED.product_matching_enabled,
                extra_exclude_keywords = EXCLUDED.extra_exclude_keywords,
                priority_override = EXCLUDED.priority_override,
                updated_at = NOW()
            RETURNING
                rule_version AS RuleVersion,
                product_matching_enabled AS ProductMatchingEnabled,
                extra_exclude_keywords AS ExtraExcludeKeywords,
                priority_override AS PriorityOverride
            """,
            new
            {
                AdoptionId = pin.Id,
                RuleVersion = pin.RuleVersion,
                Enabled = request.ProductMatchingEnabled,
                Excludes = excludes,
                PriorityOverride = request.PriorityOverride,
            },
            tx);
        await tx.CommitAsync(cancellationToken);
        return new PharmacyKnowledgePolicyDto(
            code,
            saved.RuleVersion,
            saved.ProductMatchingEnabled,
            saved.ExtraExcludeKeywords ?? [],
            saved.PriorityOverride);
    }

    public async Task<IReadOnlyList<PharmacyKnowledgeApprovalEventDto>> ListHistoryAsync(
        string ruleCode,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PharmacyKnowledgeApprovalEventDto>(
            """
            SELECT
                rule_code AS RuleCode,
                rule_version AS Version,
                from_status AS FromStatus,
                to_status AS ToStatus,
                actor_user_id AS ActorUserId,
                reference_source AS ReferenceSource,
                created_at AS CreatedAt
            FROM pharmacy_knowledge_approval_event
            WHERE rule_code = @RuleCode
              AND tenant_id = @TenantId
            ORDER BY created_at
            """,
            new { RuleCode = RequireCode(ruleCode), TenantId = tenantId });
        return rows.AsList();
    }

    private static async Task<PharmacyKnowledgeRuleApprovalDto> PinAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        string code,
        string? version,
        string target,
        string? referenceSource,
        Guid tenantId,
        Guid userId)
    {
        var existing = await conn.QuerySingleOrDefaultAsync<PinRow>(
            """
            SELECT id AS Id,
                   rule_version AS RuleVersion,
                   status AS Status,
                   reference_source AS ReferenceSource,
                   approved_by AS ApprovedBy,
                   approved_at AS ApprovedAt
            FROM pharmacy_knowledge_adoption
            WHERE tenant_id = @TenantId AND rule_code = @RuleCode
            FOR UPDATE
            """,
            new { TenantId = tenantId, RuleCode = code },
            tx);

        var pinnedVersion = target == PharmacyKnowledgeApproval.Approved
            ? version!
            : existing?.RuleVersion ?? version;
        if (pinnedVersion is null)
            throw new InvalidOperationException("Nhà thuốc chưa có pin để thu hồi.");

        if (target == PharmacyKnowledgeApproval.Approved)
        {
            var head = await conn.QuerySingleOrDefaultAsync<VersionRow>(
                """
                SELECT lifecycle_status AS LifecycleStatus,
                       effective_from AS EffectiveFrom,
                       effective_to AS EffectiveTo,
                       priority AS Priority,
                       source_verification_status AS SourceVerificationStatus,
                       CURRENT_DATE AS Today
                FROM pharmacy_knowledge_rule_version
                WHERE rule_code = @RuleCode AND version = @Version
                """,
                new { RuleCode = code, Version = pinnedVersion },
                tx);
            if (head is null)
                throw new InvalidOperationException("Không tìm thấy phiên bản.");
            PharmacyKnowledgeApproval.EnsureVersionMayBePinned(
                code,
                head.LifecycleStatus,
                head.EffectiveFrom,
                head.EffectiveTo,
                head.Today,
                head.SourceVerificationStatus);
        }

        var keptReference = PharmacyKnowledgeApproval.PreserveReference(existing?.ReferenceSource, referenceSource);
        if (target == PharmacyKnowledgeApproval.Approved && string.IsNullOrWhiteSpace(keptReference))
            throw new InvalidOperationException("Duyệt tri thức cần nguồn tham chiếu.");

        var unchanged = existing is not null
            && existing.Status == target
            && existing.RuleVersion == pinnedVersion;
        if (unchanged)
        {
            return new PharmacyKnowledgeRuleApprovalDto(
                code,
                existing!.Status,
                existing.ApprovedBy,
                existing.ApprovedAt,
                existing.ReferenceSource,
                existing.RuleVersion);
        }

        var fromStatus = existing?.Status ?? PharmacyKnowledgeApproval.None;
        var row = existing is null
            ? await conn.QuerySingleAsync<PinRow>(
                """
                INSERT INTO pharmacy_knowledge_adoption (
                    tenant_id, rule_code, rule_version, status, approved_by, approved_at, reference_source)
                VALUES (
                    @TenantId, @RuleCode, @Version, @Status,
                    CASE WHEN @Status = 'APPROVED' THEN @UserId ELSE NULL END,
                    CASE WHEN @Status = 'APPROVED' THEN NOW() ELSE NULL END,
                    @ReferenceSource)
                RETURNING id AS Id,
                          rule_version AS RuleVersion,
                          status AS Status,
                          approved_by AS ApprovedBy,
                          approved_at AS ApprovedAt,
                          reference_source AS ReferenceSource
                """,
                new
                {
                    TenantId = tenantId,
                    RuleCode = code,
                    Version = pinnedVersion,
                    Status = target,
                    UserId = userId.ToString(),
                    ReferenceSource = keptReference,
                },
                tx)
            : await conn.QuerySingleAsync<PinRow>(
                """
                UPDATE pharmacy_knowledge_adoption
                SET rule_version = @Version,
                    status = @Status,
                    approved_by = CASE WHEN @Status = 'APPROVED' THEN @UserId ELSE approved_by END,
                    approved_at = CASE WHEN @Status = 'APPROVED' THEN NOW() ELSE approved_at END,
                    reference_source = COALESCE(reference_source, @ReferenceSource),
                    updated_at = NOW()
                WHERE id = @Id AND tenant_id = @TenantId
                RETURNING id AS Id,
                          rule_version AS RuleVersion,
                          status AS Status,
                          approved_by AS ApprovedBy,
                          approved_at AS ApprovedAt,
                          reference_source AS ReferenceSource
                """,
                new
                {
                    existing.Id,
                    TenantId = tenantId,
                    Version = pinnedVersion,
                    Status = target,
                    UserId = userId.ToString(),
                    ReferenceSource = keptReference,
                },
                tx);

        if (existing is not null && existing.RuleVersion != row.RuleVersion)
        {
            await conn.ExecuteAsync(
                "DELETE FROM pharmacy_tenant_knowledge_policy WHERE adoption_id = @Id",
                new { row.Id },
                tx);
        }

        await conn.ExecuteAsync(
            """
            INSERT INTO pharmacy_knowledge_approval_event (
                rule_code, rule_version, from_status, to_status, actor_user_id, reference_source, tenant_id)
            VALUES (@RuleCode, @Version, @FromStatus, @ToStatus, @Actor, @ReferenceSource, @TenantId)
            """,
            new
            {
                RuleCode = code,
                Version = row.RuleVersion,
                FromStatus = fromStatus,
                ToStatus = row.Status,
                Actor = userId.ToString(),
                ReferenceSource = row.ReferenceSource,
                TenantId = tenantId,
            },
            tx);

        return new PharmacyKnowledgeRuleApprovalDto(
            code,
            row.Status,
            row.ApprovedBy,
            row.ApprovedAt,
            row.ReferenceSource,
            row.RuleVersion);
    }

    private static string RequireCode(string ruleCode)
    {
        var code = ruleCode.Trim();
        if (code.Length == 0)
            throw new InvalidOperationException("Thiếu mã quy tắc.");
        return code;
    }

    private sealed class PinRow
    {
        public Guid Id { get; set; }
        public string RuleVersion { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ReferenceSource { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    private sealed class VersionRow
    {
        public string LifecycleStatus { get; set; } = "";
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public DateTime Today { get; set; }
        public int Priority { get; set; }
        public string? SourceVerificationStatus { get; set; }
    }

    private sealed class PolicyRow
    {
        public string RuleVersion { get; set; } = "";
        public bool ProductMatchingEnabled { get; set; }
        public string[]? ExtraExcludeKeywords { get; set; }
        public int? PriorityOverride { get; set; }
    }
}
