using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

/// <summary>
/// Auth:DefaultTenantCode is empty in the test host, and Assessment:EventTenantCode is NVX-CS08.
/// A login that omits tenantCode must fail closed. It must not issue a session for NVX-CS08.
/// </summary>
public sealed class DefaultTenantResolutionTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DefaultTenantResolutionTests(KitPlatformWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Login_without_tenant_fails_and_does_not_enter_NVX_CS08()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Mã nhà thuốc là bắt buộc", body, StringComparison.Ordinal);
        Assert.DoesNotContain("NVX-CS08", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);

        using var payload = JsonDocument.Parse(body);
        Assert.False(payload.RootElement.TryGetProperty("user", out _));
        Assert.False(payload.RootElement.TryGetProperty("tenantCode", out _));
    }

    [Fact]
    public async Task Explicit_demo_pharmacy_login_is_not_NVX_CS08()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
            tenantCode = "DEMO_PHARMACY",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("NVX-CS08", body, StringComparison.OrdinalIgnoreCase);

        using var payload = JsonDocument.Parse(body);
        var tenantCode = payload.RootElement.GetProperty("user").GetProperty("tenantCode").GetString();
        Assert.Equal("DEMO_PHARMACY", tenantCode);
    }
}
