using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentPlatformScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid FbId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TopicA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TopicB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static ContentScheduleTarget Fb() =>
        new("channel:fb", "Fanpage", "facebook_page", null, FbId, ["fb_page"]);

    private static ContentScheduleCandidate Cand(Guid id, string title, string? format = null) =>
        new(
            id,
            title,
            "P1",
            Now.AddDays(-1),
            ["fb_page"],
            new Dictionary<string, string?> { ["facebook_page"] = null },
            format);

    private static ContentScheduleSettings Settings(int days = 1, int perDay = 2, int gap = 1, int? shortPerDay = null) =>
        new(days, perDay, gap, ["09:00", "19:30"], 7, shortPerDay);

    [Fact]
    public void Matches_caption_after_stripping_accents_hashtags_and_spaces()
    {
        var candidates = new[]
        {
            new ContentPlatformMatchCandidate(
                TopicA,
                "Bài khác",
                [],
                ["  Người   giữ vốn GIỮ kỷ luật   #novixa mỗi tuần  "],
                []),
            new ContentPlatformMatchCandidate(TopicB, "Chủ đề khác hẳn", [], ["Nội dung không liên quan gì hết"], []),
        };

        var hit = ContentPlatformRules.Match(
            "post-1",
            null,
            "Người giữ vốn giữ kỷ luật #khac mỗi tuần",
            candidates);

        Assert.Equal(TopicA, hit);
        Assert.Equal(
            ContentPlatformRules.CaptionKey("Người giữ vốn giữ kỷ luật #novixa mỗi tuần"),
            ContentPlatformRules.CaptionKey("  Người   giữ vốn GIỮ kỷ luật   #khac mỗi tuần  "));
    }

    [Fact]
    public void Leaves_unmatched_when_two_topics_share_the_caption()
    {
        const string caption = "Người giữ vốn giữ kỷ luật mỗi tuần này";
        var candidates = new[]
        {
            new ContentPlatformMatchCandidate(TopicA, "A", [], [caption], []),
            new ContentPlatformMatchCandidate(TopicB, "B", [], [caption], []),
        };
        Assert.Null(ContentPlatformRules.Match("x", null, caption, candidates));
    }

    [Fact]
    public void Matches_job_external_id_including_facebook_page_prefix()
    {
        var candidates = new[]
        {
            new ContentPlatformMatchCandidate(TopicA, "Tiêu đề khác", [], [], ["1234567890"]),
        };
        Assert.Equal(TopicA, ContentPlatformRules.Match("998877_1234567890", "Không cùng tiêu đề", null, candidates));
    }

    [Fact]
    public void Matches_video_file_name_to_title()
    {
        var candidates = new[]
        {
            new ContentPlatformMatchCandidate(TopicA, "Bài nói về chuyện khác hẳn", ["Nguoi-giu-khac.mp4"], [], []),
            new ContentPlatformMatchCandidate(TopicB, "Khác biệt", ["Dau-tu-an-toan-moi-tuan.mp4"], [], []),
        };
        Assert.Equal(TopicB, ContentPlatformRules.Match("vid", "Đầu tư an toàn mỗi tuần", null, candidates));
    }

    [Fact]
    public void Cancelled_job_plus_platform_schedule_is_not_proposed_again()
    {
        var topic = Cand(TopicA, "Reel đã hẹn");
        var slot = new DateTimeOffset(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);
        var existing = new[]
        {
            new ContentScheduleExistingJob(TopicA, null, FbId, "Cancelled", slot),
            ContentPlatformRules.ToBlockingJob(new ContentPlatformPostSnapshot(
                FbId, "facebook_page", "reel", "scheduled", slot, TopicA)),
        };

        var (proposals, skipped) = ContentScheduleRules.Plan(Now, [Fb()], [topic], existing, Settings(days: 7));

        Assert.DoesNotContain(proposals, p => p.TopicId == TopicA);
        Assert.Equal("Đã có trên Fanpage/YouTube (hẹn giờ 11/11 19:30)", Assert.Single(skipped).Reason);
    }

    [Fact]
    public void Published_platform_post_says_already_on_platform()
    {
        var topic = Cand(TopicA, "Đã lên");
        var existing = new[]
        {
            ContentPlatformRules.ToBlockingJob(new ContentPlatformPostSnapshot(
                FbId, "facebook_page", "post", "published", Now.AddDays(-1), TopicA)),
        };
        var (_, skipped) = ContentScheduleRules.Plan(Now, [Fb()], [topic], existing, Settings());
        Assert.Equal("Đã đăng trên nền tảng", Assert.Single(skipped).Reason);
    }

    [Fact]
    public void Unmatched_platform_post_still_occupies_the_slot_and_short_quota()
    {
        var shortAt = new DateTimeOffset(2026, 11, 2, 2, 0, 0, TimeSpan.Zero);
        var existing = new[]
        {
            ContentPlatformRules.ToBlockingJob(new ContentPlatformPostSnapshot(
                FbId, "facebook_page", "short", "scheduled", shortAt, null)),
        };
        var candidate = Cand(TopicA, "Video ngắn mới", "short");

        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb()], [candidate], existing, Settings(shortPerDay: 1));

        Assert.Empty(proposals);
    }

    [Fact]
    public void Unmatched_post_blocks_the_same_clock_time()
    {
        var evening = new DateTimeOffset(2026, 11, 2, 12, 30, 0, TimeSpan.Zero);
        var existing = new[]
        {
            ContentPlatformRules.ToBlockingJob(new ContentPlatformPostSnapshot(
                FbId, "facebook_page", "post", "scheduled", evening, null)),
        };
        var (proposals, _) = ContentScheduleRules.Plan(
            Now, [Fb()], [Cand(TopicA, "Bài mới"), Cand(TopicB, "Bài sau")], existing, Settings());

        Assert.Single(proposals);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 2, 0, 0, TimeSpan.Zero), proposals[0].ProposedAtUtc);
    }

    [Fact]
    public void RunJob_cancels_when_topic_is_already_on_the_channel()
    {
        Assert.True(ContentPlatformRules.ShouldCancel("facebook_page", FbId, true));
        Assert.True(ContentPlatformRules.ShouldCancel("youtube_channel", FbId, true));
        Assert.False(ContentPlatformRules.ShouldCancel("facebook_page", FbId, false));
        Assert.False(ContentPlatformRules.ShouldCancel("tiktok_channel", FbId, true));
        Assert.Equal("Đã có trên nền tảng", ContentPlatformRules.CancelReason);
    }

    [Fact]
    public void Facebook_edges_keep_scheduled_posts_and_recent_reels()
    {
        var since = Now.AddDays(-30);
        var scheduled = """
            {"data":[{"id":"111_222","message":"Hẹn giờ","scheduled_publish_time":1762878600}]}
            """;
        var oldPost = """
            {"data":[{"id":"old","message":"Cũ","created_time":"2026-01-01T00:00:00Z"}]}
            """;
        var reel = """
            {"data":[{"id":"reel1","description":"Reel mới","created_time":"2026-11-01T00:00:00Z","published":true}]}
            """;
        var video = """
            {"data":[{"id":"reel1","title":"cùng id","length":45,"created_time":"2026-11-01T00:00:00Z"}]}
            """;

        var items = ContentPlatformReaders.Merge(
            ContentPlatformReaders.ParseFacebookEdge("scheduled_posts", scheduled, Now, since)
            .Concat(ContentPlatformReaders.ParseFacebookEdge("published_posts", oldPost, Now, since))
            .Concat(ContentPlatformReaders.ParseFacebookEdge("video_reels", reel, Now, since))
            .Concat(ContentPlatformReaders.ParseFacebookEdge("videos", video, Now, since)));

        var kept = Assert.Single(items, i => i.ExternalId == "111_222");
        Assert.Equal("scheduled", kept.Status);
        Assert.Equal("post", kept.Kind);
        Assert.DoesNotContain(items, i => i.ExternalId == "old");
        var merged = Assert.Single(items, i => i.ExternalId == "reel1");
        Assert.Equal("reel", merged.Kind);
        Assert.Null(ContentPlatformReaders.NextGraphPage("""{"paging":{"next":"https://evil.example/next"}}"""));
    }

    [Fact]
    public void Youtube_publishAt_is_scheduled_and_shorts_are_short()
    {
        var json = """
            {"items":[
              {"id":"vid123","snippet":{"title":"Người giữ vốn","description":"mô tả","publishedAt":"2026-11-01T00:00:00Z"},
               "status":{"privacyStatus":"private","publishAt":"2026-11-11T12:30:00Z"}},
              {"id":"oldvid","snippet":{"title":"Cũ","description":"","publishedAt":"2026-01-01T00:00:00Z"},
               "status":{"privacyStatus":"public"}},
              {"id":"short1","snippet":{"title":"Clip","description":"xem #Shorts","publishedAt":"2026-11-01T03:00:00Z"},
               "status":{"privacyStatus":"public"}}
            ]}
            """;
        var items = ContentPlatformReaders.ParseYoutubeVideos(json, Now, Now.AddDays(-30));
        var scheduled = Assert.Single(items, i => i.ExternalId == "vid123");
        Assert.Equal("scheduled", scheduled.Status);
        Assert.Equal("video", scheduled.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 12, 30, 0, TimeSpan.Zero), scheduled.PublishAt);
        Assert.DoesNotContain(items, i => i.ExternalId == "oldvid");
        var shorts = Assert.Single(items, i => i.ExternalId == "short1");
        Assert.Equal("short", shorts.Kind);
        Assert.Equal("published", shorts.Status);

        Assert.Equal(
            "UU123",
            ContentPlatformReaders.ParseUploadsPlaylistId(
                """{"items":[{"contentDetails":{"relatedPlaylists":{"uploads":"UU123"}}}]}"""));
    }

    [Fact]
    public void Unpublished_flag_or_future_time_is_scheduled_and_created_time_is_not()
    {
        var since = Now.AddDays(-30);
        var reels = """
            {"data":[
              {"id":"reel-future","description":"Reel 19:30","created_time":"2026-01-01T00:00:00Z","published":false,"scheduled_publish_time":"2026-11-24T12:30:00Z"},
              {"id":"reel-flag","description":"Chưa đăng","created_time":"2026-11-01T00:00:00Z","published":false}
            ]}
            """;
        var posts = """
            {"data":[
              {"id":"post-flag","message":"Bài 09:00","created_time":"2026-01-01T00:00:00Z","is_published":false,"scheduled_publish_time":"2026-11-12T02:00:00Z"},
              {"id":"created-only","message":"created không đủ","created_time":"2026-11-20T00:00:00Z","published":true}
            ]}
            """;

        var parsedReels = ContentPlatformReaders.ParseFacebookEdge("video_reels", reels, Now, since);
        var future = Assert.Single(parsedReels, i => i.ExternalId == "reel-future");
        Assert.Equal("scheduled", future.Status);
        Assert.Equal("reel", future.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 12, 30, 0, TimeSpan.Zero), future.PublishAt);
        var flagged = Assert.Single(parsedReels, i => i.ExternalId == "reel-flag");
        Assert.Equal("scheduled", flagged.Status);
        Assert.Null(flagged.PublishAt);

        var parsedPosts = ContentPlatformReaders.ParseFacebookEdge("published_posts", posts, Now, since);
        var post = Assert.Single(parsedPosts, i => i.ExternalId == "post-flag");
        Assert.Equal("scheduled", post.Status);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero), post.PublishAt);
        var createdOnly = Assert.Single(parsedPosts, i => i.ExternalId == "created-only");
        Assert.Equal("published", createdOnly.Status);
    }

    [Fact]
    public void Matches_description_head_and_file_name_when_title_differs()
    {
        var head = "Một nghiên cứu ở Brazil theo dõi người giao dịch trong ngày";
        var description = head + " rồi ghi lại kết quả sau nhiều năm theo dõi thêm cho đủ dài.";
        var candidates = new[]
        {
            new ContentPlatformMatchCandidate(
                TopicA,
                "Tiêu đề khác hẳn",
                [],
                ["Mở đầu ngắn. " + description],
                []),
            new ContentPlatformMatchCandidate(TopicB, "Không liên quan", ["kien-tri-300-phien.mp4"], [], []),
        };

        Assert.Equal(TopicA, ContentPlatformRules.Match("yt1", "Tiêu đề YouTube khác", description, candidates));
        Assert.Equal(
            TopicB,
            ContentPlatformRules.Match("yt2", "Clip kien-tri-300-phien bản đầy đủ trên kênh", null, candidates));
    }

    [Fact]
    public void Four_cancelled_reel_topics_are_not_proposed_on_fanpage()
    {
        var ids = new[]
        {
            Guid.Parse("01a19af9-0ab9-744c-93d3-cc650dc376cc"),
            Guid.Parse("01a19af9-0a41-7da0-95e4-fe7c8adc1389"),
            Guid.Parse("01a19af9-0be4-7e59-8865-2af1d39e846a"),
            Guid.Parse("01a19af9-0d83-7729-88f3-61303f1c9236"),
        };
        var slot = new DateTimeOffset(2026, 11, 12, 12, 30, 0, TimeSpan.Zero);
        var candidates = ids.Select((id, i) => Cand(id, "Reel " + i)).ToList();
        var cancelledOnly = ids.Select(id => new ContentScheduleExistingJob(id, null, FbId, "Cancelled", slot)).ToList();
        var settings = Settings(days: 14, perDay: 2);

        var (open, _) = ContentScheduleRules.Plan(Now, [Fb()], candidates, cancelledOnly, settings);
        Assert.Equal(4, open.Count(p => ids.Contains(p.TopicId)));

        var blocked = cancelledOnly.Concat(ids.Select(id =>
            ContentPlatformRules.ToBlockingJob(new ContentPlatformPostSnapshot(
                FbId, "facebook_page", "reel", "scheduled", slot, id)))).ToList();
        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb()], candidates, blocked, settings);
        Assert.DoesNotContain(proposals, p => ids.Contains(p.TopicId));
    }
}
