namespace KitPlatform.Packs.Content;

/// <summary>
/// Cài đặt lịch đăng của một thương hiệu: khung giờ, khoảng cách và hạn mức video dài/ngắn trên mỗi kênh.
/// Agent dùng cài đặt này mỗi lần xếp lịch.
/// </summary>
public sealed record ContentBrandScheduleSettingsDto(
    IReadOnlyList<string> LocalSlots,
    int MinGapHours,
    int MaxPerDayPerChannel,
    int ShortPerDayPerChannel,
    int LongPerWeekPerChannel,
    int Days,
    bool Saved,
    bool AutopilotEnabled = false,
    int AutopilotPostsPerWeek = 3,
    bool PublishPaused = false,
    int MediaKeepDaysPosted = 3,
    int MediaKeepDaysCancelled = 7);

public sealed class ContentBrandScheduleSettingsRequest
{
    public List<string>? LocalSlots { get; set; }
    public int? MinGapHours { get; set; }
    public int? MaxPerDayPerChannel { get; set; }
    public int? ShortPerDayPerChannel { get; set; }
    public int? LongPerWeekPerChannel { get; set; }
    public int? Days { get; set; }
    /// <summary>Viết tự động theo lịch (mặc định tắt).</summary>
    public bool? AutopilotEnabled { get; set; }
    /// <summary>Số bài Agent viết nháp mỗi tuần (1–7).</summary>
    public int? AutopilotPostsPerWeek { get; set; }
    /// <summary>Tạm dừng đăng: bài đã có lịch của thương hiệu đứng chờ, không đăng cho tới khi bật lại.</summary>
    public bool? PublishPaused { get; set; }
    /// <summary>Số ngày giữ file video trên máy chủ sau khi bài đã đăng xong mọi kênh (1–60, mặc định 3).</summary>
    public int? MediaKeepDaysPosted { get; set; }
    /// <summary>Số ngày giữ file video của bài đã huỷ hết lượt đăng (1–60, mặc định 7).</summary>
    public int? MediaKeepDaysCancelled { get; set; }
}

/// <summary>Kế hoạch đăng của một bài: video dài hay ngắn, đăng ở loại kênh nào, thứ tự trong lô.</summary>
public sealed record ContentTopicPublishPlanDto(
    Guid TopicId,
    string Kind,
    IReadOnlyList<string> Connectors,
    int Seq,
    string? SourceRef);

public sealed class ContentTopicPublishPlanInput
{
    public Guid TopicId { get; set; }
    /// <summary>"long" hoặc "short".</summary>
    public string Kind { get; set; } = "short";
    /// <summary>Loại kênh: youtube_channel, tiktok_channel.</summary>
    public List<string> Connectors { get; set; } = new();
    public int Seq { get; set; }
    /// <summary>Nguồn của bài, ví dụ "studio:&lt;buildId&gt;:wide".</summary>
    public string? SourceRef { get; set; }
}

public sealed class ContentScheduleTopicsRequest
{
    /// <summary>Mã, tên hoặc Guid thương hiệu.</summary>
    public string Brand { get; set; } = "";
    public List<ContentTopicPublishPlanInput> Topics { get; set; } = new();
}

public sealed record ContentScheduleTopicsResultDto(
    IReadOnlyList<ContentScheduleProposalRecordDto> Scheduled,
    IReadOnlyList<ContentScheduleSkippedDto> Skipped,
    IReadOnlyList<string> Warnings);

public interface IContentPublishPlanStore
{
    Task<ContentBrandScheduleSettingsDto> GetSettingsAsync(Guid brandId, CancellationToken ct = default);

    Task<ContentBrandScheduleSettingsDto> SaveSettingsAsync(Guid brandId, ContentBrandScheduleSettingsRequest request, CancellationToken ct = default);

