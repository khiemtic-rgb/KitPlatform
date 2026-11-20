using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentPagePostRulesTests
{
    [Fact]
    public void Slot_is_15_00_and_not_a_video_clock()
    {
        Assert.Equal(new TimeOnly(15, 0), ContentPagePostRules.Slot);
        foreach (var video in new[] { "07:00", "09:00", "10:00", "12:00", "19:30", "20:00" })
            Assert.NotEqual(TimeOnly.Parse(video), ContentPagePostRules.Slot);
        foreach (var slot in ContentScheduleRules.DefaultLocalSlots)
            Assert.NotEqual(TimeOnly.Parse(slot), ContentPagePostRules.Slot);
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData("post", false, true)]
    [InlineData("long", false, false)]
    [InlineData("short", false, false)]
    [InlineData(null, true, false)]
    [InlineData("post", true, false)]
    public void IsArticle_rejects_video_plan_and_video_file(string? format, bool hasVideo, bool article)
    {
        Assert.Equal(article, ContentPagePostRules.IsArticle(format, hasVideo));
    }

    [Fact]
    public void NextOpen_uses_today_at_15_00_then_skips_a_taken_day()
    {
        var now = new DateTimeOffset(2026, 11, 14, 1, 0, 0, TimeSpan.Zero);
        var today = ContentPagePostRules.NextOpen(now, []);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 8, 0, 0, TimeSpan.Zero), today);

        var tomorrow = ContentPagePostRules.NextOpen(now, [today!.Value]);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero), tomorrow);
    }

    [Fact]
    public void NextOpen_moves_to_tomorrow_after_15_00_and_ignores_a_19_30_video()
    {
        var now = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
        var video = new DateTimeOffset(2026, 11, 14, 12, 30, 0, TimeSpan.Zero);
        var slot = ContentPagePostRules.NextOpen(now, [video]);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero), slot);
    }
}
