using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesAgentService : IKitSalesAgentService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly KitSalesFacebookSettingsService _facebook;
    private readonly KitSalesAssessmentLookup _assessments;

    public KitSalesAgentService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        KitSalesFacebookSettingsService facebook,
        KitSalesAssessmentLookup assessments)
    {
        _db = db;
        _tenant = tenant;
        _facebook = facebook;
        _assessments = assessments;
    }

    private Guid TenantId => _tenant.TenantId;
    private Guid? ActorId => _tenant.UserId == Guid.Empty ? null : _tenant.UserId;

    public async Task<KitSalesQualificationDto> QualifyAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, leadId, cancellationToken);
        var lead = await LoadLeadAsync(conn, tx, leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead không tồn tại.");

        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.agent_task
                SET status = 'dismissed',
                    completed_at = NOW(),
                    payload_json = payload_json || '{"reason":"superseded"}'::jsonb
                WHERE tenant_id = @TenantId AND lead_id = @LeadId AND status = 'pending'
                """,
                new { TenantId, LeadId = leadId },
                tx,
                cancellationToken: cancellationToken));

        if (!string.Equals(lead.ProductStatus, "active", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(lead.ProfileStatus, "active", StringComparison.OrdinalIgnoreCase))
        {
            var blocked = await InsertRunAsync(
                conn, tx, leadId, "completed",
                new { lead.ProductCode, lead.LeadStatus },
                new { decision = "blocked_catalog", productCode = lead.ProductCode, human = new { accepted = (bool?)null } },
                cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new KitSalesQualificationDto(
                blocked, null, lead.ProductCode, "blocked_catalog", false,
                null, null, null, null, null, NovixaQualificationScore.Formula,
                null, null, [], ["catalog"], "none");
        }

        var facts = await LoadFactsAsync(conn, tx, lead, cancellationToken);
        var score = NovixaQualificationScore.Evaluate(facts);
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        var pain = string.IsNullOrWhiteSpace(lead.PainCode)
            ? NovixaPainCatalog.All[0].Code
            : NovixaPainCatalog.Canonical(lead.PainCode);
        var step = score.Decision == "pursue_phc" ? "phc_ask" : "hook";
        var draft = score.Decision is "pursue_phc" or "nurture"
            ? NovixaSalesJourney.Draft(step, pain, options.PhcUrl)
            : null;
        var citations = new List<string> { "novixa-sales-v1" };
        if (!string.IsNullOrWhiteSpace(lead.LastInbound))
        {
            var match = NovixaSalesKnowledge.Resolve(lead.LastInbound, options.PhcUrl);
            citations.AddRange(match.Citations);
        }

        var next = draft is null ? null : NovixaSalesJourney.Encode(step, pain);
        var output = new
        {
            decision = score.Decision,
            productCode = lead.ProductCode,
            profileVersion = "novixa-sales-v1+weights-default",
            score = new
            {
                scored = score.Scored,
                fit = score.Fit,
                pain = score.Pain,
                intent = score.Intent,
                engagement = score.Engagement,
                total = score.Total,
                formula = score.Formula,
                temperature = score.Temperature,
                factors = score.Factors,
                missing = score.Missing,
            },
            nextActionCode = next,
            draft,
            citations,
            human = new { accepted = (bool?)null, acceptedAt = (DateTimeOffset?)null },
        };
        var runId = await InsertRunAsync(
            conn, tx, leadId, "completed",
            new { lead.ProductCode, lead.LeadStatus, profileVersion = "novixa-sales-v1+weights-default" },
            output,
            cancellationToken);
        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, leadId, "qualification_scored", null, null,
            JsonSerializer.Serialize(new { decision = score.Decision, total = score.Total, runId }, Json),
            ActorId, runId, $"qualification_scored:{runId:N}", cancellationToken);

        Guid? taskId = null;
        var taskStatus = "none";
        if (score.Decision is "pursue_phc" or "nurture" or "disqualify")
        {
            taskId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.agent_task (
                        tenant_id, agent_run_id, lead_id, task_type, status, payload_json)
                    VALUES (
                        @TenantId, @RunId, @LeadId, 'accept_score', 'pending', @Payload::jsonb)
                    RETURNING id
                    """,
                    new
                    {
                        TenantId,
                        RunId = runId,
                        LeadId = leadId,
                        Payload = JsonSerializer.Serialize(output, Json),
                    },
                    tx,
                    cancellationToken: cancellationToken));
            taskStatus = "pending";
        }

        await tx.CommitAsync(cancellationToken);
        return new KitSalesQualificationDto(
            runId, taskId, lead.ProductCode, score.Decision, score.Scored,
            score.Fit, score.Pain, score.Intent, score.Engagement, score.Total,
            score.Formula, next, draft, citations, score.Missing, taskStatus);
    }

    public async Task<KitSalesAgentTaskDto> AcceptAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var task = await LockTaskAsync(conn, tx, taskId, cancellationToken)
            ?? throw new InvalidOperationException("Agent task không tồn tại.");
        if (task.Status is "accepted" or "dismissed")
        {
            await tx.CommitAsync(cancellationToken);
            return new KitSalesAgentTaskDto(taskId, task.Status, ReadDecision(task.Payload));
        }

        if (!SalesReviewRules.OldEndpointAllows(task.TaskType) || task.LeadId is null)
            throw new InvalidOperationException("Endpoint này chỉ nhận accept_score của một lead.");
        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, task.LeadId.Value, cancellationToken);
        var score = JsonSerializer.Deserialize<ProposalBody>(task.Payload, Json)
            ?? throw new InvalidOperationException("Proposal không đọc được.");
        if (score.Decision is not ("pursue_phc" or "nurture" or "disqualify"))
            throw new InvalidOperationException("Proposal này không nhận được.");

        if (score.Score?.Scored == true)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_sales.lead
                    SET fit_score = @Fit,
                        pain_score = @Pain,
                        intent_score = @Intent,
                        engagement_score = @Engagement,
                        total_score = @Total,
                        lead_temperature = COALESCE(@Temperature, lead_temperature),
                        next_action_code = COALESCE(@Next, next_action_code),
                        next_action_at = COALESCE(@When, next_action_at),
                        updated_at = NOW()
                    WHERE id = @LeadId AND tenant_id = @TenantId
                    """,
                    new
                    {
                        Fit = score.Score.Fit ?? 0,
                        Pain = score.Score.Pain ?? 0,
                        Intent = score.Score.Intent ?? 0,
                        Engagement = score.Score.Engagement ?? 0,
                        Total = score.Score.Total ?? 0,
                        Temperature = score.Score.Temperature,
                        Next = score.NextActionCode,
                        When = score.NextActionCode is null
                            ? (DateTime?)null
                            : NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow).UtcDateTime,
                        task.LeadId,
                        TenantId,
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }

        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, task.LeadId.Value, "qualification_decided", null, null,
            JsonSerializer.Serialize(new { decision = score.Decision, taskId }, Json),
            ActorId, task.AgentRunId, $"qualification_decided:{taskId:N}", cancellationToken);
        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, task.LeadId.Value, "proposal_accepted", null, null,
            JsonSerializer.Serialize(new { taskId }, Json),
            ActorId, task.AgentRunId, $"proposal_accepted:{taskId:N}", cancellationToken);

        if (!string.IsNullOrWhiteSpace(score.Draft))
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.interaction (
                        tenant_id, lead_id, channel, direction, interaction_type, content, outcome,
                        review_status, ai_generated, classification)
                    VALUES (
                        @TenantId, @LeadId, 'facebook', 'outbound', 'message', @Content, 'qualification_draft',
                        'draft', TRUE, 'qualification')
                    """,
                    new { TenantId, task.LeadId, Content = score.Draft },
                    tx,
                    cancellationToken: cancellationToken));
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, TenantId, task.LeadId.Value, "outbound_drafted", null, null,
                "{}", ActorId, task.AgentRunId, $"outbound_drafted:{taskId:N}", cancellationToken);
        }

        var action = score.Decision == "pursue_phc" ? "phc" : "follow_up";
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.task (
                    tenant_id, lead_id, action_code, title, due_at, created_by_user_id)
                VALUES (
                    @TenantId, @LeadId, @Action, @Title, @Due, @Actor)
                """,
                new
                {
                    TenantId,
                    task.LeadId,
                    Action = action,
                    Title = score.Decision == "disqualify"
                        ? "Xác nhận lý do loại lead"
                        : "Việc tiếp theo từ Qualification Copilot",
                    Due = DateTime.UtcNow.AddDays(1),
                    Actor = ActorId,
                },
                tx,
                cancellationToken: cancellationToken));

        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.agent_task
                SET status = 'accepted', completed_at = NOW()
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = taskId, TenantId },
                tx,
                cancellationToken: cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return new KitSalesAgentTaskDto(taskId, "accepted", score.Decision);
    }

    public async Task<KitSalesAgentTaskDto> DismissAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var task = await LockTaskAsync(conn, tx, taskId, cancellationToken)
            ?? throw new InvalidOperationException("Agent task không tồn tại.");
        if (task.Status == "accepted")
            throw new InvalidOperationException("Proposal đã được nhận.");
        if (!SalesReviewRules.OldEndpointAllows(task.TaskType) || task.LeadId is null)
            throw new InvalidOperationException("Endpoint này chỉ nhận accept_score của một lead.");
        if (task.Status != "dismissed")
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_sales.agent_task
                    SET status = 'dismissed', completed_at = NOW()
                    WHERE id = @Id AND tenant_id = @TenantId AND status = 'pending'
                    """,
                    new { Id = taskId, TenantId },
                    tx,
                    cancellationToken: cancellationToken));
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, TenantId, task.LeadId.Value, "proposal_dismissed", null, null,
                JsonSerializer.Serialize(new { taskId }, Json),
                ActorId, task.AgentRunId, $"proposal_dismissed:{taskId:N}", cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return new KitSalesAgentTaskDto(taskId, "dismissed", ReadDecision(task.Payload));
    }

    public async Task ConfirmPhcReportAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        PhcLinkLock.EnsureOpen();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await KitSalesLeadGuard.EnsureWorkableAsync(conn, tx, TenantId, leadId, cancellationToken);
        var submissionId = await conn.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                """
                SELECT assessment_submission_id
                FROM pack_sales.lead
                WHERE id = @LeadId AND tenant_id = @TenantId
                """,
                new { LeadId = leadId, TenantId },
                tx,
                cancellationToken: cancellationToken));
        if (submissionId is null)
            throw new InvalidOperationException("Lead chưa gắn bài PHC.");
        var row = await _assessments.FindAsync(submissionId.Value, cancellationToken);
        if (row is null || !KitSalesAssessmentLookup.ReportReady(row))
            throw new InvalidOperationException("Báo cáo PHC chưa sẵn sàng.");
        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, TenantId, leadId, "phc_completed", null, null,
            JsonSerializer.Serialize(new { submissionId }, Json),
            ActorId, null, $"phc_completed:{leadId:N}:{submissionId:N}", cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task<KitSalesOwnerKpiDto> OwnerKpiAsync(
        DateTimeOffset? periodStart,
        DateTimeOffset? asOf,
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var end = asOf ?? DateTimeOffset.UtcNow;
        var start = periodStart ?? end.AddDays(-30);
        var product = string.IsNullOrWhiteSpace(productCode) ? "novixa" : productCode.Trim().ToLowerInvariant();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var profileJson = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT qualification_json::text
                FROM pack_sales.product_profile
                WHERE product_code = @Product AND status = 'active'
                ORDER BY effective_from DESC
                LIMIT 1
                """,
                new { Product = product },
                cancellationToken: cancellationToken));
        var maturity = ReadInt(profileJson, "qualificationMaturityDays", SalesCohort.NovixaQualificationMaturityDays);
        var leads = (await conn.QueryAsync<LeadStamp>(
            new CommandDefinition(
                """
                SELECT id AS Id, created_at AS CreatedAt, lead_status AS LeadStatus
                FROM pack_sales.lead
                WHERE tenant_id = @TenantId AND product_code = @Product AND lead_status <> @Archived
                """,
                new { TenantId, Product = product, Archived = LeadRetention.Archived },
                cancellationToken: cancellationToken))).ToList();
        var allEvents = (await conn.QueryAsync<EventStamp>(
            new CommandDefinition(
                """
                SELECT e.lead_id AS LeadId, e.event_type AS EventType, e.to_status AS ToStatus,
                       e.occurred_at AS OccurredAt, e.payload::text AS Payload,
                       l.lead_status AS LeadStatus
                FROM pack_sales.lead_event e
                INNER JOIN pack_sales.lead l ON l.id = e.lead_id
                WHERE e.tenant_id = @TenantId AND l.product_code = @Product
                """,
                new { TenantId, Product = product },
                cancellationToken: cancellationToken))).ToList();
        var events = allEvents.Where(e => LeadRetention.CountsInKpi(e.LeadStatus)).ToList();

        var eligible = leads.Where(l => SalesCohort.InEligibleWindow(AsOffset(l.CreatedAt), start, end, maturity)).Select(l => l.Id).ToHashSet();
        var immature = leads.Count(l => l.CreatedAt > end.AddDays(-maturity).UtcDateTime && l.CreatedAt >= start.UtcDateTime);
        bool Has(Guid id, string type) => events.Any(e => e.LeadId == id && e.EventType == type && AsOffset(e.OccurredAt) <= end);
        var decided = eligible.Count(id => Has(id, "qualification_decided"));
        var lost = eligible.Count(id => Has(id, "lost") || events.Any(e => e.LeadId == id && e.EventType == "stage_entered" && e.ToStatus == "lost"));
        var closed = eligible.Count(id => Has(id, "qualification_decided") || Has(id, "lost") || events.Any(e => e.LeadId == id && e.ToStatus == "lost"));
        var pursue = eligible.Count(id => events.Any(e =>
            e.LeadId == id && e.EventType == "qualification_decided" && (e.Payload ?? "").Contains("pursue_phc", StringComparison.Ordinal)));
        var accepted = events.Count(e => e.EventType == "proposal_accepted" && eligible.Contains(e.LeadId) && AsOffset(e.OccurredAt) <= end);
        var dismissed = events.Count(e => e.EventType == "proposal_dismissed" && eligible.Contains(e.LeadId) && AsOffset(e.OccurredAt) <= end);
        var acceptanceDenom = accepted + dismissed;

        var rates = new List<KitSalesStageRateDto>();
        foreach (var pair in new[] { ("research", "phc"), ("phc", "demo"), ("demo", "pilot"), ("pilot", "paid") })
        {
            var key = $"{pair.Item1}>{pair.Item2}";
            var days = SalesCohort.NovixaTransitionMaturityDays[key];
            days = ReadTransition(profileJson, key, days);
            var entryEnd = end.AddDays(-days);
            var cohort = FirstEntries(events, pair.Item1, start, entryEnd);
            var reached = cohort.Count(id => events.Any(e =>
                e.LeadId == id && e.EventType == "stage_entered" && e.ToStatus == pair.Item2
                && AsOffset(e.OccurredAt) > FirstAt(events, id, pair.Item1)
                && AsOffset(e.OccurredAt) <= end));
            var rate = SalesCohort.Rate(cohort.Count, reached, entryEnd, end, days);
            var label = cohort.Count == 0 ? "empty" : rate is null ? "cohort_immature" : "ready";
            var flowCohort = FirstEntries(events, pair.Item1, start, end);
            var flowReached = flowCohort.Count(id => events.Any(e =>
                e.LeadId == id && e.EventType == "stage_entered" && e.ToStatus == pair.Item2
                && AsOffset(e.OccurredAt) > FirstAt(events, id, pair.Item1)
                && AsOffset(e.OccurredAt) >= start && AsOffset(e.OccurredAt) <= end));
            rates.Add(new KitSalesStageRateDto(
                pair.Item1, pair.Item2, cohort.Count, reached, rate, label,
                flowCohort.Count, flowReached, SalesCohort.Flow(flowCohort.Count, flowReached), "flow_in_period"));
        }

        var (dwell, openDwell) = AverageDwell(events, eligible, end);
        var humanPaid = events.Count(e => e.EventType == "stage_entered" && e.ToStatus == "paid" && eligible.Contains(e.LeadId));
        var revenue = events.Count(e => e.EventType == "revenue_recognized" && eligible.Contains(e.LeadId));
        var unapproved = allEvents.Count(e => e.EventType == "outbound_sent" && !allEvents.Any(a =>
            a.EventType == "outbound_approved" && a.LeadId == e.LeadId && a.OccurredAt <= e.OccurredAt));
        return new KitSalesOwnerKpiDto(
            end, start, eligible.Count, immature,
            eligible.Count == 0 ? null : Math.Round((decimal)decided / eligible.Count, 4),
            eligible.Count == 0 ? null : Math.Round((decimal)closed / eligible.Count, 4),
            lost,
            eligible.Count == 0 ? null : Math.Round((decimal)pursue / eligible.Count, 4),
            acceptanceDenom == 0 ? null : Math.Round((decimal)accepted / acceptanceDenom, 4),
            humanPaid, revenue,
            revenue == 0 ? "chua_co_nguon_doi_soat" : "revenue_recognized",
            unapproved,
            allEvents.Count(e => e.EventType == "outbound_blocked"),
            allEvents.Count(e => e.EventType == "stage_waiver"),
            rates, dwell, openDwell, 0,
            InPeriod("outbound_sent"),
            InPeriod("outbound_unknown"),
            InPeriod("outbound_failed"));

        int InPeriod(string type) => allEvents.Count(e =>
            e.EventType == type && AsOffset(e.OccurredAt) >= start && AsOffset(e.OccurredAt) <= end);
    }

    public async Task<KitSalesDeskKpiDto> DeskKpiAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var due = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM pack_sales.task t
                JOIN pack_sales.lead l ON l.id = t.lead_id AND l.tenant_id = t.tenant_id
                WHERE t.tenant_id = @TenantId AND t.status = 'open' AND l.lead_status <> @Archived
                  AND t.due_at IS NOT NULL AND t.due_at < (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date + 1
                  AND (@UserId IS NULL OR l.owner_user_id = @UserId OR l.owner_user_id IS NULL)
                """,
                new { TenantId, UserId = ActorId, Archived = LeadRetention.Archived },
                cancellationToken: cancellationToken));
        var drafts = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM pack_sales.interaction i
                JOIN pack_sales.lead l ON l.id = i.lead_id AND l.tenant_id = i.tenant_id
                WHERE i.tenant_id = @TenantId AND i.review_status = 'draft' AND i.direction = 'outbound'
                  AND l.lead_status <> @Archived
                  AND (@UserId IS NULL OR l.owner_user_id = @UserId OR l.owner_user_id IS NULL)
                """,
                new { TenantId, UserId = ActorId, Archived = LeadRetention.Archived },
                cancellationToken: cancellationToken));
        var undecided = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM pack_sales.lead l
                WHERE l.tenant_id = @TenantId AND l.lead_status NOT IN ('lost', 'archived')
                  AND (@UserId IS NULL OR l.owner_user_id = @UserId OR l.owner_user_id IS NULL)
                  AND NOT EXISTS (
                      SELECT 1 FROM pack_sales.lead_event e
                      WHERE e.lead_id = l.id AND e.event_type = 'qualification_decided')
                """,
                new { TenantId, UserId = ActorId },
                cancellationToken: cancellationToken));
        var phc = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM pack_sales.lead l
                WHERE l.tenant_id = @TenantId AND l.lead_status <> @Archived
                  AND (@UserId IS NULL OR l.owner_user_id = @UserId OR l.owner_user_id IS NULL)
                  AND l.assessment_submission_id IS NULL
                  AND EXISTS (
                      SELECT 1 FROM pack_sales.lead_event e
                      WHERE e.lead_id = l.id AND e.event_type = 'qualification_decided'
                        AND e.payload->>'decision' = 'pursue_phc')
                """,
                new { TenantId, UserId = ActorId, Archived = LeadRetention.Archived },
                cancellationToken: cancellationToken));
        var unknownOpen = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int FROM pack_sales.lead_event u
                JOIN pack_sales.interaction i
                  ON i.tenant_id = u.tenant_id AND i.id::text = u.payload->>'interactionId'
                WHERE u.tenant_id = @TenantId AND u.event_type = 'outbound_unknown'
                  AND i.sent_at IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM pack_sales.lead_event r
                      WHERE r.tenant_id = u.tenant_id AND r.lead_id = u.lead_id
                        AND r.event_type = @Resolved
                        AND r.payload->>'approvalEventId' = u.payload->>'approvalEventId')
                """,
                new { TenantId, Resolved = FacebookOutboundRules.UnknownResolvedEvent },
                cancellationToken: cancellationToken));
        return new KitSalesDeskKpiDto(
            due, drafts, undecided, phc, NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow), unknownOpen);
    }

    private async Task<Guid> InsertRunAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid leadId,
        string status,
        object input,
        object output,
        CancellationToken cancellationToken) =>
        await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.agent_run (
                    tenant_id, agent_code, lead_id, status, input_json, output_json, policy_json, created_by_user_id)
                VALUES (
                    @TenantId, 'qualification', @LeadId, @Status,
                    @Input::jsonb, @Output::jsonb, @Policy::jsonb, @Actor)
                RETURNING id
                """,
                new
                {
                    TenantId,
                    LeadId = leadId,
                    Status = status,
                    Input = JsonSerializer.Serialize(input, Json),
                    Output = JsonSerializer.Serialize(output, Json),
                    Policy = """{"autonomy":2,"outbound":"draft_only","model":"none","roles":["qualification"]}""",
                    Actor = ActorId,
                },
                tx,
                cancellationToken: cancellationToken));

    private async Task<TaskRow?> LockTaskAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid taskId,
        CancellationToken cancellationToken) =>
        await conn.QuerySingleOrDefaultAsync<TaskRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, agent_run_id AS AgentRunId, lead_id AS LeadId,
                       task_type AS TaskType, status AS Status, payload_json::text AS Payload
                FROM pack_sales.agent_task
                WHERE id = @Id AND tenant_id = @TenantId
                FOR UPDATE
                """,
                new { Id = taskId, TenantId },
                tx,
                cancellationToken: cancellationToken));

    private async Task<LeadContext?> LoadLeadAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid leadId,
        CancellationToken cancellationToken) =>
        await conn.QuerySingleOrDefaultAsync<LeadContext>(
            new CommandDefinition(
                $"""
                SELECT l.id AS Id, l.product_code AS ProductCode, l.lead_status AS LeadStatus,
                       b.business_type AS BusinessType, b.province AS Province, b.phone AS Phone,
                       b.email AS Email, b.website AS Website,
                       p.status AS ProductStatus, pr.status AS ProfileStatus,
                       pr.qualification_json::text AS QualificationJson,
                       EXISTS (
                           SELECT 1 FROM pack_sales.business_identity i
                           WHERE i.business_id = b.id AND i.tenant_id = l.tenant_id
                             AND {SalesContactability.BusinessSocialIdentitySql}) AS HasSocialChannel,
                       EXISTS (
                           SELECT 1 FROM pack_sales.contact c
                           WHERE c.business_id = b.id AND c.is_decision_maker) AS DecisionMaker,
                       (
                           SELECT lp.pain_code FROM pack_sales.lead_pain lp
                           WHERE lp.lead_id = l.id AND lp.tenant_id = l.tenant_id
                           ORDER BY lp.confidence DESC, lp.updated_at DESC
                           LIMIT 1) AS PainCode
                FROM pack_sales.lead l
                INNER JOIN pack_sales.business b ON b.id = l.business_id
                INNER JOIN pack_sales.product p ON p.code = l.product_code
                LEFT JOIN pack_sales.product_profile pr
                    ON pr.product_code = l.product_code AND pr.status = 'active'
                WHERE l.id = @LeadId AND l.tenant_id = @TenantId
                """,
                new { LeadId = leadId, TenantId },
                tx,
                cancellationToken: cancellationToken));

    private async Task<QualificationFacts> LoadFactsAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        LeadContext lead,
        CancellationToken cancellationToken)
    {
        var pains = (await conn.QueryAsync<PainRow>(
            new CommandDefinition(
                """
                SELECT response_code AS ResponseCode, confidence AS Confidence, last_response_at AS RespondedAt
                FROM pack_sales.lead_pain
                WHERE tenant_id = @TenantId AND lead_id = @LeadId
                  AND response_code IS NOT NULL
                """,
                new { TenantId, LeadId = lead.Id },
                tx,
                cancellationToken: cancellationToken))).ToList();
        var interactions = (await conn.QueryAsync<TouchRow>(
            new CommandDefinition(
                """
                SELECT direction AS Direction, occurred_at AS OccurredAt, content AS Content
                FROM pack_sales.interaction
                WHERE tenant_id = @TenantId AND lead_id = @LeadId
                ORDER BY occurred_at
                """,
                new { TenantId, LeadId = lead.Id },
                tx,
                cancellationToken: cancellationToken))).ToList();
        var inboundAt = interactions.Where(i => i.Direction == "inbound").Select(i => i.OccurredAt).Cast<DateTime?>().LastOrDefault();
        var outboundAt = interactions.Where(i => i.Direction == "outbound").Select(i => i.OccurredAt).Cast<DateTime?>().FirstOrDefault();
        lead.LastInbound = interactions.LastOrDefault(i => i.Direction == "inbound")?.Content;
        var recentPain = pains.Any(p => p.RespondedAt is { } at && at > DateTime.UtcNow.AddDays(-14));
        return new QualificationFacts(
            lead.BusinessType,
            !string.IsNullOrWhiteSpace(lead.Province),
            SalesContactability.IsReachable(lead.Phone, lead.Email, lead.Website, lead.HasSocialChannel),
            lead.DecisionMaker,
            true,
            pains.Select(p => new QualificationPainSignal(
                p.ResponseCode ?? "",
                p.Confidence,
                p.RespondedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(p.RespondedAt.Value, DateTimeKind.Utc)))).ToList(),
            interactions.Count > 0,
            interactions.Count > 0 && interactions.All(i => i.Direction == "outbound"),
            interactions.Any(i => i.Direction == "inbound"),
            inboundAt is not null && outboundAt is not null && inboundAt > outboundAt,
            recentPain);
    }

    private static string? ReadDecision(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("decision", out var value) ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ReadInt(string? json, string name, int fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(name, out var value) && value.TryGetInt32(out var number))
                return number;
        }
        catch (JsonException)
        {
            return fallback;
        }

        return fallback;
    }

    private static int ReadTransition(string? json, string key, int fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("transitionMaturityDays", out var map)
                && map.TryGetProperty(key, out var value)
                && value.TryGetInt32(out var number))
                return number;
        }
        catch (JsonException)
        {
            return fallback;
        }

        return fallback;
    }

    private static List<Guid> FirstEntries(List<EventStamp> events, string status, DateTimeOffset start, DateTimeOffset end) =>
        events.Where(e => e.EventType == "stage_entered" && e.ToStatus == status)
            .GroupBy(e => e.LeadId)
            .Select(g => g.OrderBy(e => e.OccurredAt).First())
            .Where(e => AsOffset(e.OccurredAt) >= start && AsOffset(e.OccurredAt) < end)
            .Select(e => e.LeadId)
            .ToList();

    private static DateTimeOffset FirstAt(List<EventStamp> events, Guid leadId, string status)
    {
        var at = events.Where(e => e.LeadId == leadId && e.EventType == "stage_entered" && e.ToStatus == status)
            .Min(e => e.OccurredAt);
        return AsOffset(at);
    }

    private static (List<KitSalesDwellStatDto> Closed, int OpenLeads) AverageDwell(List<EventStamp> events, HashSet<Guid> eligible, DateTimeOffset asOf)
    {
        var totals = new Dictionary<string, (double Hours, int Leads)>(StringComparer.Ordinal);
        var openLeads = 0;
        foreach (var leadId in eligible)
        {
            var visits = events.Where(e => e.LeadId == leadId && e.EventType == "stage_entered" && e.ToStatus is not null)
                .OrderBy(e => e.OccurredAt)
                .Select(e => new StageVisitEvent(e.ToStatus!, AsOffset(e.OccurredAt)))
                .ToList();
            if (visits.Count == 0)
                continue;
            var (closed, open, _) = SalesDwell.Compute(visits, asOf);
            if (open > TimeSpan.Zero)
                openLeads++;
            foreach (var (status, span) in closed)
            {
                var current = totals.GetValueOrDefault(status);
                totals[status] = (current.Hours + span.TotalHours, current.Leads + 1);
            }
        }

        var closedStats = totals.Select(kv => new KitSalesDwellStatDto(
            kv.Key,
            kv.Value.Leads == 0 ? 0 : Math.Round(kv.Value.Hours / kv.Value.Leads, 2),
            kv.Value.Leads)).ToList();
        return (closedStats, openLeads);
    }

    private static DateTimeOffset AsOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    internal static string[] TemplateCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("phcTemplateCodes", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [];
            return arr.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>().ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class ProposalBody
    {
        public string? Decision { get; set; }
        public string? NextActionCode { get; set; }
        public string? Draft { get; set; }
        public ScoreBody? Score { get; set; }
    }

    private sealed class ScoreBody
    {
        public bool Scored { get; set; }
        public decimal? Fit { get; set; }
        public decimal? Pain { get; set; }
        public decimal? Intent { get; set; }
        public decimal? Engagement { get; set; }
        public decimal? Total { get; set; }
        public string? Temperature { get; set; }
    }

    private sealed class TaskRow
    {
        public Guid Id { get; init; }
        public Guid? AgentRunId { get; init; }
        public Guid? LeadId { get; init; }
        public string TaskType { get; init; } = "";
        public string Status { get; init; } = "";
        public string Payload { get; init; } = "{}";
    }

    private sealed class LeadContext
    {
        public Guid Id { get; init; }
        public string ProductCode { get; init; } = "";
        public string LeadStatus { get; init; } = "";
        public string? BusinessType { get; init; }
        public string? Province { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string? Website { get; init; }
        public string ProductStatus { get; init; } = "";
        public string? ProfileStatus { get; init; }
        public string? QualificationJson { get; init; }
        public bool HasSocialChannel { get; init; }
        public bool DecisionMaker { get; init; }
        public string? PainCode { get; init; }
        public string? LastInbound { get; set; }
    }

    private sealed class PainRow
    {
        public string? ResponseCode { get; init; }
        public decimal Confidence { get; init; }
        public DateTime? RespondedAt { get; init; }
    }

    private sealed class TouchRow
    {
        public string Direction { get; init; } = "";
        public DateTime OccurredAt { get; init; }
        public string? Content { get; init; }
    }

    private sealed class LeadStamp
    {
        public Guid Id { get; init; }
        public DateTime CreatedAt { get; init; }
        public string LeadStatus { get; init; } = "";
    }

    private sealed class EventStamp
    {
        public Guid LeadId { get; init; }
        public string EventType { get; init; } = "";
        public string? ToStatus { get; init; }
        public DateTime OccurredAt { get; init; }
        public string? Payload { get; init; }
        public string? LeadStatus { get; init; }
    }
}
