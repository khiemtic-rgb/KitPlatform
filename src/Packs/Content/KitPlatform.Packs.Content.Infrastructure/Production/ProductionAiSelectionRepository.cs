using System.Data;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionAiSelectionRepository : IProductionAiSelectionStore
{
    readonly IDbConnectionFactory _db;
    readonly IProductionCallerTenant _caller;

    public ProductionAiSelectionRepository(IDbConnectionFactory db, IProductionCallerTenant? caller = null)
    {
        _db = db;
        _caller = caller ?? ProductionMissingCallerTenant.Instance;
    }

    public async Task<ProductionAiSelection> UpsertActiveAsync(UpsertProductionAiSelectionCommand command, CancellationToken ct = default)
    {
        var created = ProductionAiSelectionCanon.Create(command);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var existing = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT selection_id AS SelectionId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, stage AS Stage,
                   provider_id AS ProviderId, model_id AS ModelId, selection_mode AS SelectionMode,
                   selected_by AS SelectedBy, selected_at AS SelectedAt, reason AS Reason,
                   status AS Status, selection_hash AS SelectionHash, tenant_id AS TenantId
            FROM pack_content.production_ai_selection
            WHERE build_id = @BuildId AND shot_code = @ShotCode AND stage = @Stage AND status = 'ACTIVE'
            """, new { created.BuildId, created.ShotCode, created.Stage }, cancellationToken: ct));
        var tenant = _caller.RequireTenant();
        await RequireBuildAsync(conn, created.BuildId, tenant, ct);
        if (existing is not null)
        {
            ProductionBuildTenantGate.Require(tenant, existing.TenantId);
            var mapped = Map(existing);
            if (!string.Equals(mapped.SelectionHash, created.SelectionHash, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
            return mapped;
        }

        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_ai_selection (
                selection_id, build_id, shot_code, episode_code, scene_code, stage,
                provider_id, model_id, selection_mode, selected_by, selected_at,
                reason, status, selection_hash, tenant_id)
            VALUES (
                @SelectionId, @BuildId, @ShotCode, @EpisodeCode, @SceneCode, @Stage,
                @ProviderId, @ModelId, @SelectionMode, @SelectedBy, @SelectedAt,
                @Reason, @Status, @SelectionHash, @TenantId)
            """, new
        {
            created.SelectionId, created.BuildId, created.ShotCode, created.EpisodeCode, created.SceneCode, created.Stage,
            created.ProviderId, created.ModelId, created.SelectionMode, created.SelectedBy, created.SelectedAt,
            created.Reason, created.Status, created.SelectionHash, TenantId = tenant,
        }, cancellationToken: ct));
        return created;
    }

    public async Task<ProductionAiSelectionSupersedeResult> SupersedeActiveAsync(
        SupersedeProductionAiSelectionCommand command, CancellationToken ct = default)
    {
        var next = ProductionAiSelectionCanon.Create(command.Next);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var tenant = _caller.RequireTenant();
        await RequireBuildAsync(conn, next.BuildId, tenant, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var existing = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT selection_id AS SelectionId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, stage AS Stage,
                   provider_id AS ProviderId, model_id AS ModelId, selection_mode AS SelectionMode,
                   selected_by AS SelectedBy, selected_at AS SelectedAt, reason AS Reason,
                   status AS Status, selection_hash AS SelectionHash, tenant_id AS TenantId
            FROM pack_content.production_ai_selection
            WHERE build_id = @BuildId AND shot_code = @ShotCode AND stage = @Stage AND status = 'ACTIVE'
            """, new { next.BuildId, next.ShotCode, next.Stage }, tx, cancellationToken: ct));
        if (existing is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);
        ProductionBuildTenantGate.Require(tenant, existing.TenantId);
        var current = Map(existing);
        if (current.SelectionId != command.ExpectedSelectionId)
            throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
        if (string.Equals(current.SelectionHash, next.SelectionHash, StringComparison.Ordinal))
        {
            await tx.CommitAsync(ct);
            return new ProductionAiSelectionSupersedeResult(current, current);
        }

        var reason = AuditReason(next.Reason, current.SelectionId);
        var superseded = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            UPDATE pack_content.production_ai_selection
            SET status = 'SUPERSEDED'
            WHERE selection_id = @SelectionId AND status = 'ACTIVE' AND tenant_id = @TenantId
            RETURNING selection_id AS SelectionId, build_id AS BuildId, shot_code AS ShotCode,
                      episode_code AS EpisodeCode, scene_code AS SceneCode, stage AS Stage,
                      provider_id AS ProviderId, model_id AS ModelId, selection_mode AS SelectionMode,
                      selected_by AS SelectedBy, selected_at AS SelectedAt, reason AS Reason,
                      status AS Status, selection_hash AS SelectionHash, tenant_id AS TenantId
            """, new { SelectionId = current.SelectionId, TenantId = tenant }, tx, cancellationToken: ct));
        if (superseded is null)
            throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
        var active = next with { Reason = reason };
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.production_ai_selection (
                selection_id, build_id, shot_code, episode_code, scene_code, stage,
                provider_id, model_id, selection_mode, selected_by, selected_at,
                reason, status, selection_hash, tenant_id)
            VALUES (
                @SelectionId, @BuildId, @ShotCode, @EpisodeCode, @SceneCode, @Stage,
                @ProviderId, @ModelId, @SelectionMode, @SelectedBy, @SelectedAt,
                @Reason, @Status, @SelectionHash, @TenantId)
            """, new
        {
            active.SelectionId, active.BuildId, active.ShotCode, active.EpisodeCode, active.SceneCode, active.Stage,
            active.ProviderId, active.ModelId, active.SelectionMode, active.SelectedBy, active.SelectedAt,
            active.Reason, active.Status, active.SelectionHash, TenantId = tenant,
        }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return new ProductionAiSelectionSupersedeResult(Map(superseded), active);
    }

    static string AuditReason(string reason, Guid previousSelectionId)
    {
        var text = (reason ?? "").Trim();
        if (text.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
        var marker = "supersedes=" + previousSelectionId.ToString("D");
        return text.Contains(marker, StringComparison.OrdinalIgnoreCase) ? text : text + "; " + marker;
    }

    public async Task<ProductionAiSelection?> GetActiveAsync(Guid buildId, string shotCode, string stage, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT selection_id AS SelectionId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, stage AS Stage,
                   provider_id AS ProviderId, model_id AS ModelId, selection_mode AS SelectionMode,
                   selected_by AS SelectedBy, selected_at AS SelectedAt, reason AS Reason,
                   status AS Status, selection_hash AS SelectionHash, tenant_id AS TenantId
            FROM pack_content.production_ai_selection
            WHERE build_id = @buildId AND shot_code = @shotCode AND stage = @stage AND status = 'ACTIVE'
              AND tenant_id = @TenantId
            """, new { buildId, shotCode, stage, TenantId = _caller.RequireTenant() }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    static ProductionAiSelection Map(Row row)
    {
        var voice = ProductionVoiceSelectionCanon.VoiceIdOf(row.Reason);
        return new(row.SelectionId, row.BuildId, row.ShotCode, row.EpisodeCode, row.SceneCode, row.Stage,
            row.ProviderId, row.ModelId, row.SelectionMode, row.SelectedBy, row.SelectedAt,
            row.Reason, row.Status, row.SelectionHash, voice.Length == 0 ? null : voice);
    }

    sealed class Row
    {
        public Guid SelectionId { get; set; }
        public Guid BuildId { get; set; }
        public string ShotCode { get; set; } = "";
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public string Stage { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string SelectionMode { get; set; } = "";
        public string SelectedBy { get; set; } = "";
        public DateTimeOffset SelectedAt { get; set; }
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "";
        public string SelectionHash { get; set; } = "";
        public Guid? TenantId { get; set; }
    }

    internal static async Task RequireBuildAsync(IDbConnection conn, Guid buildId, Guid tenant, CancellationToken ct)
    {
        var row = await conn.QuerySingleOrDefaultAsync<BuildRow>(new CommandDefinition("""
            SELECT id AS Id, tenant_id AS TenantId
            FROM pack_content.series_build
            WHERE id = @Id
            """, new { Id = buildId }, cancellationToken: ct));
        if (row is null)
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        ProductionBuildTenantGate.Require(tenant, row.TenantId);
    }

    sealed class BuildRow
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
    }
}
