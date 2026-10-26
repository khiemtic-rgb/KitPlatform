using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchDashboardV1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reader_sql_is_select_only_inside_research_schema()
    {
        Assert.NotEmpty(ResearchReadSql.Statements);
        foreach (var sql in ResearchReadSql.Statements)
        {
            var compact = string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
            Assert.StartsWith("SELECT ", compact);
            Assert.DoesNotContain("INSERT", compact);
            Assert.DoesNotContain("UPDATE", compact);
            Assert.DoesNotContain("DELETE", compact);
            Assert.Contains("RESEARCH_TRADING.", compact);
            Assert.DoesNotContain("NOVIXA", compact);
            Assert.DoesNotContain("PUBLIC.", compact);
        }
    }

    [Fact]
    public void Missing_data_is_empty_and_pnl_is_unknown()
    {
        var document = ResearchReadModel.Build(new ResearchReadBundle(0, [], [], [], [], [], []), Now, "fixture");

        Assert.Equal("NO_DATA", document.Overview.Status);
        Assert.Null(document.Overview.LastRunAt);
        Assert.Equal(0, document.Overview.Candles);
        Assert.Equal("0", document.Overview.Fees);
        Assert.Equal("0", document.Overview.CashFromFills);
        Assert.False(document.Overview.RealizedPnl.Known);
        Assert.Null(document.Overview.RealizedPnl.Amount);
        Assert.Equal("unavailable", document.Overview.RealizedPnl.Status);
        Assert.Empty(document.Candles);
        Assert.Empty(document.Signals);
        Assert.Empty(document.Fills);
        Assert.Empty(document.Runs);
    }

    [Fact]
    public void Replay_keeps_null_pnl_distinct_from_zero_and_counts_one_replay()
    {
        var bundle = new ResearchReadBundle(
            48,
            [],
            [],
            [],
            [new FillInput("op", "BUY", 81889.32m, 81971.20932m, 0.001m, 0.08197121m, 0.08188932m, -82.05318053m)],
            [
                new RunInput(2, Now, "REPLAYED", "http=1;weight1m=2;inserted=1;duplicates=46;new_fills=0;replayed=True"),
                new RunInput(1, Now.AddHours(-1), "COMPLETED", "http=1;weight1m=2;inserted=47;duplicates=0;new_fills=1;replayed=False"),
            ],
            [new SessionInput(Now, "COMPLETED", null, "not_recomputed_on_replay")]);

        var document = ResearchReadModel.Build(bundle, Now, "research_trading");

        Assert.Equal("REPLAYED", document.Overview.Status);
        Assert.Equal(1, document.Overview.Replays);
        Assert.Equal(0, document.Runs[0].NewFills);
        Assert.Equal(46, document.Runs[0].Duplicates);
        Assert.Equal(1, document.Runs[0].Inserted);
        Assert.False(document.Overview.RealizedPnl.Known);
        Assert.Null(document.Overview.RealizedPnl.Amount);
        Assert.Equal("not_recomputed_on_replay", document.Overview.RealizedPnl.Status);
        Assert.Equal("-82.05318053", document.Overview.CashFromFills);
    }

    [Fact]
    public void Realized_zero_is_shown_as_zero()
    {
        var document = ResearchReadModel.Build(
            new ResearchReadBundle(0, [], [], [], [], [], [new SessionInput(Now, "COMPLETED", 0m, "realized")]),
            Now,
            "fixture");

        Assert.True(document.Overview.RealizedPnl.Known);
        Assert.Equal("0", document.Overview.RealizedPnl.Amount);
    }

    [Fact]
    public void Stored_snapshot_json_preserves_null_and_zero_pnl()
    {
        var unknown = JsonSerializer.Serialize(SampleSnapshot(null, "no_closed_position"));
        var zero = JsonSerializer.Serialize(SampleSnapshot(0m, "realized"));

        Assert.Null(ResearchReadModel.ReadPnl(unknown));
        Assert.Equal("no_closed_position", ResearchReadModel.ReadPnlStatus(unknown));
        Assert.Equal(0m, ResearchReadModel.ReadPnl(zero));
        Assert.Equal("realized", ResearchReadModel.ReadPnlStatus(zero));
    }

    [Fact]
    public void Open_candle_is_marked_incomplete_and_refused_risk_keeps_its_reason()
    {
        var bundle = new ResearchReadBundle(
            1,
            [new CandleInput("BTCUSDT", "1h", Now, Now.AddHours(1), 1m, 2m, 1m, 2m, Now)],
            [new SignalInput("sma-cross-v1:BTCUSDT:1h:1:BUY", "BTCUSDT", "BUY", Now, null, null, "awaiting_next_open")],
            [new DecisionInput("sma-cross-v1:BTCUSDT:1h:1:BUY", false, "session notional")],
            [],
            [],
            []);

        var document = ResearchReadModel.Build(bundle, Now, "fixture");

        Assert.False(document.Candles[0].Closed);
        Assert.Equal("sma-cross-v1", document.Signals[0].Strategy);
        Assert.False(document.Signals[0].RiskAllowed);
        Assert.Equal("session notional", document.Signals[0].RiskReason);
    }

    [Fact]
    public void Connection_file_refuses_production_api_and_novixa_database()
    {
        var env = Path.Combine(Path.GetTempPath(), "research-dashboard-" + Guid.NewGuid().ToString("N"), "api.env");
        Directory.CreateDirectory(Path.GetDirectoryName(env)!);
        File.WriteAllText(env, "Host=127.0.0.1;Database=trading_research;Username=reader;Password=secret");
        var database = Path.Combine(Path.GetTempPath(), "research-dashboard-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(database, "Host=127.0.0.1;Database=novixa_prod;Username=reader;Password=secret");

        Assert.Throws<InvalidOperationException>(() => DashboardConnection.ReadFile(env));
        Assert.Throws<InvalidOperationException>(() => DashboardConnection.ReadFile(database));
        Assert.Throws<InvalidOperationException>(() => new SqlResearchReader("Host=127.0.0.1;Port=1;Database=novixa_prod;Username=reader;Password=secret"));
        var collectorFile = Path.Combine(Path.GetTempPath(), "research-dashboard-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(collectorFile, "Host=127.0.0.1;Port=1;Database=TRADING_RESEARCH;Username=reader;Password=secret");
        var collector = Assert.Throws<InvalidOperationException>(() => DashboardConnection.ReadFile(collectorFile));
        Assert.Contains("trading_research", collector.Message, StringComparison.OrdinalIgnoreCase);
        var reader = Assert.Throws<InvalidOperationException>(() => new SqlResearchReader("Host=127.0.0.1;Port=1;Database=Trading_Research;Username=reader;Password=secret"));
        Assert.Contains("trading_research", reader.Message, StringComparison.OrdinalIgnoreCase);
        var otherFile = Path.Combine(Path.GetTempPath(), "research-dashboard-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(otherFile, "Host=127.0.0.1;Port=1;Database=kit_other;Username=reader");
        var other = Assert.Throws<InvalidOperationException>(() => DashboardConnection.ReadFile(otherFile));
        Assert.Contains("kit_other", other.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => new SqlResearchReader("Host=127.0.0.1;Port=1;Database=kit_other;Username=reader"));
        var allowed = Path.Combine(Path.GetTempPath(), "research-dashboard-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(allowed, "Host=127.0.0.1;Port=1;Database=TRADING_AGENT;Username=reader");
        var read = DashboardConnection.ReadFile(allowed);
        Assert.Contains("trading_agent", read, StringComparison.OrdinalIgnoreCase);
        var accepted = Record.Exception(() => new SqlResearchReader("Host=127.0.0.1;Port=1;Database=Trading_Agent;Username=reader"));
        Assert.NotNull(accepted);
        Assert.IsNotType<InvalidOperationException>(accepted);
    }

    [Fact]
    public void Dashboard_binds_only_the_trading_agent_port()
    {
        Assert.Equal("http://127.0.0.1:5090", DashboardEndpoint.DefaultUrl);
        DashboardEndpoint.Require(null);
        DashboardEndpoint.Require("http://127.0.0.1:5090");
        DashboardEndpoint.Require("HTTP://127.0.0.1:5090/");
        var collectorPort = Assert.Throws<InvalidOperationException>(() => DashboardEndpoint.Require("http://127.0.0.1:5088"));
        Assert.Contains("127.0.0.1:5090", collectorPort.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => DashboardEndpoint.Require("http://localhost:5090"));
        Assert.Throws<InvalidOperationException>(() => DashboardEndpoint.Require("http://0.0.0.0:5090"));
    }

    [Fact]
    public async Task Dashboard_serves_fixture_without_a_write_route()
    {
        var fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Packs", "PersonalIntelligence",
            "KitPlatform.Packs.PersonalIntelligence.ResearchDashboard",
            "fixture", "pilot.json"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Dashboard:Fixture", fixture);
        });
        var client = factory.CreateClient();

        var page = await client.GetAsync("/");
        var body = await page.Content.ReadAsStringAsync();
        var data = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        var post = await client.PostAsync("/api/dashboard", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Research desk", body);
        Assert.Equal("fixture", data.GetProperty("source").GetString());
        Assert.False(data.GetProperty("overview").GetProperty("realizedPnl").GetProperty("known").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("overview").GetProperty("realizedPnl").GetProperty("amount").ValueKind);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    private static ResearchSnapshot SampleSnapshot(decimal? pnl, string status)
        => new(status, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0m, 0m, pnl, status, null, null, "unavailable", [], [], []);
}
