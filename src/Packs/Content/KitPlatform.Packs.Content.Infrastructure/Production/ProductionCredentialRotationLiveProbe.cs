using System.Security.Cryptography;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Rolled-back PostgreSQL check for credential version rotation.
/// Creates its dataset inside the transaction. Does not leave a row.
/// </summary>
public static class ProductionCredentialRotationLiveProbe
{
    public static async Task<string> RunAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var beforeSnapshots = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots
            """, ct);
        var beforeCredentials = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_credentials
            """, ct);

        var tenant = await Dapper.SqlMapper.ExecuteScalarAsync<Guid?>(conn,
            "SELECT id FROM public.tenants WHERE tenant_code = 'KIT_LOCAL'");
        if (tenant is null || tenant == Guid.Empty)
            throw new InvalidOperationException(ProductionTenantBoundary.Required);

        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("probe", new Dictionary<string, byte[]> { ["probe"] = key });
        await using var tx = await conn.BeginTransactionAsync(ct);
        var caller = new ProbeCaller(tenant.Value);
        var identities = new ProductionCredentialRepository(conn, tx, caller);
        var secrets = new ProductionCredentialSecretRepository(conn, tx, ring, caller);
        var rotation = new ProductionCredentialRotationRepository(conn, tx, ring, caller);
        var resolver = new ProductionCredentialResolver(identities, secrets, caller);

        var credentialId = Guid.Parse("380e0000-0000-7000-8000-0000000000e1");
        var version1Id = Guid.Parse("380e0000-0000-7000-8000-0000000000e2");
        var version2Id = Guid.Parse("380e0000-0000-7000-8000-0000000000e3");
        const string secret1 = "gate-e-probe-v1";
        const string secret2 = "gate-e-probe-v2";
        await identities.CreateAsync(ProductionCredentialOwnership.Create(credentialId, tenant.Value, "gate-e-probe"), ct);
        var version1 = await rotation.RotateAsync(credentialId, version1Id, "runway", "gen4_turbo", secret1, ct);
        if (version1.Version != 1 || version1.TenantId != tenant.Value || version1.CredentialId != credentialId)
            throw new InvalidOperationException("ROTATION_V1_IDENTITY");
        var cipherBefore = await Cipher(conn, tx, version1Id, ct);

        var buildId = Guid.Parse("380e0000-0000-7000-8000-0000000000e0");
        await Exec(conn, tx, """
            INSERT INTO pack_content.series_build (id, series_code, title, tenant_id)
            VALUES (@buildId, 'GATE_E_PROBE', 'gate-e', @tenant)
            """, new { buildId, tenant }, ct);
        var requestId = Guid.Parse("380e0000-0000-7000-8000-0000000000e4");
        var taskId = Guid.Parse("380e0000-0000-7000-8000-0000000000e5");
        var snapshotId = Guid.Parse("380e0000-0000-7000-8000-0000000000e6");
        await Exec(conn, tx, """
            INSERT INTO pack_content.production_requests (
                request_id, build_id, shot_code, user_action, required_output_type, idempotency_key, tenant_id)
            VALUES (@requestId, @buildId, 'EP01-SC01-SH01', 'CREATE_VIDEO', 'VIDEO', @idem, @tenant)
            """, new { requestId, buildId, idem = "gate-e-" + requestId.ToString("N"), tenant }, ct);
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
                'runway', 'gen4_turbo', 'gate-e-hash', @tenant, @credentialId, @version1Id)
            """, new { snapshotId, taskId, tenant, credentialId, version1Id }, ct);

        var version2 = await rotation.RotateAsync(credentialId, version2Id, "runway", "gen4_turbo", secret2, ct);
        if (version2.Version != 2 || version2.CredentialId != credentialId || version2.CredentialVersionId == version1Id)
            throw new InvalidOperationException("ROTATION_V2_IDENTITY");
        var cipherAfter = await Cipher(conn, tx, version1Id, ct);
        if (cipherAfter.Status != ProductionCredentialStatuses.Superseded
            || cipherAfter.Version != cipherBefore.Version
            || cipherAfter.ProviderId != cipherBefore.ProviderId
            || cipherAfter.ModelId != cipherBefore.ModelId
            || !cipherBefore.Ciphertext.AsSpan().SequenceEqual(cipherAfter.Ciphertext))
            throw new InvalidOperationException("ROTATION_V1_CIPHERTEXT_CHANGED");
        if (version2.Status != ProductionCredentialStatuses.Active)
            throw new InvalidOperationException("ROTATION_V2_NOT_ACTIVE");

        var pinned = await Dapper.SqlMapper.ExecuteScalarAsync<Guid>(conn, """
            SELECT credential_version_id
            FROM pack_content.production_task_input_snapshots
            WHERE snapshot_id = @snapshotId
            """, new { snapshotId }, tx);
        if (pinned != version1Id)
            throw new InvalidOperationException("ROTATION_SNAPSHOT_REWRITTEN");

        var retry = await resolver.ResolveAsync(credentialId, pinned, "runway", "gen4_turbo", ct);
        if (retry.CredentialVersionId != version1Id || retry.Secret != secret1 || retry.ToString().Contains(secret1, StringComparison.Ordinal))
            throw new InvalidOperationException("ROTATION_RETRY_NOT_V1");
        var selected = await resolver.ResolveAsync(credentialId, version2Id, "runway", "gen4_turbo", ct);
        if (selected.CredentialVersionId != version2Id || selected.Secret != secret2)
            throw new InvalidOperationException("ROTATION_V2_UNRESOLVED");

        var outsider = new ProductionCredentialRotationRepository(
            conn, tx, ring, new ProbeCaller(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var denied = false;
        try
        {
            await outsider.RotateAsync(credentialId, Guid.NewGuid(), "runway", "gen4_turbo", "foreign", ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialOwnership.VersionCredentialRequired)
        {
            denied = true;
        }
        if (!denied)
            throw new InvalidOperationException("ROTATION_TENANT_BYPASS");

        var duplicate = false;
        try
        {
            await rotation.RotateAsync(credentialId, version2Id, "runway", "gen4_turbo", "again", ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialOwnership.VersionExists)
        {
            duplicate = true;
        }
        if (!duplicate)
            throw new InvalidOperationException("ROTATION_PARTIAL_VERSION");
        var versionCount = await Scalar(conn, tx, """
            SELECT COUNT(*) FROM pack_content.production_credential_versions
            WHERE credential_id = '380e0000-0000-7000-8000-0000000000e1'
            """, ct);
        if (versionCount != 2)
            throw new InvalidOperationException("ROTATION_PARTIAL_VERSION");

        var superseded = await resolver.ResolveAsync(credentialId, version1Id, "runway", "gen4_turbo", ct);
        if (superseded.CredentialVersionId != version1Id || superseded.Secret != secret1)
            throw new InvalidOperationException("ROTATION_SUPERSEDED_V1_UNRESOLVED");
        var revoked = await rotation.RevokeVersionAsync(version2Id, ct);
        if (revoked.Status != ProductionCredentialStatuses.Revoked || revoked.CredentialVersionId != version2Id)
            throw new InvalidOperationException("ROTATION_REVOKE_IDENTITY");
        var revokedClosed = false;
        try
        {
            await resolver.ResolveAsync(credentialId, version2Id, "runway", "gen4_turbo", ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Revoked
            && !ex.ToString().Contains(secret1, StringComparison.Ordinal)
            && !ex.ToString().Contains(secret2, StringComparison.Ordinal))
        {
            revokedClosed = true;
        }
        if (!revokedClosed)
            throw new InvalidOperationException("ROTATION_REVOKED_RETRY_OPEN");
        var stillPinned = await Dapper.SqlMapper.ExecuteScalarAsync<Guid>(conn, """
            SELECT credential_version_id
            FROM pack_content.production_task_input_snapshots
            WHERE snapshot_id = @snapshotId
            """, new { snapshotId }, tx);
        if (stillPinned != version1Id)
            throw new InvalidOperationException("ROTATION_REVOKED_SNAPSHOT_REWRITTEN");
        var revokedCipher = await Cipher(conn, tx, version1Id, ct);
        if (revokedCipher.Status != ProductionCredentialStatuses.Superseded
            || revokedCipher.Version != cipherBefore.Version
            || revokedCipher.ProviderId != cipherBefore.ProviderId
            || revokedCipher.ModelId != cipherBefore.ModelId
            || !cipherBefore.Ciphertext.AsSpan().SequenceEqual(revokedCipher.Ciphertext))
            throw new InvalidOperationException("ROTATION_REVOKED_CIPHERTEXT_CHANGED");

        await tx.RollbackAsync(ct);
        var afterSnapshots = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_task_input_snapshots
            """, ct);
        var afterCredentials = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.production_credentials
            """, ct);
        var buildLeft = await Scalar(conn, null, """
            SELECT COUNT(*) FROM pack_content.series_build WHERE id = '380e0000-0000-7000-8000-0000000000e0'
            """, ct);
        if (afterSnapshots != beforeSnapshots || afterCredentials != beforeCredentials || buildLeft != 0)
            throw new InvalidOperationException("ROTATION_PROBE_PERSISTED");
        return "PROBE_OK versions=2 snapshot_v1=1 retry_v1=1 revoked_closed=1 tenant_lock=1 persisted=0";
    }

    static async Task<CipherRow> Cipher(NpgsqlConnection conn, NpgsqlTransaction tx, Guid versionId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT version_number, status, provider_id, model_id, secret_ciphertext
            FROM pack_content.production_credential_versions
            WHERE credential_version_id = @id
            """, conn, tx);
        cmd.Parameters.AddWithValue("id", versionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("ROTATION_VERSION_MISSING");
        return new CipherRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            (byte[])reader.GetValue(4));
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

    sealed record CipherRow(int Version, string Status, string ProviderId, string ModelId, byte[] Ciphertext)
    {
        public bool Same(CipherRow other) =>
            Version == other.Version
            && Status == other.Status
            && ProviderId == other.ProviderId
            && ModelId == other.ModelId
            && Ciphertext.AsSpan().SequenceEqual(other.Ciphertext);
    }

    sealed class ProbeCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }
}
