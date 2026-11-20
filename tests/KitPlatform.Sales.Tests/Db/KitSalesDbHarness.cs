using System.Text.RegularExpressions;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace KitPlatform.Sales.Tests.Db;

/// <summary>
/// Runs only against a throwaway database built by build-kit-sales-test-db.ps1.
/// Refuses port 5432 and any database not named kit_sales_ci*.
/// </summary>
public static class KitSalesTestDb
{
    public const string EnvVar = "KIT_SALES_TEST_CONNECTION";
    private static readonly Regex AllowedName = new("^kit_sales_ci[a-z0-9_]*$", RegexOptions.Compiled);

    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

    public static string ConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVar)
            ?? throw new InvalidOperationException($"{EnvVar} is not set.");
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Port == 5432)
            throw new InvalidOperationException("Refusing port 5432; use the throwaway test cluster.");
        if (builder.Database is null || !AllowedName.IsMatch(builder.Database))
            throw new InvalidOperationException("Refusing a database not named kit_sales_ci*.");
        return raw;
    }
}

/// <summary>Skips locally without a test database. In CI a missing database is a failure, not a skip.</summary>
public sealed class DbFactAttribute : FactAttribute
{
    private static bool RunningInCi =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public DbFactAttribute()
    {
        if (!KitSalesTestDb.Configured && !RunningInCi)
            Skip = $"{KitSalesTestDb.EnvVar} not set; DB integration skipped.";
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class KitSalesDbCollection : ICollectionFixture<KitSalesDbFixture>
{
    public const string Name = "kit-sales-db";
}

public sealed class KitSalesDbFixture
{
    public KitSalesDbFixture()
    {
        if (!KitSalesTestDb.Configured
            && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            return;
        ConnectionString = KitSalesTestDb.ConnectionString();
        Db = new NpgsqlConnectionFactory(ConnectionString);
        using var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        var tenantRows = conn.ExecuteScalar<int>(
            "SELECT COUNT(*)::int FROM tenants WHERE id = @Id",
            new { Id = SalesPackDefinition.DedicatedTenantId });
        if (tenantRows != 1)
            throw new InvalidOperationException("KIT_SALES tenant missing; rebuild the test database.");
        UserId = conn.ExecuteScalar<Guid?>(
            "SELECT id FROM users WHERE tenant_id = @Id ORDER BY created_at LIMIT 1",
            new { Id = SalesPackDefinition.DedicatedTenantId }) ?? Guid.NewGuid();
        conn.Execute(
            """
            DELETE FROM pack_sales.facebook_settings WHERE tenant_id = @Id;
            DELETE FROM pack_sales.ai_settings WHERE tenant_id = @Id;
            DELETE FROM pack_sales.agent_run WHERE tenant_id = @Id AND agent_code = @Ai;
            """,
            new { Id = SalesPackDefinition.DedicatedTenantId, Ai = SalesAiBudget.AgentCode });
    }

    public string ConnectionString { get; } = "";
    public IDbConnectionFactory Db { get; } = null!;
    public Guid UserId { get; }

    public NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }
}

internal sealed class TestTenant : ITenantContext
{
    public TestTenant(Guid userId) => UserId = userId;
    public Guid TenantId => SalesPackDefinition.DedicatedTenantId;
    public Guid UserId { get; }
    public bool IsAuthenticated => true;
    public Guid? WorkspaceId => null;
}

/// <summary>Webhook requests carry no tenant claim. Any read of the tenant fails the test.</summary>
internal sealed class AnonymousTenant : ITenantContext
{
    public Guid TenantId => throw new UnauthorizedAccessException("no tenant claim");
    public Guid UserId => Guid.Empty;
    public bool IsAuthenticated => false;
    public Guid? WorkspaceId => null;
}

internal sealed class TestAudit : IAuditLogService
{
    public Func<string, Task>? OnWrite { get; set; }
    public int Writes { get; private set; }

    public async Task WriteAsync(
        string entityType,
        Guid? entityId,
        string action,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        Writes++;
        if (OnWrite is not null)
            await OnWrite(entityType);
    }
}

internal sealed class CountingHandler : HttpMessageHandler
{
    private int _calls;
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => throw new InvalidOperationException("No HTTP expected.");
    public int Calls => Volatile.Read(ref _calls);
    public List<Uri?> Uris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        lock (Uris)
            Uris.Add(request.RequestUri);
        return Respond(request, cancellationToken);
    }

    public static HttpResponseMessage Json(int status, string body) =>
        new((System.Net.HttpStatusCode)status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
}

internal sealed class TestHttpFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public TestHttpFactory(HttpMessageHandler handler) => _handler = handler;
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

/// <summary>Real KIT SALES services on the test database. Only HTTP and audit are fakes.</summary>
internal sealed class KitSalesServices
{
    public KitSalesServices(
        KitSalesDbFixture fx,
        CountingHandler? http = null,
        bool channelOn = true,
        bool settingsEnabled = true,
        ITenantContext? tenant = null,
        TestAudit? audit = null,
        long aiBudget = 0)
    {
        Http = http ?? new CountingHandler();
        Audit = audit ?? new TestAudit();
        Tenant = tenant ?? new TestTenant(fx.UserId);
        var factory = new TestHttpFactory(Http);
        Config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [FacebookOutboundRules.ChannelFlagKey] = channelOn ? "true" : "false",
                [SalesAiBudget.ConfigKey] = aiBudget.ToString(System.Globalization.CultureInfo.InvariantCulture),
            })
            .Build();
        Facebook = new KitSalesFacebookSettingsService(
            fx.Db,
            factory,
            Options.Create(new KitSalesFacebookOptions { Enabled = settingsEnabled, PageAccessToken = "test-page-token" }),
            Audit);
        var assessments = new KitSalesAssessmentLookup(fx.Db);
        Desk = new KitSalesDeskService(fx.Db, Tenant, assessments);
        Gate = new FacebookOutboundGate(fx.Db, Tenant, Config, factory, Facebook);
        Pains = new KitSalesPainService(fx.Db);
        Ai = new KitSalesAiSettingsService(fx.Db, factory, Config, Audit);
        var voice = new KitSalesGeminiVoice(new HttpClient(Http, false), Ai, NullLogger<KitSalesGeminiVoice>.Instance);
        Chat = new KitSalesChatService(fx.Db, Desk, Pains, Gate, voice, Facebook);
        Agent = new KitSalesAgentService(fx.Db, Tenant, Facebook, assessments);
    }

