using System.Globalization;
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
    public const string AssessTool = "trading.assess";
    public const string ExecuteTool = "trading.paper_execute";
    public const string RecordTool = "runtime.record";
    public const string ResearchPermission = "personal_intelligence.research.read";
    public const string RunPermission = "personal_intelligence.trading.run";
    public const string ApprovePermission = "personal_intelligence.trading.approve";
    public const string Scope = "trading-paper; source workflow-poc; no live orders";

    public static IReadOnlyList<AgentStepSpec> Plan { get; } = PlanFor(
        "Record a paper recommendation.",
        "The receipt names the outcome and does not claim a live order.");

    public static IReadOnlyList<AgentStepSpec> PlanFor(string objective, string doneWhen)
    {
        var goal = OneLine(objective, "the trading objective");
        var criterion = OneLine(doneWhen, "the completion condition");
        return
        [
            new("research", ResearchTool, null, $"Collect the permitted research session for: {goal}"),
            new("recommend", RecommendTool, "research", "Infer a paper recommendation from that session. A recommendation is not an execution."),
            new("assess", AssessTool, "recommend", $"Check whether this completion condition can be verified: {criterion}"),
            new("execute", ExecuteTool, "assess", "Record one task-scoped paper operation only after approval."),
            new("record", RecordTool, "recommend", "Store the research snapshot as historical evidence."),
        ];
    }

    public static bool CannotProve(string text)
    {
        var value = text.ToLowerInvariant();
        return value.Contains("profit", StringComparison.Ordinal)
            || value.Contains("lợi nhuận", StringComparison.Ordinal)
            || value.Contains("win rate", StringComparison.Ordinal)
            || value.Contains("percent", StringComparison.Ordinal)
            || value.Contains('%');
    }

    public static readonly DateTimeOffset FixedPaperRecordedAt = DateTimeOffset.Parse("2031-06-02T06:00:00Z");

    public static string TaskPaperOperationKey(Guid tenantId, string taskKey, Guid decisionId)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(taskKey) || decisionId == Guid.Empty)
            throw new InvalidOperationException("A paper operation requires a task and an approval decision.");
        return $"task:{tenantId:N}:{taskKey}:{decisionId:N}";
    }

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
        var receipt = new AgentRuntime(Registry(request, state, calls), crash).Execute(request.Task, log);
        return new TradingResearchResult(receipt, calls.Research, calls.Recommend, calls.Execute);
    }

    public TradingResearchResult ContinueAfterApproval(TradingResearchRequest request, IAgentRunLog log, DateTimeOffset approvedAt)
    {
        var calls = new CallCount();
        var state = new RunState();
        var receipt = new AgentRuntime(Registry(request, state, calls)).ContinueApproved(request.Task, log, approvedAt);
        return new TradingResearchResult(receipt, calls.Research, calls.Recommend, calls.Execute);
    }

    private static ToolRegistry Registry(TradingResearchRequest request, RunState state, CallCount calls)
    {
        var registry = new ToolRegistry();
        registry.Register(
            new ToolContract(ResearchTool, ResearchPermission, ToolEffect.Paper, false),
            call => Research(request, state, calls));
        registry.Register(
            new ToolContract(RecommendTool, RunPermission, ToolEffect.Read, false),
            call => Recommend(request, state, calls, call));
        registry.Register(
            new ToolContract(AssessTool, RunPermission, ToolEffect.Read, false),
            call => Assess(request, state, call));
        registry.Register(
            new ToolContract(ExecuteTool, ApprovePermission, ToolEffect.Paper, true),
            call => Execute(request, state, calls, call));
        registry.Register(
            new ToolContract(RecordTool, ResearchPermission, ToolEffect.Paper, false),
            call => Record(request, state));
        return registry;
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
        if (!TryLoadRecommendation(request, state, call, out var failure) || state.Report is null)
            return failure;
        return ToolResponse.Succeeded(
            "Recommendation recorded.",
            new Dictionary<string, string>
            {
                ["verified"] = "true",
                ["recommendationExecuted"] = "false",
                ["researchFillCount"] = state.Report.Snapshot.Fills.Count.ToString(),
                ["evidence"] = state.Report.SessionKey,
            });
    }

    private static ToolResponse Assess(TradingResearchRequest request, RunState state, ToolCall call)
    {
        if (!TryLoadRecommendation(request, state, call, out var failure) || state.Report is null || state.Recommendation is null)
            return failure;
        var outputs = EvidenceOutputs(request, state);
        var missing = new List<string>();
        if (CannotProve(request.Task.Objective))
            missing.Add(request.Task.Objective);
        if (CannotProve(request.Task.DoneWhen) && !missing.Contains(request.Task.DoneWhen, StringComparer.Ordinal))
            missing.Add(request.Task.DoneWhen);
        if (state.Report.Snapshot.Fills.Count == 0 || string.IsNullOrWhiteSpace(state.Report.SessionKey))
            missing.Add("research fills from the permitted session");
        if (missing.Count > 0)
        {
            outputs["goalMet"] = "false";
            outputs["goalReason"] = "The completion condition asks for a result the permitted evidence cannot prove.";
            outputs["missing"] = string.Join('\n', missing);
            return ToolResponse.Stop(ToolStepStatus.Failed, outputs["goalReason"], outputs);
        }

        outputs["verified"] = "true";
        outputs["goalMet"] = "false";
        outputs["goalReason"] = "Research evidence is present. The goal is met only when an approved paper operation matches this task.";
        outputs["evidence"] = state.Report.SessionKey;
        outputs["researchFillCount"] = state.Report.Snapshot.Fills.Count.ToString(CultureInfo.InvariantCulture);
        return ToolResponse.Succeeded("The completion condition can be checked from the research session.", outputs);
    }

    private static ToolResponse Execute(TradingResearchRequest request, RunState state, CallCount calls, ToolCall call)
    {
        calls.Execute++;
        if (request.RequestedProposal.Mode == ExecutionMode.Live)
        {
            return ToolResponse.Stop(
                ToolStepStatus.Rejected,
                "Live execution is outside Agent Runtime V1.",
                new Dictionary<string, string>
                {
                    ["goalMet"] = "false",
                    ["goalReason"] = "Live execution was rejected. No live order was sent.",
                    ["scope"] = Scope,
                    ["limit"] = "Live orders are not sent.",
                });
        }
        if ((state.Recommendation is null || state.PaperVerdict is null) && !TryLoadRecommendation(request, state, call, out _))
            return ToolResponse.Stop(ToolStepStatus.Failed, "There is no paper recommendation to execute.");
        if (state.Recommendation is null || state.PaperVerdict is null)
            return ToolResponse.Stop(ToolStepStatus.Failed, "There is no paper recommendation to execute.");
        var paper = DemoPaperExecution.Execute(state.Recommendation.Proposal, state.PaperVerdict, request.Task.Approval);
        if (!paper.Executed || paper.Fill is not { Mode: "PAPER" })
            return ToolResponse.Stop(ToolStepStatus.Failed, paper.Reasons.FirstOrDefault() ?? "Paper execution did not complete.");
        return StorePaperOperation(request, state, paper.Fill);
    }

    private static ToolResponse StorePaperOperation(TradingResearchRequest request, RunState state, PaperFill fill)
    {
        var approval = request.Task.Approval;
        if (approval is null || approval.DecisionId == Guid.Empty)
            return ToolResponse.Stop(ToolStepStatus.Failed, "Paper execution requires a separate approval.");
        var key = TaskPaperOperationKey(request.Task.TenantId, request.Task.TaskKey, approval.DecisionId);
        var sessionKey = state.Report?.SessionKey ?? "";
        var result = string.Join('\n',
            request.Task.TaskKey,
            request.Task.TenantId.ToString("D"),
            approval.DecisionId.ToString("D"),
            sessionKey,
            "PAPER",
            fill.Symbol,
            fill.Side,
            fill.Quantity.ToString(CultureInfo.InvariantCulture),
            fill.Price.ToString(CultureInfo.InvariantCulture));
        request.Paper.TryAdd(new PaperOperationReceipt(key, request.Task.PayloadHash, "paper_filled", result));
        var stored = request.Paper.Find(key);
        if (stored is null
            || !string.Equals(stored.PayloadHash, request.Task.PayloadHash, StringComparison.Ordinal)
            || !string.Equals(stored.Status, "paper_filled", StringComparison.Ordinal))
            return ToolResponse.Stop(ToolStepStatus.Failed, "The paper operation was not stored.");

        var outputs = new Dictionary<string, string>
        {
            ["verified"] = "true",
            ["recommendationExecuted"] = "true",
            ["outcome"] = "paper_executed",
            ["paperOperationKey"] = stored.OperationKey,
            ["goalMet"] = "true",
            ["goalReason"] = "An approved paper operation matches this task, and no live order was sent.",
            ["scope"] = Scope,
            ["fact"] = $"Paper operation {stored.OperationKey} is paper_filled for this task hash.",
            ["limit"] = "Live orders are not sent.",
        };
        if (!string.IsNullOrWhiteSpace(sessionKey))
            outputs["evidence"] = sessionKey;
        return ToolResponse.Succeeded("Paper execution recorded.", outputs);
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

    private static Dictionary<string, string> EvidenceOutputs(TradingResearchRequest request, RunState state)
    {
        var report = state.Report!;
        var recommendation = state.Recommendation!;
        var proposal = recommendation.Proposal;
        var assumptions = new List<string>(request.Unknowns)
        {
            "The workflow-poc candles are the permitted research fixture, not a live market.",
        };
        return new Dictionary<string, string>
        {
            ["scope"] = Scope,
            ["fact"] = string.Join('\n',
                $"Research session {report.SessionKey} contains {report.Snapshot.Fills.Count.ToString(CultureInfo.InvariantCulture)} fills from source workflow-poc.",
                "Those fills are observations in the research ledger, not an approved paper order."),
            ["inference"] = string.Join('\n',
                recommendation.Rationale,
                $"{proposal.Symbol} {proposal.Side} is the paper option. It is not an execution.",
                $"The recommendation kind is {recommendation.Kind} and its autonomy is {recommendation.Autonomy}."),
            ["assumption"] = string.Join('\n', assumptions),
            ["limit"] = string.Join('\n',
                "Live orders are not sent.",
                "Only a task-scoped paper operation can count as paper execution."),
        };
    }

    private static string OneLine(string text, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(text) ? fallback : text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 180 ? value : value[..180];
    }

    private static bool TryLoadRecommendation(TradingResearchRequest request, RunState state, ToolCall call, out ToolResponse failure)
    {
        failure = ToolResponse.Stop(ToolStepStatus.Failed, "There is no paper recommendation to execute.");
        if (state.Report is null)
        {
            if (!call.Inputs.TryGetValue("evidence", out var sessionKey) || string.IsNullOrWhiteSpace(sessionKey))
            {
                failure = ToolResponse.Uncertain("The research step has no committed session to reread.");
                return false;
            }

            var stored = request.Research.Find(sessionKey);
            if (stored is null)
            {
                failure = ToolResponse.Uncertain("The committed research session could not be reread.");
                return false;
            }

            state.Report = new ResearchReport(
                stored.Status,
                true,
                0,
                0,
                0,
                stored.SessionKey,
                ResearchSession.Deserialize(stored.Result));
        }

        if (state.Recommendation is not null && state.PaperVerdict is not null)
            return true;
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
        {
            failure = ToolResponse.Stop(ToolStepStatus.Failed, "The recommendation did not stay paper and recommend_only.");
            return false;
        }

        state.PaperVerdict = RiskEngine.Evaluate(state.Recommendation.Proposal, request.Limits);
        return true;
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
