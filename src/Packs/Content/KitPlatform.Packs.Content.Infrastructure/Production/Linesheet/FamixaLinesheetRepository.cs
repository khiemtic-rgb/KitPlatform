using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Kho Dapper của M1 (migration 430). Mọi câu đọc/ghi có <c>tenant_id = @Tenant</c>, kể cả câu của worker:
/// worker nhận lần dựng không lọc tenant (giống <c>work_job</c>), rồi mọi câu sau đó dùng tenant của bản ghi đã nhận.
/// </summary>
internal sealed class FamixaLinesheetRepository : IFamixaLinesheetStore
{
    readonly IDbConnectionFactory _db;

    public FamixaLinesheetRepository(IDbConnectionFactory db) => _db = db;

    // ---------- sản xuất ----------

    internal const string ProductionSelect = """
        SELECT id AS Id, tenant_id AS TenantId, universe_code AS UniverseCode, episode_code AS EpisodeCode,
               title AS Title, CAST(settings_json AS text) AS SettingsJson, created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM pack_content.linesheet_production
        """;

    internal const string CreateProductionSql = """
        INSERT INTO pack_content.linesheet_production AS p (id, tenant_id, universe_code, episode_code, title)
        VALUES (@Id, @Tenant, @UniverseCode, @EpisodeCode, @Title)
        ON CONFLICT (tenant_id, universe_code, episode_code) DO UPDATE
            SET title = CASE WHEN EXCLUDED.title <> '' THEN EXCLUDED.title ELSE p.title END,
                updated_at = NOW()
        RETURNING p.id
        """;

