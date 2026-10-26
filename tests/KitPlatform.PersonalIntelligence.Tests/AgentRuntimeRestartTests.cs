using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

/// <summary>
/// Reloads a task receipt from a file into a new log. This is not a database and not a new OS process.
/// </summary>
public sealed class AgentRuntimeRestartTests
{
    private static readonly Guid Tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Reloaded_same_payload_returns_the_stored_receipt_without_running_tools()
    {
        var calls = 0;
        using var store = new TempRunFile();
        var request = Request("task-reload", "payload-1", Tenant, [new AgentStepSpec("one", "read", null)], ["personal_intelligence.research.read"]);
        var first = new AgentRuntime(Registry(() => calls++)).Execute(request, new FileAgentRunLog(store.Path));
        var restarted = new AgentRuntime(Registry(() => calls++)).Execute(request, new FileAgentRunLog(store.Path));

        Assert.Equal(AgentTaskStatus.Completed, first.Status);
        Assert.Equal(1, calls);
        Assert.True(restarted.Replayed);
        Assert.Equal(AgentTaskStatus.Completed, restarted.Status);
        Assert.Equal(first.Steps[0].Summary, restarted.Steps[0].Summary);
    }

    [Fact]
    public void Reloaded_different_payload_is_rejected_and_does_not_run_tools()
    {
        var calls = 0;
        using var store = new TempRunFile();
        var request = Request("task-conflict", "payload-1", Tenant, [new AgentStepSpec("one", "read", null)], ["personal_intelligence.research.read"]);
        new AgentRuntime(Registry(() => calls++)).Execute(request, new FileAgentRunLog(store.Path));

        var conflict = Assert.Throws<AgentRunConflictException>(() =>
            new AgentRuntime(Registry(() => calls++)).Execute(
                request with { PayloadHash = "payload-2" },
                new FileAgentRunLog(store.Path)));

        Assert.Equal(1, calls);
        Assert.DoesNotContain("payload-1", conflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reloaded_approval_wait_keeps_its_status_and_does_not_repeat_the_earlier_step()
    {
        var reads = 0;
        var writes = 0;
        using var store = new TempRunFile();
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            reads++;
            return ToolResponse.Succeeded("research stored", new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "session-1" });
        });
        registry.Register(new ToolContract("paper", "personal_intelligence.trading.approve", ToolEffect.Paper, true), _ =>
        {
            writes++;
            return ToolResponse.Succeeded("filled", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var request = Request(
            "task-approval",
            "payload-1",
            Tenant,
            [new AgentStepSpec("one", "read", null), new AgentStepSpec("two", "paper", "one")],
            ["personal_intelligence.research.read", "personal_intelligence.trading.approve"]);
        var first = new AgentRuntime(registry).Execute(request, new FileAgentRunLog(store.Path));
        var restarted = new AgentRuntime(registry).Execute(request, new FileAgentRunLog(store.Path));

        Assert.Equal(AgentTaskStatus.AwaitingApproval, first.Status);
        Assert.Equal(ToolStepStatus.Blocked, first.Steps[1].Status);
        Assert.Equal(1, reads);
        Assert.Equal(0, writes);
        Assert.True(restarted.Replayed);
        Assert.Equal(AgentTaskStatus.AwaitingApproval, restarted.Status);
        Assert.Equal(ToolStepStatus.Blocked, restarted.Steps[1].Status);
        Assert.Equal(1, reads);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void Reloaded_completed_task_does_not_run_the_effect_again()
    {
        var calls = 0;
        using var store = new TempRunFile();
        var request = Request("task-done", "payload-1", Tenant, [new AgentStepSpec("one", "read", null)], ["personal_intelligence.research.read"]);
        var first = new AgentRuntime(Registry(() => calls++)).Execute(request, new FileAgentRunLog(store.Path));
        var restarted = new AgentRuntime(Registry(() => calls++)).Execute(request, new FileAgentRunLog(store.Path));

        Assert.Equal(AgentTaskStatus.Completed, first.Status);
        Assert.False(first.Replayed);
        Assert.True(restarted.Replayed);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Reloaded_failure_keeps_the_failed_step_and_its_error()
    {
        var later = 0;
        using var store = new TempRunFile();
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("first", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
            ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "step-one" }));
        registry.Register(new ToolContract("second", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
            throw new InvalidOperationException("boom"));
        registry.Register(new ToolContract("third", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            later++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var request = Request(
            "task-failed",
            "payload-1",
            Tenant,
            [
                new AgentStepSpec("one", "first", null),
                new AgentStepSpec("two", "second", "one"),
                new AgentStepSpec("three", "third", "two"),
            ],
            ["personal_intelligence.research.read"]);
        var first = new AgentRuntime(registry).Execute(request, new FileAgentRunLog(store.Path));
        var restarted = new AgentRuntime(registry).Execute(request, new FileAgentRunLog(store.Path));

        Assert.Equal(AgentTaskStatus.Failed, restarted.Status);
        Assert.True(restarted.Replayed);
        Assert.Equal(2, restarted.Steps.Count);
        Assert.Equal(ToolStepStatus.Failed, restarted.Steps[1].Status);
        Assert.Contains("boom", restarted.Error, StringComparison.Ordinal);
        Assert.Contains("step-one", restarted.EvidenceRefs);
        Assert.Equal(first.Error, restarted.Error);
        Assert.Equal(0, later);
    }

    [Fact]
    public void Reloaded_task_is_not_visible_to_another_tenant()
    {
        var calls = 0;
        using var store = new TempRunFile();
        var request = Request(
            "task-tenant",
            "payload-1",
            Tenant,
            [new AgentStepSpec("one", "read", null)],
            ["personal_intelligence.research.read"]);
        var stored = new AgentRuntime(Registry(() => calls++, "tenant-a-outcome")).Execute(request, new FileAgentRunLog(store.Path));
        var otherTenant = Guid.Parse("b2222222-2222-2222-2222-222222222222");

        var denied = Assert.Throws<AgentTenantMismatchException>(() =>
            new AgentRuntime(Registry(() => calls++)).Execute(
                request with { TenantId = otherTenant },
                new FileAgentRunLog(store.Path)));

        Assert.Equal(1, calls);
        Assert.DoesNotContain(stored.Steps[0].Summary, denied.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant-a-outcome", denied.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_tool_stays_rejected_after_another_task_was_reloaded()
    {
        var calls = 0;
        using var store = new TempRunFile();
        var done = Request("task-paper", "payload-1", Tenant, [new AgentStepSpec("one", "read", null)], ["personal_intelligence.research.read"]);
        new AgentRuntime(Registry(() => calls++)).Execute(done, new FileAgentRunLog(store.Path));
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("live", "personal_intelligence.trading.approve", ToolEffect.Live, true), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("sent", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var live = Request(
            "task-live",
            "payload-live",
            Tenant,
            [new AgentStepSpec("one", "live", null)],
            ["personal_intelligence.trading.approve"],
            new ExecutionApproval(Guid.NewGuid(), "operator"));

        var receipt = new AgentRuntime(registry).Execute(live, new FileAgentRunLog(store.Path));

        Assert.Equal(1, calls);
        Assert.Equal(AgentTaskStatus.Failed, receipt.Status);
        Assert.Equal(ToolStepStatus.Rejected, receipt.Steps[0].Status);
        Assert.False(receipt.LiveAccepted);
    }

    private static ToolRegistry Registry(Action call, string summary = "ok")
    {
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            call();
            return ToolResponse.Succeeded(summary, new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "step-one" });
        });
        return registry;
    }

    private static AgentRequest Request(
        string key,
        string payload,
        Guid tenant,
        IReadOnlyList<AgentStepSpec> plan,
        IReadOnlyList<string> permissions,
        ExecutionApproval? approval = null)
        => new(key, payload, tenant, "Record a paper recommendation.", "The receipt names the outcome.", permissions, plan, approval);

    private sealed class FileAgentRunLog : IAgentRunLog
    {
        private readonly string _path;
        private readonly Dictionary<string, AgentTaskReceipt> _runs;

        public FileAgentRunLog(string path)
        {
            _path = path;
            if (!File.Exists(path))
            {
                _runs = new Dictionary<string, AgentTaskReceipt>(StringComparer.Ordinal);
                return;
            }

            var loaded = JsonSerializer.Deserialize<List<AgentTaskReceipt>>(File.ReadAllText(path), Json) ?? [];
            _runs = loaded.ToDictionary(row => row.TaskKey, StringComparer.Ordinal);
        }

        public AgentTaskReceipt? Find(string taskKey)
            => _runs.TryGetValue(taskKey, out var receipt) ? receipt : null;

        public AgentTaskReceipt? Claim(AgentTaskReceipt receipt)
        {
            if (_runs.TryGetValue(receipt.TaskKey, out var existing))
            {
                Reject(existing, receipt);
                return existing;
            }

            _runs.Add(receipt.TaskKey, receipt);
            Flush();
            return null;
        }

        public void Remember(AgentTaskReceipt receipt)
        {
            if (_runs.TryGetValue(receipt.TaskKey, out var existing))
            {
                Reject(existing, receipt);
                _runs[receipt.TaskKey] = receipt;
                Flush();
                return;
            }

            _runs.Add(receipt.TaskKey, receipt);
            Flush();
        }

        private static void Reject(AgentTaskReceipt existing, AgentTaskReceipt incoming)
        {
            if (existing.TenantId != incoming.TenantId)
                throw new AgentTenantMismatchException();
            if (!string.Equals(existing.PayloadHash, incoming.PayloadHash, StringComparison.Ordinal))
                throw new AgentRunConflictException();
        }

        private void Flush()
            => File.WriteAllText(_path, JsonSerializer.Serialize(_runs.Values.ToList(), Json));
    }

    private sealed class TempRunFile : IDisposable
    {
        public TempRunFile() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kit-agent-run-" + Guid.NewGuid().ToString("N") + ".json");

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
