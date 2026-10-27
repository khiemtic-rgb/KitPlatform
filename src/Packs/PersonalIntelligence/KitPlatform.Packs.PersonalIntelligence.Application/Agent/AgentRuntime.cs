using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Packs.PersonalIntelligence.Agent;

public enum AgentTaskStatus
{
    Received,
    Planned,
    Running,
    AwaitingApproval,
    Verified,
    Completed,
    Failed,
    Cancelled,
    ReconciliationRequired,
}

public enum ToolEffect
{
    Read,
    Paper,
    Live,
}

public enum ToolStepStatus
{
    Succeeded,
    Blocked,
    Denied,
    Rejected,
    Failed,
    Uncertain,
}

public sealed record AgentStepSpec(string StepId, string ToolName, string? DependsOn);

public sealed record AgentRequest(
    string TaskKey,
    string PayloadHash,
    Guid TenantId,
    string Objective,
    string DoneWhen,
    IReadOnlyList<string> GrantedPermissions,
    IReadOnlyList<AgentStepSpec> Plan,
    ExecutionApproval? Approval,
    Guid? RequestedTenantId = null,
    string? ExecutionMode = null,
    IReadOnlyList<TaskDecisionRecord>? Decisions = null,
    DateTimeOffset? ApprovedAt = null);

public sealed record ToolContract(
    string Name,
    string RequiredPermission,
    ToolEffect Effect,
    bool RequiresApproval);

public sealed record ToolCall(string StepId, Guid TenantId, IReadOnlyDictionary<string, string> Inputs);

public sealed record ToolResponse(
    ToolStepStatus Status,
    string Summary,
    IReadOnlyDictionary<string, string> Outputs,
    string? Error)
{
    public static ToolResponse Succeeded(string summary, IReadOnlyDictionary<string, string> outputs)
        => new(ToolStepStatus.Succeeded, summary, outputs, null);

    public static ToolResponse Stop(ToolStepStatus status, string error)
        => new(status, error, new Dictionary<string, string>(), error);

    public static ToolResponse Uncertain(string error)
        => new(ToolStepStatus.Uncertain, error, new Dictionary<string, string>(), error);
}

public sealed record AgentStepRecord(
    string StepId,
    string ToolName,
    ToolStepStatus Status,
    string Summary,
    string? Error,
    IReadOnlyDictionary<string, string>? Outputs = null);

public sealed record AgentTaskReceipt(
    string TaskKey,
    string PayloadHash,
    Guid TenantId,
    AgentTaskStatus Status,
    string Objective,
    string DoneWhen,
    IReadOnlyList<AgentStepRecord> Steps,
    string? Outcome,
    int ResearchFillCount,
    bool RecommendationExecuted,
    bool LiveAccepted,
    bool Replayed,
    string? Error,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<AgentStepSpec>? Plan = null,
    bool ApprovalRecorded = false,
    Guid? ApprovalDecisionId = null,
    string? ApprovedBy = null,
    DateTimeOffset? ApprovedAt = null,
    string? ExecutionMode = null,
    IReadOnlyList<TaskDecisionRecord>? Decisions = null);

/// <summary>
/// Copy of the task outcome stored beside a research snapshot. It is not a second ledger.
/// </summary>
public sealed record AgentTaskTrace(
    string TaskKey,
    string Status,
    string? Outcome,
    int ResearchFillCount,
    bool RecommendationExecuted,
    bool LiveAccepted);

public sealed class AgentRunConflictException : InvalidOperationException
{
    public AgentRunConflictException(string? message = null)
        : base(message ?? "The task key was already used with a different payload.")
    {
    }
}

public sealed class AgentTenantMismatchException : InvalidOperationException
{
    public AgentTenantMismatchException()
        : base("The task key belongs to another tenant.")
    {
    }
}

public sealed class AgentTaskBusyException : InvalidOperationException
{
    public AgentTaskBusyException()
        : base("The task is still running for another caller.")
    {
    }
}

public sealed class AgentTaskNotFoundException : InvalidOperationException
{
    public AgentTaskNotFoundException(string message) : base(message) { }
}

