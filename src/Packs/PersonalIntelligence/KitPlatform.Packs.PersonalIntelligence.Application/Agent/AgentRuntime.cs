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
    Guid? RequestedTenantId = null);

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
    string? Error);

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
    string? ApprovedBy = null);

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
    public AgentRunConflictException()
        : base("The task key was already used with a different payload.")
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

    public AgentRuntime(ToolRegistry registry) => _registry = registry;

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
            return Replay(request, existing);

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

        var records = new List<AgentStepRecord>();
        var outputs = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var blocked = false;
        string? error = null;

        for (var index = 0; index < request.Plan.Count; index++)
        {
            var step = request.Plan[index];
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

            records.Add(new AgentStepRecord(step.StepId, step.ToolName, response.Status, response.Summary, response.Error));
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
        return new AgentTaskReceipt(
            request.TaskKey,
            request.PayloadHash,
            request.TenantId,
            status,
            request.Objective,
            request.DoneWhen,
            steps,
            flat.LastOrDefault(pair => pair.Key == "outcome").Value,
            int.TryParse(fills, out var count) ? count : 0,
            executed,
            false,
            replayed,
            error,
            evidence,
            request.Plan,
            request.Approval is not null && request.Approval.DecisionId != Guid.Empty && !string.IsNullOrWhiteSpace(request.Approval.ApprovedBy),
            request.Approval?.DecisionId is Guid decision && decision != Guid.Empty ? decision : null,
            request.Approval?.ApprovedBy);
    }
}
