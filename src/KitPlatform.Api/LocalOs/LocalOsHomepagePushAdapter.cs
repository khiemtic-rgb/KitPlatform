using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Api.LocalOs;

public sealed class LocalOsHomepagePushAdapter : ILocalOsHomepagePush
{
    private readonly LocalOsHomepageFeedPublisher _publisher;
    private readonly ILocalOsListingService _listings;
    private readonly ILogger<LocalOsHomepagePushAdapter> _log;

    public LocalOsHomepagePushAdapter(
        LocalOsHomepageFeedPublisher publisher,
        ILocalOsListingService listings,
        ILogger<LocalOsHomepagePushAdapter> log)
    {
        _publisher = publisher;
        _listings = listings;
        _log = log;
    }

    public async Task PushAfterTrustedPublishAsync(
        int createdCount,
        CancellationToken cancellationToken = default)
    {
        if (createdCount <= 0) return;
        try
        {
            var jobs = await _listings.ListAsync(
                new LocalListingQuery("job", null, null, null, PublicOnly: true),
                cancellationToken);
            if (jobs.Count < 8)
            {
                _log.LogWarning(
                    "Watch tạo {Created} tin nhưng chưa đẩy trang chủ — việc công khai còn {Jobs}.",
                    createdCount,
                    jobs.Count);
                return;
            }

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
