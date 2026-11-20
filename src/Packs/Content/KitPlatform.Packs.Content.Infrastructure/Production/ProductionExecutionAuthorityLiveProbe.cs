using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Read-only execution counts. Does not insert or update a production row.</summary>
public static class ProductionExecutionAuthorityLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var requests = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_requests", ct);
        var tasks = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_tasks", ct);
        var attempts = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_attempts", ct);
        var snapshots = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_task_input_snapshots", ct);
        var permits = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_execution_permit", ct);
        var artifacts = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_artifacts", ct);
        var bindings = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_bindings", ct);
        return "PROBE_OK requests=" + requests + " tasks=" + tasks + " attempts=" + attempts + " snapshots=" + snapshots
            + " permits=" + permits + " artifacts=" + artifacts + " bindings=" + bindings;
    }

    static async Task<int> Scalar(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
