using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class NarrativeFitIntegrityTests
{
    private static readonly NarrativeModelSuggestion HostileCore = new(
        "core", "famixa.core.habits", true, 95, "Cứu bằng một góc khác", "score cao", []);

    [Fact]
    public void Test1_off_brand_fit_does_not_materialize_angle()
    {
        var decision = BrandNarrativeFit.Reconcile(
            BrandNarrativeCatalog.ByCode("famixa"),
            "7 thói quen tốt cho người cao tuổi.",
            HostileCore);

        Assert.False(decision.Fit);
        Assert.Equal("off-brand", decision.Territory);
        Assert.Null(decision.BrandAngle);
        Assert.False(BrandNarrativeFit.ShouldMaterializeAngle(decision, createPackages: true, includeMaybe: true, legacyVerdict: "fit"));
        Assert.True(BrandNarrativeFit.BlocksAngle(decision.Territory, decision.Fit));
    }

    [Fact]
    public void Test2_off_brand_package_cannot_enter_series()
    {
        var package = Fit("famixa", "fit", 92, "off-brand", false);
        Assert.Equal(BrandNarrativeFit.OffBrandStop, BrandNarrativeFit.SeriesEntryBlock(package));
    }

    [Fact]
    public void Test3_legacy_package_without_territory_is_not_core()
    {
        var legacy = Fit("famixa", "fit", 99, territory: null, narrativeFit: null);
        var block = BrandNarrativeFit.SeriesEntryBlock(legacy);
        Assert.Equal(BrandNarrativeFit.NarrativeFitRequired, block);
        Assert.NotEqual("core", block);
    }

    [Fact]
    public void Test4_core_package_can_enter_series()
    {
        var package = Fit("xuanhoa", "fit", 80, "core", true);
        Assert.Null(BrandNarrativeFit.SeriesEntryBlock(package));
    }

    [Fact]
    public void Test5_supporting_package_can_enter_series()
    {
        var package = Fit("famixa", "fit", 80, "supporting", true);
        Assert.Null(BrandNarrativeFit.SeriesEntryBlock(package));
    }

    [Fact]
    public void Test6_adjacent_package_keeps_territory_and_can_enter_series()
    {
        var package = Fit("vandinhtra", "maybe", 60, "adjacent", true);
        Assert.Null(BrandNarrativeFit.SeriesEntryBlock(package));
        Assert.Equal("adjacent", package.Territory);
    }

    [Fact]
    public void Smoke_famixa_core_materializes_angle()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("famixa"),
            "Những điều nhỏ bé mỗi ngày tạo nên cách một đứa trẻ lớn lên.");
        Assert.Equal("core", decision.Territory);
        Assert.True(decision.Fit);
        Assert.True(BrandNarrativeFit.ShouldMaterializeAngle(decision, true, false, "skip"));
        Assert.Null(BrandNarrativeFit.SeriesEntryBlock(decision.Territory, decision.Fit));
    }

    [Fact]
    public void Smoke_famixa_supporting_allows_angle()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("famixa"),
            "Ông bà có một cách rất khác khi dạy trẻ.");
        Assert.Equal("supporting", decision.Territory);
        Assert.True(decision.Fit);
        Assert.True(decision.AllowsAngle);
        Assert.True(BrandNarrativeFit.ShouldMaterializeAngle(decision, true, false, "skip"));
    }

    [Fact]
    public void Smoke_famixa_off_brand_creates_no_angle()
    {
        var decision = BrandNarrativeFit.Reconcile(
            BrandNarrativeCatalog.ByCode("famixa"),
            "7 thói quen tốt cho người cao tuổi.",
            HostileCore);
        Assert.Equal("off-brand", decision.Territory);
        Assert.False(decision.Fit);
        Assert.False(BrandNarrativeFit.ShouldMaterializeAngle(decision, true, true, "fit"));
        Assert.Equal(BrandNarrativeFit.OffBrandStop, BrandNarrativeFit.SeriesEntryBlock(decision.Territory, decision.Fit));
    }

    [Fact]
    public void Smoke_novixa_tea_creates_no_angle()
    {
        var decision = BrandNarrativeFit.Reconcile(
            BrandNarrativeCatalog.ByCode("novixa"),
            "Cách pha một ấm trà ngon.",
            new NarrativeModelSuggestion("core", "novixa.core.pharmacy-operations", true, 88, "Pha trà cho nhà thuốc", null, []));
        Assert.Equal("off-brand", decision.Territory);
        Assert.False(decision.Fit);
        Assert.False(BrandNarrativeFit.ShouldMaterializeAngle(decision, true, true, "fit"));
    }

    [Fact]
    public void Smoke_xuanhoa_counter_allows_angle()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("xuanhoa"),
            "Khách đến quầy cần được dược sĩ tư vấn đúng cách.");
        Assert.Equal("core", decision.Territory);
        Assert.True(decision.Fit);
        Assert.True(BrandNarrativeFit.ShouldMaterializeAngle(decision, true, false, "skip"));
    }

    [Fact]
    public void Quality_gate_blocks_stored_off_brand_and_ignores_missing_territory()
    {
        var off = ContentQualityGate.Evaluate(
            ContentBrandKnowledge.Empty,
            new ContentCoreIdeaDto(null, null, null, [], null),
            "angle",
            [],
            "Famixa",
            new ContentCreativeBriefDto("Giữ một việc nhỏ.", null, "article"),
            "off-brand",
            false);
        Assert.Contains(ContentQualityGate.NarrativeBoundary, off.Issues);
        Assert.False(off.CanApprove);
        Assert.False(off.CanPublish);

        var legacy = ContentQualityGate.Evaluate(
            ContentBrandKnowledge.Empty,
            new ContentCoreIdeaDto(null, null, null, [], null),
            "angle",
            [],
            "Famixa",
            new ContentCreativeBriefDto("Giữ một việc nhỏ.", null, "article"));
        Assert.DoesNotContain(legacy.Issues, issue => issue.Contains("Narrative boundary", StringComparison.Ordinal));
    }

    private static ContentBrandFitDto Fit(string code, string verdict, int score, string? territory, bool? narrativeFit) =>
        new(
            Guid.NewGuid(),
            code,
            code,
            verdict,
            score,
            "legacy score must not choose territory",
            null,
            null,
            null,
            null,
            null,
            null,
            narrativeFit,
            territory);
}
