using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.ResearchHost;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "AgentRuntime";
    public string? TaskConnection { get; set; }
    public string? ResearchConnection { get; set; }
    public bool AllowCrashHook { get; set; }
}

public static class AgentCrashHook
{
    public static void ExitIfRequested(string phase, string stepId)
    {
        if (phase == "after-commit" && string.Equals(stepId, Environment.GetEnvironmentVariable("KIT_AGENT_HOLD_AFTER_COMMIT"), StringComparison.Ordinal))
            Thread.Sleep(Timeout.Infinite);
        if (phase == "after-tool" && string.Equals(stepId, Environment.GetEnvironmentVariable("KIT_AGENT_CRASH_AFTER_TOOL"), StringComparison.Ordinal))
            Environment.Exit(86);
        if (phase == "after-commit" && string.Equals(stepId, Environment.GetEnvironmentVariable("KIT_AGENT_CRASH_AFTER_COMMIT"), StringComparison.Ordinal))
            Environment.Exit(87);
    }
}

public sealed class AgentTaskCommand
{
    public string TaskKey { get; set; } = "";
    public string Objective { get; set; } = "";
    public string DoneWhen { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? ExecutionMode { get; set; }
    public Guid? ApprovalDecisionId { get; set; }
    public Guid? RequestedTenantId { get; set; }
    public List<CompletionAsk>? Asks { get; set; }
    public BusinessRequest? Business { get; set; }
}

public sealed record AgentTaskView(
    string TaskKey,
    string PayloadHash,
    Guid TenantId,
    string Status,
    string? Outcome,
    int ResearchFillCount,
    bool RecommendationExecuted,
    bool LiveAccepted,
    bool Replayed,
    bool ApprovalRecorded,
    string? Error,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<AgentTaskStepView> Steps,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? ExecutionMode,
    string NextAction,
    IReadOnlyList<AgentTaskDecisionView> Decisions,
    AgentTaskResultView? Result,
    string Objective = "",
    string DoneWhen = "",
    AgentTaskGoalView? Goal = null);

public sealed record AgentTaskStepView(string StepId, string ToolName, string Status, string Summary, string? Error, string? Purpose = null);

public sealed record AgentTaskDecisionView(
    string Kind,
    string Decision,
    Guid? DecisionId,
    string ActorId,
    DateTimeOffset At,
    string? Evidence,
    string? CheckSource,
    string? ObservedResult);

public sealed record AgentTaskResultView(
    string? ResearchSessionKey,
    string? SnapshotOutcome,
    string? PaperOperationKey,
    string? TaskOutcome,
    DateTimeOffset RecordedAt);

public sealed record AgentTaskGoalView(
    string Scope,
    bool Met,
    string Reason,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Inferences,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Limits,
    string Understanding = "");

public sealed class AgentApprovalCommand
{
    public Guid? ApprovalDecisionId { get; set; }
    public Guid? RequestedTenantId { get; set; }
}

public sealed class AgentReconciliationCommand
{
    public string Disposition { get; set; } = "";
    public string? Evidence { get; set; }
    public Guid? RequestedTenantId { get; set; }
}

public sealed class AgentTaskUnavailableException : InvalidOperationException
{
    public AgentTaskUnavailableException(string message) : base(message) { }
}

public static class AgentTaskServiceCollectionExtensions
{
    public static IServiceCollection AddAgentTasks(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentRuntimeOptions>(configuration.GetSection(AgentRuntimeOptions.SectionName));
        services.AddScoped<AgentTaskService>();
        return services;
    }
}

public sealed class AgentTaskService
{
    public const string TradingPaperKind = "trading-paper";
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _configuration;
    private readonly AgentRuntimeOptions _options;

    public AgentTaskService(ITenantContext tenant, IOptions<AgentRuntimeOptions> options, IConfiguration configuration)
    {
        _tenant = tenant;
        _options = options.Value;
        _configuration = configuration;
    }

