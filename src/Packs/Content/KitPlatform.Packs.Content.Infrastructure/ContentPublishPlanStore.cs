using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Lưu cài đặt lịch đăng theo thương hiệu và kế hoạch đăng theo bài (migration 421).
/// Bảng được tạo nếu chưa có (giống 421), để máy dev chưa chạy migration vẫn dùng được.
/// </summary>
internal sealed class ContentPublishPlanStore : IContentPublishPlanStore
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private static int _ensured;

    private const string EnsureSql = """
        CREATE TABLE IF NOT EXISTS pack_content.brand_schedule_setting (
            brand_id      UUID PRIMARY KEY REFERENCES pack_content.brand(id) ON DELETE CASCADE,
            settings_json JSONB NOT NULL DEFAULT '{}'::jsonb,
            updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE TABLE IF NOT EXISTS pack_content.topic_publish_plan (
            topic_id    UUID PRIMARY KEY REFERENCES pack_content.topic(id) ON DELETE CASCADE,
            kind        VARCHAR(16) NOT NULL,
            connectors  TEXT[] NOT NULL DEFAULT '{}',
            seq         INT NOT NULL DEFAULT 0,
            source_ref  VARCHAR(200),
            created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            CONSTRAINT ck_content_topic_publish_plan_kind CHECK (kind IN ('long', 'short'))
        );
        CREATE TABLE IF NOT EXISTS pack_content.autopilot_run (
            id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            brand_id    UUID NOT NULL REFERENCES pack_content.brand(id) ON DELETE CASCADE,
            series_id   UUID NOT NULL,
            episode_id  UUID NOT NULL,
            topic_id    UUID,
            note        VARCHAR(300) NOT NULL DEFAULT '',
            created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        CREATE INDEX IF NOT EXISTS ix_content_autopilot_run_brand ON pack_content.autopilot_run (brand_id, created_at DESC);
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_content_schedule_proposal_status'
                     AND pg_get_constraintdef(oid) NOT LIKE '%Posted%') THEN
            ALTER TABLE pack_content.schedule_proposal DROP CONSTRAINT ck_content_schedule_proposal_status;
            ALTER TABLE pack_content.schedule_proposal ADD CONSTRAINT ck_content_schedule_proposal_status
              CHECK (status IN ('Proposed', 'Approved', 'Posted', 'Rejected', 'Cancelled', 'Failed'));
          END IF;
        EXCEPTION WHEN insufficient_privilege THEN
          RAISE NOTICE 'Không đủ quyền sửa ràng buộc schedule_proposal; chạy migrations/422.';
        END $$;
        """;

    private readonly IDbConnectionFactory _db;

    public ContentPublishPlanStore(IDbConnectionFactory db) => _db = db;

    private async Task EnsureAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _ensured) == 1) return;
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(EnsureSql);
            Interlocked.Exchange(ref _ensured, 1);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Chưa có bảng lịch đăng. Chạy migrations/421_pack_content_publish_plan.sql rồi thử lại. " + ex.Message);
        }
    }

    public async Task<ContentBrandScheduleSettingsDto> GetSettingsAsync(Guid brandId, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var raw = await conn.ExecuteScalarAsync<string?>(
            "SELECT settings_json::text FROM pack_content.brand_schedule_setting WHERE brand_id = @BrandId",
            new { BrandId = brandId });
        var defaults = ContentPublishPlanRules.Defaults();
        if (string.IsNullOrWhiteSpace(raw)) return defaults;
        try
        {
            var request = JsonSerializer.Deserialize<ContentBrandScheduleSettingsRequest>(raw, Json) ?? new();
            return ContentPublishPlanRules.Normalize(request, defaults);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return defaults;
        }
    }

    public async Task<ContentBrandScheduleSettingsDto> SaveSettingsAsync(
        Guid brandId, ContentBrandScheduleSettingsRequest request, CancellationToken ct = default)
    {
        var current = await GetSettingsAsync(brandId, ct);
        var next = ContentPublishPlanRules.Normalize(request, current);
        var json = JsonSerializer.Serialize(new ContentBrandScheduleSettingsRequest
        {
            LocalSlots = next.LocalSlots.ToList(),
            MinGapHours = next.MinGapHours,
            MaxPerDayPerChannel = next.MaxPerDayPerChannel,
            ShortPerDayPerChannel = next.ShortPerDayPerChannel,
            LongPerWeekPerChannel = next.LongPerWeekPerChannel,
            Days = next.Days,
            AutopilotEnabled = next.AutopilotEnabled,
            AutopilotPostsPerWeek = next.AutopilotPostsPerWeek,
            PublishPaused = next.PublishPaused,
            MediaKeepDaysPosted = next.MediaKeepDaysPosted,
            MediaKeepDaysCancelled = next.MediaKeepDaysCancelled,
        }, Json);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_content.brand_schedule_setting (brand_id, settings_json, updated_at)
            VALUES (@BrandId, @Json::jsonb, NOW())
            ON CONFLICT (brand_id) DO UPDATE SET settings_json = EXCLUDED.settings_json, updated_at = NOW()
            """,
            new { BrandId = brandId, Json = json });
        return next;
    }

    public async Task SavePlansAsync(IReadOnlyList<ContentTopicPublishPlanInput> plans, CancellationToken ct = default)
    {
        if (plans.Count == 0) return;
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        foreach (var plan in plans)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_content.topic_publish_plan (topic_id, kind, connectors, seq, source_ref, updated_at)
                VALUES (@TopicId, @Kind, @Connectors, @Seq, @SourceRef, NOW())
                ON CONFLICT (topic_id) DO UPDATE SET
                    kind = EXCLUDED.kind, connectors = EXCLUDED.connectors, seq = EXCLUDED.seq,
                    source_ref = EXCLUDED.source_ref, updated_at = NOW()
                """,
                new { plan.TopicId, plan.Kind, Connectors = plan.Connectors.ToArray(), plan.Seq, plan.SourceRef });
        }
    }

    public async Task<IReadOnlyDictionary<Guid, ContentTopicPublishPlanDto>> GetPlansAsync(
        IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, ContentTopicPublishPlanDto>();
        if (topicIds.Count == 0) return result;
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PlanRow>(
            """
            SELECT topic_id AS "TopicId", kind AS "Kind", connectors AS "Connectors", seq AS "Seq", source_ref AS "SourceRef"
            FROM pack_content.topic_publish_plan WHERE topic_id = ANY(@Ids)
            """,
            new { Ids = topicIds.Distinct().ToArray() });
        foreach (var r in rows)
            result[r.TopicId] = new ContentTopicPublishPlanDto(r.TopicId, r.Kind, r.Connectors ?? [], r.Seq, r.SourceRef);
        return result;
    }

    public async Task<IReadOnlyList<ContentManualSlot>> ListManualSlotsAsync(Guid brandId, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ManualRow>(
            """
            SELECT topic_id AS "TopicId", channel_id AS "ChannelId", site_id AS "SiteId", publish_at AS "PublishAt", status AS "Status"
            FROM pack_content.schedule_proposal
            WHERE brand_id = @BrandId AND publish_job_id IS NULL AND status IN ('Approved', 'Posted')
              AND publish_at > NOW() - INTERVAL '30 days'
            """,
            new { BrandId = brandId });
        return rows.Select(r => new ContentManualSlot(r.TopicId, r.ChannelId, r.SiteId, r.PublishAt, r.Status)).ToList();
    }

    private sealed class ManualRow
    {
        public Guid TopicId { get; set; }
        public Guid? ChannelId { get; set; }
        public Guid? SiteId { get; set; }
        public DateTimeOffset PublishAt { get; set; }
        public string Status { get; set; } = "";
    }

    public async Task<IReadOnlyList<Guid>> ListAutopilotBrandsAsync(CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var ids = await conn.QueryAsync<Guid>(
            """
            SELECT s.brand_id FROM pack_content.brand_schedule_setting s
            JOIN pack_content.brand b ON b.id = s.brand_id AND b.is_active
            WHERE (s.settings_json ->> 'autopilotEnabled') = 'true'
            """);
        return ids.ToList();
    }

    public async Task<int> CountAutopilotRunsAsync(Guid brandId, DateTimeOffset since, bool includeFailed = false, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*)::int FROM pack_content.autopilot_run WHERE brand_id = @BrandId AND created_at >= @Since AND (@All OR topic_id IS NOT NULL)",
            new { BrandId = brandId, Since = since.ToUniversalTime(), All = includeFailed });
    }

    public async Task<int> CountAutopilotPendingAsync(Guid brandId, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT r.topic_id)::int FROM pack_content.autopilot_run r
            JOIN pack_content.topic t ON t.id = r.topic_id
            WHERE r.brand_id = @BrandId AND r.created_at >= NOW() - INTERVAL '21 days'
              AND t.status IN ('Draft', 'Generating', 'Review')
              AND NOT EXISTS (
                SELECT 1 FROM pack_content.publish_job j
                WHERE j.topic_id = t.id
                  AND j.connector_type IN ('astro_git', 'wordpress_rest', 'local_os')
                  AND j.status IN ('Queued', 'Running', 'Succeeded')
              )
            """,
            new { BrandId = brandId });
    }

    public async Task AddAutopilotRunAsync(Guid brandId, Guid seriesId, Guid episodeId, Guid? topicId, string note, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_content.autopilot_run (brand_id, series_id, episode_id, topic_id, note)
            VALUES (@BrandId, @SeriesId, @EpisodeId, @TopicId, @Note)
            """,
            new { BrandId = brandId, SeriesId = seriesId, EpisodeId = episodeId, TopicId = topicId, Note = note.Length > 300 ? note[..300] : note });
    }

    public async Task<IReadOnlyList<Guid>> ListAutopilotTopicIdsAsync(Guid brandId, CancellationToken ct = default)
    {
        await EnsureAsync(ct);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var ids = await conn.QueryAsync<Guid>(
            """
            SELECT DISTINCT topic_id FROM pack_content.autopilot_run
            WHERE brand_id = @BrandId AND topic_id IS NOT NULL
            """,
            new { BrandId = brandId });
        return ids.ToList();
    }

    private sealed class PlanRow
    {
        public Guid TopicId { get; set; }
        public string Kind { get; set; } = "short";
        public string[]? Connectors { get; set; }
        public int Seq { get; set; }
        public string? SourceRef { get; set; }
    }
}
