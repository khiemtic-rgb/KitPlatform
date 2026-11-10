using Dapper;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;
using Xunit;

namespace KitPlatform.Sales.Tests.Db;

/// <summary>Real services, real PostgreSQL, real constraints and locks. HTTP and audit are fakes.</summary>
[Collection(KitSalesDbCollection.Name)]
public sealed class KitSalesDbIntegrationTests
{
    private static readonly Guid Tenant = SalesPackDefinition.DedicatedTenantId;
    private readonly KitSalesDbFixture _fx;

    public KitSalesDbIntegrationTests(KitSalesDbFixture fx) => _fx = fx;

    private static CountingHandler GraphOk(int delayMs = 0) => new()
    {
        Respond = async (_, ct) =>
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, ct);
            return CountingHandler.Json(200, "{\"recipient_id\":\"1\",\"message_id\":\"m.ci." + Guid.NewGuid().ToString("N") + "\"}");
        },
    };

    // ---------- WP6: settings audit is part of the commit ----------

    [DbFact]
    public async Task Facebook_settings_roll_back_when_audit_fails()
    {
        var audit = new TestAudit { OnWrite = _ => throw new InvalidOperationException("audit down") };
        var s = new KitSalesServices(_fx, audit: audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Facebook.SaveAsync(new SaveKitSalesFacebookSettingsRequest(true, PageAccessToken: "ci-token-rollback"), "https://ci/webhook"));

        await using var conn = _fx.Open();
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.facebook_settings WHERE tenant_id = @Tenant", new { Tenant }));
        Assert.Equal(1, audit.Writes);
    }

    [DbFact]
    public async Task Facebook_settings_change_is_invisible_while_audit_runs_then_commits()
    {
        bool? visibleDuringAudit = null;
        var audit = new TestAudit
        {
            OnWrite = async _ =>
            {
                await using var other = _fx.Open();
                visibleDuringAudit = await other.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::int FROM pack_sales.facebook_settings WHERE tenant_id = @Tenant", new { Tenant }) > 0;
            },
        };
        var s = new KitSalesServices(_fx, audit: audit);
        try
        {
            await s.Facebook.SaveAsync(new SaveKitSalesFacebookSettingsRequest(true, PageAccessToken: "ci-token-commit"), "https://ci/webhook");
            Assert.False(visibleDuringAudit);
            await using var conn = _fx.Open();
            Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM pack_sales.facebook_settings WHERE tenant_id = @Tenant", new { Tenant }));
        }
        finally
        {
            await using var conn = _fx.Open();
            await conn.ExecuteAsync("DELETE FROM pack_sales.facebook_settings WHERE tenant_id = @Tenant", new { Tenant });
        }
    }

    [DbFact]
    public async Task Ai_settings_roll_back_when_audit_fails()
    {
        var s = new KitSalesServices(_fx, audit: new TestAudit { OnWrite = _ => throw new InvalidOperationException("audit down") });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Ai.SaveAsync(new SaveKitSalesGeminiSettingsRequest(GeminiApiKey: "ci-key-rollback")));

        await using var conn = _fx.Open();
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.ai_settings WHERE tenant_id = @Tenant", new { Tenant }));
    }

    [DbFact]
    public async Task Psid_attach_rolls_back_when_audit_fails()
    {
        var leadId = await KitSalesSeed.BareLeadAsync(_fx);
        var s = new KitSalesServices(_fx, audit: new TestAudit { OnWrite = _ => throw new InvalidOperationException("audit down") });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Facebook.AttachPsidAsync(leadId, new AttachKitSalesPsidRequest(KitSalesSeed.NewPsid())));

        Assert.Null(await s.Facebook.GetPsidAsync(leadId));
    }

    // ---------- Webhook: no tenant claim, idempotent per message ----------

    [DbFact]
    public async Task Webhook_runs_without_tenant_claim_and_ignores_a_replayed_message()
    {
        var anonymous = new KitSalesServices(_fx, tenant: new AnonymousTenant());
        var psid = KitSalesSeed.NewPsid();
        var mid = "m.ci." + Guid.NewGuid().ToString("N");

        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, anonymous, psid: psid, mid: mid);
        await KitSalesSeed.InboundAsync(_fx, anonymous, psid: psid, mid: mid);

        Assert.NotNull(draftId);
        Assert.Equal(1, await KitSalesSeed.EventsAsync(_fx, leadId, FacebookOutboundRules.InboundEvent));
        await using var conn = _fx.Open();
        var rows = (await conn.QueryAsync<(string Direction, string ReviewStatus, string TenantId)>(
            "SELECT direction, review_status, tenant_id::text FROM pack_sales.interaction WHERE lead_id = @LeadId",
            new { LeadId = leadId })).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Direction == "inbound");
        Assert.Contains(rows, r => r.Direction == "outbound" && r.ReviewStatus == "draft");
        Assert.All(rows, r => Assert.Equal(Tenant.ToString(), r.TenantId));
        Assert.Equal(0, anonymous.Http.Calls);
    }

    // ---------- WP7: archive keeps history and stops new work ----------

    [DbFact]
    public async Task Archive_keeps_history_dismisses_agent_tasks_and_blocks_every_write_path()
    {
        var s = new KitSalesServices(_fx);
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await using (var conn = _fx.Open())
        {
            await conn.ExecuteAsync(
                "INSERT INTO pack_sales.agent_task (tenant_id, lead_id, task_type) VALUES (@Tenant, @LeadId, 'ci_probe')",
                new { Tenant, LeadId = leadId });
        }

        var draftsBefore = (await s.Agent.DeskKpiAsync()).DraftsWaiting;
        Assert.Equal(LeadRetention.Archived, await s.Desk.DeleteLeadAsync(leadId));
        Assert.Equal(draftsBefore - 1, (await s.Agent.DeskKpiAsync()).DraftsWaiting);

        await using (var conn = _fx.Open())
        {
            Assert.Equal("dismissed", await conn.ExecuteScalarAsync<string>(
                "SELECT status FROM pack_sales.agent_task WHERE lead_id = @LeadId AND task_type = 'ci_probe'", new { LeadId = leadId }));
            Assert.Equal(2, await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*)::int FROM pack_sales.interaction WHERE lead_id = @LeadId", new { LeadId = leadId }));
        }

        Assert.Equal(1, await KitSalesSeed.EventsAsync(_fx, leadId, "archived"));
        var detail = await s.Desk.GetLeadDetailAsync(leadId);
        Assert.Equal(LeadRetention.Archived, detail!.Lead.LeadStatus);

        await AssertArchived(() => s.Desk.CreateInteractionAsync(leadId, new CreateKitSalesInteractionRequest(Content: "ci")));
        await AssertArchived(() => s.Desk.CreateTaskAsync(leadId, new CreateKitSalesTaskRequest("call")));
        await AssertArchived(() => s.Desk.ReviewInteractionAsync(draftId!.Value, new ReviewKitSalesInteractionRequest("approved")));
        await AssertArchived(() => s.Agent.QualifyAsync(leadId));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => s.Agent.ConfirmPhcReportAsync(leadId));
        await AssertArchived(() => s.Pains.RecordApproachAsync(leadId, NovixaPainCatalog.All[0].Code));
        await AssertArchived(() => s.Gate.ApproveAsync(draftId!.Value, null));
        await AssertArchived(() => s.Facebook.AttachPsidAsync(leadId, new AttachKitSalesPsidRequest(KitSalesSeed.NewPsid())));
        Assert.Equal(0, s.Http.Calls);
    }

    [DbFact]
    public async Task Webhook_on_archived_lead_records_inbound_but_drafts_nothing()
    {
        var s = new KitSalesServices(_fx);
        var (leadId, _, psid) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Desk.DeleteLeadAsync(leadId);
        await using var conn = _fx.Open();
        var draftsBefore = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.interaction WHERE lead_id = @LeadId AND direction = 'outbound'", new { LeadId = leadId });

        await KitSalesSeed.InboundAsync(_fx, s, psid: psid);

        Assert.Equal(draftsBefore, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.interaction WHERE lead_id = @LeadId AND direction = 'outbound'", new { LeadId = leadId }));
        Assert.Equal(2, await KitSalesSeed.EventsAsync(_fx, leadId, FacebookOutboundRules.InboundEvent));
    }

    [DbFact]
    public async Task Lead_without_history_is_deleted_not_archived()
    {
        var s = new KitSalesServices(_fx);
        var leadId = await KitSalesSeed.BareLeadAsync(_fx);

        Assert.Equal(LeadRetention.Deleted, await s.Desk.DeleteLeadAsync(leadId));
        Assert.Null(await s.Desk.GetLeadDetailAsync(leadId));
    }

    [DbFact]
    public async Task Archive_waits_for_a_write_that_holds_the_lead_guard()
    {
        var s = new KitSalesServices(_fx);
        var (leadId, _, _) = await KitSalesSeed.InboundAsync(_fx, s);

        await using var holder = _fx.Open();
        await using var tx = await holder.BeginTransactionAsync();
        await KitSalesLeadGuard.EnsureWorkableAsync(holder, tx, Tenant, leadId, CancellationToken.None);

        var archive = s.Desk.DeleteLeadAsync(leadId);
        var raced = await Task.WhenAny(archive, Task.Delay(1500));
        Assert.NotSame(archive, raced);

        await holder.ExecuteAsync(
            "INSERT INTO pack_sales.task (tenant_id, lead_id, action_code, title) VALUES (@Tenant, @LeadId, 'ci_hold', 'ci')",
            new { Tenant, LeadId = leadId }, tx);
        await tx.CommitAsync();

        Assert.Equal(LeadRetention.Archived, await archive.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    // ---------- Facebook outbound gate ----------

    [DbFact]
    public async Task Approved_reply_inside_window_sends_once_and_second_send_makes_no_call()
    {
        var s = new KitSalesServices(_fx, GraphOk());
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Gate.ApproveAsync(draftId!.Value, null);

        var sent = await s.Gate.SendApprovedAsync(draftId.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        Assert.Equal("sent", sent.ReviewStatus);
        Assert.Equal(1, s.Http.Calls);
        Assert.All(s.Http.Uris, u => Assert.Equal(FacebookOutboundGate_Endpoint, u?.ToString()));
        Assert.Equal(1, await KitSalesSeed.EventsAsync(_fx, leadId, "outbound_sent"));
        await using var conn = _fx.Open();
        Assert.StartsWith("m.ci.", await conn.ExecuteScalarAsync<string>(
            "SELECT payload->>'messageId' FROM pack_sales.lead_event WHERE lead_id = @LeadId AND event_type = 'outbound_sent'",
            new { LeadId = leadId }));
        var detail = await s.Desk.GetLeadDetailAsync(leadId);
        Assert.Equal("outbound_sent", detail!.Interactions.Single(i => i.Id == draftId).OutboundState);
    }

    private const string FacebookOutboundGate_Endpoint = "https://graph.facebook.com/v21.0/me/messages";

    [DbFact]
    public async Task Channel_flag_off_blocks_without_http()
    {
        var s = new KitSalesServices(_fx, GraphOk(), channelOn: false);
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Gate.ApproveAsync(draftId!.Value, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        Assert.Contains("channel_off", ex.Message);
        Assert.Equal(0, s.Http.Calls);
        Assert.Equal(1, await KitSalesSeed.EventsAsync(_fx, leadId, "outbound_blocked"));
    }

    [DbFact]
    public async Task Closed_messenger_window_blocks_without_http()
    {
        var s = new KitSalesServices(_fx, GraphOk());
        var old = DateTimeOffset.UtcNow.AddHours(-25).ToUnixTimeMilliseconds();
        var (_, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s, sentAtMs: old);
        await s.Gate.ApproveAsync(draftId!.Value, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        Assert.Contains("messenger_window_closed", ex.Message);
        Assert.Equal(0, s.Http.Calls);
    }

    [DbFact]
    public async Task No_inbound_event_blocks_as_window_unknown()
    {
        var s = new KitSalesServices(_fx, GraphOk());
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await using (var conn = _fx.Open())
        {
            await conn.ExecuteAsync(
                "DELETE FROM pack_sales.lead_event WHERE lead_id = @LeadId AND event_type = @Type",
                new { LeadId = leadId, Type = FacebookOutboundRules.InboundEvent });
        }

        await s.Gate.ApproveAsync(draftId!.Value, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        Assert.Contains("messenger_window_unknown", ex.Message);
        Assert.Equal(0, s.Http.Calls);
    }

    [DbFact]
    public async Task Expired_approval_blocks_without_http()
    {
        var s = new KitSalesServices(_fx, GraphOk());
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Gate.ApproveAsync(draftId!.Value, null);
        await using (var conn = _fx.Open())
        {
            await conn.ExecuteAsync(
                "UPDATE pack_sales.lead_event SET occurred_at = NOW() - INTERVAL '25 hours' WHERE lead_id = @LeadId AND event_type = 'outbound_approved'",
                new { LeadId = leadId });
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        Assert.Contains("approval_expired", ex.Message);
        Assert.Equal(0, s.Http.Calls);
    }

    [DbFact]
    public async Task Parallel_sends_of_one_approval_make_exactly_one_http_call()
    {
        var http = GraphOk(delayMs: 400);
        var first = new KitSalesServices(_fx, http);
        var (_, draftId, _) = await KitSalesSeed.InboundAsync(_fx, first);
        await first.Gate.ApproveAsync(draftId!.Value, null);

        var attempts = Enumerable.Range(0, 6)
            .Select(_ => new KitSalesServices(_fx, http).Gate.SendApprovedAsync(draftId.Value))
            .Select(async t =>
            {
                try
                {
                    return (await t).ReviewStatus;
                }
                catch (InvalidOperationException)
                {
                    return "denied";
                }
            })
            .ToList();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, http.Calls);
        Assert.Equal(1, results.Count(r => r == "sent"));
    }

    [DbFact]
    public async Task Timeout_is_unknown_never_retried_and_a_person_resolves_it()
    {
        var http = new CountingHandler { Respond = (_, _) => throw new TaskCanceledException("ci timeout") };
        var s = new KitSalesServices(_fx, http);
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Gate.ApproveAsync(draftId!.Value, null);
        var openBefore = (await s.Agent.DeskKpiAsync()).OutboundUnknownOpen;

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        Assert.Equal(1, http.Calls);
        Assert.Equal(1, await KitSalesSeed.EventsAsync(_fx, leadId, "outbound_unknown"));
        Assert.Equal(1, await KitSalesSeed.OpenTasksAsync(_fx, leadId, FacebookOutboundReconciler.TaskAction));
        Assert.Equal(openBefore + 1, (await s.Agent.DeskKpiAsync()).OutboundUnknownOpen);
        var detail = await s.Desk.GetLeadDetailAsync(leadId);
        Assert.Equal("outbound_unknown", detail!.Interactions.Single(i => i.Id == draftId).OutboundState);

        var noNote = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Gate.ResolveUnknownAsync(draftId.Value, new ResolveKitSalesOutboundRequest(true)));
        Assert.Contains("note_required", noNote.Message);

        var resolved = await s.Gate.ResolveUnknownAsync(
            draftId.Value, new ResolveKitSalesOutboundRequest(true, "Thấy tin trong hộp thư Page"));

        Assert.Equal("sent", resolved.ReviewStatus);
        Assert.Equal(1, http.Calls);
        Assert.Equal(0, await KitSalesSeed.OpenTasksAsync(_fx, leadId, FacebookOutboundReconciler.TaskAction));
        Assert.Equal(openBefore, (await s.Agent.DeskKpiAsync()).OutboundUnknownOpen);
        await using var conn = _fx.Open();
        Assert.Equal("human", await conn.ExecuteScalarAsync<string>(
            "SELECT payload->>'confirmedBy' FROM pack_sales.lead_event WHERE lead_id = @LeadId AND event_type = 'outbound_sent'",
            new { LeadId = leadId }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Gate.ResolveUnknownAsync(draftId.Value, new ResolveKitSalesOutboundRequest(false)));

        var kpi = await s.Agent.OwnerKpiAsync(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMinutes(1), "novixa");
        Assert.True(kpi.OutboundUnknown >= 1);
        Assert.True(kpi.OutboundSent >= 1);
    }

    [DbFact]
    public async Task Unknown_marked_not_sent_returns_to_draft_and_needs_a_new_approval()
    {
        var http = new CountingHandler { Respond = (_, _) => throw new TaskCanceledException("ci timeout") };
        var s = new KitSalesServices(_fx, http);
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Gate.ApproveAsync(draftId!.Value, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));

        var back = await s.Gate.ResolveUnknownAsync(draftId.Value, new ResolveKitSalesOutboundRequest(false));
        Assert.Equal("draft", back.ReviewStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Gate.SendApprovedAsync(draftId.Value));
        Assert.Equal(1, http.Calls);

        http.Respond = (_, _) => Task.FromResult(CountingHandler.Json(200, "{\"message_id\":\"m.ci.retry\"}"));
        await s.Gate.ApproveAsync(draftId.Value, null);
        var sent = await s.Gate.SendApprovedAsync(draftId.Value);

        Assert.Equal("sent", sent.ReviewStatus);
        Assert.Equal(2, http.Calls);
        Assert.Equal(2, await KitSalesSeed.EventsAsync(_fx, leadId, "outbound_dispatching"));
    }

    [DbFact]
    public async Task Reconciler_marks_a_stale_dispatch_unknown_without_http()
    {
        var s = new KitSalesServices(_fx, GraphOk());
        var (leadId, draftId, _) = await KitSalesSeed.InboundAsync(_fx, s);
        await s.Gate.ApproveAsync(draftId!.Value, null);
        var approvalId = Guid.NewGuid();
        await using var conn = _fx.Open();
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.lead_event (tenant_id, lead_id, event_type, payload, idempotency_key, occurred_at)
            VALUES (@Tenant, @LeadId, 'outbound_dispatching', @Payload::jsonb, @Key, NOW() - INTERVAL '3 minutes')
            """,
            new
            {
                Tenant,
                LeadId = leadId,
                Payload = $"{{\"interactionId\":\"{draftId}\",\"approvalEventId\":\"{approvalId}\",\"contentHash\":\"ci\"}}",
                Key = FacebookOutboundRules.DispatchKey(draftId.Value, approvalId),
            });

        Assert.True(await FacebookOutboundReconciler.ReconcileAsync(conn, Tenant, CancellationToken.None) >= 1);
        Assert.Equal(0, await FacebookOutboundReconciler.ReconcileAsync(conn, Tenant, CancellationToken.None));

        Assert.Equal(1, await KitSalesSeed.EventsAsync(_fx, leadId, "outbound_unknown"));
        Assert.Equal(1, await KitSalesSeed.OpenTasksAsync(_fx, leadId, FacebookOutboundReconciler.TaskAction));
        Assert.Equal(0, s.Http.Calls);
    }

    // ---------- AI gate ----------

    [DbFact]
    public async Task Ai_budget_zero_denies_before_any_reservation()
    {
        await WithAiKeyAsync(async () =>
        {
            var s = new KitSalesServices(_fx, aiBudget: 0);
            var ticket = await ((IKitSalesAiGate)s.Ai).AcquireAsync("ci", 100, CancellationToken.None);
            Assert.False(ticket.Granted);
            Assert.Equal("budget_zero", ticket.DenyReason);
            Assert.Equal(0, await AiRunsAsync());
            Assert.Equal(0, s.Http.Calls);
        });
    }

    [DbFact]
    public async Task Ai_concurrent_reservations_never_exceed_the_budget()
    {
        await WithAiKeyAsync(async () =>
        {
            const long reserve = 1000;
            var tickets = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
                ((IKitSalesAiGate)new KitSalesServices(_fx, aiBudget: 3 * reserve + 500).Ai)
                    .AcquireAsync("ci", reserve, CancellationToken.None)));

            Assert.Equal(3, tickets.Count(t => t.Granted));
            Assert.All(tickets.Where(t => !t.Granted), t => Assert.Equal("budget_exhausted", t.DenyReason));
            Assert.Equal(3, await AiRunsAsync());
        });
    }

    private async Task WithAiKeyAsync(Func<Task> body)
    {
        await using var conn = _fx.Open();
        const string cleanup = """
            DELETE FROM pack_sales.agent_run WHERE tenant_id = @Tenant AND agent_code = @Ai;
            DELETE FROM pack_sales.ai_settings WHERE tenant_id = @Tenant;
            """;
        await conn.ExecuteAsync(cleanup, new { Tenant, Ai = SalesAiBudget.AgentCode });
        await conn.ExecuteAsync(
            "INSERT INTO pack_sales.ai_settings (tenant_id, gemini_api_key) VALUES (@Tenant, 'ci-not-a-real-key')",
            new { Tenant });
        try
        {
            await body();
        }
        finally
        {
            await conn.ExecuteAsync(cleanup, new { Tenant, Ai = SalesAiBudget.AgentCode });
        }
    }

    private async Task<int> AiRunsAsync()
    {
        await using var conn = _fx.Open();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.agent_run WHERE tenant_id = @Tenant AND agent_code = @Ai",
            new { Tenant, Ai = SalesAiBudget.AgentCode });
    }

    private static async Task AssertArchived(Func<Task> act)
    {
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(act);
        Assert.Equal(KitSalesLeadGuard.ArchivedMessage, ex.Message);
    }
}
