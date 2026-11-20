using System.Text.Json.Nodes;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentTelegramBrandRow
{
    public Guid BrandId { get; set; }
    public bool Enabled { get; set; }
    public string? ChatId { get; set; }
    public bool NotifySuccess { get; set; } = true;
    public bool NotifyLowStock { get; set; } = true;
    public bool NotifyFailure { get; set; } = true;
    public bool NotifyProposal { get; set; } = true;
    public int LowStockRemaining { get; set; } = ContentTelegramDefaults.LowStockRemaining;
    public int LowStockEmptyDays { get; set; } = ContentTelegramDefaults.LowStockEmptyDays;
    public int QuietFromHour { get; set; } = ContentTelegramDefaults.QuietFromHour;
    public int QuietToHour { get; set; } = ContentTelegramDefaults.QuietToHour;

    /// <summary>Brand xếp được tin: bật và có chat_id.</summary>
    public bool CanQueue => Enabled && !string.IsNullOrWhiteSpace(ChatId);
}

internal sealed class ContentTelegramOutboxRow
{
    public Guid Id { get; set; }
    public Guid? BrandId { get; set; }
    public string Kind { get; set; } = "";
    public string DedupeKey { get; set; } = "";
    public string? ErrorClass { get; set; }
    public string ChatId { get; set; } = "";
    public string Html { get; set; } = "";
    public string Status { get; set; } = ContentTelegramDefaults.StatusPending;
    public int Attempts { get; set; }
    public bool Digest { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? QuietFromHour { get; set; }
    public int? QuietToHour { get; set; }
}

internal sealed class ContentTelegramPublishRow
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public Guid TopicId { get; set; }
    public string ConnectorType { get; set; } = "";
    public string Status { get; set; } = "";
    public string? LastError { get; set; }
    public string? ResultJson { get; set; }
    public DateTimeOffset? PublishAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? BrandName { get; set; }
    public string? TopicTitle { get; set; }
    public string? TargetName { get; set; }
}

internal sealed class ContentTelegramDraftRow
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public Guid TopicId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? BrandName { get; set; }
    public string? Title { get; set; }
}

internal sealed class ContentTelegramProposalRow
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public Guid TopicId { get; set; }
    public string? Title { get; set; }
    public string? TargetLabel { get; set; }
    public DateTimeOffset PublishAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? BrandName { get; set; }
}

internal sealed class ContentTelegramLowStockRow
{
    public Guid BrandId { get; set; }
    public string? BrandName { get; set; }
    public Guid TargetId { get; set; }
    public string? TargetName { get; set; }
    public string? Connector { get; set; }
    public int Remaining { get; set; }
    public DateTimeOffset? NextAt { get; set; }
}

internal sealed record ContentTelegramBotConfig(string SecretRef, string? StoredSecret);

/// <summary>
/// Dapper cho bảng Telegram của Content (migration 443). Chỉ đọc publish_job / autopilot_run / schedule_proposal,
/// không ghi vào các bảng đó. Cột <c>org_settings.telegram_config_json</c> được ghi riêng, không qua UpdateOrgSettingsAsync.
/// </summary>
internal sealed class ContentTelegramStore
{
    public const string CursorPublish = "publish_job";
    public const string CursorDraft = "autopilot_run";
    public const string CursorProposal = "schedule_proposal";

    private const string OrgId = "a0000000-0000-7000-8000-000000000001";
    private const string StoredSecretKey = "storedSecret";
    private const string SecretRefKey = "botTokenSecretRef";

    private readonly IDbConnectionFactory _db;

    public ContentTelegramStore(IDbConnectionFactory db) => _db = db;

    // ---- bot config (secret) ----

