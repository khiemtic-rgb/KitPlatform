using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ContentChannelFrameworkTests
{
    [Fact]
    public void Build_ranks_measured_posts_and_does_not_invent_views()
    {
        var text = ContentChannelFramework.Build(
        [
            new WatchedChannelPost("youtube_channel", "Bài ít xem", 40, 1, null, true),
            new WatchedChannelPost("facebook_page", "Bài được xem", 900, 20, null, true),
        ]);

        Assert.Contains(ContentChannelFramework.Marker, text);
        Assert.Contains("900 lượt xem", text);
        Assert.Contains("Bài được xem", text);
        var popular = text.IndexOf("Bài được xem", StringComparison.Ordinal);
        var quiet = text.IndexOf("Bài ít xem", StringComparison.Ordinal);
        Assert.True(popular >= 0 && popular < quiet);
        Assert.DoesNotContain("nhiều người xem", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_without_measurements_does_not_call_a_post_popular()
    {
        var text = ContentChannelFramework.Build(
        [
            new WatchedChannelPost("fb_page", "Mở quầy sáng", null, null, null, false),
        ]);

        Assert.Contains("Chưa có số lượt xem", text);
        Assert.Contains("Không xếp bài nào là phổ biến", text);
    }

    [Fact]
    public void Extract_returns_only_the_framework_block()
    {
        var block = ContentChannelFramework.Build([]);
        var outline = "Dàn ý tập 1\n\n" + block;
        Assert.StartsWith(ContentChannelFramework.Marker, ContentChannelFramework.Extract(outline));
        Assert.Null(ContentChannelFramework.Extract("không có khung"));
    }
}
