namespace PharmaCore.Infrastructure.Data;

public interface IDbConnectionFactory
{
    Task<Npgsql.NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        DapperTypeHandlers.EnsureRegistered();
        _connectionString = connectionString;
    }

    public async Task<Npgsql.NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
