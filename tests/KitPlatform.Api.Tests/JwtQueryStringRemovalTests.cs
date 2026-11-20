using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Configuration;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class JwtQueryStringRemovalTests : IClassFixture<HeaderOnlyJwtFactory>
{
    private readonly HeaderOnlyJwtFactory _factory;

    public JwtQueryStringRemovalTests(HeaderOnlyJwtFactory factory) => _factory = factory;

    [Fact]
    public async Task Header_jwt_authenticates_a_protected_upload()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = _factory.CreateToken(tenant);

        var response = await SendAsync($"/uploads/products/{tenant:N}/missing.jpg", token, queryToken: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Query_jwt_alone_is_not_authenticated()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = _factory.CreateToken(tenant);

        var response = await SendAsync($"/uploads/products/{tenant:N}/missing.jpg", headerToken: null, queryToken: token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Header_jwt_stays_authoritative_when_a_query_token_is_also_present()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var header = _factory.CreateToken(tenant);

        var response = await SendAsync(
            $"/uploads/products/{tenant:N}/missing.jpg",
            header,
            queryToken: "not-a-jwt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_query_jwt_does_not_authenticate()
    {
        var response = await SendAsync(
            "/uploads/products/11111111111111111111111111111111/missing.jpg",
            headerToken: null,
            queryToken: "not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_authorization_header_stays_anonymous_on_a_protected_route()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/sales/customer-chat/threads");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_health_route_ignores_a_query_token()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = _factory.CreateToken(tenant);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/health?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ok", body, StringComparison.Ordinal);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protected_sse_route_authenticates_only_through_the_authorization_header()
    {
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var token = _factory.CreateToken(tenant);

        var header = await SendAsync("/api/sales/customer-chat/events", token, queryToken: null);
        var query = await SendAsync("/api/sales/customer-chat/events", headerToken: null, queryToken: token);

        Assert.NotEqual(HttpStatusCode.Unauthorized, header.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, query.StatusCode);
    }

    [Fact]
    public async Task Family_upload_authenticates_through_the_authorization_header()
    {
        var tenant = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var token = _factory.CreateToken(tenant);

        var header = await SendAsync($"/uploads/family-os/{tenant:N}/missing.jpg", token, queryToken: null);
        var query = await SendAsync($"/uploads/family-os/{tenant:N}/missing.jpg", headerToken: null, queryToken: token);

        Assert.Equal(HttpStatusCode.NotFound, header.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, query.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string? headerToken, string? queryToken)
    {
        var url = queryToken is null
            ? path
            : $"{path}{(path.Contains('?') ? '&' : '?')}access_token={Uri.EscapeDataString(queryToken)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (headerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headerToken);

        using var client = _factory.CreateClient();
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }
}

public sealed class HeaderOnlyJwtFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddScoped<IAuthService, OfflineAuthService>();
        });
    }

    public string CreateToken(Guid tenantId)
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("permission", "sales.read"),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class OfflineAuthService : IAuthService
    {
        public Task<AuthUserDto?> GetUserAsync(Guid userId, Guid callerTenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthUserDto?>(null);

        public Task<AuthLoginResult?> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LoginResponse?> SelectWorkspaceAsync(SelectWorkspaceRequest request, string? ipAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AuthWorkspaceDto>> ListWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
