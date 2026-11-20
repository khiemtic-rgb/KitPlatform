using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Dọn file video đính bài (asset kind = video) sau khi bài đã đăng xong.
/// Quy tắc theo cài đặt lịch của thương hiệu: đăng xong mọi lượt → giữ MediaKeepDaysPosted ngày (mặc định 3);
/// mọi lượt đã huỷ → giữ MediaKeepDaysCancelled ngày (mặc định 7). Còn lượt chờ / đang đăng / lỗi,
/// hoặc Fanpage đăng tay chưa bấm "Đã đăng" → không xoá. Chỉ xoá file và đánh dấu asset (meta_json.purgedAt);
/// bài, link đã đăng, lịch và nhật ký giữ nguyên.
/// </summary>
internal sealed class ContentMediaCleanupService : IContentMediaCleanupService
{
    private static DateTimeOffset? _lastAt;
    private static string? _lastNote;

    private readonly IDbConnectionFactory _db;
    private readonly IOptions<ContentOptions> _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ContentMediaCleanupService> _log;

    public ContentMediaCleanupService(
        IDbConnectionFactory db,
        IOptions<ContentOptions> options,
        IHostEnvironment env,
        ILogger<ContentMediaCleanupService> log)
    {
        _db = db;
        _options = options;
        _env = env;
        _log = log;
    }

    private const string Sql = """
        SELECT
            a.id AS Id,
            a.topic_id AS TopicId,
            a.storage_path AS StoragePath,
            COALESCE((s.settings_json ->> 'mediaKeepDaysPosted')::int, 3) AS KeepPosted,
            COALESCE((s.settings_json ->> 'mediaKeepDaysCancelled')::int, 7) AS KeepCancelled,
            (SELECT COUNT(*) FROM pack_content.publish_job j
              WHERE j.topic_id = a.topic_id AND j.status NOT IN ('Succeeded', 'Cancelled'))::int AS OpenJobs,
            (SELECT COUNT(*) FROM pack_content.publish_job j
              WHERE j.topic_id = a.topic_id AND j.status = 'Succeeded')::int AS DoneJobs,
            (SELECT COUNT(*) FROM pack_content.publish_job j
              WHERE j.topic_id = a.topic_id)::int AS AllJobs,
            (SELECT MAX(j.updated_at) FROM pack_content.publish_job j
              WHERE j.topic_id = a.topic_id) AS JobAt,
            (SELECT COUNT(*) FROM pack_content.schedule_proposal p
              WHERE p.topic_id = a.topic_id AND p.publish_job_id IS NULL
                AND p.status NOT IN ('Posted', 'Cancelled', 'Rejected'))::int AS OpenManual,
            (SELECT COUNT(*) FROM pack_content.schedule_proposal p
              WHERE p.topic_id = a.topic_id AND p.publish_job_id IS NULL AND p.status = 'Posted')::int AS DoneManual,
            (SELECT COUNT(*) FROM pack_content.schedule_proposal p
              WHERE p.topic_id = a.topic_id AND p.publish_job_id IS NULL)::int AS AllManual,
            (SELECT MAX(p.updated_at) FROM pack_content.schedule_proposal p
              WHERE p.topic_id = a.topic_id AND p.publish_job_id IS NULL) AS ManualAt
        FROM pack_content.asset a
        JOIN pack_content.topic t ON t.id = a.topic_id
        LEFT JOIN pack_content.brand_schedule_setting s ON s.brand_id = t.brand_id
        WHERE a.kind = 'video'
          AND COALESCE(a.meta_json ->> 'purgedAt', '') = ''
        """;

    private sealed class Row
    {
        public Guid Id { get; set; }
        public Guid TopicId { get; set; }
        public string StoragePath { get; set; } = "";
        public int KeepPosted { get; set; }
        public int KeepCancelled { get; set; }
        public int OpenJobs { get; set; }
        public int DoneJobs { get; set; }
        public int AllJobs { get; set; }
        public DateTimeOffset? JobAt { get; set; }
        public int OpenManual { get; set; }
        public int DoneManual { get; set; }
        public int AllManual { get; set; }
        public DateTimeOffset? ManualAt { get; set; }
    }

