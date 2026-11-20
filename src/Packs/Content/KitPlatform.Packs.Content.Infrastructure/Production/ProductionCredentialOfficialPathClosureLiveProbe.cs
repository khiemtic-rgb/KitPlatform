using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Read-only counts. Does not open a write transaction and does not read secret values.
/// </summary>
public static class ProductionCredentialOfficialPathClosureLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var snapshots = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_task_input_snapshots", ct);
        var nullCred = await Scalar(conn, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NULL
            """, ct);
        var pinned = await Scalar(conn, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NOT NULL
            """, ct);
        var credentials = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_credentials", ct);
        var orgRows = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.org_settings", ct);
        return "PROBE_OK snapshots=" + snapshots + " null_cred=" + nullCred + " pinned=" + pinned
            + " credentials=" + credentials + " org_rows=" + orgRows;
    }

    static async Task<int> Scalar(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var value = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(value);
    }
}
