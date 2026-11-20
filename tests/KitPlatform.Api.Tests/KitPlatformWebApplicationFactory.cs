using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace KitPlatform.Api.Tests;

public sealed class KitPlatformWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ConnectionEnvironmentVariable = "KIT_API_TEST_CONNECTION";

    private static readonly HashSet<string> DeniedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitplatform",
        "pharmacore",
        "postgres",
        "novixa_prod",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var connection = RequireIsolatedConnection();
        builder.UseSetting("ConnectionStrings:Default", connection);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connection,
                ["LocalOs:PublicFeedUrl"] = "",
                ["LocalOs:PublicFeedSecret"] = "",
                ["IntegrationOutbox:WebhookUrl"] = "",
            }));
        // Workers call external services (feed, catalog sync, push); none belong in a test run.
        builder.ConfigureTestServices(services =>
        {
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(hosted);
        });
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = false;
        });
    }

    // These tests write orders, receipts and stock. Without this gate they fall back to the dev database.
    internal static string RequireIsolatedConnection()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                $"Stopped before any connection. Set {ConnectionEnvironmentVariable} to an isolated seeded database.");

        var builder = new NpgsqlConnectionStringBuilder(connection);
        var database = builder.Database ?? "";
        if (builder.Port == 5432
            || DeniedDatabases.Contains(database)
            || database.Contains("prod", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Refusing database '{database}' on port {builder.Port}. No connection was opened.");

        return connection;
    }
}
