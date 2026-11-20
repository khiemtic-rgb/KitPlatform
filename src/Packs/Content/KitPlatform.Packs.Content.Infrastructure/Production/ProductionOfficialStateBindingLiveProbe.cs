using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Read-only binding and artifact counts. Does not insert or update a production row.</summary>
public static class ProductionOfficialStateBindingLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var artifacts = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_artifacts", ct);
        var nullArtifacts = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_artifacts WHERE tenant_id IS NULL", ct);
        var scopedArtifacts = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_artifacts WHERE tenant_id IS NOT NULL", ct);
        var bindings = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_bindings", ct);
        var nullBindings = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_bindings WHERE tenant_id IS NULL", ct);
        var active = await Scalar(conn, "SELECT COUNT(*) FROM pack_content.production_bindings WHERE unbound_at IS NULL", ct);
        var roles = await Text(conn, """
            SELECT COALESCE(string_agg(role || '=' || n::text, ',' ORDER BY role), '')
            FROM (
                SELECT role, COUNT(*) AS n
                FROM pack_content.production_bindings
                GROUP BY role
            ) counts
            """, ct);
        return "PROBE_OK artifacts=" + artifacts + " null_artifacts=" + nullArtifacts
            + " scoped_artifacts=" + scopedArtifacts + " bindings=" + bindings
            + " null_bindings=" + nullBindings + " active_bindings=" + active + " roles=" + roles;
    }

    static async Task<int> Scalar(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    static async Task<string> Text(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToString(await cmd.ExecuteScalarAsync(ct)) ?? "";
    }
}
