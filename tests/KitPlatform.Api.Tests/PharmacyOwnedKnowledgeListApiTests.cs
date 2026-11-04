using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

/// <summary>
/// Owned-knowledge list and in-use read on the release schema (migration 433 adds review_due_on as DATE).
/// Dapper builds the record constructor map before reading rows, so an empty tenant still exercises the mapping.
/// </summary>
public sealed class PharmacyOwnedKnowledgeListApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private readonly KitPlatformWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PharmacyOwnedKnowledgeListApiTests(KitPlatformWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/pharmacy/owned-knowledge")]
    [InlineData("/api/pharmacy/owned-knowledge/in-use")]
    public async Task Owned_knowledge_reads_map_review_due_on(string path)
    {
        using var authed = CreateAuthedClient(await LoginAsync());
        var response = await authed.GetAsync(path);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantCode = "DEMO_PHARMACY",
            username = "admin",
            password = "Admin@123",
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Missing access token.");
    }

    private HttpClient CreateAuthedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
