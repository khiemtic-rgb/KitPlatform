using System.Collections.Concurrent;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Refresh token hết hạn, bị thu hồi hoặc không còn: phải kết nối lại TikTok.</summary>
internal sealed class ContentTiktokAuthException : InvalidOperationException
{
    public ContentTiktokAuthException(string message) : base(message) { }
}

/// <summary>TikTok tạm thời không trả lời (lỗi mạng, 5xx, 429). Chưa mất kết nối, thử lại sau.</summary>
internal sealed class ContentTiktokTransientException : InvalidOperationException
{
    public ContentTiktokTransientException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Đổi refresh token lấy access token cho một kênh TikTok, mỗi lần một yêu cầu.
/// Từ 01/12/2026 mỗi lần đổi thành công TikTok cấp refresh token mới và vô hiệu token cũ ngay,
/// nên hai lần đổi cùng lúc bằng một token sẽ làm lần sau thất bại và kênh mất kết nối.
/// Khóa theo kênh trong tiến trình API; luôn đọc lại token mới nhất trong khóa và ghi token mới ngay sau khi đổi.
/// </summary>
internal static class ContentTiktokTokenGate
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    public static async Task<(ContentTiktokTokens Tokens, ContentRepository.ChannelRow Channel)> RefreshAsync(
        Guid channelId, ContentRepository repo, ContentTiktokClient client, CancellationToken ct)
    {
        var gate = Locks.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var channel = await repo.GetChannelAsync(channelId, ct)
                          ?? throw new InvalidOperationException("Kênh không tồn tại.");
            var refresh = ContentTargetSecrets.ExtractStored(channel.ConfigJson)
                          ?? throw new ContentTiktokAuthException("Chưa kết nối TikTok — vào Kênh TikTok → Kết nối TikTok.");

            var tokens = await client.RefreshAsync(refresh, ct);

            if (!string.IsNullOrWhiteSpace(tokens.RefreshToken) && tokens.RefreshToken != refresh)
            {
                channel.ConfigJson = ContentTargetSecrets.MergeConfig(channel.ConfigJson, channel.ConfigJson, tokens.RefreshToken);
                await SaveAsync(repo, channel);
            }
            return (tokens, channel);
        }
        finally
        {
            gate.Release();
        }
    }

    // TikTok đã đổi token: không được làm mất token mới chỉ vì người dùng hủy yêu cầu, nên ghi không theo ct và thử lại vài lần.
    private static async Task SaveAsync(ContentRepository repo, ContentRepository.ChannelRow channel)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                await repo.UpsertChannelAsync(channel.BrandId, channel, CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), CancellationToken.None);
            }
        }
        throw new ContentTiktokAuthException(
            "TikTok đã đổi mã kết nối nhưng hệ thống không ghi lại được, nên mã cũ không còn dùng được. Vào Kênh TikTok → Kết nối lại. (" + last?.Message + ")");
    }
}
