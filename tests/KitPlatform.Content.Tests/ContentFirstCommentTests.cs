using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentFirstCommentTests
{
    static readonly DateTimeOffset Live = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Due_only_after_live_plus_two_minutes()
    {
        Assert.False(ContentFirstCommentRules.IsDue("pending", 0, Live, null, Live.AddMinutes(1)));
        Assert.True(ContentFirstCommentRules.IsDue("pending", 0, Live, null, Live.AddMinutes(2)));
        Assert.False(ContentFirstCommentRules.IsDue("pending", 0, Live, Live.AddMinutes(12), Live.AddMinutes(5)));
        Assert.True(ContentFirstCommentRules.IsDue("pending", 5, Live, Live.AddMinutes(12), Live.AddMinutes(12)));
        Assert.False(ContentFirstCommentRules.IsDue("pending", 6, Live, null, Live.AddHours(2)));
        Assert.False(ContentFirstCommentRules.IsDue("posted", 0, Live, null, Live.AddHours(2)));
    }

    [Fact]
    public void Miss_retries_ten_minutes_apart_then_stops()
    {
        var now = Live.AddMinutes(3);
        var again = ContentFirstCommentRules.AfterMiss(0, now, "Bài chưa công khai.");
        Assert.Equal("pending", again.Status);
        Assert.Equal(1, again.Attempts);
        Assert.Equal(now.AddMinutes(10), again.NextAttemptAt);

        var last = ContentFirstCommentRules.AfterMiss(5, now, "Bài chưa công khai.");
        Assert.Equal("failed", last.Status);
        Assert.Equal(6, last.Attempts);
        Assert.Null(last.NextAttemptAt);
        Assert.Equal("Bài chưa công khai.", last.LastError);
    }

    [Fact]
    public void Facebook_scheduled_goes_to_the_queue()
    {
        Assert.Equal(ContentFirstCommentRules.Action.Queue, ContentFirstCommentRules.DecideFacebook(scheduled: true, hasText: true));
        Assert.Equal(ContentFirstCommentRules.Action.PostNow, ContentFirstCommentRules.DecideFacebook(scheduled: false, hasText: true));
        Assert.Equal(ContentFirstCommentRules.Action.Skip, ContentFirstCommentRules.DecideFacebook(scheduled: true, hasText: false));
    }

    [Fact]
    public void Youtube_private_or_future_publish_waits_public_posts_now()
    {
        var now = Live;
        Assert.Equal(ContentFirstCommentRules.Action.Queue, ContentFirstCommentRules.DecideYoutube("private", null, now, true));
        Assert.Equal(
            ContentFirstCommentRules.Action.Queue,
            ContentFirstCommentRules.DecideYoutube("public", now.AddHours(1), now, true));
        Assert.Equal(ContentFirstCommentRules.Action.PostNow, ContentFirstCommentRules.DecideYoutube("public", now, now, true));
        Assert.Equal(ContentFirstCommentRules.Action.PostNow, ContentFirstCommentRules.DecideYoutube("unlisted", null, now, true));
        Assert.False(ContentFirstCommentRules.YoutubeIsPublic("private"));
        Assert.True(ContentFirstCommentRules.YoutubeIsPublic("public"));
    }

    [Fact]
    public void Old_youtube_token_without_force_ssl_needs_reconnect()
    {
        Assert.False(ContentFirstCommentRules.ScopeAllowsComment(
            "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly"));
        Assert.True(ContentFirstCommentRules.ScopeAllowsComment(
            "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.force-ssl"));
        Assert.True(ContentFirstCommentRules.ScopeAllowsComment(null));
        Assert.True(ContentFirstCommentRules.IsMissingCommentScope(403, "insufficientPermissions"));
        Assert.False(ContentFirstCommentRules.IsMissingCommentScope(403, "commentsDisabled"));
    }

    [Fact]
    public void Comment_uses_marked_section_or_cta_and_ngv_disclaimer()
    {
        var marked = "Mô tả\n\n---\nBình luận đầu:\nNguồn: sách\nhttps://example.com";
        Assert.Equal("Nguồn: sách\nhttps://example.com", ContentFirstCommentRules.Compose(marked, "https://cta", "trading"));

        var ngv = ContentFirstCommentRules.Compose("Chỉ mô tả", "https://nguongiuvon.vn", "trading");
        Assert.Contains("https://nguongiuvon.vn", ngv);
        Assert.Contains(ContentFirstCommentRules.NgvDisclaimer, ngv);

        Assert.Equal("https://famixa.vn", ContentFirstCommentRules.Compose("Chỉ mô tả", "https://famixa.vn", "famixa"));
        Assert.Null(ContentFirstCommentRules.Compose("Chỉ mô tả", null, "famixa"));
    }
}
