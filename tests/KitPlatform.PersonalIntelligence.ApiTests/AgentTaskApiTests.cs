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
    public static readonly Guid EvidenceUser = Guid.Parse("d8888888-8888-8888-8888-888888888888");

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
        await EnsureEvidenceAccess();
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

    private async Task EnsureEvidenceAccess()
    {
        await using var connection = new NpgsqlConnection(ValidationConnection);
        await connection.OpenAsync();
        var database = (string)(await new NpgsqlCommand("SELECT current_database()", connection).ExecuteScalarAsync())!;
        var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", connection).ExecuteScalarAsync());
        if (database != "kit_pi_validation" || port != 5433)
            throw new InvalidOperationException("Refusing to grant evidence access outside the isolated validation database.");
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO public.permissions (id, permission_code, permission_name, module_name)
            VALUES ('c8888888-8888-8888-8888-888888888888', 'personal_intelligence.evidence.read', 'Read stored evidence', 'personal_intelligence')
            ON CONFLICT (permission_code) DO NOTHING;

            INSERT INTO public.roles (id, tenant_id, role_code, role_name)
            VALUES
                ('c9999999-9999-9999-9999-999999999999', @TenantA, 'PI_EVIDENCE', 'PI evidence read'),
                ('caaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', @TenantB, 'PI_EVIDENCE', 'PI evidence read')
            ON CONFLICT (tenant_id, role_code) DO NOTHING;

            INSERT INTO public.role_permissions (role_id, permission_id)
            SELECT r.id, p.id
            FROM public.roles r
            JOIN public.permissions p ON p.permission_code = 'personal_intelligence.evidence.read'
            WHERE r.role_code = 'PI_EVIDENCE'
            ON CONFLICT DO NOTHING;

            INSERT INTO public.user_roles (user_id, role_id)
            SELECT u.id, r.id
            FROM public.users u
            JOIN public.roles r ON r.tenant_id = u.tenant_id AND r.role_code = 'PI_EVIDENCE'
            WHERE u.id IN (@UserA, @UserB)
            ON CONFLICT DO NOTHING;

            INSERT INTO public.users (id, tenant_id, username, email, password_hash)
            VALUES (@Evidence, @TenantA, 'pi-evidence-only', 'pi-evidence-only@validation.local', 'not-a-secret')
            ON CONFLICT (id) DO NOTHING;

            DELETE FROM public.user_roles
            WHERE user_id = @Evidence
              AND role_id IN (
                SELECT id FROM public.roles
                WHERE tenant_id = @TenantA AND role_code IN ('PI_TRADING', 'PI_APPROVE'));

            INSERT INTO public.user_roles (user_id, role_id)
            SELECT @Evidence, id
            FROM public.roles
            WHERE tenant_id = @TenantA AND role_code = 'PI_EVIDENCE'
            ON CONFLICT DO NOTHING;
            """,
            connection);
        command.Parameters.AddWithValue("TenantA", TenantA);
        command.Parameters.AddWithValue("TenantB", TenantB);
        command.Parameters.AddWithValue("UserA", UserA);
        command.Parameters.AddWithValue("UserB", UserB);
        command.Parameters.AddWithValue("Evidence", EvidenceUser);
        await command.ExecuteNonQueryAsync();
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

        Assert.Equal("approve", first.GetProperty("nextAction").GetString());
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

    [Fact]
    public async Task Approval_continues_the_same_task_without_running_committed_steps()
    {
        var key = "approve-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Continue this paper task.")));
        var sessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var hash = created.GetProperty("payloadHash").GetString();
        var research = ResearchSummary(created);
        var decision = Guid.NewGuid();
        var conflicting = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Continue this paper task.", approval: decision));
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        var continued = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = decision, executionMode = "live" }));
        var revision = Revision(key);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = decision }));
        var other = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = Guid.NewGuid() });

        Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "research" && step.GetProperty("purpose").GetString()!.Contains("Continue this paper task.", StringComparison.Ordinal));
        Assert.Equal("Continue this paper task.", created.GetProperty("objective").GetString());
        Assert.Equal("Completed", continued.GetProperty("status").GetString());
        Assert.True(continued.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Empty(continued.GetProperty("goal").GetProperty("missing").EnumerateArray());
        Assert.Contains(continued.GetProperty("goal").GetProperty("facts").EnumerateArray(), fact => fact.GetString()!.Contains("not an approved paper order", StringComparison.Ordinal));
        Assert.True(replay.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("paper_executed", continued.GetProperty("outcome").GetString());
        Assert.Equal("none", continued.GetProperty("nextAction").GetString());
        Assert.True(continued.GetProperty("recommendationExecuted").GetBoolean());
        Assert.True(continued.GetProperty("approvalRecorded").GetBoolean());
        Assert.False(continued.GetProperty("liveAccepted").GetBoolean());
        Assert.False(continued.GetProperty("replayed").GetBoolean());
        Assert.Equal(hash, continued.GetProperty("payloadHash").GetString());
        Assert.Equal(research, ResearchSummary(continued));
        Assert.Equal(AgentTaskApiFixture.UserA.ToString("D"), continued.GetProperty("approvedBy").GetString());
        Assert.False(string.IsNullOrWhiteSpace(continued.GetProperty("approvedAt").GetString()));
        Assert.Equal("Paper", continued.GetProperty("executionMode").GetString());
        Assert.Single(continued.GetProperty("decisions").EnumerateArray());
        Assert.Contains(continued.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "trading.paper_execute" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(revision, Revision(key));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));
        Assert.Equal(operations + 1, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
        var result = continued.GetProperty("result");
        Assert.Equal("approval_required", result.GetProperty("snapshotOutcome").GetString());
        Assert.Equal("paper_executed", result.GetProperty("taskOutcome").GetString());
        var paperKey = result.GetProperty("paperOperationKey").GetString();
        var sessionKey = result.GetProperty("researchSessionKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(paperKey));
        Assert.Equal(hash, PaperHash(paperKey!));
        Assert.Equal("approval_required", SnapshotOutcome(sessionKey!));

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
            var outsider = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var hidden = await outsider.GetAsync("/api/personal-intelligence/tasks/" + key);
            var body = await hidden.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.DoesNotContain(paperKey, body, StringComparison.Ordinal);
            Assert.DoesNotContain("Continue this paper task.", body, StringComparison.Ordinal);
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
    public async Task An_unprovable_goal_is_not_completed_and_does_not_execute()
    {
        var key = "unproved-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var criterion = "Profit is at least 10 percent.";
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Research the paper session.", doneWhen: criterion)));
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Research the paper session.", doneWhen: criterion)));

        Assert.Equal("Failed", created.GetProperty("status").GetString());
        Assert.NotEqual("Completed", created.GetProperty("status").GetString());
        Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.False(created.GetProperty("recommendationExecuted").GetBoolean());
        Assert.False(created.GetProperty("liveAccepted").GetBoolean());
        Assert.Equal(criterion, created.GetProperty("doneWhen").GetString());
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == criterion);
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "assess" && step.GetProperty("status").GetString() == "Failed");
        Assert.DoesNotContain(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() is "research" or "execute");
        Assert.Equal("review_the_stated_gap", created.GetProperty("nextAction").GetString());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.False(replay.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task Evidence_review_uses_the_stored_rows_and_does_not_invent_a_missing_kind()
    {
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var presentKey = "evidence-" + Guid.NewGuid().ToString("N");
        var present = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody(presentKey, "The tenant has at least one observation evidence row.")));
        var revision = Revision(presentKey);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody(presentKey, "The tenant has at least one observation evidence row.")));
        var missingKey = "facts-" + Guid.NewGuid().ToString("N");
        var missing = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody(missingKey, "The tenant has at least one fact evidence row.")));
        var unknownKey = "vague-" + Guid.NewGuid().ToString("N");
        var unknown = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody(unknownKey, "Make it good.")));

        Assert.Equal("Completed", present.GetProperty("status").GetString());
        Assert.True(present.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.False(present.GetProperty("recommendationExecuted").GetBoolean());
        Assert.False(present.GetProperty("liveAccepted").GetBoolean());
        Assert.Contains(present.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("OBSERVATION", StringComparison.Ordinal) && item.GetString()!.Contains("pi_evidence", StringComparison.Ordinal));
        Assert.Contains(present.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect" && step.GetProperty("purpose").GetString()!.Contains("Review stored evidence.", StringComparison.Ordinal));
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(revision, Revision(presentKey));
        Assert.Equal("Failed", missing.GetProperty("status").GetString());
        Assert.False(missing.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Contains(missing.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "FACT evidence rows");
        Assert.Contains(missing.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.Contains(missing.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "assess" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.Equal("Failed", unknown.GetProperty("status").GetString());
        Assert.False(unknown.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.DoesNotContain(unknown.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect");
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task Evidence_review_does_not_read_another_tenant()
    {
        var sample = EvidenceScalar("SELECT MIN(source_ref) FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant", AgentTaskApiFixture.TenantA);
        var ownedKey = "evidence-a-" + Guid.NewGuid().ToString("N");
        var owner = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var owned = await Read(await owner.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody(ownedKey, "The tenant has at least one observation evidence row.")));
        Assert.Contains(sample, owned.GetRawText(), StringComparison.Ordinal);
        var key = "evidence-b-" + Guid.NewGuid().ToString("N");
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
            var outsider = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var created = await Read(await outsider.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody(key, "The tenant has at least one observation evidence row.")));
            var hidden = await outsider.GetAsync("/api/personal-intelligence/tasks/" + ownedKey);
            var hiddenBody = await hidden.Content.ReadAsStringAsync();
            var body = created.GetRawText();

            Assert.Equal("Failed", created.GetProperty("status").GetString());
            Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
            Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("0 OBSERVATION", StringComparison.Ordinal));
            Assert.DoesNotContain(sample, body, StringComparison.Ordinal);
            Assert.DoesNotContain(sample, hiddenBody, StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
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
    public async Task A_new_evidence_comparison_is_judged_from_the_stored_counts()
    {
        var observations = EvidenceCount("OBSERVATION", AgentTaskApiFixture.TenantA);
        var facts = EvidenceCount("FACT", AgentTaskApiFixture.TenantA);
        Assert.True(observations > facts);
        Assert.Equal(0, facts);
        var sample = EvidenceScalar("SELECT MIN(source_ref) FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND epistemic_kind = 'OBSERVATION'", AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var sentence = "Observation rows outnumber fact rows.";
        var key = "goal-compare-" + Guid.NewGuid().ToString("N");
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [Ask("obs-over-fact", "OBSERVATION", "greaterThan", "FACT")])));
        var revision = Revision(key);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [Ask("obs-over-fact", "OBSERVATION", "greaterThan", "FACT")])));
        var bothKey = "goal-both-" + Guid.NewGuid().ToString("N");
        var both = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(bothKey, "Both stored kinds are present.", [Ask("observations", "OBSERVATION", "atLeast", "1"), Ask("facts", "FACT", "atLeast", "1")])));

        Assert.NotEqual(EvidenceReviewRuntimeStatement(), sentence);
        Assert.Equal("Completed", created.GetProperty("status").GetString());
        Assert.True(created.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("proven", created.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("none", created.GetProperty("nextAction").GetString());
        Assert.False(created.GetProperty("recommendationExecuted").GetBoolean());
        Assert.False(created.GetProperty("liveAccepted").GetBoolean());
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{observations} OBSERVATION", StringComparison.Ordinal) && item.GetString()!.Contains("pi_evidence", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{facts} FACT", StringComparison.Ordinal));
        Assert.Contains(sample, created.GetRawText(), StringComparison.Ordinal);
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "evidence.collect" && step.GetProperty("purpose").GetString()!.Contains("personal_intelligence.evidence.read", StringComparison.Ordinal));
        Assert.DoesNotContain(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() is "trading.paper_execute" or "research.session");
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(revision, Revision(key));
        Assert.Equal("Failed", both.GetProperty("status").GetString());
        Assert.False(both.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("executed-unproven", both.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("review_unproven_criteria", both.GetProperty("nextAction").GetString());
        Assert.Contains(both.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "FACT evidence rows");
        Assert.Contains(both.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect-FACT" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.Contains(both.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "assess" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task An_unusable_goal_names_the_gap_and_does_not_read_or_invent_a_tool()
    {
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var vagueKey = "goal-vague-" + Guid.NewGuid().ToString("N");
        var vague = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(vagueKey, "Make it good.", [], "Improve the work.")));
        var gapKey = "goal-gap-" + Guid.NewGuid().ToString("N");
        var gap = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(gapKey, "Summarize the evidence payload.", [Ask("payload", "OBSERVATION", "atLeast", "1", "evidence.payload")])));
        var dataKey = "goal-data-" + Guid.NewGuid().ToString("N");
        var data = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(dataKey, "Opinions are present.", [Ask("opinion", "OPINION", "atLeast", "1")])));

        Assert.Equal("Failed", vague.GetProperty("status").GetString());
        Assert.Equal("not-understood", vague.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("state_a_completion_condition", vague.GetProperty("nextAction").GetString());
        Assert.False(vague.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Contains("Make it good.", vague.GetProperty("goal").GetProperty("missing").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain(vague.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString()!.StartsWith("collect", StringComparison.Ordinal));
        Assert.Equal("Failed", gap.GetProperty("status").GetString());
        Assert.Equal("missing-capability", gap.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("no_registered_capability", gap.GetProperty("nextAction").GetString());
        Assert.Contains(gap.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("evidence.payload", StringComparison.Ordinal));
        Assert.DoesNotContain(gap.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "evidence.payload");
        Assert.Equal("Failed", data.GetProperty("status").GetString());
        Assert.Equal("missing-data", data.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("provide_the_missing_condition", data.GetProperty("nextAction").GetString());
        Assert.Contains(data.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("OPINION", StringComparison.Ordinal));
        Assert.DoesNotContain(data.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString()!.StartsWith("collect", StringComparison.Ordinal));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task Evidence_permission_is_required_and_does_not_grant_trading()
    {
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var runner = Guid.Parse("c7777777-7777-7777-7777-777777777777");
        await EnsureRunOnlyUser(runner, AgentTaskApiFixture.TenantA);
        var tradingOnly = Authorized(_fixture.Client(), runner, AgentTaskApiFixture.TenantA);
        var evidenceKey = "goal-denied-" + Guid.NewGuid().ToString("N");
        var deniedGoal = await tradingOnly.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(evidenceKey, "Observation rows outnumber fact rows.", [Ask("obs-over-fact", "OBSERVATION", "greaterThan", "FACT")]));
        var deniedReview = await tradingOnly.PostAsJsonAsync("/api/personal-intelligence/tasks", EvidenceBody("review-denied-" + Guid.NewGuid().ToString("N"), "The tenant has at least one observation evidence row."));
        var evidenceOnly = Authorized(_fixture.Client(), AgentTaskApiFixture.EvidenceUser, AgentTaskApiFixture.TenantA);
        var deniedTrade = await evidenceOnly.PostAsJsonAsync("/api/personal-intelligence/tasks", Body("trade-denied-" + Guid.NewGuid().ToString("N"), "Review the paper fixture."));
        var allowedKey = "goal-evidence-user-" + Guid.NewGuid().ToString("N");
        var allowed = await Read(await evidenceOnly.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(allowedKey, "Observation rows outnumber fact rows.", [Ask("obs-over-fact", "OBSERVATION", "greaterThan", "FACT")])));

        Assert.Equal(HttpStatusCode.Forbidden, deniedGoal.StatusCode);
        Assert.Contains("personal_intelligence.evidence.read", await deniedGoal.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, Count(_fixture.TaskConnection, "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE task_key = '" + evidenceKey + "'"));
        Assert.Equal(HttpStatusCode.Forbidden, deniedReview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedTrade.StatusCode);
        Assert.Contains("personal_intelligence.trading.run", await deniedTrade.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("Completed", allowed.GetProperty("status").GetString());
        Assert.True(allowed.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.False(allowed.GetProperty("recommendationExecuted").GetBoolean());
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task A_goal_comparison_does_not_read_another_tenant()
    {
        var sample = EvidenceScalar("SELECT MIN(source_ref) FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND epistemic_kind = 'OBSERVATION'", AgentTaskApiFixture.TenantA);
        var ownedKey = "goal-a-" + Guid.NewGuid().ToString("N");
        var owner = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var owned = await Read(await owner.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(ownedKey, "Observation rows outnumber fact rows.", [Ask("obs-over-fact", "OBSERVATION", "greaterThan", "FACT")])));
        Assert.Contains(sample, owned.GetRawText(), StringComparison.Ordinal);
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
            var outsider = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var key = "goal-b-" + Guid.NewGuid().ToString("N");
            var created = await Read(await outsider.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, "Observation rows outnumber fact rows.", [Ask("obs-over-fact", "OBSERVATION", "greaterThan", "FACT")])));
            var hidden = await outsider.GetAsync("/api/personal-intelligence/tasks/" + ownedKey);
            var hiddenBody = await hidden.Content.ReadAsStringAsync();

            Assert.Equal("Failed", created.GetProperty("status").GetString());
            Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
            Assert.Equal("executed-unproven", created.GetProperty("goal").GetProperty("understanding").GetString());
            Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("0 OBSERVATION", StringComparison.Ordinal));
            Assert.DoesNotContain(sample, created.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain(sample, hiddenBody, StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
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
    public async Task A_completeness_request_is_judged_from_decision_coverage()
    {
        var coverage = Coverage(AgentTaskApiFixture.TenantA);
        Assert.True(coverage.Decisions >= 1);
        Assert.True(coverage.Linked < coverage.Decisions);
        Assert.Equal(0, coverage.Content);
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var sentence = "Is the stored evidence complete enough to support the tenant's decisions?";
        var key = "complete-" + Guid.NewGuid().ToString("N");
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [])));
        var revision = Revision(key);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [])));
        var recordedKey = "complete-recorded-" + Guid.NewGuid().ToString("N");
        var recorded = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            BusinessBody(recordedKey, "Recorded decisions are the completion criterion.", "decision-recorded")));

        Assert.NotEqual("The tenant has at least one observation evidence row.", sentence);
        Assert.Equal("Failed", created.GetProperty("status").GetString());
        Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("executed-unproven", created.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Decisions} decisions", StringComparison.Ordinal) && item.GetString()!.Contains("pi_decision", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Linked} decisions with evidence", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Content} with a non-empty payload", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "decisions without evidence on the same context");
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "evidence rows with a non-empty payload");
        Assert.Contains(created.GetProperty("goal").GetProperty("limits").EnumerateArray(), item => item.GetString()!.Contains("thesis", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "discover" && step.GetProperty("purpose").GetString()!.Contains("before any read", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "decision.support-coverage" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.DoesNotContain(thesis, created.GetRawText(), StringComparison.Ordinal);
        Assert.False(created.GetProperty("liveAccepted").GetBoolean());
        Assert.False(created.GetProperty("recommendationExecuted").GetBoolean());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(revision, Revision(key));
        Assert.Equal("Completed", recorded.GetProperty("status").GetString());
        Assert.True(recorded.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("proven", recorded.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(recorded.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Decisions} decisions", StringComparison.Ordinal));
        Assert.DoesNotContain(recorded.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("payload", StringComparison.Ordinal));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task An_incomplete_business_request_does_not_read_and_a_thesis_request_is_not_a_tool()
    {
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var partial = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody("partial-" + Guid.NewGuid().ToString("N"), "Check the evidence.", [])));
        var thesisOnly = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            BusinessBody("thesis-" + Guid.NewGuid().ToString("N"), "Compare the thesis.", "thesis-consistent")));
        var runner = Guid.Parse("c7777777-7777-7777-7777-777777777777");
        await EnsureRunOnlyUser(runner, AgentTaskApiFixture.TenantA);
        var deniedKey = "complete-denied-" + Guid.NewGuid().ToString("N");
        var denied = await Authorized(_fixture.Client(), runner, AgentTaskApiFixture.TenantA).PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            GoalBody(deniedKey, "Is the stored evidence complete enough to support the tenant's decisions?", []));

        Assert.Equal("Failed", partial.GetProperty("status").GetString());
        Assert.Equal("missing-data", partial.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(partial.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("subject", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(partial.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect");
        Assert.Equal("Failed", thesisOnly.GetProperty("status").GetString());
        Assert.Equal("missing-capability", thesisOnly.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.DoesNotContain(thesisOnly.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect");
        Assert.DoesNotContain(thesis, thesisOnly.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, Count(_fixture.TaskConnection, "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE task_key = '" + deniedKey + "'"));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task Decision_support_does_not_read_another_tenant()
    {
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var owned = Coverage(AgentTaskApiFixture.TenantA);
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
            var outsider = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var created = await Read(await outsider.PostAsJsonAsync(
                "/api/personal-intelligence/tasks",
                GoalBody("complete-b-" + Guid.NewGuid().ToString("N"), "Is the stored evidence complete enough to support the tenant's decisions?", [])));
            var body = created.GetRawText();

            Assert.Equal("Failed", created.GetProperty("status").GetString());
            Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
            Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("0 decisions", StringComparison.Ordinal));
            Assert.DoesNotContain(thesis, body, StringComparison.Ordinal);
            Assert.DoesNotContain($"{owned.Decisions} decisions", body, StringComparison.Ordinal);
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
    public async Task A_validation_request_does_not_treat_co_location_as_support()
    {
        var quality = Quality(AgentTaskApiFixture.TenantA);
        Assert.True(quality.Evidence >= 1);
        Assert.True(quality.Decisions >= 1);
        Assert.True(quality.Unlinked >= 1);
        Assert.True(quality.Colocated >= 1);
        Assert.Equal(0, quality.Statements);
        Assert.True(quality.Observation >= 1);
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var source = EvidenceScalar(
            "SELECT MIN(source_ref) FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant",
            AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var sentence = "Validate whether the stored evidence supports a specific decision.";
        var key = "validate-" + Guid.NewGuid().ToString("N");
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [])));
        var revision = Revision(key);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [])));

        Assert.Equal("Failed", created.GetProperty("status").GetString());
        Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("unable-to-assess", created.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("cannot_assess_from_stored_rows", created.GetProperty("nextAction").GetString());
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Evidence} evidence rows", StringComparison.Ordinal) && item.GetString()!.Contains($"{quality.Decisions} decisions", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Unlinked} decisions with no context", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Colocated} decisions that share a context", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.EmptyPayload} evidence rows with an empty payload", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Observation} evidence rows whose context summary", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Statements} evidence rows whose payload contains a statement", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "evidence rows with a payload statement");
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "decisions with no context link while evidence exists");
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("supports-decision", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("limits").EnumerateArray(), item => item.GetString()!.Contains("not a support verdict", StringComparison.Ordinal) || item.GetString()!.Contains("cannot prove", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "discover" && step.GetProperty("purpose").GetString()!.Contains("before any read", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "evidence.decision-quality" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.DoesNotContain(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "decision.support-coverage");
        Assert.DoesNotContain(thesis, created.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(source, created.GetRawText(), StringComparison.Ordinal);
        Assert.False(created.GetProperty("liveAccepted").GetBoolean());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(revision, Revision(key));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task Evidence_criteria_stay_distinct_and_a_trading_caller_cannot_validate()
    {
        var quality = Quality(AgentTaskApiFixture.TenantA);
        Assert.True(quality.Unlinked >= 1);
        Assert.True(quality.Observation >= 1);
        Assert.Equal(0, quality.Statements);
        Assert.True(quality.Evidence >= 1);
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var observation = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            ValidationBody("validate-obs-" + Guid.NewGuid().ToString("N"), "The observation statement is the criterion.", "observation-statement")));
        var payload = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            ValidationBody("validate-payload-" + Guid.NewGuid().ToString("N"), "A payload statement is the criterion.", "payload-statement")));
        var relation = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            ValidationBody("validate-link-" + Guid.NewGuid().ToString("N"), "A context relation is the criterion.", "context-relation")));
        var support = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            ValidationBody("validate-support-" + Guid.NewGuid().ToString("N"), "Support is the criterion.", "supports-decision")));
        var partial = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            GoalBody("validate-partial-" + Guid.NewGuid().ToString("N"), "Validate the evidence.", [], "Review the request.")));
        var runner = Guid.Parse("c7777777-7777-7777-7777-777777777777");
        await EnsureRunOnlyUser(runner, AgentTaskApiFixture.TenantA);
        var deniedKey = "validate-denied-" + Guid.NewGuid().ToString("N");
        var denied = await Authorized(_fixture.Client(), runner, AgentTaskApiFixture.TenantA).PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            GoalBody(deniedKey, "Validate whether the stored evidence supports a specific decision.", []));

        Assert.Equal("Completed", observation.GetProperty("status").GetString());
        Assert.True(observation.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("proven", observation.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(observation.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Observation} evidence rows whose context summary", StringComparison.Ordinal));
        Assert.Contains(observation.GetProperty("goal").GetProperty("limits").EnumerateArray(), item => item.GetString()!.Contains("not a support verdict", StringComparison.Ordinal));
        Assert.DoesNotContain(observation.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("payload", StringComparison.Ordinal));
        Assert.Equal("Failed", payload.GetProperty("status").GetString());
        Assert.False(payload.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("executed-unproven", payload.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(payload.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "evidence rows with a payload statement");
        Assert.Contains(payload.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Evidence} evidence rows", StringComparison.Ordinal));
        Assert.DoesNotContain(payload.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("context link", StringComparison.Ordinal));
        Assert.Equal("Failed", relation.GetProperty("status").GetString());
        Assert.Equal("executed-unproven", relation.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(relation.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "decisions with no context link while evidence exists");
        Assert.Contains(relation.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{quality.Evidence} evidence rows", StringComparison.Ordinal));
        Assert.Equal("Failed", support.GetProperty("status").GetString());
        Assert.Equal("missing-capability", support.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.DoesNotContain(support.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect");
        Assert.DoesNotContain(thesis, support.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("Failed", partial.GetProperty("status").GetString());
        Assert.Equal("missing-data", partial.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.DoesNotContain(partial.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "collect");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, Count(_fixture.TaskConnection, "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE task_key = '" + deniedKey + "'"));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task Evidence_validation_does_not_read_another_tenant()
    {
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var owned = Quality(AgentTaskApiFixture.TenantA);
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
            var outsider = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var created = await Read(await outsider.PostAsJsonAsync(
                "/api/personal-intelligence/tasks",
                GoalBody("validate-b-" + Guid.NewGuid().ToString("N"), "Validate whether the stored evidence supports a specific decision.", [])));
            var body = created.GetRawText();

            Assert.Equal("Failed", created.GetProperty("status").GetString());
            Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
            Assert.Equal("unable-to-assess", created.GetProperty("goal").GetProperty("understanding").GetString());
            Assert.Equal("cannot_assess_from_stored_rows", created.GetProperty("nextAction").GetString());
            Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("0 evidence rows", StringComparison.Ordinal));
            Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "evidence rows");
            Assert.DoesNotContain(thesis, body, StringComparison.Ordinal);
            Assert.DoesNotContain($"{owned.Evidence} evidence rows and {owned.Decisions} decisions", body, StringComparison.Ordinal);
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
    public async Task A_two_criterion_request_uses_both_capabilities_and_stays_unproven()
    {
        var observation = EvidenceCount("OBSERVATION", AgentTaskApiFixture.TenantA);
        var fact = EvidenceCount("FACT", AgentTaskApiFixture.TenantA);
        var coverage = Coverage(AgentTaskApiFixture.TenantA);
        Assert.True(observation > fact);
        Assert.True(coverage.Decisions >= 1);
        Assert.True(coverage.Linked < coverage.Decisions);
        Assert.Equal(0, coverage.Content);
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var source = EvidenceScalar(
            "SELECT MIN(source_ref) FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND epistemic_kind = 'OBSERVATION'",
            AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var sentence = "Confirm both that observation evidence outnumbers fact evidence and that the stored evidence is complete enough to support the tenant's decisions.";
        var key = "multi-" + Guid.NewGuid().ToString("N");
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [], "Review the tenant evidence.")));
        var revision = Revision(key);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", GoalBody(key, sentence, [], "Review the tenant evidence.")));

        Assert.Equal("Failed", created.GetProperty("status").GetString());
        Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("executed-unproven", created.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Equal("review_unproven_criteria", created.GetProperty("nextAction").GetString());
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString() == "Criterion observation-over-fact is proven.");
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString() == "Criterion coverage-complete is executed-unproven.");
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{observation} OBSERVATION rows", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{fact} FACT rows", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Decisions} decisions in pack_personal_intelligence.pi_decision", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Linked} decisions with evidence", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{coverage.Content} with a non-empty payload", StringComparison.Ordinal));
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "decisions without evidence on the same context");
        Assert.Contains(created.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString() == "evidence rows with a non-empty payload");
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "evidence.collect" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "decision.support-coverage" && step.GetProperty("status").GetString() == "Succeeded");
        Assert.Contains(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "assess" && step.GetProperty("purpose").GetString()!.Contains("collect-FACT,coverage", StringComparison.Ordinal));
        Assert.DoesNotContain(created.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "trading.paper_execute");
        Assert.DoesNotContain(thesis, created.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(source, created.GetRawText(), StringComparison.Ordinal);
        Assert.False(created.GetProperty("liveAccepted").GetBoolean());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(revision, Revision(key));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task A_single_criterion_is_not_expanded_and_an_unknown_criterion_does_not_read()
    {
        var observation = EvidenceCount("OBSERVATION", AgentTaskApiFixture.TenantA);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var comparison = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            MultiBody("multi-compare-" + Guid.NewGuid().ToString("N"), "Observation rows outnumber fact rows.", "observation-over-fact")));
        var blocked = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            MultiBody("multi-live-" + Guid.NewGuid().ToString("N"), "A live order is the criterion.", "live-order")));
        var partial = await Read(await client.PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            GoalBody("multi-partial-" + Guid.NewGuid().ToString("N"), "Confirm both the evidence.", [], "Review the request.")));
        var runner = Guid.Parse("c7777777-7777-7777-7777-777777777777");
        await EnsureRunOnlyUser(runner, AgentTaskApiFixture.TenantA);
        var deniedKey = "multi-denied-" + Guid.NewGuid().ToString("N");
        var denied = await Authorized(_fixture.Client(), runner, AgentTaskApiFixture.TenantA).PostAsJsonAsync(
            "/api/personal-intelligence/tasks",
            GoalBody(deniedKey, "Confirm both that observation evidence outnumbers fact evidence and that the stored evidence is complete enough to support the tenant's decisions.", [], "Review the tenant evidence."));

        Assert.Equal("Completed", comparison.GetProperty("status").GetString());
        Assert.True(comparison.GetProperty("goal").GetProperty("met").GetBoolean());
        Assert.Equal("proven", comparison.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.Contains(comparison.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains($"{observation} OBSERVATION rows", StringComparison.Ordinal));
        Assert.DoesNotContain(comparison.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "decision.support-coverage");
        Assert.DoesNotContain(comparison.GetProperty("goal").GetProperty("missing").EnumerateArray(), item => item.GetString()!.Contains("payload", StringComparison.Ordinal));
        Assert.Equal("Failed", blocked.GetProperty("status").GetString());
        Assert.Equal("missing-capability", blocked.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.DoesNotContain(blocked.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() is "collect-OBSERVATION" or "coverage");
        Assert.Equal("Failed", partial.GetProperty("status").GetString());
        Assert.Equal("missing-data", partial.GetProperty("goal").GetProperty("understanding").GetString());
        Assert.DoesNotContain(partial.GetProperty("steps").EnumerateArray(), step => step.GetProperty("stepId").GetString() == "coverage");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, Count(_fixture.TaskConnection, "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE task_key = '" + deniedKey + "'"));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task A_multi_criterion_plan_does_not_read_another_tenant()
    {
        var observation = EvidenceCount("OBSERVATION", AgentTaskApiFixture.TenantA);
        var thesis = EvidenceScalar(
            "SELECT thesis FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND length(thesis) > 20 LIMIT 1",
            AgentTaskApiFixture.TenantA);
        var source = EvidenceScalar(
            "SELECT MIN(source_ref) FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND epistemic_kind = 'OBSERVATION'",
            AgentTaskApiFixture.TenantA);
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
            var outsider = Authorized(_fixture.Client(), AgentTaskApiFixture.UserB, AgentTaskApiFixture.TenantB);
            var created = await Read(await outsider.PostAsJsonAsync(
                "/api/personal-intelligence/tasks",
                GoalBody("multi-b-" + Guid.NewGuid().ToString("N"), "Confirm both that observation evidence outnumbers fact evidence and that the stored evidence is complete enough to support the tenant's decisions.", [], "Review the tenant evidence.")));
            var body = created.GetRawText();

            Assert.Equal("Failed", created.GetProperty("status").GetString());
            Assert.False(created.GetProperty("goal").GetProperty("met").GetBoolean());
            Assert.Equal("executed-unproven", created.GetProperty("goal").GetProperty("understanding").GetString());
            Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("0 OBSERVATION rows", StringComparison.Ordinal));
            Assert.Contains(created.GetProperty("goal").GetProperty("facts").EnumerateArray(), item => item.GetString()!.Contains("0 decisions in pack_personal_intelligence.pi_decision", StringComparison.Ordinal));
            Assert.DoesNotContain(thesis, body, StringComparison.Ordinal);
            Assert.DoesNotContain(source, body, StringComparison.Ordinal);
            Assert.DoesNotContain($"{observation} OBSERVATION rows", body, StringComparison.Ordinal);
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
    public async Task Approval_rejects_the_wrong_permission_tenant_and_status()
    {
        var key = "guard-" + Guid.NewGuid().ToString("N");
        var owner = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await Read(await owner.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Visible only to tenant A.")));
        Assert.Equal("AwaitingApproval", created.GetProperty("status").GetString());
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
            var cross = await other.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = Guid.NewGuid() });
            var body = await cross.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
            Assert.DoesNotContain("Visible only to tenant A.", body, StringComparison.Ordinal);
        }
        finally
        {
            await new NpgsqlCommand("UPDATE public.tenants SET settings = @Settings::jsonb WHERE id = @Id", connection)
            {
                Parameters = { new("Settings", saved), new("Id", AgentTaskApiFixture.TenantB) },
            }.ExecuteNonQueryAsync();
        }

        var runner = Guid.Parse("c7777777-7777-7777-7777-777777777777");
        await EnsureRunOnlyUser(runner, AgentTaskApiFixture.TenantA);
        var denied = Authorized(_fixture.Client(), runner, AgentTaskApiFixture.TenantA);
        var forbidden = await denied.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = Guid.NewGuid() });
        var waiting = await Read(await denied.GetAsync("/api/personal-intelligence/tasks/" + key));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("wait_for_approval", waiting.GetProperty("nextAction").GetString());
        Assert.Equal("AwaitingApproval", waiting.GetProperty("status").GetString());

        var completedKey = "done-" + Guid.NewGuid().ToString("N");
        var failedKey = "failed-" + Guid.NewGuid().ToString("N");
        var doneDecision = Guid.NewGuid();
        var failedDecision = Guid.NewGuid();
        var done = await Read(await owner.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(completedKey, "Already finished.", approval: doneDecision)));
        var failed = await Read(await owner.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(failedKey, "Do not send a live order.", approval: failedDecision, mode: "live")));
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        var again = await owner.PostAsJsonAsync("/api/personal-intelligence/tasks/" + completedKey + "/approval", new { approvalDecisionId = Guid.NewGuid() });
        var failedAgain = await owner.PostAsJsonAsync("/api/personal-intelligence/tasks/" + failedKey + "/approval", new { approvalDecisionId = Guid.NewGuid() });
        var same = await Read(await owner.PostAsJsonAsync("/api/personal-intelligence/tasks/" + completedKey + "/approval", new { approvalDecisionId = doneDecision }));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, failedAgain.StatusCode);
        Assert.True(same.GetProperty("replayed").GetBoolean());
        Assert.Equal("Completed", same.GetProperty("status").GetString());
        Assert.False(same.GetProperty("liveAccepted").GetBoolean());
    }

    [Fact]
    public async Task Concurrent_approvals_record_one_decision()
    {
        var key = "race-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Approve this once.")));
        var sessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        var decision = Guid.NewGuid();
        var first = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var second = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = decision }),
            second.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = decision }));
        var receipts = new List<JsonElement>();
        foreach (var response in responses)
            receipts.Add(await Read(response));
        var different = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = Guid.NewGuid() });

        Assert.All(receipts, receipt => Assert.Equal("Completed", receipt.GetProperty("status").GetString()));
        Assert.All(receipts, receipt => Assert.False(receipt.GetProperty("liveAccepted").GetBoolean()));
        Assert.Contains(receipts, receipt => receipt.GetProperty("replayed").GetBoolean());
        Assert.Equal(created.GetProperty("payloadHash").GetString(), receipts[0].GetProperty("payloadHash").GetString());
        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));
        var stored = await Read(await client.GetAsync("/api/personal-intelligence/tasks/" + key));
        Assert.Single(stored.GetProperty("decisions").EnumerateArray());
        Assert.Equal(decision, stored.GetProperty("decisions")[0].GetProperty("decisionId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
    }

    [Fact]
    public async Task Live_approval_cannot_send_an_order()
    {
        var key = "live-wait-" + Guid.NewGuid().ToString("N");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "Do not send a live order.", mode: "live")));
        var continued = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = Guid.NewGuid() }));

        Assert.Equal("AwaitingApproval", created.GetProperty("status").GetString());
        Assert.Equal("Live", created.GetProperty("executionMode").GetString());
        Assert.Equal("Failed", continued.GetProperty("status").GetString());
        Assert.False(continued.GetProperty("liveAccepted").GetBoolean());
        Assert.False(continued.GetProperty("recommendationExecuted").GetBoolean());
        Assert.Equal("Live", continued.GetProperty("executionMode").GetString());
        Assert.Contains(continued.GetProperty("steps").EnumerateArray(), step => step.GetProperty("toolName").GetString() == "trading.paper_execute" && step.GetProperty("status").GetString() == "Rejected");
        Assert.Equal("action_rejected", continued.GetProperty("nextAction").GetString());
    }

    [Fact]
    public async Task Reconciliation_stores_evidence_and_does_not_retry()
    {
        var key = "reconcile-" + Guid.NewGuid().ToString("N");
        var hash = "hash-" + Guid.NewGuid().ToString("N");
        var sessions = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result");
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        await InsertReconciliation(key, hash, "The paper effect is uncertain.");
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var waiting = await Read(await client.GetAsync("/api/personal-intelligence/tasks/" + key));
        var before = Revision(key);
        var missing = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "occurred" });
        var unknown = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "unknown", evidence = "still looking" });
        Assert.Equal(before, Revision(key));
        var recorded = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "absent", evidence = "no paper operation for this task" }));
        var after = Revision(key);
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "absent", evidence = "no paper operation for this task" }));
        var different = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "occurred", evidence = "a fill exists" });
        var approved = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/approval", new { approvalDecisionId = Guid.NewGuid() });
        var retried = await client.PostAsJsonAsync("/api/personal-intelligence/tasks", Body(key, "The paper effect is uncertain."));

        Assert.Equal("ReconciliationRequired", waiting.GetProperty("status").GetString());
        Assert.Equal("manual_reconciliation", waiting.GetProperty("nextAction").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.True(after > before);
        Assert.Equal(after, Revision(key));
        Assert.Equal("ReconciliationRequired", recorded.GetProperty("status").GetString());
        Assert.False(recorded.GetProperty("replayed").GetBoolean());
        Assert.Equal("absent", recorded.GetProperty("decisions")[0].GetProperty("decision").GetString());
        Assert.Equal("no paper operation for this task", recorded.GetProperty("decisions")[0].GetProperty("evidence").GetString());
        Assert.Equal("unknown", recorded.GetProperty("decisions")[0].GetProperty("observedResult").GetString());
        Assert.Equal("not_available", recorded.GetProperty("decisions")[0].GetProperty("checkSource").GetString());
        Assert.Equal(AgentTaskApiFixture.UserA.ToString("D"), recorded.GetProperty("decisions")[0].GetProperty("actorId").GetString());
        Assert.Equal("Uncertain", recorded.GetProperty("steps")[0].GetProperty("status").GetString());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Single(replay.GetProperty("decisions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, retried.StatusCode);
        Assert.Equal(sessions, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.session_result"));
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
        Assert.Equal("absent", (await Read(await client.GetAsync("/api/personal-intelligence/tasks/" + key))).GetProperty("decisions")[0].GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Concurrent_reconciliation_records_one_decision_and_does_not_retry()
    {
        var key = "recon-race-" + Guid.NewGuid().ToString("N");
        var hash = "hash-" + Guid.NewGuid().ToString("N");
        await InsertReconciliation(key, hash, "Two checks of one uncertain step.");
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        var first = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var second = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var body = new { disposition = "absent", evidence = "no task operation" };
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", body),
            second.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", body));
        var receipts = new List<JsonElement>();
        foreach (var response in responses)
            receipts.Add(await Read(response));
        var different = await first.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "occurred", evidence = "no task operation" });

        Assert.All(receipts, receipt => Assert.Equal("ReconciliationRequired", receipt.GetProperty("status").GetString()));
        Assert.Contains(receipts, receipt => receipt.GetProperty("replayed").GetBoolean());
        Assert.All(receipts, receipt => Assert.Equal("unknown", receipt.GetProperty("decisions")[0].GetProperty("observedResult").GetString()));
        var stored = await Read(await first.GetAsync("/api/personal-intelligence/tasks/" + key));
        Assert.Single(stored.GetProperty("decisions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
        Assert.Equal(operations, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
    }

    [Fact]
    public async Task An_operator_note_does_not_override_the_stored_paper_operation()
    {
        var key = "recon-seen-" + Guid.NewGuid().ToString("N");
        var hash = "hash-" + Guid.NewGuid().ToString("N");
        var decision = Guid.Parse("d6666666-6666-6666-6666-666666666666");
        var operationKey = $"task:{AgentTaskApiFixture.TenantA:N}:{key}:{decision:N}";
        await InsertReconciliation(key, hash, "Check the stored paper operation.", decision);
        var operations = Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation");
        await InsertPaperOperation(operationKey, hash);
        var client = Authorized(_fixture.Client(), AgentTaskApiFixture.UserA, AgentTaskApiFixture.TenantA);
        var recorded = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "absent", evidence = "I did not see a fill" }));
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "absent", evidence = "I did not see a fill" }));
        var opposite = await client.PostAsJsonAsync("/api/personal-intelligence/tasks/" + key + "/reconciliation", new { disposition = "occurred", evidence = "I did not see a fill" });

        Assert.Equal("ReconciliationRequired", recorded.GetProperty("status").GetString());
        Assert.Equal("absent", recorded.GetProperty("decisions")[0].GetProperty("decision").GetString());
        Assert.Equal("I did not see a fill", recorded.GetProperty("decisions")[0].GetProperty("evidence").GetString());
        Assert.Equal("occurred", recorded.GetProperty("decisions")[0].GetProperty("observedResult").GetString());
        Assert.Equal("paper_operation", recorded.GetProperty("decisions")[0].GetProperty("checkSource").GetString());
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Single(replay.GetProperty("decisions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, opposite.StatusCode);
        Assert.Equal(operations + 1, Count(_fixture.ResearchConnection, "SELECT COUNT(*) FROM research_trading.paper_operation"));
        Assert.Equal(hash, PaperHash(operationKey));
    }

    private static HttpClient Authorized(HttpClient client, Guid userId, Guid tenantId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AgentTaskApiFixture.Token(userId, tenantId));
        return client;
    }

    private static object EvidenceBody(string key, string doneWhen)
        => new
        {
            taskKey = key,
            objective = "Review stored evidence.",
            doneWhen,
            kind = "evidence-review",
        };

    private static object GoalBody(string key, string doneWhen, object[] asks, string objective = "Compare stored evidence for this tenant.")
        => new
        {
            taskKey = key,
            objective,
            doneWhen,
            kind = "goal",
            asks,
        };

    private static object BusinessBody(string key, string doneWhen, params string[] needs)
        => new
        {
            taskKey = key,
            objective = "Assess whether stored records support a decision.",
            doneWhen,
            kind = "goal",
            business = new { intent = "assess-completeness", subject = "decision-support", needs },
        };

    private static object MultiBody(string key, string doneWhen, params string[] needs)
        => new
        {
            taskKey = key,
            objective = "Review the tenant evidence.",
            doneWhen,
            kind = "goal",
            business = new { intent = "plan-criteria", subject = "tenant-evidence", needs },
        };

    private static object ValidationBody(string key, string doneWhen, params string[] needs)
        => new
        {
            taskKey = key,
            objective = "Validate stored evidence against a decision.",
            doneWhen,
            kind = "goal",
            business = new { intent = "validate-evidence", subject = "decision-support", needs },
        };

    private QualityNumbers Quality(Guid tenantId)
    {
        var decisions = int.Parse(EvidenceScalar("SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant", tenantId));
        var unlinked = int.Parse(EvidenceScalar("SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND context_id IS NULL", tenantId));
        var colocated = int.Parse(EvidenceScalar(
            """
            SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_decision d
            WHERE d.tenant_id = @Tenant AND d.context_id IS NOT NULL
              AND EXISTS (
                SELECT 1 FROM pack_personal_intelligence.pi_evidence e
                WHERE e.tenant_id = d.tenant_id AND e.context_id = d.context_id)
            """,
            tenantId));
        var evidence = int.Parse(EvidenceScalar("SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant", tenantId));
        var empty = int.Parse(EvidenceScalar("SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND payload = '{}'::jsonb", tenantId));
        var observation = int.Parse(EvidenceScalar(
            """
            SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_evidence e
            JOIN pack_personal_intelligence.pi_context c
              ON c.context_id = e.context_id AND c.tenant_id = e.tenant_id
            WHERE e.tenant_id = @Tenant
              AND c.summary = 'Các giao dịch đã lưu được chọn làm dữ liệu quan sát. Đây chưa phải một quyết định.'
            """,
            tenantId));
        var statements = int.Parse(EvidenceScalar(
            """
            SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_evidence
            WHERE tenant_id = @Tenant
              AND jsonb_typeof(payload) = 'object'
              AND jsonb_exists(payload, 'statement')
              AND length(btrim(payload->>'statement')) > 0
            """,
            tenantId));
        return new QualityNumbers(decisions, unlinked, colocated, evidence, empty, observation, statements);
    }

    private readonly record struct QualityNumbers(int Decisions, int Unlinked, int Colocated, int Evidence, int EmptyPayload, int Observation, int Statements);

    private CoverageNumbers Coverage(Guid tenantId)
    {
        var decisions = int.Parse(EvidenceScalar("SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant", tenantId));
        var linked = int.Parse(EvidenceScalar(
            """
            SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_decision d
            WHERE d.tenant_id = @Tenant
              AND EXISTS (
                SELECT 1 FROM pack_personal_intelligence.pi_evidence e
                WHERE e.tenant_id = d.tenant_id AND e.context_id = d.context_id)
            """,
            tenantId));
        var content = int.Parse(EvidenceScalar(
            "SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND payload <> '{}'::jsonb",
            tenantId));
        return new CoverageNumbers(decisions, linked, content);
    }

    private readonly record struct CoverageNumbers(int Decisions, int Linked, int Content);

    private static object Ask(string id, string subject, string relation, string target, string capability = "evidence.count")
        => new { id, capability, subject, relation, target };

    private static string EvidenceReviewRuntimeStatement()
        => "The tenant has at least one observation evidence row.";

    private int EvidenceCount(string kind, Guid tenantId)
    {
        if (kind is not ("OBSERVATION" or "FACT"))
            throw new InvalidOperationException("The test only counts stored evidence kinds.");
        return int.Parse(EvidenceScalar(
            "SELECT COUNT(*)::text FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant AND epistemic_kind = '" + kind + "'",
            tenantId));
    }

    private string EvidenceScalar(string sql, Guid tenantId)
    {
        using var connection = new NpgsqlConnection(_fixture.ValidationConnection);
        connection.Open();
        using var command = new NpgsqlCommand("SELECT current_database(), inet_server_port()", connection);
        using var probe = command.ExecuteReader();
        probe.Read();
        if (probe.GetString(0) != "kit_pi_validation" || probe.GetInt32(1) != 5433)
            throw new InvalidOperationException("Refusing to read evidence outside the isolated validation database.");
        probe.Close();
        using var query = new NpgsqlCommand(sql, connection);
        query.Parameters.AddWithValue("Tenant", tenantId);
        return (string)query.ExecuteScalar()!;
    }

    private static object Body(string key, string objective, Guid? approval = null, string? mode = null, string? doneWhen = null)
        => new
        {
            taskKey = key,
            objective,
            doneWhen = doneWhen ?? "The receipt names the outcome and does not claim a live order.",
            kind = "trading-paper",
            executionMode = mode,
            approvalDecisionId = approval,
        };

    private static string ResearchSummary(JsonElement receipt)
        => receipt.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("toolName").GetString() == "research.session").GetProperty("summary").GetString()!;

    private int Revision(string key)
    {
        using var connection = new NpgsqlConnection(_fixture.TaskConnection);
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT revision FROM pack_personal_intelligence.pi_agent_task WHERE tenant_id = @Tenant AND task_key = @Key",
            connection);
        command.Parameters.AddWithValue("Tenant", AgentTaskApiFixture.TenantA);
        command.Parameters.AddWithValue("Key", key);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private string PaperHash(string operationKey)
    {
        return ResearchScalar(
            "SELECT payload_hash FROM research_trading.paper_operation WHERE operation_key = @Key",
            operationKey);
    }

    private string SnapshotOutcome(string sessionKey)
    {
        var result = ResearchScalar(
            "SELECT result FROM research_trading.session_result WHERE session_key = @Key",
            sessionKey);
        using var document = JsonDocument.Parse(result);
        return document.RootElement.GetProperty("Receipt").GetProperty("Outcome").GetString()!;
    }

    private string ResearchScalar(string sql, string key)
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ResearchConnection);
        if (builder.Port != 5433 || builder.Database != "trading_agent")
            throw new InvalidOperationException("Refusing a research read outside trading_agent.");
        using var connection = new NpgsqlConnection(_fixture.ResearchConnection);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("Key", key);
        return (string)command.ExecuteScalar()!;
    }

    private static int Count(string connectionString, string sql)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Port != 5433)
            throw new InvalidOperationException("Refusing a count outside the isolated database.");
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return Convert.ToInt32(new NpgsqlCommand(sql, connection).ExecuteScalar());
    }

    private async Task InsertPaperOperation(string operationKey, string hash)
    {
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ResearchConnection);
        if (builder.Port != 5433 || builder.Database != "trading_agent")
            throw new InvalidOperationException("Refusing a paper insert outside trading_agent.");
        await using var connection = new NpgsqlConnection(_fixture.ResearchConnection);
        await connection.OpenAsync();
        var database = (string)(await new NpgsqlCommand("SELECT current_database()", connection).ExecuteScalarAsync())!;
        var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", connection).ExecuteScalarAsync());
        if (database != "trading_agent" || port != 5433)
            throw new InvalidOperationException("Refusing a paper insert outside trading_agent.");
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO research_trading.paper_operation (operation_key, payload_hash, status, result)
            VALUES (@Key, @Hash, 'paper_filled', @Result)
            """,
            connection);
        command.Parameters.AddWithValue("Key", operationKey);
        command.Parameters.AddWithValue("Hash", hash);
        command.Parameters.AddWithValue("Result", "stored-before-reconciliation");
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertReconciliation(string key, string hash, string objective, Guid? decisionId = null)
    {
        var receipt = $$"""
            {
              "taskKey": "{{key}}",
              "payloadHash": "{{hash}}",
              "tenantId": "{{AgentTaskApiFixture.TenantA}}",
              "status": "ReconciliationRequired",
              "objective": "{{objective}}",
              "doneWhen": "The receipt names the outcome and does not claim a live order.",
              "steps": [
                {
                  "stepId": "execute",
                  "toolName": "trading.paper_execute",
                  "status": "Uncertain",
                  "summary": "Not committed.",
                  "error": "Step execute (trading.paper_execute) has no committed result and its effect is not safe to repeat."
                }
              ],
              "outcome": null,
              "researchFillCount": 0,
              "recommendationExecuted": false,
              "liveAccepted": false,
              "replayed": false,
              "error": "Step execute (trading.paper_execute) has no committed result and its effect is not safe to repeat.",
              "evidenceRefs": [],
              "approvalRecorded": {{(decisionId is null ? "false" : "true")}},
              "approvalDecisionId": {{(decisionId is Guid id ? $"\"{id:D}\"" : "null")}},
              "approvedBy": {{(decisionId is null ? "null" : "\"operator\"")}}
            }
            """;
        await using var connection = new NpgsqlConnection(_fixture.TaskConnection);
        await connection.OpenAsync();
        var database = (string)(await new NpgsqlCommand("SELECT current_database()", connection).ExecuteScalarAsync())!;
        var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", connection).ExecuteScalarAsync());
        if (database != "kit_pi_agent_task" || port != 5433)
            throw new InvalidOperationException("Refusing to insert a task outside the isolated task database.");
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO pack_personal_intelligence.pi_agent_task
                (tenant_id, task_key, payload_hash, status, receipt, revision)
            VALUES
                (@Tenant, @Key, @Hash, 'ReconciliationRequired', @Receipt::jsonb, 1)
            """,
            connection);
        command.Parameters.AddWithValue("Tenant", AgentTaskApiFixture.TenantA);
        command.Parameters.AddWithValue("Key", key);
        command.Parameters.AddWithValue("Hash", hash);
        command.Parameters.AddWithValue("Receipt", receipt);
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureRunOnlyUser(Guid userId, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ValidationConnection);
        await connection.OpenAsync();
        var database = (string)(await new NpgsqlCommand("SELECT current_database()", connection).ExecuteScalarAsync())!;
        var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", connection).ExecuteScalarAsync());
        if (database != "kit_pi_validation" || port != 5433)
            throw new InvalidOperationException("Refusing to add a user outside the isolated validation database.");
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO public.users (id, tenant_id, username, email, password_hash)
            VALUES (@User, @Tenant, 'pi-run-only', 'pi-run-only@validation.local', 'not-a-secret')
            ON CONFLICT (id) DO NOTHING;

            DELETE FROM public.user_roles
            WHERE user_id = @User
              AND role_id IN (
                SELECT id FROM public.roles
                WHERE tenant_id = @Tenant AND role_code IN ('PI_APPROVE', 'PI_EVIDENCE'));

            INSERT INTO public.user_roles (user_id, role_id)
            SELECT @User, id
            FROM public.roles
            WHERE tenant_id = @Tenant AND role_code = 'PI_TRADING'
            ON CONFLICT DO NOTHING;
            """,
            connection);
        command.Parameters.AddWithValue("User", userId);
        command.Parameters.AddWithValue("Tenant", tenantId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
