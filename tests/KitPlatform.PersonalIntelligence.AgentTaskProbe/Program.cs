using KitPlatform.Packs.PersonalIntelligence.Agent;

var connection = Environment.GetEnvironmentVariable("KIT_AGENT_TASK_CONNECTION");
var key = Environment.GetEnvironmentVariable("KIT_AGENT_TASK_KEY");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(key))
    return 2;

var tenant = Guid.Parse("a1111111-1111-1111-1111-111111111111");
var registry = new ToolRegistry();
registry.Register(
    new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false),
    _ => ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "step-one" }));
var request = new AgentRequest(
    key,
    "payload-" + key,
    tenant,
    "Record a paper recommendation.",
    "The receipt names the outcome.",
    ["personal_intelligence.research.read"],
    [new AgentStepSpec("one", "read", null), new AgentStepSpec("two", "read", "one")],
    null);
using var log = new PostgresAgentRunLog(connection, tenant);
new AgentRuntime(registry).Execute(request, new StopAfterFirstStep(log));
return 0;

file sealed class StopAfterFirstStep(PostgresAgentRunLog inner) : IAgentRunLog
{
    public AgentTaskReceipt? Find(string taskKey) => inner.Find(taskKey);

    public AgentTaskReceipt? Claim(AgentTaskReceipt receipt) => inner.Claim(receipt);

    public void Remember(AgentTaskReceipt receipt)
    {
        inner.Remember(receipt);
        if (receipt.Status == AgentTaskStatus.Running && receipt.Steps.Count >= 1)
            Environment.Exit(87);
    }
}
