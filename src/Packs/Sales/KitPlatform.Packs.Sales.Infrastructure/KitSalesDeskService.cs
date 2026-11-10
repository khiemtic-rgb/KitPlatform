using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesDeskService : IKitSalesDeskService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly KitSalesAssessmentLookup _assessments;

    public KitSalesDeskService(IDbConnectionFactory db, ITenantContext tenant, KitSalesAssessmentLookup assessments)
    {
        _db = db;
        _tenant = tenant;
        _assessments = assessments;
    }

    private Guid TenantId => _tenant.TenantId;

    private Guid? CurrentUserId => _tenant.UserId == Guid.Empty ? null : _tenant.UserId;

    public Task<KitSalesHealthDto> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new KitSalesHealthDto(SalesPackDefinition.PackCode, "v1", true));

    public async Task<IReadOnlyList<KitSalesProductDto>> ListProductsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ProductRow>(
            """
            SELECT code AS Code, display_name AS DisplayName, status AS Status
            FROM pack_sales.product
            ORDER BY code
            """);
        return rows.Select(r => new KitSalesProductDto(r.Code, r.DisplayName, r.Status)).ToList();
    }

    public async Task<IReadOnlyList<KitSalesBusinessDto>> ListBusinessesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<BusinessRow>(
            """
            SELECT id AS Id, name AS Name, business_type AS BusinessType,
                   province AS Province, phone AS Phone, status AS Status,
                   source AS Source, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM pack_sales.business
            WHERE tenant_id = @TenantId
            ORDER BY updated_at DESC
            LIMIT @Limit
            """,
            new { TenantId, Limit = limit });
        return rows.Select(MapBusiness).ToList();
    }

    public async Task<KitSalesBusinessDto> CreateBusinessAsync(
        CreateKitSalesBusinessRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên business là bắt buộc.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<BusinessRow>(
            """
            INSERT INTO pack_sales.business (
                tenant_id, name, business_type, province, district, phone, email, source, description
            )
            VALUES (
                @TenantId, @Name, @BusinessType, @Province, @District, @Phone, @Email, @Source, @Notes
            )
            RETURNING id AS Id, name AS Name, business_type AS BusinessType,
                      province AS Province, phone AS Phone, status AS Status,
                      source AS Source, created_at AS CreatedAt, updated_at AS UpdatedAt
            """,
            new
            {
                TenantId,
                Name = name,
                BusinessType = NormalizeCode(request.BusinessType, "pharmacy"),
                Province = TrimOrNull(request.Province),
                District = TrimOrNull(request.District),
                Phone = TrimOrNull(request.Phone),
                Email = TrimOrNull(request.Email),
                Source = TrimOrNull(request.Source),
                Notes = TrimOrNull(request.Notes),
            });
        return MapBusiness(row);
    }

    public async Task<IReadOnlyList<KitSalesLeadDto>> ListLeadsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default,
        string? province = null,
        string? facebookKind = null)
    {
        limit = Math.Clamp(limit, 1, 200);
        var statusFilter = TrimOrNull(status);
        var kindFilter = TrimOrNull(facebookKind)?.ToLowerInvariant();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<LeadRow>(
            LeadSelectSql + """
              AND ((@Status IS NULL AND l.lead_status <> @Archived) OR l.lead_status = @Status)
            ORDER BY l.updated_at DESC
            LIMIT @Limit
            """,
            new { TenantId, Status = statusFilter, Limit = limit, Archived = LeadRetention.Archived });
        return rows.Select(MapLead)
            .Where(l => NovixaFacebookSource.ProvinceMatches(l.Province, province))
            .Where(l => kindFilter is null
                || (kindFilter is NovixaFacebookSource.Profile or NovixaFacebookSource.Page
                    && l.FacebookKind == kindFilter))
            .ToList();
    }

    public async Task<KitSalesLeadFilterDto> GetLeadFiltersAsync(
        CancellationToken cancellationToken = default)
    {
        var leads = await ListLeadsAsync(null, 200, cancellationToken);
        var provinces = leads
            .Select(l => NovixaFacebookSource.NormalizeProvince(l.Province))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!provinces.Contains("Thái Nguyên", StringComparer.OrdinalIgnoreCase))
            provinces.Insert(0, "Thái Nguyên");
        var kinds = new[] { NovixaFacebookSource.Profile, NovixaFacebookSource.Page }
            .Select(kind => new KitSalesPipelineBucketDto(kind, leads.Count(l => l.FacebookKind == kind)))
            .ToList();
        return new KitSalesLeadFilterDto(provinces, kinds);
    }

    public async Task<KitSalesLeadDto> CreateLeadAsync(
        CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await InsertLeadAsync(conn, request, cancellationToken);
    }

    public async Task<KitSalesLeadDto> CreateProspectAsync(
        CreateKitSalesProspectRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.BusinessName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên business là bắt buộc.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var business = await conn.QuerySingleAsync<BusinessRow>(
            """
            INSERT INTO pack_sales.business (
                tenant_id, name, business_type, province, district, phone, email, source, description
            )
            VALUES (
                @TenantId, @Name, @BusinessType, @Province, @District, @Phone, @Email, @Source, @Notes
            )
            RETURNING id AS Id, name AS Name, business_type AS BusinessType,
                      province AS Province, phone AS Phone, status AS Status,
                      source AS Source, created_at AS CreatedAt, updated_at AS UpdatedAt
            """,
            new
            {
                TenantId,
                Name = name,
                BusinessType = NormalizeCode(request.BusinessType, "pharmacy"),
                Province = string.IsNullOrWhiteSpace(request.Province)
                    ? null
                    : NovixaFacebookSource.NormalizeProvince(request.Province),
                District = (string?)null,
                Phone = TrimOrNull(request.Phone),
                Email = (string?)null,
                Source = TrimOrNull(request.Source),
                Notes = TrimOrNull(request.Notes),
            },
            tx);

        var lead = await InsertLeadAsync(
            conn,
            new CreateKitSalesLeadRequest(
                business.Id,
                request.ProductCode,
                request.Source,
                "discovered",
                "cold",
                request.Notes),
            cancellationToken,
            tx);

        await UpsertFacebookIdentityAsync(
            conn,
            business.Id,
            request.Source,
            request.FacebookKind,
            tx,
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return await GetMappedLeadAsync(conn, lead.Id, cancellationToken);
    }

    public async Task<KitSalesImportResultDto> ImportProspectsAsync(
        ImportKitSalesProspectsRequest request,
        CancellationToken cancellationToken = default)
    {
        var rows = NovixaFacebookSource.ParseImportLines(request.Text);
        if (rows.Count == 0)
            throw new ArgumentException("Không thấy link Facebook trong danh sách.");
        if (rows.Count > 50)
            throw new ArgumentException("Tối đa 50 link mỗi lần dán.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = (await conn.QueryAsync<(string? Url, string? Username, string? Source)>(
            """
            SELECT i.url AS Url, i.username AS Username, COALESCE(l.source, b.source) AS Source
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business b ON b.id = l.business_id
            LEFT JOIN pack_sales.business_identity i
                ON i.business_id = b.id
               AND i.tenant_id = l.tenant_id
               AND i.identity_type = 'facebook'
               AND i.platform IN ('profile', 'page')
            WHERE l.tenant_id = @TenantId
            """,
            new { TenantId })).ToList();

        var created = new List<KitSalesLeadDto>();
        var errors = new List<string>();
        var skipped = 0;

        foreach (var (name, url) in rows)
        {
            var already = existing.Any(row =>
                NovixaFacebookSource.SameFacebook(url, row.Url)
                || NovixaFacebookSource.SameFacebook(url, row.Source)
                || NovixaFacebookSource.SameFacebook(url, row.Username));
            if (already)
            {
                skipped++;
                continue;
            }

            try
            {
                var lead = await CreateProspectAsync(
                    new CreateKitSalesProspectRequest(
                        name,
                        request.ProductCode,
                        "pharmacy",
                        request.Province,
                        null,
                        url,
                        null,
                        request.FacebookKind),
                    cancellationToken);
                created.Add(lead);
                existing.Add((url, NovixaFacebookSource.Resolve(null, url).Username, url));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        return new KitSalesImportResultDto(created.Count, skipped, errors, created);
    }

    public async Task<KitSalesLeadDto> UpdateLeadAsync(
        Guid leadId,
        UpdateKitSalesLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var existing = await conn.QuerySingleOrDefaultAsync<LeadLookupRow>(
            """
            SELECT l.business_id AS BusinessId, b.name AS Name,
                   b.province AS Province, b.phone AS Phone,
                   b.source AS BusinessSource, l.source AS LeadSource,
                   l.notes AS Notes, l.next_action_code AS NextActionCode,
                   l.next_action_at AS NextActionAt, l.lead_status AS LeadStatus,
                   l.product_code AS ProductCode,
                   l.assessment_submission_id AS AssessmentSubmissionId
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business b ON b.id = l.business_id
            WHERE l.id = @LeadId AND l.tenant_id = @TenantId
            """,
            new { LeadId = leadId, TenantId },
            tx);
        if (existing is null)
            throw new InvalidOperationException("Lead không tồn tại.");
        if (existing.LeadStatus == LeadRetention.Archived)
            throw new InvalidOperationException("Lead đã lưu trữ, không sửa được.");

        var businessId = existing.BusinessId;
        var businessName = TrimOrNull(request.BusinessName) ?? existing.Name;
        if (string.IsNullOrWhiteSpace(businessName))
            throw new ArgumentException("Tên business là bắt buộc.");

        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.business
            SET name = @Name,
                province = @Province,
                phone = @Phone,
                source = @Source,
                updated_at = NOW()
            WHERE id = @BusinessId AND tenant_id = @TenantId
            """,
            new
            {
                BusinessId = businessId,
                TenantId,
                Name = businessName,
                Province = request.Province is null
                    ? existing.Province
                    : NovixaFacebookSource.NormalizeProvince(request.Province),
                Phone = request.Phone is null ? existing.Phone : TrimOrNull(request.Phone),
                Source = request.Source is null ? existing.BusinessSource : TrimOrNull(request.Source),
            },
            tx);

        var newStatus = string.IsNullOrWhiteSpace(request.LeadStatus)
            ? existing.LeadStatus
            : NormalizeCode(request.LeadStatus, existing.LeadStatus);
        Guid? assessmentId = existing.AssessmentSubmissionId;
        if (request.LinkAssessment)
        {
            if (request.AssessmentSubmissionId is null)
                throw new ArgumentException("Thiếu assessmentSubmissionId.");
            assessmentId = await LinkAssessmentAsync(conn, tx, leadId, existing, request.AssessmentSubmissionId.Value, cancellationToken);
        }

        if (!string.Equals(newStatus, existing.LeadStatus, StringComparison.Ordinal))
            await EnsureStageAsync(conn, tx, leadId, existing, newStatus, assessmentId, request, cancellationToken);

        string? nextActionCode = existing.NextActionCode;
        DateTime? nextActionAt = existing.NextActionAt;
        if (request.ClearNextAction)
        {
            nextActionCode = null;
            nextActionAt = null;
        }
        else
        {
            if (request.NextActionCode is not null)
                nextActionCode = TrimOrNull(request.NextActionCode) is { } code
                    ? NormalizeCode(code, "follow_up")
                    : null;
            if (request.NextActionAt.HasValue)
                nextActionAt = request.NextActionAt.Value.UtcDateTime;
        }

        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.lead
            SET source = @Source,
                lead_status = @LeadStatus,
                lead_temperature = COALESCE(@LeadTemperature, lead_temperature),
                notes = @Notes,
                next_action_code = @NextActionCode,
                next_action_at = @NextActionAt,
                assessment_submission_id = @AssessmentId,
                updated_at = NOW()
            WHERE id = @LeadId AND tenant_id = @TenantId
            """,
            new
            {
                LeadId = leadId,
                TenantId,
                Source = request.Source is null ? existing.LeadSource : TrimOrNull(request.Source),
                LeadStatus = newStatus,
                AssessmentId = assessmentId,
                LeadTemperature = string.IsNullOrWhiteSpace(request.LeadTemperature)
                    ? null
                    : NormalizeCode(request.LeadTemperature, "cold"),
                Notes = request.Notes is null ? existing.Notes : TrimOrNull(request.Notes),
                NextActionCode = nextActionCode,
                NextActionAt = nextActionAt,
            },
            tx);

        await UpsertFacebookIdentityAsync(
            conn,
            businessId,
            request.Source is null ? existing.LeadSource : request.Source,
            request.FacebookKind,
            tx,
            cancellationToken);

        var row = await conn.QuerySingleAsync<LeadRow>(LeadSelectSql + " AND l.id = @LeadId", new { TenantId, LeadId = leadId }, tx);
        await tx.CommitAsync(cancellationToken);
        return MapLead(row);
    }

    public async Task<string> DeleteLeadAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var lead = await conn.QuerySingleOrDefaultAsync<RetentionRow>(
            $"""
            SELECT l.business_id AS BusinessId, l.lead_status AS LeadStatus,
                   ({LeadHistorySql}) AS HasHistory
            FROM pack_sales.lead l
            WHERE l.id = @LeadId AND l.tenant_id = @TenantId
            FOR UPDATE OF l
            """,
            new { LeadId = leadId, TenantId },
            tx);
        if (lead is null)
            throw new InvalidOperationException("Lead không tồn tại.");

        if (LeadRetention.Decide(lead.HasHistory) == LeadRetention.Archived)
        {
            if (lead.LeadStatus != LeadRetention.Archived)
            {
                await conn.ExecuteAsync(
                    """
                    UPDATE pack_sales.lead
                    SET lead_status = @Archived, next_action_code = NULL, next_action_at = NULL, updated_at = NOW()
                    WHERE id = @LeadId AND tenant_id = @TenantId
                    """,
                    new { LeadId = leadId, TenantId, Archived = LeadRetention.Archived },
                    tx);
                var dismissed = await conn.ExecuteAsync(
                    """
                    UPDATE pack_sales.agent_task
                    SET status = 'dismissed',
                        completed_at = NOW(),
                        payload_json = payload_json || '{"reason":"lead_archived"}'::jsonb
                    WHERE tenant_id = @TenantId AND lead_id = @LeadId AND status = 'pending'
                    """,
                    new { LeadId = leadId, TenantId },
                    tx);
                await KitSalesLeadEventWriter.WriteAsync(
                    conn, tx, TenantId, leadId, "archived", lead.LeadStatus, LeadRetention.Archived,
                    JsonSerializer.Serialize(new { reason = "delete_requested_with_history", dismissedAgentTasks = dismissed }),
                    CurrentUserId, null, $"archived:{leadId:N}", cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return LeadRetention.Archived;
        }

        await conn.ExecuteAsync(
            """
            DELETE FROM pack_sales.lead
            WHERE id = @LeadId AND tenant_id = @TenantId
            """,
            new { LeadId = leadId, TenantId },
            tx);

        var business = await conn.QuerySingleAsync<(int Remaining, bool HasSignals)>(
            """
            SELECT
                (SELECT COUNT(*)::int FROM pack_sales.lead
                 WHERE business_id = @BusinessId AND tenant_id = @TenantId) AS Remaining,
                EXISTS (SELECT 1 FROM pack_sales.signal
                        WHERE business_id = @BusinessId AND tenant_id = @TenantId) AS HasSignals
            """,
            new { BusinessId = lead.BusinessId, TenantId },
            tx);
        if (LeadRetention.MayDeleteBusiness(true, business.Remaining, business.HasSignals))
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM pack_sales.business
                WHERE id = @BusinessId AND tenant_id = @TenantId
                """,
                new { BusinessId = lead.BusinessId, TenantId },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
        return LeadRetention.Deleted;
    }

    internal const string LeadHistorySql = """
        EXISTS (SELECT 1 FROM pack_sales.interaction x WHERE x.lead_id = l.id)
        OR EXISTS (SELECT 1 FROM pack_sales.lead_event x WHERE x.lead_id = l.id)
        OR EXISTS (SELECT 1 FROM pack_sales.task x WHERE x.lead_id = l.id)
        OR EXISTS (SELECT 1 FROM pack_sales.lead_pain x WHERE x.lead_id = l.id)
        OR EXISTS (SELECT 1 FROM pack_sales.signal x WHERE x.lead_id = l.id)
        OR EXISTS (SELECT 1 FROM pack_sales.agent_run x WHERE x.lead_id = l.id)
        OR EXISTS (SELECT 1 FROM pack_sales.agent_task x WHERE x.lead_id = l.id)
        OR l.assessment_submission_id IS NOT NULL
        OR l.lead_status <> 'discovered'
        """;

    public async Task<KitSalesLeadDetailDto?> GetLeadDetailAsync(
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var leadRow = await conn.QuerySingleOrDefaultAsync<LeadRow>(
            LeadSelectSql + " AND l.id = @LeadId",
            new { TenantId, LeadId = leadId });
        if (leadRow is null)
            return null;

        var interactions = await conn.QueryAsync<InteractionRow>(
            """
            SELECT i.id AS Id, i.lead_id AS LeadId, i.channel AS Channel, i.direction AS Direction,
                   i.interaction_type AS InteractionType, i.content AS Content, i.outcome AS Outcome,
                   i.occurred_at AS OccurredAt, i.review_status AS ReviewStatus,
                   i.ai_generated AS AiGenerated, i.approved_by_user_id AS ApprovedByUserId,
                   i.sent_at AS SentAt, i.classification AS Classification,
                   su.username AS SentByName, au.username AS ApprovedByName,
                   CASE WHEN i.channel = 'facebook' AND i.direction = 'outbound' THEN (
                       SELECT e.event_type FROM pack_sales.lead_event e
                       WHERE e.tenant_id = i.tenant_id AND e.lead_id = i.lead_id
                         AND e.event_type = ANY(@DispatchEvents)
                         AND e.payload->>'interactionId' = i.id::text
                         AND e.payload ? 'approvalEventId'
                       ORDER BY e.occurred_at DESC
                       LIMIT 1) END AS OutboundState
            FROM pack_sales.interaction i
            LEFT JOIN users su ON su.id = i.sent_by_user_id
            LEFT JOIN users au ON au.id = i.approved_by_user_id
            WHERE i.lead_id = @LeadId AND i.tenant_id = @TenantId
            ORDER BY i.occurred_at DESC
            LIMIT 50
            """,
            new
            {
                LeadId = leadId,
                TenantId,
                DispatchEvents = FacebookOutboundRules.DispatchOutcomeEvents
                    .Append("outbound_dispatching")
                    .Append(FacebookOutboundRules.UnknownResolvedEvent)
                    .ToArray(),
            });

        var tasks = await conn.QueryAsync<TaskRow>(
            """
            SELECT id AS Id, lead_id AS LeadId, action_code AS ActionCode, title AS Title,
                   status AS Status, due_at AS DueAt, completed_at AS CompletedAt,
                   created_at AS CreatedAt
            FROM pack_sales.task
            WHERE lead_id = @LeadId AND tenant_id = @TenantId
            ORDER BY CASE WHEN status = 'open' THEN 0 ELSE 1 END, due_at NULLS LAST, created_at DESC
            LIMIT 50
            """,
            new { LeadId = leadId, TenantId });

        return new KitSalesLeadDetailDto(
            MapLead(leadRow),
            interactions.Select(MapInteraction).ToList(),
            tasks.Select(MapTask).ToList());
    }

    public async Task<KitSalesInteractionDto> CreateInteractionAsync(
        Guid leadId,
        CreateKitSalesInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, leadId, cancellationToken);

        var review = NormalizeReview(request.ReviewStatus);
        if (FacebookOutboundRules.DenyCreate(request.Channel, request.Direction, review) is not null)
            throw new InvalidOperationException("Outbound Facebook chỉ được tạo ở trạng thái draft.");
        var markSent = request.MarkSent || review == "sent";
        var sentAt = markSent && review == "sent" ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        var row = await conn.QuerySingleAsync<InteractionRow>(
            """
            INSERT INTO pack_sales.interaction (
                tenant_id, lead_id, channel, direction, interaction_type, content, outcome,
                sent_by_user_id, review_status, ai_generated, approved_by_user_id, sent_at, classification
            )
            VALUES (
                @TenantId, @LeadId, @Channel, @Direction, @InteractionType, @Content, @Outcome,
                @UserId, @ReviewStatus, @AiGenerated, @ApprovedBy, @SentAt, @Classification
            )
            RETURNING id AS Id, lead_id AS LeadId, channel AS Channel, direction AS Direction,
                      interaction_type AS InteractionType, content AS Content, outcome AS Outcome,
                      occurred_at AS OccurredAt, review_status AS ReviewStatus, ai_generated AS AiGenerated,
                      approved_by_user_id AS ApprovedByUserId, sent_at AS SentAt, classification AS Classification
            """,
            new
            {
                TenantId,
                LeadId = leadId,
                Channel = Clip(NormalizeCode(request.Channel, "phone"), 32),
                Direction = Clip(NormalizeCode(request.Direction, "outbound"), 16),
                InteractionType = Clip(NormalizeCode(request.InteractionType, "note"), 32),
                Content = TrimOrNull(request.Content),
                Outcome = ClipOrNull(request.Outcome, 64),
                UserId = CurrentUserId,
                ReviewStatus = review,
                AiGenerated = request.AiGenerated,
                ApprovedBy = review is "approved" or "sent" ? CurrentUserId : null,
                SentAt = sentAt?.UtcDateTime,
                Classification = ClipOrNull(request.Classification, 64),
            },
            tx);

        if (review != "draft")
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_sales.lead
                SET last_interaction_at = NOW(), updated_at = NOW()
                WHERE id = @LeadId AND tenant_id = @TenantId
                """,
                new { LeadId = leadId, TenantId },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
        return await LoadInteractionAsync(conn, row.Id, cancellationToken);
    }

    public async Task<KitSalesInteractionDto> ReviewInteractionAsync(
        Guid interactionId,
        ReviewKitSalesInteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        var review = NormalizeReview(request.ReviewStatus);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var existing = await conn.QuerySingleOrDefaultAsync<InteractionRow>(
            """
            SELECT id AS Id, lead_id AS LeadId, channel AS Channel, direction AS Direction,
                   interaction_type AS InteractionType, content AS Content, outcome AS Outcome,
                   occurred_at AS OccurredAt, review_status AS ReviewStatus, ai_generated AS AiGenerated,
                   approved_by_user_id AS ApprovedByUserId, sent_at AS SentAt, classification AS Classification
            FROM pack_sales.interaction
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new { Id = interactionId, TenantId },
            tx);
        if (existing is null)
            throw new InvalidOperationException("Interaction không tồn tại.");
        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, existing.LeadId, cancellationToken);
        if (string.Equals(existing.Channel, "facebook", StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.Direction, "outbound", StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeReview(request.ReviewStatus), "sent", StringComparison.Ordinal))
            throw new InvalidOperationException("Outbound Facebook chỉ sang sent qua cổng đã duyệt.");
        if (string.Equals(existing.Channel, "facebook", StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.Direction, "outbound", StringComparison.OrdinalIgnoreCase)
            && (existing.SentAt is not null || existing.ReviewStatus == "sent"))
            throw new InvalidOperationException("Tin Facebook đã gửi, không mở lại được.");

        var content = TrimOrNull(request.Content) ?? existing.Content;
        DateTime? sentAt = review == "sent" ? DateTime.UtcNow : existing.SentAt;
        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.interaction
            SET content = @Content,
                review_status = @ReviewStatus,
                approved_by_user_id = COALESCE(@ApprovedBy, approved_by_user_id),
                sent_at = @SentAt
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new
            {
                Id = interactionId,
                TenantId,
                Content = content,
                ReviewStatus = review,
                ApprovedBy = CurrentUserId,
                SentAt = sentAt,
            },
            tx);

        if (review != "draft")
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_sales.lead
                SET last_interaction_at = NOW(), updated_at = NOW()
                WHERE id = @LeadId AND tenant_id = @TenantId
                """,
                new { LeadId = existing.LeadId, TenantId },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
        return await LoadInteractionAsync(conn, interactionId, cancellationToken);
    }

    private async Task<KitSalesInteractionDto> LoadInteractionAsync(
        System.Data.Common.DbConnection conn,
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await conn.QuerySingleAsync<InteractionRow>(
            """
            SELECT i.id AS Id, i.lead_id AS LeadId, i.channel AS Channel, i.direction AS Direction,
                   i.interaction_type AS InteractionType, i.content AS Content, i.outcome AS Outcome,
                   i.occurred_at AS OccurredAt, i.review_status AS ReviewStatus,
                   i.ai_generated AS AiGenerated, i.approved_by_user_id AS ApprovedByUserId,
                   i.sent_at AS SentAt, i.classification AS Classification,
                   su.username AS SentByName, au.username AS ApprovedByName
            FROM pack_sales.interaction i
            LEFT JOIN users su ON su.id = i.sent_by_user_id
            LEFT JOIN users au ON au.id = i.approved_by_user_id
            WHERE i.id = @Id AND i.tenant_id = @TenantId
            """,
            new { Id = id, TenantId });
        return MapInteraction(row);
    }

    public async Task<KitSalesTaskDto> CreateTaskAsync(
        Guid leadId,
        CreateKitSalesTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var actionCode = Clip(NormalizeCode(request.ActionCode, "follow_up"), 32);
        var title = TrimOrNull(request.Title) ?? actionCode;
        var dueAt = request.DueAt?.UtcDateTime;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, leadId, cancellationToken);

        var row = await conn.QuerySingleAsync<TaskRow>(
            """
            INSERT INTO pack_sales.task (
                tenant_id, lead_id, action_code, title, due_at, created_by_user_id
            )
            VALUES (
                @TenantId, @LeadId, @ActionCode, @Title, @DueAt, @UserId
            )
            RETURNING id AS Id, lead_id AS LeadId, action_code AS ActionCode, title AS Title,
                      status AS Status, due_at AS DueAt, completed_at AS CompletedAt,
                      created_at AS CreatedAt
            """,
            new
            {
                TenantId,
                LeadId = leadId,
                ActionCode = actionCode,
                Title = title,
                DueAt = dueAt,
                UserId = CurrentUserId,
            },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.lead
            SET next_action_code = @ActionCode,
                next_action_at = @DueAt,
                updated_at = NOW()
            WHERE id = @LeadId AND tenant_id = @TenantId
            """,
            new { LeadId = leadId, TenantId, ActionCode = actionCode, DueAt = dueAt },
            tx);

        await tx.CommitAsync(cancellationToken);
        return MapTask(row);
    }

    public async Task<KitSalesTaskDto> CompleteTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var existing = await conn.QuerySingleOrDefaultAsync<TaskRow>(
            """
            SELECT id AS Id, lead_id AS LeadId, action_code AS ActionCode, title AS Title,
                   status AS Status, due_at AS DueAt, completed_at AS CompletedAt,
                   created_at AS CreatedAt
            FROM pack_sales.task
            WHERE id = @TaskId AND tenant_id = @TenantId
            """,
            new { TaskId = taskId, TenantId },
            tx);
        if (existing is null)
            throw new InvalidOperationException("Task không tồn tại.");
        if (existing.Status == "done")
            return MapTask(existing);

        var row = await conn.QuerySingleAsync<TaskRow>(
            """
            UPDATE pack_sales.task
            SET status = 'done', completed_at = NOW()
            WHERE id = @TaskId AND tenant_id = @TenantId
            RETURNING id AS Id, lead_id AS LeadId, action_code AS ActionCode, title AS Title,
                      status AS Status, due_at AS DueAt, completed_at AS CompletedAt,
                      created_at AS CreatedAt
            """,
            new { TaskId = taskId, TenantId },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.lead
            SET next_action_code = NULL,
                next_action_at = NULL,
                updated_at = NOW()
            WHERE id = @LeadId
              AND tenant_id = @TenantId
              AND next_action_code = @ActionCode
            """,
            new { LeadId = row.LeadId, TenantId, ActionCode = row.ActionCode },
            tx);

        await tx.CommitAsync(cancellationToken);
        return MapTask(row);
    }

    public async Task<KitSalesPipelineSummaryDto> GetPipelineSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var buckets = await conn.QueryAsync<KitSalesPipelineBucketDto>(
            """
            SELECT lead_status AS Status, COUNT(*)::int AS Count
            FROM pack_sales.lead
            WHERE tenant_id = @TenantId AND lead_status <> @Archived
            GROUP BY lead_status
            ORDER BY Count DESC, lead_status
            """,
            new { TenantId, Archived = LeadRetention.Archived });
        var list = buckets.ToList();
        return new KitSalesPipelineSummaryDto(list.Sum(b => b.Count), list);
    }

    private async Task<KitSalesLeadDto> InsertLeadAsync(
        System.Data.Common.DbConnection conn,
        CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken,
        System.Data.Common.DbTransaction? tx = null)
    {
        var businessExists = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_sales.business
                WHERE id = @BusinessId AND tenant_id = @TenantId
            )
            """,
            new { request.BusinessId, TenantId },
            tx);
        if (!businessExists)
            throw new InvalidOperationException("Business không tồn tại trong tenant.");

        var productCode = NormalizeCode(request.ProductCode, "novixa");
        await EnsureProductAsync(conn, productCode, tx);

        var row = await conn.QuerySingleAsync<LeadRow>(
            """
            WITH inserted AS (
                INSERT INTO pack_sales.lead (
                    tenant_id, business_id, product_code, source, lead_status, lead_temperature, notes
                )
                VALUES (
                    @TenantId, @BusinessId, @ProductCode, @Source, @LeadStatus, @LeadTemperature, @Notes
                )
                RETURNING *
            )
            SELECT i.id AS Id, i.business_id AS BusinessId, b.name AS BusinessName,
                   i.product_code AS ProductCode, i.lead_status AS LeadStatus,
                   i.lead_temperature AS LeadTemperature, i.total_score AS TotalScore,
                   i.source AS Source, b.province AS Province, b.phone AS Phone,
                   i.notes AS Notes, i.owner_user_id AS OwnerUserId,
                   i.next_action_code AS NextActionCode, i.next_action_at AS NextActionAt,
                   i.last_interaction_at AS LastInteractionAt,
                   i.created_at AS CreatedAt, i.updated_at AS UpdatedAt
            FROM inserted i
            INNER JOIN pack_sales.business b ON b.id = i.business_id
            """,
            new
            {
                TenantId,
                request.BusinessId,
                ProductCode = productCode,
                Source = TrimOrNull(request.Source),
                LeadStatus = NormalizeCode(request.LeadStatus, "discovered"),
                LeadTemperature = NormalizeCode(request.LeadTemperature, "cold"),
                Notes = TrimOrNull(request.Notes),
            },
            tx);
        return MapLead(row);
    }

    private const string LeadSelectSql = """
        SELECT l.id AS Id, l.business_id AS BusinessId, b.name AS BusinessName,
               l.product_code AS ProductCode, l.lead_status AS LeadStatus,
               l.lead_temperature AS LeadTemperature, l.total_score AS TotalScore,
               l.source AS Source, b.province AS Province, b.phone AS Phone,
               l.notes AS Notes, l.owner_user_id AS OwnerUserId,
               l.next_action_code AS NextActionCode, l.next_action_at AS NextActionAt,
               l.last_interaction_at AS LastInteractionAt,
               l.created_at AS CreatedAt, l.updated_at AS UpdatedAt,
               fb.platform AS FacebookKind, COALESCE(fb.url, l.source, b.source) AS FacebookUrl
        FROM pack_sales.lead l
        INNER JOIN pack_sales.business b ON b.id = l.business_id
        LEFT JOIN LATERAL (
            SELECT i.platform, i.url
            FROM pack_sales.business_identity i
            WHERE i.business_id = b.id
              AND i.tenant_id = l.tenant_id
              AND i.identity_type = 'facebook'
              AND i.platform IN ('profile', 'page')
            ORDER BY i.created_at DESC
            LIMIT 1
        ) fb ON TRUE
        WHERE l.tenant_id = @TenantId
        """;

    private static async Task EnsureProductAsync(
        System.Data.IDbConnection conn,
        string productCode,
        System.Data.IDbTransaction? tx = null)
    {
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM pack_sales.product WHERE code = @ProductCode)",
            new { ProductCode = productCode },
            tx);
        if (!exists)
            throw new ArgumentException($"Product '{productCode}' chưa được khai báo.");
    }

    private static KitSalesBusinessDto MapBusiness(BusinessRow r) => new(
        r.Id,
        r.Name,
        r.BusinessType,
        r.Province,
        r.Phone,
        r.Status,
        r.Source,
        ToOffset(r.CreatedAt),
        ToOffset(r.UpdatedAt));

    private static KitSalesInteractionDto MapInteraction(InteractionRow r) => new(
        r.Id,
        r.LeadId,
        r.Channel,
        r.Direction,
        r.InteractionType,
        r.Content,
        r.Outcome,
        ToOffset(r.OccurredAt),
        string.IsNullOrWhiteSpace(r.ReviewStatus) ? "sent" : r.ReviewStatus,
        r.AiGenerated,
        r.ApprovedByUserId,
        ToOffset(r.SentAt),
        r.Classification,
        r.SentByName,
        r.ApprovedByName,
        r.OutboundState);

    private static KitSalesTaskDto MapTask(TaskRow r) => new(
        r.Id,
        r.LeadId,
        r.ActionCode,
        r.Title,
        r.Status,
        ToOffset(r.DueAt),
        ToOffset(r.CompletedAt),
        ToOffset(r.CreatedAt));

    private static KitSalesLeadDto MapLead(LeadRow r)
    {
        var facebook = NovixaFacebookSource.Resolve(r.FacebookKind, r.FacebookUrl ?? r.Source);
        return new(
            r.Id,
            r.BusinessId,
            r.BusinessName,
            r.ProductCode,
            r.LeadStatus,
            r.LeadTemperature,
            r.TotalScore,
            r.Source,
            string.IsNullOrWhiteSpace(r.Province)
                ? r.Province
                : NovixaFacebookSource.NormalizeProvince(r.Province),
            r.Phone,
            r.Notes,
            r.OwnerUserId,
            r.NextActionCode,
            ToOffset(r.NextActionAt),
            ToOffset(r.LastInteractionAt),
            ToOffset(r.CreatedAt),
            ToOffset(r.UpdatedAt),
            facebook.Kind,
            facebook.Url);
    }

    private async Task<KitSalesLeadDto> GetMappedLeadAsync(
        System.Data.Common.DbConnection conn,
        Guid leadId,
        CancellationToken cancellationToken)
    {
        var row = await conn.QuerySingleAsync<LeadRow>(
            LeadSelectSql + " AND l.id = @LeadId",
            new { TenantId, LeadId = leadId });
        return MapLead(row);
    }

    private async Task UpsertFacebookIdentityAsync(
        System.Data.Common.DbConnection conn,
        Guid businessId,
        string? source,
        string? facebookKind,
        System.Data.Common.DbTransaction? tx,
        CancellationToken cancellationToken)
    {
        var resolved = NovixaFacebookSource.Resolve(facebookKind, source);
        if (resolved.Kind is null && resolved.Url is null)
            return;

        var existingId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id
            FROM pack_sales.business_identity
            WHERE tenant_id = @TenantId
              AND business_id = @BusinessId
              AND identity_type = 'facebook'
              AND platform IN ('profile', 'page')
            ORDER BY created_at DESC
            LIMIT 1
            """,
            new { TenantId, BusinessId = businessId },
            tx);

        if (existingId is null)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_sales.business_identity (
                    tenant_id, business_id, identity_type, platform, url, username, source
                )
                VALUES (
                    @TenantId, @BusinessId, 'facebook', @Platform, @Url, @Username, 'lead_source'
                )
                """,
                new
                {
                    TenantId,
                    BusinessId = businessId,
                    Platform = resolved.Kind ?? NovixaFacebookSource.Profile,
                    Url = resolved.Url ?? TrimOrNull(source),
                    Username = resolved.Username,
                },
                tx);
            return;
        }

        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.business_identity
            SET platform = COALESCE(@Platform, platform),
                url = COALESCE(@Url, url),
                username = COALESCE(@Username, username)
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new
            {
                Id = existingId.Value,
                TenantId,
                Platform = resolved.Kind,
                Url = resolved.Url,
                Username = resolved.Username,
            },
            tx);
    }

    private async Task<Guid> LinkAssessmentAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid leadId,
        LeadLookupRow existing,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        PhcLinkLock.EnsureOpen();
        var json = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT qualification_json::text
                FROM pack_sales.product_profile
                WHERE product_code = @Product AND status = 'active'
                ORDER BY effective_from DESC
                LIMIT 1
                """,
                new { Product = existing.ProductCode },
                tx,
                cancellationToken: cancellationToken));
        var row = await _assessments.FindAsync(submissionId, cancellationToken);
        var deny = KitSalesAssessmentLookup.Deny(
            row,
            SalesPackDefinition.DedicatedTenantId,
            KitSalesAgentService.TemplateCodes(json),
            existing.Phone);
        if (deny is not null)
            throw new InvalidOperationException(deny);
        var taken = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS(
                    SELECT 1 FROM pack_sales.lead
                    WHERE tenant_id = @TenantId
                      AND assessment_submission_id = @SubmissionId
                      AND id <> @LeadId
                      AND lead_status <> 'lost')
                """,
                new { TenantId, SubmissionId = submissionId, LeadId = leadId },
                tx,
                cancellationToken: cancellationToken));
        if (taken)
            throw new InvalidOperationException("Bài PHC đã gắn lead khác.");
        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, leadId, "phc_linked", null, null,
            JsonSerializer.Serialize(new
            {
                submissionId,
                templateCode = row!.TemplateCode,
                submissionStatus = row.Status,
                phoneMatch = KitSalesAssessmentLookup.PhoneMatch(row, existing.Phone),
            }),
            CurrentUserId, null, $"phc_linked:{leadId:N}:{submissionId:N}", cancellationToken);
        return submissionId;
    }

    private async Task EnsureStageAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid leadId,
        LeadLookupRow existing,
        string newStatus,
        Guid? assessmentId,
        UpdateKitSalesLeadRequest request,
        CancellationToken cancellationToken)
    {
        var hasReport = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS(
                    SELECT 1 FROM pack_sales.lead_event
                    WHERE tenant_id = @TenantId AND lead_id = @LeadId AND event_type = 'phc_completed')
                """,
                new { TenantId, LeadId = leadId },
                tx,
                cancellationToken: cancellationToken));
        var hasWaiver = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS(
                    SELECT 1 FROM pack_sales.lead_event
                    WHERE tenant_id = @TenantId AND lead_id = @LeadId
                      AND event_type = 'stage_waiver' AND to_status = @ToStatus)
                """,
                new { TenantId, LeadId = leadId, ToStatus = newStatus },
                tx,
                cancellationToken: cancellationToken));
        var hasTask = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS(
                    SELECT 1 FROM pack_sales.task
                    WHERE tenant_id = @TenantId AND lead_id = @LeadId
                      AND action_code = 'phc' AND status = 'open')
                """,
                new { TenantId, LeadId = leadId },
                tx,
                cancellationToken: cancellationToken));
        var reason = string.IsNullOrWhiteSpace(request.LostReasonCode)
            ? null
            : NormalizeCode(request.LostReasonCode, "");
        var hasReason = reason is not null && LeadStagePolicy.LostReasons.Contains(reason)
            && (reason != "other" || !string.IsNullOrWhiteSpace(request.LostNote));
        var fact = string.IsNullOrWhiteSpace(request.PaidFactCode) ? null : NormalizeCode(request.PaidFactCode, "");
        var deny = LeadStagePolicy.Deny(
            existing.LeadStatus,
            newStatus,
            assessmentId is not null || hasTask,
            hasReport,
            hasWaiver,
            hasReason,
            fact);
        if (deny is not null)
            throw new InvalidOperationException($"Không chuyển được trạng thái: {deny}.");

        var payload = newStatus switch
        {
            "lost" => JsonSerializer.Serialize(new { reasonCode = reason, note = request.LostNote }),
            "paid" => JsonSerializer.Serialize(new
            {
                confirmation = "human",
                factCode = fact,
                amountVnd = LeadStagePolicy.PaidAmount(fact!),
            }),
            _ => "{}",
        };
        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, leadId, "stage_entered", existing.LeadStatus, newStatus, payload,
            CurrentUserId, null,
            $"stage:{leadId:N}:{existing.LeadStatus}:{newStatus}:{DateTime.UtcNow.Ticks}",
            cancellationToken);
        if (newStatus == "lost")
        {
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, TenantId, leadId, "lost", existing.LeadStatus, "lost", payload,
                CurrentUserId, null, $"lost:{leadId:N}:{DateTime.UtcNow.Ticks}", cancellationToken);
        }
    }

    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : ToOffset(value.Value);

    private static string NormalizeReview(string? value)
    {
        var code = NormalizeCode(value, "sent");
        return code is "draft" or "approved" or "sent" ? code : "sent";
    }

    private static string NormalizeCode(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed.ToLowerInvariant();
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string? ClipOrNull(string? value, int max)
    {
        var trimmed = TrimOrNull(value);
        return trimmed is null ? null : Clip(trimmed, max);
    }

    private sealed class ProductRow
    {
        public string Code { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Status { get; init; } = "";
    }

    private sealed class BusinessRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string BusinessType { get; init; } = "";
        public string? Province { get; init; }
        public string? Phone { get; init; }
        public string Status { get; init; } = "";
        public string? Source { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private sealed class RetentionRow
    {
        public Guid BusinessId { get; init; }
        public string LeadStatus { get; init; } = "";
        public bool HasHistory { get; init; }
    }

    private sealed class LeadLookupRow
    {
        public Guid BusinessId { get; init; }
        public string Name { get; init; } = "";
        public string? Province { get; init; }
        public string? Phone { get; init; }
        public string? BusinessSource { get; init; }
        public string? LeadSource { get; init; }
        public string? Notes { get; init; }
        public string? NextActionCode { get; init; }
        public DateTime? NextActionAt { get; init; }
        public string LeadStatus { get; init; } = "discovered";
        public string ProductCode { get; init; } = "novixa";
        public Guid? AssessmentSubmissionId { get; init; }
    }

    private sealed class InteractionRow
    {
        public Guid Id { get; init; }
        public Guid LeadId { get; init; }
        public string Channel { get; init; } = "";
        public string Direction { get; init; } = "";
        public string InteractionType { get; init; } = "";
        public string? Content { get; init; }
        public string? Outcome { get; init; }
        public DateTime OccurredAt { get; init; }
        public string ReviewStatus { get; init; } = "sent";
        public bool AiGenerated { get; init; }
        public Guid? ApprovedByUserId { get; init; }
        public DateTime? SentAt { get; init; }
        public string? Classification { get; init; }
        public string? SentByName { get; init; }
        public string? ApprovedByName { get; init; }
        public string? OutboundState { get; init; }
    }

    private sealed class TaskRow
    {
        public Guid Id { get; init; }
        public Guid LeadId { get; init; }
        public string ActionCode { get; init; } = "";
        public string Title { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime? DueAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class LeadRow
    {
        public Guid Id { get; init; }
        public Guid BusinessId { get; init; }
        public string BusinessName { get; init; } = "";
        public string ProductCode { get; init; } = "";
        public string LeadStatus { get; init; } = "";
        public string LeadTemperature { get; init; } = "";
        public decimal TotalScore { get; init; }
        public string? Source { get; init; }
        public string? Province { get; init; }
        public string? Phone { get; init; }
        public string? Notes { get; init; }
        public Guid? OwnerUserId { get; init; }
        public string? NextActionCode { get; init; }
        public DateTime? NextActionAt { get; init; }
        public DateTime? LastInteractionAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string? FacebookKind { get; init; }
        public string? FacebookUrl { get; init; }
    }
}
