using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.PersonalIntelligence;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Security;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class OperationalDeliveryTests : IClassFixture<PiValidationDatabase>
{
    private readonly PiValidationDatabase _db;

    public OperationalDeliveryTests(PiValidationDatabase db) => _db = db;

    [Fact]
    public async Task Unauthenticated_call_is_rejected()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var response = await host.Client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", Sample("unauth"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var response = await host.Client.SendAsync(Json(Sample("no-permission"), _db.TenantA, _db.UserA, permission: null, admin: false));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Client_supplied_tenant_does_not_override_the_authenticated_tenant()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var body = Sample("tenant-switch");
        body.RequestedTenantId = _db.TenantA;
        var previous = await ModuleSettings(true);
        try
        {
            var response = await host.Client.SendAsync(Json(body, _db.TenantB, _db.UserB, PersonalIntelligencePolicies.Permission, admin: false));
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, text);
            Assert.Contains("cannot switch", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await RestoreSettings(previous);
        }

        await using var conn = await _db.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { body.Thesis });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Fixture_loop_round_trips_through_the_api_and_postgres()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var body = Sample("happy");
        var response = await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, admin: false));
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("riskAllowed").GetBoolean());
        Assert.True(root.GetProperty("paperExecuted").GetBoolean());
        Assert.Equal("PAPER", root.GetProperty("executionMode").GetString());
        Assert.Equal("proposed", root.GetProperty("policyStatus").GetString());
        Assert.Equal("recommend_only", root.GetProperty("autonomy").GetString());
        Assert.Equal("Fixture", root.GetProperty("evidenceBasis").GetString());
        Assert.Equal(0.06m, root.GetProperty("reconstructedNetQuantity").GetDecimal());
        var events = root.GetProperty("eventTypes").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("ContextObserved", events);
        Assert.Contains("EvidenceRecorded", events);
        Assert.Contains("DecisionMade", events);
        Assert.Contains("ActionTaken", events);
        Assert.Contains("OutcomeObserved", events);
        Assert.Contains("ReflectionCreated", events);
        Assert.Contains("ExperienceDerived", events);
        Assert.Contains("PolicyProposed", events);

        var decisionId = root.GetProperty("decisionId").GetGuid();
        await using var conn = await _db.OpenAsync();
        var mode = await conn.ExecuteScalarAsync<string>(
            """
            SELECT e.mode
            FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o
              ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
            WHERE o.decision_id = @DecisionId AND o.tenant_id = @TenantId
            """,
            new { DecisionId = decisionId, TenantId = _db.TenantA });
        var tradeSource = await conn.ExecuteScalarAsync<string>(
            """
            SELECT source FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantId AND external_trade_id = @ExternalId
            """,
            new { TenantId = _db.TenantA, ExternalId = ExternalId(body, 1) });
        var eventCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.pi_events
            WHERE tenant_id = @TenantId AND correlation_id = @DecisionId AND event_type = 'DecisionMade'
            """,
            new { TenantId = _db.TenantA, DecisionId = decisionId });
        Assert.Equal("PAPER", mode);
        Assert.Equal("FIXTURE", tradeSource);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task Repeating_the_fixture_does_not_duplicate_trades()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var body = Sample("repeat");
        Assert.True((await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false))).IsSuccessStatusCode);
        Assert.True((await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false))).IsSuccessStatusCode);

        await using var conn = await _db.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantId AND external_trade_id IN (@A, @B)
            """,
            new { TenantId = _db.TenantA, A = ExternalId(body, 1), B = ExternalId(body, 2) });
        var price = await conn.ExecuteScalarAsync<decimal>(
            "SELECT price FROM pack_personal_intelligence.trade WHERE external_trade_id = @Id AND tenant_id = @TenantId",
            new { Id = ExternalId(body, 1), TenantId = _db.TenantA });
        Assert.Equal(2, count);
        Assert.Equal(100m, price);
    }

    [Fact]
    public async Task Veto_and_live_do_not_create_an_execution()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var veto = Sample("veto");
        veto.Quantity = 10m;
        veto.Price = 500m;
        var vetoResponse = await host.Client.SendAsync(Json(veto, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false));
        var vetoJson = await vetoResponse.Content.ReadAsStringAsync();
        Assert.True(vetoResponse.IsSuccessStatusCode, vetoJson);
        using var vetoDoc = JsonDocument.Parse(vetoJson);
        Assert.False(vetoDoc.RootElement.GetProperty("riskAllowed").GetBoolean());
        Assert.False(vetoDoc.RootElement.GetProperty("paperExecuted").GetBoolean());
        var vetoDecision = vetoDoc.RootElement.GetProperty("decisionId").GetGuid();

        var live = Sample("live");
        live.Live = true;
        var liveResponse = await host.Client.SendAsync(Json(live, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false));
        Assert.Equal(HttpStatusCode.BadRequest, liveResponse.StatusCode);

        await using var conn = await _db.OpenAsync();
        var executions = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id
            WHERE o.decision_id = @DecisionId
            """,
            new { DecisionId = vetoDecision });
        var liveDecisions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { live.Thesis });
        Assert.Equal(0, executions);
        Assert.Equal(0, liveDecisions);
    }

    [Fact]
    public async Task Failed_commit_leaves_no_trade_or_decision()
    {
        var loop = new TradingOperationalLoop(new NpgsqlConnectionFactory(_db.ConnectionString), new LoopTenant(_db.TenantA, _db.UserA));
        var body = Sample("rollback");
        await Assert.ThrowsAnyAsync<PostgresException>(() => loop.ProbeRollbackAsync(body));

        await using var conn = await _db.OpenAsync();
        var trades = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE external_trade_id = @Id",
            new { Id = ExternalId(body, 1) });
        var decisions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { body.Thesis });
        Assert.Equal(0, trades);
        Assert.Equal(0, decisions);
    }

    private TradingLoopRequest Sample(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        return new TradingLoopRequest
        {
            PersonId = _db.PersonA,
            TradingAccountId = _db.AccountA,
            FixtureCsv = $"""
                external_trade_id,symbol,side,quantity,price,executed_at
                {id}-1,BTCUSDT,BUY,0.10,100,2026-10-01T00:00:00Z
                {id}-2,BTCUSDT,SELL,0.04,110,2026-10-02T00:00:00Z
                """,
            Thesis = "Operational " + name + " " + id,
            ExpectedOutcome = "Smaller drawdown",
            Invalidation = "Break of the prior low",
            Symbol = "BTCUSDT",
            Side = "BUY",
            Quantity = 0.1m,
            Price = 100m,
            Leverage = 1m,
            Confidence = 0.8m,
            MaxNotional = 1000m,
            MaxLeverage = 2m,
            MinConfidence = 0.55m,
            ApprovedBy = "owner",
            ReflectionNote = "Fixture only. Do not promote this to a rule.",
            PolicyRule = "Do not change policy from one fixture outcome.",
            OperationKey = id,
        };
    }

    [Fact]
    public async Task Module_disabled_tenant_is_forbidden()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var response = await host.Client.SendAsync(Json(Sample("module-off"), _db.TenantB, _db.UserB, PersonalIntelligencePolicies.Permission, admin: false));
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Module is not enabled for this tenant.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_operation_key_returns_the_original_decision()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var body = Sample("retry");
        var first = await Read(await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        var second = await Read(await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        Assert.Equal(first.GetProperty("decisionId").GetGuid(), second.GetProperty("decisionId").GetGuid());

        await using var conn = await _db.OpenAsync();
        var decisions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { body.Thesis });
        var executions = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id
            WHERE o.decision_id = @DecisionId
            """,
            new { DecisionId = first.GetProperty("decisionId").GetGuid() });
        var events = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_events WHERE correlation_id = @DecisionId AND event_type = 'DecisionMade'",
            new { DecisionId = first.GetProperty("decisionId").GetGuid() });
        Assert.Equal(1, decisions);
        Assert.Equal(1, executions);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task Same_operation_key_with_a_different_payload_conflicts()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var body = Sample("conflict");
        Assert.True((await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false))).IsSuccessStatusCode);
        body.Thesis += "-other";
        var conflict = await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using var conn = await _db.OpenAsync();
        var decisions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis LIKE @Prefix",
            new { Prefix = body.Thesis.Replace("-other", "") + "%" });
        Assert.Equal(1, decisions);
    }

    [Fact]
    public async Task Stored_trades_are_the_evidence_and_a_veto_does_not_execute()
    {
        await using var conn = await _db.OpenAsync();
        var before = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND trading_account_id = @AccountId",
            new { TenantId = _db.TenantA, AccountId = _db.AccountA });
        var trade = await conn.QuerySingleAsync<(Guid TradeId, string Symbol, string Side, decimal Quantity, decimal Price)>(
            """
            SELECT trade_id AS TradeId, symbol AS Symbol, side AS Side, quantity AS Quantity, price AS Price
            FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantId AND trading_account_id = @AccountId
            ORDER BY executed_at DESC
            LIMIT 1
            """,
            new { TenantId = _db.TenantA, AccountId = _db.AccountA });
        var id = Guid.NewGuid().ToString("N");
        var body = new TradingLoopRequest
        {
            PersonId = _db.PersonA,
            TradingAccountId = _db.AccountA,
            Thesis = "Workspace stored " + id,
            Invalidation = "The selected fill is not representative",
            Symbol = trade.Symbol,
            Side = trade.Side,
            Quantity = trade.Quantity,
            Price = trade.Price,
            Leverage = 1m,
            Confidence = 0.2m,
            MaxNotional = 1000m,
            MaxLeverage = 2m,
            MinConfidence = 0.55m,
            OperationKey = id,
            TradeIds = [trade.TradeId],
        };

        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var first = await Read(await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        var second = await Read(await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        Assert.Equal(first.GetProperty("decisionId").GetGuid(), second.GetProperty("decisionId").GetGuid());
        Assert.False(first.GetProperty("riskAllowed").GetBoolean());
        Assert.False(first.GetProperty("paperExecuted").GetBoolean());
        Assert.Contains("Confidence is below the configured minimum.", first.GetProperty("riskReasons").EnumerateArray().Select(item => item.GetString()));
        Assert.True(first.GetProperty("storedTradeCount").GetInt32() >= 1);

        body.Invalidation += " changed";
        var conflict = await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var decisionId = first.GetProperty("decisionId").GetGuid();
        var decisions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { body.Thesis });
        var executions = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id
            WHERE o.decision_id = @DecisionId
            """,
            new { DecisionId = decisionId });
        var reflections = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_events WHERE correlation_id = @DecisionId AND event_type = 'ReflectionCreated'",
            new { DecisionId = decisionId });
        var after = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantId AND trading_account_id = @AccountId",
            new { TenantId = _db.TenantA, AccountId = _db.AccountA });
        Assert.Equal(1, decisions);
        Assert.Equal(0, executions);
        Assert.Equal(0, reflections);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Paper_approval_requires_the_approve_permission()
    {
        var body = Sample("paper-gate");
        body.ApprovedBy = null;
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var created = await Read(await host.Client.SendAsync(Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        Assert.True(created.GetProperty("riskAllowed").GetBoolean());
        Assert.False(created.GetProperty("paperExecuted").GetBoolean());
        var decisionId = created.GetProperty("decisionId").GetGuid();

        var denied = new HttpRequestMessage(HttpMethod.Post, $"/api/personal-intelligence/trading/decisions/{decisionId}/paper");
        denied.Headers.Add("X-Test-User", _db.UserA.ToString());
        denied.Headers.Add("X-Test-Tenant", _db.TenantA.ToString());
        denied.Headers.Add("X-Test-Permission", PersonalIntelligencePolicies.Permission);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.SendAsync(denied)).StatusCode);

        var allowed = new HttpRequestMessage(HttpMethod.Post, $"/api/personal-intelligence/trading/decisions/{decisionId}/paper");
        allowed.Headers.Add("X-Test-User", _db.UserA.ToString());
        allowed.Headers.Add("X-Test-Tenant", _db.TenantA.ToString());
        allowed.Headers.Add("X-Test-Permission", PersonalIntelligencePolicies.Permission);
        allowed.Headers.Add("X-Test-Permission", PersonalIntelligencePolicies.ApprovePermission);
        var approvedResponse = await host.Client.SendAsync(allowed);
        var approvedText = await approvedResponse.Content.ReadAsStringAsync();
        Assert.True(approvedResponse.IsSuccessStatusCode, ((int)approvedResponse.StatusCode) + " " + approvedText);
        var approved = JsonDocument.Parse(approvedText).RootElement;
        Assert.Equal("PAPER", approved.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task A_new_operation_key_creates_a_new_decision()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var first = Sample("new-op");
        var second = Sample("new-op");
        var left = await Read(await host.Client.SendAsync(Json(first, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        var right = await Read(await host.Client.SendAsync(Json(second, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false)));
        Assert.NotEqual(left.GetProperty("decisionId").GetGuid(), right.GetProperty("decisionId").GetGuid());
    }

    [Fact]
    public async Task Concurrent_retries_create_one_execution()
    {
        await using var host = await OperationalHost.Start(_db.ConnectionString);
        var body = Sample("concurrent");
        var message = Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false);
        var again = Json(body, _db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission, false);
        var responses = await Task.WhenAll(host.Client.SendAsync(message), host.Client.SendAsync(again));
        var docs = new List<JsonElement>();
        foreach (var response in responses)
            docs.Add(await Read(response));
        Assert.Equal(docs[0].GetProperty("decisionId").GetGuid(), docs[1].GetProperty("decisionId").GetGuid());

        await using var conn = await _db.OpenAsync();
        var executions = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id
            WHERE o.decision_id = @DecisionId
            """,
            new { DecisionId = docs[0].GetProperty("decisionId").GetGuid() });
        Assert.Equal(1, executions);
    }

    private static string ExternalId(TradingLoopRequest request, int index) =>
        request.FixtureCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[index].Split(',')[0].Trim();

    private async Task<string> ModuleSettings(bool enabled)
    {
        await using var conn = await _db.OpenAsync();
        var previous = await conn.ExecuteScalarAsync<string>(
            "SELECT settings::text FROM public.tenants WHERE id = @TenantId",
            new { TenantId = _db.TenantB });
        var modules = enabled ? "[\"personal_intelligence\"]" : "[]";
        await conn.ExecuteAsync(
            """
            UPDATE public.tenants
            SET settings = settings || jsonb_build_object('platform', jsonb_build_object('enabled_modules', @Modules::jsonb))
            WHERE id = @TenantId
            """,
            new { TenantId = _db.TenantB, Modules = modules });
        return previous ?? "{}";
    }

    private async Task RestoreSettings(string settings)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            "UPDATE public.tenants SET settings = @Settings::jsonb WHERE id = @TenantId",
            new { TenantId = _db.TenantB, Settings = settings });
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static HttpRequestMessage Json(TradingLoopRequest body, Guid tenantId, Guid userId, string? permission, bool admin)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/personal-intelligence/trading/loops")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Add("X-Test-User", userId.ToString());
        message.Headers.Add("X-Test-Tenant", tenantId.ToString());
        if (permission is not null)
            message.Headers.Add("X-Test-Permission", permission);
        if (!string.IsNullOrWhiteSpace(body.ApprovedBy))
            message.Headers.Add("X-Test-Permission", "personal_intelligence.trading.approve");
        if (admin)
            message.Headers.Add("X-Test-Admin", "1");
        return message;
    }

    private sealed class ValidationModuleSettings(IDbConnectionFactory db, ITenantContext tenant) : ITenantPlatformSettings
    {
        private static readonly string[] DefaultPharmacyModules =
        [
            "inventory", "procurement", "sales", "loyalty", "customer_app",
            "medication", "health_wallet", "reservations", "reports",
        ];

        public async Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default)
        {
            await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
            var json = await conn.ExecuteScalarAsync<string>(
                "SELECT settings::text FROM public.tenants WHERE id = @TenantId AND deleted_at IS NULL",
                new { tenant.TenantId });
            IEnumerable<string> modules = DefaultPharmacyModules;
            if (!string.IsNullOrWhiteSpace(json))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("platform", out var platform)
                    && platform.TryGetProperty("enabled_modules", out var list)
                    && list.ValueKind == JsonValueKind.Array)
                {
                    modules = list.EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToArray();
                }
            }

            return modules.Contains(moduleCode, StringComparer.OrdinalIgnoreCase);
        }

        public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(UpdateTenantPlatformSettingsRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsModuleEnabledForTenantCodeAsync(string tenantCode, string moduleCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsFeatureEnabledForTenantCodeAsync(string tenantCode, string featureCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(Guid? branchId = null, Guid? customerAccountId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class OperationalHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public HttpClient Client { get; }

        private OperationalHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public static async Task<OperationalHost> Start(string connectionString)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
            builder.Services.AddScoped<ITenantContext, TenantContext>();
            builder.Services.AddScoped<ITenantPlatformSettings, ValidationModuleSettings>();
            builder.Services.AddPersonalIntelligencePack();
            builder.Services.Configure<KitPlatform.Packs.PersonalIntelligence.Trading.ExchangeBackfillOptions>(options => options.WorkerEnabled = false);
            builder.Services.AddAuthentication(HeaderAuthHandler.AuthScheme)
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.AuthScheme, _ => { });
            builder.Services.AddAuthorization(options => options.AddPersonalIntelligenceAuthorization());
            builder.Services.AddControllers().AddApplicationPart(typeof(TradingLoopController).Assembly);

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return new OperationalHost(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync() => await _app.DisposeAsync();
    }

    private sealed class HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthScheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user) || !Guid.TryParse(user, out _))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new("sub", user.ToString()),
                new("tenant_id", Request.Headers["X-Test-Tenant"].ToString()),
            };
            foreach (var permission in Request.Headers["X-Test-Permission"])
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    claims.Add(new Claim("permission", permission));
            }
            if (Request.Headers.ContainsKey("X-Test-Admin"))
                claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));

            var identity = new ClaimsIdentity(claims, AuthScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthScheme)));
        }
    }

    private sealed class LoopTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }
}
