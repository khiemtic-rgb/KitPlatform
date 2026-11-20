using KitPlatform.Infrastructure.Data;
using Dapper;
using KitPlatform.Packs.TestPack;

namespace KitPlatform.Packs.TestPack.Infrastructure;

internal sealed class SqlTestPackProbe : ITestPackProbe
{
    private readonly IDbConnectionFactory _db;

    public SqlTestPackProbe(IDbConnectionFactory db) => _db = db;

    public async Task RememberAsync(Guid tenantId, string marker, CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO pack_test.marker (tenant_id, marker)
            VALUES (@TenantId, @Marker)
            ON CONFLICT (tenant_id) DO UPDATE
            SET marker = EXCLUDED.marker
            """,
            new { TenantId = tenantId, Marker = marker },
            cancellationToken: cancellationToken));
    }

    public async Task<string?> ReadAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenTenantAsync(tenantId, cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT marker
            FROM pack_test.marker
            WHERE tenant_id = @TenantId
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    private async Task<Npgsql.NpgsqlConnection> OpenTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(
            "SELECT set_config('app.tenant_id', @Value, false)",
            new { Value = tenantId.ToString() },
            cancellationToken: cancellationToken));
        return conn;
    }
}
