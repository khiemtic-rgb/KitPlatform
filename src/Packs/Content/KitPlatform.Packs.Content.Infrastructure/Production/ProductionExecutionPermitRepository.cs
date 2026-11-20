using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionExecutionPermitRepository : IProductionExecutionPermitStore
{
    readonly IDbConnectionFactory _db;
    readonly IProductionCallerTenant _caller;

    public ProductionExecutionPermitRepository(IDbConnectionFactory db, IProductionCallerTenant? caller = null)
    {
        _db = db;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    public async Task<ProductionExecutionPermit> IssueAsync(IssueProductionExecutionPermitCommand command, CancellationToken ct = default)
    {
        var created = ProductionExecutionPermitCanon.Create(command);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var existing = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT permit_id AS PermitId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, action AS Action,
                   provider_id AS ProviderId, model_id AS ModelId, status AS Status,
                   issued_by AS IssuedBy, issued_at AS IssuedAt, consumed_at AS ConsumedAt,
                   reason AS Reason, permit_hash AS PermitHash, tenant_id AS TenantId
            FROM pack_content.production_execution_permit
            WHERE build_id = @BuildId AND shot_code = @ShotCode AND action = @Action
              AND lower(provider_id) = lower(@ProviderId) AND lower(model_id) = lower(@ModelId)
              AND status = 'ISSUED'
            """, created, cancellationToken: ct));
        var tenant = _caller.RequireTenant();
        await ProductionAiSelectionRepository.RequireBuildAsync(conn, created.BuildId, tenant, ct);
        if (existing is not null)
        {
            ProductionBuildTenantGate.Require(tenant, existing.TenantId);
            var mapped = Map(existing);
            if (!string.Equals(mapped.PermitHash, created.PermitHash, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.PermitConflict);
            return mapped;
        }

        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_execution_permit (
                permit_id, build_id, shot_code, episode_code, scene_code, action,
                provider_id, model_id, status, issued_by, issued_at, consumed_at,
                reason, permit_hash, tenant_id)
            VALUES (
                @PermitId, @BuildId, @ShotCode, @EpisodeCode, @SceneCode, @Action,
                @ProviderId, @ModelId, @Status, @IssuedBy, @IssuedAt, @ConsumedAt,
                @Reason, @PermitHash, @TenantId)
            """, new
        {
            created.PermitId, created.BuildId, created.ShotCode, created.EpisodeCode, created.SceneCode, created.Action,
            created.ProviderId, created.ModelId, created.Status, created.IssuedBy, created.IssuedAt, created.ConsumedAt,
            created.Reason, created.PermitHash, TenantId = tenant,
        }, cancellationToken: ct));
        return created;
    }

    public async Task<ProductionExecutionPermit?> GetIssuedAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT permit_id AS PermitId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, action AS Action,
                   provider_id AS ProviderId, model_id AS ModelId, status AS Status,
                   issued_by AS IssuedBy, issued_at AS IssuedAt, consumed_at AS ConsumedAt,
                   reason AS Reason, permit_hash AS PermitHash
            FROM pack_content.production_execution_permit
            WHERE build_id = @buildId AND shot_code = @shotCode AND action = @action
              AND lower(provider_id) = lower(@providerId) AND lower(model_id) = lower(@modelId)
              AND status = 'ISSUED' AND tenant_id = @TenantId
            """, new { buildId, shotCode, action, providerId, modelId, TenantId = _caller.RequireTenant() }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<ProductionExecutionPermit?> GetLatestAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT permit_id AS PermitId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, action AS Action,
                   provider_id AS ProviderId, model_id AS ModelId, status AS Status,
                   issued_by AS IssuedBy, issued_at AS IssuedAt, consumed_at AS ConsumedAt,
                   reason AS Reason, permit_hash AS PermitHash
            FROM pack_content.production_execution_permit
            WHERE build_id = @buildId AND shot_code = @shotCode AND action = @action
              AND lower(provider_id) = lower(@providerId) AND lower(model_id) = lower(@modelId)
              AND tenant_id = @TenantId
            ORDER BY issued_at DESC
            LIMIT 1
            """, new { buildId, shotCode, action, providerId, modelId, TenantId = _caller.RequireTenant() }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<bool> TryConsumeAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE pack_content.production_execution_permit
            SET status = 'CONSUMED', consumed_at = NOW()
            WHERE build_id = @buildId AND shot_code = @shotCode AND action = @action
              AND lower(provider_id) = lower(@providerId) AND lower(model_id) = lower(@modelId)
              AND status = 'ISSUED' AND tenant_id = @TenantId
            """, new { buildId, shotCode, action, providerId, modelId, TenantId = _caller.RequireTenant() }, cancellationToken: ct));
        return n == 1;
    }

    public async Task<IReadOnlyList<ProductionExecutionPermit>> ListByTargetAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Row>(new CommandDefinition("""
            SELECT permit_id AS PermitId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, action AS Action,
                   provider_id AS ProviderId, model_id AS ModelId, status AS Status,
                   issued_by AS IssuedBy, issued_at AS IssuedAt, consumed_at AS ConsumedAt,
                   reason AS Reason, permit_hash AS PermitHash
            FROM pack_content.production_execution_permit
            WHERE build_id = @buildId AND shot_code = @shotCode AND action = @action
              AND lower(provider_id) = lower(@providerId) AND lower(model_id) = lower(@modelId)
              AND tenant_id = @TenantId
            """, new { buildId, shotCode, action, providerId, modelId, TenantId = _caller.RequireTenant() }, cancellationToken: ct));
        return rows.Select(Map).ToArray();
    }

    static ProductionExecutionPermit Map(Row row) =>
        new(row.PermitId, row.BuildId, row.ShotCode, row.EpisodeCode, row.SceneCode, row.Action,
            row.ProviderId, row.ModelId, row.Status, row.IssuedBy, row.IssuedAt, row.ConsumedAt,
            row.Reason, row.PermitHash);

    sealed class Row
    {
        public Guid PermitId { get; set; }
        public Guid BuildId { get; set; }
        public string ShotCode { get; set; } = "";
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public string Action { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string Status { get; set; } = "";
        public string IssuedBy { get; set; } = "";
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset? ConsumedAt { get; set; }
        public string Reason { get; set; } = "";
        public string PermitHash { get; set; } = "";
        public Guid? TenantId { get; set; }
    }
}
