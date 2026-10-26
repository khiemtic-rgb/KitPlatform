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
    public async Task Trading_loop_is_not_mapped_on_the_safe_host()
    {
        var anonymousGet = await Client.GetAsync("/api/personal-intelligence/trading/loops");
        Assert.Equal(HttpStatusCode.NotFound, anonymousGet.StatusCode);

        var anonymous = await Client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", Sample("http-anon"));
        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);

        var authorized = await Client.SendAsync(Request(Sample("http-auth"), Token(_db.UserA, _db.TenantA)));
        Assert.Equal(HttpStatusCode.NotFound, authorized.StatusCode);
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
            Assert.Equal(HttpStatusCode.NotFound, blockedResponse.StatusCode);

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
            var created = await Client.SendAsync(Request(opened, Token(_db.UserB, _db.TenantB)));
            Assert.Equal(HttpStatusCode.NotFound, created.StatusCode);

            await using var check = await _db.OpenAsync();

            var closed = await Read(await Client.SendAsync(Entitlement(_db.TenantB, baseline.ToArray(), _platformKey)));
            Assert.DoesNotContain(
                "personal_intelligence",
                closed.GetProperty("enabledModules").EnumerateArray().Select(item => item.GetString()));

            var after = SampleFor(_db.PersonB, accountB, "http-b-again");
            var rejected = await Client.SendAsync(Request(after, Token(_db.UserB, _db.TenantB)));
            Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
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
    public async Task Trading_summary_is_not_mapped_on_the_safe_host()
    {
        var summary = await Client.GetAsync("/api/personal-intelligence/trading/summary");
        Assert.Equal(HttpStatusCode.NotFound, summary.StatusCode);

        var authorized = await Client.SendAsync(AuthorizedGet("/api/personal-intelligence/trading/summary", Token(_db.UserA, _db.TenantA)));
        Assert.Equal(HttpStatusCode.NotFound, authorized.StatusCode);
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