public sealed class AgentTaskStateException : InvalidOperationException
{
    public AgentTaskStateException(string message) : base(message) { }
}

public sealed record TaskDecisionRecord(
    string Kind,
    string Decision,
    Guid? DecisionId,
    string ActorId,
    DateTimeOffset At,
    string? Evidence);

public interface IAgentRunLog
{
    AgentTaskReceipt? Find(string taskKey);

    /// <summary>
    /// Inserts the receipt when the key is free and returns null.
    /// Returns the stored receipt when the key is already present. The caller must not run tools.
    /// </summary>
    AgentTaskReceipt? Claim(AgentTaskReceipt receipt);

    /// <summary>
    /// Replaces the claimed receipt when the tenant and payload hash still match.
    /// </summary>
    void Remember(AgentTaskReceipt receipt);

    /// <summary>
    /// Locks a stored task so recovery can continue it.
    /// A Running or Verified row stays locked. Any other stored row is returned and unlocked.
    /// </summary>
    AgentTaskReceipt? Adopt(AgentTaskReceipt receipt)
    {
        var existing = Find(receipt.TaskKey);
        if (existing is null)
            return null;
        if (existing.TenantId != receipt.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(existing.PayloadHash, receipt.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        return existing;
    }
}

public sealed class MemoryAgentRunLog : IAgentRunLog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentTaskReceipt> _runs = new(StringComparer.Ordinal);

    public AgentTaskReceipt? Find(string taskKey)
    {
        lock (_gate)
            return _runs.TryGetValue(taskKey, out var receipt) ? receipt : null;
    }

    public AgentTaskReceipt? Claim(AgentTaskReceipt receipt)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(receipt.TaskKey, out var existing))
            {
                Reject(existing, receipt);
                return existing;
            }

            _runs.Add(receipt.TaskKey, receipt);
            return null;
        }
    }

    public void Remember(AgentTaskReceipt receipt)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(receipt.TaskKey, out var existing))
            {
                Reject(existing, receipt);
                _runs[receipt.TaskKey] = receipt;
                return;
            }

            _runs.Add(receipt.TaskKey, receipt);
        }
    }

    private static void Reject(AgentTaskReceipt existing, AgentTaskReceipt incoming)
    {
        if (existing.TenantId != incoming.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(existing.PayloadHash, incoming.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
    }
}

public static class AgentTaskTransitions
{
    public static AgentTaskStatus Move(AgentTaskStatus from, AgentTaskStatus to)
    {
        if (!Allowed(from, to))
            throw new InvalidOperationException($"Cannot move a task from {from} to {to}.");
        return to;
    }

    public static bool Allowed(AgentTaskStatus from, AgentTaskStatus to) => (from, to) switch
    {
        (AgentTaskStatus.Received, AgentTaskStatus.Planned) => true,
        (AgentTaskStatus.Received, AgentTaskStatus.Failed) => true,
        (AgentTaskStatus.Received, AgentTaskStatus.Cancelled) => true,
        (AgentTaskStatus.Planned, AgentTaskStatus.Running) => true,
        (AgentTaskStatus.Planned, AgentTaskStatus.Failed) => true,
        (AgentTaskStatus.Planned, AgentTaskStatus.Cancelled) => true,
        (AgentTaskStatus.Running, AgentTaskStatus.AwaitingApproval) => true,
        (AgentTaskStatus.Running, AgentTaskStatus.Verified) => true,
        (AgentTaskStatus.Running, AgentTaskStatus.Failed) => true,
        (AgentTaskStatus.Running, AgentTaskStatus.Cancelled) => true,
        (AgentTaskStatus.Running, AgentTaskStatus.ReconciliationRequired) => true,
        (AgentTaskStatus.AwaitingApproval, AgentTaskStatus.Running) => true,
        (AgentTaskStatus.AwaitingApproval, AgentTaskStatus.Failed) => true,
        (AgentTaskStatus.AwaitingApproval, AgentTaskStatus.Cancelled) => true,
        (AgentTaskStatus.Verified, AgentTaskStatus.Completed) => true,
        (AgentTaskStatus.Verified, AgentTaskStatus.Failed) => true,
        _ => false,
    };
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, (ToolContract Contract, Func<ToolCall, ToolResponse> Handler)> _tools = new(StringComparer.Ordinal);

    public void Register(ToolContract contract, Func<ToolCall, ToolResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(contract.Name) || string.IsNullOrWhiteSpace(contract.RequiredPermission))
            throw new InvalidOperationException("A tool needs a name and a required permission.");
        if (!_tools.TryAdd(contract.Name, (contract, handler)))
            throw new InvalidOperationException($"Tool {contract.Name} is already registered.");
    }

    public bool TryGet(string name, out ToolContract contract, out Func<ToolCall, ToolResponse> handler)
    {
        if (_tools.TryGetValue(name, out var tool))
        {
            contract = tool.Contract;
            handler = tool.Handler;
            return true;
        }

        contract = null!;
        handler = null!;
        return false;
    }
}

