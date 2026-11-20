using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Rolled-back artifact authority check. Does not write the video directory and does not backfill history.
/// </summary>
public static class ProductionArtifactAuthorityLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var before = await Measure(conn, null, ct);
        var tenant = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn,
            "SELECT id FROM public.tenants WHERE tenant_code = 'KIT_LOCAL'");
        if (tenant is null || tenant == Guid.Empty)
            throw new InvalidOperationException("ARTIFACT_PROBE_TENANT");

        await using var tx = await conn.BeginTransactionAsync(ct);
        var buildId = Guid.Parse("381a0000-0000-7000-8000-0000000000a0");
        var requestId = Guid.Parse("381a0000-0000-7000-8000-0000000000a1");
        var taskId = Guid.Parse("381a0000-0000-7000-8000-0000000000a2");
        var attemptId = Guid.Parse("381a0000-0000-7000-8000-0000000000a3");
        var artifactId = Guid.Parse("381a0000-0000-7000-8000-0000000000a4");
        await Exec(conn, tx, """
            INSERT INTO pack_content.series_build (id, series_code, title, tenant_id)
            VALUES (@buildId, 'ART_AUTH_PROBE', 'artifact-authority', @tenant)
            """, new { buildId, tenant }, ct);
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_requests (
                request_id, build_id, shot_code, user_action, required_output_type, idempotency_key, tenant_id)
            VALUES (@requestId, @buildId, 'EP01-SC01-SH01', 'CREATE_VIDEO', 'VIDEO', @idem, @tenant)
            """, new { requestId, buildId, idem = "art-auth-" + requestId.ToString("N"), tenant }, ct);
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_tasks (
                task_id, request_id, action, target_id, build_id, shot_code, tenant_id)
            VALUES (@taskId, @requestId, 'CREATE_VIDEO', 'EP01-SC01-SH01', @buildId, 'EP01-SC01-SH01', @tenant)
            """, new { taskId, requestId, buildId, tenant }, ct);
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_attempts (
                attempt_id, task_id, attempt_number, provider_id, model_id, status, tenant_id)
            VALUES (@attemptId, @taskId, 1, 'runway', 'gen4_turbo', 'PROVIDER_SUCCEEDED', @tenant)
            """, new { attemptId, taskId, tenant }, ct);

        var prefixed = tenant.Value.ToString("N") + "/os/artauth01";
        var sha = new string('a', 64);
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_artifacts (
                artifact_id, task_id, attempt_id, role, mime_type, size_bytes, duration_sec, byte_sha256,
                storage_key, verification_status, input_snapshot_hash, parent_artifact_ids, tenant_id)
            VALUES (
                @artifactId, @taskId, @attemptId, 'VIDEO_TAKE', 'video/mp4', 32, 1, @sha,
                @prefixed, 'VERIFIED', 'snap', '[]'::jsonb, @tenant)
            """, new { artifactId, taskId, attemptId, sha, prefixed, tenant }, ct);

        var outsider = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var visible = await Scalar(conn, tx, """
            SELECT COUNT(*) FROM pack_content.production_artifacts
            WHERE artifact_id = '381a0000-0000-7000-8000-0000000000a4' AND tenant_id = @outsider
            """.Replace("@outsider", "'" + outsider + "'"), ct);
        if (visible != 0)
            throw new InvalidOperationException("ARTIFACT_CROSS_TENANT_VISIBLE");

        await Exec(conn, tx, "SAVEPOINT art_auth", null, ct);
        var locked = false;
        try
        {
            await Exec(conn, tx, """
                UPDATE pack_content.production_artifacts
                SET storage_key = storage_key || '-moved'
                WHERE artifact_id = '381a0000-0000-7000-8000-0000000000a4'
                """, null, ct);
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("PRODUCTION_ARTIFACT_LOCKED", StringComparison.Ordinal))
        {
            locked = true;
            await Exec(conn, tx, "ROLLBACK TO SAVEPOINT art_auth", null, ct);
        }
        if (!locked)
            throw new InvalidOperationException("ARTIFACT_STORAGE_KEY_MUTABLE");

        await Exec(conn, tx, "SAVEPOINT art_null", null, ct);
        var nullRejected = false;
        try
        {
            await Exec(conn, tx, """
                INSERT INTO pack_content.production_artifacts (
                    artifact_id, task_id, attempt_id, role, mime_type, size_bytes, duration_sec, byte_sha256,
                    storage_key, verification_status, input_snapshot_hash, parent_artifact_ids)
                VALUES (
                    '381a0000-0000-7000-8000-0000000000a5', @taskId, @attemptId, 'VIDEO_TAKE', 'video/mp4', 32, 1, @sha,
                    'os/unscoped', 'VERIFIED', 'snap', '[]'::jsonb)
                """, new { taskId, attemptId, sha }, ct);
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("PRODUCTION_TENANT_REQUIRED", StringComparison.Ordinal))
        {
            nullRejected = true;
            await Exec(conn, tx, "ROLLBACK TO SAVEPOINT art_null", null, ct);
        }
        if (!nullRejected)
            throw new InvalidOperationException("ARTIFACT_NULL_TENANT_ACCEPTED");

        await tx.RollbackAsync(ct);
        var after = await Measure(conn, null, ct);
        if (before != after)
            throw new InvalidOperationException("ARTIFACT_PROBE_PERSISTED");
        return "PROBE_OK artifacts=" + after.Artifacts + " null_tenant=" + after.NullTenant
            + " scoped=" + after.Scoped + " key_locked=1 null_rejected=1 persisted=0";
    }

    static async Task<ArtifactCounts> Measure(NpgsqlConnection conn, NpgsqlTransaction? tx, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT COUNT(*),
                   COUNT(*) FILTER (WHERE tenant_id IS NULL),
                   COUNT(*) FILTER (WHERE tenant_id IS NOT NULL)
            FROM pack_content.production_artifacts
            """, conn, tx);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new ArtifactCounts(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    static async Task<int> Scalar(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    static async Task Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, object? args, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        if (args is not null)
        {
            foreach (var property in args.GetType().GetProperties())
                cmd.Parameters.AddWithValue(property.Name, property.GetValue(args) ?? DBNull.Value);
        }
        await cmd.ExecuteNonQueryAsync(ct);
    }

    readonly record struct ArtifactCounts(int Artifacts, int NullTenant, int Scoped);
}
