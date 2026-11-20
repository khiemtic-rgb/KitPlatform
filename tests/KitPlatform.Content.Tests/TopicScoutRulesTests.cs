using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class TopicScoutRulesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T00:00:00Z");

    [Fact]
    public void Vietnamese_chart_video_stays_in_the_vietnamese_lane()
    {
        Assert.Equal("vi", TopicScoutRules.LaneOf("VN", "Mẹ thấy tin nhắn"));
        Assert.Null(TopicScoutRules.LaneOf("US", "Mẹ thấy tin nhắn"));
        Assert.Equal("en", TopicScoutRules.LaneOf("US", "She saw the message"));
        Assert.Null(TopicScoutRules.LaneOf("VN", "She saw the message"));
    }

    [Fact]
    public void Only_a_recent_short_from_a_small_channel_is_kept()
    {
        var clip = Sample("en", "US", "She saw the secret message tonight");
        Assert.True(TopicScoutRules.Keep(clip, Now));
        Assert.False(TopicScoutRules.Keep(clip with { DurationSeconds = 90 }, Now));
        Assert.False(TopicScoutRules.Keep(clip with { Subscribers = 100_000 }, Now));
        Assert.False(TopicScoutRules.Keep(clip with { Views = 49_999 }, Now));
        Assert.False(TopicScoutRules.Keep(clip with { PublishedAt = Now.AddDays(-91) }, Now));
        Assert.False(TopicScoutRules.Keep(clip with { Subscribers = -1 }, Now));
    }

    [Fact]
    public void Three_videos_that_share_a_word_become_one_brief_with_their_real_views()
    {
        var clips = new[]
        {
            Sample("en", "US", "She saw the secret message tonight", "v1", "c1", 80_000),
            Sample("en", "GB", "The secret message on the lock screen", "v2", "c2", 70_000),
            Sample("en", "AU", "A secret message changed dinner", "v3", "c3", 60_000),
            Sample("en", "US", "Totally different football final", "v4", "c4", 90_000),
        };
        var clusters = TopicScoutRules.Cluster(clips);
        var cluster = Assert.Single(clusters);
        Assert.Equal("en", cluster.Lane);
        Assert.Equal(3, cluster.Clips.Count);
        Assert.DoesNotContain(cluster.Clips, clip => clip.VideoId == "v4");
        var (_, brief) = TopicScoutRules.Brief(cluster);
        Assert.Contains("80,000 views", brief, StringComparison.Ordinal);
        Assert.Contains("https://www.youtube.com/watch?v=v1", brief, StringComparison.Ordinal);
        Assert.Contains("When to stop", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_drops_the_wrong_language_for_the_region()
    {
        var json = """
            {"items":[
              {"id":"a","snippet":{"title":"Mẹ thấy tin","channelId":"c","channelTitle":"Nha","publishedAt":"2026-11-01T00:00:00Z"},
               "statistics":{"viewCount":"10"},"contentDetails":{"duration":"PT20S"}},
              {"id":"b","snippet":{"title":"She saw it","channelId":"d","channelTitle":"Home","publishedAt":"2026-11-01T00:00:00Z"},
               "statistics":{"viewCount":"12"},"contentDetails":{"duration":"PT1M"}}
            ]}
            """;
        var vn = TopicScoutChartParser.Videos(json, "VN");
        Assert.Equal("a", Assert.Single(vn).VideoId);
        var us = TopicScoutChartParser.Videos(json, "US");
        Assert.Equal("b", Assert.Single(us).VideoId);
        Assert.Equal(60, TopicScoutRules.DurationSeconds("PT1M"));
        Assert.Equal(20, TopicScoutRules.DurationSeconds("PT20S"));
    }

    private static TopicScoutClip Sample(
        string lane, string region, string title, string videoId = "v", string channelId = "c", long views = 60_000) =>
        new(lane, region, videoId, title, channelId, "Channel " + channelId, views, 10_000, 30, Now.AddDays(-10));
}