public sealed class ToolDispatcher
{
    public ToolResponse Dispatch(ToolRegistry registry, ToolCall call, AgentRequest request)
    {
        var step = request.Plan.FirstOrDefault(row => row.StepId == call.StepId);
        if (step is null || !registry.TryGet(step.ToolName, out var contract, out var handler))
            return ToolResponse.Stop(ToolStepStatus.Failed, "The tool is not registered.");

        if (contract.Effect == ToolEffect.Live)
            return ToolResponse.Stop(ToolStepStatus.Rejected, "Live execution is outside Agent Runtime V1.");
        if (request.RequestedTenantId is Guid requested && requested != request.TenantId)
            return ToolResponse.Stop(ToolStepStatus.Denied, "A task cannot switch tenant.");
        if (call.TenantId != request.TenantId)
            return ToolResponse.Stop(ToolStepStatus.Denied, "The tool call tenant does not match the task.");
        if (!request.GrantedPermissions.Contains(contract.RequiredPermission, StringComparer.Ordinal))
            return ToolResponse.Stop(ToolStepStatus.Denied, "The granted permissions do not include this tool.");
        if (contract.RequiresApproval && !Approved(request.Approval))
            return ToolResponse.Stop(ToolStepStatus.Blocked, "This tool requires a separate approval.");

        return handler(call);
    }

    private static bool Approved(ExecutionApproval? approval)
        => approval is not null && approval.DecisionId != Guid.Empty && !string.IsNullOrWhiteSpace(approval.ApprovedBy);
}

public sealed class AgentRuntime
{
    public const int MaxSteps = 8;
    private readonly ToolRegistry _registry;
    private readonly ToolDispatcher _dispatcher = new();
    private readonly Action<string, string>? _boundary;

    public AgentRuntime(ToolRegistry registry, Action<string, string>? boundary = null)
    {
        _registry = registry;
        _boundary = boundary;
    }

    public AgentTaskReceipt Execute(AgentRequest request, IAgentRunLog log)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(log);
        if (request.TenantId == Guid.Empty)
            throw new InvalidOperationException("A task requires a tenant.");
        if (string.IsNullOrWhiteSpace(request.TaskKey) || string.IsNullOrWhiteSpace(request.PayloadHash))
            throw new InvalidOperationException("A task requires a key and a payload hash.");
        if (string.IsNullOrWhiteSpace(request.Objective) || string.IsNullOrWhiteSpace(request.DoneWhen))
            throw new InvalidOperationException("A task requires an objective and a completion criterion.");

        var existing = log.Find(request.TaskKey);
        if (existing is not null)
            return RecoverOrReplay(request, log, existing);

        if (request.RequestedTenantId is Guid requested && requested != request.TenantId)
            return StoreNew(log, request, Fail(request, [], "A task cannot switch tenant."));
        if (request.Plan.Count is 0 or > MaxSteps)
            return StoreNew(log, request, Fail(request, [], "The plan is empty or longer than the local step budget."));
        if (request.Plan.Select(step => step.StepId).Distinct(StringComparer.Ordinal).Count() != request.Plan.Count)
            return StoreNew(log, request, Fail(request, [], "Step ids must be unique."));

