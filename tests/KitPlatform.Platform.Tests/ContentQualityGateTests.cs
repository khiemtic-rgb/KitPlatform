using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class ContentQualityGateTests
{
    [Fact]
    public void Thin_web_long_blocks_publish()
    {
        var gate = ContentQualityGate.Evaluate(
            Brain(),
            Core(),
            "Góc riêng",
            [("web_long", "Bài quá ngắn, không có mục.")],
            "Novixa",
            Brief());

        Assert.False(gate.CanPublish);
        Assert.Contains(gate.BlockingIssues ?? [], i => i.Contains("quá mỏng"));
    }

    [Fact]
    public void Group_promo_does_not_block_publish()
    {
        var web = new string('a', 2300) + "\n## Một\n" + new string('b', 200) + "\n## Hai\n" + new string('c', 200);
        var gate = ContentQualityGate.Evaluate(
            Brain(),
            Core(),
            "Góc riêng",
            [("web_long", web), ("group_suggested", "Mua ngay sản phẩm của chúng tôi http://x.com")],
            "Novixa",
            Brief());

        Assert.True(gate.CanPublish);
        Assert.False(gate.Passed);
        Assert.Contains(gate.Issues, i => i.Contains("group_suggested"));
    }

    [Fact]
    public void Missing_brief_blocks_approve_not_publish()
    {
        var web = new string('a', 2300) + "\n## Một\n" + new string('b', 200) + "\n## Hai\n" + new string('c', 200);
        var gate = ContentQualityGate.Evaluate(
            Brain(),
            Core(),
            "Góc riêng",
            [("web_long", web)],
            "Novixa",
            null);

        Assert.True(gate.CanPublish);
        Assert.False(gate.CanApprove);
        Assert.Contains(ContentQualityGate.BriefMissing, gate.ApproveIssues ?? []);
    }

    [Fact]
    public void Forbidden_claim_blocks_publish()
    {
        var gate = ContentQualityGate.Evaluate(
            Brain("chữa khỏi"),
            Core(),
            "Góc riêng",
            [("fb_page", "Thuốc này chữa khỏi trong 3 ngày.")],
            "Novixa",
            Brief());

        Assert.False(gate.CanPublish);
        Assert.Contains(gate.BlockingIssues ?? [], i => i.Contains("claim cấm"));
    }

    private static ContentBrandKnowledgeDto Brain(params string[] forbidden) =>
        new(
            null, null, [], [], [], [], [],
            null, null, null, null, null,
            [], [], [], [], [], forbidden,
            [], [], [], [], [], [], []);

    private static ContentCoreIdeaDto Core() =>
        new(null, null, null, [], null);

    private static ContentCreativeBriefDto Brief() =>
        new("traffic", "calm", "web_article", null, 60);
}
