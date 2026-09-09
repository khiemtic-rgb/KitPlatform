using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesChatService : IKitSalesChatService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IKitSalesDeskService _desk;
    private readonly IKitSalesPainService _pains;
    private readonly KitSalesFacebookMessengerClient _messenger;
    private readonly KitSalesGeminiVoice _voice;
    private readonly KitSalesFacebookSettingsService _facebook;

    public KitSalesChatService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IKitSalesDeskService desk,
        IKitSalesPainService pains,
        KitSalesFacebookMessengerClient messenger,
        KitSalesGeminiVoice voice,
        KitSalesFacebookSettingsService facebook)
    {
        _db = db;
        _tenant = tenant;
        _desk = desk;
        _pains = pains;
        _messenger = messenger;
        _voice = voice;
        _facebook = facebook;
    }

    public async Task<KitSalesChatChannelDto> GetChannelAsync(CancellationToken cancellationToken = default)
    {
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        return new(
            options.Enabled,
            await _messenger.IsSendReadyAsync(cancellationToken),
            options.MeLink,
            options.PhcUrl,
            options.PageUrl);
    }

    public async Task<KitSalesChatPreviewDto> PreviewAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        var match = NovixaSalesKnowledge.Resolve(text, options.PhcUrl);
        return new KitSalesChatPreviewDto(
            match.Intent,
            match.Reply,
            match.Escalate,
            match.Citations,
            await _messenger.IsSendReadyAsync(cancellationToken),
            options.MeLink,
            options.PhcUrl);
    }

    public async Task<KitSalesChatPreviewDto> ReplyAsync(
        KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(request.Text ?? "", cancellationToken);
        if (!request.Log || request.LeadId is null)
            return preview;

        await _desk.CreateInteractionAsync(
            request.LeadId.Value,
            new CreateKitSalesInteractionRequest(
                "facebook",
                "inbound",
                "message",
                request.Text,
                preview.Intent,
                ReviewStatus: "sent",
                Classification: preview.Intent),
            cancellationToken);

        await _desk.CreateInteractionAsync(
            request.LeadId.Value,
            new CreateKitSalesInteractionRequest(
                "facebook",
                "outbound",
                "message",
                preview.Reply,
                preview.Escalate ? "escalate" : preview.Intent,
                ReviewStatus: "sent",
                AiGenerated: true,
                Classification: preview.Intent),
            cancellationToken);

        if (preview.Escalate)
        {
            await _desk.CreateTaskAsync(
                request.LeadId.Value,
                new CreateKitSalesTaskRequest("follow_up", "Trả lời Facebook — ngoài SoT"),
                cancellationToken);
        }

        return preview;
    }

    public async Task<KitSalesAssistDto> GetAssistAsync(
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _desk.GetLeadDetailAsync(leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead không tồn tại.");
        var journey = await GetJourneyAsync(leadId, null, cancellationToken);
        var inbound = detail.Interactions.FirstOrDefault(i => i.Direction == "inbound");
        var outbound = detail.Interactions.FirstOrDefault(i =>
            i.Direction == "outbound" && i.ReviewStatus == "sent");
        return NovixaSalesAssist.Build(detail.Lead, journey, inbound, outbound);
    }

    public async Task<KitSalesChatComposeDto> ComposeAsync(
        KitSalesChatPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(request.Text ?? "", cancellationToken);
        var voiced = await _voice.RestyleInboundAsync(
            request.Text ?? "",
            preview.Reply,
            preview.Escalate,
            cancellationToken);
        var geminiUsed = !string.Equals(voiced, preview.Reply, StringComparison.Ordinal);
        preview = preview with { Reply = voiced };
        KitSalesAssistDto? assist = null;
        Guid? draftId = null;
        var status = "draft";

        if (request.LeadId is { } leadId)
        {
            assist = await GetAssistAsync(leadId, cancellationToken);
            var detail = await _desk.GetLeadDetailAsync(leadId, cancellationToken)
                ?? throw new InvalidOperationException("Lead không tồn tại.");
            var lastIn = detail.Interactions.FirstOrDefault(i => i.Direction == "inbound");
            if (!string.IsNullOrWhiteSpace(request.Text)
                && !string.Equals(lastIn?.Content?.Trim(), request.Text.Trim(), StringComparison.Ordinal))
            {
                await _desk.CreateInteractionAsync(
                    leadId,
                    new CreateKitSalesInteractionRequest(
                        "facebook",
                        "inbound",
                        "message",
                        request.Text.Trim(),
                        preview.Intent,
                        ReviewStatus: "sent",
                        Classification: preview.Intent),
                    cancellationToken);
            }

            var openDraft = detail.Interactions.FirstOrDefault(i =>
                i.Direction == "outbound" && i.ReviewStatus is "draft" or "approved");
            if (openDraft is not null)
            {
                var updated = await _desk.ReviewInteractionAsync(
                    openDraft.Id,
                    new ReviewKitSalesInteractionRequest("draft", preview.Reply),
                    cancellationToken);
                draftId = updated.Id;
                status = updated.ReviewStatus;
            }
            else
            {
                var draft = await _desk.CreateInteractionAsync(
                    leadId,
                    new CreateKitSalesInteractionRequest(
                        "facebook",
                        "outbound",
                        "message",
                        preview.Reply,
                        preview.Escalate ? "escalate" : preview.Intent,
                        ReviewStatus: "draft",
                        AiGenerated: true,
                        Classification: preview.Intent,
                        MarkSent: false),
                    cancellationToken);
                draftId = draft.Id;
                status = draft.ReviewStatus;
            }

            assist = await GetAssistAsync(leadId, cancellationToken);
        }

        var autoReplied = false;
        if (request.AutoReply && draftId is { } readyId && !string.IsNullOrWhiteSpace(preview.Reply))
        {
            var sent = await SendDraftAsync(
                readyId,
                new SendKitSalesDraftRequest(preview.Reply),
                cancellationToken);
            draftId = sent.Id;
            status = sent.ReviewStatus;
            autoReplied = true;
            if (request.LeadId is { } repliedLead)
                assist = await GetAssistAsync(repliedLead, cancellationToken);
        }

        return new KitSalesChatComposeDto(
            preview.Intent,
            preview.Reply,
            preview.Escalate,
            preview.Citations,
            preview.FacebookSendReady,
            preview.MeLink,
            preview.PhcUrl,
            draftId,
            status,
            NovixaSalesAssist.ClassificationLabel(preview.Intent),
            assist?.NextHint ?? NovixaSalesAssist.ClassificationLabel(preview.Intent),
            assist,
            geminiUsed,
            autoReplied);
    }

    public async Task<KitSalesInteractionDto> SendDraftAsync(
        Guid draftId,
        SendKitSalesDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var sent = await _desk.ReviewInteractionAsync(
            draftId,
            new ReviewKitSalesInteractionRequest("sent", request.Content),
            cancellationToken);

        var psid = await FindPsidAsync(sent.LeadId, cancellationToken);
        if (await _messenger.IsSendReadyAsync(cancellationToken)
            && !string.IsNullOrWhiteSpace(psid)
            && !string.IsNullOrWhiteSpace(sent.Content))
        {
            try
            {
                await _messenger.SendTextAsync(psid, sent.Content, cancellationToken);
            }
            catch (Exception)
            {
                // V1: staff still copied / will paste. Interaction stays sent (human approved).
            }
        }

        return sent;
    }

    public async Task HandleFacebookInboundAsync(
        string senderPsid,
        string text,
        CancellationToken cancellationToken = default)
    {
        var psid = senderPsid.Trim();
        if (string.IsNullOrWhiteSpace(psid))
            return;

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        var preview = await PreviewAsync(text, cancellationToken);
        preview = preview with
        {
            Reply = await _voice.RestyleInboundAsync(text, preview.Reply, preview.Escalate, cancellationToken),
        };
        var leadId = await FindOrCreateLeadByPsidAsync(tenantId, psid, cancellationToken);

        await InsertInteractionAsync(tenantId, leadId, "inbound", text, preview.Intent, cancellationToken);
        var sendOutcome = preview.Escalate ? "escalate" : preview.Intent;
        try
        {
            if (await _messenger.IsSendReadyAsync(cancellationToken))
            {
                await _messenger.SendTextAsync(psid, preview.Reply, cancellationToken);
                sendOutcome += ",sent";
            }
            else
            {
                sendOutcome += ",queued_no_token";
            }
        }
        catch (Exception)
        {
            sendOutcome += ",send_failed";
        }

        await InsertInteractionAsync(tenantId, leadId, "outbound", preview.Reply, sendOutcome, cancellationToken);

        if (preview.Escalate)
        {
            await InsertOpenTaskAsync(tenantId, leadId, cancellationToken);
        }
    }

    private async Task<Guid> FindOrCreateLeadByPsidAsync(
        Guid tenantId,
        string psid,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var existing = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT l.id
            FROM pack_sales.business_identity i
            INNER JOIN pack_sales.lead l ON l.business_id = i.business_id AND l.tenant_id = i.tenant_id
            WHERE i.tenant_id = @TenantId
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
              AND i.username = @Psid
            ORDER BY l.updated_at DESC
            LIMIT 1
            """,
            new { TenantId = tenantId, Psid = psid });
        if (existing is not null)
            return existing.Value;

        var suffix = psid.Length <= 6 ? psid : psid[^6..];
        var lead = await InsertProspectOnTenantAsync(
            conn,
            tenantId,
            $"Facebook {suffix}",
            cancellationToken);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.business_identity (
                tenant_id, business_id, identity_type, platform, username, source
            )
            VALUES (
                @TenantId, @BusinessId, 'facebook', 'messenger', @Psid, 'facebook_messenger'
            )
            """,
            new { TenantId = tenantId, BusinessId = lead.BusinessId, Psid = psid });

        return lead.Id;
    }

    private static async Task<KitSalesLeadDto> InsertProspectOnTenantAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        string name,
        CancellationToken cancellationToken)
    {
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var businessId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_sales.business (
                tenant_id, name, business_type, source, description
            )
            VALUES (
                @TenantId, @Name, 'pharmacy', 'facebook_messenger', 'Inbound Messenger'
            )
            RETURNING id
            """,
            new { TenantId = tenantId, Name = name },
            tx);

        var leadId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_sales.lead (
                tenant_id, business_id, product_code, source, lead_status, lead_temperature, notes
            )
            VALUES (
                @TenantId, @BusinessId, 'novixa', 'facebook_messenger', 'contact', 'warm',
                'Inbound Facebook Messenger'
            )
            RETURNING id
            """,
            new { TenantId = tenantId, BusinessId = businessId },
            tx);

        await tx.CommitAsync(cancellationToken);
        return new KitSalesLeadDto(
            leadId,
            businessId,
            name,
            "novixa",
            "contact",
            "warm",
            0,
            "facebook_messenger",
            null,
            null,
            "Inbound Facebook Messenger",
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private async Task InsertInteractionAsync(
        Guid tenantId,
        Guid leadId,
        string direction,
        string content,
        string? outcome,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.interaction (
                tenant_id, lead_id, channel, direction, interaction_type, content, outcome,
                review_status, ai_generated, sent_at, classification
            )
            VALUES (
                @TenantId, @LeadId, 'facebook', @Direction, 'message', @Content, @Outcome,
                'sent', @AiGenerated, NOW(), @Classification
            )
            """,
            new
            {
                TenantId = tenantId,
                LeadId = leadId,
                Direction = direction,
                Content = content,
                Outcome = outcome,
                AiGenerated = direction == "outbound",
                Classification = ClipClassification(outcome),
            });

        await conn.ExecuteAsync(
            """
            UPDATE pack_sales.lead
            SET last_interaction_at = NOW(), updated_at = NOW()
            WHERE id = @LeadId AND tenant_id = @TenantId
            """,
            new { LeadId = leadId, TenantId = tenantId });
    }

    private async Task InsertOpenTaskAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.task (tenant_id, lead_id, action_code, title)
            VALUES (@TenantId, @LeadId, 'follow_up', 'Trả lời Facebook — ngoài SoT')
            """,
            new { TenantId = tenantId, LeadId = leadId });
    }

    public IReadOnlyList<KitSalesJourneyPainDto> ListPains() => NovixaSalesJourney.Pains;

    public async Task<KitSalesJourneyPlanDto> GetJourneyAsync(
        Guid leadId,
        string? painCode = null,
        CancellationToken cancellationToken = default)
    {
        var detail = await _desk.GetLeadDetailAsync(leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead không tồn tại.");
        return await BuildPlanAsync(detail.Lead, painCode, cancellationToken);
    }

    public async Task<IReadOnlyList<KitSalesJourneyPlanDto>> ListDueJourneysAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var leads = await _desk.ListLeadsAsync(null, 100, cancellationToken);
        var due = new List<KitSalesJourneyPlanDto>();
        foreach (var lead in leads.Where(l =>
                     NovixaSalesJourney.IsJourneyCode(l.NextActionCode) && l.NextActionAt is not null))
        {
            var plan = await BuildPlanAsync(lead, null, cancellationToken);
            if (plan.IsDue)
                due.Add(plan);
        }

        return due.OrderBy(p => p.SuggestedAt).Take(limit).ToList();
    }

    public async Task<KitSalesJourneyPlanDto> ScheduleJourneyAsync(
        Guid leadId,
        ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetJourneyAsync(leadId, request.PainCode, cancellationToken);
        var step = plan.Step;
        var pain = string.IsNullOrWhiteSpace(request.PainCode) ? plan.PainCode : request.PainCode;
        var when = NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow);
        var code = NovixaSalesJourney.Encode(step, pain);

        await _desk.UpdateLeadAsync(
            leadId,
            new UpdateKitSalesLeadRequest(NextActionCode: code, NextActionAt: when),
            cancellationToken);
        await _desk.CreateTaskAsync(
            leadId,
            new CreateKitSalesTaskRequest(code, $"Facebook · {NovixaSalesJourney.Pain(pain).Label}", when),
            cancellationToken);

        return await GetJourneyAsync(leadId, pain, cancellationToken);
    }

    public async Task<KitSalesJourneyPlanDto> SendJourneyAsync(
        Guid leadId,
        ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetJourneyAsync(leadId, request.PainCode, cancellationToken);
        var step = plan.Step;
        var pain = string.IsNullOrWhiteSpace(request.PainCode) ? plan.PainCode : request.PainCode;
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        var draft = step == (request.Step ?? step) && !string.IsNullOrWhiteSpace(request.Content)
            ? request.Content.Trim()
            : NovixaSalesJourney.Draft(step, pain, options.PhcUrl);
        var outcome = $"journey:{step}";

        await _desk.CreateInteractionAsync(
            leadId,
            new CreateKitSalesInteractionRequest(
                "facebook",
                "outbound",
                "message",
                draft,
                outcome,
                ReviewStatus: "sent",
                AiGenerated: true),
            cancellationToken);

        var psid = await FindPsidAsync(leadId, cancellationToken);
        if (await _messenger.IsSendReadyAsync(cancellationToken) && !string.IsNullOrWhiteSpace(psid))
        {
            try
            {
                await _messenger.SendTextAsync(psid, draft, cancellationToken);
                outcome += ",sent";
            }
            catch (Exception)
            {
                outcome += ",send_failed";
            }
        }
        else
        {
            outcome += ",copied";
        }

        if (step == "hook")
            await _pains.RecordApproachAsync(leadId, pain, cancellationToken);

        var next = NovixaSalesJourney.NextStep(step);
        var when = NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow);
        await _desk.UpdateLeadAsync(
            leadId,
            new UpdateKitSalesLeadRequest(
                NextActionCode: NovixaSalesJourney.Encode(next, pain),
                NextActionAt: when),
            cancellationToken);

        return await GetJourneyAsync(leadId, pain, cancellationToken);
    }

    public async Task<KitSalesJourneyPlanDto> RewriteJourneyAsync(
        Guid leadId,
        ScheduleKitSalesJourneyRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetJourneyAsync(leadId, request.PainCode, cancellationToken);
        var step = plan.Step;
        var pain = NovixaPainCatalog.Get(
            string.IsNullOrWhiteSpace(request.PainCode) ? plan.PainCode : request.PainCode);
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        var catalog = NovixaSalesJourney.Draft(step, pain.Code, options.PhcUrl);
        var voiced = await _voice.RestyleOutboundAsync(step, pain, catalog, cancellationToken);
        return plan with
        {
            Step = step,
            PainCode = pain.Code,
            PainLabel = pain.Name,
            Draft = voiced.Draft,
            Hook = voiced.Hook,
            Outreach = voiced.Outreach,
            Question = voiced.Question,
            GeminiUsed = voiced.GeminiUsed,
        };
    }

    public async Task<int> ProcessDueJourneysAsync(CancellationToken cancellationToken = default)
    {
        if (!await _messenger.IsSendReadyAsync(cancellationToken))
            return 0;

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<DueJourneyRow>(
            """
            SELECT id AS Id, next_action_code AS Code
            FROM pack_sales.lead
            WHERE tenant_id = @TenantId
              AND next_action_at IS NOT NULL
              AND next_action_at <= NOW()
              AND next_action_code LIKE '%:%'
            ORDER BY next_action_at
            LIMIT 20
            """,
            new { TenantId = tenantId })).ToList();

        var sent = 0;
        foreach (var row in rows)
        {
            if (!NovixaSalesJourney.IsJourneyCode(row.Code))
                continue;
            var parsed = NovixaSalesJourney.Parse(row.Code);
            var psid = await FindPsidOnTenantAsync(tenantId, row.Id, cancellationToken);
            if (string.IsNullOrWhiteSpace(psid))
                continue;

            var options = await _facebook.GetEffectiveAsync(cancellationToken);
            var draft = NovixaSalesJourney.Draft(parsed.Step, parsed.Pain, options.PhcUrl);
            try
            {
                await _messenger.SendTextAsync(psid, draft, cancellationToken);
            }
            catch (Exception)
            {
                continue;
            }

            await InsertInteractionAsync(
                tenantId, row.Id, "outbound", draft, $"journey:{parsed.Step},sent", cancellationToken);
            var next = NovixaSalesJourney.NextStep(parsed.Step);
            var when = NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow);
            await conn.ExecuteAsync(
                """
                UPDATE pack_sales.lead
                SET next_action_code = @Code,
                    next_action_at = @When,
                    last_interaction_at = NOW(),
                    updated_at = NOW()
                WHERE id = @LeadId AND tenant_id = @TenantId
                """,
                new
                {
                    LeadId = row.Id,
                    TenantId = tenantId,
                    Code = NovixaSalesJourney.Encode(next, parsed.Pain),
                    When = when.UtcDateTime,
                });
            sent++;
        }

        return sent;
    }

    private async Task<KitSalesJourneyPlanDto> BuildPlanAsync(
        KitSalesLeadDto lead,
        string? painOverride,
        CancellationToken cancellationToken)
    {
        var parsed = NovixaSalesJourney.Parse(lead.NextActionCode);
        var profile = await _pains.GetLeadProfileAsync(lead.Id, cancellationToken);
        var mode = "explore";
        var why = NovixaSalesJourney.WhyNow;
        string pain;
        if (string.IsNullOrWhiteSpace(painOverride))
        {
            pain = string.IsNullOrWhiteSpace(parsed.Pain) || parsed.Pain == NovixaPainCatalog.All[0].Code
                ? profile.RecommendedPainCode
                : NovixaPainCatalog.Canonical(parsed.Pain);
            if (pain == profile.RecommendedPainCode)
            {
                mode = profile.DiscoveryMode;
                why = profile.WhyThisPain;
            }
        }
        else
        {
            pain = NovixaPainCatalog.Canonical(painOverride);
            mode = "staff";
            why = "Nhân viên chọn nỗi đau này.";
        }

        var responseCode = profile.Scores.FirstOrDefault(s => s.PainCode == pain)?.ResponseCode;
        var def = NovixaPainCatalog.Get(pain);
        var reveal = NovixaPainDiscovery.RevealsSolution(responseCode);
        var step = reveal ? parsed.Step : "hook";
        var stored = lead.NextActionAt;
        var suggested = stored is { } at && at > DateTimeOffset.UtcNow.AddMinutes(-30)
            ? at
            : NovixaSalesJourney.NextQuietWindow(DateTimeOffset.UtcNow);
        var due = stored is { } dueAt && dueAt <= DateTimeOffset.UtcNow.AddMinutes(20);

        return new KitSalesJourneyPlanDto(
            lead.Id,
            lead.BusinessName,
            step,
            def.Code,
            def.Name,
            NovixaSalesJourney.Draft(step, def.Code, (await _facebook.GetEffectiveAsync(cancellationToken)).PhcUrl),
            NovixaSalesJourney.Citations(step, def.Code),
            suggested,
            NovixaSalesJourney.WindowLabel(suggested),
            NovixaSalesJourney.WhyNow,
            due,
            await _messenger.IsSendReadyAsync(cancellationToken),
            def.Category,
            def.Hook,
            def.Outreach,
            def.Question,
            def.Cta,
            reveal ? def.SolutionDirection : null,
            why,
            mode,
            lead.FacebookUrl);
    }

    private async Task<string?> FindPsidAsync(Guid leadId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<string?>(
            """
            SELECT i.username
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business_identity i
                ON i.business_id = l.business_id AND i.tenant_id = l.tenant_id
            WHERE l.id = @LeadId
              AND l.tenant_id = @TenantId
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
            LIMIT 1
            """,
            new { LeadId = leadId, TenantId = _tenant.TenantId == Guid.Empty ? SalesPackDefinition.DedicatedTenantId : _tenant.TenantId });
    }

    private async Task<string?> FindPsidOnTenantAsync(
        Guid tenantId,
        Guid leadId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<string?>(
            """
            SELECT i.username
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business_identity i
                ON i.business_id = l.business_id AND i.tenant_id = l.tenant_id
            WHERE l.id = @LeadId
              AND l.tenant_id = @TenantId
              AND i.identity_type = 'facebook'
              AND i.platform = 'messenger'
            LIMIT 1
            """,
            new { LeadId = leadId, TenantId = tenantId });
    }

    private static string? ClipClassification(string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
            return null;
        var code = outcome.Split(',', 2)[0].Trim();
        return code.Length <= 64 ? code : code[..64];
    }

    private sealed class DueJourneyRow
    {
        public Guid Id { get; init; }
        public string? Code { get; init; }
    }
}
