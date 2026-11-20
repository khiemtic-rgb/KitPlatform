using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;
using Npgsql;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionWorkRepository : IProductionWorkStore
{
    readonly IDbConnectionFactory _db;
    readonly IProductionCallerTenant _caller;
    readonly AsyncLocal<Scope?> _scope = new();

    public ProductionWorkRepository(IDbConnectionFactory db, IProductionCallerTenant? caller = null)
    {
        _db = db;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    Guid Tenant() => _caller.RequireTenant();

    async Task RequireBuildAsync(Guid buildId, Guid tenant, CancellationToken ct)
    {
        var row = await WithAsync(async (c, tx) =>
            await c.QuerySingleOrDefaultAsync<BuildTenantRow>(new CommandDefinition("""
                SELECT id AS Id, tenant_id AS TenantId
                FROM pack_content.series_build
                WHERE id = @Id
                """, new { Id = buildId }, tx, cancellationToken: ct)), ct);
        if (row is null)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        ProductionBuildTenantGate.Require(tenant, row.TenantId);
    }

    async Task RequireRowAsync(string sql, object args, CancellationToken ct)
    {
        var n = await WithAsync(async (c, tx) =>
            await c.ExecuteScalarAsync<int>(new CommandDefinition(sql, args, tx, cancellationToken: ct)), ct);
        if (n != 1)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    sealed class BuildTenantRow
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
    }

    sealed record Scope(NpgsqlConnection Conn, NpgsqlTransaction Tx);

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        if (_scope.Value is not null) return await work(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        _scope.Value = new Scope(conn, tx);
        try
        {
            var result = await work(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            _scope.Value = null;
        }
    }

    async Task<T> WithAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction?, Task<T>> work, CancellationToken ct)
    {
        if (_scope.Value is { } s) return await work(s.Conn, s.Tx);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await work(conn, null);
    }

    async Task ExecAsync(string sql, object args, CancellationToken ct) =>
        await WithAsync(async (c, tx) =>
        {
            await c.ExecuteAsync(new CommandDefinition(sql, args, tx, cancellationToken: ct));
            return 0;
        }, ct);

    public Task<ProductionRequest?> GetRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapRequest(await c.QuerySingleOrDefaultAsync<RequestRow>(
            new CommandDefinition(RequestSelect + " WHERE request_id = @Id AND tenant_id = @TenantId",
                new { Id = requestId, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public Task<ProductionRequest?> GetRequestByIdempotencyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapRequest(await c.QuerySingleOrDefaultAsync<RequestRow>(
            new CommandDefinition(RequestSelect + " WHERE idempotency_key = @Key AND tenant_id = @TenantId",
                new { Key = idempotencyKey, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public async Task InsertRequestAsync(ProductionRequest request, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireBuildAsync(request.Scope.BuildId, tenant, ct);
        var args = new
        {
            request.RequestId,
            request.Scope.BuildId,
            request.Scope.EpisodeCode,
            request.Scope.SceneCode,
            request.Scope.ShotCode,
            request.UserAction,
            request.RequiredOutputType,
            request.CreatedAt,
            request.CreatedBy,
            request.Status,
            request.ParentRequestId,
            request.IdempotencyKey,
            Forbidden = JsonSerializer.Serialize(request.ForbiddenFallbacks),
            TenantId = tenant,
        };
        await WithAsync(async (c, tx) =>
        {
            // A 23505 aborts an open transaction. The savepoint lets the same transaction
            // reload the winning (tenant, idempotency) row. Autocommit inserts have no transaction to abort.
            var savepoint = tx is null ? null : "req_idem_" + Guid.NewGuid().ToString("N");
            if (savepoint is not null)
                await tx!.SaveAsync(savepoint, ct);
            try
            {
                await c.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO pack_content.production_requests (
                        request_id, build_id, episode_code, scene_code, shot_code, user_action, required_output_type,
                        created_at, created_by, status, parent_request_id, idempotency_key, forbidden_fallbacks, tenant_id)
                    VALUES (
                        @RequestId, @BuildId, @EpisodeCode, @SceneCode, @ShotCode, @UserAction, @RequiredOutputType,
                        @CreatedAt, @CreatedBy, @Status, @ParentRequestId, @IdempotencyKey, @Forbidden::jsonb, @TenantId);
                    """, args, tx, cancellationToken: ct));
                if (savepoint is not null)
                    await tx!.ReleaseAsync(savepoint, ct);
                return 0;
            }
            catch (PostgresException ex) when (IsTenantIdempotencyConflict(ex))
            {
                if (savepoint is not null)
                    await tx!.RollbackAsync(savepoint, ct);
                throw new InvalidOperationException("IDEMPOTENCY_CONFLICT");
            }
        }, ct);
    }

    static bool IsTenantIdempotencyConflict(PostgresException ex) =>
        ex.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(ex.ConstraintName, "uq_prod_request_tenant_idempotency", StringComparison.Ordinal);

    public async Task UpdateRequestAsync(ProductionRequest request, CancellationToken ct = default)
    {
        var tenant = Tenant();
        var n = await WithAsync(async (c, tx) => await c.ExecuteAsync(new CommandDefinition("""
            UPDATE pack_content.production_requests
            SET status = @Status
            WHERE request_id = @RequestId AND tenant_id = @TenantId;
            """, new { request.RequestId, request.Status, TenantId = tenant }, tx, cancellationToken: ct)), ct);
        if (n != 1)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    public Task<ProductionTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapTask(await c.QuerySingleOrDefaultAsync<TaskRow>(
            new CommandDefinition(TaskSelect + " WHERE task_id = @Id AND tenant_id = @TenantId",
                new { Id = taskId, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public Task<IReadOnlyList<ProductionTask>> ListTasksByRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<TaskRow>(new CommandDefinition(
                TaskSelect + " WHERE request_id = @Id AND tenant_id = @TenantId",
                new { Id = requestId, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<ProductionTask>)rows.Select(r => MapTask(r)!).ToArray();
        }, ct);
    }

    public Task<IReadOnlyList<ProductionTask>> ListTasksByScopeAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<TaskRow>(new CommandDefinition(
                TaskSelect + " WHERE build_id = @BuildId AND shot_code = @ShotCode AND tenant_id = @TenantId",
                new { BuildId = buildId, ShotCode = shotCode, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<ProductionTask>)rows.Select(r => MapTask(r)!).ToArray();
        }, ct);
    }

    public async Task InsertTaskAsync(ProductionTask task, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireBuildAsync(task.Scope.BuildId, tenant, ct);
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_requests WHERE request_id = @Id AND tenant_id = @TenantId AND build_id = @BuildId",
            new { Id = task.RequestId, TenantId = tenant, BuildId = task.Scope.BuildId }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_tasks (
                task_id, request_id, parent_task_id, action, target_type, target_id,
                build_id, episode_code, scene_code, shot_code, input_snapshot_id, provider_id, model_id,
                status, created_at, started_at, completed_at, failure_code, failure_reason, tenant_id)
            VALUES (
                @TaskId, @RequestId, @ParentTaskId, @Action, @TargetType, @TargetId,
                @BuildId, @EpisodeCode, @SceneCode, @ShotCode, @InputSnapshotId, @ProviderId, @ModelId,
                @Status, @CreatedAt, @StartedAt, @CompletedAt, @FailureCode, @FailureReason, @TenantId);
            """, TaskArgs(task, tenant), ct);
    }

    public async Task UpdateTaskAsync(ProductionTask task, CancellationToken ct = default)
    {
        var tenant = Tenant();
        var n = await WithAsync(async (c, tx) => await c.ExecuteAsync(new CommandDefinition("""
            UPDATE pack_content.production_tasks
            SET input_snapshot_id = @InputSnapshotId, provider_id = @ProviderId, model_id = @ModelId,
                status = @Status, started_at = @StartedAt, completed_at = @CompletedAt,
                failure_code = @FailureCode, failure_reason = @FailureReason
            WHERE task_id = @TaskId AND tenant_id = @TenantId;
            """, TaskArgs(task, tenant), tx, cancellationToken: ct)), ct);
        if (n != 1)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    public Task LockTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var id = await c.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT task_id FROM pack_content.production_tasks WHERE task_id = @Id AND tenant_id = @TenantId FOR UPDATE",
                new { Id = taskId, TenantId = tenant }, tx, cancellationToken: ct));
            if (id is null)
                throw new InvalidOperationException("TASK_MISSING");
            return 0;
        }, ct);
    }

    public Task<ProductionTaskInputSnapshot?> GetSnapshotAsync(Guid snapshotId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapSnap(await c.QuerySingleOrDefaultAsync<SnapRow>(
            new CommandDefinition(SnapSelect + " WHERE snapshot_id = @Id AND tenant_id = @TenantId",
                new { Id = snapshotId, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public Task<ProductionTaskInputSnapshot?> GetSnapshotByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapSnap(await c.QuerySingleOrDefaultAsync<SnapRow>(
            new CommandDefinition(SnapSelect + " WHERE task_id = @Id AND tenant_id = @TenantId",
                new { Id = taskId, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public async Task InsertSnapshotAsync(ProductionTaskInputSnapshot s, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_tasks WHERE task_id = @Id AND tenant_id = @TenantId",
            new { Id = s.TaskId, TenantId = tenant }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_task_input_snapshots (
                snapshot_id, task_id, story, acting_beat, visual_contract, shot_contract, prompt, compiler_output,
                story_hash, acting_hash, visual_hash, compiler_hash, keyframe_hash, voice_hash, motion_hash,
                lipsync_source_hash, timing_hash, provider_id, model_id, snapshot_hash, frozen_at,
                keyframe_artifact_id, aspect_ratio, duration_sec, compiler_version, parent_artifact_ids,
                performance_snapshot_id, performance_hash, performance_payload_json, tenant_id,
                credential_id, credential_version_id)
            VALUES (
                @SnapshotId, @TaskId, @Story, @ActingBeat, @VisualContract, @ShotContract, @Prompt, @CompilerOutput,
                @StoryHash, @ActingHash, @VisualHash, @CompilerHash, @KeyframeHash, @VoiceHash, @MotionHash,
                @LipsyncSourceHash, @TimingHash, @ProviderId, @ModelId, @SnapshotHash, @FrozenAt,
                @KeyframeArtifactId, @AspectRatio, @DurationSec, @CompilerVersion, @Parents::jsonb,
                @PerformanceSnapshotId, @PerformanceHash, @PerformancePayloadJson, @TenantId,
                @CredentialId, @CredentialVersionId);
            """, new
        {
            s.SnapshotId, s.TaskId, s.Story, s.ActingBeat, s.VisualContract, s.ShotContract, s.Prompt, s.CompilerOutput,
            s.StoryHash, s.ActingHash, s.VisualHash, s.CompilerHash, s.KeyframeHash, s.VoiceHash, s.MotionHash,
            s.LipsyncSourceHash, s.TimingHash, s.ProviderId, s.ModelId, s.SnapshotHash, s.FrozenAt,
            s.KeyframeArtifactId, s.AspectRatio, s.DurationSec, s.CompilerVersion,
            Parents = JsonSerializer.Serialize(s.ParentArtifactIds ?? []),
            s.PerformanceSnapshotId, s.PerformanceHash, s.PerformancePayloadJson,
            TenantId = tenant,
            s.CredentialId, s.CredentialVersionId,
        }, ct);
    }

    public Task<ProductionAttempt?> GetAttemptAsync(Guid attemptId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapAttempt(await c.QuerySingleOrDefaultAsync<AttemptRow>(
            new CommandDefinition(AttemptSelect + " WHERE attempt_id = @Id AND tenant_id = @TenantId",
                new { Id = attemptId, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public Task<IReadOnlyList<ProductionAttempt>> ListAttemptsAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<AttemptRow>(new CommandDefinition(
                AttemptSelect + " WHERE task_id = @Id AND tenant_id = @TenantId ORDER BY attempt_number",
                new { Id = taskId, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<ProductionAttempt>)rows.Select(r => MapAttempt(r)!).ToArray();
        }, ct);
    }

    public async Task InsertAttemptAsync(ProductionAttempt a, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_tasks WHERE task_id = @Id AND tenant_id = @TenantId",
            new { Id = a.TaskId, TenantId = tenant }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_attempts (
                attempt_id, task_id, attempt_number, provider_id, model_id, provider_job_id, status,
                started_at, completed_at, request_payload_hash, response_payload_hash, error_code, tenant_id)
            VALUES (
                @AttemptId, @TaskId, @AttemptNumber, @ProviderId, @ModelId, @ProviderJobId, @Status,
                @StartedAt, @CompletedAt, @RequestPayloadHash, @ResponsePayloadHash, @ErrorCode, @TenantId);
            """, new
        {
            a.AttemptId, a.TaskId, a.AttemptNumber, a.ProviderId, a.ModelId, a.ProviderJobId, a.Status,
            a.StartedAt, a.CompletedAt, a.RequestPayloadHash, a.ResponsePayloadHash, a.ErrorCode,
            TenantId = tenant,
        }, ct);
    }

    public async Task UpdateAttemptAsync(ProductionAttempt a, CancellationToken ct = default)
    {
        var tenant = Tenant();
        var n = await WithAsync(async (c, tx) => await c.ExecuteAsync(new CommandDefinition("""
            UPDATE pack_content.production_attempts
            SET provider_job_id = @ProviderJobId, status = @Status, completed_at = @CompletedAt,
                response_payload_hash = @ResponsePayloadHash, error_code = @ErrorCode
            WHERE attempt_id = @AttemptId AND tenant_id = @TenantId;
            """, new
        {
            a.AttemptId, a.ProviderJobId, a.Status, a.CompletedAt, a.ResponsePayloadHash, a.ErrorCode,
            TenantId = tenant,
        }, tx, cancellationToken: ct)), ct);
        if (n != 1)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    public Task<ProductionArtifact?> GetArtifactAsync(Guid artifactId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapArt(await c.QuerySingleOrDefaultAsync<ArtRow>(
            new CommandDefinition(ArtSelect + " WHERE artifact_id = @Id AND tenant_id = @TenantId",
                new { Id = artifactId, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public Task<IReadOnlyList<ProductionArtifact>> ListArtifactsByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<ArtRow>(new CommandDefinition(
                ArtSelect + " WHERE task_id = @Id AND tenant_id = @TenantId",
                new { Id = taskId, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<ProductionArtifact>)rows.Select(r => MapArt(r)!).ToArray();
        }, ct);
    }

    public async Task InsertArtifactAsync(ProductionArtifact a, CancellationToken ct = default)
    {
        var tenant = Tenant();
        if (!ProductionTenantBoundary.OwnsStorageKey(tenant, a.StorageKey))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_tasks WHERE task_id = @Id AND tenant_id = @TenantId",
            new { Id = a.TaskId, TenantId = tenant }, ct);
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_attempts WHERE attempt_id = @Id AND tenant_id = @TenantId AND task_id = @TaskId",
            new { Id = a.AttemptId, TaskId = a.TaskId, TenantId = tenant }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_artifacts (
                artifact_id, task_id, attempt_id, role, mime_type, size_bytes, duration_sec, byte_sha256,
                storage_key, provider_job_id, created_at, verification_status, input_snapshot_hash, parent_artifact_ids, tenant_id)
            VALUES (
                @ArtifactId, @TaskId, @AttemptId, @Role, @MimeType, @SizeBytes, @DurationSec, @ByteSha256,
                @StorageKey, @ProviderJobId, @CreatedAt, @VerificationStatus, @InputSnapshotHash, @Parents::jsonb, @TenantId);
            """, new
        {
            a.ArtifactId, a.TaskId, a.AttemptId, a.Role, a.MimeType, a.SizeBytes, a.DurationSec, a.ByteSha256,
            a.StorageKey, a.ProviderJobId, a.CreatedAt, a.VerificationStatus, a.InputSnapshotHash,
            Parents = JsonSerializer.Serialize(a.ParentArtifactIds ?? []),
            TenantId = tenant,
        }, ct);
    }

    public Task<ProductionBinding?> GetActiveBindingAsync(Guid buildId, string shotCode, string role, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) => MapBind(await c.QuerySingleOrDefaultAsync<BindRow>(
            new CommandDefinition(
                BindSelect + " WHERE build_id = @BuildId AND shot_code = @ShotCode AND role = @Role AND unbound_at IS NULL AND tenant_id = @TenantId",
                new { BuildId = buildId, ShotCode = shotCode, Role = role, TenantId = tenant }, tx, cancellationToken: ct))), ct);
    }

    public Task<IReadOnlyList<ProductionBinding>> ListActiveBindingsAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<BindRow>(new CommandDefinition(
                BindSelect + " WHERE build_id = @BuildId AND shot_code = @ShotCode AND unbound_at IS NULL AND tenant_id = @TenantId",
                new { BuildId = buildId, ShotCode = shotCode, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<ProductionBinding>)rows.Select(r => MapBind(r)!).ToArray();
        }, ct);
    }

    public async Task InsertBindingAsync(ProductionBinding b, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireBuildAsync(b.BuildId, tenant, ct);
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_artifacts WHERE artifact_id = @Id AND tenant_id = @TenantId",
            new { Id = b.ArtifactId, TenantId = tenant }, ct);
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_tasks WHERE task_id = @Id AND tenant_id = @TenantId",
            new { Id = b.BoundByTaskId, TenantId = tenant }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_bindings (
                binding_id, build_id, shot_code, role, artifact_id, bound_by_task_id, bound_at, unbound_at, tenant_id)
            VALUES (@BindingId, @BuildId, @ShotCode, @Role, @ArtifactId, @BoundByTaskId, @BoundAt, @UnboundAt, @TenantId);
            """, new
        {
            b.BindingId, b.BuildId, b.ShotCode, b.Role, b.ArtifactId, b.BoundByTaskId, b.BoundAt, b.UnboundAt,
            TenantId = tenant,
        }, ct);
    }

    public async Task UnbindAsync(Guid bindingId, DateTimeOffset unboundAt, CancellationToken ct = default)
    {
        var tenant = Tenant();
        var n = await WithAsync(async (c, tx) => await c.ExecuteAsync(new CommandDefinition("""
            UPDATE pack_content.production_bindings
            SET unbound_at = @UnboundAt
            WHERE binding_id = @BindingId AND tenant_id = @TenantId;
            """, new { BindingId = bindingId, UnboundAt = unboundAt, TenantId = tenant }, tx, cancellationToken: ct)), ct);
        if (n != 1)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
    }

    public async Task InsertEventAsync(ProductionTaskEvent ev, CancellationToken ct = default)
    {
        var tenant = Tenant();
        if (ev.TaskId is Guid taskId)
            await RequireRowAsync(
                "SELECT 1 FROM pack_content.production_tasks WHERE task_id = @Id AND tenant_id = @TenantId",
                new { Id = taskId, TenantId = tenant }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_task_events (
                event_id, event_type, created_at, request_id, task_id, attempt_id, artifact_id, shot_code, action, payload_json, tenant_id)
            VALUES (
                @EventId, @EventType, @Timestamp, @RequestId, @TaskId, @AttemptId, @ArtifactId, @ShotCode, @Action, @Payload::jsonb, @TenantId);
            """, new
        {
            ev.EventId, ev.EventType, ev.Timestamp, ev.RequestId, ev.TaskId, ev.AttemptId, ev.ArtifactId,
            ev.ShotCode, ev.Action, Payload = ev.PayloadJson ?? "{}", TenantId = tenant,
        }, ct);
    }

    public Task<IReadOnlyList<ProductionTaskEvent>> ListEventsByTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<EventRow>(new CommandDefinition(
                EventSelect + " WHERE task_id = @Id AND tenant_id = @TenantId ORDER BY created_at",
                new { Id = taskId, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<ProductionTaskEvent>)rows.Select(r => new ProductionTaskEvent(
                r.EventId, r.EventType, r.CreatedAt, r.RequestId, r.TaskId, r.AttemptId, r.ArtifactId,
                r.ShotCode, r.Action, r.PayloadJson)).ToArray();
        }, ct);
    }

    public async Task InsertAssemblyAsync(FilmAssembly assembly, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireBuildAsync(assembly.BuildId, tenant, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_film_assemblies (assembly_id, build_id, title, created_at, tenant_id)
            VALUES (@AssemblyId, @BuildId, @Title, @CreatedAt, @TenantId);
            """, new
        {
            assembly.AssemblyId, assembly.BuildId, assembly.Title, assembly.CreatedAt, TenantId = tenant,
        }, ct);
    }

    public async Task InsertAssemblyShotAsync(FilmAssemblyShot shot, CancellationToken ct = default)
    {
        var tenant = Tenant();
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_film_assemblies WHERE assembly_id = @Id AND tenant_id = @TenantId",
            new { Id = shot.AssemblyId, TenantId = tenant }, ct);
        await RequireRowAsync(
            "SELECT 1 FROM pack_content.production_artifacts WHERE artifact_id = @Id AND tenant_id = @TenantId",
            new { Id = shot.ArtifactId, TenantId = tenant }, ct);
        await ExecAsync("""
            INSERT INTO pack_content.production_film_assembly_shots (
                assembly_shot_id, assembly_id, build_id, shot_code, artifact_id, sort_order, duration_sec, transition, audio_state, tenant_id)
            VALUES (
                @AssemblyShotId, @AssemblyId, @ShotBuildId, @ShotCode, @ArtifactId, @Order, @DurationSec, @Transition, @AudioState, @TenantId);
            """, new
        {
            shot.AssemblyShotId, shot.AssemblyId, shot.ShotBuildId, shot.ShotCode, shot.ArtifactId,
            shot.Order, shot.DurationSec, shot.Transition, shot.AudioState, TenantId = tenant,
        }, ct);
    }

    public Task<IReadOnlyList<FilmAssemblyShot>> ListAssemblyShotsAsync(Guid assemblyId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<AsmRow>(new CommandDefinition("""
                SELECT assembly_shot_id AS AssemblyShotId, assembly_id AS AssemblyId, build_id AS ShotBuildId,
                       shot_code AS ShotCode, artifact_id AS ArtifactId, sort_order AS SortOrder,
                       duration_sec AS DurationSec, transition AS Transition, audio_state AS AudioState
                FROM pack_content.production_film_assembly_shots
                WHERE assembly_id = @Id AND tenant_id = @TenantId
                ORDER BY sort_order
                """, new { Id = assemblyId, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<FilmAssemblyShot>)rows.Select(r => new FilmAssemblyShot(
                r.AssemblyShotId, r.AssemblyId, r.ShotBuildId, r.ShotCode, r.ArtifactId,
                r.SortOrder, r.DurationSec, r.Transition, r.AudioState)).ToArray();
        }, ct);
    }

    public Task<IReadOnlyList<FilmAssembly>> ListAssembliesByBuildAsync(Guid buildId, CancellationToken ct = default)
    {
        var tenant = Tenant();
        return WithAsync(async (c, tx) =>
        {
            var rows = await c.QueryAsync<AssemblyRow>(new CommandDefinition("""
                SELECT assembly_id AS AssemblyId, build_id AS BuildId, title AS Title, created_at AS CreatedAt
                FROM pack_content.production_film_assemblies
                WHERE build_id = @BuildId AND tenant_id = @TenantId
                ORDER BY created_at
                """, new { BuildId = buildId, TenantId = tenant }, tx, cancellationToken: ct));
            return (IReadOnlyList<FilmAssembly>)rows.Select(r => new FilmAssembly(
                r.AssemblyId, r.BuildId, r.Title, r.CreatedAt)).ToArray();
        }, ct);
    }

    const string RequestSelect = """
        SELECT request_id AS RequestId, build_id AS BuildId, episode_code AS EpisodeCode, scene_code AS SceneCode,
               shot_code AS ShotCode, user_action AS UserAction, required_output_type AS RequiredOutputType,
               created_at AS CreatedAt, created_by AS CreatedBy, status AS Status, parent_request_id AS ParentRequestId,
               idempotency_key AS IdempotencyKey, forbidden_fallbacks::text AS ForbiddenJson
        FROM pack_content.production_requests
        """;

    const string TaskSelect = """
        SELECT task_id AS TaskId, request_id AS RequestId, parent_task_id AS ParentTaskId, action AS Action,
               target_type AS TargetType, target_id AS TargetId, build_id AS BuildId, episode_code AS EpisodeCode,
               scene_code AS SceneCode, shot_code AS ShotCode, input_snapshot_id AS InputSnapshotId,
               provider_id AS ProviderId, model_id AS ModelId, status AS Status, created_at AS CreatedAt,
               started_at AS StartedAt, completed_at AS CompletedAt, failure_code AS FailureCode, failure_reason AS FailureReason
        FROM pack_content.production_tasks
        """;

    const string SnapSelect = """
        SELECT snapshot_id AS SnapshotId, task_id AS TaskId, story AS Story, acting_beat AS ActingBeat,
               visual_contract AS VisualContract, shot_contract AS ShotContract, prompt AS Prompt,
               compiler_output AS CompilerOutput, story_hash AS StoryHash, acting_hash AS ActingHash,
               visual_hash AS VisualHash, compiler_hash AS CompilerHash, keyframe_hash AS KeyframeHash,
               voice_hash AS VoiceHash, motion_hash AS MotionHash, lipsync_source_hash AS LipsyncSourceHash,
               timing_hash AS TimingHash, provider_id AS ProviderId, model_id AS ModelId,
               snapshot_hash AS SnapshotHash, frozen_at AS FrozenAt,
               keyframe_artifact_id AS KeyframeArtifactId, aspect_ratio AS AspectRatio,
               duration_sec AS DurationSec, compiler_version AS CompilerVersion,
               parent_artifact_ids::text AS ParentsJson,
               performance_snapshot_id AS PerformanceSnapshotId, performance_hash AS PerformanceHash,
               performance_payload_json AS PerformancePayloadJson,
               credential_id AS CredentialId, credential_version_id AS CredentialVersionId
        FROM pack_content.production_task_input_snapshots
        """;

    const string AttemptSelect = """
        SELECT attempt_id AS AttemptId, task_id AS TaskId, attempt_number AS AttemptNumber, provider_id AS ProviderId,
               model_id AS ModelId, provider_job_id AS ProviderJobId, status AS Status, started_at AS StartedAt,
               completed_at AS CompletedAt, request_payload_hash AS RequestPayloadHash,
               response_payload_hash AS ResponsePayloadHash, error_code AS ErrorCode
        FROM pack_content.production_attempts
        """;

    const string ArtSelect = """
        SELECT artifact_id AS ArtifactId, task_id AS TaskId, attempt_id AS AttemptId, role AS Role, mime_type AS MimeType,
               size_bytes AS SizeBytes, duration_sec AS DurationSec, byte_sha256 AS ByteSha256, storage_key AS StorageKey,
               provider_job_id AS ProviderJobId, created_at AS CreatedAt, verification_status AS VerificationStatus,
               input_snapshot_hash AS InputSnapshotHash, parent_artifact_ids::text AS ParentsJson
        FROM pack_content.production_artifacts
        """;

    const string BindSelect = """
        SELECT binding_id AS BindingId, build_id AS BuildId, shot_code AS ShotCode, role AS Role, artifact_id AS ArtifactId,
               bound_by_task_id AS BoundByTaskId, bound_at AS BoundAt, unbound_at AS UnboundAt
        FROM pack_content.production_bindings
        """;

    const string EventSelect = """
        SELECT event_id AS EventId, event_type AS EventType, created_at AS CreatedAt, request_id AS RequestId,
               task_id AS TaskId, attempt_id AS AttemptId, artifact_id AS ArtifactId, shot_code AS ShotCode,
               action AS Action, payload_json::text AS PayloadJson
        FROM pack_content.production_task_events
        """;

    static object TaskArgs(ProductionTask t, Guid tenant) => new
    {
        t.TaskId, t.RequestId, t.ParentTaskId, t.Action, t.TargetType, t.TargetId,
        t.Scope.BuildId, t.Scope.EpisodeCode, t.Scope.SceneCode, t.Scope.ShotCode,
        t.InputSnapshotId, t.ProviderId, t.ModelId, t.Status, t.CreatedAt, t.StartedAt,
        t.CompletedAt, t.FailureCode, t.FailureReason, TenantId = tenant,
    };

    static ProductionRequest? MapRequest(RequestRow? r) => r is null ? null : new(
        r.RequestId, new ProductionScope(r.BuildId, r.EpisodeCode, r.SceneCode, r.ShotCode),
        r.UserAction, r.RequiredOutputType, r.CreatedAt, r.CreatedBy, r.Status, r.ParentRequestId,
        r.IdempotencyKey, ParseList(r.ForbiddenJson));

    static ProductionTask? MapTask(TaskRow? r) => r is null ? null : new(
        r.TaskId, r.RequestId, r.ParentTaskId, r.Action, r.TargetType, r.TargetId,
        new ProductionScope(r.BuildId, r.EpisodeCode, r.SceneCode, r.ShotCode),
        r.InputSnapshotId, r.ProviderId, r.ModelId, r.Status, r.CreatedAt, r.StartedAt, r.CompletedAt,
        r.FailureCode, r.FailureReason);

    static ProductionTaskInputSnapshot? MapSnap(SnapRow? r) => r is null ? null : new(
        r.SnapshotId, r.TaskId, r.Story, r.ActingBeat, r.VisualContract, r.ShotContract, r.Prompt, r.CompilerOutput,
        r.StoryHash, r.ActingHash, r.VisualHash, r.CompilerHash, r.KeyframeHash, r.VoiceHash, r.MotionHash,
        r.LipsyncSourceHash, r.TimingHash, r.ProviderId, r.ModelId, r.SnapshotHash, r.FrozenAt,
        r.KeyframeArtifactId, r.AspectRatio, r.DurationSec, r.CompilerVersion, ParseGuids(r.ParentsJson),
        r.PerformanceSnapshotId, r.PerformanceHash, r.PerformancePayloadJson,
        r.CredentialId, r.CredentialVersionId);

    static ProductionAttempt? MapAttempt(AttemptRow? r) => r is null ? null : new(
        r.AttemptId, r.TaskId, r.AttemptNumber, r.ProviderId, r.ModelId, r.ProviderJobId, r.Status,
        r.StartedAt, r.CompletedAt, r.RequestPayloadHash, r.ResponsePayloadHash, r.ErrorCode);

    static ProductionArtifact? MapArt(ArtRow? r) => r is null ? null : new(
        r.ArtifactId, r.TaskId, r.AttemptId, r.Role, r.MimeType, r.SizeBytes, r.DurationSec, r.ByteSha256,
        r.StorageKey, r.ProviderJobId, r.CreatedAt, r.VerificationStatus, r.InputSnapshotHash, ParseGuids(r.ParentsJson));

    static ProductionBinding? MapBind(BindRow? r) => r is null ? null : new(
        r.BindingId, r.BuildId, r.ShotCode, r.Role, r.ArtifactId, r.BoundByTaskId, r.BoundAt, r.UnboundAt);

    static IReadOnlyList<Guid> ParseGuids(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}" or "[]") return [];
        try { return JsonSerializer.Deserialize<Guid[]>(json) ?? []; }
        catch { return []; }
    }

    static IReadOnlyList<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }

    sealed class RequestRow
    {
        public Guid RequestId { get; set; }
        public Guid BuildId { get; set; }
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public string ShotCode { get; set; } = "";
        public string UserAction { get; set; } = "";
        public string RequiredOutputType { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public string Status { get; set; } = "";
        public Guid? ParentRequestId { get; set; }
        public string IdempotencyKey { get; set; } = "";
        public string? ForbiddenJson { get; set; }
    }

    sealed class TaskRow
    {
        public Guid TaskId { get; set; }
        public Guid RequestId { get; set; }
        public Guid? ParentTaskId { get; set; }
        public string Action { get; set; } = "";
        public string TargetType { get; set; } = "";
        public string TargetId { get; set; } = "";
        public Guid BuildId { get; set; }
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public string ShotCode { get; set; } = "";
        public Guid? InputSnapshotId { get; set; }
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string Status { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? FailureCode { get; set; }
        public string? FailureReason { get; set; }
    }

    sealed class SnapRow
    {
        public Guid SnapshotId { get; set; }
        public Guid TaskId { get; set; }
        public string Story { get; set; } = "";
        public string ActingBeat { get; set; } = "";
        public string VisualContract { get; set; } = "";
        public string ShotContract { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string CompilerOutput { get; set; } = "";
        public string StoryHash { get; set; } = "";
        public string ActingHash { get; set; } = "";
        public string VisualHash { get; set; } = "";
        public string CompilerHash { get; set; } = "";
        public string KeyframeHash { get; set; } = "";
        public string VoiceHash { get; set; } = "";
        public string MotionHash { get; set; } = "";
        public string LipsyncSourceHash { get; set; } = "";
        public string TimingHash { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string SnapshotHash { get; set; } = "";
        public DateTimeOffset FrozenAt { get; set; }
        public Guid? KeyframeArtifactId { get; set; }
        public string AspectRatio { get; set; } = "16:9";
        public decimal DurationSec { get; set; }
        public string CompilerVersion { get; set; } = ShotExecutionCompiler.Version;
        public string? ParentsJson { get; set; }
        public Guid? PerformanceSnapshotId { get; set; }
        public string? PerformanceHash { get; set; }
        public string? PerformancePayloadJson { get; set; }
        public Guid? CredentialId { get; set; }
        public Guid? CredentialVersionId { get; set; }
    }

    sealed class AttemptRow
    {
        public Guid AttemptId { get; set; }
        public Guid TaskId { get; set; }
        public int AttemptNumber { get; set; }
        public string ProviderId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string? ProviderJobId { get; set; }
        public string Status { get; set; } = "";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string RequestPayloadHash { get; set; } = "";
        public string? ResponsePayloadHash { get; set; }
        public string? ErrorCode { get; set; }
    }

    sealed class ArtRow
    {
        public Guid ArtifactId { get; set; }
        public Guid TaskId { get; set; }
        public Guid AttemptId { get; set; }
        public string Role { get; set; } = "";
        public string MimeType { get; set; } = "";
        public long SizeBytes { get; set; }
        public decimal DurationSec { get; set; }
        public string ByteSha256 { get; set; } = "";
        public string StorageKey { get; set; } = "";
        public string? ProviderJobId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string VerificationStatus { get; set; } = "";
        public string InputSnapshotHash { get; set; } = "";
        public string? ParentsJson { get; set; }
    }

    sealed class BindRow
    {
        public Guid BindingId { get; set; }
        public Guid BuildId { get; set; }
        public string ShotCode { get; set; } = "";
        public string Role { get; set; } = "";
        public Guid ArtifactId { get; set; }
        public Guid BoundByTaskId { get; set; }
        public DateTimeOffset BoundAt { get; set; }
        public DateTimeOffset? UnboundAt { get; set; }
    }

    sealed class EventRow
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? RequestId { get; set; }
        public Guid? TaskId { get; set; }
        public Guid? AttemptId { get; set; }
        public Guid? ArtifactId { get; set; }
        public string ShotCode { get; set; } = "";
        public string Action { get; set; } = "";
        public string? PayloadJson { get; set; }
    }

    sealed class AssemblyRow
    {
        public Guid AssemblyId { get; set; }
        public Guid BuildId { get; set; }
        public string Title { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    sealed class AsmRow
    {
        public Guid AssemblyShotId { get; set; }
        public Guid AssemblyId { get; set; }
        public Guid ShotBuildId { get; set; }
        public string ShotCode { get; set; } = "";
        public Guid ArtifactId { get; set; }
        public int SortOrder { get; set; }
        public decimal DurationSec { get; set; }
        public string Transition { get; set; } = "";
        public string AudioState { get; set; } = "";
    }
}
