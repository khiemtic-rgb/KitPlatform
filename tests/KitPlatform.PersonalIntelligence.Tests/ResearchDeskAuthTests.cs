using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchDeskAuthTests
{
    [Fact]
    public async Task Desk_rejects_missing_invalid_and_unauthorized_tokens()
    {
        await using var factory = Factory(configured: true, allowExecute: true);
        var client = factory.CreateClient();

        var missing = await client.GetAsync("/api/personal-intelligence/trading-research/desk");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-jwt");
        var invalid = await client.GetAsync("/api/personal-intelligence/trading-research/desk");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", NoneAlgorithmToken());
        var none = await client.GetAsync("/api/personal-intelligence/trading-research/desk");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", DeskJwt.Token("permission", "personal_intelligence.exchange.read"));
        var wrong = await client.PostAsJsonAsync("/api/personal-intelligence/trading-research/tasks", new { dataSource = "fixture" });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", DeskJwt.Token("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "ADMIN"));
        var admin = await client.GetAsync("/api/personal-intelligence/trading-research/desk");
        var dashboard = await factory.CreateClient().GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Fact]
    public async Task Desk_stays_closed_when_jwt_is_not_configured()
    {
        await using var factory = Factory(configured: false, allowExecute: true);
        var status = await factory.CreateClient().GetAsync("/api/personal-intelligence/trading-research/desk");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
    }

    static WebApplicationFactory<Program> Factory(bool configured, bool allowExecute)
    {
        var fixture = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Packs", "PersonalIntelligence",
            "KitPlatform.Packs.PersonalIntelligence.ResearchDashboard", "fixture", "pilot.json"));
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Dashboard:Fixture", fixture);
            builder.UseSetting("Dashboard:AllowExecute", allowExecute ? "true" : "false");
            builder.UseSetting("Dashboard:DeskRoot", Path.GetTempPath());
            if (!configured)
                return;
            builder.UseSetting("Jwt:Secret", DeskJwt.Secret);
            builder.UseSetting("Jwt:Issuer", DeskJwt.Issuer);
            builder.UseSetting("Jwt:Audience", DeskJwt.Audience);
        });
    }

    static string NoneAlgorithmToken()
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"permission\":\"personal_intelligence.trading.run\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return header + "." + payload + ".";
    }
}
