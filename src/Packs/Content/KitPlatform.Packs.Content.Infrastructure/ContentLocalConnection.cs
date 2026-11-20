using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Content.Infrastructure;

public static class ContentLocalConnection
{
    public const string Variable = "CK18B_CONNECTION";

    public static string Require()
    {
        var value = Environment.GetEnvironmentVariable(Variable);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(Variable + " is required.");
        return value.Trim();
    }
}

public sealed class ContentLocalConnectionFactory : IDbConnectionFactory
{
    public async Task<Npgsql.NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new Npgsql.NpgsqlConnection(ContentLocalConnection.Require());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
