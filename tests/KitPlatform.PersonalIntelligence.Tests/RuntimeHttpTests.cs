using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class RuntimeHttpTests : IClassFixture<PiValidationDatabase>, IAsyncLifetime
{
    private const string ApplicationName = "pi-runtime-validation";
    private readonly PiValidationDatabase _db;
    private readonly string _secret = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
    private readonly string _platformKey = "pi-validation-" + Guid.NewGuid().ToString("N");
    private readonly StringBuilder _log = new();
    private Process? _process;
    private HttpClient? _client;
    private int _port;

    public RuntimeHttpTests(PiValidationDatabase db) => _db = db;

    public async Task InitializeAsync()
    {
        var dll = Path.Combine(@"E:\KitPlatform\.dev\sb-runtime-http", "KitPlatform.Api.dll");
        if (!File.Exists(dll))
            throw new InvalidOperationException("The validation API output is missing.");

        _port = FreePort();
        var info = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        info.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{_port}";
        info.Environment["ConnectionStrings__Default"] = new NpgsqlConnectionStringBuilder(_db.ConnectionString)
        {
            ApplicationName = ApplicationName,
        }.ConnectionString;
        info.Environment["Jwt__Secret"] = _secret;
        info.Environment["Jwt__Issuer"] = "KitPlatform";
        info.Environment["Jwt__Audience"] = "KitPlatform.Client";
        info.Environment["Platform__ProvisioningKey"] = _platformKey;
        _process = new Process { StartInfo = info, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) _log.AppendLine(eventArgs.Data); };
        _process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) _log.AppendLine(eventArgs.Data); };
        if (!_process.Start())
            throw new InvalidOperationException("The validation API process did not start.");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
        var ready = false;
        for (var attempt = 0; attempt < 40 && !_process.HasExited; attempt++)
        {
            try
            {
                var health = await _client.GetAsync("/api/health");
                if (health.IsSuccessStatusCode)
                {
                    ready = true;
                    break;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250);
        }

        if (!ready)
            throw new InvalidOperationException("The validation API did not become ready.\n" + _log);

        var probe = await _client.GetAsync("/api/health/db");
        var probeBody = await probe.Content.ReadAsStringAsync();
        Assert.True(probe.IsSuccessStatusCode, probeBody);
        await using var conn = await _db.OpenAsync();
        var sessions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pg_stat_activity WHERE application_name = @Name AND datname = current_database()",
            new { Name = ApplicationName });
        Assert.True(sessions > 0, "The validation API did not connect to kit_pi_validation.");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();
    }

    [Fact]
    public async Task Real_http_runtime_enforces_auth_module_risk_and_retry()
    {
        var anonymous = await Client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", Sample("http-anon"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var deniedUser = Guid.Parse("a9999999-9999-9999-9999-999999999999");
        await using (var conn = await _db.OpenAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO public.users (id, tenant_id, username, email, password_hash)
                VALUES (@Id, @TenantId, 'pi-a-noperm', 'pi-a-noperm@validation.local', 'not-a-secret')
                ON CONFLICT (id) DO NOTHING
                """,
                new { Id = deniedUser, TenantId = _db.TenantA });
        }

        var forbidden = await Client.SendAsync(Request(Sample("http-forbidden"), Token(deniedUser, _db.TenantA)));
        var forbiddenBody = await forbidden.Content.ReadAsStringAsync();
        Assert.True(forbidden.StatusCode == HttpStatusCode.Forbidden, forbiddenBody);

        var moduleOff = await Client.SendAsync(Request(Sample("http-module"), Token(_db.UserB, _db.TenantB)));
        var moduleText = await moduleOff.Content.ReadAsStringAsync();
        Assert.True(moduleOff.StatusCode == HttpStatusCode.Forbidden, moduleText);
        Assert.Contains("Module is not enabled for this tenant.", moduleText, StringComparison.Ordinal);

        await using var setup = await _db.OpenAsync();
        var previous = await setup.ExecuteScalarAsync<string>(
            "SELECT settings::text FROM public.tenants WHERE id = @TenantId",
            new { TenantId = _db.TenantB });
        await setup.ExecuteAsync(
            """
            UPDATE public.tenants
            SET settings = settings || '{"platform":{"enabled_modules":["personal_intelligence"]}}'::jsonb
            WHERE id = @TenantId
            """,
            new { TenantId = _db.TenantB });
        await setup.DisposeAsync();

        var switched = Sample("http-switch");
        switched.RequestedTenantId = _db.TenantA;
        try
        {
            var switchResponse = await Client.SendAsync(Request(switched, Token(_db.UserB, _db.TenantB)));
            var switchText = await switchResponse.Content.ReadAsStringAsync();
            Assert.True(switchResponse.StatusCode == HttpStatusCode.Forbidden, switchText);
            Assert.Contains("cannot switch", switchText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var restore = await _db.OpenAsync();
            await restore.ExecuteAsync(
                "UPDATE public.tenants SET settings = @Settings::jsonb WHERE id = @TenantId",
                new { TenantId = _db.TenantB, Settings = previous });
        }

        var body = Sample("http-ok");
        var created = await Read(await Client.SendAsync(Request(body, Token(_db.UserA, _db.TenantA))));
        var replay = await Read(await Client.SendAsync(Request(body, Token(_db.UserA, _db.TenantA))));
        var decisionId = created.GetProperty("decisionId").GetGuid();
        Assert.Equal(decisionId, replay.GetProperty("decisionId").GetGuid());
        Assert.Equal("PAPER", created.GetProperty("executionMode").GetString());
        Assert.Equal("Fixture", created.GetProperty("evidenceBasis").GetString());
        Assert.Equal("proposed", created.GetProperty("policyStatus").GetString());
        Assert.Equal("recommend_only", created.GetProperty("autonomy").GetString());
        Assert.Contains("PolicyProposed", created.GetProperty("eventTypes").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("PolicyApproved", created.GetProperty("eventTypes").EnumerateArray().Select(item => item.GetString()));

        await using var check = await _db.OpenAsync();
        var stored = await check.QuerySingleAsync<(string Mode, string Source, int Decisions, int Events, int Evidence, string PolicyStatus)>(
            """
            SELECT
                e.mode AS Mode,
                t.source AS Source,
                (SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision d WHERE d.thesis = @Thesis AND d.tenant_id = @TenantId) AS Decisions,
                (SELECT COUNT(*) FROM pack_personal_intelligence.pi_events ev WHERE ev.correlation_id = @DecisionId AND ev.event_type = 'DecisionMade' AND ev.tenant_id = @TenantId) AS Events,
                (SELECT COUNT(*) FROM pack_personal_intelligence.pi_evidence ev
                    JOIN pack_personal_intelligence.pi_decision d ON d.context_id = ev.context_id AND d.tenant_id = ev.tenant_id
                    WHERE d.decision_id = @DecisionId AND d.tenant_id = @TenantId) AS Evidence,
                (SELECT p.status FROM pack_personal_intelligence.pi_policy p
                    JOIN pack_personal_intelligence.pi_events ev ON ev.correlation_id = p.policy_id AND ev.tenant_id = p.tenant_id
                    WHERE ev.event_type = 'PolicyProposed' AND p.tenant_id = @TenantId
                    ORDER BY ev.occurred_at DESC LIMIT 1) AS PolicyStatus
            FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
            JOIN pack_personal_intelligence.trade t ON t.tenant_id = o.tenant_id AND t.external_trade_id LIKE @Prefix
            WHERE o.decision_id = @DecisionId AND o.tenant_id = @TenantId
            LIMIT 1
            """,
            new { DecisionId = decisionId, TenantId = _db.TenantA, body.Thesis, Prefix = body.OperationKey + "%" });
        Assert.Equal("PAPER", stored.Mode);
        Assert.Equal("FIXTURE", stored.Source);
        Assert.Equal(1, stored.Decisions);
        Assert.Equal(1, stored.Events);
        Assert.True(stored.Evidence >= 1);
        Assert.Equal("proposed", stored.PolicyStatus);

        var switchedDecisions = await check.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { switched.Thesis });
        Assert.Equal(0, switchedDecisions);

        body.Thesis += "-changed";
        var conflict = await Client.SendAsync(Request(body, Token(_db.UserA, _db.TenantA)));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictDecisions = await check.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE decision_id = @DecisionId",
            new { DecisionId = decisionId });
        Assert.Equal(1, conflictDecisions);

        var fresh = Sample("http-new");
        var second = await Read(await Client.SendAsync(Request(fresh, Token(_db.UserA, _db.TenantA))));
        Assert.NotEqual(decisionId, second.GetProperty("decisionId").GetGuid());

        var veto = Sample("http-veto");
        veto.Quantity = 10m;
        veto.Price = 500m;
        var vetoDoc = await Read(await Client.SendAsync(Request(veto, Token(_db.UserA, _db.TenantA))));
        Assert.False(vetoDoc.GetProperty("riskAllowed").GetBoolean());
        Assert.False(vetoDoc.GetProperty("paperExecuted").GetBoolean());
        var vetoExecutions = await check.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id
            WHERE o.decision_id = @DecisionId
            """,
            new { DecisionId = vetoDoc.GetProperty("decisionId").GetGuid() });
        Assert.Equal(0, vetoExecutions);

        var live = Sample("http-live");
        live.Live = true;
        var liveResponse = await Client.SendAsync(Request(live, Token(_db.UserA, _db.TenantA)));
        Assert.Equal(HttpStatusCode.BadRequest, liveResponse.StatusCode);
        var liveDecisions = await check.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE thesis = @Thesis",
            new { live.Thesis });
        Assert.Equal(0, liveDecisions);
    }

    [Fact]
    public async Task Official_entitlement_api_toggles_personal_intelligence_for_tenant_b()
    {
        var accountB = Guid.Parse("b8888888-8888-8888-8888-888888888888");
        var exchangeB = Guid.Parse("b7777777-7777-7777-7777-777777777777");
        await using var setup = await _db.OpenAsync();
        var registry = await setup.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM platform_module_registry WHERE module_code = @Code AND status = 1",
            new { Code = KitPlatform.Application.Core.PlatformModuleCodes.PersonalIntelligence });
        Assert.Equal(1, registry);

        var saved = await setup.QuerySingleAsync<(string Settings, string Vertical)>(
            """
            SELECT settings::text AS Settings, business_vertical AS Vertical
            FROM public.tenants WHERE id = @TenantId
            """,
            new { TenantId = _db.TenantB });
        using var savedDoc = JsonDocument.Parse(saved.Settings);
        var baseline = savedDoc.RootElement.GetProperty("platform").GetProperty("enabled_modules")
            .EnumerateArray().Select(item => item.GetString()!).ToList();
        Assert.DoesNotContain("personal_intelligence", baseline);

        await setup.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.exchange (exchange_id, tenant_id, exchange_code, exchange_name)
            VALUES (@ExchangeId, @TenantId, 'fixture', 'Fixture')
            ON CONFLICT (exchange_id) DO NOTHING;

            INSERT INTO pack_personal_intelligence.trading_account (
                trading_account_id, person_id, tenant_id, exchange_id, external_account_id)
            VALUES (@AccountId, @PersonId, @TenantId, @ExchangeId, 'fixture-account-b')
            ON CONFLICT (trading_account_id) DO NOTHING;
            """,
            new { ExchangeId = exchangeB, AccountId = accountB, PersonId = _db.PersonB, TenantId = _db.TenantB });
        await setup.DisposeAsync();

        try
        {
            var denied = await Client.SendAsync(Entitlement(_db.TenantB, baseline.Append("personal_intelligence").ToArray(), null));
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

            var blocked = SampleFor(_db.PersonB, accountB, "http-b-off");
            var blockedResponse = await Client.SendAsync(Request(blocked, Token(_db.UserB, _db.TenantB)));
            var blockedText = await blockedResponse.Content.ReadAsStringAsync();
            Assert.True(blockedResponse.StatusCode == HttpStatusCode.Forbidden, blockedText);
            Assert.Contains("Module is not enabled for this tenant.", blockedText, StringComparison.Ordinal);

            var enabled = await Read(await Client.SendAsync(Entitlement(
                _db.TenantB,
                baseline.Append("personal_intelligence").Append("not_a_real_module").ToArray(),
                _platformKey)));
            var enabledModules = enabled.GetProperty("enabledModules").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains("personal_intelligence", enabledModules);
            Assert.DoesNotContain("not_a_real_module", enabledModules);

            var settings = await Read(await Client.SendAsync(AuthorizedGet(
                "/api/system/tenant-platform",
                Token(_db.UserB, _db.TenantB))));
            Assert.Contains(
                "personal_intelligence",
                settings.GetProperty("enabledModules").EnumerateArray().Select(item => item.GetString()));

            var opened = SampleFor(_db.PersonB, accountB, "http-b-on");
            var created = await Read(await Client.SendAsync(Request(opened, Token(_db.UserB, _db.TenantB))));
            var decisionId = created.GetProperty("decisionId").GetGuid();
            Assert.Equal("PAPER", created.GetProperty("executionMode").GetString());
            Assert.Equal("recommend_only", created.GetProperty("autonomy").GetString());

            await using var check = await _db.OpenAsync();
            var stored = await check.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM pack_personal_intelligence.pi_decision d
                JOIN pack_personal_intelligence.trading_order o ON o.decision_id = d.decision_id AND o.tenant_id = d.tenant_id
                JOIN pack_personal_intelligence.trading_execution e ON e.order_id = o.order_id AND e.tenant_id = o.tenant_id
                WHERE d.decision_id = @DecisionId AND d.tenant_id = @TenantId AND e.mode = 'PAPER'
                """,
                new { DecisionId = decisionId, TenantId = _db.TenantB });
            Assert.Equal(1, stored);

            var closed = await Read(await Client.SendAsync(Entitlement(_db.TenantB, baseline.ToArray(), _platformKey)));
            Assert.DoesNotContain(
                "personal_intelligence",
                closed.GetProperty("enabledModules").EnumerateArray().Select(item => item.GetString()));

            var after = SampleFor(_db.PersonB, accountB, "http-b-again");
            var rejected = await Client.SendAsync(Request(after, Token(_db.UserB, _db.TenantB)));
            var rejectedText = await rejected.Content.ReadAsStringAsync();
            Assert.True(rejected.StatusCode == HttpStatusCode.Forbidden, rejectedText);
            Assert.Contains("Module is not enabled for this tenant.", rejectedText, StringComparison.Ordinal);
            var leaked = await check.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @TenantId AND thesis = @Thesis",
                new { TenantId = _db.TenantB, after.Thesis });
            Assert.Equal(0, leaked);

            var untouched = await check.ExecuteScalarAsync<string>(
                "SELECT (settings->'platform'->'enabled_modules')::text FROM public.tenants WHERE id = @TenantId",
                new { TenantId = _db.TenantA });
            Assert.Contains("personal_intelligence", untouched);
        }
        finally
        {
            await using var restore = await _db.OpenAsync();
            await restore.ExecuteAsync(
                """
                UPDATE public.tenants
                SET settings = @Settings::jsonb, business_vertical = @Vertical
                WHERE id = @TenantId
                """,
                new { TenantId = _db.TenantB, saved.Settings, saved.Vertical });
        }
    }

    [Fact]
    public async Task Desk_reads_postgres_and_paper_approval_is_a_separate_permission()
    {
        var summary = await Client.GetAsync("/api/personal-intelligence/trading/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, summary.StatusCode);

        var authorized = await Client.SendAsync(AuthorizedGet("/api/personal-intelligence/trading/summary", Token(_db.UserA, _db.TenantA)));
        var summaryDoc = await Read(authorized);
        Assert.True(summaryDoc.GetProperty("tradeCount").GetInt32() >= 0);

        var analysis = Sample("desk-analysis");
        analysis.ApprovedBy = null;
        var created = await Read(await Client.SendAsync(Request(analysis, Token(_db.UserA, _db.TenantA))));
        Assert.False(created.GetProperty("paperExecuted").GetBoolean());
        var decisionId = created.GetProperty("decisionId").GetGuid();

        var runOnly = Guid.Parse("a1212121-1212-1212-1212-121212121212");
        await using (var conn = await _db.OpenAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO public.users (id, tenant_id, username, email, password_hash)
                VALUES (@Id, @TenantId, 'pi-a-runonly', 'pi-a-runonly@validation.local', 'not-a-secret')
                ON CONFLICT (id) DO NOTHING;
                INSERT INTO public.user_roles (user_id, role_id)
                SELECT @Id, r.id FROM public.roles r
                WHERE r.tenant_id = @TenantId AND r.role_code = 'PI_TRADING'
                ON CONFLICT DO NOTHING;
                """,
                new { Id = runOnly, TenantId = _db.TenantA });
        }

        var denied = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/personal-intelligence/trading/decisions/{decisionId}/paper")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token(runOnly, _db.TenantA)) },
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var approved = await Read(await Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/personal-intelligence/trading/decisions/{decisionId}/paper")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token(_db.UserA, _db.TenantA)) },
        }));
        Assert.Equal("PAPER", approved.GetProperty("mode").GetString());
        await using var check = await _db.OpenAsync();
        var stored = await check.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
            WHERE o.decision_id = @DecisionId AND o.tenant_id = @TenantId AND e.mode = 'PAPER'
            """,
            new { DecisionId = decisionId, TenantId = _db.TenantA });
        Assert.Equal(1, stored);
    }

    private HttpClient Client => _client ?? throw new InvalidOperationException("The validation API client is not open.");

    private string Token(Guid userId, Guid tenantId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: "KitPlatform",
            audience: "KitPlatform.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpRequestMessage Request(TradingLoopRequest body, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/personal-intelligence/trading/loops")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    private HttpRequestMessage Entitlement(Guid tenantId, string[] modules, string? platformKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/tenants/{tenantId}/entitlement")
        {
            Content = JsonContent.Create(new
            {
                vertical = "pharmacy",
                allowedModules = modules,
                syncEnabledModules = true,
            }),
        };
        if (platformKey is not null)
            message.Headers.TryAddWithoutValidation("X-Platform-Key", platformKey);
        return message;
    }

    private static HttpRequestMessage AuthorizedGet(string path, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    private TradingLoopRequest SampleFor(Guid personId, Guid accountId, string name)
    {
        var sample = Sample(name);
        sample.PersonId = personId;
        sample.TradingAccountId = accountId;
        return sample;
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
            Thesis = "Runtime " + name + " " + id,
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

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, json);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

}
