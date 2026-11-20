namespace KitPlatform.Packs.Content;

public sealed record ContentScheduleProposalRecordDto(
    Guid Id,
    Guid BrandId,
    Guid TopicId,
    string Title,
    string TargetKey,
    string TargetLabel,
    string ConnectorType,
    Guid? SiteId,
    Guid? ChannelId,
    DateTimeOffset PublishAt,
    string Status,
    string Reason,
    Guid? PublishJobId,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? LastError,
    DateTimeOffset CreatedAt,
    // Kết quả thật của job đăng (null với lượt đăng tay hoặc đề xuất chưa duyệt).
    string? JobStatus = null,
    string? JobError = null,
    string? JobUrl = null,
    DateTimeOffset? JobUpdatedAt = null,
    // Chú thích TikTok đã duyệt (hộp nháp TikTok không nhận chú thích qua API, người đăng dán tay).
    string? JobCaption = null);

public sealed record ContentScheduleGenerateResultDto(
    string BrandCode,
    int Created,
    int Superseded,
    IReadOnlyList<ContentScheduleProposalRecordDto> Proposals,
    IReadOnlyList<ContentScheduleSkippedDto> Skipped,
    IReadOnlyList<string> Warnings);

public sealed class ContentScheduleApproveRequest
{
    /// <summary>Optional new time (UTC). Must be in the future.</summary>
    public DateTimeOffset? PublishAt { get; set; }
}

public sealed class ContentScheduleDecisionRequest
{
    public string? Note { get; set; }
}

/// <summary>Approval gate between the agent's proposal and the real publish job.</summary>
public interface IContentScheduleProposalService
{
    Task<ContentScheduleGenerateResultDto> GenerateAsync(
        string brand, int days, int maxPerDay, string actor, CancellationToken ct = default);

    Task<IReadOnlyList<ContentScheduleProposalRecordDto>> ListAsync(
        string? brand, string? status, CancellationToken ct = default);

    Task<ContentScheduleProposalRecordDto> ApproveAsync(
        Guid id, DateTimeOffset? publishAt, string actor, CancellationToken ct = default);

    Task<ContentScheduleProposalRecordDto> RejectAsync(
        Guid id, string? note, string actor, CancellationToken ct = default);

    /// <summary>Cancels an approved proposal whose publish job has not started yet.</summary>
    Task<ContentScheduleProposalRecordDto> CancelAsync(
        Guid id, string? note, string actor, CancellationToken ct = default);

    /// <summary>
    /// Lưu kế hoạch đăng của các bài (dài/ngắn, loại kênh), xếp vào khung giờ trống theo cài đặt lịch của thương hiệu
    /// và duyệt luôn các khung đó. Dùng khi người dùng đã duyệt thành phẩm ở Video Studio.
    /// </summary>
    Task<ContentScheduleTopicsResultDto> ScheduleTopicsAsync(
        ContentScheduleTopicsRequest request, string actor, CancellationToken ct = default)
        => throw new NotSupportedException();

    /// <summary>
    /// Đăng lại lượt đã duyệt có job lỗi, job kẹt (máy tắt khi đang đăng) hoặc job còn chờ:
    /// đưa job về hàng chờ với giờ đăng = bây giờ, worker chạy ở vòng kế tiếp.
    /// </summary>
    Task<ContentScheduleProposalRecordDto> RetryAsync(
        Guid id, string actor, CancellationToken ct = default)
        => throw new NotSupportedException();

    /// <summary>Đánh dấu một lượt đăng tay (Fanpage chưa có API) là đã đăng.</summary>
    Task<ContentScheduleProposalRecordDto> MarkPostedAsync(
        Guid id, string? postUrl, string actor, CancellationToken ct = default)
        => throw new NotSupportedException();

    /// <summary>
    /// Xếp và duyệt chỉ các nơi đăng website (astro_git, wordpress_rest, local_os) cho các bài đã chỉ định.
    /// Không hủy đề xuất Fanpage/YouTube và không tạo job cho các kênh đó.
    /// </summary>
    Task<string> ScheduleAgentWebsitesAsync(
        string brand, IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default)
        => Task.FromResult("Chưa xếp website.");

    /// <summary>
    /// Xếp và duyệt bài viết Fanpage lúc 15:00. Bỏ bài có file video hoặc kế hoạch video dài/ngắn.
    /// Không hủy lịch thước phim đã duyệt.
    /// </summary>
    Task<string> ScheduleAgentPagePostsAsync(
        string brand, IReadOnlyCollection<Guid> topicIds, CancellationToken ct = default)
        => Task.FromResult("Chưa xếp bài viết Fanpage.");
}

public static class ContentScheduleApprovalRules
{
    public const int MinLeadMinutes = 2;

    public static string? CheckApprove(string status, DateTimeOffset publishAt, DateTimeOffset nowUtc)
    {
        if (!string.Equals(status, "Proposed", StringComparison.OrdinalIgnoreCase))
            return $"Đề xuất đang ở trạng thái «{status}», chỉ duyệt được đề xuất ở trạng thái Proposed.";
        if (publishAt < nowUtc.AddMinutes(MinLeadMinutes))
            return "Giờ đăng đã qua hoặc quá sát. Chọn giờ mới (sau ít nhất 2 phút) hoặc tạo đề xuất mới.";
        return null;
    }

    /// <summary>Job chạy lâu hơn mức này coi như bị kẹt (máy tắt hoặc API khởi động lại giữa chừng).</summary>
    public static readonly TimeSpan StuckRunningAfter = TimeSpan.FromMinutes(45);

    public static string? CheckRetry(string status, string? jobStatus, DateTimeOffset? jobUpdatedAt, DateTimeOffset nowUtc)
    {
        if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
            return $"Lượt đăng đang ở trạng thái «{status}», chỉ đăng lại được lượt đã duyệt.";
        return jobStatus switch
        {
            null => "Lượt này đăng tay, không có job để đăng lại.",
            "Succeeded" => "Job đã đăng xong, không đăng lại để tránh trùng bài.",
            "Failed" or "Queued" => null,
            "Running" when jobUpdatedAt is { } at && nowUtc - at < StuckRunningAfter
                => "Job đang đăng, chờ vài phút rồi tải lại.",
            "Running" => null,
            _ => $"Job đang ở trạng thái «{jobStatus}», không đăng lại được.",
        };
    }

    public static string? CheckCancel(string status, string? jobStatus, bool manual = false)
    {
        if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
            return "Chỉ huỷ được đề xuất đã duyệt.";
        if (manual) return null;
        // Queued: chưa chạy. Failed: đã thử và hỏng, huỷ để bỏ lượt (ví dụ chọn nhầm nơi đăng).
        if (!string.Equals(jobStatus, "Queued", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(jobStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            return $"Job đăng đang ở trạng thái «{jobStatus ?? "không rõ"}», không huỷ được nữa.";
        return null;
    }
}
