using Npgsql;
using Xunit;

namespace KitPlatform.Tests;

internal static class IsolatedDatabase
{
    public const string ConnectionEnvironmentVariable = "KIT_APP_TEST_CONNECTION";

    private static readonly HashSet<string> DeniedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitplatform",
        "pharmacore",
        "postgres",
        "novixa_prod",
    };

    // These tests create and delete tenants. Port 5432 is the dev cluster.
    public static string RequireConnection()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Fail($"Set {ConnectionEnvironmentVariable} to an isolated database. No connection was opened.");

        var builder = new NpgsqlConnectionStringBuilder(connection);
        var database = builder.Database ?? "";
        if (builder.Port == 5432
            || DeniedDatabases.Contains(database)
            || database.Contains("prod", StringComparison.OrdinalIgnoreCase))
            Assert.Fail($"Refusing database '{database}' on port {builder.Port}. No connection was opened.");

        return connection!;
    }
}
