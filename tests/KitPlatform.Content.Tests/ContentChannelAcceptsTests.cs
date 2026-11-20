using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentChannelAcceptsTests
{
    [Fact]
    public void Missing_accepts_allows_video_and_post()
    {
        Assert.Null(ContentChannelAccepts.Parse(null));
        Assert.Null(ContentChannelAccepts.Parse("{}"));
        Assert.Null(ContentChannelAccepts.Parse("""{"facebook":{"status":"CONNECTED"}}"""));
        Assert.Null(ContentChannelAccepts.Parse("not-json"));
        Assert.Null(ContentChannelAccepts.Parse("""{"accepts":[]}"""));
        Assert.Null(ContentChannelAccepts.Parse("""{"accepts":["reel"]}"""));

        Assert.True(ContentChannelAccepts.Allows("{}", ContentChannelAccepts.Video));
        Assert.True(ContentChannelAccepts.Allows("{}", ContentChannelAccepts.Post));
    }

    [Fact]
    public void Post_only_rejects_video()
    {
        const string json = """{"accepts":["post"]}""";
        Assert.False(ContentChannelAccepts.Allows(json, ContentChannelAccepts.Video));
        Assert.True(ContentChannelAccepts.Allows(json, ContentChannelAccepts.Post));
        Assert.Equal(
            "Fanpage «Famixa» chỉ nhận bài viết, bỏ qua video.",
            ContentChannelAccepts.SkipWarning("Famixa", ContentChannelAccepts.Video));
    }

    [Fact]
    public void Video_only_rejects_post()
    {
        const string json = """{"accepts":["Video"]}""";
        Assert.True(ContentChannelAccepts.Allows(json, ContentChannelAccepts.Video));
        Assert.False(ContentChannelAccepts.Allows(json, ContentChannelAccepts.Post));
        Assert.Equal(
            "Fanpage «Famixa - Vũ trụ phim» chỉ nhận video, bỏ qua bài viết.",
            ContentChannelAccepts.SkipWarning("Famixa - Vũ trụ phim", ContentChannelAccepts.Post));
    }

    [Fact]
    public void Schedule_skips_video_on_a_post_only_page()
    {
        var page = new ContentScheduleTarget(
            "channel:parent", "Famixa", "facebook_page", null, Guid.NewGuid(),
            ["fb_page", "fb_short", "social_caption"],
            ["post"]);
        var film = new ContentScheduleCandidate(
            Guid.NewGuid(), "Tập 1", "P1", DateTimeOffset.UtcNow,
            ["fb_page", "video_asset"],
            new Dictionary<string, string?> { ["facebook_page"] = null },
            Format: "long",
            Connectors: ["facebook_page"]);
        var news = new ContentScheduleCandidate(
            Guid.NewGuid(), "Tin", "P1", DateTimeOffset.UtcNow,
            ["fb_page"],
            new Dictionary<string, string?> { ["facebook_page"] = null });

        Assert.False(ContentScheduleRules.IsEligible(film, page));
        Assert.True(ContentScheduleRules.IsEligible(news, page));
    }
}
