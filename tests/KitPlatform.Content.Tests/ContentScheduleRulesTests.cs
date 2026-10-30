using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentScheduleRulesTests
{
    // 2026-11-02 00:00 UTC = 07:00 giờ Việt Nam (thứ Hai).
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid FbId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SiteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ContentScheduleTarget Fb() =>
        new("channel:fb", "Fanpage", "facebook_page", null, FbId, ["fb_page", "social_caption"]);

    private static ContentScheduleTarget Site() =>
        new("site:web", "Website", "astro_git", SiteId, null, ["web_long"]);

    private static ContentScheduleCandidate Cand(
        string title,
        string priority = "P1",
        int readyDaysAgo = 1,
        string[]? kinds = null,
        string? fbBlock = null,
        string? siteBlock = null) =>
        new(
            Guid.NewGuid(),
            title,
            priority,
            Now.AddDays(-readyDaysAgo),
            kinds ?? ["fb_page", "web_long"],
            new Dictionary<string, string?>
            {
                ["facebook_page"] = fbBlock,
                ["astro_git"] = siteBlock,
            });

    private static ContentScheduleSettings Settings(int days = 3, int perDay = 1, int gap = 6) =>
        new(days, perDay, gap, ["09:00", "19:30"], 7);

    [Fact]
    public void Respects_max_per_day_per_target()
    {
        var cands = Enumerable.Range(0, 10).Select(i => Cand("Bài " + i)).ToList();
        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb()], cands, [], Settings(days: 3, perDay: 1));
        var perDay = proposals.GroupBy(p => p.ProposedAtLocal[..10]).Select(g => g.Count());
        Assert.All(perDay, n => Assert.True(n <= 1));
        Assert.Equal(3, proposals.Count);
    }

    [Fact]
    public void Keeps_minimum_gap_between_posts_on_same_target()
    {
        var cands = Enumerable.Range(0, 10).Select(i => Cand("Bài " + i)).ToList();
        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb()], cands, [], Settings(days: 2, perDay: 2, gap: 12));
        var times = proposals.Select(p => p.ProposedAtUtc).OrderBy(t => t).ToList();
        for (var i = 1; i < times.Count; i++)
            Assert.True((times[i] - times[i - 1]).TotalHours >= 12);
    }

    [Fact]
    public void Higher_priority_goes_first_then_oldest()
    {
        var p0 = Cand("Khẩn", priority: "P0", readyDaysAgo: 1);
        var oldP1 = Cand("Cũ", priority: "P1", readyDaysAgo: 9);
        var newP1 = Cand("Mới", priority: "P1", readyDaysAgo: 1);
        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb()], [newP1, oldP1, p0], [], Settings(days: 3));
        Assert.Equal(new[] { "Khẩn", "Cũ", "Mới" }, proposals.OrderBy(p => p.ProposedAtUtc).Select(p => p.Title).ToArray());
    }

    [Fact]
    public void Skips_topic_blocked_by_quality_gate_and_says_why()
    {
        var bad = Cand("Hứa lãi", fbBlock: "Câu hứa lãi không được phép");
        var ok = Cand("Bình thường");
        var (proposals, skipped) = ContentScheduleRules.Plan(Now, [Fb()], [bad, ok], [], Settings());
        Assert.DoesNotContain(proposals, p => p.TopicId == bad.TopicId);
        var s = Assert.Single(skipped, x => x.TopicId == bad.TopicId);
        Assert.Contains("cổng chất lượng", s.Reason);
        Assert.Contains("Câu hứa lãi", s.Reason);
    }

    [Fact]
    public void Does_not_propose_topic_that_already_has_a_job_for_the_target()
    {
        var c = Cand("Đã có job");
        var existing = new[] { new ContentScheduleExistingJob(c.TopicId, null, FbId, "Queued", Now.AddHours(5)) };
        var (proposals, skipped) = ContentScheduleRules.Plan(Now, [Fb()], [c], existing, Settings());
        Assert.Empty(proposals);
        Assert.Contains("Đã có job", Assert.Single(skipped).Reason);
    }

    [Fact]
    public void Same_topic_is_never_proposed_twice_for_one_target()
    {
        var c = Cand("Chỉ một bài");
        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb()], [c], [], Settings(days: 5, perDay: 2));
        Assert.Single(proposals);
    }

    [Fact]
    public void Only_uses_targets_with_matching_variant_kind()
    {
        var onlyFb = Cand("Chỉ có bản FB", kinds: ["fb_page"]);
        var (proposals, _) = ContentScheduleRules.Plan(Now, [Fb(), Site()], [onlyFb], [], Settings());
        Assert.All(proposals, p => Assert.Equal("facebook_page", p.ConnectorType));
    }

    [Fact]
    public void Does_not_propose_slots_in_the_past()
    {
        // 09:00 giờ VN đã qua nếu bây giờ là 10:00 giờ VN (03:00 UTC).
        var now = new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);
        var cands = Enumerable.Range(0, 5).Select(i => Cand("Bài " + i)).ToList();
        var (proposals, _) = ContentScheduleRules.Plan(now, [Fb()], cands, [], Settings(days: 1, perDay: 2, gap: 1));
        Assert.All(proposals, p => Assert.True(p.ProposedAtUtc > now));
    }

    [Fact]
    public void No_targets_means_nothing_proposed_and_clear_reason()
    {
        var c = Cand("Không có nơi đăng");
        var (proposals, skipped) = ContentScheduleRules.Plan(Now, [], [c], [], Settings());
        Assert.Empty(proposals);
        Assert.Contains("nơi đăng", Assert.Single(skipped).Reason);
    }

    [Fact]
    public void Plan_is_deterministic()
    {
        var cands = Enumerable.Range(0, 6).Select(i => Cand("Bài " + i, readyDaysAgo: i + 1)).ToList();
        var a = ContentScheduleRules.Plan(Now, [Fb(), Site()], cands, [], Settings(days: 4));
        var b = ContentScheduleRules.Plan(Now, [Fb(), Site()], cands, [], Settings(days: 4));
        Assert.Equal(
            a.Proposals.Select(p => (p.TopicId, p.TargetKey, p.ProposedAtUtc)),
            b.Proposals.Select(p => (p.TopicId, p.TargetKey, p.ProposedAtUtc)));
    }
}
