using System.Net;
using System.Text.Json;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KitPlatform.Sales.Tests;

public sealed class Phase0GateTests
{
    // ---------- WP4: AI budget, one model, usage ----------

    [Theory]
    [InlineData(null, 1000, 0, 10, "no_key")]
    [InlineData("  ", 1000, 0, 10, "no_key")]
    [InlineData("k", 0, 0, 10, "budget_zero")]
    [InlineData("k", -5, 0, 10, "budget_zero")]
    [InlineData("k", 1000, 995, 10, "budget_exhausted")]
    [InlineData("k", 1000, 0, 0, "budget_exhausted")]
    [InlineData("k", 1000, -1, 10, "budget_exhausted")]
    public void Ai_budget_denies_fail_closed(string? key, long budget, long used, long reserve, string expected)
    {
        Assert.Equal(expected, SalesAiBudget.Deny(key, budget, used, reserve));
    }

    [Fact]
    public void Ai_budget_allows_only_when_reserve_fits()
    {
        Assert.Null(SalesAiBudget.Deny("k", 1000, 990, 10));
        Assert.True(SalesAiBudget.Reserve("abc", "def") > SalesAiBudget.OutputReserve);
        Assert.Equal(42, SalesAiBudget.Charge(42, 900));
        Assert.Equal(900, SalesAiBudget.Charge(null, 900));
        Assert.Equal(900, SalesAiBudget.Charge(0, 900));
    }

    [Fact]
    public async Task Denied_ticket_makes_no_http_call()
    {
        var http = new CountingHandler(_ => Json(HttpStatusCode.OK, GeminiReply("""{"reply":"x"}""")));
        var gate = new FakeAiGate(KitSalesAiTicket.Denied("budget_zero", SalesAiBudget.DefaultModel));
        var voice = Voice(http, gate);

        var inbound = await voice.RestyleInboundAsync("hỏi", "câu khóa", false, CancellationToken.None);
        var outbound = await Assert.ThrowsAsync<KitSalesAiDeniedException>(() =>
            voice.RestyleOutboundAsync("hook", NovixaPainCatalog.All[0], "bản gốc", CancellationToken.None));

        Assert.Equal("câu khóa", inbound);
        Assert.Equal("budget_zero", outbound.Reason);
        Assert.Equal(0, http.Calls);
        Assert.Empty(gate.Settled);
    }

    [Fact]
    public async Task Failed_model_is_not_followed_by_a_fallback_model()
    {
        var http = new CountingHandler(_ => Json(HttpStatusCode.InternalServerError, "{}"));
        var gate = new FakeAiGate(Granted("gemini-x"));
        var voice = Voice(http, gate);

        var reply = await voice.RestyleInboundAsync("hỏi", "câu khóa", false, CancellationToken.None);

        Assert.Equal("câu khóa", reply);
        Assert.Equal(1, http.Calls);
        Assert.Contains("gemini-x", http.LastUri!.AbsoluteUri, StringComparison.Ordinal);
        var settled = Assert.Single(gate.Settled);
        Assert.False(settled.Ok);
        Assert.Null(settled.TotalTokens);
    }

    [Fact]
    public async Task Successful_call_settles_reported_usage()
    {
        var http = new CountingHandler(_ => Json(HttpStatusCode.OK, GeminiReply("""{"reply":"viết lại"}""", 120, 30, 150)));
        var gate = new FakeAiGate(Granted("gemini-x"));
        var voice = Voice(http, gate);

        var reply = await voice.RestyleInboundAsync("hỏi", "câu khóa", false, CancellationToken.None);

        Assert.Equal("viết lại", reply);
        Assert.Equal(1, http.Calls);
        var settled = Assert.Single(gate.Settled);
        Assert.True(settled.Ok);
        Assert.Equal(150, settled.TotalTokens);
        Assert.Equal(120, settled.PromptTokens);
    }

