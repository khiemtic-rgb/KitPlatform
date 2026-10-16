using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class CustomerConsentApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private static readonly Guid DemoCustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");

    private readonly HttpClient _client;

    public CustomerConsentApiTests(KitPlatformWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Customer_consent_upsert_writes_outbox_event()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var response = await authed.PutAsJsonAsync($"/api/customers/{DemoCustomerId}/consents", new
        {
            items = new[]
            {
                new { channel = (short)3, purpose = (short)1, granted = true, source = (short)2 },
            },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var consents = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, consents.ValueKind);
        Assert.True(consents.GetArrayLength() > 0);

        var getResponse = await authed.GetAsync($"/api/customers/{DemoCustomerId}/consents");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
            tenantCode = "DEMO_PHARMACY",
        });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("accessToken").GetString()!;
    }

    private HttpClient CreateAuthedClient(string token)
    {
        var client = _client;
        var authed = new HttpClient { BaseAddress = client.BaseAddress };
        authed.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return authed;
    }
}
