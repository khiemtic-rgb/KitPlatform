using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentBrandKnowledgeUnitsTests
{
    private const string ToneWithUnits =
        "{\"positioning\":\"Kênh giáo dục quản trị rủi ro cho trader cá nhân.\"," +
        "\"claimsForbidden\":[\"nên mua\",\"chắc chắn lãi\"]," +
        "\"proofPoints\":[\"Lot = vốn x %rủi ro / (SL pip x giá trị pip)\"]," +
        "\"goodExamples\":[\"Vốn 10.000 USD rủi ro 1% là 100 USD\"]," +
        "\"competitors\":[\"Các kênh dạy trade\"]," +
        "\"knowledgeUnits\":[" +
        "\"A1 Sự thật về nghề trade: sống sót trước kiếm tiền sau\"," +
        "\"B2 Định cỡ vị thế: Lot = (vốn x %rủi ro) / (SL pip x giá trị pip)\"," +
        "\"C1 FOMO và cách dừng lại\"]}";

    [Fact]
    public void Units_round_trip_through_tone_json()
    {
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null);
        Assert.Equal(3, ContentBrandKnowledge.Units(k).Count);

        var (tone, visual) = ContentBrandKnowledge.Serialize(k);
        var again = ContentBrandKnowledge.Parse(tone, visual);
        Assert.Equal(ContentBrandKnowledge.Units(k), ContentBrandKnowledge.Units(again));
    }

    [Fact]
    public void Legacy_brand_without_units_is_unchanged()
    {
        var k = ContentBrandKnowledge.Parse("{\"positioning\":\"Định vị đủ dài cho thương hiệu cũ.\"}", null);
        Assert.Null(k.KnowledgeUnits);
        Assert.Empty(ContentBrandKnowledge.Units(k));
        Assert.Empty(ContentBrandKnowledge.SelectKnowledge(k, "bất kỳ", 2000));
        Assert.Equal("", ContentBrandKnowledge.FormatForSeriesDraft(ContentBrandKnowledge.Empty, null));
    }

    [Fact]
    public void Unit_with_matching_code_is_selected_first()
    {
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null);
        var picked = ContentBrandKnowledge.SelectKnowledge(k, "Video B2 tính khối lượng lệnh", 2000);
        Assert.StartsWith("B2 ", picked[0]);
    }

    [Fact]
    public void Words_without_diacritics_still_match()
    {
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null);
        var picked = ContentBrandKnowledge.SelectKnowledge(k, "fomo", 2000);
        Assert.StartsWith("C1 ", picked[0]);
    }

    [Fact]
    public void Selection_respects_budget()
    {
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null);
        var picked = ContentBrandKnowledge.SelectKnowledge(k, null, 70);
        Assert.Single(picked);
        Assert.StartsWith("A1 ", picked[0]);
    }

    [Fact]
    public void Series_draft_keeps_forbidden_claims_and_fits_budget()
    {
        var units = Enumerable.Range(1, 80).Select(i => "D" + (i % 9) + " Đơn vị tri thức số " + i + " " + new string('x', 120)).ToList();
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null) with { KnowledgeUnits = units };
        var draft = ContentBrandKnowledge.FormatForSeriesDraft(k, new string('b', 3000), 4500, focus: "D3");
        Assert.True(draft.Length <= 4500, "draft length " + draft.Length);
        Assert.Contains("Claims FORBIDDEN: nên mua; chắc chắn lãi", draft);
        Assert.Contains("Brand knowledge", draft);
        Assert.DoesNotContain("Famixa", draft);
    }

    [Fact]
    public void Update_without_units_keeps_saved_units()
    {
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null);
        var (savedTone, _) = ContentBrandKnowledge.Serialize(k);
        var (incomingTone, _) = ContentBrandKnowledge.Serialize(k with { KnowledgeUnits = null });

        var merged = ContentBrandKnowledge.CarryKnowledgeUnits(incomingTone, savedTone);
        Assert.Equal(3, ContentBrandKnowledge.Units(ContentBrandKnowledge.Parse(merged, null)).Count);
    }

    [Fact]
    public void Update_with_new_units_replaces_saved_units()
    {
        var k = ContentBrandKnowledge.Parse(ToneWithUnits, null);
        var (savedTone, _) = ContentBrandKnowledge.Serialize(k);
        var (incomingTone, _) = ContentBrandKnowledge.Serialize(k with { KnowledgeUnits = ["E1 Forex"] });

        var merged = ContentBrandKnowledge.CarryKnowledgeUnits(incomingTone, savedTone);
        Assert.Equal(["E1 Forex"], ContentBrandKnowledge.Units(ContentBrandKnowledge.Parse(merged, null)));
    }
}