    public CountingHandler Http { get; }
    public TestAudit Audit { get; }
    public ITenantContext Tenant { get; }
    public IConfiguration Config { get; }
    public KitSalesFacebookSettingsService Facebook { get; }
    public KitSalesDeskService Desk { get; }
    public FacebookOutboundGate Gate { get; }
    public KitSalesPainService Pains { get; }
    public KitSalesAiSettingsService Ai { get; }
    public KitSalesChatService Chat { get; }
    public KitSalesAgentService Agent { get; }
}

internal static class KitSalesSeed
{
    private static long _psid = 900000000000000 + Random.Shared.Next(0, 99999) * 1000L;

    public static string NewPsid() => Interlocked.Increment(ref _psid).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>One Messenger message through the real webhook handler. Returns the lead and its newest draft.</summary>
    public static async Task<(Guid LeadId, Guid? DraftId, string Psid)> InboundAsync(
        KitSalesDbFixture fx,
        KitSalesServices services,
        long? sentAtMs = null,
        string? psid = null,
        string? mid = null)
    {
        psid ??= NewPsid();
        await services.Chat.HandleFacebookInboundAsync(
            psid, "Cho mình hỏi giá phần mềm", mid ?? "m." + Guid.NewGuid().ToString("N"), sentAtMs ?? NowMs());
        await using var conn = fx.Open();
        var leadId = await conn.ExecuteScalarAsync<Guid>(
            """
            SELECT l.id FROM pack_sales.business_identity i
            JOIN pack_sales.lead l ON l.business_id = i.business_id AND l.tenant_id = i.tenant_id
            WHERE i.tenant_id = @T AND i.platform = 'messenger' AND i.username = @Psid
            """,
            new { T = SalesPackDefinition.DedicatedTenantId, Psid = psid });
        var draftId = await LatestDraftAsync(fx, leadId);
        return (leadId, draftId, psid);
    }

    public static async Task<Guid?> LatestDraftAsync(KitSalesDbFixture fx, Guid leadId)
    {
        await using var conn = fx.Open();
        return await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM pack_sales.interaction
            WHERE lead_id = @LeadId AND channel = 'facebook' AND direction = 'outbound' AND review_status = 'draft'
            ORDER BY created_at DESC LIMIT 1
            """,
            new { LeadId = leadId });
    }

    public static async Task<int> EventsAsync(KitSalesDbFixture fx, Guid leadId, string type)
    {
        await using var conn = fx.Open();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.lead_event WHERE lead_id = @LeadId AND event_type = @Type",
            new { LeadId = leadId, Type = type });
    }

    public static async Task<int> OpenTasksAsync(KitSalesDbFixture fx, Guid leadId, string action)
    {
        await using var conn = fx.Open();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_sales.task WHERE lead_id = @LeadId AND action_code = @Action AND status = 'open'",
            new { LeadId = leadId, Action = action });
    }

    public static async Task<Guid> BareLeadAsync(KitSalesDbFixture fx)
    {
        await using var conn = fx.Open();
        var businessId = await conn.ExecuteScalarAsync<Guid>(
            "INSERT INTO pack_sales.business (tenant_id, name, business_type) VALUES (@T, @Name, 'pharmacy') RETURNING id",
            new { T = SalesPackDefinition.DedicatedTenantId, Name = "CI bare " + Guid.NewGuid().ToString("N")[..8] });
        return await conn.ExecuteScalarAsync<Guid>(
            "INSERT INTO pack_sales.lead (tenant_id, business_id, product_code) VALUES (@T, @B, 'novixa') RETURNING id",
            new { T = SalesPackDefinition.DedicatedTenantId, B = businessId });
    }
}
