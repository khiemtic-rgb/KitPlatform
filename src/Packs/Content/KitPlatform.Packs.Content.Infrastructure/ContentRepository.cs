using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbConnectionFactory _db;

    public ContentRepository(IDbConnectionFactory db) => _db = db;

    public sealed class OrgSettingsRow
    {
        public Guid Id { get; set; }
        public decimal MonthlyCeilingUsd { get; set; }
        public int MaxImageCandidatesPerItem { get; set; }
        public decimal RegenMultiplier { get; set; }
        public string DefaultImageTier { get; set; } = "balanced";
        public string ImageRateUsdJson { get; set; } = "{}";
        public decimal TextPackEstimateUsd { get; set; }
        public string VariantKindsJson { get; set; } = "[]";
        public string ConnectorTypesJson { get; set; } = "[]";
        public string ChannelTypesJson { get; set; } = "[]";
        public string AiConfigJson { get; set; } = "{}";
        public string VideoConfigJson { get; set; } = "{}";
        public string FacebookConfigJson { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class BrandRow
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? DefaultCtaUrl { get; set; }
        public string? DefaultCtaLabel { get; set; }
        public decimal? MonthlyCeilingUsd { get; set; }
        public string? ImageTier { get; set; }
        public bool PauseWhenExceeded { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? OperationalBrief { get; set; }
        public string ToneJson { get; set; } = "{}";
        public string VisualKitJson { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class SiteRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string ConnectorType { get; set; } = "";
        public string? BaseUrl { get; set; }
        public string ConfigJson { get; set; } = "{}";
        public string? SecretRef { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class ChannelRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string ChannelType { get; set; } = "";
        public string? ExternalId { get; set; }
        public string ConfigJson { get; set; } = "{}";
        public string? SecretRef { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class TopicRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Pillar { get; set; }
        public string Goal { get; set; } = "traffic";
        public string? CtaUrl { get; set; }
        public string? UtmCampaign { get; set; }
        public string Priority { get; set; } = "P1";
        public string Status { get; set; } = "Draft";
        public string? BodyOutline { get; set; }
        public DateTimeOffset? DisplayAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int VariantCount { get; set; }
        public Guid? CorePackageId { get; set; }
        public string? CoreTitle { get; set; }
    }

    public sealed class PackageRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public Guid TopicId { get; set; }
        public string Title { get; set; } = "";
        public string? Angle { get; set; }
        public string? Audience { get; set; }
        public string ContentType { get; set; } = "educational";
        public string? Pillar { get; set; }
        public string Goal { get; set; } = "traffic";
        public string Priority { get; set; } = "P1";
        public string Status { get; set; } = "Draft";
        public Guid? SourcePackageId { get; set; }
        public string? SourceTitle { get; set; }
        public DateTimeOffset? DisplayAt { get; set; }
        public int VariantCount { get; set; }
        public int AdaptationCount { get; set; }
        public string ExtraJson { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class VariantRow
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public string Kind { get; set; } = "";
        public string? Title { get; set; }
        public string BodyMarkdown { get; set; } = "";
        public string MetaJson { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class AssetRow
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public string Kind { get; set; } = "image";
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "image/png";
        public string StoragePath { get; set; } = "";
        public string? Prompt { get; set; }
        public string? Model { get; set; }
        public string? ImageTier { get; set; }
        public decimal EstimateUsd { get; set; }
        public bool IsSelected { get; set; }
        public string MetaJson { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class PublishJobRow
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public Guid BrandId { get; set; }
        public string TargetKind { get; set; } = "";
        public Guid? SiteTargetId { get; set; }
        public Guid? ChannelTargetId { get; set; }
        public string ConnectorType { get; set; } = "";
        public string Status { get; set; } = "Queued";
        public DateTimeOffset? PublishAt { get; set; }
        public string? ExternalRef { get; set; }
        public string? LastError { get; set; }
        public string ResultJson { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public sealed class SeriesPilotRow
    {
        public string SeriesCode { get; set; } = "";
        public string GraphJson { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public async Task<OrgSettingsRow> GetOrgSettingsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT
                id AS Id,
                monthly_ceiling_usd AS MonthlyCeilingUsd,
                max_image_candidates_per_item AS MaxImageCandidatesPerItem,
                regen_multiplier AS RegenMultiplier,
                default_image_tier AS DefaultImageTier,
                image_rate_usd_json::text AS ImageRateUsdJson,
                text_pack_estimate_usd AS TextPackEstimateUsd,
                variant_kinds_json::text AS VariantKindsJson,
                connector_types_json::text AS ConnectorTypesJson,
                channel_types_json::text AS ChannelTypesJson,
                COALESCE(ai_config_json, '{}'::jsonb)::text AS AiConfigJson,
                COALESCE(video_config_json, '{}'::jsonb)::text AS VideoConfigJson,
                COALESCE(facebook_config_json, '{}'::jsonb)::text AS FacebookConfigJson,
                updated_at AS UpdatedAt
            FROM pack_content.org_settings
            ORDER BY updated_at
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<OrgSettingsRow>(sql);
        if (row is null)
            throw new InvalidOperationException("Content org_settings missing — apply migration-files.content.txt");
        return row;
    }

    public async Task UpdateOrgSettingsAsync(OrgSettingsRow row, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.org_settings SET
                monthly_ceiling_usd = @MonthlyCeilingUsd,
                max_image_candidates_per_item = @MaxImageCandidatesPerItem,
                regen_multiplier = @RegenMultiplier,
                default_image_tier = @DefaultImageTier,
                image_rate_usd_json = @ImageRateUsdJson::jsonb,
                text_pack_estimate_usd = @TextPackEstimateUsd,
                variant_kinds_json = @VariantKindsJson::jsonb,
                connector_types_json = @ConnectorTypesJson::jsonb,
                channel_types_json = @ChannelTypesJson::jsonb,
                ai_config_json = @AiConfigJson::jsonb,
                video_config_json = @VideoConfigJson::jsonb,
                facebook_config_json = @FacebookConfigJson::jsonb,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<decimal> SumSpendAsync(Guid? brandId, DateTimeOffset fromUtc, CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(SUM(estimate_usd), 0)
            FROM pack_content.usage_ledger
            WHERE created_at >= @FromUtc
              AND (@BrandId IS NULL OR brand_id = @BrandId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<decimal>(sql, new { BrandId = brandId, FromUtc = fromUtc });
    }

    public async Task<IReadOnlyList<BrandRow>> ListBrandsAsync(bool? activeOnly, CancellationToken ct)
    {
        var sql = """
            SELECT
                id AS Id, code AS Code, name AS Name,
                default_cta_url AS DefaultCtaUrl, default_cta_label AS DefaultCtaLabel,
                monthly_ceiling_usd AS MonthlyCeilingUsd, image_tier AS ImageTier,
                pause_when_exceeded AS PauseWhenExceeded, is_active AS IsActive,
                sort_order AS SortOrder, operational_brief AS OperationalBrief,
                tone_json::text AS ToneJson, visual_kit_json::text AS VisualKitJson,
                updated_at AS UpdatedAt
            FROM pack_content.brand
            WHERE (@ActiveOnly IS NULL OR is_active = @ActiveOnly)
            ORDER BY sort_order, name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<BrandRow>(sql, new { ActiveOnly = activeOnly })).ToList();
    }

    public async Task<BrandRow?> GetBrandAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT
                id AS Id, code AS Code, name AS Name,
                default_cta_url AS DefaultCtaUrl, default_cta_label AS DefaultCtaLabel,
                monthly_ceiling_usd AS MonthlyCeilingUsd, image_tier AS ImageTier,
                pause_when_exceeded AS PauseWhenExceeded, is_active AS IsActive,
                sort_order AS SortOrder, operational_brief AS OperationalBrief,
                tone_json::text AS ToneJson, visual_kit_json::text AS VisualKitJson,
                updated_at AS UpdatedAt
            FROM pack_content.brand WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<BrandRow>(sql, new { Id = id });
    }

    public async Task<Guid> InsertBrandAsync(BrandRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.brand (
                code, name, default_cta_url, default_cta_label,
                monthly_ceiling_usd, image_tier, pause_when_exceeded, is_active, sort_order,
                operational_brief, tone_json, visual_kit_json
            ) VALUES (
                @Code, @Name, @DefaultCtaUrl, @DefaultCtaLabel,
                @MonthlyCeilingUsd, @ImageTier, @PauseWhenExceeded, @IsActive, @SortOrder,
                @OperationalBrief, @ToneJson::jsonb, @VisualKitJson::jsonb
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, row);
    }

    public async Task UpdateBrandAsync(BrandRow row, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.brand SET
                code = @Code,
                name = @Name,
                default_cta_url = @DefaultCtaUrl,
                default_cta_label = @DefaultCtaLabel,
                monthly_ceiling_usd = @MonthlyCeilingUsd,
                image_tier = @ImageTier,
                pause_when_exceeded = @PauseWhenExceeded,
                is_active = @IsActive,
                sort_order = @SortOrder,
                operational_brief = @OperationalBrief,
                tone_json = @ToneJson::jsonb,
                visual_kit_json = @VisualKitJson::jsonb,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, row);
    }

    public async Task<IReadOnlyList<SiteRow>> ListSitesAsync(Guid brandId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, brand_id AS BrandId, code AS Code, name AS Name,
                   connector_type AS ConnectorType, base_url AS BaseUrl,
                   config_json::text AS ConfigJson, secret_ref AS SecretRef,
                   is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.site_target
            WHERE brand_id = @BrandId
            ORDER BY sort_order, name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<SiteRow>(sql, new { BrandId = brandId })).ToList();
    }

    public async Task<Guid> UpsertSiteAsync(Guid brandId, SiteRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.site_target (
                brand_id, code, name, connector_type, base_url, config_json, secret_ref, is_active, sort_order
            ) VALUES (
                @BrandId, @Code, @Name, @ConnectorType, @BaseUrl, @ConfigJson::jsonb, @SecretRef, @IsActive, @SortOrder
            )
            ON CONFLICT (brand_id, code) DO UPDATE SET
                name = EXCLUDED.name,
                connector_type = EXCLUDED.connector_type,
                base_url = EXCLUDED.base_url,
                config_json = EXCLUDED.config_json,
                secret_ref = EXCLUDED.secret_ref,
                is_active = EXCLUDED.is_active,
                sort_order = EXCLUDED.sort_order,
                updated_at = NOW()
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            BrandId = brandId,
            row.Code,
            row.Name,
            row.ConnectorType,
            row.BaseUrl,
            ConfigJson = string.IsNullOrWhiteSpace(row.ConfigJson) ? "{}" : row.ConfigJson,
            row.SecretRef,
            row.IsActive,
            row.SortOrder,
        });
    }

    public async Task<SiteRow?> GetSiteAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, brand_id AS BrandId, code AS Code, name AS Name,
                   connector_type AS ConnectorType, base_url AS BaseUrl,
                   config_json::text AS ConfigJson, secret_ref AS SecretRef,
                   is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.site_target WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SiteRow>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<ChannelRow>> ListChannelsAsync(Guid brandId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, brand_id AS BrandId, code AS Code, name AS Name,
                   channel_type AS ChannelType, external_id AS ExternalId,
                   config_json::text AS ConfigJson, secret_ref AS SecretRef,
                   is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.channel_target
            WHERE brand_id = @BrandId
            ORDER BY sort_order, name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<ChannelRow>(sql, new { BrandId = brandId })).ToList();
    }

    public async Task<Guid> UpsertChannelAsync(Guid brandId, ChannelRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.channel_target (
                brand_id, code, name, channel_type, external_id, config_json, secret_ref, is_active, sort_order
            ) VALUES (
                @BrandId, @Code, @Name, @ChannelType, @ExternalId, @ConfigJson::jsonb, @SecretRef, @IsActive, @SortOrder
            )
            ON CONFLICT (brand_id, code) DO UPDATE SET
                name = EXCLUDED.name,
                channel_type = EXCLUDED.channel_type,
                external_id = EXCLUDED.external_id,
                config_json = EXCLUDED.config_json,
                secret_ref = EXCLUDED.secret_ref,
                is_active = EXCLUDED.is_active,
                sort_order = EXCLUDED.sort_order,
                updated_at = NOW()
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            BrandId = brandId,
            row.Code,
            row.Name,
            row.ChannelType,
            row.ExternalId,
            ConfigJson = string.IsNullOrWhiteSpace(row.ConfigJson) ? "{}" : row.ConfigJson,
            row.SecretRef,
            row.IsActive,
            row.SortOrder,
        });
    }

    public async Task<ChannelRow?> GetChannelAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, brand_id AS BrandId, code AS Code, name AS Name,
                   channel_type AS ChannelType, external_id AS ExternalId,
                   config_json::text AS ConfigJson, secret_ref AS SecretRef,
                   is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.channel_target WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ChannelRow>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<TopicRow>> ListTopicsAsync(Guid? brandId, string? status, CancellationToken ct)
    {
        const string sql = """
            SELECT
                t.id AS Id, t.brand_id AS BrandId,
                b.code AS BrandCode, b.name AS BrandName,
                t.title AS Title, t.pillar AS Pillar, t.goal AS Goal,
                t.cta_url AS CtaUrl, t.utm_campaign AS UtmCampaign,
                t.priority AS Priority, t.status AS Status,
                t.body_outline AS BodyOutline, t.display_at AS DisplayAt,
                t.created_at AS CreatedAt, t.updated_at AS UpdatedAt,
                (SELECT COUNT(*)::int FROM pack_content.variant v WHERE v.topic_id = t.id) AS VariantCount,
                COALESCE(p.source_package_id, p.id) AS CorePackageId,
                COALESCE(src.title, p.title) AS CoreTitle
            FROM pack_content.topic t
            INNER JOIN pack_content.brand b ON b.id = t.brand_id
            LEFT JOIN pack_content.content_package p ON p.topic_id = t.id
            LEFT JOIN pack_content.content_package src ON src.id = p.source_package_id
            WHERE (@BrandId IS NULL OR t.brand_id = @BrandId)
              AND (@Status IS NULL OR t.status = @Status)
              AND (p.id IS NULL OR p.source_package_id IS NOT NULL)
            ORDER BY VariantCount DESC, t.updated_at DESC
            LIMIT 500
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<TopicRow>(sql, new { BrandId = brandId, Status = status })).ToList();
    }

    public async Task<TopicRow?> GetTopicAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT
                t.id AS Id, t.brand_id AS BrandId,
                b.code AS BrandCode, b.name AS BrandName,
                t.title AS Title, t.pillar AS Pillar, t.goal AS Goal,
                t.cta_url AS CtaUrl, t.utm_campaign AS UtmCampaign,
                t.priority AS Priority, t.status AS Status,
                t.body_outline AS BodyOutline, t.display_at AS DisplayAt,
                t.created_at AS CreatedAt, t.updated_at AS UpdatedAt,
                (SELECT COUNT(*)::int FROM pack_content.variant v WHERE v.topic_id = t.id) AS VariantCount,
                COALESCE(p.source_package_id, p.id) AS CorePackageId,
                COALESCE(src.title, p.title) AS CoreTitle
            FROM pack_content.topic t
            INNER JOIN pack_content.brand b ON b.id = t.brand_id
            LEFT JOIN pack_content.content_package p ON p.topic_id = t.id
            LEFT JOIN pack_content.content_package src ON src.id = p.source_package_id
            WHERE t.id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<TopicRow>(sql, new { Id = id });
    }

    public async Task<Guid> InsertTopicAsync(
        Guid brandId,
        string title,
        string? pillar,
        string goal,
        string? ctaUrl,
        string? utm,
        string priority,
        string status,
        string? outline,
        DateTimeOffset? displayAt,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.topic (
                brand_id, title, pillar, goal, cta_url, utm_campaign, priority, status, body_outline, display_at
            ) VALUES (
                @BrandId, @Title, @Pillar, @Goal, @CtaUrl, @Utm, @Priority, @Status, @Outline, @DisplayAt
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            BrandId = brandId,
            Title = title,
            Pillar = pillar,
            Goal = goal,
            CtaUrl = ctaUrl,
            Utm = utm,
            Priority = priority,
            Status = status,
            Outline = outline,
            DisplayAt = displayAt,
        });
    }

    public async Task UpdateTopicCtaAsync(Guid id, string? ctaUrl, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.topic SET cta_url = @CtaUrl, updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, CtaUrl = ctaUrl });
    }

    public async Task UpdateTopicAsync(
        Guid id,
        Guid brandId,
        string title,
        string? pillar,
        string goal,
        string? ctaUrl,
        string? utm,
        string priority,
        string status,
        string? outline,
        DateTimeOffset? displayAt,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.topic SET
                brand_id = @BrandId,
                title = @Title,
                pillar = @Pillar,
                goal = @Goal,
                cta_url = @CtaUrl,
                utm_campaign = @Utm,
                priority = @Priority,
                status = @Status,
                body_outline = @Outline,
                display_at = @DisplayAt,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            BrandId = brandId,
            Title = title,
            Pillar = pillar,
            Goal = goal,
            CtaUrl = ctaUrl,
            Utm = utm,
            Priority = priority,
            Status = status,
            Outline = outline,
            DisplayAt = displayAt,
        });
    }

    public async Task UpdateTopicStatusAsync(Guid id, string status, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.topic SET status = @Status, updated_at = NOW() WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public async Task EnsureTopicDisplayAtAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.topic
            SET display_at = COALESCE(display_at, NOW()), updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> DeleteTopicAsync(Guid id, CancellationToken ct)
    {
        // Cascades variants / assets / publish_jobs / packages (FK ON DELETE CASCADE).
        const string sql = """
            DELETE FROM pack_content.topic WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(sql, new { Id = id });
        return n > 0;
    }

    public async Task InsertUsageAsync(
        Guid? brandId,
        Guid? topicId,
        string kind,
        string? imageTier,
        int quantity,
        decimal estimateUsd,
        string metaJson,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.usage_ledger (
                brand_id, topic_id, kind, image_tier, quantity, estimate_usd, meta_json
            ) VALUES (
                @BrandId, @TopicId, @Kind, @ImageTier, @Quantity, @EstimateUsd, @MetaJson::jsonb
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            BrandId = brandId,
            TopicId = topicId,
            Kind = kind,
            ImageTier = imageTier,
            Quantity = quantity,
            EstimateUsd = estimateUsd,
            MetaJson = string.IsNullOrWhiteSpace(metaJson) ? "{}" : metaJson,
        });
    }

    public async Task UpsertVariantAsync(
        Guid topicId,
        string kind,
        string? title,
        string bodyMarkdown,
        string metaJson,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.variant (topic_id, kind, title, body_markdown, meta_json)
            VALUES (@TopicId, @Kind, @Title, @BodyMarkdown, @MetaJson::jsonb)
            ON CONFLICT (topic_id, kind) DO UPDATE SET
                title = EXCLUDED.title,
                body_markdown = EXCLUDED.body_markdown,
                meta_json = EXCLUDED.meta_json,
                updated_at = NOW()
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            TopicId = topicId,
            Kind = kind,
            Title = title,
            BodyMarkdown = bodyMarkdown,
            MetaJson = string.IsNullOrWhiteSpace(metaJson) ? "{}" : metaJson,
        });
    }

    public async Task<IReadOnlyList<VariantRow>> ListVariantsAsync(Guid topicId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, topic_id AS TopicId, kind AS Kind, title AS Title,
                   body_markdown AS BodyMarkdown, meta_json::text AS MetaJson, updated_at AS UpdatedAt
            FROM pack_content.variant
            WHERE topic_id = @TopicId
            ORDER BY kind
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<VariantRow>(sql, new { TopicId = topicId })).ToList();
    }

    public async Task DeleteAssetsForTopicAsync(Guid topicId, CancellationToken ct)
    {
        const string sql = "DELETE FROM pack_content.asset WHERE topic_id = @TopicId";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { TopicId = topicId });
    }

    public async Task<Guid> InsertAssetAsync(AssetRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.asset (
                id, topic_id, kind, file_name, content_type, storage_path, prompt, model,
                image_tier, estimate_usd, is_selected, meta_json
            ) VALUES (
                COALESCE(@Id, kit_uuid_v7()), @TopicId, @Kind, @FileName, @ContentType, @StoragePath, @Prompt, @Model,
                @ImageTier, @EstimateUsd, @IsSelected, @MetaJson::jsonb
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            Id = row.Id == Guid.Empty ? (Guid?)null : row.Id,
            row.TopicId,
            row.Kind,
            row.FileName,
            row.ContentType,
            row.StoragePath,
            row.Prompt,
            row.Model,
            row.ImageTier,
            row.EstimateUsd,
            row.IsSelected,
            MetaJson = string.IsNullOrWhiteSpace(row.MetaJson) ? "{}" : row.MetaJson,
        });
    }

    public async Task<IReadOnlyList<AssetRow>> ListAssetsAsync(Guid topicId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, topic_id AS TopicId, kind AS Kind, file_name AS FileName,
                   content_type AS ContentType, storage_path AS StoragePath, prompt AS Prompt,
                   model AS Model, image_tier AS ImageTier, estimate_usd AS EstimateUsd,
                   is_selected AS IsSelected, meta_json::text AS MetaJson, created_at AS CreatedAt
            FROM pack_content.asset
            WHERE topic_id = @TopicId
            ORDER BY created_at
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<AssetRow>(sql, new { TopicId = topicId })).ToList();
    }

    public async Task<AssetRow?> GetAssetAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, topic_id AS TopicId, kind AS Kind, file_name AS FileName,
                   content_type AS ContentType, storage_path AS StoragePath, prompt AS Prompt,
                   model AS Model, image_tier AS ImageTier, estimate_usd AS EstimateUsd,
                   is_selected AS IsSelected, meta_json::text AS MetaJson, created_at AS CreatedAt
            FROM pack_content.asset WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AssetRow>(sql, new { Id = id });
    }

    public async Task SelectAssetAsync(Guid topicId, Guid assetId, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.asset SET is_selected = (id = @AssetId)
            WHERE topic_id = @TopicId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { TopicId = topicId, AssetId = assetId });
    }

    public async Task<Guid> InsertPublishJobAsync(PublishJobRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.publish_job (
                topic_id, brand_id, target_kind, site_target_id, channel_target_id,
                connector_type, status, publish_at, result_json
            ) VALUES (
                @TopicId, @BrandId, @TargetKind, @SiteTargetId, @ChannelTargetId,
                @ConnectorType, @Status, @PublishAt, @ResultJson::jsonb
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            row.TopicId,
            row.BrandId,
            row.TargetKind,
            row.SiteTargetId,
            row.ChannelTargetId,
            row.ConnectorType,
            Status = string.IsNullOrWhiteSpace(row.Status) ? "Queued" : row.Status,
            row.PublishAt,
            ResultJson = string.IsNullOrWhiteSpace(row.ResultJson) ? "{}" : row.ResultJson,
        });
    }

    public async Task UpdatePublishJobAsync(PublishJobRow row, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.publish_job SET
                status = @Status,
                external_ref = @ExternalRef,
                last_error = @LastError,
                result_json = @ResultJson::jsonb,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            row.Id,
            row.Status,
            row.ExternalRef,
            row.LastError,
            ResultJson = string.IsNullOrWhiteSpace(row.ResultJson) ? "{}" : row.ResultJson,
        });
    }

    public async Task InsertPublishLogAsync(Guid jobId, string level, string message, string detailJson, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.publish_log (job_id, level, message, detail_json)
            VALUES (@JobId, @Level, @Message, @DetailJson::jsonb)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            JobId = jobId,
            Level = level,
            Message = message,
            DetailJson = string.IsNullOrWhiteSpace(detailJson) ? "{}" : detailJson,
        });
    }

    public async Task<PublishJobRow?> GetPublishJobAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, topic_id AS TopicId, brand_id AS BrandId, target_kind AS TargetKind,
                   site_target_id AS SiteTargetId, channel_target_id AS ChannelTargetId,
                   connector_type AS ConnectorType, status AS Status, publish_at AS PublishAt,
                   external_ref AS ExternalRef, last_error AS LastError,
                   result_json::text AS ResultJson, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM pack_content.publish_job WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PublishJobRow>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<PublishJobRow>> ListPublishJobsAsync(Guid? topicId, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, topic_id AS TopicId, brand_id AS BrandId, target_kind AS TargetKind,
                   site_target_id AS SiteTargetId, channel_target_id AS ChannelTargetId,
                   connector_type AS ConnectorType, status AS Status, publish_at AS PublishAt,
                   external_ref AS ExternalRef, last_error AS LastError,
                   result_json::text AS ResultJson, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM pack_content.publish_job
            WHERE (@TopicId IS NULL OR topic_id = @TopicId)
            ORDER BY created_at DESC
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<PublishJobRow>(sql, new { TopicId = topicId })).ToList();
    }

    public async Task<IReadOnlyList<PackageRow>> ListPackagesAsync(
        Guid? brandId,
        string? status,
        bool coresOnly,
        CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, p.brand_id AS BrandId, b.code AS BrandCode, b.name AS BrandName,
                   p.topic_id AS TopicId, p.title AS Title, p.angle AS Angle, p.audience AS Audience,
                   p.content_type AS ContentType, p.pillar AS Pillar, p.goal AS Goal,
                   p.priority AS Priority, COALESCE(t.status, p.status) AS Status,
                   p.source_package_id AS SourcePackageId, src.title AS SourceTitle, t.display_at AS DisplayAt,
                   (SELECT COUNT(*)::int FROM pack_content.variant v WHERE v.topic_id = p.topic_id) AS VariantCount,
                   (SELECT COUNT(*)::int FROM pack_content.content_package c WHERE c.source_package_id = p.id) AS AdaptationCount,
                   CAST(p.extra_json AS text) AS ExtraJson,
                   p.created_at AS CreatedAt, p.updated_at AS UpdatedAt
            FROM pack_content.content_package p
            INNER JOIN pack_content.brand b ON b.id = p.brand_id
            INNER JOIN pack_content.topic t ON t.id = p.topic_id
            LEFT JOIN pack_content.content_package src ON src.id = p.source_package_id
            WHERE (@BrandId IS NULL OR p.brand_id = @BrandId)
              AND (@Status IS NULL OR COALESCE(t.status, p.status) = @Status)
              AND (@CoresOnly = FALSE OR p.source_package_id IS NULL)
            ORDER BY p.created_at DESC
            LIMIT 500
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<PackageRow>(sql, new { BrandId = brandId, Status = status, CoresOnly = coresOnly })).ToList();
    }

    public async Task<PackageRow?> GetPackageAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, p.brand_id AS BrandId, b.code AS BrandCode, b.name AS BrandName,
                   p.topic_id AS TopicId, p.title AS Title, p.angle AS Angle, p.audience AS Audience,
                   p.content_type AS ContentType, p.pillar AS Pillar, p.goal AS Goal,
                   p.priority AS Priority, COALESCE(t.status, p.status) AS Status,
                   p.source_package_id AS SourcePackageId, src.title AS SourceTitle, t.display_at AS DisplayAt,
                   (SELECT COUNT(*)::int FROM pack_content.variant v WHERE v.topic_id = p.topic_id) AS VariantCount,
                   (SELECT COUNT(*)::int FROM pack_content.content_package c WHERE c.source_package_id = p.id) AS AdaptationCount,
                   CAST(p.extra_json AS text) AS ExtraJson,
                   p.created_at AS CreatedAt, p.updated_at AS UpdatedAt
            FROM pack_content.content_package p
            INNER JOIN pack_content.brand b ON b.id = p.brand_id
            INNER JOIN pack_content.topic t ON t.id = p.topic_id
            LEFT JOIN pack_content.content_package src ON src.id = p.source_package_id
            WHERE p.id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PackageRow>(sql, new { Id = id });
    }

    public async Task<Guid> InsertPackageAsync(
        Guid brandId,
        Guid topicId,
        string title,
        string? angle,
        string? audience,
        string contentType,
        string? pillar,
        string goal,
        string priority,
        string status,
        Guid? sourcePackageId,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.content_package (
                brand_id, topic_id, title, angle, audience, content_type,
                pillar, goal, priority, status, source_package_id
            ) VALUES (
                @BrandId, @TopicId, @Title, @Angle, @Audience, @ContentType,
                @Pillar, @Goal, @Priority, @Status, @SourcePackageId
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            BrandId = brandId,
            TopicId = topicId,
            Title = title,
            Angle = angle,
            Audience = audience,
            ContentType = contentType,
            Pillar = pillar,
            Goal = goal,
            Priority = priority,
            Status = status,
            SourcePackageId = sourcePackageId,
        });
    }

    public async Task<IReadOnlyList<PackageRow>> ListPackagesBySourceAsync(Guid sourcePackageId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, p.brand_id AS BrandId, b.code AS BrandCode, b.name AS BrandName,
                   p.topic_id AS TopicId, p.title AS Title, p.angle AS Angle, p.audience AS Audience,
                   p.content_type AS ContentType, p.pillar AS Pillar, p.goal AS Goal,
                   p.priority AS Priority, COALESCE(t.status, p.status) AS Status,
                   p.source_package_id AS SourcePackageId, src.title AS SourceTitle, t.display_at AS DisplayAt,
                   (SELECT COUNT(*)::int FROM pack_content.variant v WHERE v.topic_id = p.topic_id) AS VariantCount,
                   (SELECT COUNT(*)::int FROM pack_content.content_package c WHERE c.source_package_id = p.id) AS AdaptationCount,
                   CAST(p.extra_json AS text) AS ExtraJson,
                   p.created_at AS CreatedAt, p.updated_at AS UpdatedAt
            FROM pack_content.content_package p
            INNER JOIN pack_content.brand b ON b.id = p.brand_id
            INNER JOIN pack_content.topic t ON t.id = p.topic_id
            LEFT JOIN pack_content.content_package src ON src.id = p.source_package_id
            WHERE p.source_package_id = @SourcePackageId
            ORDER BY b.sort_order, b.name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<PackageRow>(sql, new { SourcePackageId = sourcePackageId })).ToList();
    }

    public async Task<Guid?> GetPackageIdBySourceAndBrandAsync(Guid sourcePackageId, Guid brandId, CancellationToken ct)
    {
        const string sql = """
            SELECT id FROM pack_content.content_package
            WHERE source_package_id = @SourcePackageId AND brand_id = @BrandId
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid?>(sql, new { SourcePackageId = sourcePackageId, BrandId = brandId });
    }

    public async Task UpdatePackageExtraJsonAsync(Guid id, string extraJson, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.content_package
            SET extra_json = CAST(@ExtraJson AS jsonb), updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, ExtraJson = string.IsNullOrWhiteSpace(extraJson) ? "{}" : extraJson });
    }

    public async Task UpdatePackageAsync(
        Guid id,
        string title,
        string? angle,
        string? audience,
        string contentType,
        string? pillar,
        string goal,
        string priority,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.content_package SET
                title = @Title,
                angle = @Angle,
                audience = @Audience,
                content_type = @ContentType,
                pillar = @Pillar,
                goal = @Goal,
                priority = @Priority,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Title = title,
            Angle = angle,
            Audience = audience,
            ContentType = contentType,
            Pillar = pillar,
            Goal = goal,
            Priority = priority,
        });
    }

    public async Task<Guid?> GetPackageIdByTopicAsync(Guid topicId, CancellationToken ct)
    {
        const string sql = """
            SELECT id FROM pack_content.content_package WHERE topic_id = @TopicId LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid?>(sql, new { TopicId = topicId });
    }

    public async Task UpdatePackageStatusAsync(Guid id, string status, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.content_package
            SET status = @Status, updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public sealed class VideoTemplateRow
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Provider { get; set; } = "storyboard_local";
        public string? ExternalTemplateId { get; set; }
        public string AspectRatio { get; set; } = "9:16";
        public int DurationSec { get; set; } = 45;
        public string? Description { get; set; }
        public string ConfigJson { get; set; } = "{}";
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class VideoJobRow
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public Guid? PackageId { get; set; }
        public Guid? TopicId { get; set; }
        public Guid TemplateId { get; set; }
        public string TemplateCode { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string Title { get; set; } = "";
        public string ScriptBody { get; set; } = "";
        public string Status { get; set; } = "Draft";
        public string Provider { get; set; } = "storyboard_local";
        public string? ExternalRenderId { get; set; }
        public string? PreviewUrl { get; set; }
        public string? OutputUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public string StoryboardJson { get; set; } = "[]";
        public string ConfigJson { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? RenderedAt { get; set; }
    }

    public async Task<IReadOnlyList<VideoTemplateRow>> ListVideoTemplatesAsync(bool? activeOnly, CancellationToken ct)
    {
        var sql = """
            SELECT id AS Id, code AS Code, name AS Name, provider AS Provider,
                   external_template_id AS ExternalTemplateId, aspect_ratio AS AspectRatio,
                   duration_sec AS DurationSec, description AS Description,
                   config_json::text AS ConfigJson, is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.video_template
            WHERE (@ActiveOnly IS NULL OR is_active = @ActiveOnly)
            ORDER BY sort_order, name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<VideoTemplateRow>(sql, new { ActiveOnly = activeOnly })).ToList();
    }

    public async Task<VideoTemplateRow?> GetVideoTemplateAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, code AS Code, name AS Name, provider AS Provider,
                   external_template_id AS ExternalTemplateId, aspect_ratio AS AspectRatio,
                   duration_sec AS DurationSec, description AS Description,
                   config_json::text AS ConfigJson, is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.video_template
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<VideoTemplateRow>(sql, new { Id = id });
    }

    public async Task<VideoTemplateRow?> GetVideoTemplateByCodeAsync(string code, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, code AS Code, name AS Name, provider AS Provider,
                   external_template_id AS ExternalTemplateId, aspect_ratio AS AspectRatio,
                   duration_sec AS DurationSec, description AS Description,
                   config_json::text AS ConfigJson, is_active AS IsActive, sort_order AS SortOrder
            FROM pack_content.video_template
            WHERE code = @Code
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<VideoTemplateRow>(sql, new { Code = code });
    }

    public async Task SetVideoTemplateExternalIdByCodeAsync(string code, string? externalTemplateId, CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_template
            SET external_template_id = @ExternalTemplateId,
                updated_at = NOW()
            WHERE code = @Code
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Code = code,
            ExternalTemplateId = string.IsNullOrWhiteSpace(externalTemplateId) ? null : externalTemplateId.Trim(),
        });
    }

    public async Task<IReadOnlyList<VideoJobRow>> ListVideoJobsAsync(Guid? brandId, string? status, CancellationToken ct)
    {
        const string sql = """
            SELECT j.id AS Id, j.brand_id AS BrandId, b.code AS BrandCode, b.name AS BrandName,
                   j.package_id AS PackageId, j.topic_id AS TopicId, j.template_id AS TemplateId,
                   t.code AS TemplateCode, t.name AS TemplateName, j.title AS Title,
                   j.script_body AS ScriptBody, j.status AS Status, j.provider AS Provider,
                   j.external_render_id AS ExternalRenderId, j.preview_url AS PreviewUrl,
                   j.output_url AS OutputUrl, j.error_message AS ErrorMessage,
                   j.storyboard_json::text AS StoryboardJson, j.config_json::text AS ConfigJson,
                   j.created_at AS CreatedAt, j.updated_at AS UpdatedAt, j.rendered_at AS RenderedAt
            FROM pack_content.video_job j
            INNER JOIN pack_content.brand b ON b.id = j.brand_id
            INNER JOIN pack_content.video_template t ON t.id = j.template_id
            WHERE (@BrandId IS NULL OR j.brand_id = @BrandId)
              AND (@Status IS NULL OR j.status = @Status)
            ORDER BY j.created_at DESC
            LIMIT 300
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<VideoJobRow>(sql, new { BrandId = brandId, Status = status })).ToList();
    }

    public async Task<VideoJobRow?> GetVideoJobAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT j.id AS Id, j.brand_id AS BrandId, b.code AS BrandCode, b.name AS BrandName,
                   j.package_id AS PackageId, j.topic_id AS TopicId, j.template_id AS TemplateId,
                   t.code AS TemplateCode, t.name AS TemplateName, j.title AS Title,
                   j.script_body AS ScriptBody, j.status AS Status, j.provider AS Provider,
                   j.external_render_id AS ExternalRenderId, j.preview_url AS PreviewUrl,
                   j.output_url AS OutputUrl, j.error_message AS ErrorMessage,
                   j.storyboard_json::text AS StoryboardJson, j.config_json::text AS ConfigJson,
                   j.created_at AS CreatedAt, j.updated_at AS UpdatedAt, j.rendered_at AS RenderedAt
            FROM pack_content.video_job j
            INNER JOIN pack_content.brand b ON b.id = j.brand_id
            INNER JOIN pack_content.video_template t ON t.id = j.template_id
            WHERE j.id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<VideoJobRow>(sql, new { Id = id });
    }

    public async Task<Guid> InsertVideoJobAsync(
        Guid brandId,
        Guid? packageId,
        Guid? topicId,
        Guid templateId,
        string title,
        string scriptBody,
        string status,
        string provider,
        string storyboardJson,
        string configJson,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.video_job (
                brand_id, package_id, topic_id, template_id, title, script_body,
                status, provider, storyboard_json, config_json
            ) VALUES (
                @BrandId, @PackageId, @TopicId, @TemplateId, @Title, @ScriptBody,
                @Status, @Provider, @StoryboardJson::jsonb, @ConfigJson::jsonb
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            BrandId = brandId,
            PackageId = packageId,
            TopicId = topicId,
            TemplateId = templateId,
            Title = title,
            ScriptBody = scriptBody,
            Status = status,
            Provider = provider,
            StoryboardJson = storyboardJson,
            ConfigJson = configJson,
        });
    }

    public async Task UpdateVideoJobAsync(
        Guid id,
        string? scriptBody,
        string status,
        string? externalRenderId,
        string? previewUrl,
        string? outputUrl,
        string? errorMessage,
        string storyboardJson,
        string configJson,
        DateTimeOffset? renderedAt,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE pack_content.video_job SET
                script_body = COALESCE(@ScriptBody, script_body),
                status = @Status,
                external_render_id = @ExternalRenderId,
                preview_url = @PreviewUrl,
                output_url = @OutputUrl,
                error_message = @ErrorMessage,
                storyboard_json = @StoryboardJson::jsonb,
                config_json = @ConfigJson::jsonb,
                rendered_at = @RenderedAt,
                updated_at = NOW()
            WHERE id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            ScriptBody = scriptBody,
            Status = status,
            ExternalRenderId = externalRenderId,
            PreviewUrl = previewUrl,
            OutputUrl = outputUrl,
            ErrorMessage = errorMessage,
            StoryboardJson = storyboardJson,
            ConfigJson = configJson,
            RenderedAt = renderedAt,
        });
    }

    public sealed class OauthPendingRow
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public Guid BrandId { get; set; }
        public string? RedirectUri { get; set; }
        public string PagesJson { get; set; } = "[]";
        public DateTimeOffset ExpiresAt { get; set; }
    }

    public sealed class FailedPublishRow
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public string TopicTitle { get; set; } = "";
        public string ConnectorType { get; set; } = "";
        public string? LastError { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public async Task PutOauthPendingAsync(
        string id,
        string kind,
        Guid brandId,
        string? redirectUri,
        string pagesJson,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.facebook_oauth_pending
                (id, kind, brand_id, redirect_uri, pages_json, expires_at)
            VALUES (@Id, @Kind, @BrandId, @RedirectUri, @PagesJson::jsonb, @ExpiresAt)
            ON CONFLICT (id) DO UPDATE SET
                kind = EXCLUDED.kind,
                brand_id = EXCLUDED.brand_id,
                redirect_uri = EXCLUDED.redirect_uri,
                pages_json = EXCLUDED.pages_json,
                expires_at = EXCLUDED.expires_at
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(sql, new
        {
            Id = id,
            Kind = kind,
            BrandId = brandId,
            RedirectUri = redirectUri,
            PagesJson = pagesJson,
            ExpiresAt = expiresAt,
        });
    }

    public async Task<OauthPendingRow?> GetOauthPendingAsync(string id, CancellationToken ct)
    {
        const string sql = """
            SELECT id AS Id, kind AS Kind, brand_id AS BrandId, redirect_uri AS RedirectUri,
                   pages_json::text AS PagesJson, expires_at AS ExpiresAt
            FROM pack_content.facebook_oauth_pending
            WHERE id = @Id AND expires_at > NOW()
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<OauthPendingRow>(sql, new { Id = id });
    }

    public async Task DeleteOauthPendingAsync(string id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            "DELETE FROM pack_content.facebook_oauth_pending WHERE id = @Id",
            new { Id = id });
    }

    public async Task PurgeOauthPendingAsync(CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync("DELETE FROM pack_content.facebook_oauth_pending WHERE expires_at <= NOW()");
    }

    public sealed class PerformanceRow
    {
        public Guid Id { get; set; }
        public Guid PackageId { get; set; }
        public Guid TopicId { get; set; }
        public Guid BrandId { get; set; }
        public string BrandCode { get; set; } = "";
        public string BrandName { get; set; } = "";
        public string Channel { get; set; } = "";
        public DateTime MetricDate { get; set; }
        public int? Impressions { get; set; }
        public int? Views { get; set; }
        public int? Clicks { get; set; }
        public int? Engagements { get; set; }
        public int? Comments { get; set; }
        public int? Shares { get; set; }
        public string? UtmCampaign { get; set; }
        public string? UtmSource { get; set; }
        public string? UtmMedium { get; set; }
        public string? Notes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public async Task<IReadOnlyList<PerformanceRow>> ListPerformanceAsync(Guid packageId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, p.package_id AS PackageId, p.topic_id AS TopicId, p.brand_id AS BrandId,
                   b.code AS BrandCode, b.name AS BrandName, p.channel AS Channel,
                   p.metric_date AS MetricDate, p.impressions AS Impressions, p.views AS Views,
                   p.clicks AS Clicks, p.engagements AS Engagements, p.comments AS Comments,
                   p.shares AS Shares, p.utm_campaign AS UtmCampaign, p.utm_source AS UtmSource,
                   p.utm_medium AS UtmMedium, p.notes AS Notes, p.created_at AS CreatedAt
            FROM pack_content.content_performance p
            INNER JOIN pack_content.brand b ON b.id = p.brand_id
            WHERE p.package_id = @PackageId
            ORDER BY p.metric_date DESC, p.created_at DESC
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<PerformanceRow>(sql, new { PackageId = packageId })).ToList();
    }

    public async Task<PerformanceRow?> GetPerformanceAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id AS Id, p.package_id AS PackageId, p.topic_id AS TopicId, p.brand_id AS BrandId,
                   b.code AS BrandCode, b.name AS BrandName, p.channel AS Channel,
                   p.metric_date AS MetricDate, p.impressions AS Impressions, p.views AS Views,
                   p.clicks AS Clicks, p.engagements AS Engagements, p.comments AS Comments,
                   p.shares AS Shares, p.utm_campaign AS UtmCampaign, p.utm_source AS UtmSource,
                   p.utm_medium AS UtmMedium, p.notes AS Notes, p.created_at AS CreatedAt
            FROM pack_content.content_performance p
            INNER JOIN pack_content.brand b ON b.id = p.brand_id
            WHERE p.id = @Id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PerformanceRow>(sql, new { Id = id });
    }

    public async Task<Guid> InsertPerformanceAsync(
        Guid packageId,
        Guid topicId,
        Guid brandId,
        string channel,
        DateTime metricDate,
        int? impressions,
        int? views,
        int? clicks,
        int? engagements,
        int? comments,
        int? shares,
        string? utmCampaign,
        string? utmSource,
        string? utmMedium,
        string? notes,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.content_performance (
                package_id, topic_id, brand_id, channel, metric_date,
                impressions, views, clicks, engagements, comments, shares,
                utm_campaign, utm_source, utm_medium, notes
            ) VALUES (
                @PackageId, @TopicId, @BrandId, @Channel, @MetricDate,
                @Impressions, @Views, @Clicks, @Engagements, @Comments, @Shares,
                @UtmCampaign, @UtmSource, @UtmMedium, @Notes
            ) RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            PackageId = packageId,
            TopicId = topicId,
            BrandId = brandId,
            Channel = channel,
            MetricDate = metricDate.Date,
            Impressions = impressions,
            Views = views,
            Clicks = clicks,
            Engagements = engagements,
            Comments = comments,
            Shares = shares,
            UtmCampaign = utmCampaign,
            UtmSource = utmSource,
            UtmMedium = utmMedium,
            Notes = notes,
        });
    }

    public async Task<IReadOnlyList<FailedPublishRow>> ListFailedPublishJobsAsync(int take, CancellationToken ct)
    {
        const string sql = """
            SELECT j.id AS Id, j.topic_id AS TopicId, t.title AS TopicTitle,
                   j.connector_type AS ConnectorType, j.last_error AS LastError, j.updated_at AS UpdatedAt
            FROM pack_content.publish_job j
            INNER JOIN pack_content.topic t ON t.id = j.topic_id
            WHERE j.status = 'Failed'
            ORDER BY j.updated_at DESC
            LIMIT @Take
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<FailedPublishRow>(sql, new { Take = take })).ToList();
    }

    public static Dictionary<string, decimal> ParseRates(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOpts)
                   ?? new Dictionary<string, decimal>();
        }
        catch
        {
            return new Dictionary<string, decimal>();
        }
    }

    public static List<string> ParseStringList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, JsonOpts);

    public async Task<SeriesPilotRow?> GetSeriesPilotAsync(string seriesCode, CancellationToken ct)
    {
        const string sql = """
            SELECT
                series_code AS SeriesCode,
                graph_json::text AS GraphJson,
                updated_at AS UpdatedAt
            FROM pack_content.series_pilot
            WHERE series_code = @SeriesCode
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SeriesPilotRow>(sql, new { SeriesCode = seriesCode });
    }

    public async Task<SeriesPilotRow> UpsertSeriesPilotAsync(string seriesCode, string graphJson, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.series_pilot (series_code, graph_json, updated_at)
            VALUES (@SeriesCode, CAST(@GraphJson AS jsonb), NOW())
            ON CONFLICT (series_code) DO UPDATE SET
                graph_json = CAST(@GraphJson AS jsonb),
                updated_at = NOW()
            RETURNING
                series_code AS SeriesCode,
                graph_json::text AS GraphJson,
                updated_at AS UpdatedAt
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<SeriesPilotRow>(sql, new
        {
            SeriesCode = seriesCode,
            GraphJson = string.IsNullOrWhiteSpace(graphJson) ? "{}" : graphJson,
        });
        return row;
    }
}
