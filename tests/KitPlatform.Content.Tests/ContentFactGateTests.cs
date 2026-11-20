using System.Text.Json;
using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ContentFactGateTests
{
    private const string Card =
        "Khi đơn cũ còn trong nhà, hỏi dược sĩ trước khi dùng lại. Gọi quầy 0914.960.069. Không chia thuốc người lớn cho trẻ.";

    [Fact]
    public void Missing_card_is_not_ready()
    {
        var result = ContentFactGate.Evaluate("", "Tiêu đề", ["Một bài."], [], []);
        Assert.True(result.Applies);
        Assert.False(result.ReadyToWrite);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Card_article_without_new_claims_passes()
    {
        var body = "Khi đơn cũ còn trong nhà, hỏi dược sĩ trước khi dùng lại. Gọi quầy 0914.960.069.";
        var result = ContentFactGate.Evaluate(Card, "Hỏi dược sĩ trước khi dùng lại đơn cũ", [body], [], ["Novixa"]);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Dose_and_cure_are_refused()
    {
        var body = Card + " Uống 500mg sẽ chữa khỏi.";
        var result = ContentFactGate.Evaluate(Card, "Hỏi lại dược sĩ", [body], [], []);
        Assert.False(result.Passed);
        Assert.Contains(result.Issues, i => i.Contains("liều"));
        Assert.Contains(result.Issues, i => i.Contains("chữa"));
    }

    [Fact]
    public void Price_must_be_in_the_card()
    {
        var missing = ContentFactGate.Evaluate(Card, "Hỏi dược sĩ", [Card + " Giá 150.000 đồng."], [], []);
        Assert.Contains(missing.Issues, i => i.Contains("Giá"));

        var cardWithPrice = Card + " Giá niêm yết 150.000 đồng tại quầy.";
        var kept = ContentFactGate.Evaluate(cardWithPrice, "Hỏi dược sĩ", ["Giá niêm yết 150.000 đồng tại quầy."], [], []);
        Assert.DoesNotContain(kept.Issues, i => i.Contains("Giá") || i.Contains("Số"));
    }

    [Fact]
    public void Other_brand_and_repeated_thesis_are_refused()
    {
        var result = ContentFactGate.Evaluate(
            Card,
            "Đơn thuốc cũ, khi nào nên hỏi lại dược sĩ",
            ["Khi đơn cũ còn trong nhà, hỏi dược sĩ. Novixa giúp nhà thuốc."],
            ["Đơn thuốc cũ, khi nào nên hỏi lại dược sĩ"],
            ["Novixa"]);
        Assert.Contains(result.Issues, i => i.Contains("Novixa"));
        Assert.Contains(result.Issues, i => i.Contains("Trùng luận điểm"));
    }

    [Fact]
    public void Site_url_does_not_count_as_another_brand()
    {
        var body = Card + " Xem thêm tại https://xuanhoa.novixa.vn/kien-thuc.";
        var result = ContentFactGate.Evaluate(Card, "Hỏi dược sĩ trước khi ra quầy", [body], [], ["Novixa", "novixa"]);
        Assert.DoesNotContain(result.Issues, i => i.Contains("brand"));
    }

    [Fact]
    public void Merge_keeps_existing_continuity_fields()
    {
        var merged = ContentFactGate.MergeCard("{\"previousSummary\":\"đã hỏi\"}", Card);
        using var doc = JsonDocument.Parse(merged);
        Assert.Equal("đã hỏi", doc.RootElement.GetProperty("previousSummary").GetString());
        Assert.Equal(Card, ContentFactGate.ReadCard(merged));
        Assert.Equal("", ContentFactGate.ReadCard(ContentFactGate.MergeCard(merged, "")));
    }

    [Fact]
    public void Reserve_keeps_one_spare_beyond_the_weekly_posts()
    {
        Assert.Equal(3, ContentPublishPlanRules.ReserveTarget(2));
        Assert.Equal(2, ContentPublishPlanRules.ReserveTarget(1));
    }
}
