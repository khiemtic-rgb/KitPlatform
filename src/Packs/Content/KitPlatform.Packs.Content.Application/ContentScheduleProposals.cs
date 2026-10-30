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
    DateTimeOffset CreatedAt);

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

    public static string? CheckCancel(string status, string? jobStatus)
    {
        if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
            return "Chỉ huỷ được đề xuất đã duyệt.";
        if (!string.Equals(jobStatus, "Queued", StringComparison.OrdinalIgnoreCase))
            return $"Job đăng đang ở trạng thái «{jobStatus ?? "không rõ"}», không huỷ được nữa.";
        return null;
    }
}
