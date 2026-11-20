namespace KitPlatform.Packs.Content;

/// <summary>
/// Thông tin đăng kèm video của một bài (Video Studio bước 5): ảnh bìa ngang (YouTube, video Fanpage),
/// ảnh bìa dọc (Shorts, Reels), danh sách phát YouTube, khung hình làm bìa TikTok.
/// </summary>
public sealed record ContentPublishExtrasRequest(
    string? YoutubePlaylist,
    int? CoverTimestampMs);

public sealed record ContentPublishImageUpload(Stream Content, string FileName, string ContentType);

public sealed record ContentPublishExtrasDto(
    Guid TopicId,
    Guid? ThumbnailAssetId,
    Guid? CoverAssetId,
    string? YoutubePlaylist,
    int? CoverTimestampMs);

public interface IContentPublishExtrasService
{
    Task<ContentPublishExtrasDto?> SaveAsync(
        Guid topicId,
        ContentPublishExtrasRequest request,
        ContentPublishImageUpload? thumbnail,
        ContentPublishImageUpload? cover,
        CancellationToken cancellationToken = default);
}
