using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using KitPlatform.Api.Controllers;
using KitPlatform.Infrastructure.Data;
using Xunit;

namespace KitPlatform.Tests;

public sealed class HealthEndpointHardeningTests
{
    private const string SafeMessage =
        "Không kết nối được PostgreSQL. Kiểm tra service PostgreSQL và connection string.";

    private const string SecretText =
        "28P01 password authentication failed for user \"kit\" Host=10.1.2.3 Database=novixa_prod Password=SuperSecret";

    [Fact]
    public async Task Database_health_success_returns_ok_without_exception_detail()
    {
        var controller = new SuccessfulProbeController();

        var result = await controller.Database(
            new ThrowingDb(SecretFailure()),
            new NamedEnvironment(Environments.Production),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.GetProperty("database").GetBoolean());
        Assert.Equal(1, doc.RootElement.GetProperty("value").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain(SecretText, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Database_failure_in_development_keeps_the_exception_message_only()
    {
        var error = SecretFailure();
        var controller = new HealthController();

        var result = await controller.Database(
            new ThrowingDb(error),
            new NamedEnvironment(Environments.Development),
            CancellationToken.None);

        var body = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, body.StatusCode);
        var json = JsonSerializer.Serialize(body.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(SafeMessage, doc.RootElement.GetProperty("message").GetString());
        Assert.Equal(error.Message, doc.RootElement.GetProperty("detail").GetString());
        Assert.Contains("SuperSecret", doc.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(HealthEndpointHardeningTests), json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Database_failure_outside_development_hides_exception_details()
    {
        var error = SecretFailure();
        var controller = new HealthController();

        var result = await controller.Database(
            new ThrowingDb(error),
            new NamedEnvironment(Environments.Production),
            CancellationToken.None);

        var body = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, body.StatusCode);
        var json = JsonSerializer.Serialize(body.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(SafeMessage, doc.RootElement.GetProperty("message").GetString());
        Assert.False(doc.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain(SecretText, json, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperSecret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("10.1.2.3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("novixa_prod", json, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(HealthEndpointHardeningTests), json, StringComparison.Ordinal);
    }

    [Fact]
    public void Health_routes_stay_on_api_health_and_remain_anonymous()
    {
        var type = typeof(HealthController);
        var route = Assert.Single(type.GetCustomAttributes<RouteAttribute>());
        Assert.Equal("api/health", route.Template);
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());

        var ping = type.GetMethod(nameof(HealthController.Ping));
        Assert.NotNull(ping);
        Assert.Null(ping!.GetCustomAttribute<AuthorizeAttribute>());
        var pingGet = Assert.Single(ping.GetCustomAttributes<HttpGetAttribute>());
        Assert.True(string.IsNullOrEmpty(pingGet.Template));

        var database = type.GetMethod(nameof(HealthController.Database));
        Assert.NotNull(database);
        Assert.Null(database!.GetCustomAttribute<AuthorizeAttribute>());
        var databaseGet = Assert.Single(database.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("db", databaseGet.Template);
    }

    [Fact]
    public void Ping_stays_ok_without_database_or_exception_fields()
    {
        var result = new HealthController().Ping();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
        Assert.False(doc.RootElement.TryGetProperty("database", out _));
        Assert.False(doc.RootElement.TryGetProperty("message", out _));
        Assert.False(doc.RootElement.TryGetProperty("detail", out _));
    }

    private static Exception SecretFailure()
    {
        try
        {
            throw new InvalidOperationException(SecretText);
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private sealed class SuccessfulProbeController : HealthController
    {
        protected override Task<int> ReadDatabaseProbeAsync(
            IDbConnectionFactory db,
            CancellationToken cancellationToken) =>
            Task.FromResult(1);
    }

    private sealed class ThrowingDb(Exception error) : IDbConnectionFactory
    {
        public Task<Npgsql.NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<Npgsql.NpgsqlConnection>(error);
    }

    private sealed class NamedEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "KitPlatform.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
