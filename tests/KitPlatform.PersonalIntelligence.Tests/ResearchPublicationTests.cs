using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.PersonalIntelligence;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Research;
using KitPlatform.Packs.PersonalIntelligence.Research;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchPublicationTests
{
    static readonly UTF8Encoding Utf8 = new(false);
    static readonly DateTimeOffset SummaryAt = DateTimeOffset.Parse("2026-01-02T00:00:00Z");
    const string PublishedAt = "2026-10-22T04:00:00+00:00";
    const string SourceRunAt = "2026-10-22T03:04:02.201078+00:00";

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        await using var host = await ResearchHost.Start();
        var response = await host.Anonymous().GetAsync("/api/personal-intelligence/research/publication");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_module_is_forbidden()
    {
        await using var host = await ResearchHost.Start();
        host.Settings.Enabled = false;
        var response = await host.Client("personal_intelligence.research.read").GetAsync("/api/personal-intelligence/research/publication");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Module is not enabled for this tenant.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Trading_run_and_research_read_and_admin_can_read()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root);
        foreach (var client in new[]
        {
            host.Client("personal_intelligence.trading.run"),
            host.Client("personal_intelligence.research.read"),
            host.Client(role: "ADMIN"),
        })
        {
            var response = await client.GetAsync("/api/personal-intelligence/research/publication");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("FRESH", body.RootElement.GetProperty("availability").GetString());
        }
    }

    [Fact]
    public async Task Approve_or_exchange_permissions_cannot_read()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root);
        foreach (var permission in new[]
        {
            "personal_intelligence.trading.approve",
            "personal_intelligence.exchange.read",
            "personal_intelligence.exchange.connect",
        })
        {
            var response = await host.Client(permission).GetAsync("/api/personal-intelligence/research/experiments");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Missing_manifest_or_object_has_no_fake_numbers()
    {
        await using var host = await ResearchHost.Start();
        var client = host.Client("personal_intelligence.research.read");

        host.Reader.Root = Path.Combine(host.Root, "does-not-exist");
        await AssertMissingWithoutNumbers(client);

        WriteValid(host.Root, "MISSING_OBJECT_MARKER");
        File.Delete(Path.Combine(host.Root, "manifest.json"));
        host.Reader.Root = host.Root;
        await AssertMissingWithoutNumbers(client);

        WriteValid(host.Root, "MISSING_OBJECT_MARKER");
        File.Delete(Path.Combine(host.Root, "lab", "LAB_RESULT.json"));
        var report = await client.GetAsync("/api/personal-intelligence/research/paper/report");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var reportBody = await report.Content.ReadAsStringAsync();
        using var reportJson = JsonDocument.Parse(reportBody);
        Assert.Equal("MISSING", reportJson.RootElement.GetProperty("availability").GetString());
        Assert.Equal(JsonValueKind.Null, reportJson.RootElement.GetProperty("report").ValueKind);
        AssertNoFinancialPlaceholder(reportBody);
        Assert.DoesNotContain("MISSING_OBJECT_MARKER", reportBody);
    }

    [Fact]
    public async Task Stale_health_keeps_source_timestamps()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root, health: "STALE", lastBarAgeMs: 242330);
        Touch1999(host.Root);
        var body = await Read(host, "/api/personal-intelligence/research/paper/status");
        Assert.Equal("STALE", body.GetProperty("availability").GetString());
        var status = body.GetProperty("status");
        Assert.Equal("STALE", status.GetProperty("health").GetString());
        Assert.Equal(SourceRunAt, status.GetProperty("lastRunAt").GetString());
        Assert.Equal(242330, status.GetProperty("lastBarAgeMs").GetInt64());
        Assert.DoesNotContain("1999", body.GetRawText());
    }

    [Fact]
    public async Task Lab_without_a_timestamp_is_present_untimed()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root);
        Touch1999(host.Root);
        var body = await Read(host, "/api/personal-intelligence/research/publication");
        Assert.Equal("FRESH", body.GetProperty("availability").GetString());
        Assert.Equal(PublishedAt, body.GetProperty("publishedAt").GetString());
        var lab = body.GetProperty("objects").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "lab-result");
        Assert.Equal("PRESENT_UNTIMED", lab.GetProperty("availability").GetString());
        Assert.Equal(JsonValueKind.Null, lab.GetProperty("publishedAt").ValueKind);
        Assert.DoesNotContain("1999", body.GetRawText());
    }

    [Fact]
    public async Task Broken_json_is_unreadable_without_a_path_or_file_text()
    {
        await using var host = await ResearchHost.Start();
        var broken = Utf8.GetBytes("{\"experiments\":[{\"secret\":\"secret-root-marker\"}");
        WriteValid(host.Root);
        var labPath = Path.Combine(host.Root, "lab", "LAB_RESULT.json");
        await File.WriteAllBytesAsync(labPath, broken);
        RewriteHash(host.Root, "labResultSha256", broken);
        var response = await host.Client("personal_intelligence.research.read").GetAsync("/api/personal-intelligence/research/experiments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal("UNREADABLE", body.RootElement.GetProperty("availability").GetString());
        Assert.Equal("UNREADABLE", body.RootElement.GetProperty("readError").GetProperty("code").GetString());
        Assert.Empty(body.RootElement.GetProperty("experiments").EnumerateArray());
        AssertNoLeak(text);
    }

    [Fact]
    public async Task Hash_mismatch_is_inconsistent_and_hides_every_object_body()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root, "SECRET_PNL_MARKER", "GOOD_REPORT_MARKER");
        RewriteHash(host.Root, "labResultSha256", Utf8.GetBytes("different-bytes"));
        var experiments = await host.Client("personal_intelligence.research.read").GetAsync("/api/personal-intelligence/research/experiments/EXP-ONE");
        var report = await host.Client("personal_intelligence.research.read").GetAsync("/api/personal-intelligence/research/paper/report");
        Assert.Equal(HttpStatusCode.OK, experiments.StatusCode);
        var experimentText = await experiments.Content.ReadAsStringAsync();
        var reportText = await report.Content.ReadAsStringAsync();
        using var experimentJson = JsonDocument.Parse(experimentText);
        using var reportJson = JsonDocument.Parse(reportText);
        Assert.Equal("INCONSISTENT", experimentJson.RootElement.GetProperty("availability").GetString());
        Assert.Equal(JsonValueKind.Null, experimentJson.RootElement.GetProperty("experiment").ValueKind);
        Assert.Equal("INCONSISTENT", reportJson.RootElement.GetProperty("availability").GetString());
        Assert.Equal(JsonValueKind.Null, reportJson.RootElement.GetProperty("report").ValueKind);
        Assert.DoesNotContain("SECRET_PNL_MARKER", experimentText + reportText);
        Assert.DoesNotContain("GOOD_REPORT_MARKER", experimentText + reportText);
    }

    [Fact]
    public async Task Unknown_schema_is_unsupported()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root, "UNSUPPORTED_BODY_MARKER", schema: "research-publication/v9");
        var text = await (await host.Client("personal_intelligence.research.read").GetAsync("/api/personal-intelligence/research/publication")).Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal("UNSUPPORTED_VERSION", body.RootElement.GetProperty("availability").GetString());
        Assert.DoesNotContain("UNSUPPORTED_BODY_MARKER", text);
    }

    [Fact]
    public async Task Invalid_manifest_or_hash_is_rejected()
    {
        await using var host = await ResearchHost.Start();
        var client = host.Client("personal_intelligence.research.read");
        WriteValid(host.Root, "INVALID_MANIFEST_MARKER");
        var manifestPath = Path.Combine(host.Root, "manifest.json");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(manifestPath))!;
        manifest["labResultSha256"] = "not-a-hash";
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest), Utf8);
        var badHash = await client.GetAsync("/api/personal-intelligence/research/publication");
        Assert.Equal("UNREADABLE", (await JsonDocument.ParseAsync(await badHash.Content.ReadAsStreamAsync())).RootElement.GetProperty("availability").GetString());

        manifest.Remove("paperHealthSha256");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest), Utf8);
        var missingHash = await client.GetAsync("/api/personal-intelligence/research/experiments");
        var missingText = await missingHash.Content.ReadAsStringAsync();
        using var missingJson = JsonDocument.Parse(missingText);
        Assert.Equal("UNREADABLE", missingJson.RootElement.GetProperty("availability").GetString());
        Assert.Empty(missingJson.RootElement.GetProperty("experiments").EnumerateArray());
        Assert.DoesNotContain("INVALID_MANIFEST_MARKER", missingText);
    }

    [Fact]
    public async Task Traversal_and_unknown_ids_do_not_leave_the_publication()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root);
        var parent = Directory.GetParent(host.Root)!;
        await File.WriteAllTextAsync(Path.Combine(parent.FullName, "checkpoint.json"), "LEAKED_CHECKPOINT", Utf8);
        await File.WriteAllTextAsync(Path.Combine(host.Root, "checkpoint.json"), "LEAKED_CHECKPOINT", Utf8);
        using var outside = new FileStream(Path.Combine(parent.FullName, "checkpoint.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        using var inside = new FileStream(Path.Combine(host.Root, "checkpoint.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        var client = host.Client("personal_intelligence.research.read");
        var publication = await client.GetAsync("/api/personal-intelligence/research/publication?path=../checkpoint.json");
        Assert.Equal(HttpStatusCode.OK, publication.StatusCode);
        Assert.Equal("FRESH", (await JsonDocument.ParseAsync(await publication.Content.ReadAsStreamAsync())).RootElement.GetProperty("availability").GetString());

        foreach (var path in new[]
        {
            "/api/personal-intelligence/research/experiments/EXP-OTHER",
            "/api/personal-intelligence/research/experiments/..%2Fcheckpoint.json",
            "/api/personal-intelligence/research/experiments/foo%5Cbar",
            "/api/personal-intelligence/research/experiments/checkpoint.json",
        })
        {
            var response = await client.GetAsync(path);
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            AssertNoLeak(text);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                using var body = JsonDocument.Parse(text);
                Assert.Equal("NOT_IN_PUBLICATION", body.RootElement.GetProperty("code").GetString());
            }
        }
    }

    [Fact]
    public async Task Response_omits_state_dir_keys_paths_and_frozen_lab_result()
    {
        await using var host = await ResearchHost.Start();
        WriteValid(host.Root);
        var client = host.Client("personal_intelligence.research.read");
        var combined = new StringBuilder();
        foreach (var path in new[]
        {
            "/api/personal-intelligence/research/publication",
            "/api/personal-intelligence/research/experiments",
            "/api/personal-intelligence/research/experiments/EXP-ONE",
            "/api/personal-intelligence/research/paper/status",
            "/api/personal-intelligence/research/paper/report",
        })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            combined.Append(await response.Content.ReadAsStringAsync());
        }

        var text = combined.ToString();
        AssertNoLeak(text);
        Assert.Contains("SEE_LAB_RESULT_ENDPOINT", text);
        Assert.Contains("EXPERIMENT_NOT_APPROVED", text);
        Assert.Contains("NOT_A_VERIFIED_PERFORMANCE_CLAIM", text);
        using var report = JsonDocument.Parse(await (await client.GetAsync("/api/personal-intelligence/research/paper/report")).Content.ReadAsStringAsync());
        Assert.Equal("NOT_REPORTED_SAMPLE_TOO_SMALL", report.RootElement.GetProperty("report").GetProperty("strategies")[0].GetProperty("netPnl").GetString());
    }

    [Fact]
    public async Task Trading_summary_stays_unchanged_when_research_is_missing_unreadable_or_throws()
    {
        await using var host = await ResearchHost.Start();
        var client = host.Client("personal_intelligence.trading.run");
        string? first = null;
        foreach (var mode in new[] { "missing", "unreadable", "throw" })
        {
            host.Reader.Mode = mode;
            var summary = await client.GetAsync("/api/personal-intelligence/trading/summary");
            Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
            var text = await summary.Content.ReadAsStringAsync();
            first ??= text;
            Assert.Equal(first, text);
            using var body = JsonDocument.Parse(text);
            var root = body.RootElement;
            Assert.Equal(7, root.GetProperty("tradeCount").GetInt32());
            Assert.Equal(3, root.GetProperty("fixtureTradeCount").GetInt32());
            Assert.Equal(4, root.GetProperty("otherSourceTradeCount").GetInt32());
            Assert.Equal(2, root.GetProperty("decisionCount").GetInt32());
            Assert.Equal(1, root.GetProperty("paperExecutionCount").GetInt32());
            Assert.Equal(0, root.GetProperty("riskVetoCount").GetInt32());
            Assert.Equal("FIXTURE", root.GetProperty("latestSource").GetString());
            Assert.Equal(SummaryAt, root.GetProperty("latestRecordedAt").GetDateTimeOffset());
            Assert.False(root.GetProperty("onlyFixtureData").GetBoolean());
            Assert.Equal("missing-fee", root.GetProperty("missingData")[0].GetString());
            Assert.Equal(1, root.GetProperty("importTradeCount").GetInt32());
            Assert.Equal(0, root.GetProperty("exchangeTradeCount").GetInt32());

            var research = await client.GetAsync("/api/personal-intelligence/research/publication");
            var researchText = await research.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, research.StatusCode);
            Assert.DoesNotContain("secret-root-marker", researchText);
            Assert.DoesNotContain("supersecret", researchText);
        }
    }

    static async Task AssertMissingWithoutNumbers(HttpClient client)
    {
        var response = await client.GetAsync("/api/personal-intelligence/research/experiments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal("MISSING", body.RootElement.GetProperty("availability").GetString());
        Assert.Empty(body.RootElement.GetProperty("experiments").EnumerateArray());
        AssertNoFinancialPlaceholder(text);
    }

    static async Task<JsonElement> Read(ResearchHost host, string path)
    {
        var response = await host.Client("personal_intelligence.research.read").GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    static void AssertNoFinancialPlaceholder(string text)
    {
        Assert.DoesNotContain("netPnl", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profitFactor", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tradeCount", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("winRate", text, StringComparison.OrdinalIgnoreCase);
    }

    static void AssertNoLeak(string text)
    {
        Assert.DoesNotContain("PAPER_OPS_STATE_DIR", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frozen_lab_result", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-root-marker", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LEAKED_CHECKPOINT", text, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\", text, StringComparison.Ordinal);
    }

    static void Touch1999(string root)
    {
        var stamp = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, stamp);
    }

    static void RewriteHash(string root, string field, byte[] bytes)
    {
        var manifestPath = Path.Combine(root, "manifest.json");
        var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath))!;
        manifest[field] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Utf8);
    }

    static void WriteValid(string root, string? labMarker = null, string? reportMarker = null, string health = "OK", long? lastBarAgeMs = 10, string schema = ResearchPublicationSchema.V1)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
        Directory.CreateDirectory(Path.Combine(root, "lab"));
        Directory.CreateDirectory(Path.Combine(root, "paper"));
        var lab = JsonSerializer.Serialize(new
        {
            experiments = new[]
            {
                new
                {
                    experiment_id = "EXP-ONE",
                    strategy_id = labMarker ?? "S1",
                    strategy_version = "v1",
                    family = "trend",
                    symbol = "SOLUSDT",
                    timeframe = "15m",
                    data_source = "fixture",
                    snapshot_sha256 = "abc",
                    approval_status = "EXPERIMENT_NOT_APPROVED",
                    evidence_status = "INSUFFICIENT_EVIDENCE",
                    performance_claim = "NOT_A_VERIFIED_PERFORMANCE_CLAIM",
                    risk_limits_approval = "NOT_APPROVED",
                    selected_by_pnl = false,
                    parameters_chosen_on_out_of_sample = false,
                    development = new { win_rate_is_performance_evidence = false },
                    out_of_sample = new { profit_factor_is_performance_evidence = false },
                    live_orders = false,
                    exchange_orders_sent = 0,
                },
            },
        });
        var status = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["runner_status"] = "SUCCEEDED",
            ["cycle_outcome"] = "NO_NEW_SIGNAL",
            ["last_run_at"] = SourceRunAt,
            ["last_processed_open_time"] = 1700000000000,
            ["last_error"] = null,
            ["live_orders"] = false,
            ["exchange_orders_sent"] = 0,
            ["effective_live_mode"] = "LIVE_DISABLED",
            ["risk_limits_approval"] = "NOT_APPROVED",
            ["extension_count"] = 1,
            ["extension_sha256"] = "aa",
            ["api_key_used"] = false,
            ["api_key_present_ignored"] = true,
            ["paper_ops_state_dir"] = "E:\\secret-root-marker\\state",
        });
        var report = JsonSerializer.Serialize(new
        {
            report_kind = "PAPER_EXTENSION",
            symbol = "SOLUSDT",
            timeframe = "15m",
            extension_bar_count = 2,
            extension_start_utc = "2026-10-21T09:00:00+00:00",
            extension_end_utc = "2026-10-21T10:00:00+00:00",
            strategies = new[]
            {
                new
                {
                    experiment_id = "EXP-ONE",
                    extension_bars_represented = 2,
                    closed_trades = 1,
                    net_pnl = "NOT_REPORTED_SAMPLE_TOO_SMALL",
                    sample_warning = reportMarker ?? "SAMPLE_TOO_SMALL",
                    stand_aside_net_pnl = "0",
                },
            },
            frozen_lab_result = "research-lab-v1/output/LAB_RESULT.json",
            performance_claim = "NOT_A_VERIFIED_PERFORMANCE_CLAIM",
            live_orders = false,
            exchange_orders_sent = 0,
            effective_live_mode = "LIVE_DISABLED",
        });
        var healthJson = lastBarAgeMs is null
            ? JsonSerializer.Serialize(new { health, last_run_at = SourceRunAt })
            : JsonSerializer.Serialize(new { health, last_run_at = SourceRunAt, last_bar_age_ms = lastBarAgeMs });
        var files = new Dictionary<string, byte[]>
        {
            ["lab/LAB_RESULT.json"] = Utf8.GetBytes(lab),
            ["paper/PAPER_OPS_STATUS.json"] = Utf8.GetBytes(status),
            ["paper/PAPER_RESEARCH_REPORT.json"] = Utf8.GetBytes(report),
            ["paper/PAPER_OPS_HEALTH.json"] = Utf8.GetBytes(healthJson),
        };
        foreach (var (relative, bytes) in files)
            File.WriteAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), bytes);
        var manifest = new Dictionary<string, string>
        {
            ["schemaVersion"] = schema,
            ["publicationId"] = "pub-fixture00000001",
            ["publishedAt"] = PublishedAt,
            ["publisher"] = "research-lab-v1",
            ["labResultSha256"] = Hash(files["lab/LAB_RESULT.json"]),
            ["paperStatusSha256"] = Hash(files["paper/PAPER_OPS_STATUS.json"]),
            ["paperReportSha256"] = Hash(files["paper/PAPER_RESEARCH_REPORT.json"]),
            ["paperHealthSha256"] = Hash(files["paper/PAPER_OPS_HEALTH.json"]),
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest), Utf8);
    }

    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    sealed class ResearchHost : IAsyncDisposable
    {
        public WebApplication App { get; }
        public string Root { get; }
        public FlagSettings Settings { get; }
        public ModeReader Reader { get; }

        ResearchHost(WebApplication app, string root, FlagSettings settings, ModeReader reader)
        {
            App = app;
            Root = root;
            Settings = settings;
            Reader = reader;
        }

        public static async Task<ResearchHost> Start()
        {
            var root = Path.Combine(Path.GetTempPath(), "pi-research-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ContentRootPath = Path.GetTempPath(),
                ApplicationName = typeof(ResearchPublicationTests).Assembly.GetName().Name,
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddAuthentication(HeaderAuth.AuthScheme)
                .AddScheme<AuthenticationSchemeOptions, HeaderAuth>(HeaderAuth.AuthScheme, _ => { });
            builder.Services.AddAuthorization(options => options.AddPersonalIntelligenceAuthorization());
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(ResearchPublicationController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
            var settings = new FlagSettings();
            var reader = new ModeReader { Root = root };
            builder.Services.AddSingleton<ITenantPlatformSettings>(settings);
            builder.Services.AddSingleton<IResearchPublicationReader>(reader);
            builder.Services.AddSingleton<ITradingDesk, FixedDesk>();
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return new ResearchHost(app, root, settings, reader);
        }

        public HttpClient Anonymous() => App.GetTestClient();

        public HttpClient Client(string? permission = null, string? role = null)
        {
            var client = App.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("X-Test-Tenant", Guid.NewGuid().ToString());
            if (!string.IsNullOrWhiteSpace(permission))
                client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
            if (!string.IsNullOrWhiteSpace(role))
                client.DefaultRequestHeaders.Add("X-Test-Role", role);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }

    sealed class FlagSettings : ITenantPlatformSettings
    {
        public bool Enabled { get; set; } = true;
        public Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enabled && moduleCode == PersonalIntelligencePolicies.ModuleCode);
        public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(UpdateTenantPlatformSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsModuleEnabledForTenantCodeAsync(string tenantCode, string moduleCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFeatureEnabledForTenantCodeAsync(string tenantCode, string featureCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(Guid? branchId = null, Guid? customerAccountId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class ModeReader : IResearchPublicationReader
    {
        public string Mode { get; set; } = "file";
        public string Root { get; set; } = "";

        public Task<ResearchPublicationRead> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (Mode == "throw")
                throw new InvalidOperationException(@"E:\secret-root-marker\checkpoint.json api_key=supersecret PAPER_OPS_STATE_DIR");
            if (Mode == "missing")
                return Task.FromResult(Blank(ResearchAvailability.Missing, "MISSING"));
            if (Mode == "unreadable")
                return Task.FromResult(Blank(ResearchAvailability.Unreadable, "UNREADABLE"));
            return new FileResearchPublicationReader(Options.Create(new ResearchPublicationOptions { Root = Root })).ReadAsync(cancellationToken);
        }

        static ResearchPublicationRead Blank(string availability, string code) => new(
            availability, null, null, null, null, [], new ResearchReadError(code, "Publication is not available."), [], null, null);
    }

    sealed class HeaderAuth(
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
            if (Request.Headers.TryGetValue("X-Test-Role", out var role) && !string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, AuthScheme)), AuthScheme)));
        }
    }

    sealed class FixedDesk : ITradingDesk
    {
        static readonly TradingSummaryDto Summary = new(7, 3, 4, 2, 1, 0, "FIXTURE", SummaryAt, false, ["missing-fee"], 1, 0);
        public Task<TradingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) => Task.FromResult(Summary);
        public Task<IReadOnlyList<TradingAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<TradeRowDto>> ListTradesAsync(TradeListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeRowDto?> GetTradeAsync(Guid tradeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<DecisionRowDto>> ListDecisionsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DecisionDetailDto?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<OperationRowDto>> ListOperationsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OperationRowDto?> GetOperationAsync(string operationKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<PaperExecutionRowDto>> ListPaperExecutionsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeImportPreviewDto> PreviewImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeImportCommitDto> CommitImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaperApprovalDto> ApprovePaperAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
