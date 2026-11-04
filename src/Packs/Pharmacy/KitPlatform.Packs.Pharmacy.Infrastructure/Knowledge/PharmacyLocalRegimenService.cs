using Dapper;
using Npgsql;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed class PharmacyLocalRegimenService : IPharmacyLocalRegimenService
{
    private readonly IDbConnectionFactory _db;

    public PharmacyLocalRegimenService(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LocalRegimenSummaryDto>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        await using var conn = await OpenAsync(cancellationToken);
        var rows = await conn.QueryAsync<SummaryRow>(
            new CommandDefinition(LatestSql + " ORDER BY v.updated_at DESC", new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.Select(MapSummary).ToList();
    }

    public Task<LocalRegimenDetailDto?> GetAsync(
        Guid tenantId,
        Guid regimenId,
        CancellationToken cancellationToken = default) =>
        LoadAsync(tenantId, regimenId, cancellationToken);

    public async Task<LocalRegimenInUseResult> ListInUseAsync(
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        var text = PharmacyKnowledgeSearchText.Pattern(query);
        var today = PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow);
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var headers = (await conn.QueryAsync<VersionRow>(
                new CommandDefinition(
                    """
                    SELECT v.regimen_id AS RegimenId,
                           v.id AS VersionId,
                           v.version_no AS VersionNo,
                           v.title AS Title,
                           v.condition_group AS ConditionGroup,
                           v.audience AS Audience,
                           v.symptoms AS Symptoms,
                           v.applicability AS Applicability,
                           v.limits AS Limits,
                           v.lifecycle_status AS LifecycleStatus,
                           v.created_by AS CreatedBy,
                           v.approved_by AS ApprovedBy,
                           v.updated_at AS UpdatedAt,
                           v.review_due_on AS ReviewDueOn,
                           a.status AS ActivationStatus
                    FROM pharmacy_local_regimen_version v
                    JOIN pharmacy_local_regimen_activation a
                      ON a.version_id = v.id
                     AND a.tenant_id = v.tenant_id
                    WHERE v.tenant_id = @TenantId
                      AND v.lifecycle_status = 'APPROVED'
                      AND a.status = 'ACTIVE'
                      AND a.acted_at = (
                          SELECT MAX(acted_at)
                          FROM pharmacy_local_regimen_activation
                          WHERE tenant_id = v.tenant_id
                            AND regimen_id = v.regimen_id)
                      AND (@Query IS NULL
                           OR translate(
                               v.title || ' ' || v.condition_group || ' ' || v.symptoms || ' ' || v.audience,
                               @FoldFrom, @FoldTo) ILIKE @Query)
                    ORDER BY v.title
                    """,
                    new
                    {
                        TenantId = tenantId,
                        Query = text,
                        FoldFrom = PharmacyKnowledgeSearchText.SqlFrom,
                        FoldTo = PharmacyKnowledgeSearchText.SqlTo,
                    },
                    cancellationToken: cancellationToken))).ToList();
            var items = await LoadItemsAsync(conn, tenantId, headers.Select(row => row.VersionId).ToArray(), cancellationToken);
            var result = headers.Select(row => new LocalRegimenInUseDto(
                row.RegimenId,
                row.VersionId,
                row.VersionNo,
                row.Title,
                row.ConditionGroup,
                row.Audience,
                row.Symptoms,
                row.Applicability,
                row.Limits,
                items.Where(item => item.VersionId == row.VersionId).Select(MapItem).ToList(),
                row.ReviewDueOn,
                PharmacyKnowledgeReviewDue.IsOverdue(row.ReviewDueOn, today))).ToList();
            return new LocalRegimenInUseResult(result, true);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return new LocalRegimenInUseResult([], false);
        }
    }

    public async Task<LocalRegimenDetailDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        LocalRegimenWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureGrantAsync(tenantId, userId, isTenantAdmin, "DRAFT", cancellationToken);
        var draft = Normalize(request);
        var actor = userId.ToString("D");
        await using var conn = await OpenAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var regimenId = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_local_regimen (tenant_id, created_by)
                VALUES (@TenantId, @Actor)
                RETURNING id
                """,
                new { TenantId = tenantId, Actor = actor },
                tx,
                cancellationToken: cancellationToken));
        var versionId = await InsertVersionAsync(conn, tx, tenantId, regimenId, 1, draft, actor, cancellationToken);
        await InsertItemsAsync(conn, tx, tenantId, versionId, draft.Items, cancellationToken);
        await InsertEventAsync(conn, tx, tenantId, regimenId, 1, "CREATED", actor, null, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await LoadAsync(tenantId, regimenId, cancellationToken))!;
    }

    public async Task<LocalRegimenDetailDto> UpdateAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid regimenId,
        LocalRegimenWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureGrantAsync(tenantId, userId, isTenantAdmin, "DRAFT", cancellationToken);
        var draft = Normalize(request);
        var actor = userId.ToString("D");
        await using var conn = await OpenAsync(cancellationToken);
        var current = await CurrentAsync(conn, tenantId, regimenId, cancellationToken);
        if (!string.Equals(current.LifecycleStatus, PharmacyLocalRegimenRules.Draft, StringComparison.Ordinal))
            throw new InvalidOperationException("Chỉ sửa được bản nháp.");
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pharmacy_local_regimen_version
                SET title = @Title,
                    condition_group = @ConditionGroup,
                    audience = @Audience,
                    symptoms = @Symptoms,
                    applicability = @Applicability,
                    limits = @Limits,
                    updated_at = NOW()
                WHERE id = @VersionId AND tenant_id = @TenantId AND lifecycle_status = 'DRAFT'
                """,
                new
                {
                    current.VersionId,
                    TenantId = tenantId,
                    draft.Title,
                    draft.ConditionGroup,
                    draft.Audience,
                    draft.Symptoms,
                    draft.Applicability,
                    draft.Limits,
                },
                tx,
                cancellationToken: cancellationToken));
        await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM pharmacy_local_regimen_item WHERE version_id = @VersionId AND tenant_id = @TenantId",
                new { current.VersionId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
        await InsertItemsAsync(conn, tx, tenantId, current.VersionId, draft.Items, cancellationToken);
        await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo, "UPDATED", actor, null, cancellationToken);
        await TouchHeadAsync(conn, tx, tenantId, regimenId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await LoadAsync(tenantId, regimenId, cancellationToken))!;
    }

    public async Task<LocalRegimenDetailDto> TransitionAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid regimenId,
        string action,
        LocalRegimenTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var verb = action.Trim().ToLowerInvariant();
        var grant = verb switch
        {
            "submit" or "return" or "next-draft" => "SUBMIT",
            "approve" => "APPROVE",
            "activate" or "stop" => "ACTIVATE",
            _ => throw new InvalidOperationException("Thao tác không hợp lệ."),
        };
        await EnsureGrantAsync(tenantId, userId, isTenantAdmin, grant, cancellationToken);
        var actor = userId.ToString("D");
        await using var conn = await OpenAsync(cancellationToken);
        var current = await CurrentAsync(conn, tenantId, regimenId, cancellationToken);
        if (request.VersionId != current.VersionId)
            throw new InvalidOperationException("Không tìm thấy phiên bản.");
        if (Math.Abs((current.UpdatedAt - request.ExpectedUpdatedAt).TotalSeconds) > 1)
            throw new InvalidOperationException("Bản này đã được người khác cập nhật.");

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        switch (verb)
        {
            case "submit":
                RequireStatus(current, PharmacyLocalRegimenRules.Draft);
                await SetStatusAsync(conn, tx, tenantId, current.VersionId, PharmacyLocalRegimenRules.InReview, null, cancellationToken);
                await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo, "SUBMITTED", actor, request.Note, cancellationToken);
                break;
            case "return":
                RequireStatus(current, PharmacyLocalRegimenRules.InReview);
                await SetStatusAsync(conn, tx, tenantId, current.VersionId, PharmacyLocalRegimenRules.Draft, null, cancellationToken);
                await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo, "RETURNED", actor, request.Note, cancellationToken);
                break;
            case "approve":
                RequireStatus(current, PharmacyLocalRegimenRules.InReview);
                var authors = (await conn.QueryAsync<string>(
                    new CommandDefinition(
                        """
                        SELECT actor_user_id FROM pharmacy_local_regimen_event
                        WHERE tenant_id = @TenantId AND regimen_id = @RegimenId AND version_no = @VersionNo
                          AND action IN ('CREATED', 'UPDATED', 'SUBMITTED')
                        """,
                        new { TenantId = tenantId, RegimenId = regimenId, current.VersionNo },
                        tx,
                        cancellationToken: cancellationToken))).Append(current.CreatedBy);
                PharmacyKnowledgeSeparationOfDuties.EnsureApproverIsNotAuthor(userId, authors);
                await SetStatusAsync(conn, tx, tenantId, current.VersionId, PharmacyLocalRegimenRules.Approved, actor, cancellationToken);
                await SetReviewDueAsync(conn, tx, tenantId, current.VersionId, ReviewDue(request), cancellationToken);
                await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo, "APPROVED", actor, request.Note, cancellationToken);
                break;
            case "activate":
                RequireStatus(current, PharmacyLocalRegimenRules.Approved);
                await SetReviewDueAsync(conn, tx, tenantId, current.VersionId, ReviewDue(request), cancellationToken);
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO pharmacy_local_regimen_activation (regimen_id, version_id, tenant_id, status, note, acted_by)
                        SELECT regimen_id, version_id, tenant_id, 'STOPPED', 'Ngừng vì áp dụng phiên bản mới.', @Actor
                        FROM pharmacy_local_regimen_activation a
                        WHERE a.tenant_id = @TenantId
                          AND a.regimen_id = @RegimenId
                          AND a.status = 'ACTIVE'
                          AND a.acted_at = (
                              SELECT MAX(acted_at) FROM pharmacy_local_regimen_activation
                              WHERE tenant_id = @TenantId AND regimen_id = @RegimenId)
                        """,
                        new { TenantId = tenantId, RegimenId = regimenId, Actor = actor },
                        tx,
                        cancellationToken: cancellationToken));
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO pharmacy_local_regimen_activation (regimen_id, version_id, tenant_id, status, note, acted_by)
                        VALUES (@RegimenId, @VersionId, @TenantId, 'ACTIVE', @Note, @Actor)
                        """,
                        new { RegimenId = regimenId, current.VersionId, TenantId = tenantId, request.Note, Actor = actor },
                        tx,
                        cancellationToken: cancellationToken));
                await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo, "ACTIVATED", actor, request.Note, cancellationToken);
                break;
            case "stop":
                if (!string.Equals(current.ActivationStatus, PharmacyLocalRegimenRules.Active, StringComparison.Ordinal))
                    throw new InvalidOperationException("Chỉ ngừng được bản đang áp dụng.");
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO pharmacy_local_regimen_activation (regimen_id, version_id, tenant_id, status, note, acted_by)
                        VALUES (@RegimenId, @VersionId, @TenantId, 'STOPPED', @Note, @Actor)
                        """,
                        new { RegimenId = regimenId, current.VersionId, TenantId = tenantId, request.Note, Actor = actor },
                        tx,
                        cancellationToken: cancellationToken));
                await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo, "STOPPED", actor, request.Note, cancellationToken);
                break;
            case "next-draft":
                if (current.LifecycleStatus is not (PharmacyLocalRegimenRules.Approved))
                    throw new InvalidOperationException("Chỉ tạo bản nháp mới từ bản đã duyệt.");
                var open = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        """
                        SELECT COUNT(*) FROM pharmacy_local_regimen_version
                        WHERE tenant_id = @TenantId AND regimen_id = @RegimenId
                          AND lifecycle_status IN ('DRAFT', 'IN_REVIEW')
                        """,
                        new { TenantId = tenantId, RegimenId = regimenId },
                        tx,
                        cancellationToken: cancellationToken));
                if (open > 0)
                    throw new InvalidOperationException("Đang có bản nháp hoặc bản chờ duyệt.");
                var items = await LoadItemsAsync(conn, tenantId, [current.VersionId], cancellationToken, tx);
                var copy = new NormalizedRegimen(
                    current.Title,
                    current.ConditionGroup,
                    current.Audience,
                    current.Symptoms,
                    current.Applicability,
                    current.Limits,
                    items.Select(item => new LocalRegimenItemWrite(
                        item.Ingredient,
                        item.Strength,
                        item.DosageForm,
                        item.ProductId,
                        item.ProductLabel,
                        item.MatchStatus)).ToList());
                var nextId = await InsertVersionAsync(conn, tx, tenantId, regimenId, current.VersionNo + 1, copy, actor, cancellationToken);
                await InsertItemsAsync(conn, tx, tenantId, nextId, copy.Items, cancellationToken);
                await InsertEventAsync(conn, tx, tenantId, regimenId, current.VersionNo + 1, "CREATED", actor, request.Note, cancellationToken);
                break;
        }

        await TouchHeadAsync(conn, tx, tenantId, regimenId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await LoadAsync(tenantId, regimenId, cancellationToken))!;
    }

    public async Task GrantAsync(
        Guid tenantId,
        Guid actorUserId,
        bool isTenantAdmin,
        LocalRegimenGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!isTenantAdmin)
            throw new InvalidOperationException("Không có quyền cấp quyền mẫu đơn.");
        RequireTenant(tenantId);
        if (request.UserId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người được cấp quyền.");
        var action = PharmacyLocalRegimenRules.NormalizeGrant(request.Action);
        await using var conn = await OpenAsync(cancellationToken);
        var userExists = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM users WHERE id = @UserId AND tenant_id = @TenantId)",
                new { request.UserId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        if (!userExists)
            throw new InvalidOperationException("Không tìm thấy người dùng trong nhà thuốc này.");
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_local_regimen_grant (tenant_id, user_id, action, granted_by)
                VALUES (@TenantId, @UserId, @Action, @Actor)
                """,
                new { TenantId = tenantId, request.UserId, Action = action, Actor = actorUserId.ToString("D") },
                cancellationToken: cancellationToken));
    }

    private async Task EnsureGrantAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        string action,
        CancellationToken cancellationToken)
    {
        RequireTenant(tenantId);
        if (isTenantAdmin)
            return;
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var allowed = await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    """
                    SELECT EXISTS (
                        SELECT 1 FROM pharmacy_local_regimen_grant
                        WHERE tenant_id = @TenantId
                          AND user_id = @UserId
                          AND action = @Action
                          AND revoked_at IS NULL)
                    """,
                    new { TenantId = tenantId, UserId = userId, Action = action },
                    cancellationToken: cancellationToken));
            if (!allowed)
                throw new InvalidOperationException("Không có quyền với thao tác này.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw new InvalidOperationException("Kho đơn tham khảo chưa được bật trên database.");
        }
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT 1 FROM pharmacy_local_regimen WHERE false",
                    cancellationToken: cancellationToken));
            return conn;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            await conn.DisposeAsync();
            throw new InvalidOperationException("Kho đơn tham khảo chưa được bật trên database.");
        }
    }

    private const string LatestSql = """
        SELECT v.regimen_id AS RegimenId,
               v.id AS VersionId,
               v.version_no AS VersionNo,
               v.title AS Title,
               v.condition_group AS ConditionGroup,
               v.audience AS Audience,
               v.symptoms AS Symptoms,
               v.applicability AS Applicability,
               v.limits AS Limits,
               v.lifecycle_status AS LifecycleStatus,
               v.created_by AS CreatedBy,
               v.approved_by AS ApprovedBy,
               v.updated_at AS UpdatedAt,
               v.review_due_on AS ReviewDueOn,
               act.status AS ActivationStatus
        FROM pharmacy_local_regimen_version v
        LEFT JOIN pharmacy_local_regimen_activation act
          ON act.version_id = v.id
         AND act.tenant_id = v.tenant_id
         AND act.acted_at = (
             SELECT MAX(acted_at)
             FROM pharmacy_local_regimen_activation
             WHERE tenant_id = v.tenant_id
               AND regimen_id = v.regimen_id)
        WHERE v.tenant_id = @TenantId
          AND v.version_no = (
              SELECT MAX(version_no)
              FROM pharmacy_local_regimen_version
              WHERE tenant_id = v.tenant_id
                AND regimen_id = v.regimen_id)
        """;

    private async Task<VersionRow> CurrentAsync(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid regimenId,
        CancellationToken cancellationToken)
    {
        var row = await conn.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(
                LatestSql + " AND v.regimen_id = @RegimenId",
                new { TenantId = tenantId, RegimenId = regimenId },
                cancellationToken: cancellationToken));
        if (row is null)
            throw new InvalidOperationException("Không tìm thấy mẫu đơn.");
        return row;
    }

    private async Task<LocalRegimenDetailDto?> LoadAsync(
        Guid tenantId,
        Guid regimenId,
        CancellationToken cancellationToken)
    {
        await using var conn = await OpenAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(
                LatestSql + " AND v.regimen_id = @RegimenId",
                new { TenantId = tenantId, RegimenId = regimenId },
                cancellationToken: cancellationToken));
        if (row is null)
            return null;
        var items = await LoadItemsAsync(conn, tenantId, [row.VersionId], cancellationToken);
        var events = await conn.QueryAsync<EventRow>(
            new CommandDefinition(
                """
                SELECT action AS Action,
                       version_no AS Version,
                       actor_user_id AS ActorUserId,
                       note AS Note,
                       created_at AS CreatedAt
                FROM pharmacy_local_regimen_event
                WHERE tenant_id = @TenantId AND regimen_id = @RegimenId
                ORDER BY created_at
                """,
                new { TenantId = tenantId, RegimenId = regimenId },
                cancellationToken: cancellationToken));
        return new LocalRegimenDetailDto(
            row.RegimenId,
            row.VersionId,
            row.VersionNo,
            row.Title,
            row.ConditionGroup,
            row.Audience,
            row.Symptoms,
            row.Applicability,
            row.Limits,
            row.LifecycleStatus,
            row.ActivationStatus,
            row.CreatedBy,
            row.ApprovedBy,
            row.UpdatedAt,
            items.Select(MapItem).ToList(),
            events.Select(row => new LocalRegimenEventDto(
                row.Action,
                row.Version,
                row.ActorUserId,
                row.Note,
                new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)))).ToList(),
            row.ReviewDueOn,
            PharmacyKnowledgeReviewDue.IsOverdue(row.ReviewDueOn, PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow)));
    }

    private static async Task<IReadOnlyList<ItemRow>> LoadItemsAsync(
        NpgsqlConnection conn,
        Guid tenantId,
        Guid[] versionIds,
        CancellationToken cancellationToken,
        NpgsqlTransaction? tx = null)
    {
        if (versionIds.Length == 0)
            return [];
        var rows = await conn.QueryAsync<ItemRow>(
            new CommandDefinition(
                """
                SELECT i.id AS Id,
                       i.version_id AS VersionId,
                       i.ingredient AS Ingredient,
                       i.strength AS Strength,
                       i.dosage_form AS DosageForm,
                       i.product_id AS ProductId,
                       p.product_name AS CatalogProductName,
                       p.product_code AS CatalogProductCode,
                       i.product_label AS ProductLabel,
                       i.match_status AS MatchStatus
                FROM pharmacy_local_regimen_item i
                LEFT JOIN products p
                  ON p.id = i.product_id
                 AND p.tenant_id = i.tenant_id
                WHERE i.tenant_id = @TenantId
                  AND i.version_id = ANY(@VersionIds)
                ORDER BY i.sort_order
                """,
                new { TenantId = tenantId, VersionIds = versionIds },
                tx,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static async Task<Guid> InsertVersionAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid regimenId,
        int versionNo,
        NormalizedRegimen draft,
        string actor,
        CancellationToken cancellationToken)
    {
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_local_regimen_version (
                    regimen_id, tenant_id, version_no, title, condition_group, audience,
                    symptoms, applicability, limits, lifecycle_status, created_by)
                VALUES (
                    @RegimenId, @TenantId, @VersionNo, @Title, @ConditionGroup, @Audience,
                    @Symptoms, @Applicability, @Limits, 'DRAFT', @Actor)
                RETURNING id
                """,
                new
                {
                    RegimenId = regimenId,
                    TenantId = tenantId,
                    VersionNo = versionNo,
                    draft.Title,
                    draft.ConditionGroup,
                    draft.Audience,
                    draft.Symptoms,
                    draft.Applicability,
                    draft.Limits,
                    Actor = actor,
                },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task InsertItemsAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid versionId,
        IReadOnlyList<LocalRegimenItemWrite> items,
        CancellationToken cancellationToken)
    {
        var order = 0;
        foreach (var item in items)
        {
            string? catalogName = null;
            if (item.ProductId is Guid productId)
            {
                catalogName = await conn.ExecuteScalarAsync<string?>(
                    new CommandDefinition(
                        "SELECT product_name FROM products WHERE id = @Id AND tenant_id = @TenantId",
                        new { Id = productId, TenantId = tenantId },
                        tx,
                        cancellationToken: cancellationToken));
                if (catalogName is null)
                    throw new InvalidOperationException("Không tìm thấy sản phẩm trong danh mục nhà thuốc.");
            }

            var match = PharmacyLocalRegimenRules.NormalizeMatch(item.MatchStatus);
            if (match is "MATCHED" or "SUBSTITUTE_CONFIRMED" && item.ProductId is null)
                throw new InvalidOperationException("Lựa chọn đã xác nhận phải gắn với sản phẩm trong danh mục.");
            if (match == "UNMATCHED" && item.ProductId is not null)
                throw new InvalidOperationException("Sản phẩm chưa khớp không được gắn mã danh mục.");
            var ingredient = PharmacyLocalRegimenRules.RequireText(item.Ingredient, "hoạt chất", 200);
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pharmacy_local_regimen_item (
                        version_id, tenant_id, sort_order, ingredient, strength, dosage_form,
                        product_id, product_label, match_status)
                    VALUES (
                        @VersionId, @TenantId, @SortOrder, @Ingredient, @Strength, @DosageForm,
                        @ProductId, @ProductLabel, @MatchStatus)
                    """,
                    new
                    {
                        VersionId = versionId,
                        TenantId = tenantId,
                        SortOrder = order++,
                        Ingredient = ingredient,
                        Strength = Blank(item.Strength),
                        DosageForm = Blank(item.DosageForm),
                        item.ProductId,
                        ProductLabel = catalogName ?? Blank(item.ProductLabel),
                        MatchStatus = match,
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }
    }

    private static async Task SetStatusAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid versionId,
        string status,
        string? approver,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pharmacy_local_regimen_version
                SET lifecycle_status = @Status,
                    approved_by = COALESCE(@Approver, approved_by),
                    approved_at = CASE WHEN @Approver IS NULL THEN approved_at ELSE NOW() END,
                    updated_at = NOW()
                WHERE id = @VersionId AND tenant_id = @TenantId
                """,
                new { Status = status, Approver = approver, VersionId = versionId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static DateTime ReviewDue(LocalRegimenTransitionRequest request) =>
        PharmacyKnowledgeReviewDue.Resolve(
            request.ReviewDueOn, PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow));

    private static async Task SetReviewDueAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid versionId,
        DateTime dueOn,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE pharmacy_local_regimen_version SET review_due_on = @DueOn WHERE id = @VersionId AND tenant_id = @TenantId",
                new { DueOn = dueOn.Date, VersionId = versionId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid regimenId,
        int versionNo,
        string action,
        string actor,
        string? note,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_local_regimen_event (
                    tenant_id, regimen_id, version_no, action, actor_user_id, note)
                VALUES (@TenantId, @RegimenId, @VersionNo, @Action, @Actor, @Note)
                """,
                new
                {
                    TenantId = tenantId,
                    RegimenId = regimenId,
                    VersionNo = versionNo,
                    Action = action,
                    Actor = actor,
                    Note = Blank(note),
                },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task TouchHeadAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid regimenId,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE pharmacy_local_regimen SET updated_at = NOW() WHERE id = @RegimenId AND tenant_id = @TenantId",
                new { RegimenId = regimenId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static NormalizedRegimen Normalize(LocalRegimenWriteRequest request)
    {
        var items = request.Items ?? [];
        if (items.Count == 0)
            throw new InvalidOperationException("Mẫu đơn cần ít nhất một hoạt chất.");
        return new NormalizedRegimen(
            PharmacyLocalRegimenRules.RequireText(request.Title, "tiêu đề", 200),
            PharmacyLocalRegimenRules.RequireText(request.ConditionGroup, "nhóm vấn đề", 200),
            PharmacyLocalRegimenRules.RequireText(request.Audience, "đối tượng", 1000),
            PharmacyLocalRegimenRules.RequireText(request.Symptoms, "triệu chứng", 1000),
            PharmacyLocalRegimenRules.RequireText(request.Applicability, "điều kiện áp dụng", 2000),
            PharmacyLocalRegimenRules.RequireText(request.Limits, "giới hạn", 2000),
            items);
    }

    private static void RequireStatus(VersionRow row, string status)
    {
        if (!string.Equals(row.LifecycleStatus, status, StringComparison.Ordinal))
            throw new InvalidOperationException("Trạng thái hiện tại không cho phép thao tác này.");
    }

    private static LocalRegimenSummaryDto MapSummary(SummaryRow row) =>
        new(
            row.RegimenId,
            row.VersionId,
            row.VersionNo,
            row.Title,
            row.ConditionGroup,
            row.LifecycleStatus,
            row.ActivationStatus,
            row.UpdatedAt,
            row.ReviewDueOn,
            PharmacyKnowledgeReviewDue.IsOverdue(row.ReviewDueOn, PharmacyKnowledgeReviewDue.TodayInVietnam(DateTime.UtcNow)));

    private static LocalRegimenItemDto MapItem(ItemRow row) =>
        new(
            row.Id,
            row.Ingredient,
            row.Strength,
            row.DosageForm,
            row.ProductId,
            row.CatalogProductName,
            row.CatalogProductCode,
            row.ProductLabel,
            row.MatchStatus,
            PharmacyLocalRegimenRules.IsValidChoice(row.MatchStatus, row.ProductId));

    private static string? Blank(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static void RequireTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Thiếu tenant.");
    }

    private sealed class SummaryRow : VersionRow;

    private class VersionRow
    {
        public Guid RegimenId { get; init; }
        public Guid VersionId { get; init; }
        public int VersionNo { get; init; }
        public string Title { get; init; } = "";
        public string ConditionGroup { get; init; } = "";
        public string Audience { get; init; } = "";
        public string Symptoms { get; init; } = "";
        public string Applicability { get; init; } = "";
        public string Limits { get; init; } = "";
        public string LifecycleStatus { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public string? ApprovedBy { get; init; }
        public DateTime UpdatedAt { get; init; }
        public DateTime? ReviewDueOn { get; init; }
        public string? ActivationStatus { get; init; }
    }

    private sealed class EventRow
    {
        public string Action { get; init; } = "";
        public int Version { get; init; }
        public string ActorUserId { get; init; } = "";
        public string? Note { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class ItemRow
    {
        public Guid Id { get; init; }
        public Guid VersionId { get; init; }
        public string Ingredient { get; init; } = "";
        public string? Strength { get; init; }
        public string? DosageForm { get; init; }
        public Guid? ProductId { get; init; }
        public string? CatalogProductName { get; init; }
        public string? CatalogProductCode { get; init; }
        public string? ProductLabel { get; init; }
        public string MatchStatus { get; init; } = "";
    }

    private sealed record NormalizedRegimen(
        string Title,
        string ConditionGroup,
        string Audience,
        string Symptoms,
        string Applicability,
        string Limits,
        IReadOnlyList<LocalRegimenItemWrite> Items);
}
