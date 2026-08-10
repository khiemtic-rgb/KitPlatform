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
                sort_order AS SortOrder, updated_at AS UpdatedAt
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
                sort_order AS SortOrder, updated_at AS UpdatedAt
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
                monthly_ceiling_usd, image_tier, pause_when_exceeded, is_active, sort_order
            ) VALUES (
                @Code, @Name, @DefaultCtaUrl, @DefaultCtaLabel,
                @MonthlyCeilingUsd, @ImageTier, @PauseWhenExceeded, @IsActive, @SortOrder
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
                t.body_outline AS BodyOutline,
                t.created_at AS CreatedAt, t.updated_at AS UpdatedAt
            FROM pack_content.topic t
            INNER JOIN pack_content.brand b ON b.id = t.brand_id
            WHERE (@BrandId IS NULL OR t.brand_id = @BrandId)
              AND (@Status IS NULL OR t.status = @Status)
            ORDER BY t.priority, t.updated_at DESC
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
                t.body_outline AS BodyOutline,
                t.created_at AS CreatedAt, t.updated_at AS UpdatedAt
            FROM pack_content.topic t
            INNER JOIN pack_content.brand b ON b.id = t.brand_id
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
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.topic (
                brand_id, title, pillar, goal, cta_url, utm_campaign, priority, status, body_outline
            ) VALUES (
                @BrandId, @Title, @Pillar, @Goal, @CtaUrl, @Utm, @Priority, @Status, @Outline
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
        });
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
}