    public async Task<LinesheetProductionRecord> CreateProductionAsync(Guid tenant, string universeCode, string episodeCode, string title, CancellationToken ct)
    {
        Guid id;
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            id = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(CreateProductionSql,
                new { Id = Guid.CreateVersion7(), Tenant = tenant, UniverseCode = universeCode, EpisodeCode = episodeCode, Title = title },
                cancellationToken: ct));
        }

        return await GetProductionAsync(tenant, id, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
    }

    public async Task<LinesheetProductionRecord?> GetProductionAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LinesheetProductionRecord>(new CommandDefinition(
            ProductionSelect + " WHERE tenant_id = @Tenant AND id = @Id", new { Tenant = tenant, Id = productionId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LinesheetProductionRecord>> ListProductionsAsync(Guid tenant, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<LinesheetProductionRecord>(new CommandDefinition(
            ProductionSelect + " WHERE tenant_id = @Tenant ORDER BY updated_at DESC LIMIT 200", new { Tenant = tenant }, cancellationToken: ct))).ToList();
    }

    internal const string UpdateSettingsSql = """
        UPDATE pack_content.linesheet_production
        SET settings_json = @Json::jsonb, updated_at = NOW()
        WHERE tenant_id = @Tenant AND id = @Id
        """;

    public async Task UpdateSettingsAsync(Guid tenant, Guid productionId, string settingsJson, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(new CommandDefinition(UpdateSettingsSql,
            new { Tenant = tenant, Id = productionId, Json = settingsJson }, cancellationToken: ct));
        if (n != 1) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
    }

    // ---------- phiên bản kịch bản ----------

    internal const string VersionSelect = """
        SELECT id AS Id, tenant_id AS TenantId, production_id AS ProductionId, version_no AS VersionNo,
               source_sha256 AS SourceSha256, CAST(rows_json AS text) AS RowsJson, row_count AS RowCount, created_at AS CreatedAt
        FROM pack_content.linesheet_version
        """;

    // Khóa dòng sản xuất để hai lần nhập cùng lúc không lấy trùng số phiên bản.
    internal const string InsertVersionSql = """
        WITH p AS (
            SELECT id FROM pack_content.linesheet_production
            WHERE tenant_id = @Tenant AND id = @ProductionId
            FOR UPDATE
        )
        INSERT INTO pack_content.linesheet_version (id, tenant_id, production_id, version_no, source_sha256, rows_json, row_count)
        SELECT @Id, @Tenant, p.id,
               COALESCE((SELECT MAX(v.version_no) FROM pack_content.linesheet_version v
                         WHERE v.tenant_id = @Tenant AND v.production_id = p.id), 0) + 1,
               @Sha, @RowsJson::jsonb, @RowCount
        FROM p
        RETURNING id
        """;

    public async Task<LinesheetVersionRecord> InsertVersionAsync(Guid tenant, Guid productionId, string sourceSha256, string rowsJson, int rowCount, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var id = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(InsertVersionSql,
            new { Id = Guid.CreateVersion7(), Tenant = tenant, ProductionId = productionId, Sha = sourceSha256, RowsJson = rowsJson, RowCount = rowCount },
            tx, cancellationToken: ct)) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE pack_content.linesheet_production SET updated_at = NOW() WHERE tenant_id = @Tenant AND id = @Id",
            new { Tenant = tenant, Id = productionId }, tx, cancellationToken: ct));
        var v = await conn.QuerySingleAsync<LinesheetVersionRecord>(new CommandDefinition(
            VersionSelect + " WHERE tenant_id = @Tenant AND id = @Id", new { Tenant = tenant, Id = id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return v;
    }

    public async Task<LinesheetVersionRecord?> GetLatestVersionAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<LinesheetVersionRecord>(new CommandDefinition(
            VersionSelect + " WHERE tenant_id = @Tenant AND production_id = @Id ORDER BY version_no DESC LIMIT 1",
            new { Tenant = tenant, Id = productionId }, cancellationToken: ct));
    }

    public async Task<LinesheetVersionRecord?> GetVersionAsync(Guid tenant, Guid versionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LinesheetVersionRecord>(new CommandDefinition(
            VersionSelect + " WHERE tenant_id = @Tenant AND id = @Id", new { Tenant = tenant, Id = versionId }, cancellationToken: ct));
    }

    // ---------- file ----------

    internal const string UpsertFileSql = """
        INSERT INTO pack_content.linesheet_file AS f
            (id, tenant_id, production_id, kind, file_key, storage_key, sha256, size_bytes, duration_ms)
        SELECT @Id, @Tenant, p.id, @Kind, @FileKey, @StorageKey, @Sha256, @SizeBytes, @DurationMs
        FROM pack_content.linesheet_production p
        WHERE p.tenant_id = @Tenant AND p.id = @ProductionId
        ON CONFLICT (production_id, kind, file_key) DO UPDATE
            SET storage_key = EXCLUDED.storage_key, sha256 = EXCLUDED.sha256, size_bytes = EXCLUDED.size_bytes,
                duration_ms = EXCLUDED.duration_ms, updated_at = NOW()
            WHERE f.tenant_id = EXCLUDED.tenant_id
        """;

    public async Task UpsertFileAsync(Guid tenant, LinesheetFileRecord file, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(new CommandDefinition(UpsertFileSql, new
        {
            Id = Guid.CreateVersion7(),
            Tenant = tenant,
            file.ProductionId,
            file.Kind,
            file.FileKey,
            file.StorageKey,
            file.Sha256,
            file.SizeBytes,
            file.DurationMs,
        }, cancellationToken: ct));
        if (n != 1) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
    }

    internal const string ListFilesSql = """
        SELECT id AS Id, tenant_id AS TenantId, production_id AS ProductionId, kind AS Kind, file_key AS FileKey,
               storage_key AS StorageKey, sha256 AS Sha256, size_bytes AS SizeBytes, duration_ms AS DurationMs, created_at AS CreatedAt
        FROM pack_content.linesheet_file
        WHERE tenant_id = @Tenant AND production_id = @Id
        ORDER BY kind, file_key
        """;

    public async Task<IReadOnlyList<LinesheetFileRecord>> ListFilesAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<LinesheetFileRecord>(new CommandDefinition(ListFilesSql,
            new { Tenant = tenant, Id = productionId }, cancellationToken: ct))).ToList();
    }

    // ---------- dòng thời gian ----------

    internal const string TimelineSelect = """
        SELECT id AS Id, tenant_id AS TenantId, production_id AS ProductionId, version_id AS VersionId,
               audio_set_sha256 AS AudioSetSha256, CAST(entries_json AS text) AS EntriesJson,
               CAST(estimated_json AS text) AS EstimatedJson, total_ms AS TotalMs,
               audio_storage_key AS AudioStorageKey, audio_duration_ms AS AudioDurationMs, created_at AS CreatedAt
        FROM pack_content.linesheet_timeline
        """;

    internal const string InsertTimelineSql = """
        INSERT INTO pack_content.linesheet_timeline
            (id, tenant_id, production_id, version_id, audio_set_sha256, entries_json, estimated_json, total_ms)
        SELECT @Id, @Tenant, v.production_id, v.id, @AudioSetSha256, @EntriesJson::jsonb, @EstimatedJson::jsonb, @TotalMs
        FROM pack_content.linesheet_version v
        WHERE v.tenant_id = @Tenant AND v.id = @VersionId AND v.production_id = @ProductionId
        """;

    public async Task<LinesheetTimelineRecord> InsertTimelineAsync(Guid tenant, LinesheetTimelineRecord timeline, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            var n = await conn.ExecuteAsync(new CommandDefinition(InsertTimelineSql, new
            {
                Id = id,
                Tenant = tenant,
                timeline.ProductionId,
                timeline.VersionId,
                timeline.AudioSetSha256,
                timeline.EntriesJson,
                timeline.EstimatedJson,
                timeline.TotalMs,
            }, cancellationToken: ct));
            if (n != 1) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        }

        return await GetTimelineAsync(tenant, id, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
    }

    public async Task<LinesheetTimelineRecord?> GetLatestTimelineAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<LinesheetTimelineRecord>(new CommandDefinition(
            TimelineSelect + " WHERE tenant_id = @Tenant AND production_id = @Id ORDER BY created_at DESC, id DESC LIMIT 1",
            new { Tenant = tenant, Id = productionId }, cancellationToken: ct));
    }

    public async Task<LinesheetTimelineRecord?> GetTimelineAsync(Guid tenant, Guid timelineId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LinesheetTimelineRecord>(new CommandDefinition(
            TimelineSelect + " WHERE tenant_id = @Tenant AND id = @Id", new { Tenant = tenant, Id = timelineId }, cancellationToken: ct));
    }

    internal const string SetTimelineAudioSql = """
        UPDATE pack_content.linesheet_timeline
        SET audio_storage_key = @Key, audio_duration_ms = @Ms
        WHERE tenant_id = @Tenant AND id = @Id
        """;

    public async Task SetTimelineAudioAsync(Guid tenant, Guid timelineId, string storageKey, int durationMs, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(SetTimelineAudioSql,
            new { Tenant = tenant, Id = timelineId, Key = storageKey, Ms = durationMs }, cancellationToken: ct));
    }

    // ---------- bảng shot ----------

    internal const string ShotSetSelect = """
        SELECT id AS Id, tenant_id AS TenantId, production_id AS ProductionId, version_id AS VersionId,
               CAST(shots_json AS text) AS ShotsJson, shot_count AS ShotCount, edited AS Edited,
               created_by AS CreatedBy, created_at AS CreatedAt
        FROM pack_content.linesheet_shot_set
        """;

    internal const string InsertShotSetSql = """
        INSERT INTO pack_content.linesheet_shot_set (id, tenant_id, production_id, version_id, shots_json, shot_count, edited, created_by)
        SELECT @Id, @Tenant, v.production_id, v.id, @ShotsJson::jsonb, @ShotCount, @Edited, @CreatedBy
        FROM pack_content.linesheet_version v
        WHERE v.tenant_id = @Tenant AND v.id = @VersionId AND v.production_id = @ProductionId
        """;

    public async Task<LinesheetShotSetRecord> InsertShotSetAsync(Guid tenant, LinesheetShotSetRecord shotSet, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            var n = await conn.ExecuteAsync(new CommandDefinition(InsertShotSetSql, new
            {
                Id = id,
                Tenant = tenant,
                shotSet.ProductionId,
                shotSet.VersionId,
                shotSet.ShotsJson,
                shotSet.ShotCount,
                shotSet.Edited,
                CreatedBy = shotSet.CreatedBy ?? "",
            }, cancellationToken: ct));
            if (n != 1) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        }

        return await GetShotSetAsync(tenant, id, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
    }

    public async Task<LinesheetShotSetRecord?> GetLatestShotSetAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<LinesheetShotSetRecord>(new CommandDefinition(
            ShotSetSelect + " WHERE tenant_id = @Tenant AND production_id = @Id ORDER BY created_at DESC, id DESC LIMIT 1",
            new { Tenant = tenant, Id = productionId }, cancellationToken: ct));
    }

    public async Task<LinesheetShotSetRecord?> GetShotSetAsync(Guid tenant, Guid shotSetId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LinesheetShotSetRecord>(new CommandDefinition(
            ShotSetSelect + " WHERE tenant_id = @Tenant AND id = @Id", new { Tenant = tenant, Id = shotSetId }, cancellationToken: ct));
    }

    // ---------- lần dựng ----------

    internal const string RunSelect = """
        SELECT id AS Id, tenant_id AS TenantId, production_id AS ProductionId, version_id AS VersionId,
               timeline_id AS TimelineId, shot_set_id AS ShotSetId, kind AS Kind, format AS Format,
               width AS Width, height AS Height, fps AS Fps, range_start_ms AS RangeStartMs, range_end_ms AS RangeEndMs,
               text_lang AS TextLang, burn_subs AS BurnSubs, status AS Status, next_step AS NextStep, step_count AS StepCount,
               attempts AS Attempts, frames_expected AS FramesExpected, frames_actual AS FramesActual,
               CAST(shot_log_json AS text) AS ShotLogJson, output_storage_key AS OutputStorageKey,
               srt_vi_storage_key AS SrtViStorageKey, srt_en_storage_key AS SrtEnStorageKey, error AS Error,
               created_by AS CreatedBy, available_at AS AvailableAt, heartbeat_at AS HeartbeatAt,
               created_at AS CreatedAt, started_at AS StartedAt, completed_at AS CompletedAt
        FROM pack_content.linesheet_render_run
        """;

    // version/timeline/shot set phải cùng tenant và cùng sản xuất (khóa ngoại kép (id, tenant_id) chặn thêm một lớp).
    internal const string InsertRunSql = """
        INSERT INTO pack_content.linesheet_render_run (
            id, tenant_id, production_id, version_id, timeline_id, shot_set_id, kind, format, width, height, fps,
            range_start_ms, range_end_ms, text_lang, burn_subs, status, next_step, step_count, frames_expected, created_by)
        SELECT @Id, @Tenant, t.production_id, t.version_id, t.id, s.id, @Kind, @Format, @Width, @Height, @Fps,
               @RangeStartMs, @RangeEndMs, @TextLang, @BurnSubs, 'QUEUED', 0, @StepCount, @FramesExpected, @CreatedBy
        FROM pack_content.linesheet_timeline t
        JOIN pack_content.linesheet_shot_set s
          ON s.tenant_id = t.tenant_id AND s.production_id = t.production_id AND s.version_id = t.version_id
        WHERE t.tenant_id = @Tenant AND t.id = @TimelineId AND s.id = @ShotSetId
          AND t.production_id = @ProductionId AND t.version_id = @VersionId
        """;

    public async Task<LinesheetRenderRunRecord> InsertRunAsync(Guid tenant, LinesheetRenderRunRecord run, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            var n = await conn.ExecuteAsync(new CommandDefinition(InsertRunSql, new
            {
                Id = id,
                Tenant = tenant,
                run.ProductionId,
                run.VersionId,
                run.TimelineId,
                run.ShotSetId,
                run.Kind,
                run.Format,
                run.Width,
                run.Height,
                run.Fps,
                run.RangeStartMs,
                run.RangeEndMs,
                run.TextLang,
                run.BurnSubs,
                run.StepCount,
                run.FramesExpected,
                CreatedBy = run.CreatedBy ?? "",
            }, cancellationToken: ct));
            if (n != 1) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        }

        return await GetRunAsync(tenant, id, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
    }

    public async Task<LinesheetRenderRunRecord?> GetRunAsync(Guid tenant, Guid runId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LinesheetRenderRunRecord>(new CommandDefinition(
            RunSelect + " WHERE tenant_id = @Tenant AND id = @Id", new { Tenant = tenant, Id = runId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LinesheetRenderRunRecord>> ListRunsAsync(Guid tenant, Guid productionId, int limit, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<LinesheetRenderRunRecord>(new CommandDefinition(
            RunSelect + " WHERE tenant_id = @Tenant AND production_id = @Id ORDER BY created_at DESC LIMIT @Limit",
            new { Tenant = tenant, Id = productionId, Limit = Math.Clamp(limit, 1, 200) }, cancellationToken: ct))).ToList();
    }

    internal const string CancelRunSql = """
        UPDATE pack_content.linesheet_render_run
        SET status = 'CANCELLED', completed_at = NOW(), heartbeat_at = NULL, updated_at = NOW()
        WHERE tenant_id = @Tenant AND id = @Id AND status IN ('QUEUED', 'RUNNING')
        """;

    public async Task<bool> CancelRunAsync(Guid tenant, Guid runId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(CancelRunSql, new { Tenant = tenant, Id = runId }, cancellationToken: ct)) == 1;
    }

    /// <summary>
    /// Nhận một lần dựng: QUEUED, hoặc RUNNING mà không ai giữ (heartbeat NULL = bước trước đã xong và trả khóa;
    /// heartbeat quá hạn = máy tắt giữa bước). Đặt heartbeat để worker khác không nhận trùng.
    /// </summary>
    internal const string ClaimRunSql = """
        UPDATE pack_content.linesheet_render_run AS r
        SET status = 'RUNNING',
            heartbeat_at = NOW(),
            started_at = COALESCE(r.started_at, NOW()),
            updated_at = NOW()
        FROM (
            SELECT id
            FROM pack_content.linesheet_render_run
            WHERE status IN ('QUEUED', 'RUNNING')
              AND available_at <= NOW()
              AND (heartbeat_at IS NULL OR heartbeat_at < NOW() - make_interval(secs => @StaleSeconds))
            ORDER BY available_at, created_at
            LIMIT 1
            FOR UPDATE SKIP LOCKED
        ) pick
        WHERE r.id = pick.id
        RETURNING r.id AS Id, r.tenant_id AS TenantId
        """;

    sealed class ClaimRow
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
    }

    public async Task<LinesheetRenderRunRecord?> ClaimNextRunAsync(TimeSpan staleAfter, CancellationToken ct)
    {
        ClaimRow? pick;
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            pick = await conn.QuerySingleOrDefaultAsync<ClaimRow>(new CommandDefinition(ClaimRunSql,
                new { StaleSeconds = staleAfter.TotalSeconds }, cancellationToken: ct));
        }

        return pick is null ? null : await GetRunAsync(pick.TenantId, pick.Id, ct);
    }

    // Không ghi đè lần dựng đã bị hủy trong lúc worker đang làm bước.
    internal const string SaveRunSql = """
        UPDATE pack_content.linesheet_render_run
        SET status = @Status, next_step = @NextStep, attempts = @Attempts,
            frames_expected = @FramesExpected, frames_actual = @FramesActual,
            shot_log_json = @ShotLogJson::jsonb, output_storage_key = @OutputStorageKey,
            srt_vi_storage_key = @SrtViStorageKey, srt_en_storage_key = @SrtEnStorageKey,
            error = @Error, available_at = @AvailableAt, heartbeat_at = @HeartbeatAt,
            completed_at = @CompletedAt, updated_at = NOW()
        WHERE tenant_id = @TenantId AND id = @Id AND status IN ('QUEUED', 'RUNNING')
        """;

    public async Task SaveRunProgressAsync(LinesheetRenderRunRecord run, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(SaveRunSql, new
        {
            run.TenantId,
            run.Id,
            run.Status,
            run.NextStep,
            run.Attempts,
            run.FramesExpected,
            run.FramesActual,
            run.ShotLogJson,
            run.OutputStorageKey,
            run.SrtViStorageKey,
            run.SrtEnStorageKey,
            Error = run.Error is { Length: > 1000 } e ? e[..1000] : run.Error,
            run.AvailableAt,
            run.HeartbeatAt,
            run.CompletedAt,
        }, cancellationToken: ct));
    }
}
