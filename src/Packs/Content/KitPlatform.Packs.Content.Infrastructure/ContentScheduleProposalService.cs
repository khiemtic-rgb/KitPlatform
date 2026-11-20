using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentScheduleProposalService : IContentScheduleProposalService
{
    /// <summary>Lượt đăng kèm kết quả thật của job đăng (trạng thái, lỗi, link bài).</summary>
    private const string SelectSql = """
        SELECT sp.id AS "Id", sp.brand_id AS "BrandId", sp.topic_id AS "TopicId", sp.title AS "Title",
               sp.target_key AS "TargetKey", sp.target_label AS "TargetLabel", sp.connector_type AS "ConnectorType",
               sp.site_id AS "SiteId", sp.channel_id AS "ChannelId", sp.publish_at AS "PublishAt",
               sp.status AS "Status", sp.reason AS "Reason", sp.publish_job_id AS "PublishJobId",
               sp.decided_by AS "DecidedBy", sp.decided_at AS "DecidedAt", sp.last_error AS "LastError",
               sp.created_at AS "CreatedAt",
               pj.status AS "JobStatus", pj.last_error AS "JobError",
               COALESCE(pj.result_json->>'url', pj.result_json->>'link') AS "JobUrl",
               pj.updated_at AS "JobUpdatedAt",
               CASE WHEN sp.connector_type = 'tiktok_channel' THEN COALESCE(
                   NULLIF(pj.result_json->>'caption', ''),
                   (SELECT v.body_markdown FROM pack_content.variant v
                    WHERE v.topic_id = sp.topic_id AND v.kind IN ('video_caption', 'tiktok')
                    ORDER BY v.updated_at DESC LIMIT 1)) END AS "JobCaption"
        FROM pack_content.schedule_proposal sp
        LEFT JOIN pack_content.publish_job pj ON pj.id = sp.publish_job_id
        """;

    private readonly IDbConnectionFactory _db;
    private readonly IContentScheduleAdvisor _advisor;
    private readonly IContentPublishService _publish;
    private readonly ContentRepository _repo;
    private readonly IContentPublishPlanStore _plans;

    public ContentScheduleProposalService(
        IDbConnectionFactory db,
        IContentScheduleAdvisor advisor,
        IContentPublishService publish,
        ContentRepository repo,
        IContentPublishPlanStore plans)
    {
        _db = db;
        _advisor = advisor;
        _publish = publish;
        _repo = repo;
        _plans = plans;
    }

    public async Task<ContentScheduleTopicsResultDto> ScheduleTopicsAsync(
        ContentScheduleTopicsRequest request, string actor, CancellationToken ct = default)
    {
        var inputs = (request.Topics ?? new List<ContentTopicPublishPlanInput>())
            .Select(ContentPublishPlanRules.Validate)
            .GroupBy(x => x.TopicId)
            .Select(g => g.Last())
            .ToList();
        if (inputs.Count == 0)
            throw new InvalidOperationException("Chưa có bài nào để xếp lịch.");

        var key = (request.Brand ?? "").Trim();
        var brands = await _repo.ListBrandsAsync(true, ct);
        var b = Guid.TryParse(key, out var gid)
            ? brands.FirstOrDefault(x => x.Id == gid)
            : brands.FirstOrDefault(x =>
                string.Equals(x.Code, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name, key, StringComparison.OrdinalIgnoreCase));
        if (b is null)
            throw new InvalidOperationException($"Không tìm thấy thương hiệu «{key}» đang hoạt động.");
        foreach (var input in inputs)
        {
            var topic = await _repo.GetTopicAsync(input.TopicId, ct)
                        ?? throw new InvalidOperationException("Không tìm thấy bài cần xếp lịch.");
            if (topic.BrandId != b.Id)
                throw new InvalidOperationException($"Bài «{topic.Title}» không thuộc thương hiệu {b.Name}.");
        }

        await _plans.SavePlansAsync(inputs, ct);
        var ids = inputs.Select(x => x.TopicId).ToList();
        var plan = await _advisor.PlanForTopicsAsync(b.Code, ids, ct);

        // Đề xuất chưa duyệt cũ của chính các bài này (nếu có) nhường chỗ cho lịch mới.
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_content.schedule_proposal
                SET status = 'Cancelled', last_error = 'Thay bằng lịch từ Video Studio', updated_at = NOW()
                WHERE topic_id = ANY(@Ids) AND status = 'Proposed'
                """,
                new { Ids = ids.ToArray() });
        }

        var scheduled = new List<ContentScheduleProposalRecordDto>();
        var warnings = plan.Warnings
            .Where(w => !w.StartsWith("Đây chỉ là đề xuất", StringComparison.Ordinal))
            .ToList();
        foreach (var p in plan.Proposals)
        {
            Guid? id;
            await using (var conn = await _db.CreateOpenConnectionAsync(ct))
            {
                id = await conn.ExecuteScalarAsync<Guid?>(
                    """
                    INSERT INTO pack_content.schedule_proposal (
                        brand_id, topic_id, title, target_key, target_label, connector_type,
                        site_id, channel_id, publish_at, reason, created_by)
                    VALUES (
                        @BrandId, @TopicId, @Title, @TargetKey, @TargetLabel, @ConnectorType,
                        @SiteId, @ChannelId, @PublishAt, @Reason, @Actor)
                    ON CONFLICT DO NOTHING
                    RETURNING id
                    """,
                    new
                    {
                        BrandId = b.Id,
                        p.TopicId,
                        Title = Trunc(p.Title, 500),
                        p.TargetKey,
                        TargetLabel = Trunc(p.TargetLabel, 200),
                        p.ConnectorType,
                        p.SiteId,
                        p.ChannelId,
                        PublishAt = p.ProposedAtUtc,
                        p.Reason,
                        Actor = Trunc(actor, 200),
                    });
            }

            if (id is not Guid proposalId)
            {
                warnings.Add($"«{p.Title}» đã có lịch trên {p.TargetLabel}; giữ lịch cũ.");
                continue;
            }

            try
            {
                scheduled.Add(await ApproveAsync(proposalId, null, actor, ct));
            }
            catch (InvalidOperationException ex)
            {
                warnings.Add($"«{p.Title}» trên {p.TargetLabel}: {ex.Message}");
            }
        }

        return new ContentScheduleTopicsResultDto(scheduled, plan.Skipped, warnings);
    }

    public async Task<ContentScheduleGenerateResultDto> GenerateAsync(
        string brand, int days, int maxPerDay, string actor, CancellationToken ct = default)
    {
        var plan = await _advisor.PlanAsync(brand, days, maxPerDay, ct);
        var brands = await _repo.ListBrandsAsync(true, ct);
        var b = brands.First(x => string.Equals(x.Code, plan.BrandCode, StringComparison.OrdinalIgnoreCase));

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var superseded = await conn.ExecuteAsync(
            """
            UPDATE pack_content.schedule_proposal
            SET status = 'Cancelled', last_error = 'Thay bằng đề xuất mới', updated_at = NOW()
            WHERE brand_id = @BrandId AND status = 'Proposed'
            """,
            new { BrandId = b.Id }, tx);

        var ids = new List<Guid>();
        foreach (var p in plan.Proposals)
        {
            var id = await conn.ExecuteScalarAsync<Guid?>(
                """
                INSERT INTO pack_content.schedule_proposal (
                    brand_id, topic_id, title, target_key, target_label, connector_type,
                    site_id, channel_id, publish_at, reason, created_by)
                VALUES (
                    @BrandId, @TopicId, @Title, @TargetKey, @TargetLabel, @ConnectorType,
                    @SiteId, @ChannelId, @PublishAt, @Reason, @Actor)
                ON CONFLICT DO NOTHING
                RETURNING id
                """,
                new
                {
                    BrandId = b.Id,
                    p.TopicId,
                    Title = Trunc(p.Title, 500),
                    p.TargetKey,
                    TargetLabel = Trunc(p.TargetLabel, 200),
                    p.ConnectorType,
                    p.SiteId,
                    p.ChannelId,
                    PublishAt = p.ProposedAtUtc,
                    p.Reason,
                    Actor = Trunc(actor, 200),
                }, tx);
            if (id is Guid g) ids.Add(g);
        }

        await tx.CommitAsync(ct);

        var rows = ids.Count == 0
            ? new List<ContentScheduleProposalRecordDto>()
            : (await conn.QueryAsync<Row>(
                $"{SelectSql} WHERE sp.id = ANY(@Ids) ORDER BY sp.publish_at",
                new { Ids = ids.ToArray() })).Select(r => r.ToDto()).ToList();

        var warnings = plan.Warnings.Where(w => !w.StartsWith("Đây chỉ là đề xuất", StringComparison.Ordinal)).ToList();
        warnings.Insert(0, "Đề xuất đã được lưu. Chưa có gì được đăng cho đến khi bạn duyệt từng mục.");
        return new ContentScheduleGenerateResultDto(plan.BrandCode, rows.Count, superseded, rows, plan.Skipped, warnings);
    }

    public async Task<IReadOnlyList<ContentScheduleProposalRecordDto>> ListAsync(
        string? brand, string? status, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<Row>(
            $"""
            {SelectSql}
            JOIN pack_content.brand b ON b.id = sp.brand_id
            WHERE (@Brand::text IS NULL OR sp.brand_id::text = @Brand OR lower(b.code) = lower(@Brand) OR lower(b.name) = lower(@Brand))
              AND (@Status::text IS NULL OR sp.status = @Status)
            ORDER BY sp.publish_at, sp.created_at
            LIMIT 200
            """,
            new
            {
                Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
                Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            });
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<ContentScheduleProposalRecordDto> ApproveAsync(
        Guid id, DateTimeOffset? publishAt, string actor, CancellationToken ct = default)
    {
        var row = await GetAsync(id, ct) ?? throw new InvalidOperationException("Không tìm thấy đề xuất.");
        var at = publishAt ?? row.PublishAt;
        var problem = ContentScheduleApprovalRules.CheckApprove(row.Status, at, DateTimeOffset.UtcNow);
        if (problem is not null) throw new InvalidOperationException(problem);

        // Claim first so two clicks cannot create two jobs.
        await using (var conn = await _db.CreateOpenConnectionAsync(ct))
        {
            var claimed = await conn.ExecuteAsync(
                """
                UPDATE pack_content.schedule_proposal
                SET status = 'Approved', publish_at = @At, decided_by = @Actor, decided_at = NOW(),
                    last_error = NULL, updated_at = NOW()
                WHERE id = @Id AND status = 'Proposed'
                """,
                new { Id = id, At = at, Actor = Trunc(actor, 200) });
            if (claimed == 0)
                throw new InvalidOperationException("Đề xuất vừa được xử lý ở nơi khác.");
        }

        // Fanpage chưa có API Meta: giữ giờ đã duyệt làm lượt đăng tay, không tạo job (job sẽ lỗi vì không có token).
        if (await IsManualAsync(row, ct))
            return (await GetAsync(id, ct))!.ToDto();

        try
        {
            var request = new PublishContentRequest
            {
                SiteTargetIds = row.SiteId is Guid s ? [s] : [],
                ChannelTargetIds = row.ChannelId is Guid c ? [c] : [],
                IncludeManualExport = false,
                RunImmediately = false,
                PublishAt = at,
            };
            var result = await _publish.PublishAsync(row.TopicId, request, ct);
            var job = result.Jobs.FirstOrDefault()
                      ?? throw new InvalidOperationException("Không tạo được job đăng.");
            await using var conn = await _db.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(
                "UPDATE pack_content.schedule_proposal SET publish_job_id = @Job, updated_at = NOW() WHERE id = @Id",
                new { Id = id, Job = job.Id });
        }
        catch (Exception ex)
        {
            await using var conn = await _db.CreateOpenConnectionAsync(CancellationToken.None);
            await conn.ExecuteAsync(
                "UPDATE pack_content.schedule_proposal SET status = 'Failed', last_error = @Err, updated_at = NOW() WHERE id = @Id",
                new { Id = id, Err = Trunc(ex.Message, 1000) });
            throw;
        }

        return (await GetAsync(id, ct))!.ToDto();
    }

    public async Task<ContentScheduleProposalRecordDto> RejectAsync(
        Guid id, string? note, string actor, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_content.schedule_proposal
            SET status = 'Rejected', decided_by = @Actor, decided_at = NOW(), last_error = @Note, updated_at = NOW()
            WHERE id = @Id AND status = 'Proposed'
            """,
            new { Id = id, Actor = Trunc(actor, 200), Note = Trunc(note, 500) });
        if (n == 0) throw new InvalidOperationException("Chỉ từ chối được đề xuất đang ở trạng thái Proposed.");
        return (await GetAsync(id, ct))!.ToDto();
    }

    public async Task<ContentScheduleProposalRecordDto> CancelAsync(
        Guid id, string? note, string actor, CancellationToken ct = default)
    {
        var row = await GetAsync(id, ct) ?? throw new InvalidOperationException("Không tìm thấy đề xuất.");
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var jobStatus = row.PublishJobId is Guid jid
            ? await conn.ExecuteScalarAsync<string?>("SELECT status FROM pack_content.publish_job WHERE id = @Id", new { Id = jid })
            : null;
        var manual = row.PublishJobId is null && string.Equals(row.Status, "Approved", StringComparison.OrdinalIgnoreCase);
        var problem = ContentScheduleApprovalRules.CheckCancel(row.Status, jobStatus, manual);
        if (problem is not null) throw new InvalidOperationException(problem);

        if (manual)
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_content.schedule_proposal
                SET status = 'Cancelled', decided_by = @Actor, decided_at = NOW(), last_error = @Note, updated_at = NOW()
                WHERE id = @Id AND status = 'Approved'
                """,
                new { Id = id, Actor = Trunc(actor, 200), Note = Trunc(note, 500) });
            return (await GetAsync(id, ct))!.ToDto();
        }

        await using var tx = await conn.BeginTransactionAsync(ct);
        var jobs = await conn.ExecuteAsync(
            """
            UPDATE pack_content.publish_job
            SET status = 'Cancelled', last_error = 'Huỷ từ đề xuất lịch đăng', updated_at = NOW()
            WHERE id = @Job AND status IN ('Queued', 'Failed')
            """,
            new { Job = row.PublishJobId }, tx);
        if (jobs == 0)
        {
            await tx.RollbackAsync(ct);
            throw new InvalidOperationException("Job đăng đã bắt đầu chạy, không huỷ được.");
        }

        await conn.ExecuteAsync(
            """
            UPDATE pack_content.schedule_proposal
            SET status = 'Cancelled', decided_by = @Actor, decided_at = NOW(), last_error = @Note, updated_at = NOW()
            WHERE id = @Id
            """,
            new { Id = id, Actor = Trunc(actor, 200), Note = Trunc(note, 500) }, tx);
        await tx.CommitAsync(ct);
        return (await GetAsync(id, ct))!.ToDto();
    }

    public async Task<ContentScheduleProposalRecordDto> RetryAsync(
        Guid id, string actor, CancellationToken ct = default)
    {
        var row = await GetAsync(id, ct) ?? throw new InvalidOperationException("Không tìm thấy lượt đăng.");
        var problem = ContentScheduleApprovalRules.CheckRetry(row.Status, row.JobStatus, row.JobUpdatedAt, DateTimeOffset.UtcNow);
        if (problem is not null) throw new InvalidOperationException(problem);

        // Website Thái Nguyên Life (local_os) cần tenant của người đang đăng nhập, worker nền không có:
        // chạy ngay trong request này thay vì xếp hàng.
        var runNow = string.Equals(row.ConnectorType, "local_os", StringComparison.OrdinalIgnoreCase);
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        // Chỉ đổi khi job vẫn ở trạng thái vừa đọc, để không chạm vào job worker vừa giành.
        var changed = await conn.ExecuteAsync(
            """
            UPDATE pack_content.publish_job
            SET status = @NewStatus, publish_at = NOW(), last_error = NULL, updated_at = NOW()
            WHERE id = @Job AND status = @Expected
            """,
            new { Job = row.PublishJobId, Expected = row.JobStatus, NewStatus = runNow ? "Running" : "Queued" }, tx);
        if (changed == 0)
        {
            await tx.RollbackAsync(ct);
            throw new InvalidOperationException("Job vừa đổi trạng thái. Tải lại rồi thử lại.");
        }

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_content.publish_log (job_id, level, message, detail_json)
            VALUES (@Job, 'info', @Message, '{}'::jsonb)
            """,
            new { Job = row.PublishJobId, Message = Trunc($"Đăng lại (trước đó: {row.JobStatus}) bởi {actor}", 500) }, tx);
        await conn.ExecuteAsync(
            "UPDATE pack_content.schedule_proposal SET last_error = NULL, updated_at = NOW() WHERE id = @Id",
            new { Id = id }, tx);
        await tx.CommitAsync(ct);

        if (runNow && row.PublishJobId is Guid jobId)
            await _publish.RunJobAsync(jobId, null, ct); // lỗi được ghi vào job (Failed), không ném ra ngoài

        return (await GetAsync(id, ct))!.ToDto();
    }

    public async Task<ContentScheduleProposalRecordDto> MarkPostedAsync(
        Guid id, string? postUrl, string actor, CancellationToken ct = default)
    {
        var row = await GetAsync(id, ct) ?? throw new InvalidOperationException("Không tìm thấy lượt đăng.");
        if (row.PublishJobId is not null)
            throw new InvalidOperationException("Lượt này đăng tự động, không cần đánh dấu tay.");
        if (!string.Equals(row.Status, "Approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(row.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Lượt đăng đang ở trạng thái «{row.Status}», không đánh dấu đã đăng được.");
        await _plans.GetSettingsAsync(row.BrandId, ct); // bảo đảm trạng thái Posted đã có trong ràng buộc bảng
        var url = string.IsNullOrWhiteSpace(postUrl) ? null : postUrl.Trim();
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            """
            UPDATE pack_content.schedule_proposal
            SET status = 'Posted', decided_by = @Actor, decided_at = NOW(), last_error = @Note, updated_at = NOW()
            WHERE id = @Id
            """,
            new { Id = id, Actor = Trunc(actor, 200), Note = url is null ? null : Trunc("Link bài: " + url, 1000) });
        return (await GetAsync(id, ct))!.ToDto();
    }

    public async Task<string> ScheduleAgentWebsitesAsync(
        string brand, IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default)
    {
        var ids = (topicIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return "Chưa có bài Agent để xếp website.";

        var plan = await _advisor.PlanForTopicsAsync(brand, ids, ct);
        var sites = plan.Proposals
            .Where(p => ContentScheduleRules.SiteAutoConnectors.Contains(p.ConnectorType, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (sites.Count == 0)
            return "Bài Agent chưa đủ để xếp website, hoặc các khung website đã kín.";

        var brands = await _repo.ListBrandsAsync(true, ct);
        var b = brands.First(x => string.Equals(x.Code, plan.BrandCode, StringComparison.OrdinalIgnoreCase));
        var done = 0;
        var skipped = 0;
        foreach (var p in sites)
        {
            Guid? id;
            await using (var conn = await _db.CreateOpenConnectionAsync(ct))
            {
                id = await conn.ExecuteScalarAsync<Guid?>(
                    """
                    INSERT INTO pack_content.schedule_proposal (
                        brand_id, topic_id, title, target_key, target_label, connector_type,
                        site_id, channel_id, publish_at, reason, created_by)
                    VALUES (
                        @BrandId, @TopicId, @Title, @TargetKey, @TargetLabel, @ConnectorType,
                        @SiteId, @ChannelId, @PublishAt, @Reason, @Actor)
                    ON CONFLICT DO NOTHING
                    RETURNING id
                    """,
                    new
                    {
                        BrandId = b.Id,
                        p.TopicId,
                        Title = Trunc(p.Title, 500),
                        p.TargetKey,
                        TargetLabel = Trunc(p.TargetLabel, 200),
                        p.ConnectorType,
                        p.SiteId,
                        p.ChannelId,
                        PublishAt = p.ProposedAtUtc,
                        Reason = Trunc("Website theo lịch Agent. " + p.Reason, 1000),
                        Actor = "Autopilot",
                    });
            }

            if (id is not Guid proposalId)
            {
                skipped++;
                continue;
            }

            try
            {
                await ApproveAsync(proposalId, null, "Autopilot", ct);
                done++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        return done > 0
            ? $"Đã xếp và duyệt {done} lượt website theo lịch."
            : $"Không duyệt thêm lượt website ({skipped} lượt đã có lịch hoặc giờ không còn trống).";
    }

    public async Task<string> ScheduleAgentPagePostsAsync(
        string brand, IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default)
    {
        var ids = (topicIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return "Chưa có bài Agent để xếp Fanpage.";

        var brands = await _repo.ListBrandsAsync(true, ct);
        var b = brands.FirstOrDefault(x => string.Equals(x.Code, brand, StringComparison.OrdinalIgnoreCase));
        if (b is null) return "Không thấy thương hiệu để xếp Fanpage.";

        var ready = (await _repo.ListChannelsAsync(b.Id, ct))
            .Where(c => c.IsActive
                        && string.Equals(c.ChannelType, ContentScheduleRules.FacebookPage, StringComparison.OrdinalIgnoreCase)
                        && ContentScheduleRules.IsFacebookAutoReady(c.ConfigJson, c.SecretRef)
                        && ContentChannelAccepts.Allows(c.ConfigJson, ContentChannelAccepts.Post))
            .Select(c => c.Id)
            .ToHashSet();
        if (ready.Count == 0)
            return "Chưa có Fanpage nào đang tự đăng được bài viết.";

        var plan = await _advisor.PlanForTopicsAsync(b.Code, ids, ct);
        var pages = plan.Proposals
            .Where(p => string.Equals(p.ConnectorType, ContentScheduleRules.FacebookPage, StringComparison.OrdinalIgnoreCase)
                        && p.ChannelId is Guid channelId && ready.Contains(channelId))
            .ToList();
        if (pages.Count == 0)
            return "Bài Agent chưa đủ bản viết Fanpage, hoặc khung bài viết đã kín.";

        var plans = await _plans.GetPlansAsync(pages.Select(p => p.TopicId).Distinct().ToList(), ct);
        var articleIds = new HashSet<Guid>();
        foreach (var topicId in pages.Select(p => p.TopicId).Distinct())
        {
            plans.TryGetValue(topicId, out var pub);
            var hasVideo = (await _repo.ListAssetsAsync(topicId, ct))
                .Any(a => string.Equals(a.Kind, "video", StringComparison.OrdinalIgnoreCase));
            if (ContentPagePostRules.IsArticle(pub?.Kind, hasVideo))
                articleIds.Add(topicId);
        }

        var articles = pages.Where(p => articleIds.Contains(p.TopicId))
            .OrderBy(p => p.ProposedAtUtc)
            .ThenBy(p => p.TopicId)
            .ToList();
        if (articles.Count == 0)
            return "Các bài Fanpage đang là video, không xếp bài viết.";

        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var occupied = (await conn.QueryAsync<PageSlotRow>(
            """
            SELECT channel_target_id AS "ChannelId", COALESCE(publish_at, created_at) AS "PublishAt"
            FROM pack_content.publish_job
            WHERE brand_id = @Brand AND connector_type = 'facebook_page'
              AND status IN ('Queued', 'Running', 'Succeeded')
              AND channel_target_id IS NOT NULL
            UNION ALL
            SELECT channel_id, publish_at
            FROM pack_content.schedule_proposal
            WHERE brand_id = @Brand AND connector_type = 'facebook_page'
              AND status IN ('Approved', 'Posted')
              AND channel_id IS NOT NULL
            """,
            new { Brand = b.Id })).ToList();
        var byChannel = occupied.GroupBy(x => x.ChannelId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PublishAt).ToList());

        var done = 0;
        var skipped = 0;
        foreach (var p in articles)
        {
            var channelId = p.ChannelId!.Value;
            if (!byChannel.TryGetValue(channelId, out var taken))
            {
                taken = [];
                byChannel[channelId] = taken;
            }

            var at = ContentPagePostRules.NextOpen(DateTimeOffset.UtcNow, taken);
            if (at is not DateTimeOffset slot)
            {
                skipped++;
                continue;
            }

            var open = await conn.QuerySingleOrDefaultAsync<OpenProposalRow>(
                """
                SELECT id AS "Id", status AS "Status"
                FROM pack_content.schedule_proposal
                WHERE topic_id = @Topic AND target_key = @Key AND status IN ('Proposed', 'Approved')
                LIMIT 1
                """,
                new { Topic = p.TopicId, Key = p.TargetKey });
            if (string.Equals(open?.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            var reason = Trunc("Bài viết Fanpage lúc 15:00, lệch giờ video. " + p.Reason, 1000);
            Guid proposalId;
            if (open is not null)
            {
                var moved = await conn.ExecuteAsync(
                    """
                    UPDATE pack_content.schedule_proposal
                    SET publish_at = @At, reason = @Reason, updated_at = NOW()
                    WHERE id = @Id AND status = 'Proposed'
                    """,
                    new { Id = open.Id, At = slot, Reason = reason });
                if (moved == 0)
                {
                    skipped++;
                    continue;
                }
                proposalId = open.Id;
            }
            else
            {
                var id = await conn.ExecuteScalarAsync<Guid?>(
                    """
                    INSERT INTO pack_content.schedule_proposal (
                        brand_id, topic_id, title, target_key, target_label, connector_type,
                        site_id, channel_id, publish_at, reason, created_by)
                    VALUES (
                        @BrandId, @TopicId, @Title, @TargetKey, @TargetLabel, @ConnectorType,
                        NULL, @ChannelId, @PublishAt, @Reason, @Actor)
                    ON CONFLICT DO NOTHING
                    RETURNING id
                    """,
                    new
                    {
                        BrandId = b.Id,
                        p.TopicId,
                        Title = Trunc(p.Title, 500),
                        p.TargetKey,
                        TargetLabel = Trunc(p.TargetLabel, 200),
                        p.ConnectorType,
                        ChannelId = channelId,
                        PublishAt = slot,
                        Reason = reason,
                        Actor = "Autopilot",
                    });
                if (id is not Guid created)
                {
                    skipped++;
                    continue;
                }
                proposalId = created;
            }

            try
            {
                await ApproveAsync(proposalId, slot, "Autopilot", ct);
                done++;
                taken.Add(slot);
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        return done > 0
            ? $"Đã xếp và duyệt {done} bài viết Fanpage lúc 15:00."
            : $"Không duyệt thêm bài viết Fanpage ({skipped} lượt đã có lịch hoặc giờ không còn trống).";
    }

    /// <summary>Lượt đăng Fanpage khi Fanpage chưa tự đăng được (chưa có API Meta).</summary>
    private async Task<bool> IsManualAsync(Row row, CancellationToken ct)
    {
        if (!string.Equals(row.ConnectorType, ContentScheduleRules.FacebookPage, StringComparison.OrdinalIgnoreCase)
            || row.ChannelId is not Guid channelId)
            return false;
        var channel = (await _repo.ListChannelsAsync(row.BrandId, ct)).FirstOrDefault(c => c.Id == channelId);
        return channel is not null && !ContentScheduleRules.IsFacebookAutoReady(channel.ConfigJson, channel.SecretRef);
    }

    private async Task<Row?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            $"{SelectSql} WHERE sp.id = @Id", new { Id = id });
    }

    private sealed class PageSlotRow
    {
        public Guid ChannelId { get; set; }
        public DateTimeOffset PublishAt { get; set; }
    }

    private sealed class OpenProposalRow
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "";
    }

    private sealed class Row
    {
        public Guid Id { get; set; }
        public Guid BrandId { get; set; }
        public Guid TopicId { get; set; }
        public string Title { get; set; } = "";
        public string TargetKey { get; set; } = "";
        public string TargetLabel { get; set; } = "";
        public string ConnectorType { get; set; } = "";
        public Guid? SiteId { get; set; }
        public Guid? ChannelId { get; set; }
        public DateTimeOffset PublishAt { get; set; }
        public string Status { get; set; } = "";
        public string Reason { get; set; } = "";
        public Guid? PublishJobId { get; set; }
        public string? DecidedBy { get; set; }
        public DateTimeOffset? DecidedAt { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? JobStatus { get; set; }
        public string? JobError { get; set; }
        public string? JobUrl { get; set; }
        public DateTimeOffset? JobUpdatedAt { get; set; }
        public string? JobCaption { get; set; }

        public ContentScheduleProposalRecordDto ToDto() => new(
            Id, BrandId, TopicId, Title, TargetKey, TargetLabel, ConnectorType, SiteId, ChannelId,
            PublishAt, Status, Reason, PublishJobId, DecidedBy, DecidedAt, LastError, CreatedAt,
            JobStatus, JobError, JobUrl, JobUpdatedAt, JobCaption);
    }

    private static string? Trunc(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s[..max]);
}
