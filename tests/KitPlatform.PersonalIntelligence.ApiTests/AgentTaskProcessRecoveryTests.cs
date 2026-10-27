using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.ApiTests;

public sealed class AgentTaskProcessRecoveryTests : IClassFixture<AgentTaskApiFixture>
{
    private readonly AgentTaskApiFixture _fixture;

    public AgentTaskProcessRecoveryTests(AgentTaskApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_tool_that_returns_before_commit_is_not_called_by_the_next_process()
    {
        var key = "crash-tool-" + Guid.NewGuid().ToString("N");
        var sessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        using var crashed = await StartApi(5391, new Dictionary<string, string>
        {
            ["AgentRuntime__AllowCrashHook"] = "true",
            ["KIT_AGENT_CRASH_AFTER_TOOL"] = "research",
        });
        await PostExpectingExit(crashed, 5391, key, "Stop after the research tool returns.", 86);

        var afterCrashSessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        var afterCrashOperations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var stored = ReadTask(key);
        Assert.Equal("Running", stored.Status);
        Assert.DoesNotContain(stored.Steps, step => step.Tool == "research.session" && step.Status == "Succeeded");
        Assert.True(afterCrashSessions >= sessions);
        Assert.True(afterCrashOperations >= operations);

        using var recovered = await StartApi(5392, null);
        var receipt = await Post(recovered, 5392, key, "Stop after the research tool returns.");
        var finalSessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        var finalOperations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");

        Assert.Equal("ReconciliationRequired", receipt.GetProperty("status").GetString());
        Assert.False(receipt.GetProperty("liveAccepted").GetBoolean());
        Assert.Contains("not safe to repeat", receipt.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(afterCrashSessions, finalSessions);
        Assert.Equal(afterCrashOperations, finalOperations);
        Assert.DoesNotContain("Integration outbox worker started", recovered.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_committed_step_survives_process_exit_and_only_the_read_step_continues()
    {
        var key = "crash-commit-" + Guid.NewGuid().ToString("N");
        using (var crashed = await StartApi(5393, new Dictionary<string, string>
        {
            ["AgentRuntime__AllowCrashHook"] = "true",
            ["KIT_AGENT_CRASH_AFTER_COMMIT"] = "research",
        }))
        {
            await PostExpectingExit(crashed, 5393, key, "Stop after research is stored.", 87);
        }

        var sessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        using (var observer = await StartApi(5394, null))
        {
            var seen = await Get(observer, 5394, key);
            Assert.Equal("Running", seen.GetProperty("status").GetString());
            Assert.False(seen.GetProperty("liveAccepted").GetBoolean());
            Assert.Contains(seen.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "research.session" && step.GetProperty("status").GetString() == "Succeeded");
        }

        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));
        using var recovered = await StartApi(5395, null);
        var receipt = await Post(recovered, 5395, key, "Stop after research is stored.");
        var again = await Post(recovered, 5395, key, "Stop after research is stored.");

        Assert.Equal("AwaitingApproval", receipt.GetProperty("status").GetString());
        Assert.Equal(2, receipt.GetProperty("researchFillCount").GetInt32());
        Assert.False(receipt.GetProperty("recommendationExecuted").GetBoolean());
        Assert.False(receipt.GetProperty("liveAccepted").GetBoolean());
        Assert.Equal("approval_required", receipt.GetProperty("outcome").GetString());
        Assert.Contains(receipt.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "trading.paper_execute" && step.GetProperty("status").GetString() == "Blocked");
        Assert.True(again.GetProperty("replayed").GetBoolean());
        Assert.Equal("AwaitingApproval", again.GetProperty("status").GetString());
        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));
        Assert.DoesNotContain("Integration outbox worker started", recovered.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lock_held_by_another_process_fails_closed_until_that_process_exits()
    {
        var key = "hold-" + Guid.NewGuid().ToString("N");
        using var holder = await StartApi(5396, new Dictionary<string, string>
        {
            ["AgentRuntime__AllowCrashHook"] = "true",
            ["KIT_AGENT_HOLD_AFTER_COMMIT"] = "research",
        });
        using var holding = Client(5396);
        var pending = holding.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Hold the task lock."));
        var until = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < until && !ReadTask(key).Steps.Any(step => step.Tool == "research.session" && step.Status == "Succeeded"))
            await Task.Delay(200);
        Assert.Contains(ReadTask(key).Steps, step => step.Tool == "research.session" && step.Status == "Succeeded");
        var sessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");

        using var waiter = await StartApi(5397, null);
        using var waiting = Client(5397);
        var response = await waiting.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Hold the task lock."));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, body);
        Assert.Contains("still running", body, StringComparison.Ordinal);
        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));

        holder.Process.Kill(entireProcessTree: true);
        holder.Process.WaitForExit(10000);
        try
        {
            await pending;
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }

