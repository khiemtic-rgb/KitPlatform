using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class CatalogApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private readonly KitPlatformWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CatalogApiTests(KitPlatformWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Catalog_products_requires_authentication()
    {
        var response = await _client.GetAsync("/api/catalog/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Catalog_ingredients_crud_with_admin_token()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var code = $"TEST_{Guid.NewGuid():N}"[..20];
        var createResponse = await authed.PostAsJsonAsync("/api/catalog/ingredients", new
        {
            ingredientCode = code,
            ingredientName = "Test Ingredient",
            description = "Integration test",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var listResponse = await authed.GetAsync("/api/catalog/ingredients?activeOnly=true");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var updateResponse = await authed.PutAsJsonAsync($"/api/catalog/ingredients/{id}", new
        {
            ingredientName = "Test Ingredient Updated",
            description = "Updated",
            status = (short)1,
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var deleteResponse = await authed.DeleteAsync($"/api/catalog/ingredients/{id}");
        Assert.True(
            deleteResponse.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest,
            $"Unexpected status: {deleteResponse.StatusCode}");
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantCode = "DEMO_PHARMACY",
            username = "admin",
            password = "Admin@123",
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Login failed ({response.StatusCode}). Ensure PostgreSQL is running with demo seed.");
        }

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
