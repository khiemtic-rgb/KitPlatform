using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;
using Microsoft.Extensions.Configuration;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Single Facebook outbound path. Three steps: B1 reserves one dispatch per approval in a short
/// transaction, B2 re-checks the channel and makes one HTTP call outside any transaction,
/// B3 records sent / failed / unknown. Unknown is never retried; a human must check the Page inbox.
/// </summary>
internal sealed class FacebookOutboundGate : IFacebookOutboundGate
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _configuration;
    private readonly KitSalesFacebookSettingsService _settings;
    private readonly FacebookGraphSender _graph;

    public FacebookOutboundGate(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IConfiguration configuration,
        IHttpClientFactory http,
        KitSalesFacebookSettingsService settings)
    {
        _db = db;
        _tenant = tenant;
        _configuration = configuration;
        _settings = settings;
        _graph = new FacebookGraphSender(() => http.CreateClient("kit-sales-facebook"));
    }

    private Guid TenantId => _tenant.TenantId;
    private Guid? ActorId => _tenant.UserId == Guid.Empty ? null : _tenant.UserId;

    public async Task<KitSalesInteractionDto> ApproveAsync(
        Guid interactionId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var row = await LoadAsync(conn, tx, interactionId, cancellationToken)
            ?? throw new InvalidOperationException("Interaction không tồn tại.");
        if (!string.Equals(row.Channel, "facebook", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(row.Direction, "outbound", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ duyệt outbound Facebook.");
        if (row.ReviewStatus != "draft")
            throw new InvalidOperationException("Chỉ duyệt được bản draft.");
        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, row.LeadId, cancellationToken);
        if (await DispatchInFlightAsync(conn, tx, row, cancellationToken))
            throw new InvalidOperationException("Tin này đang được gửi. Chờ kết quả trước khi duyệt lại.");

        var body = string.IsNullOrWhiteSpace(content) ? row.Content : content.Trim();
        var hash = FacebookOutboundRules.Hash(body);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.interaction
                SET content = @Content,
                    review_status = 'approved',
                    approved_by_user_id = @Actor,
                    sent_at = NULL
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Content = body, Actor = ActorId, Id = interactionId, TenantId },
                tx,
                cancellationToken: cancellationToken));

        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, row.LeadId, "outbound_approved", null, null,
            JsonSerializer.Serialize(new { interactionId, contentHash = hash }),
            ActorId, null, $"outbound_approved:{interactionId:N}:{hash}:{DateTime.UtcNow.Ticks}", cancellationToken);
        await tx.CommitAsync(cancellationToken);
        row.Content = body;
        row.ReviewStatus = "approved";
        row.ApprovedByUserId = ActorId;
        return Map(row);
    }

    /// <summary>
    /// After an unknown dispatch a person checks the Page inbox. Delivered: mark sent with a note.
    /// Not delivered: back to draft, so it needs a fresh approval. No HTTP on this path.
    /// </summary>
    public async Task<KitSalesInteractionDto> ResolveUnknownAsync(
        Guid interactionId,
        ResolveKitSalesOutboundRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var row = await LoadAsync(conn, tx, interactionId, cancellationToken)
            ?? throw new InvalidOperationException("Interaction không tồn tại.");
        if (!string.Equals(row.Channel, "facebook", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(row.Direction, "outbound", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ xử lý outbound Facebook.");

        var dispatch = await conn.QuerySingleOrDefaultAsync<DispatchRow>(
            new CommandDefinition(
                """
                SELECT d.payload->>'approvalEventId' AS ApprovalEventId,
                       d.payload->>'contentHash' AS ContentHash,
                       (SELECT o.event_type FROM pack_sales.lead_event o
                        WHERE o.tenant_id = d.tenant_id AND o.lead_id = d.lead_id
                          AND o.event_type = ANY(@Outcomes)
                          AND o.payload->>'approvalEventId' = d.payload->>'approvalEventId'
                        ORDER BY o.occurred_at DESC LIMIT 1) AS Outcome,
                       EXISTS (SELECT 1 FROM pack_sales.lead_event r
                        WHERE r.tenant_id = d.tenant_id AND r.lead_id = d.lead_id
                          AND r.event_type = @Resolved
                          AND r.payload->>'approvalEventId' = d.payload->>'approvalEventId') AS Resolved
                FROM pack_sales.lead_event d
                WHERE d.tenant_id = @TenantId AND d.lead_id = @LeadId
                  AND d.event_type = 'outbound_dispatching'
                  AND d.payload->>'interactionId' = @InteractionId
                ORDER BY d.occurred_at DESC
                LIMIT 1
                """,
                new
                {
                    TenantId,
                    row.LeadId,
                    InteractionId = interactionId.ToString(),
                    Outcomes = FacebookOutboundRules.DispatchOutcomeEvents.ToArray(),
                    Resolved = FacebookOutboundRules.UnknownResolvedEvent,
                },
                tx,
                cancellationToken: cancellationToken));

        var note = request.Note?.Trim();
        var deny = FacebookOutboundRules.DenyResolve(
            dispatch?.Outcome, dispatch?.Resolved == true, row.SentAt is not null, request.Delivered, note);
        if (deny is not null || !Guid.TryParse(dispatch?.ApprovalEventId, out var approvalEventId))
            throw new InvalidOperationException($"Không xử lý được: {deny ?? "dispatch_missing"}.");

        var resolution = request.Delivered ? "sent" : "not_sent";
        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, row.LeadId, FacebookOutboundRules.UnknownResolvedEvent, null, null,
            JsonSerializer.Serialize(new { interactionId, approvalEventId, resolution }),
            ActorId, null, $"{FacebookOutboundRules.UnknownResolvedEvent}:{interactionId:N}:{approvalEventId:N}",
            cancellationToken);

        if (request.Delivered)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_sales.interaction
                    SET review_status = 'sent', sent_at = NOW(), summary = @Note
                    WHERE id = @Id AND tenant_id = @TenantId AND sent_at IS NULL
                    """,
                    new { Id = interactionId, TenantId, Note = note },
                    tx,
                    cancellationToken: cancellationToken));
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, TenantId, row.LeadId, "outbound_sent", null, null,
                JsonSerializer.Serialize(new
                {
                    interactionId,
                    approvalEventId,
                    contentHash = dispatch!.ContentHash,
                    messageId = (string?)null,
                    confirmedBy = "human",
                }),
                ActorId, null, $"outbound_sent:{interactionId:N}", cancellationToken);
        }
        else
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_sales.interaction
                    SET review_status = 'draft', approved_by_user_id = NULL, summary = COALESCE(@Note, summary)
                    WHERE id = @Id AND tenant_id = @TenantId AND sent_at IS NULL
                    """,
                    new { Id = interactionId, TenantId, Note = note },
                    tx,
                    cancellationToken: cancellationToken));
        }

        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.task
                SET status = 'done', completed_at = NOW()
                WHERE tenant_id = @TenantId AND lead_id = @LeadId
                  AND action_code = @Action AND status = 'open'
                """,
                new { TenantId, row.LeadId, Action = FacebookOutboundReconciler.TaskAction },
                tx,
                cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);

        row.ReviewStatus = request.Delivered ? "sent" : "draft";
        if (request.Delivered)
            row.SentAt = DateTime.UtcNow;
        else
            row.ApprovedByUserId = null;
        return Map(row);
    }

    public async Task<KitSalesInteractionDto> SendApprovedAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await ReserveAsync(interactionId, cancellationToken);
        var token = await PreflightAsync(reservation);
        var result = await _graph.SendOnceAsync(token, reservation.Psid, reservation.Content, CancellationToken.None);
        return await RecordOutcomeAsync(reservation, result);
    }

    /// <summary>B1: short transaction. Every check, then one dispatch key per approval.</summary>
    private async Task<Reservation> ReserveAsync(Guid interactionId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var row = await LoadAsync(conn, tx, interactionId, cancellationToken)
            ?? throw new InvalidOperationException("Interaction không tồn tại.");
        var hash = FacebookOutboundRules.Hash(row.Content);
        var approval = await conn.QuerySingleOrDefaultAsync<ApprovalRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, payload->>'contentHash' AS ContentHash, occurred_at AS ApprovedAt, NOW() AS DbNow
                FROM pack_sales.lead_event
                WHERE tenant_id = @TenantId
                  AND lead_id = @LeadId
                  AND event_type = 'outbound_approved'
                  AND payload->>'interactionId' = @InteractionId
                ORDER BY occurred_at DESC
                LIMIT 1
                """,
                new { TenantId, row.LeadId, InteractionId = interactionId.ToString() },
                tx,
                cancellationToken: cancellationToken));
        var closed = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS(
                    SELECT 1 FROM pack_sales.lead
                    WHERE id = @LeadId AND tenant_id = @TenantId AND lead_status = ANY(@Closed))
                """,
                new { row.LeadId, TenantId, Closed = LeadRetention.ClosedStatuses.ToArray() },
                tx,
                cancellationToken: cancellationToken));
        var inFlight = await DispatchInFlightAsync(conn, tx, row, cancellationToken);
        var settings = await _settings.GetFreshAsync(cancellationToken);
        var now = approval?.DbNow is { } dbNow ? Utc(dbNow) : DateTimeOffset.UtcNow;

        var deny = FacebookOutboundRules.Deny(
                row.Channel,
                row.Direction,
                row.ReviewStatus,
                row.ApprovedByUserId,
                row.SentAt is not null,
                row.Content,
                approval?.ContentHash,
                hash,
                closed)
            ?? FacebookOutboundRules.DenyDispatch(
                ReadChannelFlag(),
                settings.Enabled,
                approval?.Id,
                approval is null ? null : Utc(approval.ApprovedAt),
                now,
                inFlight);

        string? psid = null;
        if (deny is null)
        {
            psid = await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    """
                    SELECT i.username
                    FROM pack_sales.business_identity i
                    INNER JOIN pack_sales.lead l ON l.business_id = i.business_id AND l.tenant_id = i.tenant_id
                    WHERE l.id = @LeadId AND l.tenant_id = @TenantId
                      AND i.identity_type = 'facebook' AND i.platform = 'messenger'
                      AND i.username IS NOT NULL
                    ORDER BY i.created_at DESC
                    LIMIT 1
                    """,
                    new { row.LeadId, TenantId },
                    tx,
                    cancellationToken: cancellationToken));
            if (string.IsNullOrWhiteSpace(psid))
                deny = "no_psid";
        }

        if (deny is null)
        {
            var inbound = await conn.QuerySingleOrDefaultAsync<InboundRow>(
                new CommandDefinition(
                    """
                    SELECT occurred_at AS ReceivedAt, payload->>'sentAtMs' AS SentAtMs
                    FROM pack_sales.lead_event
                    WHERE tenant_id = @TenantId AND lead_id = @LeadId AND event_type = @EventType
                    ORDER BY occurred_at DESC
                    LIMIT 1
                    """,
                    new { TenantId, row.LeadId, EventType = FacebookOutboundRules.InboundEvent },
                    tx,
                    cancellationToken: cancellationToken));
            DateTimeOffset? lastInboundAt = inbound is null
                ? null
                : FacebookOutboundRules.InboundAt(
                    Utc(inbound.ReceivedAt),
                    long.TryParse(inbound.SentAtMs, out var ms) ? ms : (long?)null);
            deny = FacebookOutboundRules.DenyMessengerWindow(lastInboundAt, now);
        }

        if (deny is null)
        {
            var reserved = await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, TenantId, row.LeadId, "outbound_dispatching", null, null,
                JsonSerializer.Serialize(new { interactionId, approvalEventId = approval!.Id, contentHash = hash }),
                ActorId, null, FacebookOutboundRules.DispatchKey(interactionId, approval.Id), cancellationToken);
            if (reserved == 0)
                deny = "already_dispatched";
        }

        if (deny is not null)
        {
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, TenantId, row.LeadId, "outbound_blocked", null, null,
                JsonSerializer.Serialize(new { interactionId, approvalRef = approval?.Id, reason = deny, stage = "reserve" }),
                ActorId, null, $"outbound_blocked:{interactionId:N}:{deny}:{DateTime.UtcNow.Ticks}", cancellationToken);
            await tx.CommitAsync(cancellationToken);
            throw new InvalidOperationException($"Facebook gửi bị chặn: {deny}.");
        }

        await tx.CommitAsync(cancellationToken);
        return new Reservation(row, approval!.Id, psid!, row.Content!.Trim(), hash);
    }

    /// <summary>B2: outside any transaction, right before HTTP. Any doubt blocks.</summary>
    private async Task<string> PreflightAsync(Reservation reservation)
    {
        string? deny;
        string? token = null;
        try
        {
            var settings = await _settings.GetFreshAsync(CancellationToken.None);
            token = settings.PageAccessToken?.Trim();
            deny = ReadChannelFlag() is not true ? "channel_off"
                : !settings.Enabled ? "settings_off"
                : string.IsNullOrWhiteSpace(token) ? "no_page_token"
                : null;
        }
        catch (Exception)
        {
            deny = "preflight_read_failed";
        }

        if (deny is null)
            return token!;

        await WriteOutcomeAsync(
            reservation,
            "outbound_blocked",
            $"outbound_blocked:{reservation.Row.Id:N}:{reservation.ApprovalEventId:N}:pre_http",
            new { reason = deny, stage = "pre_http" },
            task: null);
        throw new InvalidOperationException($"Facebook gửi bị chặn: {deny}. Tin chưa đi; duyệt lại khi mở kênh.");
    }

    /// <summary>B3: record what HTTP told us. Runs even if the caller went away.</summary>
    private async Task<KitSalesInteractionDto> RecordOutcomeAsync(Reservation reservation, GraphDispatchResult result)
    {
        var row = reservation.Row;
        switch (result.Outcome)
        {
            case "sent":
                await using (var conn = await _db.CreateOpenConnectionAsync(CancellationToken.None))
                await using (var tx = await conn.BeginTransactionAsync(CancellationToken.None))
                {
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            """
                            UPDATE pack_sales.interaction
                            SET review_status = 'sent', sent_at = NOW()
                            WHERE id = @Id AND tenant_id = @TenantId AND sent_at IS NULL
                            """,
                            new { Id = row.Id, TenantId },
                            tx));
                    await KitSalesLeadEventWriter.WriteAsync(
                        conn, tx, TenantId, row.LeadId, "outbound_sent", null, null,
                        JsonSerializer.Serialize(new
                        {
                            interactionId = row.Id,
                            approvalEventId = reservation.ApprovalEventId,
                            contentHash = reservation.ContentHash,
                            messageId = result.MessageId,
                        }),
                        ActorId, null, $"outbound_sent:{row.Id:N}", CancellationToken.None);
                    await tx.CommitAsync(CancellationToken.None);
                }

                row.ReviewStatus = "sent";
                row.SentAt = DateTime.UtcNow;
                return Map(row);

            case "failed":
                await WriteOutcomeAsync(
                    reservation,
                    "outbound_failed",
                    $"outbound_failed:{row.Id:N}:{reservation.ApprovalEventId:N}",
                    new { status = result.Status, error = result.Error },
                    task: null);
                throw new InvalidOperationException(
                    $"Messenger từ chối tin ({result.Error ?? "lỗi"}). Tin chưa đi; sửa và duyệt lại để gửi.");

            default:
                await WriteOutcomeAsync(
                    reservation,
                    "outbound_unknown",
                    FacebookOutboundReconciler.UnknownKey(row.Id, reservation.ApprovalEventId),
                    new { status = result.Status, error = result.Error },
                    task: FacebookOutboundReconciler.TaskTitle);
                throw new InvalidOperationException(
                    "Không rõ tin đã đi hay chưa. Đã tạo việc kiểm hộp thư Page; hệ thống không tự gửi lại.");
        }
    }

    private async Task WriteOutcomeAsync(
        Reservation reservation,
        string eventType,
        string key,
        object detail,
        string? task)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(CancellationToken.None);
        await using var tx = await conn.BeginTransactionAsync(CancellationToken.None);
        var payload = JsonSerializer.SerializeToElement(detail).EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value);
        payload["interactionId"] = reservation.Row.Id;
        payload["approvalEventId"] = reservation.ApprovalEventId;
        var written = await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, reservation.Row.LeadId, eventType, null, null,
            JsonSerializer.Serialize(payload), ActorId, null, key, CancellationToken.None);
        if (written > 0 && task is not null)
            await FacebookOutboundReconciler.InsertTaskAsync(conn, tx, TenantId, reservation.Row.LeadId, task, ActorId);
        await tx.CommitAsync(CancellationToken.None);
    }

    private bool? ReadChannelFlag()
    {
        try
        {
            return _configuration.GetValue<bool?>(FacebookOutboundRules.ChannelFlagKey);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Task<bool> DispatchInFlightAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        InteractionGateRow row,
        CancellationToken cancellationToken) =>
        conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1 FROM pack_sales.lead_event d
                    WHERE d.tenant_id = @TenantId
                      AND d.lead_id = @LeadId
                      AND d.event_type = 'outbound_dispatching'
                      AND d.payload->>'interactionId' = @InteractionId
                      AND NOT EXISTS (
                          SELECT 1 FROM pack_sales.lead_event o
                          WHERE o.tenant_id = d.tenant_id
                            AND o.lead_id = d.lead_id
                            AND o.event_type = ANY(@Outcomes)
                            AND o.payload->>'approvalEventId' = d.payload->>'approvalEventId'))
                """,
                new
                {
                    TenantId,
                    row.LeadId,
                    InteractionId = row.Id.ToString(),
                    Outcomes = FacebookOutboundRules.DispatchOutcomeEvents.ToArray(),
                },
                tx,
                cancellationToken: cancellationToken));

    private async Task<InteractionGateRow?> LoadAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid id,
        CancellationToken cancellationToken) =>
        await conn.QuerySingleOrDefaultAsync<InteractionGateRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, lead_id AS LeadId, channel AS Channel, direction AS Direction,
                       interaction_type AS InteractionType, content AS Content, outcome AS Outcome,
                       occurred_at AS OccurredAt, review_status AS ReviewStatus,
                       ai_generated AS AiGenerated, approved_by_user_id AS ApprovedByUserId,
                       sent_at AS SentAt, classification AS Classification
                FROM pack_sales.interaction
                WHERE id = @Id AND tenant_id = @TenantId
                FOR UPDATE
                """,
                new { Id = id, TenantId },
                tx,
                cancellationToken: cancellationToken));

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static KitSalesInteractionDto Map(InteractionGateRow row) =>
        new(
            row.Id,
            row.LeadId,
            row.Channel,
            row.Direction,
            row.InteractionType,
            row.Content,
            row.Outcome,
            new DateTimeOffset(DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc)),
            row.ReviewStatus,
            row.AiGenerated,
            row.ApprovedByUserId,
            row.SentAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(row.SentAt.Value, DateTimeKind.Utc)),
            row.Classification);

    private sealed record Reservation(
        InteractionGateRow Row,
        Guid ApprovalEventId,
        string Psid,
        string Content,
        string ContentHash);

    private sealed class DispatchRow
    {
        public string? ApprovalEventId { get; init; }
        public string? ContentHash { get; init; }
        public string? Outcome { get; init; }
        public bool Resolved { get; init; }
    }

    private sealed class InboundRow
    {
        public DateTime ReceivedAt { get; init; }
        public string? SentAtMs { get; init; }
    }

    private sealed class ApprovalRow
    {
        public Guid Id { get; init; }
        public string? ContentHash { get; init; }
        public DateTime ApprovedAt { get; init; }
        public DateTime DbNow { get; init; }
    }

    private sealed class InteractionGateRow
    {
        public Guid Id { get; init; }
        public Guid LeadId { get; init; }
        public string Channel { get; set; } = "";
        public string Direction { get; set; } = "";
        public string InteractionType { get; set; } = "";
        public string? Content { get; set; }
        public string? Outcome { get; set; }
        public DateTime OccurredAt { get; init; }
        public string ReviewStatus { get; set; } = "";
        public bool AiGenerated { get; init; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? SentAt { get; set; }
        public string? Classification { get; init; }
    }
}

internal sealed record GraphDispatchResult(string Outcome, int? Status, string? MessageId, string? Error);

/// <summary>One HTTP attempt to Messenger. No retry. Classification lives in <see cref="FacebookOutboundRules"/>.</summary>
internal sealed class FacebookGraphSender
{
    internal const string Endpoint = "https://graph.facebook.com/v21.0/me/messages";
    private readonly Func<HttpClient> _client;

    public FacebookGraphSender(Func<HttpClient> client) => _client = client;

    public async Task<GraphDispatchResult> SendOnceAsync(
        string pageAccessToken,
        string recipientPsid,
        string text,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(FacebookOutboundRules.HttpTimeout);
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + pageAccessToken);
        req.Content = JsonContent.Create(new
        {
            recipient = new { id = recipientPsid },
            messaging_type = "RESPONSE",
            message = new { text },
        });

        HttpResponseMessage res;
        try
        {
            res = await _client().SendAsync(req, timeout.Token);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError is
            HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError)
        {
            return new GraphDispatchResult(
                FacebookOutboundRules.ClassifyDispatch(null, false, true), null, null, ex.HttpRequestError.ToString());
        }
        catch (HttpRequestException ex)
        {
            return new GraphDispatchResult(
                FacebookOutboundRules.ClassifyDispatch(null, false, false), null, null, ex.HttpRequestError.ToString());
        }
        catch (OperationCanceledException)
        {
            return new GraphDispatchResult(
                FacebookOutboundRules.ClassifyDispatch(null, false, false), null, null, "timeout");
        }

        using (res)
        {
            var status = (int)res.StatusCode;
            string? messageId = null;
            try
            {
                var body = await res.Content.ReadAsStringAsync(timeout.Token);
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("message_id", out var id)
                    && id.ValueKind == JsonValueKind.String)
                    messageId = id.GetString();
            }
            catch (Exception)
            {
                messageId = null;
            }

            var outcome = FacebookOutboundRules.ClassifyDispatch(status, !string.IsNullOrWhiteSpace(messageId), false);
            return new GraphDispatchResult(outcome, status, messageId, outcome == "sent" ? null : $"http_{status}");
        }
    }
}
