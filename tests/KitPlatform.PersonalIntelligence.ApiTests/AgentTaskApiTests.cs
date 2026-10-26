using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.ApiTests;

public sealed class AgentTaskApiFixture : IAsyncLifetime
{
    public const string Secret = "kit-agent-task-api-test-secret-32";
    public static readonly Guid TenantA = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantB = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    public static readonly Guid UserA = Guid.Parse("a3333333-3333-3333-3333-333333333333");
    public static readonly Guid UserB = Guid.Parse("b4444444-4444-4444-4444-444444444444");

    public string ValidationConnection { get; private set; } = "";
    public string TaskConnection { get; private set; } = "";
    public string ResearchConnection { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("KIT_PI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("KIT_PI_TEST_CONNECTION is required.");
        var builder = new NpgsqlConnectionStringBuilder(source);
        if (builder.Port != 5433 || builder.Host is not ("127.0.0.1" or "localhost") || builder.Database != "kit_pi_validation")
            throw new InvalidOperationException("Refusing a database that is not the isolated validation database.");
        ValidationConnection = builder.ConnectionString;
        builder.Database = "kit_pi_agent_task";
        TaskConnection = builder.ConnectionString;
        builder.Database = "trading_agent";
        ResearchConnection = builder.ConnectionString;
        await EnsureTaskDatabase();
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ValidationConnection);
        Environment.SetEnvironmentVariable("AgentRuntime__TaskConnection", TaskConnection);
        Environment.SetEnvironmentVariable("AgentRuntime__ResearchConnection", ResearchConnection);
        Environment.SetEnvironmentVariable("ResearchSafeHost__SuppressBackgroundWriters", "true");
        Environment.SetEnvironmentVariable("IntegrationOutbox__Enabled", "false");
        Environment.SetEnvironmentVariable("Jwt__Secret", Secret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "KitPlatform");
        Environment.SetEnvironmentVariable("Jwt__Audience", "KitPlatform.Client");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("Environment", "Development");
            host.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = ValidationConnection,
                    ["AgentRuntime:TaskConnection"] = TaskConnection,
                    ["AgentRuntime:ResearchConnection"] = ResearchConnection,
                    ["ResearchSafeHost:SuppressBackgroundWriters"] = "true",
                    ["IntegrationOutbox:Enabled"] = "false",
                    ["Jwt:Secret"] = Secret,
                    ["Jwt:Issuer"] = "KitPlatform",
                    ["Jwt:Audience"] = "KitPlatform.Client",
                });
            });
            host.ConfigureServices((context, _) =>
            {
                var connection = context.Configuration.GetConnectionString("Default") ?? "";
                var task = context.Configuration["AgentRuntime:TaskConnection"] ?? "";
                var research = context.Configuration["AgentRuntime:ResearchConnection"] ?? "";
                if (!connection.Contains("Database=kit_pi_validation", StringComparison.Ordinal) || connection.Contains("Port=5432", StringComparison.Ordinal))
                    throw new InvalidOperationException("The API test host is not pointed at kit_pi_validation.");
                if (!task.Contains("Database=kit_pi_agent_task", StringComparison.Ordinal) || task.Contains("Port=5432", StringComparison.Ordinal))
                    throw new InvalidOperationException("The task store is not the isolated agent database.");
                if (!research.Contains("Database=trading_agent", StringComparison.Ordinal) || research.Contains("Port=5432", StringComparison.Ordinal))
                    throw new InvalidOperationException("The research ledger is not the isolated trading_agent database.");
            });
        });
        _ = Factory.CreateClient();
        var seen = Factory.Services.GetRequiredService<IConfiguration>();
        var connection = seen.GetConnectionString("Default") ?? "";
        if (!connection.Contains("Database=kit_pi_validation", StringComparison.Ordinal) || connection.Contains("Port=5432", StringComparison.Ordinal))
            throw new InvalidOperationException("The running host is not using the isolated validation database.");
        if (!string.Equals(seen["Jwt:Secret"], Secret, StringComparison.Ordinal))
            throw new InvalidOperationException("The running host is not using the test signing key.");
        if (!string.Equals(seen["ResearchSafeHost:SuppressBackgroundWriters"], "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Background writers were not suppressed.");
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    public HttpClient Client() => Factory.CreateClient();

    public static string Token(Guid userId, Guid tenantId)
    {
        var token = new JwtSecurityToken(
            issuer: "KitPlatform",
            audience: "KitPlatform.Client",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
            ],
            expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task EnsureTaskDatabase()
    {
        var admin = new NpgsqlConnectionStringBuilder(ValidationConnection) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            var current = (string)(await new NpgsqlCommand("SELECT current_database()", connection).ExecuteScalarAsync())!;
            var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", connection).ExecuteScalarAsync());
            if (current != "postgres" || port != 5433)
                throw new InvalidOperationException("Refusing to prepare the task database on this connection.");
            if (await new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = 'kit_pi_agent_task'", connection).ExecuteScalarAsync() is null)
                throw new InvalidOperationException("kit_pi_agent_task must already exist from the isolated task-store test. This API test does not create a second database and does not migrate an existing one.");
        }

        await using var database = new NpgsqlConnection(TaskConnection);
        await database.OpenAsync();
        var name = (string)(await new NpgsqlCommand("SELECT current_database()", database).ExecuteScalarAsync())!;
        if (name != "kit_pi_agent_task")
            throw new InvalidOperationException("The task database was not selected.");
        foreach (var (id, code) in new[] { (TenantA, "AGENT_A"), (TenantB, "AGENT_B") })
        {
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO public.tenants (id, tenant_code, tenant_name)
                VALUES (@Id, @Code, @Code)
                ON CONFLICT (id) DO NOTHING
                """,
                database);
            insert.Parameters.AddWithValue("Id", id);
            insert.Parameters.AddWithValue("Code", code);
            await insert.ExecuteNonQueryAsync();
        }
    }
}

public sealed class AgentTaskApiTests : IClassFixture<AgentTaskApiFixture>
{
    private readonly AgentTaskApiFixture _fixture;

    public AgentTaskApiTests(AgentTaskApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Anonymous_task_request_is_rejected()
    {
        var response = await _fixture.Client().PostAsJsonAsync("/api/personal-intelligence/tasks", Body("anon-task", "Record the paper recommendation."));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_without_permission_is_forbidden()
    {
        var client = Authorized(_fixture.Client(), Guid.NewGuid(), AgentTaskApiFixture.TenantA);
        var response = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body("no-permission", "Record the paper recommendation."));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{response.StatusCode} {response.Headers.WwwAuthenticate} {body}");
    }

    [Fact]
    public async Task Trading_paper_task_is_stored_and_replay_does_not_run_again()
    {
        var key = "api-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var first = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Record the paper recommendation.")));
        var second = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Record the paper recommendation.")));
        var fetched = await Read(await client.GetAsync("/api/personal-intelligence/tasks/" + key));

        Assert.Equal("AwaitingApproval", first.GetProperty("status").GetString());
        Assert.Equal(2, first.GetProperty("researchFillCount").GetInt32());
        Assert.False(first.GetProperty("recommendationExecuted").GetBoolean());
        Assert.False(first.GetProperty("liveAccepted").GetBoolean());
        Assert.False(first.GetProperty("approvalRecorded").GetBoolean());
        Assert.Equal("approval_required", first.GetProperty("outcome").GetString());
        Assert.Contains(first.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "trading.paper_execute" && step.GetProperty("status").GetString() == "Blocked");
        Assert.True(second.GetProperty("replayed").GetBoolean());
        Assert.Equal("AwaitingApproval", second.GetProperty("status").GetString());
        Assert.Equal(first.GetProperty("payloadHash").GetString(), fetched.GetProperty("payloadHash").GetString());
        Assert.Equal(AgentTaskApiFixture.TenantA, fetched.GetProperty("tenantId").GetGuid());
    }

    [Fact]
    public async Task Different_objective_for_the_same_task_conflicts()
    {
        var key = "conflict-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Record the paper recommendation."));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var conflict = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "A different objective."));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Approved_paper_task_executes_on_paper_and_live_stays_rejected()
    {
        var paperKey = "paper-" + Guid.NewGuid().ToString("N");
        var liveKey = "live-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var paper = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(paperKey, "Execute the paper recommendation.", approval: Guid.NewGuid(), mode: "paper")));
        var live = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(liveKey, "Do not send a live order.", approval: Guid.NewGuid(), mode: "live")));

        Assert.Equal("Completed", paper.GetProperty("status").GetString());
        Assert.Equal("paper_executed", paper.GetProperty("outcome").GetString());
        Assert.True(paper.GetProperty("recommendationExecuted").GetBoolean());
        Assert.True(paper.GetProperty("approvalRecorded").GetBoolean());
        Assert.False(paper.GetProperty("liveAccepted").GetBoolean());
        Assert.Equal("Failed", live.GetProperty("status").GetString());
        Assert.False(live.GetProperty("liveAccepted").GetBoolean());
        Assert.Contains(live.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "trading.paper_execute" && step.GetProperty("status").GetString() == "Rejected");
    }

    [Fact]
    public async Task Another_tenant_cannot_read_the_task()
    {
        var key = "tenant-" + Guid.NewGuid().ToString("N");
        var owner = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await owner.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Visible only to tenant A."));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await using var connection = new NpgsqlConnection(_fixture.ValidationConnection);
        await connection.OpenAsync();
        var saved = (string)(await new NpgsqlCommand("SELECT settings::text FROM public.tenants WHERE id = @Id", connection)
        {
            Parameters = { new("Id", AgentTaskApiFixture.TenantB) },
        }.ExecuteScalarAsync())!;
        try
        {
            await new NpgsqlCommand(
                """
                UPDATE public.tenants
                SET settings = settings || '{"platform":{"enabled_modules":["personal_intelligence"]}}'::jsonb
                WHERE id = @Id
                """,
                connection)
            {
                Parameters = { new("Id", AgentTaskApiFixture.TenantB) },
            }.ExecuteNonQueryAsync();

            var other = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var response = await other.GetAsync("/api/personal-intelligence/tasks/" + key);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("Visible only to tenant A.", body, StringComparison.Ordinal);
        }
        finally
        {
            await new NpgsqlCommand("UPDATE public.tenants SET settings = @Settings::jsonb WHERE id = @Id", connection)
            {
                Parameters = { new("Settings", saved), new("Id", AgentTaskApiFixture.TenantB) },
            }.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Tenant_without_the_module_is_forbidden()
    {
        await using var connection = new NpgsqlConnection(_fixture.ValidationConnection);
        await connection.OpenAsync();
        var saved = (string)(await new NpgsqlCommand("SELECT settings::text FROM public.tenants WHERE id = @Id", connection)
        {
            Parameters = { new("Id", AgentTaskApiFixture.TenantB) },
        }.ExecuteScalarAsync())!;
        try
        {
            await new NpgsqlCommand(
                """
                UPDATE public.tenants
                SET settings = settings || '{"platform":{"enabled_modules":["inventory"]}}'::jsonb
                WHERE id = @Id
                """,
                connection)
            {
                Parameters = { new("Id", AgentTaskApiFixture.TenantB) },
            }.ExecuteNonQueryAsync();
            var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var response = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body("module-off", "Do not run this."));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await new NpgsqlCommand("UPDATE public.tenants SET settings = @Settings::jsonb WHERE id = @Id", connection)
            {
                Parameters = { new("Settings", saved), new("Id", AgentTaskApiFixture.TenantB) },
            }.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Concurrent_requests_store_one_task()
    {
        var key = "concurrent-" + Guid.NewGuid().ToString("N");
        var first = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var second = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var body = Body(key, "Record one paper recommendation.");
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/personal-intelligence/tasks", body),
            second.PostAsJsonAsync("/api/personal-intelligence/tasks", body));
        var receipts = new List<JsonElement>();
        foreach (var response in responses)
            receipts.Add(await Read(response));

        Assert.All(receipts, receipt => Assert.Equal(2, receipt.GetProperty("researchFillCount").GetInt32()));
        Assert.Contains(receipts, receipt => receipt.GetProperty("replayed").GetBoolean());
        await using var connection = new NpgsqlConnection(_fixture.TaskConnection);
        await connection.OpenAsync();
        var count = Convert.ToInt32(await new NpgsqlCommand(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE tenant_id = @Tenant AND task_key = @Key",
            connection)
        {
            Parameters = { new("Tenant", AgentTaskApiFixture.TenantA), new("Key", key) },
        }.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Missing_task_store_is_unavailable_without_opening_it()
    {
        var previous = Environment.GetEnvironmentVariable("AgentRuntime__TaskConnection");
        Environment.SetEnvironmentVariable("AgentRuntime__TaskConnection", "Host=127.0.0.1;Port=5432;Database=kitplatform;Username=postgres");
        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            var client = Authorized(factory.CreateClient(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
            var response = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body("refused-store", "Do not open the shared database."));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AgentRuntime__TaskConnection", previous);
        }
    }

    [Fact]
    public async Task Trading_loop_route_stays_hidden()
    {
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var response = await client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_new_host_reads_the_stored_task_without_completing_it()
    {
        var key = "host-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Keep this task waiting.")));
        await using var restarted = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _fixture.ValidationConnection,
                    ["AgentRuntime:TaskConnection"] = _fixture.TaskConnection,
                    ["AgentRuntime:ResearchConnection"] = _fixture.ResearchConnection,
                    ["ResearchSafeHost:SuppressBackgroundWriters"] = "true",
                    ["Jwt:Secret"] = AgentTaskApiFixture.Secret,
                    ["Jwt:Issuer"] = "KitPlatform",
                    ["Jwt:Audience"] = "KitPlatform.Client",
                });
            });
        });
        var again = Authorized(restarted.CreateClient(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var fetched = await Read(await again.GetAsync("/api/personal-intelligence/tasks/" + key));

        Assert.Equal("AwaitingApproval", created.GetProperty("status").GetString());
        Assert.Equal("AwaitingApproval", fetched.GetProperty("status").GetString());
        Assert.False(fetched.GetProperty("liveAccepted").GetBoolean());
        Assert.Equal(created.GetProperty("evidence")[0].GetString(), fetched.GetProperty("evidence")[0].GetString());
    }

    private static HttpClient Authorized(HttpClient client, Guid userId, Guid tenantId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AgentTaskApiFixture.Token(userId, tenantId));
        return client;
    }

    private static object Body(string key, string objective, Guid? approval = null, string? mode = null)
        => new
        {
            taskKey = key,
            objective,
            doneWhen = "The receipt names the outcome and does not claim a live order.",
            kind = "trading-paper",
            executionMode = mode,
            approvalDecisionId = approval,
        };

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
