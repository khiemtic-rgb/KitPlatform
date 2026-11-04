using Dapper;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed partial class PharmacyOwnedKnowledgeService
{
    public async Task<PharmacyOwnedKnowledgeDetailDto> SubmitAsync(
        Guid knowledgeId,
        Guid versionId,
        DateTime expectedUpdatedAt,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var version = await RequireVersionAsync(conn, tx, tenantId, knowledgeId, versionId, cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.SubmitAction, version.Kind, cancellationToken);
        if (!string.Equals(version.LifecycleStatus, PharmacyOwnedKnowledgeRules.Draft, StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ gửi được bản nháp.");
        PharmacyOwnedKnowledgeDocument.EnsureSubmittable(version.Kind, version.Body);
        await TransitionVersionAsync(conn, tx, version, PharmacyOwnedKnowledgeRules.Draft, "IN_REVIEW", expectedUpdatedAt, cancellationToken);
        await InsertEventAsync(conn, tx, tenantId, knowledgeId, version.Version, "SUBMITTED", version.ToDraft(), userId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, knowledgeId, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> ReturnAsync(
        Guid knowledgeId,
        Guid versionId,
        DateTime expectedUpdatedAt,
        string? note,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        var comment = note?.Trim();
        if (string.IsNullOrWhiteSpace(comment) || comment.Length > 2000)
            throw new InvalidOperationException("Cần ghi chú khi trả lại, tối đa 2000 ký tự.");
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var version = await RequireVersionAsync(conn, tx, tenantId, knowledgeId, versionId, cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.ApproveAction, version.Kind, cancellationToken);
        if (!string.Equals(version.LifecycleStatus, "IN_REVIEW", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ trả lại bản đang chờ duyệt.");
        await TransitionVersionAsync(conn, tx, version, "IN_REVIEW", PharmacyOwnedKnowledgeRules.Draft, expectedUpdatedAt, cancellationToken);
        await InsertEventAsync(conn, tx, tenantId, knowledgeId, version.Version, "RETURNED", version.ToDraft(), userId, cancellationToken, comment);
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, knowledgeId, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> ApproveAsync(
        Guid knowledgeId,
        Guid versionId,
        DateTime expectedUpdatedAt,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default,
        DateTime? reviewDueOn = null)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        var dueOn = PharmacyKnowledgeReviewDue.Resolve(
            reviewDueOn, PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow));
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var version = await RequireVersionAsync(conn, tx, tenantId, knowledgeId, versionId, cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.ApproveAction, version.Kind, cancellationToken);
        if (!string.Equals(version.LifecycleStatus, "IN_REVIEW", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ duyệt được bản đang chờ duyệt.");
        var authors = await conn.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT actor_user_id
                FROM pharmacy_owned_knowledge_event
                WHERE tenant_id = @TenantId
                  AND knowledge_id = @KnowledgeId
                  AND version_no = @Version
                  AND action IN ('CREATED', 'UPDATED', 'SUBMITTED')
                """,
                new { TenantId = tenantId, KnowledgeId = knowledgeId, version.Version },
                tx,
                cancellationToken: cancellationToken));
        PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor(userId, authors);
        await TransitionVersionAsync(conn, tx, version, "IN_REVIEW", "APPROVED", expectedUpdatedAt, cancellationToken);
        await SetReviewDueAsync(conn, tx, version, dueOn, cancellationToken);
        await InsertEventAsync(conn, tx, tenantId, knowledgeId, version.Version, "APPROVED", version.ToDraft(), userId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, knowledgeId, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> StartNextDraftAsync(
        Guid knowledgeId,
        Guid sourceVersionId,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var source = await RequireVersionAsync(conn, tx, tenantId, knowledgeId, sourceVersionId, cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.DraftAction, source.Kind, cancellationToken);
        if (!string.Equals(source.LifecycleStatus, "APPROVED", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ mở bản nháp mới từ nội dung đã duyệt.");
        var open = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM pharmacy_owned_knowledge_version
                WHERE tenant_id = @TenantId
                  AND knowledge_id = @Id
                  AND lifecycle_status IN ('DRAFT', 'IN_REVIEW')
                """,
                new { TenantId = tenantId, Id = knowledgeId },
                tx,
                cancellationToken: cancellationToken));
        if (open > 0)
            throw new InvalidOperationException("Đang có một bản nháp hoặc bản chờ duyệt.");
        var next = source.Version + 1;
        try
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pharmacy_owned_knowledge_version (
                        knowledge_id, tenant_id, version_no, kind, title, body, classification, lifecycle_status)
                    VALUES (
                        @Id, @TenantId, @Version, @Kind, @Title, @Body, @Classification, 'DRAFT')
                    """,
                    new
                    {
                        Id = knowledgeId,
                        TenantId = tenantId,
                        Version = next,
                        source.Kind,
                        source.Title,
                        source.Body,
                        source.Classification,
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException("Đang có một bản nháp hoặc bản chờ duyệt.");
        }

        await InsertEventAsync(conn, tx, tenantId, knowledgeId, next, "CREATED", source.ToDraft(), userId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, knowledgeId, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> ActivateAsync(
        Guid knowledgeId,
        Guid versionId,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default,
        DateTime? reviewDueOn = null)
    {
        return await ChangeActivationAsync(
            knowledgeId, versionId, null, "ACTIVATED", tenantId, userId, isTenantAdmin, cancellationToken, reviewDueOn);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> StopAsync(
        Guid knowledgeId,
        Guid versionId,
        string? note,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default)
    {
        return await ChangeActivationAsync(
            knowledgeId, versionId, note, "STOPPED", tenantId, userId, isTenantAdmin, cancellationToken);
    }

    public async Task<PharmacyOwnedKnowledgeDetailDto> ResumeAsync(
        Guid knowledgeId,
        Guid versionId,
        string? note,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken = default)
    {
        return await ChangeActivationAsync(
            knowledgeId, versionId, note, "RESUMED", tenantId, userId, isTenantAdmin, cancellationToken);
    }

    public async Task GrantAsync(
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        Guid granteeUserId,
        string? action,
        string? kind,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireUser(actorUserId);
        if (!isTenantAdmin)
            throw new InvalidOperationException("Chỉ quản trị nhà thuốc mới cấp quyền tri thức nội bộ.");
        var grantAction = NormalizeGrantAction(action);
        var grantKind = PharmacyOwnedKnowledgeRules.NormalizeKind(kind);
        if (grantAction == PharmacyOwnedKnowledgeAccess.ActivateAction
            && grantKind == PharmacyOwnedKnowledgeRules.ClinicalKnowledge)
            throw new InvalidOperationException("Không cấp quyền kích hoạt tri thức lâm sàng.");
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_owned_knowledge_grant (
                    tenant_id, user_id, action, kind, granted_by)
                VALUES (@TenantId, @UserId, @Action, @Kind, @Actor)
                ON CONFLICT (tenant_id, user_id, action, kind)
                DO UPDATE SET revoked_at = NULL, granted_by = EXCLUDED.granted_by, created_at = NOW()
                """,
                new
                {
                    TenantId = tenantId,
                    UserId = granteeUserId,
                    Action = grantAction,
                    Kind = grantKind,
                    Actor = actorUserId.ToString(),
                },
                cancellationToken: cancellationToken));
    }

    private async Task<PharmacyOwnedKnowledgeDetailDto> ChangeActivationAsync(
        Guid knowledgeId,
        Guid versionId,
        string? note,
        string action,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        CancellationToken cancellationToken,
        DateTime? reviewDueOn = null)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        var comment = note?.Trim();
        if (action != "ACTIVATED" && (string.IsNullOrWhiteSpace(comment) || comment!.Length > 2000))
            throw new InvalidOperationException("Cần ghi chú khi ngừng hoặc áp dụng lại.");
        var dueOn = action == "ACTIVATED"
            ? PharmacyKnowledgeReviewDue.Resolve(reviewDueOn, PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow))
            : (DateTime?)null;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var version = await RequireVersionAsync(conn, tx, tenantId, knowledgeId, versionId, cancellationToken);
        await EnsureActorAsync(conn, tx, tenantId, userId, isTenantAdmin, PharmacyOwnedKnowledgeAccess.ActivateAction, version.Kind, cancellationToken);
        if (!string.Equals(version.LifecycleStatus, "APPROVED", StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ áp dụng được nội dung đã duyệt.");
        if (version.Kind == PharmacyOwnedKnowledgeRules.ClinicalKnowledge)
            throw new InvalidOperationException("Tri thức lâm sàng không được đưa vào sử dụng.");
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                SELECT id
                FROM pharmacy_owned_knowledge_version
                WHERE id = @VersionId
                  AND tenant_id = @TenantId
                  AND knowledge_id = @KnowledgeId
                FOR UPDATE
                """,
                new { VersionId = versionId, TenantId = tenantId, KnowledgeId = knowledgeId },
                tx,
                cancellationToken: cancellationToken));

        if (action == "ACTIVATED")
        {
            int inserted;
            try
            {
                inserted = await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pharmacy_owned_knowledge_activation (
                        tenant_id, knowledge_id, version_id, status)
                    SELECT @TenantId, @KnowledgeId, id, 'ACTIVE'
                    FROM pharmacy_owned_knowledge_version
                    WHERE id = @VersionId
                      AND tenant_id = @TenantId
                      AND knowledge_id = @KnowledgeId
                      AND lifecycle_status = 'APPROVED'
                      AND kind <> 'CLINICAL_KNOWLEDGE'
                      AND NOT EXISTS (
                          SELECT 1 FROM pharmacy_owned_knowledge_activation existing
                          WHERE existing.version_id = @VersionId
                            AND existing.tenant_id = @TenantId)
                    """,
                    new { TenantId = tenantId, KnowledgeId = knowledgeId, VersionId = versionId },
                    tx,
                    cancellationToken: cancellationToken));
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                throw new InvalidOperationException("Thao tác vừa được người khác cập nhật.");
            }

            if (inserted != 1)
                throw new InvalidOperationException("Thao tác vừa được người khác cập nhật.");
            await SetReviewDueAsync(conn, tx, version, dueOn!.Value, cancellationToken);
        }
        else
        {
            var target = action == "STOPPED" ? "ACTIVE" : "STOPPED";
            var next = action == "STOPPED" ? "STOPPED" : "ACTIVE";
            var updated = await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pharmacy_owned_knowledge_activation
                    SET status = @Next, updated_at = NOW()
                    WHERE tenant_id = @TenantId
                      AND knowledge_id = @KnowledgeId
                      AND version_id = @VersionId
                      AND status = @Current
                    """,
                    new
                    {
                        Next = next,
                        TenantId = tenantId,
                        KnowledgeId = knowledgeId,
                        VersionId = versionId,
                        Current = target,
                    },
                    tx,
                    cancellationToken: cancellationToken));
            if (updated != 1)
                throw new InvalidOperationException("Thao tác vừa được người khác cập nhật.");
        }

        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_owned_knowledge_activation_event (
                    tenant_id, knowledge_id, version_id, action, note, actor_user_id)
                VALUES (@TenantId, @KnowledgeId, @VersionId, @Action, @Note, @Actor)
                """,
                new
                {
                    TenantId = tenantId,
                    KnowledgeId = knowledgeId,
                    VersionId = versionId,
                    Action = action,
                    Note = string.IsNullOrWhiteSpace(comment) ? null : comment,
                    Actor = userId.ToString(),
                },
                tx,
                cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return await LoadRequiredAsync(conn, tenantId, knowledgeId, cancellationToken);
    }

    private static async Task TransitionVersionAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        GovernedVersion version,
        string from,
        string to,
        DateTime expectedUpdatedAt,
        CancellationToken cancellationToken)
    {
        var updated = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pharmacy_owned_knowledge_version
                SET lifecycle_status = @To, updated_at = NOW()
                WHERE id = @VersionId
                  AND tenant_id = @TenantId
                  AND knowledge_id = @KnowledgeId
                  AND lifecycle_status = @From
                  AND date_trunc('milliseconds', updated_at) = date_trunc('milliseconds', @Expected)
                """,
                new
                {
                    To = to,
                    version.VersionId,
                    version.TenantId,
                    version.KnowledgeId,
                    From = from,
                    Expected = expectedUpdatedAt,
                },
                tx,
                cancellationToken: cancellationToken));
        if (updated != 1)
            throw new InvalidOperationException("Nội dung vừa được người khác cập nhật.");
    }

    private static async Task SetReviewDueAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        GovernedVersion version,
        DateTime dueOn,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pharmacy_owned_knowledge_version
                SET review_due_on = @DueOn
                WHERE id = @VersionId
                  AND tenant_id = @TenantId
                  AND knowledge_id = @KnowledgeId
                """,
                new { DueOn = dueOn.Date, version.VersionId, version.TenantId, version.KnowledgeId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task<GovernedVersion> RequireVersionAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        Guid tenantId,
        Guid knowledgeId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await conn.QuerySingleOrDefaultAsync<GovernedVersion>(
            new CommandDefinition(
                """
                SELECT id AS VersionId,
                       tenant_id AS TenantId,
                       knowledge_id AS KnowledgeId,
                       version_no AS Version,
                       kind AS Kind,
                       title AS Title,
                       body AS Body,
                       classification AS Classification,
                       lifecycle_status AS LifecycleStatus,
                       updated_at AS UpdatedAt
                FROM pharmacy_owned_knowledge_version
                WHERE id = @VersionId
                  AND tenant_id = @TenantId
                  AND knowledge_id = @KnowledgeId
                """,
                new { VersionId = versionId, TenantId = tenantId, KnowledgeId = knowledgeId },
                tx,
                cancellationToken: cancellationToken));
        if (version is null)
            throw new InvalidOperationException("Không tìm thấy tri thức của nhà thuốc.");
        return version;
    }

    private static async Task EnsureActorAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction tx,
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        string action,
        string kind,
        CancellationToken cancellationToken)
    {
        var grants = await conn.QueryAsync<(string Action, string Kind)>(
            new CommandDefinition(
                """
                SELECT action AS Action, kind AS Kind
                FROM pharmacy_owned_knowledge_grant
                WHERE tenant_id = @TenantId
                  AND user_id = @UserId
                  AND revoked_at IS NULL
                """,
                new { TenantId = tenantId, UserId = userId },
                tx,
                cancellationToken: cancellationToken));
        if (!PharmacyOwnedKnowledgeAccess.Allows(isTenantAdmin, action, kind, grants))
            throw new InvalidOperationException("Không có quyền với loại tri thức này.");
    }

    private static string NormalizeGrantAction(string? action)
    {
        var value = action?.Trim().ToLowerInvariant();
        if (value is PharmacyOwnedKnowledgeAccess.DraftAction
            or PharmacyOwnedKnowledgeAccess.SubmitAction
            or PharmacyOwnedKnowledgeAccess.ApproveAction
            or PharmacyOwnedKnowledgeAccess.ActivateAction
            or PharmacyOwnedKnowledgeAccess.ReadAction)
            return value;
        throw new InvalidOperationException("Việc được cấp không hợp lệ.");
    }

    private static async Task<List<PharmacyOwnedKnowledgeVersionBriefDto>> LoadVersionsAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction? tx,
        Guid tenantId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var rows = await conn.QueryAsync<PharmacyOwnedKnowledgeVersionBriefDto>(
            new CommandDefinition(
                """
                SELECT id AS VersionId,
                       version_no AS Version,
                       kind AS Kind,
                       title AS Title,
                       lifecycle_status AS LifecycleStatus,
                       updated_at AS UpdatedAt,
                       body AS Body
                FROM pharmacy_owned_knowledge_version
                WHERE tenant_id = @TenantId
                  AND knowledge_id = @Id
                ORDER BY version_no
                """,
                new { TenantId = tenantId, Id = knowledgeId },
                tx,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static async Task<List<PharmacyOwnedKnowledgeActivationDto>> LoadActivationsAsync(
        Npgsql.NpgsqlConnection conn,
        Npgsql.NpgsqlTransaction? tx,
        Guid tenantId,
        Guid knowledgeId,
        CancellationToken cancellationToken)
    {
        var rows = await conn.QueryAsync<PharmacyOwnedKnowledgeActivationDto>(
            new CommandDefinition(
                """
                SELECT a.version_id AS VersionId,
                       v.version_no AS Version,
                       a.status AS Status
                FROM pharmacy_owned_knowledge_activation a
                JOIN pharmacy_owned_knowledge_version v
                  ON v.id = a.version_id
                 AND v.tenant_id = a.tenant_id
                WHERE a.tenant_id = @TenantId
                  AND a.knowledge_id = @Id
                ORDER BY v.version_no
                """,
                new { TenantId = tenantId, Id = knowledgeId },
                tx,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private sealed class GovernedVersion
    {
        public Guid VersionId { get; set; }
        public Guid TenantId { get; set; }
        public Guid KnowledgeId { get; set; }
        public int Version { get; set; }
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string? Classification { get; set; }
        public string LifecycleStatus { get; set; } = "";
        public DateTime UpdatedAt { get; set; }

        public Draft ToDraft() => new(Kind, Title, Body, Classification);
    }
}
