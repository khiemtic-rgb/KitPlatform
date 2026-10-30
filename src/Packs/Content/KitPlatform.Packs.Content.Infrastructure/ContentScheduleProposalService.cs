using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentScheduleProposalService : IContentScheduleProposalService
{
    private const string Cols = """
        id, brand_id, topic_id, title, target_key, target_label, connector_type, site_id, channel_id,
        publish_at, status, reason, publish_job_id, decided_by, decided_at, last_error, created_at
        """;

    private readonly IDbConnectionFactory _db;
    private readonly IContentScheduleAdvisor _advisor;
    private readonly IContentPublishService _publish;
    private readonly ContentRepository _repo;

    public ContentScheduleProposalService(
        IDbConnectionFactory db,
        IContentScheduleAdvisor advisor,
        IContentPublishService publish,
        ContentRepository repo)
    {
        _db = db;
        _advisor = advisor;
        _publish = publish;
        _repo = repo;
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
                $"SELECT {Cols} FROM pack_content.schedule_proposal WHERE id = ANY(@Ids) ORDER BY publish_at",
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
            SELECT sp.id, sp.brand_id, sp.topic_id, sp.title, sp.target_key, sp.target_label, sp.connector_type,
                   sp.site_id, sp.channel_id, sp.publish_at, sp.status, sp.reason, sp.publish_job_id,
                   sp.decided_by, sp.decided_at, sp.last_error, sp.created_at
            FROM pack_content.schedule_proposal sp
            JOIN pack_content.brand b ON b.id = sp.brand_id
            WHERE (@Brand::text IS NULL OR lower(b.code) = lower(@Brand) OR lower(b.name) = lower(@Brand))
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
        var problem = ContentScheduleApprovalRules.CheckCancel(row.Status, jobStatus);
        if (problem is not null) throw new InvalidOperationException(problem);

        await using var tx = await conn.BeginTransactionAsync(ct);
        var jobs = await conn.ExecuteAsync(
            """
            UPDATE pack_content.publish_job
            SET status = 'Cancelled', last_error = 'Huỷ từ đề xuất lịch đăng', updated_at = NOW()
            WHERE id = @Job AND status = 'Queued'
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

    private async Task<Row?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Row>(
            $"SELECT {Cols} FROM pack_content.schedule_proposal WHERE id = @Id", new { Id = id });
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

        public ContentScheduleProposalRecordDto ToDto() => new(
            Id, BrandId, TopicId, Title, TargetKey, TargetLabel, ConnectorType, SiteId, ChannelId,
            PublishAt, Status, Reason, PublishJobId, DecidedBy, DecidedAt, LastError, CreatedAt);
    }

    private static string? Trunc(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s[..max]);
}