    [Fact]
    public void Sales_ai_does_not_borrow_content_key_or_fallback_models()
    {
        var infra = SalesInfra();
        var settings = File.ReadAllText(Path.Combine(infra, "KitSalesAiSettingsService.cs"));
        var voice = File.ReadAllText(Path.Combine(infra, "KitSalesGeminiVoice.cs"));
        Assert.DoesNotContain("ContentParkGeminiKeyResolver", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ContentParkGeminiKeyResolver", voice, StringComparison.Ordinal);
        Assert.DoesNotContain("FallbackModels", voice, StringComparison.Ordinal);
        Assert.Contains("_gate.AcquireAsync", voice, StringComparison.Ordinal);
        Assert.Contains("pg_advisory_xact_lock", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Webhook_inbound_calls_neither_ai_nor_outbound_gate()
    {
        var chat = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesChatService.cs"));
        var body = Slice(chat, "public async Task HandleFacebookInboundAsync(", "private async Task<Guid> FindOrCreateLeadByPsidAsync(");
        Assert.DoesNotContain("_voice", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_gate", body, StringComparison.Ordinal);
        Assert.Contains("InsertDraftAsync", body, StringComparison.Ordinal);
        var draft = Slice(chat, "private static async Task InsertDraftAsync(", "private static Task InsertOpenTaskAsync(");
        Assert.Contains("'draft', TRUE, NULL", draft, StringComparison.Ordinal);
    }

    // ---------- WP6: audit without secrets ----------

    [Fact]
    public void Settings_audit_never_carries_secret_values()
    {
        const string oldToken = "EAAold-token-1234";
        const string newToken = "EAAnew-token-9876";
        const string secret = "app-secret-abcdef";
        var fb = JsonSerializer.Serialize(SalesSettingsAudit.Facebook(
            false, true, oldToken, newToken, null, "verify-xyz", secret, null, "123", "456"));
        var ai = JsonSerializer.Serialize(SalesSettingsAudit.Ai(null, "AIzaSecretKey", null, "gemini-x"));

        foreach (var raw in new[] { oldToken, newToken, secret, "verify-xyz", "AIzaSecretKey", "1234", "9876" })
        {
            Assert.DoesNotContain(raw, fb, StringComparison.Ordinal);
            Assert.DoesNotContain(raw, ai, StringComparison.Ordinal);
        }

        Assert.Contains("\"page_access_token\":\"replaced\"", fb, StringComparison.Ordinal);
        Assert.Contains("\"verify_token\":\"set\"", fb, StringComparison.Ordinal);
        Assert.Contains("\"app_secret\":\"cleared\"", fb, StringComparison.Ordinal);
        Assert.Contains("\"api_key\":\"set\"", ai, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null, "unchanged")]
    [InlineData("a", "a", "unchanged")]
    [InlineData(null, "a", "set")]
    [InlineData("a", "b", "replaced")]
    [InlineData("a", null, "cleared")]
    [InlineData("a", " ", "cleared")]
    public void Secret_change_is_a_state_not_a_value(string? before, string? after, string expected)
    {
        Assert.Equal(expected, SalesSettingsAudit.SecretChange(before, after));
    }

    [Fact]
    public void Settings_writes_are_audited()
    {
        var infra = SalesInfra();
        var ai = File.ReadAllText(Path.Combine(infra, "KitSalesAiSettingsService.cs"));
        var fb = File.ReadAllText(Path.Combine(infra, "KitSalesFacebookSettingsService.cs"));
        Assert.Contains("_audit.WriteAsync", Slice(ai, "public async Task<KitSalesGeminiSettingsDto> SaveAsync(", "public async Task<KitSalesGeminiTestDto> TestAsync("), StringComparison.Ordinal);
        Assert.Contains("_audit.WriteAsync", Slice(fb, "public async Task<KitSalesFacebookSettingsDto> SaveAsync(", "public async Task<KitSalesFacebookTestDto> TestAsync("), StringComparison.Ordinal);
        Assert.Contains("_audit.WriteAsync", Slice(fb, "public async Task<KitSalesFacebookPsidDto> AttachPsidAsync(", "public async Task DetachPsidAsync("), StringComparison.Ordinal);
        Assert.Contains("_audit.WriteAsync", Slice(fb, "public async Task DetachPsidAsync(", "private async Task<SettingsRow?> LoadRowAsync("), StringComparison.Ordinal);
    }

    // ---------- WP7: archive instead of cascade delete ----------

    [Fact]
    public void Lead_with_history_is_archived_and_business_is_kept()
    {
        Assert.Equal(LeadRetention.Archived, LeadRetention.Decide(true));
        Assert.Equal(LeadRetention.Deleted, LeadRetention.Decide(false));
        Assert.False(LeadRetention.MayDeleteBusiness(false, 0, false));
        Assert.False(LeadRetention.MayDeleteBusiness(true, 1, false));
        Assert.False(LeadRetention.MayDeleteBusiness(true, 0, true));
        Assert.True(LeadRetention.MayDeleteBusiness(true, 0, false));
        Assert.True(LeadRetention.IsClosed("archived"));
        Assert.True(LeadRetention.IsClosed("lost"));
        Assert.False(LeadRetention.IsClosed("engage"));
    }

    [Fact]
    public void Archived_is_terminal_and_not_reachable_by_stage_change()
    {
        Assert.Equal("archived_is_terminal", LeadStagePolicy.Deny("archived", "engage", true, true, true, true, null));
        Assert.Equal("archive_via_retention", LeadStagePolicy.Deny("engage", "archived", true, true, true, true, null));
    }

    [Fact]
    public void Delete_checks_every_history_table_under_row_lock()
    {
        var desk = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesDeskService.cs"));
        var history = Slice(desk, "internal const string LeadHistorySql", "\"\"\";");
        foreach (var table in new[] { "interaction", "lead_event", "task", "lead_pain", "signal", "agent_run", "agent_task" })
            Assert.Contains($"pack_sales.{table} x", history, StringComparison.Ordinal);
        Assert.Contains("assessment_submission_id IS NOT NULL", history, StringComparison.Ordinal);

        var delete = Slice(desk, "public async Task<string> DeleteLeadAsync(", "internal const string LeadHistorySql");
        Assert.Contains("FOR UPDATE OF l", delete, StringComparison.Ordinal);
        Assert.Contains("LeadRetention.MayDeleteBusiness", delete, StringComparison.Ordinal);
        Assert.Contains("\"archived\"", delete, StringComparison.Ordinal);
    }

    [Fact]
    public void Archived_leads_leave_sweep_pipeline_and_outbound()
    {
        var infra = SalesInfra();
        var sweep = File.ReadAllText(Path.Combine(infra, "KitSalesAgentSweep.cs"));
        var desk = File.ReadAllText(Path.Combine(infra, "KitSalesDeskService.cs"));
        var gate = File.ReadAllText(Path.Combine(infra, "FacebookOutboundGate.cs"));
        Assert.Contains("l.lead_status <> @Archived", sweep, StringComparison.Ordinal);
        Assert.Contains("lead_status <> @Archived", Slice(desk, "GetPipelineSummaryAsync(", "InsertLeadAsync("), StringComparison.Ordinal);
        Assert.Contains("LeadRetention.ClosedStatuses", gate, StringComparison.Ordinal);
        Assert.Contains("Lead đã lưu trữ", desk, StringComparison.Ordinal);
    }

    // ---------- WP8: three-step dispatch ----------

    [Theory]
    [InlineData(200, true, false, "sent")]
    [InlineData(200, false, false, "unknown")]
    [InlineData(204, false, false, "unknown")]
    [InlineData(400, false, false, "failed")]
    [InlineData(403, false, false, "failed")]
    [InlineData(500, false, false, "unknown")]
    [InlineData(503, true, false, "unknown")]
    [InlineData(302, false, false, "unknown")]
    [InlineData(null, false, false, "unknown")]
    [InlineData(null, false, true, "failed")]
    public void Dispatch_outcome_is_sent_only_with_message_id(int? status, bool messageId, bool preSend, string expected)
    {
        Assert.Equal(expected, FacebookOutboundRules.ClassifyDispatch(status, messageId, preSend));
    }

    [Fact]
    public void Dispatch_requires_channel_flag_fresh_approval_and_no_flight()
    {
        var now = DateTimeOffset.UtcNow;
        var approval = Guid.NewGuid();
        Assert.Equal("channel_off", FacebookOutboundRules.DenyDispatch(null, true, approval, now, now, false));
        Assert.Equal("channel_off", FacebookOutboundRules.DenyDispatch(false, true, approval, now, now, false));
        Assert.Equal("settings_off", FacebookOutboundRules.DenyDispatch(true, false, approval, now, now, false));
        Assert.Equal("approval_missing", FacebookOutboundRules.DenyDispatch(true, true, null, now, now, false));
        Assert.Equal("approval_missing", FacebookOutboundRules.DenyDispatch(true, true, approval, null, now, false));
        Assert.Equal("approval_expired", FacebookOutboundRules.DenyDispatch(true, true, approval, now.AddHours(-24).AddSeconds(-1), now, false));
        Assert.Equal("approval_expired", FacebookOutboundRules.DenyDispatch(true, true, approval, now.AddHours(1), now, false));
        Assert.Equal("dispatch_in_flight", FacebookOutboundRules.DenyDispatch(true, true, approval, now, now, true));
        Assert.Null(FacebookOutboundRules.DenyDispatch(true, true, approval, now.AddHours(-23), now, false));
    }

    [Fact]
    public void Dispatch_key_is_one_per_approval()
    {
        var interaction = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.Equal(FacebookOutboundRules.DispatchKey(interaction, a), FacebookOutboundRules.DispatchKey(interaction, a));
        Assert.NotEqual(FacebookOutboundRules.DispatchKey(interaction, a), FacebookOutboundRules.DispatchKey(interaction, b));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"recipient_id":"1","message_id":"m_1"}""", "sent")]
    [InlineData(HttpStatusCode.OK, """{"recipient_id":"1"}""", "unknown")]
    [InlineData(HttpStatusCode.OK, "not json", "unknown")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"x"}}""", "failed")]
    [InlineData(HttpStatusCode.BadGateway, "", "unknown")]
    public async Task Graph_sender_makes_one_call_and_classifies(HttpStatusCode status, string body, string expected)
    {
        var http = new CountingHandler(_ => Json(status, body));
        var sender = new FacebookGraphSender(() => new HttpClient(http));

        var result = await sender.SendOnceAsync("token", "123456", "xin chào", CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(1, http.Calls);
        Assert.Equal(FacebookGraphSender.Endpoint, http.LastUri!.AbsoluteUri);
        if (expected == "sent")
            Assert.Equal("m_1", result.MessageId);
    }

    [Fact]
    public async Task Graph_sender_treats_timeout_and_broken_response_as_unknown()
    {
        var timeout = new CountingHandler(_ => throw new TaskCanceledException("timeout"));
        var broken = new CountingHandler(_ => throw new HttpRequestException(HttpRequestError.ResponseEnded, "reset"));
        var refused = new CountingHandler(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused"));

        Assert.Equal("unknown", (await new FacebookGraphSender(() => new HttpClient(timeout)).SendOnceAsync("t", "1", "x", CancellationToken.None)).Outcome);
        Assert.Equal("unknown", (await new FacebookGraphSender(() => new HttpClient(broken)).SendOnceAsync("t", "1", "x", CancellationToken.None)).Outcome);
        Assert.Equal("failed", (await new FacebookGraphSender(() => new HttpClient(refused)).SendOnceAsync("t", "1", "x", CancellationToken.None)).Outcome);
        Assert.Equal(1, timeout.Calls);
        Assert.Equal(1, broken.Calls);
        Assert.Equal(1, refused.Calls);
    }

    [Fact]
    public void Http_never_runs_inside_a_transaction()
    {
        var gate = File.ReadAllText(Path.Combine(SalesInfra(), "FacebookOutboundGate.cs"));
        var send = Slice(gate, "public async Task<KitSalesInteractionDto> SendApprovedAsync(", "/// <summary>B1");
        Assert.DoesNotContain("BeginTransactionAsync", send, StringComparison.Ordinal);
        Assert.DoesNotContain("FOR UPDATE", send, StringComparison.Ordinal);
        Assert.Contains("ReserveAsync", send, StringComparison.Ordinal);
        Assert.Contains("PreflightAsync", send, StringComparison.Ordinal);
        Assert.Contains("RecordOutcomeAsync", send, StringComparison.Ordinal);
        Assert.Equal(1, Count(gate, "_graph.SendOnceAsync("));

        var reserve = Slice(gate, "private async Task<Reservation> ReserveAsync(", "/// <summary>B2");
        Assert.DoesNotContain("SendOnceAsync", reserve, StringComparison.Ordinal);
        Assert.Contains("FacebookOutboundRules.DispatchKey", reserve, StringComparison.Ordinal);
        Assert.Contains("already_dispatched", reserve, StringComparison.Ordinal);
        Assert.Contains("approvalRef", reserve, StringComparison.Ordinal);
    }

    [Fact]
    public void Channel_flag_is_read_again_right_before_http()
    {
        var gate = File.ReadAllText(Path.Combine(SalesInfra(), "FacebookOutboundGate.cs"));
        var preflight = Slice(gate, "private async Task<string> PreflightAsync(", "/// <summary>B3");
        Assert.Contains("ReadChannelFlag() is not true", preflight, StringComparison.Ordinal);
        Assert.Contains("GetFreshAsync", preflight, StringComparison.Ordinal);
        Assert.Contains("preflight_read_failed", preflight, StringComparison.Ordinal);
        Assert.Equal("KitSales:Outbound:facebook:Enabled", FacebookOutboundRules.ChannelFlagKey);
    }

    [Fact]
    public void Reconciler_marks_unknown_and_never_sends()
    {
        var infra = SalesInfra();
        var reconciler = File.ReadAllText(Path.Combine(infra, "FacebookOutboundReconciler.cs"));
        Assert.DoesNotContain("SendOnceAsync", reconciler, StringComparison.Ordinal);
        Assert.DoesNotContain("graph.facebook.com", reconciler, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", reconciler, StringComparison.Ordinal);
        Assert.Contains("\"outbound_unknown\"", reconciler, StringComparison.Ordinal);
        Assert.Contains("FacebookOutboundReconciler.ReconcileAsync", File.ReadAllText(Path.Combine(infra, "KitSalesAgentSweep.cs")), StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromMinutes(2), FacebookOutboundRules.DispatchStaleAfter);
    }

    [Fact]
    public void Sent_facebook_outbound_cannot_be_reopened()
    {
        var desk = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesDeskService.cs"));
        var review = Slice(desk, "public async Task<KitSalesInteractionDto> ReviewInteractionAsync(", "await conn.ExecuteAsync(");
        Assert.Contains("Tin Facebook đã gửi, không mở lại được.", review, StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private static KitSalesGeminiVoice Voice(HttpMessageHandler http, IKitSalesAiGate gate) =>
        new(new HttpClient(http), gate, NullLogger<KitSalesGeminiVoice>.Instance);

    private static KitSalesAiTicket Granted(string model) => new(Guid.NewGuid(), "test-key", model, null, 2000);

    private static string GeminiReply(string text, int? prompt = null, int? output = null, int? total = null)
    {
        var candidates = new[] { new { content = new { parts = new[] { new { text } } } } };
        return total is null
            ? JsonSerializer.Serialize(new { candidates })
            : JsonSerializer.Serialize(new
            {
                candidates,
                usageMetadata = new { promptTokenCount = prompt, candidatesTokenCount = output, totalTokenCount = total },
            });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static int Count(string source, string needle)
    {
        var count = 0;
        for (var i = source.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = source.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string Slice(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"Missing marker: {start}");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        Assert.True(to > from, $"Missing end marker: {end}");
        return source[from..to];
    }

    private static string SalesInfra() =>
        Path.Combine(FindRepo(), "src", "Packs", "Sales", "KitPlatform.Packs.Sales.Infrastructure");

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.kit-sales.txt")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeAiGate(KitSalesAiTicket ticket) : IKitSalesAiGate
    {
        public List<KitSalesAiUsage> Settled { get; } = [];

        public Task<KitSalesAiTicket> AcquireAsync(string purpose, long reserveTokens, CancellationToken cancellationToken) =>
            Task.FromResult(ticket);

        public Task SettleAsync(KitSalesAiTicket granted, KitSalesAiUsage usage, CancellationToken cancellationToken)
        {
            Settled.Add(usage);
            return Task.CompletedTask;
        }
    }
}
