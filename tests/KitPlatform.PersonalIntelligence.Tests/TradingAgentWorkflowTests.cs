using System.Text.Json.Nodes;
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
        var limits = new RiskLimits(1_000m, 1m, 0.5m);

        Assert.Equal(TradingAgent.Autonomy, recommendation.Autonomy);
        Assert.Equal(ExecutionMode.Paper, recommendation.Proposal.Mode);
        Assert.Equal(EpistemicKind.Inference, recommendation.Kind);
        Assert.Equal(evidence.Select(row => row.ExternalTradeId), recommendation.EvidenceIds);
        Assert.Contains("does not create a decision or an order", recommendation.Rationale, StringComparison.Ordinal);

        var unapproved = DemoPaperExecution.Execute(recommendation.Proposal, recommendation.Verdict, null);
        Assert.False(unapproved.Executed);
        var paperVerdict = RiskEngine.Evaluate(recommendation.Proposal, limits);
        var notApproved = DemoPaperExecution.Execute(recommendation.Proposal, paperVerdict, null);
        var recordedAt = ReadCreatedAt(second.SessionKey);
        var receipt = ExecutionReceiptProjector.Project(
            second.SessionKey,
            second.SessionKey,
            intake,
            second.Snapshot,
            recommendation,
            paperVerdict,
            notApproved,
            limits,
            null,
            recordedAt,
            "trading_agent");
        ResearchSession.SaveReceipt(store, second.SessionKey, second.SessionKey, second.Snapshot with { Receipt = receipt });

        var fillsBeforeReplay = CountFills(second.Snapshot.Fills.Select(fill => fill.OperationKey).ToArray());
        var replay = ResearchSession.Run(intake, config, store, store);
        var fillsAfterReplay = CountFills(second.Snapshot.Fills.Select(fill => fill.OperationKey).ToArray());
        var stored = ExecutionReceiptProjector.Read(replay.Snapshot);

        Assert.True(replay.Replayed);
        Assert.Equal(0, replay.NewPaperFills);
        Assert.Equal(fillsBeforeReplay, fillsAfterReplay);
        Assert.Equal(second.SessionKey, replay.SessionKey);
        Assert.True(stored.Recorded);
        Assert.NotNull(stored.Receipt);
        Assert.Equal(second.SessionKey, stored.Receipt.SessionKey);
        Assert.Equal(second.SessionKey, stored.Receipt.PayloadHash);
        Assert.Equal(Source, stored.Receipt.Input.Source);
        Assert.Equal("BTCUSDT", stored.Receipt.Input.Symbol);
        Assert.Equal("1h", stored.Receipt.Input.Timeframe);
        Assert.Equal(30, stored.Receipt.Input.CandleCount);
        Assert.Equal(ResearchSession.Completed, stored.Receipt.Research.Status);
        Assert.Equal(2, stored.Receipt.Research.FillCount);
        Assert.Equal(TradingAgent.Autonomy, stored.Receipt.Recommendation.Autonomy);
        Assert.Equal(nameof(EpistemicKind.Inference), stored.Receipt.Recommendation.Kind);
        Assert.Contains("does not create a decision or an order", stored.Receipt.Recommendation.Rationale, StringComparison.Ordinal);
        Assert.Equal(recommendation.EvidenceIds, stored.Receipt.Recommendation.EvidenceIds);
        Assert.Equal(0.8m, stored.Receipt.Recommendation.Confidence);
        Assert.Equal(nameof(ExecutionMode.Paper), stored.Receipt.Recommendation.Mode);
        Assert.True(stored.Receipt.Risk.Allowed);
        Assert.Empty(stored.Receipt.Risk.Reasons);
        Assert.Equal(1_000m, stored.Receipt.Risk.Limits.MaxNotional);
        Assert.Equal(ExecutionReceiptProjector.ApprovalAbsent, stored.Receipt.Approval.State);
        Assert.Null(stored.Receipt.Approval.ApprovedBy);
        Assert.Equal(ExecutionReceiptProjector.ApprovalRequired, stored.Receipt.Outcome);
        Assert.NotEqual(ExecutionReceiptProjector.PaperExecuted, stored.Receipt.Outcome);
        Assert.Contains("separate approval", stored.Receipt.OutcomeReason, StringComparison.Ordinal);
        Assert.NotEmpty(stored.Receipt.EvidenceRefs);
        Assert.Equal(recordedAt, stored.Receipt.RecordedAt);
        Assert.Equal("trading_agent", stored.Receipt.Provenance.Database);
        Assert.Null(stored.Receipt.Provenance.TenantId);
        Assert.DoesNotContain("ApiSecret", ReadResult(second.SessionKey), StringComparison.Ordinal);

        var live = DemoPaperExecution.Execute(
            recommendation.Proposal with { Mode = ExecutionMode.Live },
            recommendation.Verdict,
            new ExecutionApproval(Guid.NewGuid(), "workflow-poc"));
        Assert.False(live.Executed);
        Assert.Contains("Live orders are not sent.", live.Reasons);
        Assert.Equal(ExecutionReceiptProjector.ApprovalRequired, ExecutionReceiptProjector.Read(ResearchSession.Run(intake, config, store, store).Snapshot).Receipt!.Outcome);
        AssertResearchDatabaseHasNoCanonicalTables();
    }

    private DateTimeOffset ReadCreatedAt(string sessionKey)
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(
            "SELECT created_at FROM research_trading.session_result WHERE session_key = @key",
            conn);
        command.Parameters.AddWithValue("key", sessionKey);
        var value = (DateTime)command.ExecuteScalar()!;
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private string ReadResult(string sessionKey)
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(
            "SELECT result FROM research_trading.session_result WHERE session_key = @key",
            conn);
        command.Parameters.AddWithValue("key", sessionKey);
        return (string)command.ExecuteScalar()!;
    }

    private void AssertResearchDatabaseHasNoCanonicalTables()
    {
        using var conn = new NpgsqlConnection(_connection);
        conn.Open();
        using var command = new NpgsqlCommand(
            """
            SELECT to_regclass('pack_personal_intelligence.pi_decision') IS NULL
               AND to_regclass('pack_personal_intelligence.trading_order') IS NULL
               AND to_regclass('pack_personal_intelligence.pi_knowledge') IS NULL
               AND to_regclass('public.platform_events') IS NULL
            """,
            conn);
        Assert.True((bool)command.ExecuteScalar()!);
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

public sealed class ExecutionReceiptCompatibilityTests
{
    [Fact]
    public void Snapshot_without_a_receipt_reads_as_not_recorded()
    {
        var snapshot = new ResearchSnapshot(
            ResearchSession.Completed,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0m,
            0m,
            null,
            PaperSession.NoClosedPosition,
            null,
            null,
            ResearchSession.DrawdownUnavailable,
            [],
            [],
            []);
        var node = JsonNode.Parse(ResearchSession.Serialize(snapshot))!.AsObject();
        node.Remove("Receipt");
        var read = ResearchSession.Deserialize(node.ToJsonString());
        var view = ExecutionReceiptProjector.Read(read);

        Assert.Null(read.Receipt);
        Assert.False(view.Recorded);
        Assert.Null(view.Receipt);
    }
}