        using var recovered = await StartApi(5398, null);
        var receipt = await Post(recovered, 5398, key, "Hold the task lock.");
        Assert.Equal("AwaitingApproval", receipt.GetProperty("status").GetString());
        Assert.False(receipt.GetProperty("liveAccepted").GetBoolean());
        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));
    }

    private async Task PostExpectingExit(ApiProcess api, int port, string key, string objective, int exitCode)
    {
        using var client = Client(port);
        try
        {
            await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, objective));
        }
        catch (HttpRequestException)
        {
        }

        if (!api.Process.WaitForExit(20000))
        {
            api.Process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(api.Output.ToString());
        }

        Assert.True(api.Process.ExitCode == exitCode, api.Output.ToString());
        Assert.DoesNotContain("Integration outbox worker started", api.Output.ToString(), StringComparison.Ordinal);
    }

    private async Task<JsonElement> Post(ApiProcess api, int port, string key, string objective)
    {
        using var client = Client(port);
        var response = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, objective));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body + api.Output);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> Get(ApiProcess api, int port, string key)
    {
        using var client = Client(port);
        var response = await client.GetAsync("/api/personal-intelligence/tasks/" + key);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body + api.Output);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private HttpClient Client(int port)
    {
        var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AgentTaskApiFixture.Token(AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA));
        return client;
    }

    private static object Body(string key, string objective)
        => new
        {
            taskKey = key,
            objective,
            doneWhen = "The receipt names the outcome and does not claim a live order.",
            kind = "trading-paper",
        };

    private async Task<ApiProcess> StartApi(int port, IReadOnlyDictionary<string, string>? extra)
    {
        var dll = Path.Combine(FindRepo(), "src", "KitPlatform.Api", "bin", "Debug", "net10.0", "KitPlatform.Api.dll");
        if (!File.Exists(dll))
            throw new InvalidOperationException("The API test host has not been built.");
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "exec \"" + dll + "\"",
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:" + port;
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["ConnectionStrings__Default"] = _fixture.ValidationConnection;
        info.Environment["AgentRuntime__TaskConnection"] = _fixture.TaskConnection;
        info.Environment["AgentRuntime__ResearchConnection"] = _fixture.ResearchConnection;
        info.Environment["ResearchSafeHost__SuppressBackgroundWriters"] = "true";
        info.Environment["IntegrationOutbox__Enabled"] = "false";
        info.Environment["Jwt__Secret"] = AgentTaskApiFixture.Secret;
        info.Environment["Jwt__Issuer"] = "KitPlatform";
        info.Environment["Jwt__Audience"] = "KitPlatform.Client";
        if (extra is not null)
        {
            foreach (var pair in extra)
                info.Environment[pair.Key] = pair.Value;
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, args) => { if (args.Data is not null) output.AppendLine(args.Data); };
        process.ErrorDataReceived += (_, args) => { if (args.Data is not null) output.AppendLine(args.Data); };
        if (!process.Start())
            throw new InvalidOperationException("The API process did not start.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var api = new ApiProcess(process, output);
        using var probe = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(2) };
        var until = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < until)
        {
            if (process.HasExited)
                throw new InvalidOperationException(output.ToString());
            try
            {
                var health = await probe.GetAsync("/api/health");
                if (health.IsSuccessStatusCode)
                    return api;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(250);
        }

        process.Kill(entireProcessTree: true);
        throw new InvalidOperationException(output.ToString());
    }

    private int Count(string connection, string sql)
    {
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Port == 5432)
            throw new InvalidOperationException("Refusing port 5432.");
        using var database = new NpgsqlConnection(builder.ConnectionString);
        database.Open();
        using var command = new NpgsqlCommand(sql, database);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private (string Status, List<(string Tool, string Status)> Steps) ReadTask(string key)
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.TaskConnection);
        if (builder.Port != 5433 || builder.Database != "kit_pi_agent_task")
            throw new InvalidOperationException("Refusing a task database that is not the isolated agent database.");
        using var database = new NpgsqlConnection(builder.ConnectionString);
        database.Open();
        using var command = new NpgsqlCommand(
            "SELECT status, receipt::text FROM pack_personal_intelligence.pi_agent_task WHERE tenant_id = @Tenant AND task_key = @Key",
            database);
        command.Parameters.AddWithValue("Tenant", AgentTaskApiFixture.TenantA);
        command.Parameters.AddWithValue("Key", key);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return ("", []);
        using var document = JsonDocument.Parse(reader.GetString(1));
        var steps = document.RootElement.GetProperty("steps").EnumerateArray()
            .Select(step => (step.GetProperty("toolName").GetString() ?? "", step.GetProperty("status").GetString() ?? ""))
            .ToList();
        return (reader.GetString(0), steps);
    }

    private static string FindRepo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KitPlatform.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private sealed class ApiProcess : IDisposable
    {
        public ApiProcess(Process process, StringBuilder output)
        {
            Process = process;
            Output = output;
        }

        public Process Process { get; }
        public StringBuilder Output { get; }

        public void Dispose()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                Process.WaitForExit(10000);
            }

            Process.Dispose();
        }
    }
}
