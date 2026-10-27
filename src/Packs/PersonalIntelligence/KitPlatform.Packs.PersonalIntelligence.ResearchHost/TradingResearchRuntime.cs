using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchHost;

public sealed record TradingResearchRequest(
    AgentRequest Task,
    CandleIntake Intake,
    ResearchConfig Config,
    IPaperOperationLog Paper,
    IResearchLog Research,
    TradeProposal RequestedProposal,
    RiskLimits Limits,
    IReadOnlyList<string> Unknowns,
    DateTimeOffset RecordedAt);

public sealed record TradingResearchResult(AgentTaskReceipt Task, int ResearchCalls, int RecommendCalls, int ExecuteCalls);

/// <summary>
/// Trading tools behind the shared runtime. The runtime decides whether a tool may run.
/// </summary>
public sealed class TradingResearchRuntime
{
    public const string ResearchTool = "research.session";
    public const string RecommendTool = "trading.recommend";
    public const string ExecuteTool = "trading.paper_execute";
    public const string RecordTool = "runtime.record";
    public const string ResearchPermission = "personal_intelligence.research.read";
    public const string RunPermission = "personal_intelligence.trading.run";
    public const string ApprovePermission = "personal_intelligence.trading.approve";

    public static IReadOnlyList<AgentStepSpec> Plan { get; } =
    [
        new("research", ResearchTool, null),
        new("recommend", RecommendTool, "research"),
        new("execute", ExecuteTool, "recommend"),
        new("record", RecordTool, "recommend"),
    ];

    public static readonly DateTimeOffset FixedPaperRecordedAt = DateTimeOffset.Parse("2031-06-02T06:00:00Z");

    public static List<ResearchCandle> FixedPaperCandles()
    {
        var start = DateTimeOffset.Parse("2031-06-01T00:00:00Z");
        var bars = new List<ResearchCandle>();
        for (var hour = 0; hour < 30; hour++)
        {
            var close = hour == 20 ? 200m : hour > 20 ? 1m : 100m;
            var openTime = start.AddHours(hour);
            bars.Add(new ResearchCandle(
                "workflow-poc",
                "BTCUSDT",
                "1h",
                openTime,
                openTime.AddHours(1).AddMilliseconds(-1),
                100m,
                Math.Max(100m, close),
                Math.Min(100m, close),
                close,
                $"workflow-poc:{hour}:{close}"));
        }

        return bars;
    }

