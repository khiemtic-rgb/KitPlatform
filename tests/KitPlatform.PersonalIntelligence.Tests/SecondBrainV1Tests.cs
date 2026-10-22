using KitPlatform.Packs.PersonalIntelligence;
using Xunit;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class SecondBrainV1Tests
{
    private static readonly RiskLimits Limits = new(MaxNotional: 1_000m, MaxLeverage: 2m, MinConfidence: 0.55m);
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Account = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Imported_fill_is_an_observation_and_a_duplicate_is_skipped()
    {
        const string csv = """
            external_trade_id,symbol,side,quantity,price,executed_at,fee_amount,fee_currency
            T-1,BTCUSDT,BUY,0.1,50000,2026-10-01T00:00:00Z,1.5,USDT
            T-1,BTCUSDT,BUY,0.1,50000,2026-10-01T00:00:00Z,1.5,USDT
            T-2,BTCUSDT,SELL,0.04,52000,2026-10-02T00:00:00Z,0.8,USDT
            """;

        var first = HistoricalTradeImport.ImportCsv(Account, csv, new HashSet<string>());
        var second = HistoricalTradeImport.ImportCsv(Account, csv, first.Accepted.Select(row => row.ExternalTradeId).ToHashSet());

        Assert.Equal(2, first.Accepted.Count);
        Assert.Equal(["T-1"], first.DuplicateExternalIds);
        Assert.All(first.Accepted, row => Assert.Equal(EpistemicKind.Observation, row.Kind));
        Assert.Empty(second.Accepted);
        Assert.Equal(["T-1", "T-1", "T-2"], second.DuplicateExternalIds);
        Assert.IsNotType<PiDecision>(first.Accepted[0]);
    }

    [Fact]
    public void Risk_engine_vetoes_notional_leverage_confidence_and_live_mode()
    {
        var oversized = RiskEngine.Evaluate(new TradeProposal("BTCUSDT", "BUY", 1m, 5000m, 1m, 0.8m, ExecutionMode.Paper), Limits);
        var leveraged = RiskEngine.Evaluate(new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 5m, 0.8m, ExecutionMode.Paper), Limits);
        var uncertain = RiskEngine.Evaluate(new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 1m, 0.2m, ExecutionMode.Paper), Limits);
        var live = RiskEngine.Evaluate(new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 1m, 0.8m, ExecutionMode.Live), Limits);
        var allowed = RiskEngine.Evaluate(new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 1m, 0.8m, ExecutionMode.Paper), Limits);

        Assert.False(oversized.Allowed);
        Assert.False(leveraged.Allowed);
        Assert.False(uncertain.Allowed);
        Assert.False(live.Allowed);
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public void Approval_is_separate_from_a_passing_recommendation()
    {
        var proposal = new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 1m, 0.8m, ExecutionMode.Paper);
        var recommendation = TradingAgent.Recommend(Tenant, [], proposal, Limits, ["No live tape."]);
        var withoutApproval = DemoPaperExecution.Execute(recommendation.Proposal, recommendation.Verdict, null);
        var withApproval = DemoPaperExecution.Execute(
            recommendation.Proposal,
            recommendation.Verdict,
            new ExecutionApproval(Guid.NewGuid(), "owner"));
        var live = DemoPaperExecution.Execute(
            proposal with { Mode = ExecutionMode.Live },
            RiskEngine.Evaluate(proposal with { Mode = ExecutionMode.Live }, Limits),
            new ExecutionApproval(Guid.NewGuid(), "owner"));

        Assert.Equal(EpistemicKind.Inference, recommendation.Kind);
        Assert.Equal("recommend_only", recommendation.Autonomy);
        Assert.Equal(ExecutionMode.Paper, recommendation.Proposal.Mode);
        Assert.False(withoutApproval.Executed);
        Assert.True(withApproval.Executed);
        Assert.Equal("FIXTURE", withApproval.Fill!.Basis);
        Assert.False(live.Executed);
    }

    [Fact]
    public void Veto_blocks_paper_execution_even_when_approved()
    {
        var proposal = new TradeProposal("BTCUSDT", "BUY", 10m, 500m, 1m, 0.9m, ExecutionMode.Paper);
        var verdict = RiskEngine.Evaluate(proposal, Limits);
        var result = DemoPaperExecution.Execute(proposal, verdict, new ExecutionApproval(Guid.NewGuid(), "owner"));

        Assert.False(verdict.Allowed);
        Assert.False(result.Executed);
        Assert.Equal("refused", result.Status);
    }

    [Fact]
    public void Single_outcome_does_not_approve_a_policy_and_events_stay_in_pi_events()
    {
        var decision = IntelligenceLoop.Decide(Tenant, Guid.NewGuid(), null, "Wait for a pullback", 0.6m, "Smaller drawdown", "Break of the prior low", EpistemicKind.Fact);
        var policy = IntelligenceLoop.ProposePolicyFromSingleOutcome(Tenant, "Never add size after one winning fixture.");
        var evt = IntelligenceLoop.RecordEvent(Tenant, decision.PersonId, "DecisionMade", EpistemicKind.Hypothesis, "journal", decision.DecisionId);

        Assert.Equal(EpistemicKind.Inference, decision.Kind);
        Assert.Equal("proposed", policy.Status);
        Assert.Equal("pack_personal_intelligence.pi_events", evt.Sink);
        Assert.Equal(Tenant, evt.TenantId);
        Assert.Throws<InvalidOperationException>(() => IntelligenceLoop.Decide(Guid.Empty, Guid.NewGuid(), null, "x", 0.5m, null, null, EpistemicKind.Hypothesis));
    }

    [Fact]
    public void Fixture_evaluation_reconstructs_quantity_and_does_not_claim_live_performance()
    {
        var imported = HistoricalTradeImport.ImportCsv(Account, """
            external_trade_id,symbol,side,quantity,price,executed_at
            T-1,BTCUSDT,BUY,0.10,50000,2026-10-01T00:00:00Z
            T-2,BTCUSDT,SELL,0.04,52000,2026-10-02T00:00:00Z
            """, new HashSet<string>());

        var report = FixturePerformanceEvaluation.Evaluate(imported.Accepted);

        Assert.Equal(EvidenceBasis.Fixture, report.Basis);
        Assert.True(report.Consistent);
        Assert.Equal(0.06m, report.ReconstructedNetQuantity);
        Assert.Contains("not evidence of live", report.Limitation, StringComparison.Ordinal);
    }
}
