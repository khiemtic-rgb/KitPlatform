using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using KitPlatform.SecondBrainAgent.Host;
using Xunit;

namespace KitPlatform.SecondBrainAgent.Tests;

public sealed class DatabaseGuardTests
{
    [Theory]
    [InlineData("Host=127.0.0.1;Port=5433;Database=novixa_prod")]
    [InlineData("Host=127.0.0.1;Port=5433;Database=trading_research")]
    [InlineData("Host=127.0.0.1;Port=5433;Database=NOVIXA_PROD")]
    [InlineData("Host=127.0.0.1;Port=5433;Database=kit_pi_validation")]
    [InlineData("Host=103.200.23.229;Port=5433;Database=second_brain_agent_v1")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=second_brain_agent_v1")]
    public void Connection_outside_the_isolated_database_is_refused(string connectionString)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() => SecondBrainDatabaseGuard.RequireConnection(config));
        Assert.DoesNotContain("Password=", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Worker_flag_true_is_refused_before_a_connection_string_is_required()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PersonalIntelligence:ExchangeBackfill:WorkerEnabled"] = "true",
            ["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=1;Database=novixa_prod",
        }).Build();

        var error = Assert.Throws<InvalidOperationException>(() => SecondBrainDatabaseGuard.RequireWorkersDisabled(config));
        Assert.Contains("WorkerEnabled=true", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Host=;Port=5433;Database=second_brain_agent_v1", "empty host")]
    [InlineData("Host=127.0.0.1,203.0.113.10;Port=5433;Database=second_brain_agent_v1", "host list")]
    [InlineData("Host=/var/run/postgresql;Port=5433;Database=second_brain_agent_v1", "socket path")]
    public void Structural_hosts_are_refused_without_resolution(string connectionString, string expected)
    {
        var resolver = new ExplodingResolver();
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireConnection(Config(connectionString), resolver));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_loopback_name_is_refused_without_a_resolver()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireConnection(Config("Host=db.invalid;Port=5433;Database=second_brain_agent_v1")));
        Assert.Contains("loopback", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fake_resolver_result_for_the_production_address_is_refused()
    {
        var resolver = new FakeResolver { Result = [SecondBrainDatabaseGuard.ProductionApiAddress] };
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireConnection(
                Config("Host=db.invalid;Port=5433;Database=second_brain_agent_v1"),
                resolver));
        Assert.Contains("production API address", error.Message, StringComparison.Ordinal);
        Assert.Equal(["db.invalid"], resolver.Hosts);
    }

