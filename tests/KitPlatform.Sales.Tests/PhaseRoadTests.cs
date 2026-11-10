using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;
using Xunit;

namespace KitPlatform.Sales.Tests;

public sealed class PhaseRoadTests
{
    private const string Phc = "https://novixa.vn/vi/health-check/";

    [Fact]
    public void Catalog_replies_pass_the_validator()
    {
        var claims = NovixaClaimCatalog.Active(Phc);
        foreach (var claim in claims)
        {
            var result = SalesOutputValidator.Check(Request(claim.Intent, claim, claims));
            Assert.Equal("pass", result.Decision);
        }
    }

    [Fact]
    public void Changed_price_is_blocked()
    {
        var claims = NovixaClaimCatalog.Active(Phc);
        var price = claims.Single(claim => claim.Intent == "price");
        var forged = price with { Text = price.Text.Replace("299.000", "199.000", StringComparison.Ordinal) };
        var result = SalesOutputValidator.Check(Request("price", forged, claims));
        Assert.Equal("block", result.Decision);
    }

    [Fact]
    public void Opt_out_does_not_send_or_draft()
    {
        var decision = SalesMessengerTurn.Decide(
            new SalesTurnInput("đừng nhắn nữa", "unknown", "x", "novixa.escalate", true,
                SalesAutonomyMode.Autonomous, true, false, true, true, false),
            new SalesValidationResult("pass", []));
        Assert.False(decision.Send);
        Assert.False(decision.Draft);

        var later = SalesMessengerTurn.Decide(
            new SalesTurnInput("bao gia", "price", "gia", "novixa.price", false,
                SalesAutonomyMode.Supervised, true, false, false, false, false),
            new SalesValidationResult("pass", []));
        Assert.False(later.Draft);
        Assert.False(later.Send);
    }

    [Fact]
    public void Autonomous_without_legal_flag_stays_a_draft()
    {
        var claims = NovixaClaimCatalog.Active(Phc);
        var claim = claims.Single(item => item.Intent == "greeting");
        var validation = SalesOutputValidator.Check(Request("greeting", claim, claims));
        var decision = SalesMessengerTurn.Decide(
            new SalesTurnInput("xin chào", "greeting", claim.Text, claim.Id, false,
                SalesAutonomyMode.Autonomous, false, false, false, true, true),
            validation);
        Assert.False(decision.Send);
        Assert.True(decision.Draft);
    }

    [Fact]
    public void Severe_validator_failure_demotes_autonomous()
    {
        Assert.Equal(SalesAutonomyMode.Supervised, SalesAutonomy.Demote("validator_severe", SalesAutonomyMode.Autonomous));
        Assert.Equal(SalesAutonomyMode.Off, SalesAutonomy.Demote("sent_after_opt_out", SalesAutonomyMode.Autonomous));
    }

    [Fact]
    public void Duplicate_directory_names_stay_separate_and_create_no_leads()
    {
        var rows = new List<DirectoryAccount>();
        for (var pair = 0; pair < 4; pair++)
        {
            rows.Add(Row("k" + pair + "a", "Nhà thuốc " + pair));
            rows.Add(Row("k" + pair + "b", "Nhà thuốc " + pair));
        }

        var first = SalesAccountImport.Plan(rows, new HashSet<string>());
        Assert.Equal(0, first.LeadsCreated);
        Assert.Equal(8, first.Items.Count(item => item.Action == "insert_account"));
        Assert.Equal(8, first.Items.Count(item => item.Action == "open_review"));

        var second = SalesAccountImport.Plan(rows, rows.Select(row => row.SourceKey).ToHashSet());
        Assert.Equal(8, second.Items.Count);
        Assert.All(second.Items, item => Assert.Equal("skip", item.Action));

        var duplicated = new List<DirectoryAccount> { Row("same", "Nhà thuốc A"), Row("same", "Nhà thuốc A") };
        var once = SalesAccountImport.Plan(duplicated, new HashSet<string>());
        Assert.Equal(1, once.Items.Count(item => item.Action == "insert_account"));
        Assert.Equal(1, once.Items.Count(item => item.Action == "skip"));
        Assert.Throws<InvalidOperationException>(() => SalesAccountImport.Guard(Enumerable.Range(0, 501).Select(i => Row("k" + i, "N" + i)).ToList()));
    }

    [Fact]
    public void Old_task_endpoint_rejects_review_types()
    {
        Assert.True(SalesReviewRules.OldEndpointAllows("accept_score"));
        Assert.False(SalesReviewRules.OldEndpointAllows("account_review"));
        Assert.True(SalesReviewRules.ReviewEndpointAllows("need_triage"));
        Assert.False(SalesReviewRules.ReviewEndpointAllows("accept_score"));
        Assert.True(SalesReviewRules.ResolutionAllowed("need_triage", "brief_opened"));
        Assert.False(SalesReviewRules.ResolutionAllowed("need_triage", "fact_confirmed"));
        Assert.True(SalesReviewRules.ResolutionAllowed("account_review", "fact_confirmed"));
    }

    [Fact]
    public void Agent_cannot_write_consent_and_a_call_needs_a_result_code()
    {
        Assert.Equal("agent_cannot_grant_consent", SalesConsent.Reject("agent", "form_opt_in", "marketing", true, "form-1"));
        Assert.Null(SalesConsent.Reject("human", "verbal_recorded", "marketing", true, "call-1"));
        Assert.False(SalesCallLog.HasResult(null));
        Assert.True(SalesCallLog.HasResult("booked_phc"));
    }

    [Fact]
    public void Promotion_stops_at_one_step_and_rejects_the_same_person()
    {
        Assert.False(SalesAutonomy.CanPromote(
            SalesAutonomyMode.Off, SalesAutonomyMode.Autonomous, 200, 28, true, 1, 1, 0, 0, true, false,
            "a", "b", true));
        Assert.False(SalesAutonomy.CanPromote(
            SalesAutonomyMode.Off, SalesAutonomyMode.Shadow, 0, 0, true, 1, 1, 0, 0, false, false,
            "same", "same", true));
        var person = Guid.NewGuid();
        Assert.True(SalesGrantIdentity.SamePerson(person.ToString("D"), person));
        Assert.True(SalesGrantIdentity.SamePerson(person.ToString("N"), person));
        Assert.False(SalesGrantIdentity.SamePerson(Guid.NewGuid().ToString("D"), person));
        Assert.True(SalesAutonomy.CanPromote(
            SalesAutonomyMode.Off, SalesAutonomyMode.Shadow, 0, 0, true, 1, 1, 0, 0, false, false,
            "a", "b", true));
    }

    private static DirectoryAccount Row(string key, string name) =>
        new(key, name, "Thái Nguyên", "01", "2026-01-01", "https://example.test/doc", 1);

    private static SalesValidationRequest Request(
        string intent,
        SalesClaim sentence,
        IReadOnlyList<SalesClaim> claims,
        bool autonomous = false) =>
        new(
            "novixa",
            "engage",
            "messenger",
            "pharmacy",
            intent,
            false,
            autonomous,
            true,
            ["novixa.vn", "m.me"],
            claims,
            [new SalesSentence(sentence.Text, sentence.Id, false)]);
}