    public AgentTaskView Run(ClaimsPrincipal user, AgentTaskCommand command)
    {
        if (string.Equals(command.Kind, EvidenceReviewRuntime.Kind, StringComparison.Ordinal))
            return RunEvidence(user, command);
        if (string.Equals(command.Kind, GoalTaskRuntime.Kind, StringComparison.Ordinal))
            return RunGoal(user, command);
        return RunTrading(user, command);
    }

    public AgentTaskView RunTrading(ClaimsPrincipal user, AgentTaskCommand command)
    {
        var tenantId = CallerTenant(user, command.RequestedTenantId);
        var taskKey = RequireText(command.TaskKey, "A task key is required.");
        var objective = RequireText(command.Objective, "An objective is required.");
        var doneWhen = RequireText(command.DoneWhen, "A completion criterion is required.");
        if (command.Asks is { Count: > 0 })
            throw new InvalidOperationException("A trading-paper task does not accept a structured completion condition.");
        if (!CanTrade(user))
            throw new UnauthorizedAccessException("Trading paper requires the personal_intelligence.trading.run permission.");
        if (!string.Equals(command.Kind, TradingPaperKind, StringComparison.Ordinal))
            throw new InvalidOperationException("This host accepts only the trading-paper task.");
        var mode = NormalizeMode(command.ExecutionMode);
        var approval = Approval(user, command.ApprovalDecisionId);
        var approvedAt = approval is null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow;
        var request = new AgentRequest(
            taskKey,
            PayloadHash(TradingPaperKind, objective, doneWhen, mode, approval?.DecisionId),
            tenantId,
            objective,
            doneWhen,
            Grants(user),
            TradingResearchRuntime.PlanFor(objective, doneWhen),
            approval,
            command.RequestedTenantId,
            mode.ToString(),
            approval is null ? null : [Decision("approval", "approved", approval.DecisionId, approval.ApprovedBy, approvedAt!.Value, null)],
            approvedAt,
            true);
        var proposal = new TradeProposal("BTCUSDT", "BUY", 0.001m, 100m, 1m, 0.8m, mode);
        var limits = new RiskLimits(1_000m, 1m, 0.5m);
        var intake = ResearchCandles.Classify(
            TradingResearchRuntime.FixedPaperCandles(),
            TradingResearchRuntime.FixedPaperCandles().Last().CloseTime,
            TimeSpan.FromDays(30));
        var config = new ResearchConfig(
            0.001m,
            0.8m,
            10_000m,
            new PaperSessionConfig(10m, 0.001m, 0m, 1m, limits, 5_000m, false));
        using var research = OpenResearch();
        using var log = OpenTasks(tenantId);
        var result = new TradingResearchRuntime().Execute(
            new TradingResearchRequest(
                request,
                intake,
                config,
                research,
                research,
                proposal,
                limits,
                ["The next bar is not known."],
                TradingResearchRuntime.FixedPaperRecordedAt),
            log,
            _options.AllowCrashHook ? AgentCrashHook.ExitIfRequested : null);
        return ToView(result.Task, user);
    }

