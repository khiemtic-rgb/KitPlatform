using System.Net;
using Xunit;
using System.Net.Http.Json;
using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchDeskV1Tests
{
    [Fact]
    public void Desk_rejects_live_execution_and_limits_outside_the_paper_boundary()
    {
        Assert.Contains("live", Assert.Throws<ResearchDeskException>(() => ResearchDesk.Normalize(new ResearchDeskRequest { Live = true })).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ResearchDeskException>(() => ResearchDesk.Normalize(new ResearchDeskRequest { Symbol = "ETHUSDT" }));
        Assert.Throws<ResearchDeskException>(() => ResearchDesk.Normalize(new ResearchDeskRequest { MaxLeverage = 2m }));
        Assert.Throws<ResearchDeskException>(() => ResearchDesk.Normalize(new ResearchDeskRequest { Quantity = 1m }));
        Assert.Throws<ResearchDeskException>(() => ResearchDesk.Normalize(new ResearchDeskRequest { DataSource = "novixa_prod" }));
    }

    [Fact]
    public async Task Desk_runs_the_research_host_and_a_repeat_is_replay_not_a_new_fill()
    {
        var root = Directory.CreateTempSubdirectory("research-desk-");
        try
        {
            var first = await ResearchDesk.ExecuteAsync(root.FullName, new ResearchDeskRequest { DataSource = "fixture" });
            var second = await ResearchDesk.ExecuteAsync(root.FullName, new ResearchDeskRequest { DataSource = "fixture" });

            Assert.Equal("COMPLETED", first.Status);
            Assert.False(first.Replayed);
            Assert.True(first.NewFills > 0);
            Assert.Equal("fixture", first.DataSource);
            Assert.Equal("derived_from_execution", first.NarrativeKind);
            Assert.NotNull(first.Document);
            Assert.Equal("fixture", first.Document!.Source);
            Assert.True(first.Document.Overview.PaperFills > 0);
            Assert.Equal("REPLAYED", second.Status);
            Assert.True(second.Replayed);
            Assert.Equal(0, second.NewFills);
            Assert.Contains("REPLAYED", second.ReportText, StringComparison.Ordinal);
            Assert.Equal(2, ResearchDesk.List(root.FullName).Count);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Open_position_fixture_keeps_pnl_unknown()
    {
        var root = Directory.CreateTempSubdirectory("research-desk-open-");
        try
        {
            var task = await ResearchDesk.ExecuteAsync(root.FullName, new ResearchDeskRequest { DataSource = "fixture" }, ResearchDesk.OpenPositionFixture());

            Assert.Equal("COMPLETED", task.Status);
            Assert.NotNull(task.Document);
            Assert.False(task.Document!.Overview.RealizedPnl.Known);
            Assert.Null(task.Document.Overview.RealizedPnl.Amount);
            Assert.NotEqual("0", task.Document.Overview.RealizedPnl.Status);
            Assert.Contains("không phải 0", task.ReportText, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public async Task Http_desk_executes_when_enabled_and_refuses_when_disabled()
    {
        var root = Directory.CreateTempSubdirectory("research-desk-http-");
        var fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Packs", "PersonalIntelligence",
            "KitPlatform.Packs.PersonalIntelligence.ResearchDashboard", "fixture", "pilot.json"));
        try
        {
            await using var enabled = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Dashboard:Fixture", fixture);
                builder.UseSetting("Dashboard:AllowExecute", "true");
                builder.UseSetting("Dashboard:DeskRoot", root.FullName);
                builder.UseSetting("Jwt:Secret", DeskJwt.Secret);
                builder.UseSetting("Jwt:Issuer", DeskJwt.Issuer);
                builder.UseSetting("Jwt:Audience", DeskJwt.Audience);
            });
            var anonymous = await enabled.CreateClient().PostAsJsonAsync("/api/personal-intelligence/trading-research/tasks", new { dataSource = "fixture" });
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            var client = enabled.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", DeskJwt.Token("permission", "personal_intelligence.trading.run"));
            var ran = await client.PostAsJsonAsync("/api/personal-intelligence/trading-research/tasks", new { dataSource = "fixture" });
            var body = await ran.Content.ReadFromJsonAsync<JsonElement>();
            var desk = await client.GetFromJsonAsync<JsonElement>("/api/personal-intelligence/trading-research/desk");
            var live = await client.PostAsJsonAsync("/api/personal-intelligence/trading-research/tasks", new { live = true });

            Assert.Equal(HttpStatusCode.OK, ran.StatusCode);
            Assert.Equal("fixture", body.GetProperty("dataSource").GetString());
            Assert.Equal("COMPLETED", body.GetProperty("status").GetString());
            Assert.True(body.GetProperty("document").GetProperty("overview").GetProperty("paperFills").GetInt32() > 0);
            Assert.True(desk.GetProperty("executeEnabled").GetBoolean());
            Assert.Equal(HttpStatusCode.BadRequest, live.StatusCode);

            await using var disabled = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Dashboard:Fixture", fixture);
                builder.UseSetting("Dashboard:DeskRoot", root.FullName);
                builder.UseSetting("Jwt:Secret", DeskJwt.Secret);
                builder.UseSetting("Jwt:Issuer", DeskJwt.Issuer);
                builder.UseSetting("Jwt:Audience", DeskJwt.Audience);
            });
            var blockedClient = disabled.CreateClient();
            blockedClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", DeskJwt.Token("permission", "personal_intelligence.trading.run"));
            var blocked = await blockedClient.PostAsJsonAsync("/api/personal-intelligence/trading-research/tasks", new { dataSource = "fixture" });
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        }
        finally
        {
            root.Delete(true);
        }
    }
}

public static class DeskJwt
{
    public const string Secret = "research-desk-test-secret-32chars-min";
    public const string Issuer = "PiValidationLocal";
    public const string Audience = "KitPlatform.Client";

    public static string Token(string claimType, string claimValue)
        => ResearchDeskJwt.Sign(Secret, Issuer, Audience, [(claimType, claimValue)], DateTimeOffset.UtcNow.AddMinutes(5));
}
