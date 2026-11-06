using System.Text;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;
using Xunit;

namespace KitPlatform.Sales.Tests;

public class QualificationCopilotTests
{
    [Fact]
    public void Agent_cycle_plans_once_and_keeps_send_behind_approval()
    {
        var leadId = Guid.NewGuid();
        var first = SalesAgentCycle.Plan(Sample(leadId, null, false));
        Assert.Equal("pursue_phc", first.Decision);
        Assert.True(first.Changed);
        Assert.Contains(first.Actions, action => action.Code == "create_draft" && action.Class == SalesActionClass.Automatic);
        Assert.Contains(first.Actions, action => action.Code == "accept_score" && action.Class == SalesActionClass.NeedsApproval);
        Assert.Contains(first.Actions, action => action.Code == "send_outbound" && action.Class == SalesActionClass.NeedsApproval);
        Assert.DoesNotContain(SalesAgentCycle.Executable(first), action => action.Code is "send_outbound" or "link_phc" or "revenue_recognized");

        var again = SalesAgentCycle.Plan(Sample(leadId, first.Fingerprint, true));
        Assert.False(again.Changed);
        Assert.Empty(again.Actions);

        var reviewed = Sample(leadId, first.Fingerprint, true) with
        {
            LastInbound = "có quan tâm",
            HasInbound = true,
            InboundAfterOutbound = true,
            Pains = [new QualificationPainSignal("interested", 70, DateTimeOffset.UtcNow)],
        };
        var next = SalesAgentCycle.Plan(reviewed);
        Assert.NotEqual(first.Fingerprint, next.Fingerprint);
        Assert.True(next.Changed);
    }

    [Fact]
    public void Agent_cycle_blocks_catalog_lost_paid_and_phc()
    {
        var leadId = Guid.NewGuid();
        var famixa = SalesAgentCycle.Plan(Sample(leadId, null, false) with { ProductCode = "famixa", ProductStatus = "planned" });
        Assert.Equal("blocked_catalog", famixa.Decision);
        Assert.Contains(famixa.Actions, action => action.Code == "create_draft" && action.Class == SalesActionClass.Forbidden);

        var lost = SalesAgentCycle.Plan(Sample(leadId, null, false) with { LeadStatus = "lost" });
        Assert.DoesNotContain(SalesAgentCycle.Executable(lost), action => action.Code == "send_outbound");

        var paid = SalesAgentCycle.Plan(Sample(leadId, null, false) with { LeadStatus = "paid" });
        Assert.Contains(paid.Actions, action => action.Code == "revenue_recognized" && action.Class == SalesActionClass.Forbidden);

        var open = SalesAgentCycle.Plan(Sample(leadId, null, false));
        Assert.Contains(open.Actions, action => action.Code == "link_phc" && action.Class == SalesActionClass.Forbidden);
    }

    private static SalesLeadObservation Sample(Guid leadId, string? fingerprint, bool pending) =>
        new(
            leadId,
            "novixa",
            "active",
            "active",
            "engage",
            "pharmacy",
            true,
            true,
            true,
            true,
            [new QualificationPainSignal("interested", 60, DateTimeOffset.UtcNow)],
            true,
            false,
            true,
            true,
            true,
            "em quan tâm",
            false,
            pending,
            fingerprint);

    [Fact]
    public void Example_scores_76()
    {
        var score = NovixaQualificationScore.Evaluate(new QualificationFacts(
            "pharmacy",
            true,
            true,
            false,
            true,
            [new QualificationPainSignal("interested", 60, DateTimeOffset.UtcNow)],
            true,
            false,
            true,
            true,
            false));

        Assert.Equal(90, score.Fit);
        Assert.Equal(60, score.Pain);
        Assert.Equal(70, score.Intent);
        Assert.Equal(80, score.Engagement);
        Assert.Equal(76.00m, score.Total);
        Assert.Equal("pursue_phc", score.Decision);
        Assert.Equal("hot", score.Temperature);
    }