    public AgentTaskView RunEvidence(ClaimsPrincipal user, AgentTaskCommand command)
    {
        var tenantId = CallerTenant(user, command.RequestedTenantId);
        var taskKey = RequireText(command.TaskKey, "A task key is required.");
        var objective = RequireText(command.Objective, "An objective is required.");
        var doneWhen = RequireText(command.DoneWhen, "A completion criterion is required.");
        if (command.Asks is { Count: > 0 })
            throw new InvalidOperationException("Evidence review does not accept a structured completion condition.");
        if (!CanReadEvidence(user))
            throw new UnauthorizedAccessException("Evidence review requires the personal_intelligence.evidence.read permission.");
        if (string.Equals(command.ExecutionMode, "live", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Evidence review does not accept live mode.");
        if (command.ApprovalDecisionId is Guid)
            throw new InvalidOperationException("Evidence review does not accept an approval decision.");
        var request = new AgentRequest(
            taskKey,
            PayloadHash(EvidenceReviewRuntime.Kind, objective, doneWhen, ExecutionMode.Paper, null),
            tenantId,
            objective,
            doneWhen,
            Grants(user),
            EvidenceReviewRuntime.PlanFor(objective, doneWhen),
            null,
            command.RequestedTenantId,
            nameof(ExecutionMode.Paper),
            null,
            null,
            true);
        using var log = OpenTasks(tenantId);
        var result = new EvidenceReviewRuntime().Execute(request, new SqlEvidenceLedger(OpenEvidence()), log);
        return ToView(result.Task, user);
    }

    public AgentTaskView RunGoal(ClaimsPrincipal user, AgentTaskCommand command)
    {
        var tenantId = CallerTenant(user, command.RequestedTenantId);
        var taskKey = RequireText(command.TaskKey, "A task key is required.");
        var objective = RequireText(command.Objective, "An objective is required.");
        var doneWhen = RequireText(command.DoneWhen, "A completion criterion is required.");
        RequireAsks(command.Asks);
        if (!CanReadEvidence(user))
            throw new UnauthorizedAccessException("A goal task requires the personal_intelligence.evidence.read permission.");
        if (string.Equals(command.ExecutionMode, "live", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A goal task does not accept live mode.");
        if (command.ApprovalDecisionId is Guid)
            throw new InvalidOperationException("A goal task does not accept an approval decision.");
        var asks = command.Asks ?? [];
        if (asks.Count > 0 && command.Business is not null)
            throw new InvalidOperationException("A goal task accepts either structured conditions or a business request.");
        RequireBusiness(command.Business);
        if (asks.Count == 0)
        {
            var multi = MultiCriterionPlanning.Interpret(objective, doneWhen, command.Business);
            if (multi is not null)
                return RunMulti(user, command, tenantId, taskKey, objective, doneWhen, multi);
            var validation = EvidenceValidation.Interpret(objective, doneWhen, command.Business);
            if (validation is not null)
                return RunValidation(user, command, tenantId, taskKey, objective, doneWhen, validation);
            var interpreted = BusinessUnderstanding.Interpret(objective, doneWhen, command.Business);
            if (interpreted.Understanding.State != UnderstandingState.NotUnderstood || command.Business is not null)
                return RunCompleteness(user, command, tenantId, taskKey, objective, doneWhen, interpreted);
        }

        var understood = GoalTaskRuntime.Understand(objective, doneWhen, asks);
        var request = new AgentRequest(
            taskKey,
            PayloadHash(GoalTaskRuntime.Kind, objective, doneWhen, ExecutionMode.Paper, null, RequestUnderstanding.Canonical(asks)),
            tenantId,
            objective,
            doneWhen,
            Grants(user),
            understood.Plan,
            null,
            command.RequestedTenantId,
            nameof(ExecutionMode.Paper),
            null,
            null,
            true,
            asks);
        using var log = OpenTasks(tenantId);
        IEvidenceLedger ledger = understood.State == UnderstandingState.ReadyToPlan
            ? new SqlEvidenceLedger(OpenEvidence())
            : new RefusingEvidenceLedger();
        var result = new GoalTaskRuntime().Execute(request, ledger, log);
        return ToView(result.Task, user);
    }

    private AgentTaskView RunCompleteness(
        ClaimsPrincipal user,
        AgentTaskCommand command,
        Guid tenantId,
        string taskKey,
        string objective,
        string doneWhen,
        BusinessInterpretation interpreted)
    {
        var canonical = BusinessUnderstanding.Canonical(command.Business);
        if (canonical.Length == 0 && interpreted.FeasibleNeeds.Count > 0)
            canonical = string.Join("|", BusinessUnderstanding.Intent, BusinessUnderstanding.Subject, string.Join(",", interpreted.FeasibleNeeds.OrderBy(item => item, StringComparer.Ordinal)));
        var request = new AgentRequest(
            taskKey,
            PayloadHash(GoalTaskRuntime.Kind, objective, doneWhen, ExecutionMode.Paper, null, canonical),
            tenantId,
            objective,
            doneWhen,
            Grants(user),
            interpreted.Understanding.Plan,
            null,
            command.RequestedTenantId,
            nameof(ExecutionMode.Paper),
            null,
            null,
            true,
            null,
            command.Business);
        using var log = OpenTasks(tenantId);
        IDecisionSupportLedger ledger = interpreted.Understanding.State == UnderstandingState.ReadyToPlan
            ? new SqlEvidenceLedger(OpenEvidence())
            : new RefusingDecisionSupportLedger();
        var result = new CompletenessRuntime().Execute(request, ledger, log);
        return ToView(result.Task, user);
    }

    private AgentTaskView RunValidation(
        ClaimsPrincipal user,
        AgentTaskCommand command,
        Guid tenantId,
        string taskKey,
        string objective,
        string doneWhen,
        EvidenceInterpretation interpreted)
    {
        var canonical = EvidenceValidation.Canonical(command.Business);
        if (canonical.Length == 0 && (interpreted.FeasibleNeeds.Count > 0 || interpreted.UnverifiableNeeds.Count > 0))
        {
            var needs = interpreted.FeasibleNeeds.Concat(interpreted.UnverifiableNeeds).OrderBy(item => item, StringComparer.Ordinal);
            canonical = string.Join("|", EvidenceValidation.Intent, EvidenceValidation.Subject, string.Join(",", needs));
        }

        var request = new AgentRequest(
            taskKey,
            PayloadHash(GoalTaskRuntime.Kind, objective, doneWhen, ExecutionMode.Paper, null, canonical),
            tenantId,
            objective,
            doneWhen,
            Grants(user),
            interpreted.Understanding.Plan,
            null,
            command.RequestedTenantId,
            nameof(ExecutionMode.Paper),
            null,
            null,
            true,
            null,
            command.Business);
        using var log = OpenTasks(tenantId);
        IEvidenceAssessmentLedger ledger = interpreted.Understanding.State == UnderstandingState.ReadyToPlan
            ? new SqlEvidenceLedger(OpenEvidence())
            : new RefusingEvidenceAssessmentLedger();
        var result = new EvidenceAssessmentRuntime().Execute(request, ledger, log);
        return ToView(result.Task, user);
    }

    private AgentTaskView RunMulti(
        ClaimsPrincipal user,
        AgentTaskCommand command,
        Guid tenantId,
        string taskKey,
        string objective,
        string doneWhen,
        MultiCriterionInterpretation interpreted)
    {
        var canonical = MultiCriterionPlanning.Canonical(command.Business);
        if (canonical.Length == 0 && interpreted.FeasibleNeeds.Count > 0)
            canonical = string.Join("|", MultiCriterionPlanning.Intent, MultiCriterionPlanning.Subject, string.Join(",", interpreted.FeasibleNeeds.OrderBy(item => item, StringComparer.Ordinal)));
        var request = new AgentRequest(
            taskKey,
            PayloadHash(GoalTaskRuntime.Kind, objective, doneWhen, ExecutionMode.Paper, null, canonical),
            tenantId,
            objective,
            doneWhen,
            Grants(user),
            interpreted.Understanding.Plan,
            null,
            command.RequestedTenantId,
            nameof(ExecutionMode.Paper),
            null,
            null,
            true,
            null,
            command.Business);
        using var log = OpenTasks(tenantId);
        IEvidenceLedger evidence;
        IDecisionSupportLedger coverage;
        if (interpreted.Understanding.State == UnderstandingState.ReadyToPlan)
        {
            var ledger = new SqlEvidenceLedger(OpenEvidence());
            evidence = ledger;
            coverage = ledger;
        }
        else
        {
            evidence = new RefusingEvidenceLedger();
            coverage = new RefusingDecisionSupportLedger();
        }

        var result = new MultiCriterionRuntime().Execute(request, evidence, coverage, log);
        return ToView(result.Task, user);
    }

    public AgentTaskView Approve(ClaimsPrincipal user, string taskKey, AgentApprovalCommand command)
    {
        var tenantId = CallerTenant(user, command.RequestedTenantId);
        var key = RequireText(taskKey, "A task key is required.");
        var approval = Approval(user, command.ApprovalDecisionId)
            ?? throw new InvalidOperationException("An approval decision is required.");
        using var research = OpenResearch();
        using var log = OpenTasks(tenantId);
        var stored = log.Find(key) ?? throw new AgentTaskNotFoundException("The task was not found.");
        var mode = StoredMode(stored.ExecutionMode);
        var approvedAt = DateTimeOffset.UtcNow;
        var request = new AgentRequest(
            stored.TaskKey,
            stored.PayloadHash,
            tenantId,
            stored.Objective,
            stored.DoneWhen,
            Grants(user),
            stored.Plan ?? TradingResearchRuntime.Plan,
            approval,
            command.RequestedTenantId,
            stored.ExecutionMode ?? mode.ToString(),
            stored.Decisions,
            stored.ApprovedAt,
            true);
        var result = new TradingResearchRuntime().ContinueAfterApproval(
            TradingRequest(request, research, mode),
            log,
            approvedAt);
        return ToView(result.Task, user);
    }

    public AgentTaskView Reconcile(ClaimsPrincipal user, string taskKey, AgentReconciliationCommand command)
    {
        var tenantId = CallerTenant(user, command.RequestedTenantId);
        var key = RequireText(taskKey, "A task key is required.");
        using var log = OpenTasks(tenantId);
        var stored = log.Find(key) ?? throw new AgentTaskNotFoundException("The task was not found.");
        var request = new AgentRequest(
            stored.TaskKey,
            stored.PayloadHash,
            tenantId,
            stored.Objective,
            stored.DoneWhen,
            Grants(user),
            stored.Plan ?? [],
            null,
            command.RequestedTenantId,
            stored.ExecutionMode,
            stored.Decisions,
            stored.ApprovedAt);
        var (checkSource, observedResult) = ObservePaperOperation(stored);
        var receipt = new AgentRuntime(new ToolRegistry()).RecordReconciliation(
            request,
            log,
            command.Disposition,
            command.Evidence,
            CoreAuthority.RequireUserId(user).ToString("D"),
            DateTimeOffset.UtcNow,
            checkSource,
            observedResult);
        return ToView(receipt, user);
    }

    public AgentTaskView? Find(ClaimsPrincipal user, string taskKey)
    {
        var tenantId = CallerTenant(user, null);
        using var log = OpenTasks(tenantId);
        var receipt = log.Find(taskKey);
        return receipt is null ? null : ToView(receipt, user);
    }

    private Guid CallerTenant(ClaimsPrincipal user, Guid? requestedTenantId)
    {
        var claimTenant = CoreAuthority.RequireTenantId(user);
        if (_tenant.TenantId != claimTenant)
            throw new UnauthorizedAccessException("Tenant context does not match the caller.");
        if (requestedTenantId is Guid requested)
            CoreAuthority.RejectTenantSwitch(claimTenant, requested);
        return claimTenant;
    }

    private PostgresAgentRunLog OpenTasks(Guid tenantId)
    {
        var connection = RequireConnection(_options.TaskConnection, "The task store is not configured.");
        RefuseSharedPort(connection);
        return new PostgresAgentRunLog(connection, tenantId);
    }

    private SqlResearchStore OpenResearch()
    {
        var connection = RequireConnection(_options.ResearchConnection, "The research ledger is not configured.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        RefuseSharedPort(connection);
        if (!string.Equals(builder.Database, "trading_agent", StringComparison.Ordinal))
            throw new AgentTaskUnavailableException("The research ledger is not the isolated trading_agent database.");
        using var probe = new NpgsqlConnection(builder.ConnectionString);
        probe.Open();
        using var command = new NpgsqlCommand("SELECT current_database(), inet_server_port()", probe);
        using var reader = command.ExecuteReader();
        reader.Read();
        if (!string.Equals(reader.GetString(0), "trading_agent", StringComparison.Ordinal) || reader.GetInt32(1) == 5432)
            throw new AgentTaskUnavailableException("The research ledger connection was refused.");
        return new SqlResearchStore(builder.ConnectionString);
    }

    private string OpenEvidence()
    {
        var connection = RequireConnection(_configuration.GetConnectionString("Default"), "The evidence ledger is not configured.");
        RefuseSharedPort(connection);
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Database is not "kit_pi_validation")
            throw new AgentTaskUnavailableException("The evidence ledger is not the isolated validation database.");
        using var probe = new NpgsqlConnection(builder.ConnectionString);
        probe.Open();
        using var command = new NpgsqlCommand("SELECT current_database(), inet_server_port()", probe);
        using var reader = command.ExecuteReader();
        reader.Read();
        if (!string.Equals(reader.GetString(0), "kit_pi_validation", StringComparison.Ordinal) || reader.GetInt32(1) == 5432)
            throw new AgentTaskUnavailableException("The evidence ledger connection was refused.");
        return builder.ConnectionString;
    }

    private static string RequireConnection(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AgentTaskUnavailableException(message);
        return value;
    }

    private static void RefuseSharedPort(string connection)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Port == 5432)
            throw new AgentTaskUnavailableException("Refusing the shared PostgreSQL port.");
    }

    private static ExecutionApproval? Approval(ClaimsPrincipal user, Guid? decisionId)
    {
        if (decisionId is not Guid id || id == Guid.Empty)
            return null;
        var allowed = user.IsInRole("ADMIN") || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.ApprovePermission);
        if (!allowed)
            throw new UnauthorizedAccessException("Paper approval requires the personal_intelligence.trading.approve permission.");
        return new ExecutionApproval(id, CoreAuthority.RequireUserId(user).ToString("D"));
    }

    private static IReadOnlyList<string> Grants(ClaimsPrincipal user)
    {
        var admin = user.IsInRole("ADMIN");
        var granted = new List<string>();
        if (admin || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.ResearchReadPermission) || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.Permission))
            granted.Add(TradingResearchRuntime.ResearchPermission);
        if (admin || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.Permission))
            granted.Add(TradingResearchRuntime.RunPermission);
        if (admin || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.ApprovePermission))
            granted.Add(TradingResearchRuntime.ApprovePermission);
        if (admin || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.EvidenceReadPermission))
            granted.Add(EvidenceReviewRuntime.ReadPermission);
        return granted;
    }

    private static bool CanTrade(ClaimsPrincipal user)
        => user.IsInRole("ADMIN") || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.Permission);

    private static bool CanReadEvidence(ClaimsPrincipal user)
        => user.IsInRole("ADMIN") || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.EvidenceReadPermission);

    private static void RequireAsks(IReadOnlyList<CompletionAsk>? asks)
    {
        if (asks is null)
            return;
        if (asks.Count > 3)
            throw new InvalidOperationException("A goal task accepts at most three completion conditions.");
        foreach (var ask in asks)
        {
            if ((ask.Id?.Length ?? 0) > 80
                || (ask.Capability?.Length ?? 0) > 80
                || (ask.Subject?.Length ?? 0) > 80
                || (ask.Relation?.Length ?? 0) > 80
                || (ask.Target?.Length ?? 0) > 80)
                throw new InvalidOperationException("A completion condition field is too long.");
        }
    }

    private static void RequireBusiness(BusinessRequest? business)
    {
        if (business is null)
            return;
        if ((business.Intent?.Length ?? 0) > 80 || (business.Subject?.Length ?? 0) > 80)
            throw new InvalidOperationException("A business request field is too long.");
        if (business.Needs is { Count: > 4 })
            throw new InvalidOperationException("A business request accepts at most four criteria.");
        if (business.Needs is not null && business.Needs.Any(item => (item?.Length ?? 0) > 80))
            throw new InvalidOperationException("A business request field is too long.");
    }

    private static string PayloadHash(string kind, string objective, string doneWhen, ExecutionMode mode, Guid? approvalId, string asks = "")
    {
        var text = string.Join('\n', kind, objective, doneWhen, mode, approvalId?.ToString("D") ?? "");
        if (asks.Length > 0)
            text += "\n" + asks;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static TradingResearchRequest TradingRequest(AgentRequest request, SqlResearchStore research, ExecutionMode mode)
    {
        var limits = new RiskLimits(1_000m, 1m, 0.5m);
        var candles = TradingResearchRuntime.FixedPaperCandles();
        return new TradingResearchRequest(
            request,
            ResearchCandles.Classify(candles, candles.Last().CloseTime, TimeSpan.FromDays(30)),
            new ResearchConfig(0.001m, 0.8m, 10_000m, new PaperSessionConfig(10m, 0.001m, 0m, 1m, limits, 5_000m, false)),
            research,
            research,
            new TradeProposal("BTCUSDT", "BUY", 0.001m, 100m, 1m, 0.8m, mode),
            limits,
            ["The next bar is not known."],
            TradingResearchRuntime.FixedPaperRecordedAt);
    }

    private static ExecutionMode StoredMode(string? mode)
    {
        if (string.Equals(mode, nameof(ExecutionMode.Live), StringComparison.Ordinal))
            return ExecutionMode.Live;
        if (mode is null || string.Equals(mode, nameof(ExecutionMode.Paper), StringComparison.Ordinal))
            return ExecutionMode.Paper;
        throw new InvalidOperationException("The stored execution mode cannot be continued.");
    }

    private static TaskDecisionRecord Decision(string kind, string decision, Guid? id, string actor, DateTimeOffset at, string? evidence)
        => new(kind, decision, id, actor, at, evidence);

    private (string CheckSource, string ObservedResult) ObservePaperOperation(AgentTaskReceipt stored)
    {
        if (stored.ApprovalDecisionId is not Guid decision || decision == Guid.Empty)
            return ("not_available", "unknown");
        var key = TradingResearchRuntime.TaskPaperOperationKey(stored.TenantId, stored.TaskKey, decision);
        using var research = OpenResearch();
        var row = ((IPaperOperationLog)research).Find(key);
        if (row is null)
            return ("paper_operation", "absent");
        if (!string.Equals(row.PayloadHash, stored.PayloadHash, StringComparison.Ordinal)
            || !string.Equals(row.Status, "paper_filled", StringComparison.Ordinal))
            return ("paper_operation", "unknown");
        return ("paper_operation", "occurred");
    }

    private static ExecutionMode NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "paper", StringComparison.OrdinalIgnoreCase))
            return ExecutionMode.Paper;
        if (string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase))
            return ExecutionMode.Live;
        throw new InvalidOperationException("Execution mode must be paper or live.");
    }

    private static string RequireText(string value, string message)
    {
        var text = value.Trim();
        if (text.Length is 0 or > 200)
            throw new InvalidOperationException(message);
        return text;
    }

    private static AgentTaskView ToView(AgentTaskReceipt receipt, ClaimsPrincipal user)
        => new(
            receipt.TaskKey,
            receipt.PayloadHash,
            receipt.TenantId,
            receipt.Status.ToString(),
            receipt.Outcome,
            receipt.ResearchFillCount,
            receipt.RecommendationExecuted,
            receipt.LiveAccepted,
            receipt.Replayed,
            receipt.ApprovalRecorded,
            receipt.Error,
            receipt.EvidenceRefs,
            receipt.Steps.Select(step => new AgentTaskStepView(
                step.StepId,
                step.ToolName,
                step.Status.ToString(),
                step.Summary,
                step.Error,
                receipt.Plan?.FirstOrDefault(item => item.StepId == step.StepId)?.Purpose)).ToArray(),
            receipt.ApprovedBy,
            receipt.ApprovedAt,
            receipt.ExecutionMode,
            NextAction(receipt, user),
            (receipt.Decisions ?? []).Select(row => new AgentTaskDecisionView(row.Kind, row.Decision, row.DecisionId, row.ActorId, row.At, row.Evidence, row.CheckSource, row.ObservedResult)).ToArray(),
            receipt.Result is null
                ? null
                : new AgentTaskResultView(
                    receipt.Result.ResearchSessionKey,
                    receipt.Result.SnapshotOutcome,
                    receipt.Result.PaperOperationKey,
                    receipt.Result.TaskOutcome,
                    receipt.Result.RecordedAt),
            receipt.Objective,
            receipt.DoneWhen,
            receipt.Goal is null
                ? null
                : new AgentTaskGoalView(
                    receipt.Goal.Scope,
                    receipt.Goal.Met,
                    receipt.Goal.Reason,
                    receipt.Goal.Facts,
                    receipt.Goal.Inferences,
                    receipt.Goal.Assumptions,
                    receipt.Goal.Missing,
                    receipt.Goal.Limits,
                    receipt.Goal.Understanding ?? ""));

    private static string NextAction(AgentTaskReceipt receipt, ClaimsPrincipal user)
    {
        var canApprove = user.IsInRole("ADMIN") || CoreAuthority.HasPermission(user, PersonalIntelligencePolicies.ApprovePermission);
        return receipt.Status switch
        {
            AgentTaskStatus.AwaitingApproval => canApprove ? "approve" : "wait_for_approval",
            AgentTaskStatus.ReconciliationRequired => canApprove ? "manual_reconciliation" : "wait_for_reconciliation",
            AgentTaskStatus.Running or AgentTaskStatus.Received or AgentTaskStatus.Planned or AgentTaskStatus.Verified => "wait",
            _ => "none",
        };
    }
}