    public TradingResearchResult Execute(TradingResearchRequest request, IAgentRunLog log, Action<string, string>? crash = null)
    {
        var calls = new CallCount();
        var state = new RunState();
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(ResearchTool, ResearchPermission, ToolEffect.Paper, false),
            call => Research(request, state, calls));
        registry.Register(
            new ToolContract(RecommendTool, RunPermission, ToolEffect.Read, false),
            call => Recommend(request, state, calls, call));
        registry.Register(
            new ToolContract(ExecuteTool, ApprovePermission, ToolEffect.Paper, true),
            call => Execute(request, state, calls));
        registry.Register(
            new ToolContract(RecordTool, ResearchPermission, ToolEffect.Paper, false),
            call => Record(request, state));
        var receipt = new AgentRuntime(registry, crash).Execute(request.Task, log);
        return new TradingResearchResult(receipt, calls.Research, calls.Recommend, calls.Execute);
    }

    private static ToolResponse Research(TradingResearchRequest request, RunState state, CallCount calls)
    {
        calls.Research++;
        var first = ResearchSession.Run(request.Intake, request.Config, request.Paper, request.Research);
        if (!first.Replayed && request.Research is SqlResearchStore store)
        {
            store.WriteDetails(first.SessionKey, first.Snapshot);
            store.WriteRun(ResearchRunRecord.From(first, request.RecordedAt));
        }

        var replay = ResearchSession.Run(request.Intake, request.Config, request.Paper, request.Research);
        if (replay.NewPaperFills != 0)
            return ToolResponse.Stop(ToolStepStatus.Failed, "Replay added a research fill.");
        state.Report = replay;
        return ToolResponse.Succeeded(
            "Research session stored.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["researchFillCount"] = replay.Snapshot.Fills.Count.ToString(),
                ["evidence"] = replay.SessionKey,
            });
    }

    private static ToolResponse Recommend(TradingResearchRequest request, RunState state, CallCount calls, ToolCall call)
    {
        calls.Recommend++;
        if (state.Report is null)
        {
            if (!call.Inputs.TryGetValue("evidence", out var sessionKey) || string.IsNullOrWhiteSpace(sessionKey))
                return ToolResponse.Uncertain("The research step has no committed session to reread.");
            var stored = request.Research.Find(sessionKey);
            if (stored is null)
                return ToolResponse.Uncertain("The committed research session could not be reread.");
            state.Report = new ResearchReport(
                stored.Status,
                true,
                0,
                0,
                0,
                stored.SessionKey,
                ResearchSession.Deserialize(stored.Result));
        }
        var evidence = state.Report.Snapshot.Fills.Select(fill => new ObservedTrade(
            Guid.Parse("a8888888-8888-8888-8888-888888888888"),
            fill.OperationKey,
            request.RequestedProposal.Symbol,
            fill.Side,
            fill.Quantity,
            fill.ExecutionPrice,
            request.RecordedAt,
            fill.Fee,
            "USDT",
            EpistemicKind.Observation)).ToArray();
        state.Recommendation = TradingAgent.Recommend(
            request.Task.TenantId,
            evidence,
            request.RequestedProposal,
            request.Limits,
            request.Unknowns);
        if (state.Recommendation.Proposal.Mode != ExecutionMode.Paper
            || state.Recommendation.Autonomy != TradingAgent.Autonomy
            || state.Recommendation.Kind != EpistemicKind.Inference)
            return ToolResponse.Stop(ToolStepStatus.Failed, "The recommendation did not stay paper and recommend_only.");
        state.PaperVerdict = RiskEngine.Evaluate(state.Recommendation.Proposal, request.Limits);
        return ToolResponse.Succeeded(
            "Recommendation recorded.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["recommendationExecuted"] = "false",
                ["researchFillCount"] = state.Report.Snapshot.Fills.Count.ToString(),
            });
    }

    private static ToolResponse Execute(TradingResearchRequest request, RunState state, CallCount calls)
    {
        calls.Execute++;
        if (request.RequestedProposal.Mode == ExecutionMode.Live)
            return ToolResponse.Stop(ToolStepStatus.Rejected, "Live execution is outside Agent Runtime V1.");
        if (state.Recommendation is null || state.PaperVerdict is null)
            return ToolResponse.Stop(ToolStepStatus.Failed, "There is no paper recommendation to execute.");
        var paper = DemoPaperExecution.Execute(state.Recommendation.Proposal, state.PaperVerdict, request.Task.Approval);
        if (!paper.Executed || paper.Fill is not { Mode: "PAPER" })
            return ToolResponse.Stop(ToolStepStatus.Failed, paper.Reasons.FirstOrDefault() ?? "Paper execution did not complete.");
        return ToolResponse.Succeeded(
            "Paper execution recorded.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["recommendationExecuted"] = "true",
            });
    }

    private static ToolResponse Record(TradingResearchRequest request, RunState state)
    {
        if (state.Report is null || state.Recommendation is null || state.PaperVerdict is null)
            return ToolResponse.Stop(ToolStepStatus.Failed, "Nothing is ready to record.");
        var paper = DemoPaperExecution.Execute(state.Recommendation.Proposal, state.PaperVerdict, request.Task.Approval);
        var receipt = ExecutionReceiptProjector.Project(
            state.Report.SessionKey,
            state.Report.SessionKey,
            request.Intake,
            state.Report.Snapshot,
            state.Recommendation,
            state.PaperVerdict,
            paper,
            request.Limits,
            request.Task.Approval,
            request.RecordedAt,
            ExecutionReceiptProjector.DatabaseName);
        var trace = new AgentTaskTrace(
            request.Task.TaskKey,
            request.Task.Approval is null ? nameof(AgentTaskStatus.AwaitingApproval) : nameof(AgentTaskStatus.Completed),
            receipt.Outcome,
            state.Report.Snapshot.Fills.Count,
            paper.Executed,
            false);
        ResearchSession.SaveReceipt(
            request.Research,
            state.Report.SessionKey,
            state.Report.SessionKey,
            state.Report.Snapshot with { Receipt = receipt, TaskTrace = trace });
        return ToolResponse.Succeeded(
            "Receipt stored.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["outcome"] = receipt.Outcome,
                ["researchFillCount"] = state.Report.Snapshot.Fills.Count.ToString(),
                ["recommendationExecuted"] = paper.Executed ? "true" : "false",
                ["evidence"] = state.Report.SessionKey,
            });
    }

    private sealed class RunState
    {
        public ResearchReport? Report { get; set; }
        public TradingRecommendation? Recommendation { get; set; }
        public RiskVerdict? PaperVerdict { get; set; }
    }

    private sealed class CallCount
    {
        public int Research { get; set; }
        public int Recommend { get; set; }
        public int Execute { get; set; }
    }
}