    [Fact]
    public void Not_interested_is_disqualify_even_when_fit_is_high()
    {
        var score = NovixaQualificationScore.Evaluate(new QualificationFacts(
            "pharmacy", true, true, true, true,
            [new QualificationPainSignal("replied_not_interested", 10, DateTimeOffset.UtcNow)],
            true, false, true, true, false));

        Assert.Equal("disqualify", score.Decision);
    }

    [Fact]
    public void Non_pharmacy_is_out_of_icp()
    {
        var score = NovixaQualificationScore.Evaluate(new QualificationFacts(
            "clinic", true, true, false, true, [], false, false, false, false, false));
        Assert.Equal("disqualify", score.Decision);
        Assert.Equal(0, score.Fit);
    }

    [Fact]
    public void Missing_business_type_is_insufficient()
    {
        var score = NovixaQualificationScore.Evaluate(new QualificationFacts(
            null, true, true, false, true, [], false, false, false, false, false));
        Assert.Equal("insufficient", score.Decision);
        Assert.False(score.Scored);
    }

    [Fact]
    public void Demo_response_does_not_set_intent()
    {
        var score = NovixaQualificationScore.Evaluate(new QualificationFacts(
            "pharmacy", true, true, false, true,
            [new QualificationPainSignal("demo", 90, DateTimeOffset.UtcNow)],
            true, false, true, false, false));
        Assert.Equal("insufficient", score.Decision);
        Assert.Null(score.Intent);
    }

    [Fact]
    public void Stage_skip_and_paid_fact_are_rejected()
    {
        Assert.Equal("skip_forbidden", LeadStagePolicy.Deny("engage", "demo", true, true, false, false, null));
        Assert.Equal("phc_report_required", LeadStagePolicy.Deny("phc", "qualify", true, false, false, false, null));
        Assert.Equal("lost_reason_required", LeadStagePolicy.Deny("engage", "lost", false, false, false, false, null));
        Assert.Equal("paid_fact_required", LeadStagePolicy.Deny("pilot", "paid", true, true, false, false, null));
        Assert.Null(LeadStagePolicy.Deny("pilot", "paid", true, true, false, false, "pilot_30d"));
        Assert.Equal(299_000, LeadStagePolicy.PaidAmount("founding_month"));
        Assert.Equal(598_000, LeadStagePolicy.PaidAmount("pilot_30d_app"));
    }

    [Fact]
    public void Dwell_adds_return_visits_and_keeps_open_visit_out_of_closed()
    {
        var t0 = DateTimeOffset.Parse("2026-11-01T00:00:00Z");
        var events = new[]
        {
            new StageVisitEvent("engage", t0),
            new StageVisitEvent("phc", t0.AddHours(1)),
            new StageVisitEvent("engage", t0.AddHours(2)),
            new StageVisitEvent("phc", t0.AddHours(4)),
        };
        var (closed, open, status) = SalesDwell.Compute(events, t0.AddHours(5));
        Assert.Equal(TimeSpan.FromHours(3), closed["engage"]);
        Assert.Equal(TimeSpan.FromHours(1), closed["phc"]);
        Assert.Equal("phc", status);
        Assert.Equal(TimeSpan.FromHours(1), open);
    }

    [Fact]
    public void Qualification_cohort_excludes_leads_younger_than_maturity()
    {
        var asOf = DateTimeOffset.Parse("2026-11-30T00:00:00Z");
        var start = asOf.AddDays(-30);
        Assert.False(SalesCohort.InEligibleWindow(asOf.AddDays(-3), start, asOf, 7));
        Assert.True(SalesCohort.InEligibleWindow(asOf.AddDays(-10), start, asOf, 7));
    }

    [Fact]
    public void Conversion_rate_uses_one_cohort_and_stays_null_until_mature()
    {
        var end = DateTimeOffset.Parse("2026-11-01T00:00:00Z");
        Assert.Null(SalesCohort.Rate(4, 2, end, end.AddDays(13), 14));
        Assert.Equal(0.5m, SalesCohort.Rate(4, 2, end, end.AddDays(14), 14));
    }