    Task SavePlansAsync(IReadOnlyList<ContentTopicPublishPlanInput> plans, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, ContentTopicPublishPlanDto>> GetPlansAsync(IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default);

    /// <summary>
    /// Lượt đăng tay đã duyệt hoặc đã đăng (không có publish_job) của thương hiệu, để Agent coi như khung đã dùng.
    /// </summary>
    Task<IReadOnlyList<ContentManualSlot>> ListManualSlotsAsync(Guid brandId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContentManualSlot>>([]);

    /// <summary>Thương hiệu đang bật viết tự động.</summary>
    Task<IReadOnlyList<Guid>> ListAutopilotBrandsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);

    /// <summary>Số lần Agent đã viết nháp cho thương hiệu kể từ một thời điểm.</summary>
    Task<int> CountAutopilotRunsAsync(Guid brandId, DateTimeOffset since, bool includeFailed = false, CancellationToken ct = default)
        => Task.FromResult(0);

    /// <summary>Số bài Agent viết đang chờ duyệt (chưa Approved/đăng).</summary>
    Task<int> CountAutopilotPendingAsync(Guid brandId, CancellationToken ct = default)
        => Task.FromResult(0);

    Task AddAutopilotRunAsync(Guid brandId, Guid seriesId, Guid episodeId, Guid? topicId, string note, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Bài Agent đã viết (có topic), để xếp website ở lượt sau.</summary>
    Task<IReadOnlyList<Guid>> ListAutopilotTopicIdsAsync(Guid brandId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);
}

/// <summary>Một lượt đăng tay đã giữ chỗ trên lịch (chưa có API cho kênh đó).</summary>
public sealed record ContentManualSlot(Guid TopicId, Guid? ChannelId, Guid? SiteId, DateTimeOffset PublishAt, string Status);

public static class ContentPublishPlanRules
{
    /// <summary>Kênh nhận video từ Video Studio. Fanpage chưa có API Meta thì thành lượt đăng tay theo lịch.</summary>
    public static readonly string[] VideoConnectors = ["youtube_channel", "tiktok_channel", "facebook_page"];

    /// <summary>Mặc định theo hướng dẫn kênh: 09:00 và 19:30, cách nhau ≥ 6 giờ, 1 video ngắn/ngày, 2 video dài/tuần mỗi kênh.</summary>
    public static ContentBrandScheduleSettingsDto Defaults() =>
        new(["09:00", "19:30"], 6, 2, 1, 2, 14, false);

    public static ContentBrandScheduleSettingsDto Normalize(ContentBrandScheduleSettingsRequest request, ContentBrandScheduleSettingsDto current)
    {
        var slots = (request.LocalSlots ?? current.LocalSlots.ToList())
            .Select(s => (s ?? "").Trim())
            .Where(s => TimeOnly.TryParse(s, out _))
            .Select(s => TimeOnly.Parse(s).ToString("HH:mm"))
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .Take(6)
            .ToList();
        if (slots.Count == 0)
            throw new InvalidOperationException("Cần ít nhất một khung giờ đăng hợp lệ (ví dụ 09:00).");
        return new ContentBrandScheduleSettingsDto(
            slots,
            Math.Clamp(request.MinGapHours ?? current.MinGapHours, 0, 48),
            Math.Clamp(request.MaxPerDayPerChannel ?? current.MaxPerDayPerChannel, 1, 4),
            Math.Clamp(request.ShortPerDayPerChannel ?? current.ShortPerDayPerChannel, 0, 4),
            Math.Clamp(request.LongPerWeekPerChannel ?? current.LongPerWeekPerChannel, 0, 14),
            Math.Clamp(request.Days ?? current.Days, 1, 14),
            true,
            request.AutopilotEnabled ?? current.AutopilotEnabled,
            Math.Clamp(request.AutopilotPostsPerWeek ?? current.AutopilotPostsPerWeek, 1, 7),
            request.PublishPaused ?? current.PublishPaused,
            Math.Clamp(request.MediaKeepDaysPosted ?? current.MediaKeepDaysPosted, 1, 60),
            Math.Clamp(request.MediaKeepDaysCancelled ?? current.MediaKeepDaysCancelled, 1, 60));
    }

    /// <summary>Bài đã duyệt chưa đăng cần giữ: đủ suất trong tuần, cộng một bài dự phòng.</summary>
    public static int ReserveTarget(int postsPerWeek) => Math.Clamp(postsPerWeek, 1, 7) + 1;

    public static ContentTopicPublishPlanInput Validate(ContentTopicPublishPlanInput input)
    {
        if (input.TopicId == Guid.Empty) throw new InvalidOperationException("Thiếu mã bài.");
        var kind = (input.Kind ?? "").Trim().ToLowerInvariant();
        if (kind is not ("long" or "short")) throw new InvalidOperationException("Loại video chỉ được là long hoặc short.");
        var connectors = (input.Connectors ?? new List<string>())
            .Select(c => (c ?? "").Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
        if (connectors.Count == 0) throw new InvalidOperationException("Bài cần ít nhất một loại kênh để đăng.");
        var bad = connectors.FirstOrDefault(c => !VideoConnectors.Contains(c));
        if (bad is not null) throw new InvalidOperationException($"Loại kênh «{bad}» không hỗ trợ cho video.");
        return new ContentTopicPublishPlanInput
        {
            TopicId = input.TopicId,
            Kind = kind,
            Connectors = connectors,
            Seq = Math.Clamp(input.Seq, 0, 10_000),
            SourceRef = string.IsNullOrWhiteSpace(input.SourceRef) ? null : input.SourceRef.Trim()[..Math.Min(200, input.SourceRef.Trim().Length)],
        };
    }
}

/// <summary>Kết quả một lượt Viết tự động của một thương hiệu: từng bước Agent đã làm hoặc lý do dừng.</summary>
public sealed record ContentAutopilotReportDto(Guid BrandId, string BrandCode, IReadOnlyList<string> Steps, Guid? WroteTopicId);

/// <summary>Viết tự động theo Series. Worker gọi định kỳ; nút "Chạy ngay" gọi qua API.</summary>
public interface IContentAutopilotService
{
    /// <param name="ignoreDailyLimit">true khi người dùng bấm "Viết ngay": bỏ giới hạn 1 bài/ngày (vẫn giữ hạn mức tuần và số bài chờ duyệt).</param>
    Task<ContentAutopilotReportDto> RunBrandAsync(Guid brandId, bool ignoreDailyLimit, CancellationToken ct = default);
}

/// <summary>Dung lượng file video đính bài trên máy chủ và phần dọn được ngay.</summary>
public sealed record ContentMediaStorageDto(
    int Files,
    long Bytes,
    int CleanableFiles,
    long CleanableBytes,
    int WaitingFiles,
    long WaitingBytes,
    DateTimeOffset? LastCleanupAt,
    string? LastCleanupNote);

/// <summary>Kết quả một lượt dọn: số file đã xoá, dung lượng giải phóng, ghi chú từng lỗi (nếu có).</summary>
public sealed record ContentMediaCleanupReportDto(int DeletedFiles, long FreedBytes, IReadOnlyList<string> Notes);

/// <summary>
/// Dọn file video đính bài sau khi đăng xong: bài đã đăng hết mọi kênh giữ <c>MediaKeepDaysPosted</c> ngày,
/// bài đã huỷ hết giữ <c>MediaKeepDaysCancelled</c> ngày (cài đặt lịch của thương hiệu).
/// Bài còn chờ đăng, đang đăng, đăng lỗi, hoặc Fanpage chưa bấm "Đã đăng" thì không xoá.
/// Chỉ xoá file; bài, link đã đăng và nhật ký giữ nguyên.
/// </summary>
public interface IContentMediaCleanupService
{
    Task<ContentMediaStorageDto> GetStorageAsync(CancellationToken ct = default);
    Task<ContentMediaCleanupReportDto> CleanupAsync(string actor, CancellationToken ct = default);
}
