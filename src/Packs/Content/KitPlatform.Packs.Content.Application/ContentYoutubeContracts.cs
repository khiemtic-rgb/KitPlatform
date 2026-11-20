namespace KitPlatform.Packs.Content;

public sealed record ContentYoutubeStartDto(string Url, string State);

public sealed record ContentYoutubeCompleteRequest(string Code, string State);

public sealed record ContentYoutubeVerifyDto(
    bool Ok,
    string Status,
    string? ChannelId,
    string? ChannelTitle,
    string? Message,
    DateTimeOffset? LastVerifiedAt);

public sealed record ContentYoutubeVideoDto(
    Guid AssetId,
    Guid TopicId,
    string FileName,
    long SizeBytes);

/// <summary>Kết nối kênh YouTube (OAuth Google) + đính video vào bài để Agent đăng tự động.</summary>
public interface IContentYoutubeConnectionService
{
    Task<ContentYoutubeStartDto> StartAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<ContentChannelTargetDto> CompleteAsync(string code, string state, CancellationToken cancellationToken = default);
    Task<ContentYoutubeVerifyDto> VerifyAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<ContentYoutubeVideoDto?> UploadVideoAsync(
        Guid topicId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