    [Fact]
    public void Flow_in_period_is_separate_from_mature_cohort_rate()
    {
        Assert.Null(SalesCohort.Flow(0, 0));
        Assert.Equal(1m, SalesCohort.Flow(2, 2));
        var end = DateTimeOffset.Parse("2026-11-01T00:00:00Z");
        Assert.Null(SalesCohort.Rate(2, 2, end, end, 14));
    }

    [Fact]
    public void Creating_facebook_outbound_as_sent_is_rejected()
    {
        Assert.Equal("draft_only", FacebookOutboundRules.DenyCreate("facebook", "outbound", "sent"));
        Assert.Null(FacebookOutboundRules.DenyCreate("facebook", "outbound", "draft"));
        Assert.Null(FacebookOutboundRules.DenyCreate("phone", "outbound", "sent"));
    }

    [Fact]
    public void Paid_stage_and_worker_do_not_write_revenue_or_send()
    {
        var root = FindRepo();
        var desk = File.ReadAllText(Path.Combine(root, "src", "Packs", "Sales", "KitPlatform.Packs.Sales.Infrastructure", "KitSalesDeskService.cs"));
        Assert.DoesNotContain("revenue_recognized", desk, StringComparison.Ordinal);
        var worker = File.ReadAllText(Path.Combine(root, "src", "KitPlatform.Api", "KitSales", "KitSalesJourneyWorker.cs"));
        Assert.DoesNotContain("ProcessDueJourneysAsync", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("graph.facebook.com", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateInteraction", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Facebook_gate_rejects_draft_and_hash_mismatch()
    {
        var hash = FacebookOutboundRules.Hash("hello");
        Assert.Equal("not_approved", FacebookOutboundRules.Deny(
            "facebook", "outbound", "draft", Guid.NewGuid(), false, "hello", hash, hash, false));
        Assert.Equal("hash", FacebookOutboundRules.Deny(
            "facebook", "outbound", "approved", Guid.NewGuid(), false, "hello", hash, FacebookOutboundRules.Hash("other"), false));
        Assert.Null(FacebookOutboundRules.Deny(
            "facebook", "outbound", "approved", Guid.NewGuid(), false, "hello", hash, hash, false));
    }

    [Fact]
    public void Phc_link_stays_locked_until_template_codes_are_approved()
    {
        Assert.False(PhcLinkLock.Enabled);
        var ex = Assert.Throws<InvalidOperationException>(() => PhcLinkLock.EnsureOpen());
        Assert.Contains("khóa", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phc_link_rejects_missing_draft_other_tenant_and_phone_mismatch()
    {
        var sales = Guid.Parse("11111111-1111-1111-1111-111111111107");
        Assert.Equal("Bài PHC không tồn tại.", KitSalesAssessmentLookup.Deny(null, sales, ["PHC-1"], "0901"));
        var draft = Row("draft", null, "PHC-1", "0901");
        Assert.Equal("Bài PHC còn nháp.", KitSalesAssessmentLookup.Deny(draft, sales, ["PHC-1"], "0901"));
        var other = Row("completed", Guid.NewGuid(), "PHC-1", "0901");
        Assert.Equal("Bài PHC thuộc tenant khác.", KitSalesAssessmentLookup.Deny(other, sales, ["PHC-1"], "0901"));
        var ready = Row("completed", null, "PHC-1", "0901");
        Assert.Equal("Product profile chưa có phcTemplateCodes.", KitSalesAssessmentLookup.Deny(ready, sales, [], "0901"));
        Assert.Equal("Template PHC không nằm trong allowlist của sản phẩm.", KitSalesAssessmentLookup.Deny(ready, sales, ["OTHER"], "0901"));
        Assert.Equal("Số điện thoại lead và bài PHC không khớp.", KitSalesAssessmentLookup.Deny(ready, sales, ["PHC-1"], "0902"));
        Assert.Null(KitSalesAssessmentLookup.Deny(ready, sales, ["PHC-1"], null));
        Assert.Null(KitSalesAssessmentLookup.Deny(ready, sales, ["PHC-1"], "0901"));
    }

    private static KitSalesAssessmentLookup.AssessmentLinkRow Row(string status, Guid? tenant, string template, string phone) =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = status,
            TenantId = tenant,
            TemplateCode = template,
            Phone = phone,
        };

    [Fact]
    public void Graph_host_lives_only_in_the_outbound_gate()
    {
        var root = FindRepo();
        var sales = Path.Combine(root, "src", "Packs", "Sales");
        var hits = Directory.GetFiles(sales, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("/me/messages", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();
        Assert.Equal(["FacebookOutboundGate.cs"], hits);
        var chat = File.ReadAllText(Path.Combine(sales, "KitPlatform.Packs.Sales.Infrastructure", "KitSalesChatService.cs"));
        Assert.DoesNotContain("SendTextAsync", chat, StringComparison.Ordinal);
        Assert.DoesNotContain("KitSalesFacebookMessengerClient", chat, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovery_keeps_facts_separate_dedupes_and_does_not_pitch()
    {
        Assert.False(NovixaSourceRegistry.AllowsCollection("facebook"));
        Assert.False(NovixaSourceRegistry.AllowsCollection("google_maps"));
        Assert.True(NovixaSourceRegistry.AllowsCollection("osm_overpass"));

        var json = """
            {"elements":[
              {"type":"node","id":11,"lat":21.59,"lon":105.84,"tags":{"amenity":"pharmacy","name":"Nhà thuốc Lam","phone":"0900000201"}},
              {"type":"node","id":12,"tags":{"amenity":"pharmacy","name":"Nhà thuốc Long Châu"}},
              {"type":"node","id":13,"tags":{"amenity":"pharmacy","name":"Nhà thuốc"}},
              {"type":"node","id":14,"tags":{"amenity":"cafe","name":"Cafe Lam"}}
            ]}
            """;
        Assert.True(OsmOverpassParser.TryParse(json, 15, out var prospects, out var error));
        Assert.Null(error);
        var memory = new DiscoveryMemory(
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { ProspectNames.FoldBusiness("Nhà thuốc Lam") });
        var decisions = prospects.Select(item => ProspectDiscoveryPlanner.Decide(item, memory)).ToList();
        Assert.Equal("skip_duplicate", decisions[0].Action);
        Assert.Equal("chain", decisions[1].Reason);
        Assert.Equal("missing_name", decisions[2].Reason);
        Assert.Equal("not_pharmacy", decisions[3].Reason);
        Assert.All(decisions, decision => Assert.DoesNotContain(decision.Statements, statement => statement.Kind == "hypothesis"));

        var fresh = ProspectDiscoveryPlanner.Decide(prospects[0], new DiscoveryMemory(new HashSet<string>(), new HashSet<string>()));
        Assert.Equal("create", fresh.Action);
        Assert.InRange(fresh.Priority, 1, ProspectDiscoveryPlanner.PriorityCap);
        Assert.Contains(fresh.Statements, statement => statement.Kind == "fact");
        Assert.Contains(fresh.Statements, statement => statement.Kind == "evidence");
        Assert.Contains(fresh.Statements, statement => statement.Kind == "unknown");

        var root = FindRepo();
        var discovery = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Packs",
            "Sales",
            "KitPlatform.Packs.Sales.Infrastructure",
            "KitSalesProspectDiscovery.cs"));
        Assert.DoesNotContain("pack_sales.interaction", discovery, StringComparison.Ordinal);
        Assert.DoesNotContain("graph.facebook.com", discovery, StringComparison.Ordinal);
        Assert.DoesNotContain("gemini", discovery, StringComparison.OrdinalIgnoreCase);
        Assert.All(NovixaSourceRegistry.OverpassInterpreters, endpoint =>
        {
            Assert.Contains("overpass", endpoint, StringComparison.Ordinal);
            Assert.EndsWith("/api/interpreter", endpoint, StringComparison.Ordinal);
        });

        var now = DateTimeOffset.Parse("2026-11-05T05:00:00Z");
        Assert.Equal("rate_limited", DiscoverySchedule.Gate(now.AddHours(-1), now.AddMinutes(-1), now, TimeSpan.FromHours(24)));
        Assert.Equal("backoff", DiscoverySchedule.Gate(now.AddDays(-2), now.AddMinutes(-1), now, TimeSpan.FromHours(24)));
        Assert.Equal("open", DiscoverySchedule.Gate(now.AddDays(-2), now.AddMinutes(-10), now, TimeSpan.FromHours(24)));

        var named = """
            {"elements":[{"type":"node","id":99,"lat":21.7,"lon":105.46,"tags":{"amenity":"pharmacy","name:vi":"Nhà thuốc Cầu"}}]}
            """;
        Assert.True(OsmOverpassParser.TryParse(named, 15, out var namedProspects, out _));
        var first = ProspectDiscoveryPlanner.Decide(namedProspects[0], new DiscoveryMemory(new HashSet<string>(), new HashSet<string>()));
        Assert.Equal("create", first.Action);
        Assert.Contains(first.Statements, statement => statement.Kind == "fact" && statement.Text.Contains("Nhà thuốc Cầu", StringComparison.Ordinal));
        var again = ProspectDiscoveryPlanner.Decide(
            namedProspects[0],
            new DiscoveryMemory(new HashSet<string>(StringComparer.Ordinal) { "node/99" }, new HashSet<string>()));
        Assert.Equal("skip_duplicate", again.Action);
        Assert.Equal(0, again.Priority);
        Assert.Equal(21.7, namedProspects[0].Latitude);
        Assert.Equal(105.46, namedProspects[0].Longitude);
        Assert.Contains(first.Statements, statement => statement.Kind == "fact" && statement.Text.StartsWith("Vị trí:", StringComparison.Ordinal));
        Assert.Contains(first.Statements, statement => statement.Kind == "unknown");
        var note = new PublicProspect("node/7", "Qua đường bên phải", "pharmacy", null, null, null, null, "https://www.openstreetmap.org/node/7", true, false, 21.7, 105.4);
        Assert.Equal("name_not_business", ProspectDiscoveryPlanner.Decide(note, new DiscoveryMemory(new HashSet<string>(), new HashSet<string>())).Reason);
    }

    [Fact]
    public void Overpass_classifies_timeout_http_empty_and_bad_payload_without_using_success_cooldown()
    {
        var many = new StringBuilder("{\"elements\":[");
        for (var i = 0; i < 16; i++)
        {
            if (i > 0)
                many.Append(',');
            many.Append("{\"type\":\"node\",\"id\":").Append(i).Append(",\"lat\":21.5,\"lon\":105.5,\"tags\":{\"amenity\":\"pharmacy\",\"name\":\"Nhà thuốc ").Append(i).Append("\"}}");
        }
        many.Append("]}");
        Assert.Equal("ok", OverpassAttempt.Classify(null, 200, many.ToString(), 15, out var count));
        Assert.Equal(15, count);
        Assert.Equal("empty", OverpassAttempt.Classify(null, 200, "{\"elements\":[]}", 15, out count));
        Assert.Equal(0, count);
        Assert.Equal("overpass_json", OverpassAttempt.Classify(null, 200, "{", 15, out _));
        Assert.Equal("overpass_shape", OverpassAttempt.Classify(null, 200, "{\"remark\":\"no elements\"}", 15, out _));
        Assert.Equal("timeout", OverpassAttempt.Classify("timeout", null, null, 15, out _));
        Assert.Equal("http_429", OverpassAttempt.Classify(null, 429, "busy", 15, out _));
        Assert.Equal("http_500", OverpassAttempt.Classify(null, 500, "", 15, out _));

        var now = DateTimeOffset.Parse("2026-11-05T06:00:00Z");
        Assert.Equal("open", DiscoverySchedule.Gate(null, now.AddMinutes(-6), now, TimeSpan.FromHours(24)));
        Assert.Equal("backoff", DiscoverySchedule.Gate(null, now.AddMinutes(-2), now, TimeSpan.FromHours(24)));
        Assert.Equal("rate_limited", DiscoverySchedule.Gate(now.AddHours(-2), null, now, TimeSpan.FromHours(24)));
    }

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
