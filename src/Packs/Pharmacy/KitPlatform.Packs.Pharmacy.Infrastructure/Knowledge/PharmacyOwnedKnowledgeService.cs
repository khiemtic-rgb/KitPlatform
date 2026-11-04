using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed partial class PharmacyOwnedKnowledgeService : IPharmacyOwnedKnowledgeService
{
    private readonly IDbConnectionFactory _db;

    public PharmacyOwnedKnowledgeService(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<PharmacyOwnedKnowledgeSummaryDto>> ListAsync(
        Guid tenantId,
        string? kind,
        CancellationToken cancellationToken = default,
        string? query = null,
        string? lifecycleStatus = null,
        string? classification = null)
    {
        RequireTenant(tenantId);
        var filter = string.IsNullOrWhiteSpace(kind) ? null : PharmacyOwnedKnowledgeRules.NormalizeKind(kind);
        var status = NormalizeLifecycleFilter(lifecycleStatus);
        var text = NormalizeSearch(query);
        var group = NormalizeSearch(classification);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PharmacyOwnedKnowledgeSummaryDto>(
            new CommandDefinition(
                """
                SELECT v.knowledge_id AS Id,
                       v.kind AS Kind,
                       v.title AS Title,
                       v.classification AS Classification,
                       v.version_no AS Version,
                       v.lifecycle_status AS LifecycleStatus,
                       v.updated_at AS UpdatedAt,
                       a.status AS ApplicationStatus,
                       v.review_due_on::timestamp AS ReviewDueOn,
                       COALESCE(v.review_due_on < @Today, FALSE) AS ReviewOverdue
                FROM pharmacy_owned_knowledge_version v
                LEFT JOIN pharmacy_owned_knowledge_activation a
                  ON a.version_id = v.id
                 AND a.tenant_id = v.tenant_id
                WHERE v.tenant_id = @TenantId
                  AND (@Kind IS NULL OR v.kind = @Kind)
                  AND (@Status IS NULL OR v.lifecycle_status = @Status)
                  AND (@Query IS NULL OR translate(v.title || ' ' || COALESCE(v.classification, '') || ' ' || v.body, @FoldFrom, @FoldTo) ILIKE @Query)
                  AND (@Group IS NULL OR translate(COALESCE(v.classification, ''), @FoldFrom, @FoldTo) ILIKE @Group)
                  AND v.version_no = (
                      SELECT MAX(version_no)
                      FROM pharmacy_owned_knowledge_version
                      WHERE knowledge_id = v.knowledge_id
                        AND tenant_id = v.tenant_id)
                ORDER BY v.updated_at DESC
                """,
                new
                {
                    TenantId = tenantId,
                    Kind = filter,
                    Status = status,
                    Query = text,
                    Group = group,
                    FoldFrom = PharmacyKnowledgeSearchText.SqlFrom,
                    FoldTo = PharmacyKnowledgeSearchText.SqlTo,
                    Today = Today(),
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public Task<IReadOnlyList<PharmacyOwnedKnowledgeInUseDto>> ListInUseAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        string? kind,
        string? query,
        string? classification,
        CancellationToken cancellationToken = default) =>
        QueryInUseAsync(tenantId, userId, isTenantAdmin, kind, query, classification, null, cancellationToken);

    public async Task<PharmacyOwnedKnowledgeInUseDto?> GetInUseAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid knowledgeId,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryInUseAsync(
            tenantId, userId, isTenantAdmin, null, null, null, knowledgeId, cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto?> GetAsync(
        Guid tenantId,
        Guid knowledgeId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await LoadAsync(conn, null, tenantId, knowledgeId, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> CreateAsync(
        PharmacyOwnedKnowledgeWriteRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool isTenantAdmin = true)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        var draft = Normalize(request);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.DraftAction, draft.Kind, cancellationToken);
        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_owned_knowledge (id, tenant_id, created_by)
                VALUES (@Id, @TenantId, @Actor)
                """,
                new { Id = id, TenantId = tenantId, Actor = userId.ToString() },
                tx,
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_owned_knowledge_version (
                    knowledge_id, tenant_id, version_no, kind, title, body, classification, lifecycle_status)
                VALUES (
                    @Id, @TenantId, 1, @Kind, @Title, @Body, @Classification, 'DRAFT')
                """,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    draft.Kind,
                    draft.Title,
                    draft.Body,
                    draft.Classification,
                },
                tx,
                cancellationToken: cancellationToken));
        await InsertEventAsync(conn, tx, tenantId, id, 1, "CREATED", draft, userId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, id, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> UpdateAsync(
        Guid knowledgeId,
        PharmacyOwnedKnowledgeWriteRequest request,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool isTenantAdmin = true)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        var draft = Normalize(request);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.DraftAction, draft.Kind, cancellationToken);
        var current = await conn.QuerySingleOrDefaultAsync<DraftHead>(
            new CommandDefinition(
                """
                SELECT lifecycle_status AS LifecycleStatus,
                       version_no AS Version
                FROM pharmacy_owned_knowledge_version
                WHERE knowledge_id = @Id
                  AND tenant_id = @TenantId
                  AND version_no = (
                      SELECT MAX(version_no)
                      FROM pharmacy_owned_knowledge_version
                      WHERE knowledge_id = @Id
                        AND tenant_id = @TenantId)
                """,
                new { Id = knowledgeId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
        if (current is null)
            throw new InvalidOperationException("Không tìm thấy tri thức của nhà thuốc.");
        PharmacyOwnedKnowledgeRules.EnsureDraft(current.LifecycleStatus);
        var updated = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pharmacy_owned_knowledge_version
                SET kind = @Kind,
                    title = @Title,
                    body = @Body,
                    classification = @Classification,
                    updated_at = NOW()
                WHERE knowledge_id = @Id
                  AND tenant_id = @TenantId
                  AND lifecycle_status = 'DRAFT'
                  AND version_no = (
                      SELECT MAX(version_no)
                      FROM pharmacy_owned_knowledge_version
                      WHERE knowledge_id = @Id
                        AND tenant_id = @TenantId)
                """,
                new
                {
                    Id = knowledgeId,
                    TenantId = tenantId,
                    draft.Kind,
                    draft.Title,
                    draft.Body,
                    draft.Classification,
                },
                tx,
                cancellationToken: cancellationToken));
        if (updated != 1)
            throw new InvalidOperationException("Chỉ sửa được bản nháp.");
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pharmacy_owned_knowledge
                SET updated_at = NOW()
                WHERE id = @Id
                  AND tenant_id = @TenantId
                """,
                new { Id = knowledgeId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
        await InsertEventAsync(conn, tx, tenantId, knowledgeId, current.Version, "UPDATED", draft, userId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, knowledgeId, cancellationToken);
    }

    private static async Task InsertEventAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        Guid tenantId,
        Guid knowledgeId,
        int version,
        string action,
        Draft draft,
        Guid userId,
        CancellationToken cancellationToken,
        string? note = null)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_owned_knowledge_event (
                    tenant_id, knowledge_id, version_no, action, kind, title, body, classification,
                    snapshot_fidelity, actor_user_id, note)
                VALUES (
                    @TenantId, @Id, @Version, @Action, @Kind, @Title, @Body, @Classification,
                    @Fidelity, @Actor, @Note)
                """,
                new
                {
                    TenantId = tenantId,
                    Id = knowledgeId,
                    Version = version,
                    Action = action,
                    draft.Kind,
                    draft.Title,
                    draft.Body,
                    draft.Classification,
                    Fidelity = PharmacyOwnedKnowledgeRules.ExactSnapshot,
                    Actor = userId.ToString(),
                    Note = note,
                },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task<PharmacyOwnedKnowledgeDetailDto> LoadRequiredAsync(
        Npgsql.NpgsqlConnection conn,
        Guid tenantId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var row = await LoadAsync(conn, null, tenantId, knowledgeId, cancellationToken);
        return row ?? throw new InvalidOperationException("Không tìm thấy tri thức của nhà thuốc.");
    }

    private static async Task<PharmacyOwnedKnowledgeDetailDto?> LoadAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction? tx,
        Guid tenantId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var version = await conn.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(
                """
                SELECT v.knowledge_id AS Id,
                       v.kind AS Kind,
                       v.title AS Title,
                       v.body AS Body,
                       v.classification AS Classification,
                       v.version_no AS Version,
                       v.lifecycle_status AS LifecycleStatus,
                       k.created_at AS CreatedAt,
                       v.updated_at AS UpdatedAt
                FROM pharmacy_owned_knowledge_version v
                JOIN pharmacy_owned_knowledge k
                  ON k.id = v.knowledge_id
                 AND k.tenant_id = v.tenant_id
                WHERE v.knowledge_id = @Id
                  AND v.tenant_id = @TenantId
                  AND v.version_no = (
                      SELECT MAX(version_no)
                      FROM pharmacy_owned_knowledge_version
                      WHERE knowledge_id = @Id
                        AND tenant_id = @TenantId)
                """,
                new { Id = knowledgeId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
        if (version is null)
            return null;
        var history = (await conn.QueryAsync<PharmacyOwnedKnowledgeEventDto>(
            new CommandDefinition(
                """
                SELECT id AS Id,
                       action AS Action,
                       version_no AS Version,
                       kind AS Kind,
                       title AS Title,
                       CASE WHEN snapshot_fidelity = 'EXACT' THEN body END AS Body,
                       CASE WHEN snapshot_fidelity = 'EXACT' THEN classification END AS Classification,
                       snapshot_fidelity AS SnapshotFidelity,
                       actor_user_id AS ActorUserId,
                       created_at AS CreatedAt,
                       note AS Note
                FROM pharmacy_owned_knowledge_event
                WHERE knowledge_id = @Id
                  AND tenant_id = @TenantId
                ORDER BY created_at, id
                """,
                new { Id = knowledgeId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken))).ToList();
        return new PharmacyOwnedKnowledgeDetailDto(
            version.Id,
            version.Kind,
            version.Title,
            version.Body,
            version.Classification,
            version.Version,
            version.LifecycleStatus,
            version.CreatedAt,
            version.UpdatedAt,
            history,
            await LoadVersionsAsync(conn, tx, tenantId, knowledgeId, cancellationToken),
            await LoadActivationsAsync(conn, tx, tenantId, knowledgeId, cancellationToken));
    }

    private static Draft Normalize(PharmacyOwnedKnowledgeWriteRequest request)
    {
        if (request is null)
            throw new InvalidOperationException("Thiếu nội dung tri thức.");
        return new Draft(
            PharmacyOwnedKnowledgeRules.NormalizeKind(request.Kind),
            PharmacyOwnedKnowledgeRules.RequireTitle(request.Title),
            PharmacyOwnedKnowledgeRules.RequireBody(request.Body),
            PharmacyOwnedKnowledgeRules.NormalizeClassification(request.Classification));
    }

    private static void RequireTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Thiếu nhà thuốc.");
    }

    private async Task<IReadOnlyList<PharmacyOwnedKnowledgeInUseDto>> QueryInUseAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        string? kind,
        string? query,
        string? classification,
        Guid? knowledgeId,
        CancellationToken cancellationToken)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        var filter = string.IsNullOrWhiteSpace(kind) ? null : PharmacyOwnedKnowledgeRules.NormalizeKind(kind);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PharmacyOwnedKnowledgeInUseDto>(
            new CommandDefinition(
                """
                SELECT v.knowledge_id AS Id,
                       v.kind AS Kind,
                       v.title AS Title,
                       v.classification AS Classification,
                       v.version_no AS Version,
                       a.status AS Status,
                       v.body AS Body,
                       v.updated_at AS UpdatedAt,
                       v.review_due_on::timestamp AS ReviewDueOn,
                       COALESCE(v.review_due_on < @Today, FALSE) AS ReviewOverdue
                FROM pharmacy_owned_knowledge_activation a
                JOIN pharmacy_owned_knowledge_version v
                  ON v.id = a.version_id
                 AND v.tenant_id = a.tenant_id
                WHERE a.tenant_id = @TenantId
                  AND a.status = 'ACTIVE'
                  AND v.lifecycle_status = 'APPROVED'
                  AND v.kind <> 'CLINICAL_KNOWLEDGE'
                  AND (@Kind IS NULL OR v.kind = @Kind)
                  AND (@KnowledgeId IS NULL OR v.knowledge_id = @KnowledgeId)
                  AND (@Query IS NULL OR translate(v.title || ' ' || COALESCE(v.classification, '') || ' ' || v.body, @FoldFrom, @FoldTo) ILIKE @Query)
                  AND (@Group IS NULL OR translate(COALESCE(v.classification, ''), @FoldFrom, @FoldTo) ILIKE @Group)
                  AND (
                      @IsAdmin
                      OR EXISTS (
                          SELECT 1
                          FROM pharmacy_owned_knowledge_grant g
                          WHERE g.tenant_id = a.tenant_id
                            AND g.user_id = @UserId
                            AND g.revoked_at IS NULL
                            AND g.action = 'read'
                            AND g.kind = v.kind))
                ORDER BY v.title
                """,
                new
                {
                    TenantId = tenantId,
                    UserId = userId,
                    IsAdmin = isTenantAdmin,
                    Kind = filter,
                    KnowledgeId = knowledgeId,
                    Query = NormalizeSearch(query),
                    Group = NormalizeSearch(classification),
                    FoldFrom = PharmacyKnowledgeSearchText.SqlFrom,
                    FoldTo = PharmacyKnowledgeSearchText.SqlTo,
                    Today = Today(),
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static string? NormalizeLifecycleFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        var value = status.Trim().ToUpperInvariant();
        if (value is "DRAFT" or "IN_REVIEW" or "APPROVED")
            return value;
        throw new InvalidOperationException("Trạng thái không hợp lệ.");
    }

    private static string? NormalizeSearch(string? value) => PharmacyKnowledgeSearchText.Pattern(value);

    private static DateTime Today() => PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow);

    private static void RequireUser(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người soạn.");
    }

    private sealed class DraftHead
    {
        public string LifecycleStatus { get; set; } = "";
        public int Version { get; set; }
    }

    private sealed class VersionRow
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string? Classification { get; set; }
        public int Version { get; set; }
        public string LifecycleStatus { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private readonly record struct Draft(string Kind, string Title, string Body, string? Classification);
}