        var status = AgentTaskTransitions.Move(AgentTaskStatus.Received, AgentTaskStatus.Planned);
        status = AgentTaskTransitions.Move(status, AgentTaskStatus.Running);
        var claimed = StoreNew(log, request, Build(request, status, [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), null, false));
        if (claimed.Replayed)
            return claimed;

        return Run(request, log, [], new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal), false, 0, false);
    }

    private AgentTaskReceipt RecoverOrReplay(AgentRequest request, IAgentRunLog log, AgentTaskReceipt existing)
    {
        if (existing.TenantId != request.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(existing.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        if (existing.Status is not (AgentTaskStatus.Running or AgentTaskStatus.Verified))
            return existing with { Replayed = true };

        request = PreserveDurable(request, existing);
        var adopted = log.Adopt(existing) ?? throw new InvalidOperationException("The task disappeared before recovery.");
        if (adopted.TenantId != request.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(adopted.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        if (adopted.Status == AgentTaskStatus.Verified)
        {
            var completed = AgentTaskTransitions.Move(AgentTaskStatus.Verified, AgentTaskStatus.Completed);
            return Save(log, adopted with { Status = completed, Replayed = false });
        }

        if (adopted.Status != AgentTaskStatus.Running)
            return adopted with { Replayed = true };
        if (!CommittedPrefix(request.Plan, adopted.Steps))
            return Save(log, Build(request, AgentTaskStatus.ReconciliationRequired, adopted.Steps, OutputsOf(adopted.Steps), "The stored steps do not match this plan.", false));

        return Run(request, log, adopted.Steps.ToList(), OutputsOf(adopted.Steps), adopted.Steps.Any(step => step.Status == ToolStepStatus.Blocked), adopted.Steps.Count, true);
    }

    public AgentTaskReceipt ContinueApproved(AgentRequest request, IAgentRunLog log, DateTimeOffset approvedAt)
    {
        var approval = RequireApproval(request);
        var existing = RequireStored(request, log);
        if (!WaitingForApproval(existing))
            return ReplayApproval(existing, approval);

        var adopted = log.Adopt(existing) ?? throw new AgentTaskNotFoundException("The task was not found.");
        if (adopted.TenantId != request.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(adopted.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        if (!WaitingForApproval(adopted))
            return ReplayApproval(adopted, approval);
        if (adopted.ApprovalDecisionId is Guid recorded && recorded != approval.DecisionId)
            throw new AgentRunConflictException("This task already has a different approval decision.");
        if (!CommittedPrefix(request.Plan, adopted.Steps))
            throw new AgentTaskStateException("The stored steps do not match this plan.");
        if (adopted.Steps.Any(step => step.Status == ToolStepStatus.Blocked && !StepRequiresApproval(step)))
            throw new AgentTaskStateException("The blocked step is not an approval gate.");

        var decisions = adopted.Decisions?.ToList() ?? [];
        if (!decisions.Any(row => row.Kind == "approval" && row.DecisionId == approval.DecisionId))
        {
            decisions.Add(new TaskDecisionRecord(
                "approval",
                "approved",
                approval.DecisionId,
                approval.ApprovedBy,
                adopted.ApprovedAt ?? approvedAt,
                null));
        }

        var approved = request with
        {
            Objective = adopted.Objective,
            DoneWhen = adopted.DoneWhen,
            PayloadHash = adopted.PayloadHash,
            Approval = approval,
            ExecutionMode = adopted.ExecutionMode ?? request.ExecutionMode,
            Decisions = decisions,
            ApprovedAt = adopted.ApprovedAt ?? approvedAt,
            Plan = adopted.Plan is { Count: > 0 } ? adopted.Plan : request.Plan,
        };
        var records = adopted.Steps.ToList();
        var outputs = OutputsOf(records);
        var status = adopted.Status == AgentTaskStatus.AwaitingApproval
            ? AgentTaskTransitions.Move(AgentTaskStatus.AwaitingApproval, AgentTaskStatus.Running)
            : AgentTaskStatus.Running;
        Save(log, Build(approved, status, records, outputs, null, false));

        for (var index = 0; index < approved.Plan.Count; index++)
        {
            var step = approved.Plan[index];
            if (index >= records.Count)
            {
                if (!SafeToRepeat(step, out var reason))
                {
                    records.Add(new AgentStepRecord(step.StepId, step.ToolName, ToolStepStatus.Uncertain, "Not committed.", reason));
                    return Save(log, Build(approved, AgentTaskStatus.ReconciliationRequired, records, outputs, reason, false));
                }

                var added = DispatchStep(approved, step, outputs);
                records.Add(added.Record);
                outputs[step.StepId] = added.Response.Outputs;
                var stopped = StopAfter(log, approved, records, outputs, status, added.Response, step.StepId);
                if (stopped is not null)
                    return stopped;
                continue;
            }

            var current = records[index];
            if (!string.Equals(current.StepId, step.StepId, StringComparison.Ordinal))
                throw new AgentTaskStateException("The stored steps do not match this plan.");
            if (current.Status == ToolStepStatus.Succeeded)
                continue;
            if (current.Status != ToolStepStatus.Blocked)
                throw new AgentTaskStateException("A stored step cannot be approved.");

            var dispatched = DispatchStep(approved, step, outputs);
            records[index] = dispatched.Record;
            outputs[step.StepId] = dispatched.Response.Outputs;
            var terminal = StopAfter(log, approved, records, outputs, status, dispatched.Response, step.StepId);
            if (terminal is not null)
                return terminal;
        }

        if (records.Any(row => row.Status == ToolStepStatus.Blocked))
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.AwaitingApproval);
        else if (records.All(row => row.Status == ToolStepStatus.Succeeded))
        {
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.Verified);
            Save(log, Build(approved, status, records, outputs, null, false));
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.Completed);
        }
        else
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.Failed);

        return Save(log, Build(approved, status, records, outputs, null, false));
    }

    public AgentTaskReceipt RecordReconciliation(
        AgentRequest request,
        IAgentRunLog log,
        string disposition,
        string? evidence,
        string actorId,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new InvalidOperationException("Reconciliation requires an actor.");
        var existing = RequireStored(request, log);
        if (existing.Status != AgentTaskStatus.ReconciliationRequired)
            throw new AgentTaskStateException($"A {existing.Status} task is not waiting for reconciliation.");
        var decision = NormalizeDisposition(disposition, evidence);

        var adopted = log.Adopt(existing) ?? throw new AgentTaskNotFoundException("The task was not found.");
        if (adopted.TenantId != request.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(adopted.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        if (adopted.Status != AgentTaskStatus.ReconciliationRequired)
            throw new AgentTaskStateException($"A {adopted.Status} task is not waiting for reconciliation.");

        var decisions = adopted.Decisions?.ToList() ?? [];
        var previous = decisions.LastOrDefault(row => row.Kind == "reconciliation");
        if (previous is not null)
        {
            if (previous.Decision == decision && previous.Evidence == evidence!.Trim() && previous.ActorId == actorId)
                return adopted with { Replayed = true };
            throw new AgentRunConflictException("This task already has a different reconciliation decision.");
        }

        decisions.Add(new TaskDecisionRecord("reconciliation", decision, null, actorId, at, evidence!.Trim()));
        var updated = adopted with { Decisions = decisions, Replayed = false };
        log.Remember(updated);
        return updated;
    }

    private AgentTaskReceipt Run(
        AgentRequest request,
        IAgentRunLog log,
        List<AgentStepRecord> records,
        Dictionary<string, IReadOnlyDictionary<string, string>> outputs,
        bool blocked,
        int startIndex,
        bool gap)
    {
        var status = AgentTaskStatus.Running;
        string? error = null;
        for (var index = startIndex; index < request.Plan.Count; index++)
        {
            var step = request.Plan[index];
            if (gap && !SafeToRepeat(step, out var reason))
            {
                records.Add(new AgentStepRecord(step.StepId, step.ToolName, ToolStepStatus.Uncertain, "Not committed.", reason));
                return Save(log, Build(request, AgentTaskStatus.ReconciliationRequired, records, outputs, reason, false));
            }

            if (!string.IsNullOrWhiteSpace(step.DependsOn))
            {
                var dependency = records.FirstOrDefault(row => row.StepId == step.DependsOn);
                if (dependency is null || dependency.Status != ToolStepStatus.Succeeded)
                {
                    records.Add(new AgentStepRecord(step.StepId, step.ToolName, ToolStepStatus.Failed, "The dependency did not succeed.", "dependency"));
                    error = "A later step depended on a step that did not succeed.";
                    status = AgentTaskTransitions.Move(status, AgentTaskStatus.Failed);
                    return Save(log, Build(request, status, records, outputs, error, false));
                }
            }

            var prior = string.IsNullOrWhiteSpace(step.DependsOn) ? new Dictionary<string, string>() : new Dictionary<string, string>(outputs[step.DependsOn]);
            ToolResponse response;
            try
            {
                response = _dispatcher.Dispatch(_registry, new ToolCall(step.StepId, request.TenantId, prior), request);
            }
            catch (Exception ex)
            {
                response = ToolResponse.Stop(ToolStepStatus.Failed, ex.Message);
            }

            _boundary?.Invoke("after-tool", step.StepId);
            records.Add(new AgentStepRecord(step.StepId, step.ToolName, response.Status, response.Summary, response.Error, response.Outputs));
            outputs[step.StepId] = response.Outputs;
            if (response.Status == ToolStepStatus.Uncertain)
            {
                error = response.Error ?? "The tool effect is unknown and needs reconciliation.";
                status = AgentTaskTransitions.Move(status, AgentTaskStatus.ReconciliationRequired);
                return Save(log, Build(request, status, records, outputs, error, false));
            }

            if (response.Status is ToolStepStatus.Denied or ToolStepStatus.Rejected or ToolStepStatus.Failed)
            {
                error = response.Error;
                status = AgentTaskTransitions.Move(status, AgentTaskStatus.Failed);
                return Save(log, Build(request, status, records, outputs, error, false));
            }

            if (response.Status == ToolStepStatus.Blocked)
                blocked = true;
            else if (response.Outputs.TryGetValue("verified", out var verified) && !string.Equals(verified, "true", StringComparison.Ordinal))
            {
                error = "A step finished without verification.";
                status = AgentTaskTransitions.Move(status, AgentTaskStatus.Failed);
                return Save(log, Build(request, status, records, outputs, error, false));
            }

            Save(log, Build(request, status, records, outputs, error, false));
            _boundary?.Invoke("after-commit", step.StepId);
            gap = false;
        }

        if (status == AgentTaskStatus.Running && blocked)
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.AwaitingApproval);
        else if (status == AgentTaskStatus.Running)
        {
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.Verified);
            Save(log, Build(request, status, records, outputs, error, false));
            status = AgentTaskTransitions.Move(status, AgentTaskStatus.Completed);
        }

        if (status == AgentTaskStatus.Completed && records.Any(row => row.Status != ToolStepStatus.Succeeded))
            throw new InvalidOperationException("A completed task still has an unfinished step.");

        return Save(log, Build(request, status, records, outputs, error, replayed: false));
    }

    private bool SafeToRepeat(AgentStepSpec step, out string reason)
    {
        if (!_registry.TryGet(step.ToolName, out var contract, out _))
        {
            reason = $"Step {step.StepId} is not registered, so its effect cannot be classified.";
            return false;
        }

        if (contract.Effect == ToolEffect.Read)
        {
            reason = "";
            return true;
        }

        reason = $"Step {step.StepId} ({step.ToolName}) has no committed result and its effect is not safe to repeat.";
        return false;
    }

    private (AgentStepRecord Record, ToolResponse Response) DispatchStep(
        AgentRequest request,
        AgentStepSpec step,
        Dictionary<string, IReadOnlyDictionary<string, string>> outputs)
    {
        var prior = string.IsNullOrWhiteSpace(step.DependsOn)
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(outputs[step.DependsOn]);
        ToolResponse response;
        try
        {
            response = _dispatcher.Dispatch(_registry, new ToolCall(step.StepId, request.TenantId, prior), request);
        }
        catch (Exception ex)
        {
            response = ToolResponse.Stop(ToolStepStatus.Failed, ex.Message);
        }

        return (new AgentStepRecord(step.StepId, step.ToolName, response.Status, response.Summary, response.Error, response.Outputs), response);
    }

    private AgentTaskReceipt? StopAfter(
        IAgentRunLog log,
        AgentRequest request,
        List<AgentStepRecord> records,
        Dictionary<string, IReadOnlyDictionary<string, string>> outputs,
        AgentTaskStatus status,
        ToolResponse response,
        string stepId)
    {
        _boundary?.Invoke("after-tool", stepId);
        if (response.Status == ToolStepStatus.Uncertain)
        {
            var reason = response.Error ?? "The tool effect is unknown and needs reconciliation.";
            var next = AgentTaskTransitions.Move(status, AgentTaskStatus.ReconciliationRequired);
            return Save(log, Build(request, next, records, outputs, reason, false));
        }

        if (response.Status is ToolStepStatus.Denied or ToolStepStatus.Rejected or ToolStepStatus.Failed)
        {
            var next = AgentTaskTransitions.Move(status, AgentTaskStatus.Failed);
            return Save(log, Build(request, next, records, outputs, response.Error, false));
        }

        if (response.Status == ToolStepStatus.Blocked)
        {
            var next = AgentTaskTransitions.Move(status, AgentTaskStatus.AwaitingApproval);
            return Save(log, Build(request, next, records, outputs, response.Error, false));
        }

        if (response.Outputs.TryGetValue("verified", out var verified) && !string.Equals(verified, "true", StringComparison.Ordinal))
        {
            var next = AgentTaskTransitions.Move(status, AgentTaskStatus.Failed);
            return Save(log, Build(request, next, records, outputs, "A step finished without verification.", false));
        }

        Save(log, Build(request, status, records, outputs, null, false));
        _boundary?.Invoke("after-commit", stepId);
        return null;
    }

    private bool StepRequiresApproval(AgentStepRecord step)
        => _registry.TryGet(step.ToolName, out var contract, out _) && contract.RequiresApproval;

    private static bool WaitingForApproval(AgentTaskReceipt receipt)
        => receipt.Status == AgentTaskStatus.AwaitingApproval
            || (receipt.Status == AgentTaskStatus.Running && receipt.Steps.Any(step => step.Status == ToolStepStatus.Blocked));

    private static AgentTaskReceipt ReplayApproval(AgentTaskReceipt receipt, ExecutionApproval approval)
    {
        if ((receipt.Status is AgentTaskStatus.Completed or AgentTaskStatus.Failed) && receipt.ApprovalDecisionId == approval.DecisionId)
            return receipt with { Replayed = true };
        throw new AgentTaskStateException($"A {receipt.Status} task cannot be approved.");
    }

    private static ExecutionApproval RequireApproval(AgentRequest request)
    {
        if (request.Approval is not { DecisionId: var id } approval || id == Guid.Empty || string.IsNullOrWhiteSpace(approval.ApprovedBy))
            throw new InvalidOperationException("An approval decision is required.");
        return approval;
    }

    private static AgentTaskReceipt RequireStored(AgentRequest request, IAgentRunLog log)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.TaskKey) || string.IsNullOrWhiteSpace(request.PayloadHash))
            throw new InvalidOperationException("A task requires a tenant, a key, and a payload hash.");
        var existing = log.Find(request.TaskKey) ?? throw new AgentTaskNotFoundException("The task was not found.");
        if (existing.TenantId != request.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(existing.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        return existing;
    }

    private static AgentRequest PreserveDurable(AgentRequest request, AgentTaskReceipt stored)
        => request with
        {
            ExecutionMode = request.ExecutionMode ?? stored.ExecutionMode,
            Decisions = stored.Decisions ?? request.Decisions,
            ApprovedAt = stored.ApprovedAt ?? request.ApprovedAt,
            Approval = request.Approval ?? StoredApproval(stored),
        };

    private static ExecutionApproval? StoredApproval(AgentTaskReceipt receipt)
        => receipt.ApprovalDecisionId is Guid id && id != Guid.Empty && !string.IsNullOrWhiteSpace(receipt.ApprovedBy)
            ? new ExecutionApproval(id, receipt.ApprovedBy)
            : null;

    private static string NormalizeDisposition(string disposition, string? evidence)
    {
        var decision = disposition.Trim().ToLowerInvariant();
        if (decision is not ("occurred" or "absent" or "unknown"))
            throw new InvalidOperationException("Reconciliation disposition must be occurred, absent, or unknown.");
        if (decision == "unknown")
            throw new InvalidOperationException("The side effect is still unknown and cannot be continued.");
        if (string.IsNullOrWhiteSpace(evidence) || evidence.Trim().Length > 500)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(evidence)
                ? "Reconciliation requires evidence of whether the side effect happened."
                : "Reconciliation evidence is too long.");
        return decision;
    }

    private static bool CommittedPrefix(IReadOnlyList<AgentStepSpec> plan, IReadOnlyList<AgentStepRecord> steps)
    {
        if (steps.Count > plan.Count)
            return false;
        for (var index = 0; index < steps.Count; index++)
        {
            if (!string.Equals(steps[index].StepId, plan[index].StepId, StringComparison.Ordinal)
                || !string.Equals(steps[index].ToolName, plan[index].ToolName, StringComparison.Ordinal)
                || steps[index].Status is not (ToolStepStatus.Succeeded or ToolStepStatus.Blocked))
                return false;
        }

        return true;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> OutputsOf(IReadOnlyList<AgentStepRecord> steps)
    {
        var outputs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var step in steps)
            outputs[step.StepId] = step.Outputs ?? new Dictionary<string, string>();
        return outputs;
    }

    private static AgentTaskReceipt Replay(AgentRequest request, AgentTaskReceipt existing)
    {
        if (existing.TenantId != request.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(existing.PayloadHash, request.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
        return existing with { Replayed = true };
    }

    private static AgentTaskReceipt StoreNew(IAgentRunLog log, AgentRequest request, AgentTaskReceipt receipt)
    {
        var existing = log.Claim(receipt);
        if (existing is null)
            return receipt;
        return Replay(request, existing);
    }

    private static AgentTaskReceipt Save(IAgentRunLog log, AgentTaskReceipt receipt)
    {
        log.Remember(receipt);
        return receipt;
    }

    private static AgentTaskReceipt Fail(AgentRequest request, IReadOnlyList<AgentStepRecord> steps, string error)
        => Build(request, AgentTaskStatus.Failed, steps, new Dictionary<string, IReadOnlyDictionary<string, string>>(), error, false);

    private static AgentTaskReceipt Build(
        AgentRequest request,
        AgentTaskStatus status,
        IReadOnlyList<AgentStepRecord> steps,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> outputs,
        string? error,
        bool replayed)
    {
        var flat = outputs.Values.SelectMany(row => row).ToArray();
        var fills = flat.LastOrDefault(pair => pair.Key == "researchFillCount").Value;
        var executed = flat.Any(pair => pair.Key == "recommendationExecuted" && pair.Value == "true");
        var evidence = flat.Where(pair => pair.Key == "evidence").Select(pair => pair.Value).Distinct(StringComparer.Ordinal).ToArray();
        var outcome = flat.LastOrDefault(pair => pair.Key == "outcome").Value;
        if (executed && string.Equals(outcome, "approval_required", StringComparison.Ordinal))
            outcome = "paper_executed";
        var approved = request.Approval is not null && request.Approval.DecisionId != Guid.Empty && !string.IsNullOrWhiteSpace(request.Approval.ApprovedBy);
        return new AgentTaskReceipt(
            request.TaskKey,
            request.PayloadHash,
            request.TenantId,
            status,
            request.Objective,
            request.DoneWhen,
            steps,
            outcome,
            int.TryParse(fills, out var count) ? count : 0,
            executed,
            false,
            replayed,
            error,
            evidence,
            request.Plan,
            approved,
            approved ? request.Approval!.DecisionId : null,
            approved ? request.Approval!.ApprovedBy : null,
            approved ? request.ApprovedAt : null,
            request.ExecutionMode,
            request.Decisions);
    }
}
