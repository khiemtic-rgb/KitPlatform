using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

/// <summary>
/// One reproducible paper workflow: fixed candles, research session, stored evidence, recommend_only.
/// The research store still accepts only database trading_agent.
/// </summary>
public sealed class TradingAgentWorkflowTests : IAsyncLifetime
{
    private const string Source = "workflow-poc";
    private string _connection = "";

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("KIT_PI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("KIT_PI_TEST_CONNECTION is required. PostgreSQL integration is NOT VERIFIED without the isolated database.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database is "kitplatform" or "kitplatform_test" or "pharmacore" or "postgres" or "trading_research" or "novixa_prod")
            throw new InvalidOperationException("Refusing a shared or operating database.");
        if (builder.Port == 5432)
            throw new InvalidOperationException("Refusing the shared PostgreSQL port.");
        builder.Database = "trading_agent";

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = (string)(await new NpgsqlCommand("SELECT current_database()", conn).ExecuteScalarAsync())!;
        var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", conn).ExecuteScalarAsync());
        if (!string.Equals(database, "trading_agent", StringComparison.Ordinal) || port != 5433)
            throw new InvalidOperationException("Connected database does not match the isolated trading_agent database.");
        var forbidden = Convert.ToInt32(await new NpgsqlCommand(
            "SELECT COUNT(*) FROM pg_database WHERE datname IN ('trading_research', 'novixa_prod')", conn).ExecuteScalarAsync());
        if (forbidden != 0)
            throw new InvalidOperationException("Refusing a cluster that contains an operating database.");
        _connection = builder.ConnectionString;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Fixed_candles_are_stored_replayed_and_recommended_without_an_order()
    {
        var intake = ResearchCandles.Classify(Series(), Series().Last().CloseTime, TimeSpan.FromDays(30));
        var config = new ResearchConfig(
            0.001m,
            0.8m,
            10_000m,
            new PaperSessionConfig(10m, 0.001m, 0m, 1m, new RiskLimits(1_000m, 1m, 0.5m), 5_000m, false));
        using var store = new SqlResearchStore(_connection);

        var first = ResearchSession.Run(intake, config, store, store);
        if (!first.Replayed)
        {
            Assert.Equal(ResearchSession.Completed, first.Status);
            Assert.Equal(2, first.NewPaperFills);
            store.WriteDetails(first.SessionKey, first.Snapshot);
            store.WriteRun(ResearchRunRecord.From(first, DateTimeOffset.Parse("2031-06-02T06:00:00Z")));
        }

        var before = CountFills(first.Snapshot.Fills.Select(fill => fill.OperationKey).ToArray());
        var second = ResearchSession.Run(intake, config, store, store);
        var after = CountFills(first.Snapshot.Fills.Select(fill => fill.OperationKey).ToArray());

        Assert.True(second.Replayed);
        Assert.Equal(0, second.NewPaperFills);
        Assert.Equal(before, after);
        Assert.Equal(2, second.Snapshot.Fills.Count);
        Assert.Contains(second.Snapshot.Fills, fill => fill.Side == "BUY");
        Assert.Contains(second.Snapshot.Fills, fill => fill.Side == "SELL");

        var evidence = second.Snapshot.Fills.Select(fill => new ObservedTrade(
            Guid.Parse("a8888888-8888-8888-8888-888888888888"),
            fill.OperationKey,
            "BTCUSDT",
            fill.Side,
            fill.Quantity,
            fill.ExecutionPrice,
            DateTimeOffset.Parse("2031-06-02T00:00:00Z"),
            fill.Fee,
            "USDT",
            EpistemicKind.Observation)).ToArray();
        var sample = second.Snapshot.Fills[0];
        var recommendation = TradingAgent.Recommend(
            Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            evidence,
            new TradeProposal("BTCUSDT", sample.Side, sample.Quantity, sample.ExecutionPrice, 1m, 0.8m, ExecutionMode.Live),
            new RiskLimits(1_000m, 1m, 0.5m),
            ["The next bar is not known."]);

        Assert.Equal(TradingAgent.Autonomy, recommendation.Autonomy);
        Assert.Equal(ExecutionMode.Paper, recommendation.Proposal.Mode);
        Assert.Equal(EpistemicKind.Inference, recommendation.Kind);
        Assert.Equal(evidence.Select(row => row.ExternalTradeId), recommendation.EvidenceIds);
        Assert.Contains("does not create a decision or an order", recommendation.Rationale, StringComparison.Ordinal);

        var unapproved = DemoPaperExecution.Execute(recommendation.Proposal, recommendation.Verdict, null);
        Assert.False(unapproved.Executed);
        var live = DemoPaperExecution.Execute(
            recommendation.Proposal with { Mode = ExecutionMode.Live },
            recommendation.Verdict,
            new ExecutionApproval(Guid.NewGuid(), "workflow-poc"));
        Assert.False(live.Executed);
        Assert.Contains("Live orders are not sent.", live.Reasons);
    }

    private int CountFills(string[] keys)
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM research_trading.paper_fill WHERE operation_key = ANY(@keys)",
            conn);
        command.Parameters.AddWithValue("keys", keys);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static List<ResearchCandle> Series()
    {
        var start = DateTimeOffset.Parse("2031-06-01T00:00:00Z");
        var bars = new List<ResearchCandle>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? 200m : hour > 20 ? 1m : 100m;
            var openTime = start.AddHours(hour);
            bars.Add(new ResearchCandle(
                Source,
                "BTCUSDT",
                "1h",
                openTime,
                openTime.AddHours(1).AddMilliseconds(-1),
                100m,
                Math.Max(100m, close),
                Math.Min(100m, close),
                close,
                $"{Source}:{hour}:{close}"));
        }

        return bars;
    }
}
