using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class AgentRuntimeTests
{
    private static readonly Guid Tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    [Fact]
    public void Invalid_transition_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => AgentTaskTransitions.Move(AgentTaskStatus.Received, AgentTaskStatus.Completed));
        Assert.Throws<InvalidOperationException>(() => AgentTaskTransitions.Move(AgentTaskStatus.Completed, AgentTaskStatus.Running));
        Assert.Throws<InvalidOperationException>(() => AgentTaskTransitions.Move(AgentTaskStatus.AwaitingApproval, AgentTaskStatus.Completed));
    }

    [Fact]
    public void Missing_permission_does_not_call_the_tool()
    {
        var calls = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var receipt = new AgentRuntime(registry).Execute(Request([new AgentStepSpec("one", "read", null)], ["personal_intelligence.trading.run"]), new MemoryAgentRunLog());

        Assert.Equal(0, calls);
        Assert.Equal(AgentTaskStatus.Failed, receipt.Status);
        Assert.Equal(ToolStepStatus.Denied, receipt.Steps[0].Status);
        Assert.False(receipt.RecommendationExecuted);
    }

    [Fact]
    public void Missing_approval_does_not_call_the_tool()
    {
        var calls = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("paper", "personal_intelligence.trading.approve", ToolEffect.Paper, true), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true", ["recommendationExecuted"] = "true" });
        });
        var receipt = new AgentRuntime(registry).Execute(
            Request([new AgentStepSpec("one", "paper", null)], ["personal_intelligence.trading.approve"]),
            new MemoryAgentRunLog());

        Assert.Equal(0, calls);
        Assert.Equal(AgentTaskStatus.AwaitingApproval, receipt.Status);
        Assert.Equal(ToolStepStatus.Blocked, receipt.Steps[0].Status);
        Assert.False(receipt.RecommendationExecuted);
        Assert.False(receipt.LiveAccepted);
    }

    [Fact]
    public void Live_tool_is_rejected_even_with_approval()
    {
        var calls = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("live", "personal_intelligence.trading.approve", ToolEffect.Live, true), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("sent", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var receipt = new AgentRuntime(registry).Execute(
            Request(
                [new AgentStepSpec("one", "live", null)],
                ["personal_intelligence.trading.approve"],
                new ExecutionApproval(Guid.NewGuid(), "operator")),
            new MemoryAgentRunLog());

        Assert.Equal(0, calls);
        Assert.Equal(AgentTaskStatus.Failed, receipt.Status);
        Assert.Equal(ToolStepStatus.Rejected, receipt.Steps[0].Status);
        Assert.False(receipt.LiveAccepted);
    }

    [Fact]
    public void Repeated_task_does_not_call_the_tool_again()
    {
        var calls = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var log = new MemoryAgentRunLog();
        var request = Request([new AgentStepSpec("one", "read", null)], ["personal_intelligence.research.read"]);
        var first = new AgentRuntime(registry).Execute(request, log);
        var second = new AgentRuntime(registry).Execute(request, log);

        Assert.Equal(1, calls);
        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.Status, second.Status);
        Assert.Throws<AgentRunConflictException>(() => new AgentRuntime(registry).Execute(request with { PayloadHash = "other-payload" }, log));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Unknown_tool_effect_is_not_completed_or_retried()
    {
        var calls = 0;
        var later = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("effect", "personal_intelligence.trading.run", ToolEffect.Paper, false), _ =>
        {
            calls++;
            return ToolResponse.Uncertain("The paper effect was not confirmed.");
        });
        registry.Register(new ToolContract("next", "personal_intelligence.trading.run", ToolEffect.Read, false), _ =>
        {
            later++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var log = new MemoryAgentRunLog();
        var request = Request(
            [new AgentStepSpec("one", "effect", null), new AgentStepSpec("two", "next", "one")],
            ["personal_intelligence.trading.run"]);
        var first = new AgentRuntime(registry).Execute(request, log);
        var second = new AgentRuntime(registry).Execute(request, log);

        Assert.Equal(AgentTaskStatus.ReconciliationRequired, first.Status);
        Assert.NotEqual(AgentTaskStatus.Completed, first.Status);
        Assert.Single(first.Steps);
        Assert.Equal(ToolStepStatus.Uncertain, first.Steps[0].Status);
        Assert.Contains("not confirmed", first.Error, StringComparison.Ordinal);
        Assert.True(second.Replayed);
        Assert.Equal(1, calls);
        Assert.Equal(0, later);
    }

    [Fact]
    public void Tool_failure_stops_later_steps_and_keeps_the_error()
    {
        var later = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("first", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
            ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" }));
        registry.Register(new ToolContract("second", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
            throw new InvalidOperationException("boom"));
        registry.Register(new ToolContract("third", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            later++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var receipt = new AgentRuntime(registry).Execute(
            Request(
                [
                    new AgentStepSpec("one", "first", null),
                    new AgentStepSpec("two", "second", "one"),
                    new AgentStepSpec("three", "third", "two"),
                ],
                ["personal_intelligence.research.read"]),
            new MemoryAgentRunLog());

        Assert.Equal(AgentTaskStatus.Failed, receipt.Status);
        Assert.Equal(2, receipt.Steps.Count);
        Assert.Contains("boom", receipt.Error, StringComparison.Ordinal);
        Assert.Equal(0, later);
    }

    [Fact]
    public void Tenant_switch_and_mismatched_call_are_denied()
    {
        var calls = 0;
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var request = Request([new AgentStepSpec("one", "read", null)], ["personal_intelligence.research.read"]);
        var switched = new AgentRuntime(registry).Execute(request with { RequestedTenantId = Guid.NewGuid() }, new MemoryAgentRunLog());
        var denied = new ToolDispatcher().Dispatch(
            registry,
            new ToolCall("one", Guid.NewGuid(), new Dictionary<string, string>()),
            request);

        Assert.Equal(AgentTaskStatus.Failed, switched.Status);
        Assert.Empty(switched.Steps);
        Assert.Equal(ToolStepStatus.Denied, denied.Status);
        Assert.Equal(0, calls);
    }

    private static AgentRequest Request(IReadOnlyList<AgentStepSpec> plan, IReadOnlyList<string> permissions, ExecutionApproval? approval = null)
        => new(
            "task-1",
            "payload-1",
            Tenant,
            "Record a paper recommendation.",
            "The receipt names the outcome and does not claim a live order.",
            permissions,
            plan,
            approval);
}

public sealed class AgentRuntimeWorkflowTests : IAsyncLifetime
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
    public void Runtime_records_an_unapproved_paper_recommendation_without_a_second_fill()
    {
        var intake = ResearchCandles.Classify(Series(), Series().Last().CloseTime, TimeSpan.FromDays(30));
        var config = new ResearchConfig(
            0.001m,
            0.8m,
            10_000m,
            new PaperSessionConfig(10m, 0.001m, 0m, 1m, new RiskLimits(1_000m, 1m, 0.5m), 5_000m, false));
        using var store = new SqlResearchStore(_connection);
        var limits = new RiskLimits(1_000m, 1m, 0.5m);
        var task = new AgentRequest(
            "runtime-workflow-poc",
            "runtime-workflow-poc",
            Guid.Parse("a1111111-1111-1111-1111-111111111111"),
            "Recommend from the fixed research session.",
            "The task receipt shows approval_required and the research fill count stays 2.",
            [TradingResearchRuntime.ResearchPermission, TradingResearchRuntime.RunPermission, TradingResearchRuntime.ApprovePermission],
            TradingResearchRuntime.Plan,
            null);
        var request = new TradingResearchRequest(
            task,
            intake,
            config,
            store,
            store,
            new TradeProposal("BTCUSDT", "BUY", 0.001m, 100m, 1m, 0.8m, ExecutionMode.Live),
            limits,
            ["The next bar is not known."],
            DateTimeOffset.Parse("2031-06-02T06:00:00Z"));
        var log = new MemoryAgentRunLog();
        var runtime = new TradingResearchRuntime();

        var first = runtime.Execute(request, log);
        var fills = first.Task.ResearchFillCount;
        var second = runtime.Execute(request, log);
        var replay = ResearchSession.Run(intake, config, store, store);

        Assert.Equal(AgentTaskStatus.AwaitingApproval, first.Task.Status);
        Assert.Equal(1, first.ResearchCalls);
        Assert.Equal(1, first.RecommendCalls);
        Assert.Equal(0, first.ExecuteCalls);
        Assert.Equal(2, fills);
        Assert.False(first.Task.RecommendationExecuted);
        Assert.False(first.Task.LiveAccepted);
        Assert.Equal(ExecutionReceiptProjector.ApprovalRequired, first.Task.Outcome);
        Assert.Contains(first.Task.Steps, step => step.ToolName == TradingResearchRuntime.ResearchTool && step.Status == ToolStepStatus.Succeeded);
        Assert.Contains(first.Task.Steps, step => step.ToolName == TradingResearchRuntime.ExecuteTool && step.Status == ToolStepStatus.Blocked);
        Assert.NotEqual(AgentTaskStatus.Completed, first.Task.Status);
        Assert.True(second.Task.Replayed);
        Assert.Equal(0, second.ResearchCalls);
        Assert.Equal(0, second.ExecuteCalls);
        Assert.Equal(first.Task.Status, second.Task.Status);
        Assert.True(replay.Replayed);
        Assert.Equal(0, replay.NewPaperFills);
        Assert.Equal(2, replay.Snapshot.Fills.Count);
        Assert.Equal(ExecutionReceiptProjector.ApprovalRequired, replay.Snapshot.Receipt!.Outcome);
        Assert.Equal(nameof(ExecutionMode.Paper), replay.Snapshot.Receipt.Recommendation.Mode);
        Assert.Null(replay.Snapshot.Receipt.Provenance.TenantId);
        Assert.NotNull(replay.Snapshot.TaskTrace);
        Assert.Equal(nameof(AgentTaskStatus.AwaitingApproval), replay.Snapshot.TaskTrace.Status);
        Assert.False(replay.Snapshot.TaskTrace.RecommendationExecuted);
        Assert.False(replay.Snapshot.TaskTrace.LiveAccepted);
        Assert.Equal(2, replay.Snapshot.TaskTrace.ResearchFillCount);
    }

    [Fact]
    public void Approved_live_proposal_still_does_not_execute()
    {
        var intake = ResearchCandles.Classify(Series(), Series().Last().CloseTime, TimeSpan.FromDays(30));
        var config = new ResearchConfig(
            0.001m,
            0.8m,
            10_000m,
            new PaperSessionConfig(10m, 0.001m, 0m, 1m, new RiskLimits(1_000m, 1m, 0.5m), 5_000m, false));
        var paper = new MemoryPaperOperationLog();
        var research = new MemoryResearchLog();
        var request = new TradingResearchRequest(
            new AgentRequest(
                "runtime-live-block",
                "runtime-live-block",
                Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                "Do not send the live proposal.",
                "The execute tool rejects live mode.",
                [TradingResearchRuntime.ResearchPermission, TradingResearchRuntime.RunPermission, TradingResearchRuntime.ApprovePermission],
                TradingResearchRuntime.Plan,
                new ExecutionApproval(Guid.NewGuid(), "operator")),
            intake,
            config,
            paper,
            research,
            new TradeProposal("BTCUSDT", "BUY", 0.001m, 100m, 1m, 0.8m, ExecutionMode.Live),
            new RiskLimits(1_000m, 1m, 0.5m),
            ["The next bar is not known."],
            DateTimeOffset.Parse("2031-06-02T06:00:00Z"));

        var result = new TradingResearchRuntime().Execute(request, new MemoryAgentRunLog());

        Assert.Equal(1, result.ExecuteCalls);
        Assert.Equal(AgentTaskStatus.Failed, result.Task.Status);
        Assert.Equal(ToolStepStatus.Rejected, result.Task.Steps.Single(step => step.ToolName == TradingResearchRuntime.ExecuteTool).Status);
        Assert.False(result.Task.RecommendationExecuted);
        Assert.False(result.Task.LiveAccepted);
        Assert.DoesNotContain(result.Task.Steps, step => step.ToolName == TradingResearchRuntime.RecordTool);
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
