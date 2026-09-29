using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class BrandNarrativeFitTests
{
    [Fact]
    public void Test1_Famixa_child_growth_is_core()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("famixa"),
            "Những điều nhỏ bé mỗi ngày tạo nên cách một đứa trẻ lớn lên.");

        Assert.True(decision.Fit);
        Assert.Equal("core", decision.Territory);
        Assert.Equal("famixa.core.parenting", decision.TerritoryId);
        Assert.True(decision.AllowsAngle);
    }

    [Fact]
    public void Test2_Famixa_grandparents_teaching_is_supporting()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("famixa"),
            "Ông bà có một cách rất khác khi dạy trẻ.");

        Assert.True(decision.Fit);
        Assert.Equal("supporting", decision.Territory);
        Assert.Equal("famixa.supporting.grandparents", decision.TerritoryId);
        Assert.True(decision.AllowsAngle);
    }

    [Fact]
    public void Test3_Famixa_elderly_habits_are_off_brand_and_block_angle_and_series()
    {
        var idea = "7 thói quen tốt cho người cao tuổi.";
        var model = new NarrativeModelSuggestion("core", "famixa.core.habits", true, 90, "Góc ông bà sống khỏe", "score cao", []);
        var decision = BrandNarrativeFit.Reconcile(BrandNarrativeCatalog.ByCode("famixa"), idea, model);

        Assert.False(decision.Fit);
        Assert.Equal("off-brand", decision.Territory);
        Assert.Null(decision.BrandAngle);
        Assert.False(decision.AllowsAngle);
        Assert.False(decision.AllowsSeries);
        Assert.Contains(decision.BoundaryWarnings, w => w.Contains("ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Test4_Novixa_tea_is_off_brand()
    {
        var decision = BrandNarrativeFit.Reconcile(
            BrandNarrativeCatalog.ByCode("novixa"),
            "Cách pha một ấm trà ngon.",
            new NarrativeModelSuggestion("core", "novixa.core.pharmacy-operations", true, 88, "Pha trà cho nhà thuốc", null, []));

        Assert.False(decision.Fit);
        Assert.Equal("off-brand", decision.Territory);
        Assert.False(decision.AllowsAngle);
        Assert.False(decision.AllowsSeries);
    }

    [Fact]
    public void Test5_XuanHoa_counter_advice_is_core()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("xuanhoa"),
            "Khách đến quầy cần được dược sĩ tư vấn đúng cách.");

        Assert.True(decision.Fit);
        Assert.Equal("core", decision.Territory);
        Assert.Equal("xuanhoa.core.counter", decision.TerritoryId);
    }

    [Fact]
    public void Test6_ThaiNguyenLife_weekend_is_core()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("tnlife"),
            "Cuối tuần này ở Thái Nguyên có gì để đi và trải nghiệm?");

        Assert.True(decision.Fit);
        Assert.Equal("core", decision.Territory);
        Assert.Equal("tnlife.core.experience", decision.TerritoryId);
    }

    [Fact]
    public void Test7_VanDinh_tea_ritual_is_core()
    {
        var decision = BrandNarrativeFit.Classify(
            BrandNarrativeCatalog.ByCode("vandinhtra"),
            "Cách pha trà và cảm nhận hương trà.");

        Assert.True(decision.Fit);
        Assert.Equal("core", decision.Territory);
        Assert.Equal("vandinhtra.core.ritual", decision.TerritoryId);
    }

    [Fact]
    public void Famixa_prompt_marks_dna_authoritative_and_not_the_old_brief()
    {
        var block = BrandNarrativeFit.FormatAuthoritative(BrandNarrativeCatalog.ByCode("famixa"));
        Assert.Contains("AUTHORITATIVE NARRATIVE SOURCE", block);
        Assert.Contains("người cao tuổi", block);
        Assert.DoesNotContain("OPERATIONAL BRIEF", block);
    }

    [Fact]
    public void Migration_sql_matches_catalog_and_fixes_tnlife_cta()
    {
        var sql = BrandNarrativeCatalog.BuildMigrationSql();
        var path = FindRepoFile("migrations", "371_pack_content_brand_narrative_dna.sql");
        File.WriteAllText(path, sql);

        var onDisk = File.ReadAllText(path);
        Assert.Equal(sql, onDisk);
        Assert.Contains("narrative_json", onDisk);
        Assert.Contains("https://thainguyenlife.vn", onDisk);
        Assert.DoesNotContain("https://thainguyen.life", onDisk);
        foreach (var dna in BrandNarrativeCatalog.All)
        {
            Assert.Contains(dna.BrandCode, onDisk);
            Assert.Contains("off-brand", onDisk);
            Assert.Contains(dna.Territories, t => t.Type == "off-brand");
            Assert.Contains(dna.Territories, t => t.Type == "core");
        }

        var parsed = BrandNarrativeFit.Parse(BrandNarrativeFit.Serialize(BrandNarrativeCatalog.ByCode("famixa")));
        Assert.NotNull(parsed);
        Assert.Equal("famixa", parsed!.BrandCode);
        Assert.Contains(parsed.HardBoundaries, h => h.Contains("đứa trẻ", StringComparison.Ordinal));
    }

    private static string FindRepoFile(string folder, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, folder)))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Repo root not found from " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, folder, file);
    }
}
