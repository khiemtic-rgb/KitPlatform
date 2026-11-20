using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PaperLedgerAnchorTests
{
    static readonly Guid PersonA = Guid.Parse("33333333-3333-4333-8333-333333333333");
    static readonly Guid AccountA = Guid.Parse("55555555-5555-4555-8555-555555555555");
    static readonly Guid PersonB = Guid.Parse("66666666-6666-4666-8666-666666666666");
    static readonly Guid AccountB = Guid.Parse("77777777-7777-4777-8777-777777777777");

    [Fact]
    public void Kit_mkt_paper_ledger_is_unchanged()
    {
        Assert.Equal("KIT_MKT", PaperLedgerAnchor.KitMkt.TenantCode);
        Assert.Equal("PAPER_LEDGER", PaperLedgerAnchor.KitMkt.ExchangeCode);
        Assert.Equal("KIT_MKT-PAPER-LEDGER", PaperLedgerAnchor.KitMkt.ExternalAccountId);

        var choice = Select("KIT_MKT", Row("KIT_MKT", "PAPER_LEDGER", "KIT_MKT-PAPER-LEDGER", PersonA, AccountA));

        Assert.Equal(PersonA, choice.PersonId);
        Assert.Equal(AccountA, choice.TradingAccountId);
        Assert.Equal(PaperLedgerAnchor.KitMkt, choice.Anchor);
    }

    [Fact]
    public void Second_brain_paper_fixture_is_accepted()
    {
        var choice = Select(
            "SECOND_BRAIN",
            Row("SECOND_BRAIN", "PAPER", "paper-fixture", PersonA, AccountA),
            Row("SECOND_BRAIN", "BYBIT", "bybit-read", PersonB, AccountB));

        Assert.Equal(PersonA, choice.PersonId);
        Assert.Equal(AccountA, choice.TradingAccountId);
        Assert.Equal(PaperLedgerAnchor.SecondBrain, choice.Anchor);
    }

    [Theory]
    [InlineData("SECOND_BRAIN", "PAPER_LEDGER", "KIT_MKT-PAPER-LEDGER")]
    [InlineData("SECOND_BRAIN", "PAPER", "KIT_MKT-PAPER-LEDGER")]
    [InlineData("SECOND_BRAIN", "PAPER_LEDGER", "paper-fixture")]
    [InlineData("KIT_MKT", "PAPER", "paper-fixture")]
    [InlineData("KIT_MKT", "PAPER_LEDGER", "paper-fixture")]
    [InlineData("KIT_MKT", "PAPER", "KIT_MKT-PAPER-LEDGER")]
    public void Mismatched_tenant_account_or_ledger_is_rejected(string tenant, string exchange, string account)
    {
        var error = Reject(tenant, Row(tenant, exchange, account, PersonA, AccountA));
        Assert.Equal(PaperLedgerSelection.MismatchMessage, error);
    }

    [Fact]
    public void A_row_from_another_tenant_is_rejected()
    {
        var error = Reject(
            "SECOND_BRAIN",
            Row("KIT_MKT", "PAPER_LEDGER", "KIT_MKT-PAPER-LEDGER", PersonA, AccountA),
            Row("SECOND_BRAIN", "PAPER", "paper-fixture", PersonB, AccountB));
        Assert.Equal(PaperLedgerSelection.MismatchMessage, error);
    }

    [Fact]
    public void Two_matching_ledgers_are_rejected()
    {
        var error = Reject(
            "KIT_MKT",
            Row("KIT_MKT", "PAPER_LEDGER", "KIT_MKT-PAPER-LEDGER", PersonA, AccountA),
            Row("KIT_MKT", "PAPER_LEDGER", "KIT_MKT-PAPER-LEDGER", PersonB, AccountB));
        Assert.Equal(PaperLedgerSelection.MismatchMessage, error);
    }

    [Fact]
    public void Unknown_tenant_and_missing_ledger_are_rejected()
    {
        Assert.Equal(PaperLedgerSelection.MissingAnchorMessage, Reject("DEMO_PHARMACY"));
        Assert.Equal(
            PaperLedgerSelection.MissingAnchorMessage,
            Reject("SECOND_BRAIN", Row("SECOND_BRAIN", "BYBIT", "bybit-read", PersonA, AccountA)));
    }

    [Theory]
    [InlineData("personal_intelligence.research.read")]
    [InlineData("personal_intelligence.exchange.read")]
    [InlineData("personal_intelligence.trading.approve")]
    [InlineData("")]
    public void Paper_evaluation_rejects_callers_without_trading_run(string permission)
    {
        var permissions = string.IsNullOrEmpty(permission) ? Array.Empty<string>() : new[] { permission };
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation(PaperBinding()),
            PlatformCapabilityCatalog.All,
            Caller(false, permissions));
        var step = plan.Steps.Single();
        Assert.Equal(IntentDisposition.PermissionDenied, step.Disposition);
        Assert.False(step.RunHandler);
        Assert.Equal(PlatformCapabilityCatalog.TradingPaperEvaluate, step.CapabilityId);
    }

    [Fact]
    public void Trading_run_can_evaluate_paper_for_either_mapped_tenant_without_switching_ledger()
    {
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation(PaperBinding()),
            PlatformCapabilityCatalog.All,
            Caller(false, PersonalIntelligencePolicies.Permission));
        Assert.Equal(IntentDisposition.Evaluate, plan.Steps.Single().Disposition);
        Assert.True(plan.Steps.Single().RunHandler);
        Assert.Equal("KIT_MKT-PAPER-LEDGER", PaperLedgerAnchor.KitMkt.ExternalAccountId);
        Assert.Equal("paper-fixture", PaperLedgerAnchor.SecondBrain.ExternalAccountId);
    }

    static PaperLedgerChoice Select(string tenant, params PaperLedgerCandidate[] rows)
    {
        var selected = PaperLedgerSelection.TrySelect(tenant, rows, out var choice, out var error);
        Assert.True(selected, error);
        return choice;
    }

    static string Reject(string tenant, params PaperLedgerCandidate[] rows)
    {
        var selected = PaperLedgerSelection.TrySelect(tenant, rows, out _, out var error);
        Assert.False(selected);
        return error;
    }

    static PaperLedgerCandidate Row(string tenant, string exchange, string account, Guid personId, Guid accountId) =>
        new(tenant, exchange, account, personId, accountId);

    static BusinessCaller Caller(bool admin, params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), admin, permissions.ToHashSet(StringComparer.Ordinal));

    static IntentInterpretation Interpretation(IntentBinding binding) =>
        new("paper", ["done"], [], [], [], [binding], false, null, null, null, null);

    static IntentBinding PaperBinding() =>
        new(PlatformCapabilityCatalog.TradingPaperEvaluate, new Dictionary<string, string>
        {
            ["symbol"] = "BTCUSDT",
            ["side"] = "BUY",
            ["quantity"] = "0.01",
            ["price"] = "100",
            ["mode"] = "paper",
            ["maxNotional"] = "1000",
            ["maxLeverage"] = "2",
            ["minConfidence"] = "0.5",
            ["leverage"] = "1",
            ["confidence"] = "0.8",
        }, "evaluate");
}