    /// <summary>null = chưa đến hạn hoặc chưa được xoá; ngược lại là thời điểm được xoá.</summary>
    private static DateTimeOffset? DueAt(Row r)
    {
        if (r.OpenJobs > 0 || r.OpenManual > 0) return null;
        if (r.AllJobs + r.AllManual == 0) return null; // chưa từng lên lịch: không đụng
        var last = new[] { r.JobAt, r.ManualAt }.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(DateTimeOffset.UtcNow).Max();
        var posted = r.DoneJobs + r.DoneManual > 0;
        return last.AddDays(posted ? Math.Clamp(r.KeepPosted, 1, 60) : Math.Clamp(r.KeepCancelled, 1, 60));
    }

    private string AssetRoot()
    {
        var configured = string.IsNullOrWhiteSpace(_options.Value.AssetRoot) ? "App_Data/content-assets" : _options.Value.AssetRoot;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }

    /// <summary>Đường dẫn file của asset, chỉ khi nằm trong thư mục asset (chặn ../).</summary>
    private string? FileOf(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;
        var root = Path.GetFullPath(AssetRoot());
        var full = Path.GetFullPath(Path.Combine(root, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal) ? full : null;
    }

    private static long SizeOf(string? path)
    {
        try { return path is not null && File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch (IOException) { return 0; }
        catch (UnauthorizedAccessException) { return 0; }
    }

    private async Task<List<Row>> LoadAsync(CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return (await conn.QueryAsync<Row>(Sql)).ToList();
    }

    public async Task<ContentMediaStorageDto> GetStorageAsync(CancellationToken ct = default)
    {
        var rows = await LoadAsync(ct);
        var now = DateTimeOffset.UtcNow;
        int files = 0, cleanable = 0, waiting = 0;
        long bytes = 0, cleanBytes = 0, waitBytes = 0;
        foreach (var r in rows)
        {
            var size = SizeOf(FileOf(r.StoragePath));
            if (size <= 0) continue;
            files++;
            bytes += size;
            var due = DueAt(r);
            if (due is not null && due <= now) { cleanable++; cleanBytes += size; }
            else { waiting++; waitBytes += size; }
        }
        return new ContentMediaStorageDto(files, bytes, cleanable, cleanBytes, waiting, waitBytes, _lastAt, _lastNote);
    }

    public async Task<ContentMediaCleanupReportDto> CleanupAsync(string actor, CancellationToken ct = default)
    {
        var rows = await LoadAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var notes = new List<string>();
        var deleted = 0;
        long freed = 0;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        foreach (var r in rows)
        {
            ct.ThrowIfCancellationRequested();
            var due = DueAt(r);
            if (due is null || due > now) continue;
            var path = FileOf(r.StoragePath);
            var size = SizeOf(path);
            try
            {
                if (path is not null && File.Exists(path)) File.Delete(path);
                var dir = path is null ? null : Path.GetDirectoryName(path);
                if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                notes.Add($"Không xoá được file của bài {r.TopicId}: {ex.Message}");
                continue;
            }
            await conn.ExecuteAsync(
                """
                UPDATE pack_content.asset
                SET meta_json = COALESCE(meta_json, '{}'::jsonb)
                    || jsonb_build_object('purgedAt', NOW(), 'purgedBytes', @Size, 'purgedBy', @Actor)
                WHERE id = @Id
                """,
                new { r.Id, Size = size, Actor = actor.Length > 100 ? actor[..100] : actor });
            deleted++;
            freed += size;
        }
        _lastAt = now;
        _lastNote = deleted > 0
            ? $"Đã xoá {deleted} file, giải phóng {freed / 1024 / 1024} MB ({actor})."
            : $"Không có file đến hạn xoá ({actor}).";
        if (deleted > 0) _log.LogInformation("Content media cleanup: {Files} file(s), {Bytes} bytes ({Actor})", deleted, freed, actor);
        return new ContentMediaCleanupReportDto(deleted, freed, notes);
    }
}

/// <summary>Chạy dọn file video đã đăng mỗi 6 giờ (bắt đầu sau 2 phút từ khi API khởi động).</summary>
internal sealed class ContentMediaCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;
    private readonly ILogger<ContentMediaCleanupWorker> _log;

    public ContentMediaCleanupWorker(IServiceScopeFactory scopes, IOptions<ContentOptions> options, ILogger<ContentMediaCleanupWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled || !_options.Value.MediaCleanupEnabled)
        {
            _log.LogInformation("Content media cleanup disabled.");
            return;
        }
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<IContentMediaCleanupService>();
                await svc.CleanupAsync("tự động", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Content media cleanup failed.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
