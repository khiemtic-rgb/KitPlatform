using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PersistenceContractTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Pack_di_resolves_the_journal_and_paper_store()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbConnectionFactory>(new CountingConnectionFactory());
        services.AddSingleton<ITenantContext>(new FixedTenant(TenantA));
        services.AddPersonalIntelligencePack();
        using var provider = services.BuildServiceProvider();

        Assert.IsAssignableFrom<IIntelligenceJournal>(provider.GetRequiredService<IIntelligenceJournal>());
        Assert.IsAssignableFrom<IPaperExecutionStore>(provider.GetRequiredService<IPaperExecutionStore>());
        Assert.IsAssignableFrom<ITradeRepository>(provider.GetRequiredService<ITradeRepository>());
    }

    [Fact]
    public async Task Journal_refuses_another_tenant_before_opening_a_connection()
    {
        var factory = new CountingConnectionFactory();
        var journal = new IntelligenceJournal(factory, new FixedTenant(TenantA));
        var decision = IntelligenceLoop.Decide(TenantB, Guid.NewGuid(), null, "Other tenant", 0.6m, null, null, EpistemicKind.Hypothesis);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => journal.SaveDecisionAsync(decision));

        Assert.Contains("does not match", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public void Event_sql_writes_only_pi_events_and_does_not_update_them()
    {
        var statements = new[]
        {
            IntelligenceSql.InsertEvent,
            IntelligenceSql.InsertContext,
            IntelligenceSql.InsertEvidence,
            IntelligenceSql.InsertDecision,
            IntelligenceSql.InsertAction,
            IntelligenceSql.InsertOutcome,
            IntelligenceSql.InsertReflection,
            IntelligenceSql.InsertExperience,
            IntelligenceSql.InsertPolicy,
            IntelligenceSql.SelectDecision,
            IntelligenceSql.SelectEvents,
            PaperExecutionSql.InsertOrder,
            PaperExecutionSql.InsertExecution,
        };

        Assert.Contains("pack_personal_intelligence.pi_events", IntelligenceSql.InsertEvent, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE", IntelligenceSql.InsertEvent, StringComparison.OrdinalIgnoreCase);
        Assert.All(statements, sql =>
        {
            Assert.Contains("tenant_id", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("platform_events", sql, StringComparison.Ordinal);
        });
        Assert.Equal("pack_personal_intelligence.pi_events", IntelligenceSql.EventSink);
    }

    [Fact]
    public void Paper_plan_keeps_live_and_veto_away_from_execution_rows()
    {
        var limits = new RiskLimits(1_000m, 2m, 0.55m);
        var proposal = new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 1m, 0.8m, ExecutionMode.Paper);
        var veto = PaperPersistencePlanBuilder.From(
            DemoPaperExecution.Execute(
                new TradeProposal("BTCUSDT", "BUY", 10m, 500m, 1m, 0.9m, ExecutionMode.Paper),
                RiskEngine.Evaluate(new TradeProposal("BTCUSDT", "BUY", 10m, 500m, 1m, 0.9m, ExecutionMode.Paper), limits),
                new ExecutionApproval(Guid.NewGuid(), "owner")));
        var live = PaperPersistencePlanBuilder.From(
            DemoPaperExecution.Execute(
                proposal with { Mode = ExecutionMode.Live },
                RiskEngine.Evaluate(proposal with { Mode = ExecutionMode.Live }, limits),
                new ExecutionApproval(Guid.NewGuid(), "owner")));
        var paper = PaperPersistencePlanBuilder.From(
            DemoPaperExecution.Execute(proposal, RiskEngine.Evaluate(proposal, limits), new ExecutionApproval(Guid.NewGuid(), "owner")));

        Assert.False(veto.WriteExecution);
        Assert.Null(veto.ExecutionMode);
        Assert.False(live.WriteExecution);
        Assert.Null(live.ExecutionMode);
        Assert.True(paper.WriteExecution);
        Assert.Equal("PAPER", paper.ExecutionMode);
        Assert.Equal("recommend_only", TradingAgent.Autonomy);
    }

    [Fact]
    public async Task Policy_approval_is_rejected_before_a_connection_opens()
    {
        var factory = new CountingConnectionFactory();
        var journal = new IntelligenceJournal(factory, new FixedTenant(TenantA));
        var policy = new PolicyProposal(Guid.NewGuid(), TenantA, "Size up after one win", "approved");

        await Assert.ThrowsAsync<InvalidOperationException>(() => journal.SavePolicyAsync(policy, Guid.NewGuid()));

        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public void Repeat_import_sql_does_not_rewrite_an_existing_trade()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "Packs",
            "PersonalIntelligence",
            "KitPlatform.Packs.PersonalIntelligence.Infrastructure",
            "Trading",
            "TradeRepository.cs"));

        Assert.Contains("ON CONFLICT (trading_account_id, external_trade_id) DO NOTHING", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DO UPDATE", source, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "migrations", "388_pack_personal_intelligence_loop_integrity_v1.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    private sealed class CountingConnectionFactory : IDbConnectionFactory
    {
        public int Calls { get; private set; }

        public Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("connection opened");
        }
    }
}
