using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesAgentSweep : IKitSalesAgentSweep
{
    private readonly IDbConnectionFactory _db;

    public KitSalesAgentSweep(IDbConnectionFactory db) => _db = db;

    public async Task<KitSalesSweepDto> SweepAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var ready = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'pack_sales' AND table_name = 'lead_event')
                   AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'pack_sales'
                      AND table_name = 'product_profile'
                      AND column_name = 'qualification_json')
                """,
                cancellationToken: cancellationToken));
        if (!ready)
            return new KitSalesSweepDto(0, 0, false);

        var unknown = await FacebookOutboundReconciler.ReconcileAsync(conn, tenantId, cancellationToken);

        var leads = (await conn.QueryAsync<SweepLead>(
            new CommandDefinition(
                $"""
                SELECT l.id AS Id, l.product_code AS ProductCode, l.lead_status AS LeadStatus,
                       b.business_type AS BusinessType, b.province AS Province, b.phone AS Phone,
                       b.email AS Email, b.website AS Website,
                       p.status AS ProductStatus, pr.status AS ProfileStatus,
                       EXISTS (
                           SELECT 1 FROM pack_sales.business_identity i
                           WHERE i.business_id = b.id AND i.tenant_id = l.tenant_id
                             AND {SalesContactability.BusinessSocialIdentitySql}) AS HasSocialChannel,
                       EXISTS (
                           SELECT 1 FROM pack_sales.contact c
                           WHERE c.business_id = b.id AND c.is_decision_maker) AS DecisionMaker
                FROM pack_sales.lead l
                INNER JOIN pack_sales.business b ON b.id = l.business_id
                INNER JOIN pack_sales.product p ON p.code = l.product_code
                LEFT JOIN pack_sales.product_profile pr
                    ON pr.product_code = l.product_code AND pr.status = 'active'
                WHERE l.tenant_id = @TenantId AND l.lead_status <> @Archived
                ORDER BY l.updated_at DESC
                LIMIT 40
                """,
                new { TenantId = tenantId, Archived = LeadRetention.Archived },
                cancellationToken: cancellationToken))).ToList();

        var planned = 0;
        var skipped = 0;
        foreach (var lead in leads)
        {
            if (await PlanOneAsync(conn, tenantId, lead, cancellationToken))
                planned++;
            else
                skipped++;
        }

        return new KitSalesSweepDto(planned, skipped, true, unknown);
    }

    private static async Task<bool> PlanOneAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        SweepLead lead,
        CancellationToken cancellationToken)
    {
        var pending = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                """
                SELECT COALESCE(
                    (
                        SELECT payload_json->>'fingerprint'
                        FROM pack_sales.agent_task
                        WHERE tenant_id = @TenantId AND lead_id = @LeadId AND status = 'pending'
                        ORDER BY created_at DESC
                        LIMIT 1
                    ),
                    (
                        SELECT split_part(idempotency_key, ':', 3)
                        FROM pack_sales.lead_event
                        WHERE tenant_id = @TenantId AND lead_id = @LeadId
                          AND event_type = 'cycle_planned'
                        ORDER BY occurred_at DESC
                        LIMIT 1
                    ))
                """,
                new { TenantId = tenantId, LeadId = lead.Id },
                cancellationToken: cancellationToken));
        var pains = (await conn.QueryAsync<PainRow>(
            new CommandDefinition(
                """
                SELECT response_code AS ResponseCode, confidence AS Confidence, last_response_at AS RespondedAt
                FROM pack_sales.lead_pain
                WHERE tenant_id = @TenantId AND lead_id = @LeadId AND response_code IS NOT NULL
                """,
                new { TenantId = tenantId, LeadId = lead.Id },
                cancellationToken: cancellationToken))).ToList();
        var touches = (await conn.QueryAsync<TouchRow>(
            new CommandDefinition(
                """
                SELECT direction AS Direction, occurred_at AS OccurredAt, content AS Content
                FROM pack_sales.interaction
                WHERE tenant_id = @TenantId AND lead_id = @LeadId
                ORDER BY occurred_at
                """,
                new { TenantId = tenantId, LeadId = lead.Id },
                cancellationToken: cancellationToken))).ToList();
        var overdue = await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1 FROM pack_sales.task
                    WHERE tenant_id = @TenantId AND lead_id = @LeadId AND status = 'open'
                      AND due_at IS NOT NULL AND due_at < NOW())
                """,
                new { TenantId = tenantId, LeadId = lead.Id },
                cancellationToken: cancellationToken));
        var inboundAt = touches.Where(i => i.Direction == "inbound").Select(i => (DateTime?)i.OccurredAt).LastOrDefault();
        var outboundAt = touches.Where(i => i.Direction == "outbound").Select(i => (DateTime?)i.OccurredAt).FirstOrDefault();
        var observation = new SalesLeadObservation(
            lead.Id,
            lead.ProductCode,
            lead.ProductStatus,
            lead.ProfileStatus,
            lead.LeadStatus,
            lead.BusinessType,
            !string.IsNullOrWhiteSpace(lead.Province),
            SalesContactability.IsReachable(lead.Phone, lead.Email, lead.Website, lead.HasSocialChannel),
            lead.DecisionMaker,
            true,
            pains.Select(p => new QualificationPainSignal(
                p.ResponseCode ?? "",
                p.Confidence,
                p.RespondedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(p.RespondedAt.Value, DateTimeKind.Utc)))).ToList(),
            touches.Count > 0,
            touches.Count > 0 && touches.All(i => i.Direction == "outbound"),
            touches.Any(i => i.Direction == "inbound"),
            inboundAt is not null && outboundAt is not null && inboundAt > outboundAt,
            pains.Any(p => p.RespondedAt is { } at && at > DateTime.UtcNow.AddDays(-14)),
            touches.LastOrDefault(i => i.Direction == "inbound")?.Content,
            overdue,
            pending is not null,
            pending);
        var plan = SalesAgentCycle.Plan(observation);
        var actions = SalesAgentCycle.Executable(plan);
        if (!plan.Changed || actions.Count == 0)
            return false;

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.agent_task
                SET status = 'dismissed', completed_at = NOW(),
                    payload_json = payload_json || '{"reason":"superseded"}'::jsonb
                WHERE tenant_id = @TenantId AND lead_id = @LeadId AND status = 'pending'
                """,
                new { TenantId = tenantId, LeadId = lead.Id },
                tx,
                cancellationToken: cancellationToken));
        var runId = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.agent_run (
                    tenant_id, agent_code, lead_id, status, input_json, output_json, policy_json)
                VALUES (
                    @TenantId, 'qualification', @LeadId, 'completed',
                    @Input::jsonb, @Output::jsonb, @Policy::jsonb)
                RETURNING id
                """,
                new
                {
                    TenantId = tenantId,
                    LeadId = lead.Id,
                    Input = "{\"source\":\"sweep\"}",
                    Output = "{\"decision\":\"" + plan.Decision + "\",\"fingerprint\":\"" + plan.Fingerprint + "\"}",
                    Policy = """{"autonomy":2,"outbound":"draft_only","model":"none","roles":["qualification"]}""",
                },
                tx,
                cancellationToken: cancellationToken));

        foreach (var action in actions)
        {
            if (action.Code == "create_draft")
            {
                var draft = NovixaSalesJourney.Draft("hook", NovixaPainCatalog.All[0].Code, "");
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO pack_sales.interaction (
                            tenant_id, lead_id, channel, direction, interaction_type, content, outcome,
                            review_status, ai_generated, classification)
                        VALUES (
                            @TenantId, @LeadId, 'facebook', 'outbound', 'message', @Content, 'agent_draft',
                            'draft', TRUE, 'qualification')
                        """,
                        new { TenantId = tenantId, LeadId = lead.Id, Content = draft },
                        tx,
                        cancellationToken: cancellationToken));
            }
            else if (action.Code is "collect_missing" or "flag_overdue")
            {
                var exists = await conn.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        """
                        SELECT EXISTS (
                            SELECT 1 FROM pack_sales.task
                            WHERE tenant_id = @TenantId AND lead_id = @LeadId
                              AND action_code = @Code AND status = 'open')
                        """,
                        new { TenantId = tenantId, LeadId = lead.Id, Code = action.Code },
                        tx,
                        cancellationToken: cancellationToken));
                if (!exists)
                {
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            """
                            INSERT INTO pack_sales.task (tenant_id, lead_id, action_code, title, due_at)
                            VALUES (@TenantId, @LeadId, @Code, @Title, @Due)
                            """,
                            new
                            {
                                TenantId = tenantId,
                                LeadId = lead.Id,
                                Code = action.Code,
                                Title = action.Code == "collect_missing" ? "Bổ sung thông tin lead" : "Lead không tiến triển",
                                Due = DateTime.UtcNow.AddDays(1),
                            },
                            tx,
                            cancellationToken: cancellationToken));
                }
            }
            else if (action.Code == "accept_score")
            {
                var score = plan.Score;
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    decision = plan.Decision,
                    fingerprint = plan.Fingerprint,
                    score = score is null ? null : new
                    {
                        scored = score.Scored,
                        fit = score.Fit,
                        pain = score.Pain,
                        intent = score.Intent,
                        engagement = score.Engagement,
                        total = score.Total,
                        temperature = score.Temperature,
                    },
                });
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO pack_sales.agent_task (
                            tenant_id, agent_run_id, lead_id, task_type, status, payload_json)
                        VALUES (
                            @TenantId, @RunId, @LeadId, 'accept_score', 'pending', @Payload::jsonb)
                        """,
                        new
                        {
                            TenantId = tenantId,
                            RunId = runId,
                            LeadId = lead.Id,
                            Payload = payload,
                        },
                        tx,
                        cancellationToken: cancellationToken));
            }
        }

        await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, tenantId, lead.Id, "cycle_planned", null, null,
            "{\"decision\":\"" + plan.Decision + "\"}",
            null, runId, $"cycle:{lead.Id:N}:{plan.Fingerprint}", cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    private sealed class SweepLead
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
        public bool HasSocialChannel { get; init; }
        public bool DecisionMaker { get; init; }
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
}
