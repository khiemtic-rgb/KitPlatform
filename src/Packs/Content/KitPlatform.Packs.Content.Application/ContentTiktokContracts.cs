namespace KitPlatform.Packs.Content;

public sealed record ContentTiktokStartDto(string Url, string State);

public sealed record ContentTiktokCompleteRequest(string Code, string State);

public sealed record ContentTiktokVerifyDto(
    bool Ok,
    string Status,
    string? OpenId,
    string? DisplayName,
    string? Message,
    DateTimeOffset? LastVerifiedAt);

/// <summary>Kết nối tài khoản TikTok (Login Kit, OAuth) để Agent gửi video vào hộp nháp (Content Posting API, video.upload).</summary>
public interface IContentTiktokConnectionService
{
    Task<ContentTiktokStartDto> StartAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<ContentChannelTargetDto> CompleteAsync(string code, string state, CancellationToken cancellationToken = default);
    Task<ContentTiktokVerifyDto> VerifyAsync(Guid channelId, CancellationToken cancellationToken = default);
}
