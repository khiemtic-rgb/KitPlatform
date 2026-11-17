using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesChatService : IKitSalesChatService
{
    private readonly IDbConnectionFactory _db;
    private readonly IKitSalesDeskService _desk;
    private readonly IKitSalesPainService _pains;
    private readonly IFacebookOutboundGate _gate;
    private readonly KitSalesGeminiVoice _voice;
    private readonly KitSalesFacebookSettingsService _facebook;

    public KitSalesChatService(
        IDbConnectionFactory db,
        IKitSalesDeskService desk,
        IKitSalesPainService pains,
        IFacebookOutboundGate gate,
        KitSalesGeminiVoice voice,
        KitSalesFacebookSettingsService facebook)
    {
        _db = db;
        _desk = desk;
        _pains = pains;
        _gate = gate;
        _voice = voice;
        _facebook = facebook;
    }

    private async Task<bool> TokenPresentAsync(CancellationToken cancellationToken)
    {
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        return options.Enabled && !string.IsNullOrWhiteSpace(options.PageAccessToken);
    }

    public async Task<KitSalesChatChannelDto> GetChannelAsync(CancellationToken cancellationToken = default)
    {
        var options = await _facebook.GetEffectiveAsync(cancellationToken);
        return new(
            options.Enabled,
            await TokenPresentAsync(cancellationToken),
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
            await TokenPresentAsync(cancellationToken),
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
                ReviewStatus: "draft",
                AiGenerated: true,
                Classification: preview.Intent,
                MarkSent: false),
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

        const bool autoReplied = false;

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

    public Task<KitSalesInteractionDto> SendDraftAsync(
        Guid draftId,
        SendKitSalesDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Gửi dùng nội dung đã duyệt. Hãy duyệt lại nếu cần sửa.");
        return _gate.SendApprovedAsync(draftId, cancellationToken);
    }

    public async Task HandleFacebookInboundAsync(
        string senderPsid,
        string text,
        string? messageId,
        long? sentAtUnixMs,
        CancellationToken cancellationToken = default)
    {
        var psid = senderPsid.Trim();
        if (string.IsNullOrWhiteSpace(psid) || string.IsNullOrWhiteSpace(text))
            return;

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        var preview = await PreviewAsync(text, cancellationToken);
        var leadId = await FindOrCreateLeadByPsidAsync(tenantId, psid, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var status = await conn.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT lead_status FROM pack_sales.lead WHERE id = @LeadId AND tenant_id = @TenantId FOR NO KEY UPDATE",
                new { LeadId = leadId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("Lead không tồn tại.");

        var fresh = await KitSalesLeadEventWriter.WriteAsync(
            conn, tx, tenantId, leadId, FacebookOutboundRules.InboundEvent, null, null,
            JsonSerializer.Serialize(new { sentAtMs = sentAtUnixMs, intent = preview.Intent }),
            null, null, FacebookOutboundRules.InboundKey(messageId, psid, sentAtUnixMs), cancellationToken);
        if (fresh == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        await InsertInboundAsync(conn, tx, tenantId, leadId, text, preview.Intent, cancellationToken);
        var (draft, task) = FacebookInboundRules.Plan(status, preview.Escalate);
        if (draft)
            await InsertDraftAsync(conn, tx, tenantId, leadId, preview, cancellationToken);
        if (task)
            await InsertOpenTaskAsync(conn, tx, tenantId, leadId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
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

    private static async Task InsertInboundAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid tenantId,
        Guid leadId,
        string content,
        string? outcome,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.interaction (
                    tenant_id, lead_id, channel, direction, interaction_type, content, outcome,
                    review_status, ai_generated, sent_at, classification
                )
                VALUES (
                    @TenantId, @LeadId, 'facebook', 'inbound', 'message', @Content, @Outcome,
                    'sent', FALSE, NOW(), @Classification
                )
                """,
                new
                {
                    TenantId = tenantId,
                    LeadId = leadId,
                    Content = content,
                    Outcome = outcome,
                    Classification = ClipClassification(outcome),
                },
                tx,
                cancellationToken: cancellationToken));

        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_sales.lead
                SET last_interaction_at = NOW(), updated_at = NOW()
                WHERE id = @LeadId AND tenant_id = @TenantId
                """,
                new { LeadId = leadId, TenantId = tenantId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task InsertDraftAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid tenantId,
        Guid leadId,
        KitSalesChatPreviewDto preview,
        CancellationToken cancellationToken)
    {
        if (FacebookOutboundRules.DenyCreate("facebook", "outbound", "draft") is not null)
            throw new InvalidOperationException("Outbound Facebook chỉ được tạo ở trạng thái draft.");
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.interaction (
                    tenant_id, lead_id, channel, direction, interaction_type, content, outcome,
                    review_status, ai_generated, sent_at, classification
                )
                VALUES (
                    @TenantId, @LeadId, 'facebook', 'outbound', 'message', @Content, @Outcome,
                    'draft', TRUE, NULL, @Classification
                )
                """,
                new
                {
                    TenantId = tenantId,
                    LeadId = leadId,
                    Content = preview.Reply,
                    Outcome = preview.Escalate ? "escalate" : preview.Intent is { Length: > 64 } i ? i[..64] : preview.Intent,
                    Classification = ClipClassification(preview.Intent),
                },
                tx,
                cancellationToken: cancellationToken));
    }

    private static Task InsertOpenTaskAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid tenantId,
        Guid leadId,
        CancellationToken cancellationToken) =>
        conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.task (tenant_id, lead_id, action_code, title)
                VALUES (@TenantId, @LeadId, 'follow_up', 'Trả lời Facebook — ngoài SoT')
                """,
                new { TenantId = tenantId, LeadId = leadId },
                tx,
                cancellationToken: cancellationToken));

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
                ReviewStatus: "draft",
                AiGenerated: true,
                MarkSent: false),
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

    public Task<int> ProcessDueJourneysAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
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
            await TokenPresentAsync(cancellationToken),
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

    private static string? ClipClassification(string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
            return null;
        var code = outcome.Split(',', 2)[0].Trim();
        return code.Length <= 64 ? code : code[..64];
    }
}