    public async Task<ContentTelegramBotConfig> GetBotConfigAsync(CancellationToken ct)
    {
        const string sql = $"SELECT telegram_config_json::text FROM pack_content.org_settings WHERE id = '{OrgId}'::uuid";
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var json = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, cancellationToken: ct));
        return ParseBot(json);
    }

    /// <param name="botToken">null = giữ; "" = xóa; có chữ = thay (đã kiểm dạng ở service).</param>
    /// <param name="secretRef">null = giữ; "" = về mặc định.</param>
    public async Task SaveBotConfigAsync(string? botToken, string? secretRef, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var json = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT telegram_config_json::text FROM pack_content.org_settings WHERE id = '{OrgId}'::uuid FOR UPDATE",
            transaction: tx, cancellationToken: ct));
        if (json is null)
            throw new InvalidOperationException("Chưa có cấu hình org Content (org_settings). Chạy migration 281.");

        var node = ParseObject(json);
        if (botToken is not null)
        {
            if (string.IsNullOrWhiteSpace(botToken)) node.Remove(StoredSecretKey);
            else node[StoredSecretKey] = botToken.Trim();
        }
        if (secretRef is not null)
        {
            if (string.IsNullOrWhiteSpace(secretRef)) node.Remove(SecretRefKey);
            else node[SecretRefKey] = secretRef.Trim();
        }

        await conn.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE pack_content.org_settings
            SET telegram_config_json = @Json::jsonb, updated_at = NOW()
            WHERE id = '{OrgId}'::uuid
            """,
            new { Json = node.ToJsonString() }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    private static ContentTelegramBotConfig ParseBot(string? json)
    {
        var node = ParseObject(json);
        string? Str(string key) =>
            node.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)
                ? s.Trim()
                : null;
        return new ContentTelegramBotConfig(Str(SecretRefKey) ?? ContentTelegramDefaults.SecretRef, Str(StoredSecretKey));
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }

    // ---- brand settings ----

    private const string BrandCols = """
        brand_id AS BrandId, enabled AS Enabled, chat_id AS ChatId,
        notify_success AS NotifySuccess, notify_low_stock AS NotifyLowStock,
        notify_failure AS NotifyFailure, notify_proposal AS NotifyProposal,
        low_stock_remaining AS LowStockRemaining, low_stock_empty_days AS LowStockEmptyDays,
        quiet_from_hour AS QuietFromHour, quiet_to_hour AS QuietToHour
        """;

    public async Task<ContentTelegramBrandRow?> GetBrandAsync(Guid brandId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ContentTelegramBrandRow>(new CommandDefinition(
            $"SELECT {BrandCols} FROM pack_content.telegram_brand_setting WHERE brand_id = @BrandId",
            new { BrandId = brandId }, cancellationToken: ct));
    }

    public async Task<Dictionary<Guid, ContentTelegramBrandRow>> ListActiveBrandsAsync(CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramBrandRow>(new CommandDefinition(
            $"SELECT {BrandCols} FROM pack_content.telegram_brand_setting WHERE enabled AND COALESCE(chat_id, '') <> ''",
            cancellationToken: ct));
        return rows.ToDictionary(r => r.BrandId);
    }

    public async Task UpsertBrandAsync(ContentTelegramBrandRow row, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.telegram_brand_setting (
                brand_id, enabled, chat_id, notify_success, notify_low_stock, notify_failure, notify_proposal,
                low_stock_remaining, low_stock_empty_days, quiet_from_hour, quiet_to_hour, updated_at)
            VALUES (
                @BrandId, @Enabled, @ChatId, @NotifySuccess, @NotifyLowStock, @NotifyFailure, @NotifyProposal,
                @LowStockRemaining, @LowStockEmptyDays, @QuietFromHour, @QuietToHour, NOW())
            ON CONFLICT (brand_id) DO UPDATE SET
                enabled = EXCLUDED.enabled,
                chat_id = EXCLUDED.chat_id,
                notify_success = EXCLUDED.notify_success,
                notify_low_stock = EXCLUDED.notify_low_stock,
                notify_failure = EXCLUDED.notify_failure,
                notify_proposal = EXCLUDED.notify_proposal,
                low_stock_remaining = EXCLUDED.low_stock_remaining,
                low_stock_empty_days = EXCLUDED.low_stock_empty_days,
                quiet_from_hour = EXCLUDED.quiet_from_hour,
                quiet_to_hour = EXCLUDED.quiet_to_hour,
                updated_at = NOW()
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, row, cancellationToken: ct));
    }

    public async Task<string?> GetBrandNameAsync(Guid brandId, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name FROM pack_content.brand WHERE id = @Id", new { Id = brandId }, cancellationToken: ct));
    }

    // ---- outbox ----

    /// <returns>true nếu hàng mới được thêm (không trùng dedupe_key).</returns>
    public async Task<bool> InsertAsync(
        Guid? brandId, string kind, string dedupeKey, string? errorClass, string chatId, string html, string status,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO pack_content.telegram_outbox (brand_id, kind, dedupe_key, error_class, chat_id, html, status)
            VALUES (@BrandId, @Kind, @DedupeKey, @ErrorClass, @ChatId, @Html, @Status)
            ON CONFLICT (dedupe_key) DO NOTHING
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            BrandId = brandId,
            Kind = kind,
            DedupeKey = dedupeKey,
            ErrorClass = errorClass,
            ChatId = chatId,
            Html = html,
            Status = status,
        }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> ExistsAsync(string dedupeKey, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM pack_content.telegram_outbox WHERE dedupe_key = @Key)",
            new { Key = dedupeKey }, cancellationToken: ct));
    }

    /// <summary>
    /// Cùng lớp lỗi đã gửi (hoặc đang chờ gửi) trong cửa sổ. Tính cả Pending để hai lỗi trong cùng một lượt quét
    /// không cùng lọt qua.
    /// </summary>
    public async Task<bool> ClassSentSinceAsync(Guid brandId, string errorClass, TimeSpan window, CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM pack_content.telegram_outbox
                WHERE brand_id = @BrandId AND error_class = @Class
                  AND ((status = 'Sent' AND sent_at > NOW() - @Window)
                    OR (status = 'Pending' AND created_at > NOW() - @Window)))
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { BrandId = brandId, Class = errorClass, Window = window }, cancellationToken: ct));
    }

    private const string ClaimReturning = """
        RETURNING o.id, o.brand_id, o.kind, o.dedupe_key, o.error_class, o.chat_id, o.html,
                  o.status, o.attempts, o.digest, o.created_at
        """;

    private const string ClaimSelect = """
        SELECT c.id AS Id, c.brand_id AS BrandId, c.kind AS Kind, c.dedupe_key AS DedupeKey,
               c.error_class AS ErrorClass, c.chat_id AS ChatId, c.html AS Html, c.status AS Status,
               c.attempts AS Attempts, c.digest AS Digest, c.created_at AS CreatedAt,
               s.quiet_from_hour AS QuietFromHour, s.quiet_to_hour AS QuietToHour
        FROM claimed c
        LEFT JOIN pack_content.telegram_brand_setting s ON s.brand_id = c.brand_id
        ORDER BY c.created_at
        """;

    /// <summary>
    /// Lấy tối đa <paramref name="limit"/> hàng Pending đến hạn. Tăng attempts và đẩy next_attempt_at +1 phút
    /// ngay trong lệnh claim: hai vòng không gửi trùng; process chết giữa chừng thì hàng tự đến hạn lại.
    /// </summary>
    public async Task<IReadOnlyList<ContentTelegramOutboxRow>> ClaimDueAsync(int limit, CancellationToken ct)
    {
        var sql = $"""
            WITH claimed AS (
                UPDATE pack_content.telegram_outbox o
                SET attempts = o.attempts + 1, next_attempt_at = NOW() + INTERVAL '1 minute'
                WHERE o.id IN (
                    SELECT id FROM pack_content.telegram_outbox
                    WHERE status = 'Pending' AND next_attempt_at <= NOW()
                    ORDER BY next_attempt_at
                    LIMIT @Limit
                    FOR UPDATE SKIP LOCKED)
                {ClaimReturning})
            {ClaimSelect}
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramOutboxRow>(new CommandDefinition(sql, new { Limit = limit }, cancellationToken: ct));
        return rows.ToList();
    }

    /// <summary>Lấy thêm các tin tóm tắt (digest) đến hạn của cùng chat để gom thành một tin.</summary>
    public async Task<IReadOnlyList<ContentTelegramOutboxRow>> ClaimDigestForChatAsync(string chatId, int limit, CancellationToken ct)
    {
        var sql = $"""
            WITH claimed AS (
                UPDATE pack_content.telegram_outbox o
                SET attempts = o.attempts + 1, next_attempt_at = NOW() + INTERVAL '1 minute'
                WHERE o.id IN (
                    SELECT id FROM pack_content.telegram_outbox
                    WHERE status = 'Pending' AND digest AND chat_id = @ChatId AND next_attempt_at <= NOW()
                    ORDER BY created_at
                    LIMIT @Limit
                    FOR UPDATE SKIP LOCKED)
                {ClaimReturning})
            {ClaimSelect}
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramOutboxRow>(new CommandDefinition(
            sql, new { ChatId = chatId, Limit = limit }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task MarkSentAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE pack_content.telegram_outbox SET status = 'Sent', sent_at = NOW(), last_error = NULL WHERE id = ANY(@Ids)",
            new { Ids = ids.ToArray() }, cancellationToken: ct));
    }

    public async Task MarkFailedAsync(IReadOnlyCollection<Guid> ids, string? error, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE pack_content.telegram_outbox SET status = 'Failed', last_error = @Error WHERE id = ANY(@Ids)",
            new { Ids = ids.ToArray(), Error = Cut(error) }, cancellationToken: ct));
    }

    /// <param name="refundAttempt">true: lần vừa rồi không tính (429, hoãn vì giờ im lặng).</param>
    public async Task MarkRetryAsync(
        IReadOnlyCollection<Guid> ids, DateTimeOffset nextAt, string? error, bool refundAttempt, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE pack_content.telegram_outbox
            SET next_attempt_at = @NextAt, last_error = @Error,
                attempts = CASE WHEN @Refund THEN GREATEST(attempts - 1, 0) ELSE attempts END
            WHERE id = ANY(@Ids)
            """,
            new { Ids = ids.ToArray(), NextAt = nextAt.ToUniversalTime(), Error = Cut(error), Refund = refundAttempt },
            cancellationToken: ct));
    }

    /// <summary>Tin không phải lỗi rơi vào giờ im lặng: đánh dấu digest, hoãn tới lúc hết giờ im, không tính lần thử.</summary>
    public async Task MarkDigestAsync(Guid id, DateTimeOffset nextAt, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE pack_content.telegram_outbox
            SET digest = TRUE, next_attempt_at = @NextAt, attempts = GREATEST(attempts - 1, 0)
            WHERE id = @Id
            """,
            new { Id = id, NextAt = nextAt.ToUniversalTime() }, cancellationToken: ct));
    }

    private static string? Cut(string? s) =>
        s is null ? null : ContentTelegramText.Cut(ContentTelegramText.Redact(s), 500);

    // ---- cursor ----

    public async Task<DateTimeOffset> GetCursorAsync(string name, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var v = await conn.ExecuteScalarAsync<object?>(new CommandDefinition(
            "SELECT watermark FROM pack_content.telegram_cursor WHERE name = @Name", new { Name = name }, cancellationToken: ct));
        if (v is not null) return AsOffset(v);
        // Thiếu hàng (ai đó xóa tay): đặt mốc bây giờ để không bắn lịch sử.
        var inserted = await conn.ExecuteScalarAsync<object>(new CommandDefinition(
            """
            INSERT INTO pack_content.telegram_cursor (name, watermark) VALUES (@Name, NOW())
            ON CONFLICT (name) DO UPDATE SET watermark = pack_content.telegram_cursor.watermark
            RETURNING watermark
            """, new { Name = name }, cancellationToken: ct));
        return AsOffset(inserted);
    }

    /// <summary>Npgsql trả timestamptz dạng DateTime cho ExecuteScalar. Convert.ChangeType không đổi được sang DateTimeOffset.</summary>
    private static DateTimeOffset AsOffset(object value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt when dt.Kind == DateTimeKind.Local => new DateTimeOffset(dt),
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        _ => throw new InvalidCastException($"watermark kiểu {value.GetType().FullName}"),
    };

    public async Task SetCursorAsync(string name, DateTimeOffset watermark, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO pack_content.telegram_cursor (name, watermark) VALUES (@Name, @Watermark)
            ON CONFLICT (name) DO UPDATE SET watermark = GREATEST(pack_content.telegram_cursor.watermark, EXCLUDED.watermark)
            """, new { Name = name, Watermark = watermark.ToUniversalTime() }, cancellationToken: ct));
    }

    // ---- discover (chỉ đọc) ----

    public async Task<IReadOnlyList<ContentTelegramPublishRow>> ListPublishAfterAsync(DateTimeOffset watermark, CancellationToken ct)
    {
        const string sql = """
            SELECT pj.id AS Id, pj.brand_id AS BrandId, pj.topic_id AS TopicId, pj.connector_type AS ConnectorType,
                   pj.status AS Status, pj.last_error AS LastError, pj.result_json::text AS ResultJson,
                   pj.publish_at AS PublishAt, pj.updated_at AS UpdatedAt,
                   b.name AS BrandName, t.title AS TopicTitle,
                   COALESCE(ch.name, st.name, pj.connector_type) AS TargetName
            FROM pack_content.publish_job pj
            JOIN pack_content.brand b ON b.id = pj.brand_id
            JOIN pack_content.topic t ON t.id = pj.topic_id
            LEFT JOIN pack_content.channel_target ch ON ch.id = pj.channel_target_id
            LEFT JOIN pack_content.site_target st ON st.id = pj.site_target_id
            WHERE pj.updated_at > @Watermark
              AND pj.status IN ('Succeeded', 'Failed')
              AND pj.connector_type <> 'manual'
            ORDER BY pj.updated_at
            LIMIT 100
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramPublishRow>(new CommandDefinition(
            sql, new { Watermark = watermark.ToUniversalTime() }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ContentTelegramDraftRow>> ListDraftsAfterAsync(DateTimeOffset watermark, CancellationToken ct)
    {
        // Chờ 1 phút sau khi ghi nhật ký để topic kịp chuyển Review.
        const string sql = """
            SELECT r.id AS Id, r.brand_id AS BrandId, r.topic_id AS TopicId, r.created_at AS CreatedAt,
                   b.name AS BrandName, t.title AS Title
            FROM pack_content.autopilot_run r
            JOIN pack_content.brand b ON b.id = r.brand_id
            JOIN pack_content.topic t ON t.id = r.topic_id
            WHERE r.created_at > @Watermark
              AND r.created_at < NOW() - INTERVAL '1 minute'
              AND r.topic_id IS NOT NULL
              AND t.status IN ('Draft', 'Review')
            ORDER BY r.created_at
            LIMIT 50
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramDraftRow>(new CommandDefinition(
            sql, new { Watermark = watermark.ToUniversalTime() }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ContentTelegramProposalRow>> ListProposalsAfterAsync(DateTimeOffset watermark, CancellationToken ct)
    {
        const string sql = """
            SELECT sp.id AS Id, sp.brand_id AS BrandId, sp.topic_id AS TopicId, sp.title AS Title,
                   sp.target_label AS TargetLabel, sp.publish_at AS PublishAt, sp.created_at AS CreatedAt,
                   b.name AS BrandName
            FROM pack_content.schedule_proposal sp
            JOIN pack_content.brand b ON b.id = sp.brand_id
            WHERE sp.created_at > @Watermark
              AND sp.created_at < NOW() - INTERVAL '2 minutes'
              AND sp.status = 'Proposed'
              AND sp.created_by = 'Autopilot'
            ORDER BY sp.created_at
            LIMIT 50
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramProposalRow>(new CommandDefinition(
            sql, new { Watermark = watermark.ToUniversalTime() }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ContentTelegramLowStockRow>> ListLowStockCandidatesAsync(CancellationToken ct)
    {
        const string sql = """
            WITH dest AS (
              SELECT b.id AS brand_id, b.name AS brand_name,
                     c.id AS target_id, c.name AS target_name, c.channel_type AS connector
              FROM pack_content.brand b
              JOIN pack_content.channel_target c ON c.brand_id = b.id AND c.is_active
              WHERE b.is_active
                AND EXISTS (
                  SELECT 1 FROM pack_content.publish_job pj
                  WHERE pj.channel_target_id = c.id AND pj.created_at > NOW() - INTERVAL '60 days')
              UNION ALL
              SELECT b.id, b.name, s.id, s.name, s.connector_type
              FROM pack_content.brand b
              JOIN pack_content.site_target s ON s.brand_id = b.id AND s.is_active
              WHERE b.is_active
                AND EXISTS (
                  SELECT 1 FROM pack_content.publish_job pj
                  WHERE pj.site_target_id = s.id AND pj.created_at > NOW() - INTERVAL '60 days')
            ),
            queued AS (
              SELECT COALESCE(channel_target_id, site_target_id) AS target_id,
                     COUNT(*) FILTER (WHERE publish_at > NOW()) AS remaining,
                     MIN(publish_at) FILTER (WHERE publish_at > NOW()) AS next_at
              FROM pack_content.publish_job
              WHERE status = 'Queued'
              GROUP BY 1
            )
            SELECT d.brand_id AS BrandId, d.brand_name AS BrandName, d.target_id AS TargetId,
                   d.target_name AS TargetName, d.connector AS Connector,
                   COALESCE(q.remaining, 0)::int AS Remaining, q.next_at AS NextAt
            FROM dest d
            LEFT JOIN queued q ON q.target_id = d.target_id
            WHERE d.connector <> 'manual'
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ContentTelegramLowStockRow>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }
}