[ApiController]
[Route("api/personal-intelligence/tasks")]
public sealed class AgentTaskController : ControllerBase
{
    private readonly AgentTaskService _tasks;

    public AgentTaskController(AgentTaskService tasks) => _tasks = tasks;

    [HttpPost]
    [Authorize(Policy = PersonalIntelligencePolicies.TaskCreate)]
    [RequirePersonalIntelligenceModule]
    public ActionResult<AgentTaskView> Create([FromBody] AgentTaskCommand command)
    {
        try
        {
            return Ok(_tasks.Run(User, command));
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    [HttpPost("{taskKey}/approval")]
    [Authorize(Policy = PersonalIntelligencePolicies.Approve)]
    [RequirePersonalIntelligenceModule]
    public ActionResult<AgentTaskView> Approve(string taskKey, [FromBody] AgentApprovalCommand command)
    {
        try
        {
            return Ok(_tasks.Approve(User, taskKey, command));
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    [HttpPost("{taskKey}/reconciliation")]
    [Authorize(Policy = PersonalIntelligencePolicies.Approve)]
    [RequirePersonalIntelligenceModule]
    public ActionResult<AgentTaskView> Reconcile(string taskKey, [FromBody] AgentReconciliationCommand command)
    {
        try
        {
            return Ok(_tasks.Reconcile(User, taskKey, command));
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    [HttpGet("{taskKey}")]
    [Authorize(Policy = PersonalIntelligencePolicies.TaskRead)]
    [RequirePersonalIntelligenceModule]
    public ActionResult<AgentTaskView> Get(string taskKey)
    {
        try
        {
            var receipt = _tasks.Find(User, taskKey);
            return receipt is null ? NotFound(new { message = "The task was not found." }) : Ok(receipt);
        }
        catch (Exception ex)
        {
            return Map(ex);
        }
    }

    private ActionResult Map(Exception exception) => exception switch
    {
        AgentTaskUnavailableException => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }),
        AgentTaskBusyException => StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message }),
        AgentTaskNotFoundException => NotFound(new { message = exception.Message }),
        AgentTaskStateException => Conflict(new { message = exception.Message }),
        AgentRunConflictException => Conflict(new { message = exception.Message }),
        AgentTenantMismatchException => StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }),
        UnauthorizedAccessException => StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }),
        InvalidOperationException => BadRequest(new { message = exception.Message }),
        _ => throw exception,
    };
}
