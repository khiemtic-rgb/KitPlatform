using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace KitPlatform.SecondBrainAgent.Tests;

public sealed class SessionSecurityTests : IClassFixture<AgentFactory>
{
    readonly AgentFactory _factory;

    public SessionSecurityTests(AgentFactory factory) => _factory = factory;

    [Fact]
    public async Task Logout_revokes_only_that_session_and_rotation_keeps_the_sid()
    {
        using var client = _factory.CreateClient();
        var first = await Login(client);
        var second = await Login(client);
        var sid = Claim(first.Access, "sid");
        Assert.False(string.IsNullOrWhiteSpace(sid));
        Assert.NotEqual(sid, Claim(second.Access, "sid"));
        Assert.False(Payload(first.Access).TryGetProperty("role", out _));

        var rotated = await Refresh(client, first.Refresh);
        Assert.Equal(sid, Claim(rotated.Access, "sid"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.Refresh })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = rotated.Refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, rotated.Access));
        Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, first.Access));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rotated.Refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await Me(client, second.Access));

        var kept = await Refresh(client, second.Refresh);
        Assert.Equal(Claim(second.Access, "sid"), Claim(kept.Access, "sid"));
    }

    [Fact]
    public async Task Refresh_and_logout_cannot_leave_the_session_active()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var target = await Login(client);
            var other = await Login(client);
            var refresh = client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = target.Refresh });
            var logout = client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = target.Refresh });
            await Task.WhenAll(refresh, logout);
            var refreshed = await refresh;
            if (refreshed.StatusCode == HttpStatusCode.OK)
            {
                var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, body.GetProperty("accessToken").GetString()!));
            }
            Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, target.Access));
            Assert.Equal(HttpStatusCode.OK, await Me(client, other.Access));
        }
    }

    [Fact]
    public async Task Missing_or_foreign_sid_is_rejected_and_permissions_stay_separate()
    {
        using var client = _factory.CreateClient();
        var session = await Login(client);
        var sid = Claim(session.Access, "sid");
        var userId = Claim(session.Access, "sub");
        var tenantId = Claim(session.Access, "tenant_id");

        Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, Sign(new Dictionary<string, object>
        {
            ["iss"] = "SecondBrainAgent",
            ["aud"] = "SecondBrainAgent",
            ["sub"] = userId,
            ["tenant_id"] = tenantId,
            ["tenant_code"] = "SECOND_BRAIN",
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        })));
        Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, Sign(SessionClaims(sid, Guid.NewGuid().ToString(), tenantId))));
        Assert.Equal(HttpStatusCode.Unauthorized, await Me(client, Sign(SessionClaims(sid, userId, Guid.NewGuid().ToString()))));

        var research = Sign(SessionClaims(sid, userId, tenantId, "personal_intelligence.research.read"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", research);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/personal-intelligence/research/publication")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/personal-intelligence/trading/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/personal-intelligence/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/personal-intelligence/trading/exchanges/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "read" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/personal-intelligence/trading/decisions/{Guid.NewGuid()}/paper", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/personal-intelligence/trading/loops", new { live = true })).StatusCode);

        var reader = Sign(SessionClaims(sid, userId, tenantId, "personal_intelligence.task.read"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reader);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/personal-intelligence/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "read" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/personal-intelligence/trading/decisions/{Guid.NewGuid()}/paper", null)).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/personal-intelligence/trading/summary")).StatusCode);
    }

    static async Task<Session> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "sb-operator",
            password = AgentFactory.Password,
            tenantCode = "SECOND_BRAIN",
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var json = JsonDocument.Parse(body).RootElement;
        return new Session(json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    static async Task<Session> Refresh(HttpClient client, string refresh)
    {
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var json = JsonDocument.Parse(body).RootElement;
        return new Session(json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    static async Task<HttpStatusCode> Me(HttpClient client, string access)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return (await client.SendAsync(request)).StatusCode;
    }

    static Dictionary<string, object> SessionClaims(string sid, string userId, string tenantId, params string[] permissions)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = "SecondBrainAgent",
            ["aud"] = "SecondBrainAgent",
            ["sub"] = userId,
            ["tenant_id"] = tenantId,
            ["tenant_code"] = "SECOND_BRAIN",
            ["sid"] = sid,
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        };
        if (permissions.Length > 0)
            claims["permission"] = permissions;
        return claims;
    }

    static string Sign(Dictionary<string, object> payload)
    {
        var header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var body = B64(JsonSerializer.Serialize(payload));
        var signature = Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(AgentFactory.Secret),
            Encoding.UTF8.GetBytes(header + "." + body))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return header + "." + body + "." + signature;
    }

    static string B64(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static string Claim(string jwt, string name) => Payload(jwt).GetProperty(name).GetString() ?? "";

    static JsonElement Payload(string jwt)
    {
        var part = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        while (part.Length % 4 != 0)
            part += "=";
        return JsonDocument.Parse(Convert.FromBase64String(part)).RootElement.Clone();
    }

    sealed record Session(string Access, string Refresh);
}
