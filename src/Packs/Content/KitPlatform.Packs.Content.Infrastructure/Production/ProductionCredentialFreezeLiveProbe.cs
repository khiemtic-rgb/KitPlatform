using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Rolled-back PostgreSQL check that a new snapshot can pin credential identity
/// and that the pin cannot be rewritten. Does not leave a row and does not backfill.
/// </summary>
public static class ProductionCredentialFreezeLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var beforeNull = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NULL
            """, ct);
        var beforePinned = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NOT NULL
            """, ct);
        var columns = await Scalar(conn, null, """
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = 'pack_content'
              AND table_name = 'production_task_input_snapshots'
              AND column_name IN ('credential_id', 'credential_version_id')
            """, ct);
        if (columns != 2)
            throw new InvalidOperationException("SNAPSHOT_COLUMNS_MISSING");

        var tenant = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn,
            "SELECT id FROM public.tenants WHERE tenant_code = 'KIT_LOCAL'");
        if (tenant is null || tenant == Guid.Empty)
            throw new InvalidOperationException(ProductionTenantBoundary.Required);

        await using var tx = await conn.BeginTransactionAsync(ct);
        var caller = new ProbeCaller(tenant.Value);
        var identities = new ProductionCredentialRepository(conn, tx, caller);
        var credentialId = Guid.Parse("379d0000-0000-7000-8000-0000000000d1");
        var versionId = Guid.Parse("379d0000-0000-7000-8000-0000000000d2");
        var otherVersionId = Guid.Parse("379d0000-0000-7000-8000-0000000000d3");
        await identities.CreateAsync(ProductionCredentialOwnership.Create(credentialId, tenant.Value, "gate-d-probe"), ct);
        await identities.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            versionId, credentialId, tenant.Value, 1, "runway", "gen4_turbo"), ct);
        await identities.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            otherVersionId, credentialId, tenant.Value, 2, "runway", "gen4_turbo"), ct);

        var buildId = Guid.Parse("379d0000-0000-7000-8000-0000000000d0");
        await Exec(conn, tx, """
            INSERT INTO pack_content.series_build (id, series_code, title, tenant_id)
            VALUES (@buildId, 'GATE_D_PROBE', 'gate-d', @tenant)
            """, new { buildId, tenant }, ct);

        var requestId = Guid.Parse("379d0000-0000-7000-8000-0000000000d4");
        var taskId = Guid.Parse("379d0000-0000-7000-8000-0000000000d5");
        var snapshotId = Guid.Parse("379d0000-0000-7000-8000-0000000000d6");
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_requests (
                request_id, build_id, shot_code, user_action, required_output_type, idempotency_key, tenant_id)
            VALUES (@requestId, @buildId, 'EP01-SC01-SH01', 'CREATE_VIDEO', 'VIDEO', @idem, @tenant)
            """, new { requestId, buildId, idem = "gate-d-" + requestId.ToString("N"), tenant }, ct);
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_tasks (
                task_id, request_id, action, target_id, build_id, shot_code, tenant_id)
            VALUES (@taskId, @requestId, 'CREATE_VIDEO', 'EP01-SC01-SH01', @buildId, 'EP01-SC01-SH01', @tenant)
            """, new { taskId, requestId, buildId, tenant }, ct);
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_task_input_snapshots (
                snapshot_id, task_id, story, story_hash, acting_hash, visual_hash, compiler_hash,
                provider_id, model_id, snapshot_hash, tenant_id, credential_id, credential_version_id)
            VALUES (
                @snapshotId, @taskId, 'probe', 'a', 'b', 'c', 'd',
                'runway', 'gen4_turbo', 'e', @tenant, @credentialId, @versionId)
            """, new { snapshotId, taskId, tenant, credentialId, versionId }, ct);

        await Exec(conn, tx, "SAVEPOINT gate_d_lock", null, ct);
        var locked = false;
        try
        {
            await Exec(conn, tx, """
                UPDATE pack_content.production_task_input_snapshots
                SET credential_version_id = @otherVersionId
                WHERE snapshot_id = @snapshotId
                """, new { otherVersionId, snapshotId }, ct);
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("PRODUCTION_SNAPSHOT_LOCKED", StringComparison.Ordinal))
        {
            locked = true;
            await Exec(conn, tx, "ROLLBACK TO SAVEPOINT gate_d_lock", null, ct);
        }
        if (!locked)
            throw new InvalidOperationException("SNAPSHOT_CREDENTIAL_WAS_MUTABLE");

        var historicalId = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn, """
            SELECT snapshot_id FROM pack_content.production_task_input_snapshots
            WHERE credential_id IS NULL AND snapshot_id <> @snapshotId
            LIMIT 1
            """, new { snapshotId }, tx);
        var historicalLocked = historicalId is null;
        if (historicalId is Guid existingId)
        {
            await Exec(conn, tx, "SAVEPOINT gate_d_historical", null, ct);
            try
            {
                await Exec(conn, tx, """
                    UPDATE pack_content.production_task_input_snapshots
                    SET credential_id = @credentialId, credential_version_id = @versionId
                    WHERE snapshot_id = @existingId
                    """, new { credentialId, versionId, existingId }, ct);
            }
            catch (PostgresException ex) when (ex.MessageText.Contains("PRODUCTION_SNAPSHOT_LOCKED", StringComparison.Ordinal))
            {
                historicalLocked = true;
                await Exec(conn, tx, "ROLLBACK TO SAVEPOINT gate_d_historical", null, ct);
            }
        }
        if (!historicalLocked)
            throw new InvalidOperationException("HISTORICAL_SNAPSHOT_WAS_MUTABLE");

        var outsider = new ProductionCredentialRepository(
            conn, tx, new ProbeCaller(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        if (await outsider.GetAsync(credentialId, ct) is not null)
            throw new InvalidOperationException("TENANT_OWNERSHIP_BYPASS");

        await tx.RollbackAsync(ct);
        var afterNull = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NULL
            """, ct);
        var afterPinned = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NOT NULL
            """, ct);
        if (afterNull != beforeNull || afterPinned != beforePinned)
            throw new InvalidOperationException("HISTORICAL_SNAPSHOT_CHANGED");
        var buildLeft = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.series_build WHERE id = '379d0000-0000-7000-8000-0000000000d0'
            """, ct);
        if (buildLeft != 0)
            throw new InvalidOperationException("PROBE_BUILD_REMAINED");

        var work = new ProductionWorkRepository(new NpgsqlConnectionFactory(connectionString), new ProbeCaller(tenant.Value));
        var unpinned = new byte[32];
        var unpinnedSources = OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(unpinned, "unpinned");
        var refused = false;
        try
        {
            await OfficialCreateVideoFreezeV1.FreezeAsync(work, unpinnedSources, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Unresolved)
        {
            refused = true;
        }
        if (!refused)
            throw new InvalidOperationException("UNPINNED_OFFICIAL_FREEZE_SUCCEEDED");
        var afterUnpinnedNull = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NULL
            """, ct);
        var afterUnpinnedPinned = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots WHERE credential_id IS NOT NULL
            """, ct);
        if (afterUnpinnedNull != afterNull || afterUnpinnedPinned != afterPinned)
            throw new InvalidOperationException("UNPINNED_OFFICIAL_FREEZE_PERSISTED");
        return "PROBE_OK historical_null=" + afterUnpinnedNull
            + " pinned=" + afterUnpinnedPinned
            + " lock=1 historical_lock=1 unpinned_freeze=0 tenant_lock=1";
    }

    static async Task<int> Scalar(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        var value = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(value);
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

    sealed class ProbeCaller : IProductionCallerTenant
    {
        public ProbeCaller(Guid id) => Id = id;
        public Guid Id { get; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
