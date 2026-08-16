using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Api.LocalOs;

public sealed class LocalOsHomepagePushAdapter : ILocalOsHomepagePush
{
    private readonly LocalOsHomepageFeedPublisher _publisher;
    private readonly ILogger<LocalOsHomepagePushAdapter> _log;

    public LocalOsHomepagePushAdapter(
        LocalOsHomepageFeedPublisher publisher,
        ILogger<LocalOsHomepagePushAdapter> log)
    {
        _publisher = publisher;
        _log = log;
    }

    public async Task PushAfterTrustedPublishAsync(
        int createdCount,
        CancellationToken cancellationToken = default)
    {
        if (createdCount <= 0) return;
        try
        {
            var feed = await _publisher.PublishAsync(cancellationToken);
            if (!feed.Ok)
                _log.LogWarning("Watch đăng tin nhưng chưa đẩy trang chủ: {Message}", feed.Message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Watch đăng tin nhưng không đẩy được trang chủ.");
        }
    }
}