    [Fact]
    public void Loopback_literals_pass_the_pre_connect_guard()
    {
        foreach (var host in new[] { "127.0.0.1", "::1" })
        {
            var connection = SecondBrainDatabaseGuard.RequireConnection(
                Config($"Host={host};Port=5433;Database=second_brain_agent_v1"));
            Assert.Contains("second_brain_agent_v1", connection, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Host=localhost;Port=5433;Database=second_brain_agent_v1")]
    [InlineData("Host=db.invalid;Port=5433;Database=second_brain_agent_v1")]
    public void Hostnames_are_refused_without_resolution(string connectionString)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireConnection(Config(connectionString)));
        Assert.Contains("loopback", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5095")]
    [InlineData("http://[::1]:5096")]
    public void Loopback_listen_urls_are_accepted(string urls)
    {
        SecondBrainDatabaseGuard.RequireLoopbackUrls(urls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:5095")]
    [InlineData("http://0.0.0.0:5095")]
    [InlineData("http://203.0.113.10:80")]
    public void Non_loopback_listen_urls_are_refused(string? urls)
    {
        var error = Assert.Throws<InvalidOperationException>(() => SecondBrainDatabaseGuard.RequireLoopbackUrls(urls));
        Assert.Contains("loopback", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Other_loopback_range_addresses_are_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireConnection(Config("Host=127.0.0.2;Port=5433;Database=second_brain_agent_v1")));
        Assert.Contains("loopback", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Endpoint_after_connect_must_match_database_port_and_system_identifier()
    {
        SecondBrainDatabaseGuard.RequireActualEndpoint("second_brain_agent_v1", 5433, "7702079316921501630", 5433, "7702079316921501630");
        Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireActualEndpoint("novixa_prod", 5433, "7702079316921501630", 5433, "7702079316921501630"));
        Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireActualEndpoint("second_brain_agent_v1", 5432, "7702079316921501630", 5433, "7702079316921501630"));
        Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireActualEndpoint("second_brain_agent_v1", null, "7702079316921501630", 5433, "7702079316921501630"));
        Assert.Throws<InvalidOperationException>(() =>
            SecondBrainDatabaseGuard.RequireActualEndpoint("second_brain_agent_v1", 5433, "1", 5433, "7702079316921501630"));
        Assert.Throws<InvalidOperationException>(() => SecondBrainDatabaseGuard.RequireSystemIdentifier("  "));
    }

    static IConfiguration Config(string connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
        }).Build();

    sealed class ExplodingResolver : IHostAddressResolver
    {
        public IReadOnlyList<IPAddress> Resolve(string host) =>
            throw new InvalidOperationException("resolver called");
    }

    sealed class FakeResolver : IHostAddressResolver
    {
        public List<string> Hosts { get; } = [];
        public IReadOnlyList<IPAddress> Result { get; init; } = [];
        public IReadOnlyList<IPAddress> Resolve(string host)
        {
            Hosts.Add(host);
            return Result;
        }
    }
}

public sealed class IsolatedHostTests : IClassFixture<AgentFactory>
{
    readonly AgentFactory _factory;

    public IsolatedHostTests(AgentFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_login_paper_replay_and_research_stay_inside_the_isolated_database()
    {
        using var client = _factory.CreateClient();
        var health = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("second-brain-agent", health.GetProperty("service").GetString());
        Assert.Equal("second_brain_agent_v1", health.GetProperty("database").GetString());
        Assert.False(health.GetProperty("workerEnabled").GetBoolean());
        Assert.False(health.GetProperty("backfillWorkerRegistered").GetBoolean());
        var hosted = health.GetProperty("hostedServices").EnumerateArray().Select(item => item.GetString() ?? "");
        Assert.DoesNotContain(hosted, name => name.Contains("ExchangeBackfill", StringComparison.Ordinal));
        Assert.DoesNotContain(hosted, name => name.Contains("Pharmacy", StringComparison.Ordinal));

        var db = await client.GetFromJsonAsync<JsonElement>("/api/health/db");
        Assert.Equal("second_brain_agent_v1", db.GetProperty("database").GetString());
        Assert.Equal(1, db.GetProperty("value").GetInt32());

        var anonymous = await client.GetAsync("/api/personal-intelligence/trading/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "sb-operator",
            password = AgentFactory.Password,
            tenantCode = "SECOND_BRAIN",
        });
        var loginText = await login.Content.ReadAsStringAsync();
        Assert.True(login.IsSuccessStatusCode, loginText);
        Assert.DoesNotContain(AgentFactory.Password, loginText, StringComparison.Ordinal);
        var session = JsonDocument.Parse(loginText).RootElement;
        var token = session.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var summary = await client.GetAsync("/api/personal-intelligence/trading/summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);

        var key = Guid.NewGuid().ToString("N");
        var body = Loop(key, "Replay " + key);
        var created = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", body));
        var replay = await Read(await client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", body));
        Assert.Equal(created.GetProperty("decisionId").GetGuid(), replay.GetProperty("decisionId").GetGuid());
        Assert.False(created.GetProperty("paperExecuted").GetBoolean());

        var changed = Loop(key, "Different thesis " + key);
        var conflict = await client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", changed);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var live = Loop(Guid.NewGuid().ToString("N"), "Live refused");
        live["live"] = true;
        var refused = await client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", live);
        var refusedText = await refused.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("Live execution is not available.", refusedText, StringComparison.Ordinal);

        var decisionId = created.GetProperty("decisionId").GetGuid();
        var paper = await client.PostAsync($"/api/personal-intelligence/trading/decisions/{decisionId}/paper", null);
        var paperText = await paper.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, paper.StatusCode);
        Assert.Contains("PAPER", paperText, StringComparison.Ordinal);

        var research = await client.GetFromJsonAsync<JsonElement>("/api/personal-intelligence/research/publication");
        Assert.Equal("MISSING", research.GetProperty("readError").GetProperty("code").GetString());
    }

    static Dictionary<string, object?> Loop(string key, string thesis) => new()
    {
        ["personId"] = "33333333-3333-4333-8333-333333333333",
        ["tradingAccountId"] = "55555555-5555-4555-8555-555555555555",
        ["fixtureCsv"] = $"external_trade_id,symbol,side,quantity,price,executed_at\n{key}-1,BTCUSDT,BUY,0.10,100,2026-10-01T00:00:00Z\n",
        ["thesis"] = thesis,
        ["symbol"] = "BTCUSDT",
        ["side"] = "BUY",
        ["quantity"] = 0.1,
        ["price"] = 100,
        ["leverage"] = 1,
        ["confidence"] = 0.8,
        ["maxNotional"] = 1000,
        ["maxLeverage"] = 2,
        ["minConfidence"] = 0.55,
        ["operationKey"] = key,
        ["live"] = false,
    };

    static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}

public sealed class AgentFactory : WebApplicationFactory<Program>
{
    public const string Password = "sb-agent-local-fixture";
    public const string Secret = "second-brain-agent-test-secret-32";

    public AgentFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Default",
            "Host=127.0.0.1;Port=5433;Database=second_brain_agent_v1;Username=pi_validation;Application Name=second-brain-agent-test");
        Environment.SetEnvironmentVariable("SecondBrain__JwtSecret", Secret);
        Environment.SetEnvironmentVariable("SecondBrain__BootstrapAdminPassword", Password);
        Environment.SetEnvironmentVariable("PersonalIntelligence__ExchangeBackfill__WorkerEnabled", "false");
        Environment.SetEnvironmentVariable("PersonalIntelligence__ResearchPublication__Root", "");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        Environment.SetEnvironmentVariable("SecondBrain__SystemIdentifier", ReadLocalSystemIdentifier());
    }

    static string ReadLocalSystemIdentifier()
    {
        using var connection = new NpgsqlConnection(
            "Host=127.0.0.1;Port=5433;Database=second_brain_agent_v1;Username=pi_validation;Application Name=second-brain-agent-test");
        connection.Open();
        using var command = new NpgsqlCommand("SELECT system_identifier::text FROM pg_control_system()", connection);
        return (string)(command.ExecuteScalar()
            ?? throw new InvalidOperationException("Local isolated cluster did not report a system identifier."));
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("environment", "Production");
    }
}
