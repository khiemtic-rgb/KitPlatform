using System.Data;
using System.Data.Common;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;
using Xunit;

namespace KitPlatform.Sales.Tests;

public sealed class Phase0RemediationTests
{
    // ---------- WP6: no success without an audit row ----------

    [Fact]
    public async Task Audit_failure_rolls_back_and_surfaces_the_error()
    {
        var tx = new RecordingTransaction();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () => throw new InvalidOperationException("audit down")));

        Assert.Equal("audit down", error.Message);
        Assert.Equal(["rollback"], tx.Calls);
    }

    [Fact]
    public async Task Change_commits_only_after_the_audit_row_is_written()
    {
        var tx = new RecordingTransaction();

        await KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () =>
        {
            tx.Calls.Add("audit");
            return Task.CompletedTask;
        });

        Assert.Equal(["audit", "commit"], tx.Calls);
    }

    [Fact]
    public void Every_settings_write_commits_through_the_audit()
    {
        var infra = SalesInfra();
        var ai = File.ReadAllText(Path.Combine(infra, "KitSalesAiSettingsService.cs"));
        var fb = File.ReadAllText(Path.Combine(infra, "KitSalesFacebookSettingsService.cs"));
        var slices = new[]
        {
            Slice(ai, "public async Task<KitSalesGeminiSettingsDto> SaveAsync(", "public async Task<KitSalesGeminiTestDto> TestAsync("),
            Slice(fb, "public async Task<KitSalesFacebookSettingsDto> SaveAsync(", "public async Task<KitSalesFacebookTestDto> TestAsync("),
            Slice(fb, "public async Task<KitSalesFacebookPsidDto> AttachPsidAsync(", "public async Task DetachPsidAsync("),
        };
        foreach (var body in slices)
        {
            Assert.Contains("BeginTransactionAsync", body, StringComparison.Ordinal);
            Assert.Contains("KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () => _audit.WriteAsync(", body, StringComparison.Ordinal);
            Assert.DoesNotContain("tx.CommitAsync", body, StringComparison.Ordinal);
        }

        var detach = Slice(fb, "public async Task DetachPsidAsync(", "private async Task<SettingsRow?> LoadRowAsync(");
        Assert.Contains("KitSalesAuditedCommit.CommitAfterAuditAsync(tx, () => _audit.WriteAsync(", detach, StringComparison.Ordinal);
        Assert.Equal(1, Count(detach, "tx.CommitAsync"));
        Assert.True(detach.IndexOf("if (removed == 0)", StringComparison.Ordinal) < detach.IndexOf("tx.CommitAsync", StringComparison.Ordinal));

        foreach (var file in new[] { ai, fb })
            Assert.Equal(Count(file, "_audit.WriteAsync("), Count(file, "CommitAfterAuditAsync(tx, () => _audit.WriteAsync("));
    }

    // ---------- WP7: archived is terminal ----------

    [Theory]
    [InlineData("archived", "lead_archived")]
    [InlineData("lost", null)]
    [InlineData("contact", null)]
    [InlineData("discovered", null)]
    [InlineData("Archived", null)]
    public void Archived_denies_new_work(string status, string? expected)
    {
        Assert.Equal(expected, LeadRetention.DenyWork(status));
        Assert.Equal(expected is null, LeadRetention.CountsInKpi(status));
    }

    [Fact]
    public void Archived_inbound_is_kept_but_opens_no_draft_or_task()
    {
        Assert.Equal((false, false), FacebookInboundRules.Plan("archived", true));
        Assert.Equal((false, false), FacebookInboundRules.Plan("archived", false));
        Assert.Equal((true, true), FacebookInboundRules.Plan("contact", true));
        Assert.Equal((true, false), FacebookInboundRules.Plan("contact", false));
        Assert.Equal((true, false), FacebookInboundRules.Plan("lost", false));
    }

    [Theory]
    [InlineData("KitSalesAgentService.cs", "public async Task<KitSalesQualificationDto> QualifyAsync(", "public async Task<KitSalesAgentTaskDto> AcceptAsync(")]
    [InlineData("KitSalesAgentService.cs", "public async Task<KitSalesAgentTaskDto> AcceptAsync(", "public async Task<KitSalesAgentTaskDto> DismissAsync(")]
    [InlineData("KitSalesAgentService.cs", "public async Task ConfirmPhcReportAsync(", "public async Task<KitSalesOwnerKpiDto> OwnerKpiAsync(")]
    [InlineData("KitSalesDeskService.cs", "public async Task<KitSalesInteractionDto> CreateInteractionAsync(", "public async Task<KitSalesInteractionDto> ReviewInteractionAsync(")]
    [InlineData("KitSalesDeskService.cs", "public async Task<KitSalesInteractionDto> ReviewInteractionAsync(", "private async Task<KitSalesInteractionDto> LoadInteractionAsync(")]
    [InlineData("KitSalesDeskService.cs", "public async Task<KitSalesTaskDto> CreateTaskAsync(", "public async Task<KitSalesTaskDto> CompleteTaskAsync(")]
    [InlineData("KitSalesPainService.cs", "public async Task RecordApproachAsync(", "public async Task RecordResponseAsync(")]
    [InlineData("KitSalesPainService.cs", "public async Task RecordResponseAsync(", "INSERT INTO pack_sales.lead_pain")]
    [InlineData("KitSalesFacebookSettingsService.cs", "public async Task<KitSalesFacebookPsidDto> AttachPsidAsync(", "public async Task DetachPsidAsync(")]
    [InlineData("FacebookOutboundGate.cs", "public async Task<KitSalesInteractionDto> ApproveAsync(", "public async Task<KitSalesInteractionDto> SendApprovedAsync(")]
    public void Business_writes_check_archived_first(string file, string start, string end)
    {
        var body = Slice(File.ReadAllText(Path.Combine(SalesInfra(), file)), start, end);
        Assert.Contains("KitSalesLeadGuard.EnsureWorkableAsync(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_holds_the_lead_row_inside_a_transaction()
    {
        var guard = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesLeadGuard.cs"));
        Assert.Contains("FOR NO KEY UPDATE", guard, StringComparison.Ordinal);
        Assert.Contains("LeadRetention.DenyWork(status)", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void Archive_dismisses_pending_agent_tasks_and_keeps_history()
    {
        var desk = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesDeskService.cs"));
        var archive = Slice(desk, "if (LeadRetention.Decide(lead.HasHistory) == LeadRetention.Archived)", "return LeadRetention.Archived;");
        Assert.Contains("UPDATE pack_sales.agent_task", archive, StringComparison.Ordinal);
        Assert.Contains("lead_archived", archive, StringComparison.Ordinal);
        Assert.Contains("dismissedAgentTasks", archive, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", archive, StringComparison.Ordinal);
    }

    [Fact]
    public void Kpis_leave_archived_out_but_safety_counters_keep_it()
    {
        var agent = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesAgentService.cs"));
        var owner = Slice(agent, "public async Task<KitSalesOwnerKpiDto> OwnerKpiAsync(", "public async Task<KitSalesDeskKpiDto> DeskKpiAsync(");
        Assert.Contains("AND lead_status <> @Archived", owner, StringComparison.Ordinal);
        Assert.Contains("LeadRetention.CountsInKpi(e.LeadStatus)", owner, StringComparison.Ordinal);
        Assert.Contains("var unapproved = allEvents.Count(", owner, StringComparison.Ordinal);
        Assert.Contains("allEvents.Count(e => e.EventType == \"outbound_blocked\")", owner, StringComparison.Ordinal);

        var desk = Slice(agent, "public async Task<KitSalesDeskKpiDto> DeskKpiAsync(", "private async Task<Guid> InsertRunAsync(");
        Assert.Equal(3, Count(desk, "l.lead_status <> @Archived"));
        Assert.Equal(1, Count(desk, "l.lead_status NOT IN ('lost', 'archived')"));
    }

    // ---------- Webhook: KIT SALES tenant, no user principal ----------

    [Fact]
    public void Webhook_inbound_runs_on_the_sales_tenant_without_user_context()
    {
        var chat = File.ReadAllText(Path.Combine(SalesInfra(), "KitSalesChatService.cs"));
        var body = Slice(chat, "public async Task HandleFacebookInboundAsync(", "private async Task<Guid> FindOrCreateLeadByPsidAsync(");
        Assert.Contains("SalesPackDefinition.DedicatedTenantId", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_desk", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_tenant", body, StringComparison.Ordinal);
        Assert.Contains("FacebookInboundRules.Plan(status, turn.Escalate)", body, StringComparison.Ordinal);
        Assert.Contains("if (fresh == 0)", body, StringComparison.Ordinal);

        var ctor = Assert.Single(typeof(KitSalesChatService).GetConstructors());
        Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(ITenantContext));
        var settingsCtor = Assert.Single(typeof(KitSalesFacebookSettingsService).GetConstructors());
        Assert.DoesNotContain(settingsCtor.GetParameters(), p => p.ParameterType == typeof(ITenantContext));

        var webhook = File.ReadAllText(Path.Combine(FindRepo(), "src", "KitPlatform.Api", "Controllers", "KitSales", "KitSalesFacebookWebhookController.cs"));
        Assert.Contains("_chat.HandleFacebookInboundAsync(psid, text, mid, sentAt, cancellationToken)", webhook, StringComparison.Ordinal);
        Assert.Contains("if (!SignatureOk(raw, options))", webhook, StringComparison.Ordinal);
    }

    [Fact]
    public void Inbound_key_dedupes_facebook_redelivery()
    {
        var a = FacebookOutboundRules.InboundKey("m_abc", "123", 1_700_000_000_000);
        Assert.Equal(a, FacebookOutboundRules.InboundKey("m_abc", "999", null));
        Assert.NotEqual(a, FacebookOutboundRules.InboundKey("m_abd", "123", 1_700_000_000_000));
        Assert.Equal(
            FacebookOutboundRules.InboundKey(null, "123", 1_700_000_000_000),
            FacebookOutboundRules.InboundKey(" ", "123", 1_700_000_000_000));
        Assert.NotEqual(FacebookOutboundRules.InboundKey(null, "123", null), FacebookOutboundRules.InboundKey(null, "123", null));
        Assert.True(FacebookOutboundRules.InboundKey(new string('m', 900), "1", null).Length <= 160);
        Assert.DoesNotContain("m_abc", a, StringComparison.Ordinal);
    }

    // ---------- Messenger 24h window ----------

    [Theory]
    [InlineData(null, "messenger_window_unknown")]
    [InlineData(-60, null)]
    [InlineData(-(24 * 60 - 6), null)]
    [InlineData(-(24 * 60 - 4), "messenger_window_closed")]
    [InlineData(-(25 * 60), "messenger_window_closed")]
    [InlineData(1, null)]
    [InlineData(10, "messenger_window_unknown")]
    public void Messenger_window_blocks_late_or_unknown(int? minutesFromNow, string? expected)
    {
        var now = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset? last = minutesFromNow is null ? null : now.AddMinutes(minutesFromNow.Value);
        Assert.Equal(expected, FacebookOutboundRules.DenyMessengerWindow(last, now));
    }

    [Fact]
    public void Inbound_time_never_runs_ahead_of_receipt()
    {
        var received = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var earlier = received.AddMinutes(-3).ToUnixTimeMilliseconds();
        var later = received.AddMinutes(3).ToUnixTimeMilliseconds();
        Assert.Equal(received.AddMinutes(-3), FacebookOutboundRules.InboundAt(received, earlier));
        Assert.Equal(received, FacebookOutboundRules.InboundAt(received, later));
        Assert.Equal(received, FacebookOutboundRules.InboundAt(received, null));
        Assert.Equal(received, FacebookOutboundRules.InboundAt(received, 0));
        Assert.Equal(received, FacebookOutboundRules.InboundAt(received, -5));
        Assert.Equal(received, FacebookOutboundRules.InboundAt(received, long.MaxValue));
    }

    [Fact]
    public void Reserve_checks_the_window_from_signed_inbound_before_dispatch()
    {
        var gate = File.ReadAllText(Path.Combine(SalesInfra(), "FacebookOutboundGate.cs"));
        var reserve = Slice(gate, "private async Task<Reservation> ReserveAsync(", "/// <summary>B2");
        var window = reserve.IndexOf("FacebookOutboundRules.DenyMessengerWindow(", StringComparison.Ordinal);
        var dispatch = reserve.IndexOf("\"outbound_dispatching\"", StringComparison.Ordinal);
        Assert.True(window > 0 && window < dispatch);
        Assert.Contains("EventType = FacebookOutboundRules.InboundEvent", reserve, StringComparison.Ordinal);

        var writers = Directory.GetFiles(SalesInfra(), "*.cs")
            .Where(f => File.ReadAllText(f).Contains("FacebookOutboundRules.InboundEvent", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["FacebookOutboundGate.cs", "KitSalesChatService.cs"], writers);
        Assert.Equal(1, Count(gate, "FacebookOutboundRules.InboundEvent"));
    }

    [Fact]
    public void Messenger_send_exists_only_behind_the_gate()
    {
        var hits = Directory.GetFiles(Path.Combine(FindRepo(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("me/messages", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .ToArray();
        Assert.Equal(["FacebookOutboundGate.cs"], hits);

        var callers = Directory.GetFiles(SalesInfra(), "*.cs")
            .Sum(f => Count(File.ReadAllText(f), ".SendOnceAsync("));
        Assert.Equal(1, callers);
    }

    // ---------- helpers ----------

    private sealed class RecordingTransaction : DbTransaction
    {
        public List<string> Calls { get; } = [];
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection? DbConnection => null;
        public override void Commit() => Calls.Add("commit");
        public override void Rollback() => Calls.Add("rollback");
    }

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
}
