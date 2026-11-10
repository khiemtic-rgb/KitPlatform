using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesReviewService : IKitSalesReviewService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogService _audit;

    public KitSalesReviewService(IDbConnectionFactory db, ITenantContext tenant, IAuditLogService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    private Guid TenantId => _tenant.TenantId;
    private Guid? ActorId => _tenant.UserId == Guid.Empty ? null : _tenant.UserId;

    public async Task<IReadOnlyList<KitSalesReviewDto>> ListAsync(
        string? type,
        string? status,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<KitSalesReviewDto>(
            new CommandDefinition(
                """
                SELECT id AS Id, task_type AS TaskType, status AS Status, business_id AS BusinessId,
                       payload_json::text AS Payload, created_at AS CreatedAt
                FROM pack_sales.agent_task
                WHERE tenant_id = @TenantId
                  AND task_type <> 'accept_score'
                  AND (@Type IS NULL OR task_type = @Type)
                  AND (@Status IS NULL OR status = @Status)
                ORDER BY created_at DESC
                LIMIT 100
                """,
                new { TenantId, Type = string.IsNullOrWhiteSpace(type) ? null : type, Status = string.IsNullOrWhiteSpace(status) ? null : status },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public Task<KitSalesReviewDto> ResolveAsync(Guid taskId, SalesReviewResolution resolution, CancellationToken cancellationToken = default) =>
        FinishAsync(taskId, "accepted", resolution, cancellationToken);

    public Task<KitSalesReviewDto> DismissAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        FinishAsync(taskId, "dismissed", new SalesReviewResolution("dismissed", null, null, null, null), cancellationToken);

    public async Task<KitSalesReviewDto> AcceptGrantAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        if (ActorId is null)
            throw new InvalidOperationException("Cấp quyền cần một người thật.");
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ReviewRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, task_type AS TaskType, status AS Status, payload_json::text AS Payload
                FROM pack_sales.agent_task
                WHERE id = @Id AND tenant_id = @TenantId
                FOR UPDATE
                """,
                new { Id = taskId, TenantId },
                tx,
                cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("Grant không tồn tại.");
        if (row.TaskType != "autonomy_grant" || row.Status != "pending")
            throw new InvalidOperationException("Chỉ nhận autonomy_grant đang chờ.");
        var payload = JsonSerializer.Deserialize<GrantBody>(row.Payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Grant không đọc được.");
        if (!Enum.TryParse<SalesAutonomyMode>(payload.From, true, out var from)
            || !Enum.TryParse<SalesAutonomyMode>(payload.To, true, out var to))
            throw new InvalidOperationException("Mode không hợp lệ.");
        if (SalesGrantIdentity.SamePerson(payload.RequestedBy, ActorId.Value))
            throw new InvalidOperationException("Người duyệt phải khác người đề xuất.");
        var allowed = SalesAutonomy.CanPromote(
            from, to, 0, 0, false, 0, 0, 0, 1, false, false,
            Guid.Parse(payload.RequestedBy!).ToString("D"), ActorId.Value.ToString("D"), true);
        if (!allowed)
            throw new InvalidOperationException("Chưa đủ mẫu, pháp chế, hoặc người duyệt trùng người đề xuất.");
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.agent_task
                SET status = 'accepted', completed_at = NOW(), resolved_by_user_id = @Actor
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = taskId, TenantId, Actor = ActorId },
                tx,
                cancellationToken: cancellationToken));
        await KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () => _audit.WriteAsync(
            "kit_sales.autonomy_grant",
            taskId,
            "accept",
            new { from = payload.From, to = payload.To },
            cancellationToken));
        return new KitSalesReviewDto(taskId, row.TaskType, "accepted", null, row.Payload, DateTimeOffset.UtcNow);
    }

    public async Task<KitSalesAccountImportResult> ImportAccountsAsync(
        IReadOnlyList<DirectoryAccount> rows,
        CancellationToken cancellationToken = default)
    {
        SalesAccountImport.Guard(rows);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT evidence_json->>'source_key'
                FROM pack_sales.signal
                WHERE tenant_id = @TenantId AND kind = 'legal_fact'
                """,
                new { TenantId },
                cancellationToken: cancellationToken));
        var plan = SalesAccountImport.Plan(rows, existing.ToHashSet(StringComparer.Ordinal));
        var inserted = 0;
        var reviews = 0;
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        foreach (var item in plan.Items.Where(item => item.Action == "insert_account"))
        {
            var row = rows.First(candidate => candidate.SourceKey == item.SourceKey);
            var businessId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.business (tenant_id, name, business_type, province, source, status)
                    VALUES (@TenantId, @Name, 'pharmacy', @Province, 'syt_4252', 'active')
                    RETURNING id
                    """,
                    new { TenantId, row.Name, row.Province },
                    tx,
                    cancellationToken: cancellationToken));
            var evidence = JsonSerializer.Serialize(new
            {
                source_key = row.SourceKey,
                doc_number = row.DocNumber,
                doc_date = row.DocDate,
                published_at = row.DocDate,
                source_url = row.SourceUrl,
                pdf_page = row.PdfPage,
                collected_at = DateTimeOffset.UtcNow,
            });
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.signal (
                        tenant_id, business_id, signal_type, description, source, kind, evidence_json)
                    VALUES (
                        @TenantId, @BusinessId, 'directory_listed', @Name, 'syt_4252', 'legal_fact', @Evidence::jsonb)
                    """,
                    new { TenantId, BusinessId = businessId, row.Name, Evidence = evidence },
                    tx,
                    cancellationToken: cancellationToken));
            inserted++;
        }

        foreach (var item in plan.Items.Where(item => item.Action == "open_review"))
        {
            var businessId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    SELECT business_id FROM pack_sales.signal
                    WHERE tenant_id = @TenantId AND kind = 'legal_fact'
                      AND evidence_json->>'source_key' = @SourceKey
                    """,
                    new { TenantId, item.SourceKey },
                    tx,
                    cancellationToken: cancellationToken));
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.agent_task (
                        tenant_id, business_id, task_type, status, payload_json)
                    VALUES (
                        @TenantId, @BusinessId, 'account_review', 'pending', @Payload::jsonb)
                    ON CONFLICT DO NOTHING
                    """,
                    new
                    {
                        TenantId,
                        BusinessId = businessId,
                        Payload = JsonSerializer.Serialize(new { reason = item.ReviewReason, sourceKey = item.SourceKey }),
                    },
                    tx,
                    cancellationToken: cancellationToken));
            reviews++;
        }

        await tx.CommitAsync(cancellationToken);
        return new KitSalesAccountImportResult(
            inserted,
            plan.Items.Count(item => item.Action == "skip"),
            reviews,
            plan.LeadsCreated);
    }

    private async Task<KitSalesReviewDto> FinishAsync(
        Guid taskId,
        string status,
        SalesReviewResolution resolution,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ReviewRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, task_type AS TaskType, business_id AS BusinessId, status AS Status,
                       payload_json::text AS Payload
                FROM pack_sales.agent_task
                WHERE id = @Id AND tenant_id = @TenantId
                FOR UPDATE
                """,
                new { Id = taskId, TenantId },
                tx,
                cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("Review không tồn tại.");
        if (!SalesReviewRules.ReviewEndpointAllows(row.TaskType) || row.TaskType is "autonomy_grant" or "autonomy_incident")
            throw new InvalidOperationException("Endpoint này không xử lý accept_score hay quyền tự chủ.");
        if (status == "accepted" && !SalesReviewRules.ResolutionAllowed(row.TaskType, resolution.Resolution))
            throw new InvalidOperationException("Resolution không thuộc loại việc này.");
        if (status == "accepted" && resolution.Resolution == "fact_confirmed")
            await InsertConfirmedFactAsync(conn, tx, taskId, row.BusinessId, resolution, cancellationToken);
        if (row.Status == "pending")
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_sales.agent_task
                    SET status = @Status,
                        completed_at = NOW(),
                        resolved_by_user_id = @Actor,
                        payload_json = payload_json || @Extra::jsonb
                    WHERE id = @Id AND tenant_id = @TenantId
                    """,
                    new
                    {
                        Id = taskId,
                        TenantId,
                        Status = status,
                        Actor = ActorId,
                        Extra = JsonSerializer.Serialize(new { resolution = resolution.Resolution }),
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }

        await KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () => _audit.WriteAsync(
            "kit_sales.review",
            taskId,
            status,
            new { taskType = row.TaskType, resolution = resolution.Resolution },
            cancellationToken));
        return new KitSalesReviewDto(taskId, row.TaskType, status, row.BusinessId, row.Payload, DateTimeOffset.UtcNow);
    }

    private async Task InsertConfirmedFactAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid taskId,
        Guid? businessId,
        SalesReviewResolution resolution,
        CancellationToken cancellationToken)
    {
        if (businessId is null
            || string.IsNullOrWhiteSpace(resolution.DocNumber)
            || string.IsNullOrWhiteSpace(resolution.DocDate)
            || string.IsNullOrWhiteSpace(resolution.SourceUrl)
            || resolution.PdfPage is null)
            throw new InvalidOperationException("Xác nhận fact cần số văn bản, ngày, trang và URL.");
        var evidence = JsonSerializer.Serialize(new
        {
            source_key = "human-review:" + taskId.ToString("N"),
            doc_number = resolution.DocNumber.Trim(),
            doc_date = resolution.DocDate.Trim(),
            published_at = resolution.DocDate.Trim(),
            source_url = resolution.SourceUrl.Trim(),
            pdf_page = resolution.PdfPage,
            collected_at = DateTimeOffset.UtcNow,
            supersedes = taskId,
        });
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.signal (
                    tenant_id, business_id, signal_type, description, source, kind, evidence_json)
                VALUES (
                    @TenantId, @BusinessId, 'directory_listed', @DocNumber, 'human_review', 'legal_fact', @Evidence::jsonb)
                """,
                new { TenantId, BusinessId = businessId, resolution.DocNumber, Evidence = evidence },
                tx,
                cancellationToken: cancellationToken));
    }

    private sealed class GrantBody
    {
        public string? From { get; init; }
        public string? To { get; init; }
        public string? RequestedBy { get; init; }
    }

    private sealed class ReviewRow
    {
        public Guid Id { get; init; }
        public string TaskType { get; init; } = "";
        public Guid? BusinessId { get; init; }
        public string Status { get; init; } = "";
        public string Payload { get; init; } = "{}";
    }
}
