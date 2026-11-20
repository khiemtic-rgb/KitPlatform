using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class KitMktWorkspaceRulesTests
{
    private static readonly Guid BrandA = Guid.Parse("019ff475-4641-724c-ac3b-21c7eff6d784");
    private static readonly Guid BrandB = Guid.Parse("019fec28-586c-7c7c-907e-38e11152dcf7");
    private static readonly Guid Topic = Guid.Parse("019ff037-c7f6-7907-9c51-4526696a91b0");

    [Fact]
    public void New_idea_has_no_brand_topic_or_source()
    {
        Assert.True(KitMktWorkspaceRules.IdeaShape(null, null, null));
        Assert.False(KitMktWorkspaceRules.IdeaShape(BrandA, null, null));
        Assert.True(KitMktWorkspaceRules.PackageScopeAllowed(null, null, null));
        Assert.True(KitMktWorkspaceRules.PackageScopeAllowed(BrandA, Topic, Guid.NewGuid()));
        Assert.False(KitMktWorkspaceRules.PackageScopeAllowed(BrandA, null, null));
    }

    [Fact]
    public void Project_accepts_only_its_own_film_type()
    {
        Assert.False(KitMktWorkspaceRules.ProjectFilmBrandMatch(null, BrandA));
        Assert.True(KitMktWorkspaceRules.ProjectFilmBrandMatch(BrandA, BrandA));
        Assert.False(KitMktWorkspaceRules.ProjectFilmBrandMatch(BrandA, BrandB));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("famixa_series", true)]
    [InlineData("factory_short", false)]
    [InlineData("guided_production", false)]
    public void Series_build_follows_the_film_type_workflow(string? workflow, bool allowed)
    {
        Assert.Equal(allowed, KitMktWorkspaceRules.MayAttachSeriesBuild(workflow, Guid.NewGuid()));
        Assert.True(KitMktWorkspaceRules.MayAttachSeriesBuild(workflow, null));
    }

    [Fact]
    public void Active_project_blocks_a_workflow_change()
    {
        Assert.False(KitMktWorkspaceRules.MayChangeWorkflow("famixa_series", "factory_short", true));
        Assert.True(KitMktWorkspaceRules.MayChangeWorkflow("famixa_series", "famixa_series", true));
        Assert.True(KitMktWorkspaceRules.MayChangeWorkflow("famixa_series", "factory_short", false));
    }

    [Fact]
    public void Publish_selection_and_calendar_stay_on_their_own_clocks()
    {
        Assert.False(KitMktWorkspaceRules.MaySelectProductionForPublish(null));
        Assert.True(KitMktWorkspaceRules.MaySelectProductionForPublish(Topic));
        var at = DateTimeOffset.Parse("2026-10-25T10:00:00Z");
        Assert.Equal(at, KitMktWorkspaceRules.CalendarInstant(at));
        Assert.Null(KitMktWorkspaceRules.CalendarInstant(null));
    }

    [Fact]
    public void Production_requires_brand_and_its_own_film_type()
    {
        var film = Guid.Parse("01a17364-4836-707c-bd63-414942c753c9");
        Assert.Equal("Project chưa có brand.", KitMktWorkspaceRules.OpenProductionRefusal(null, null, null));
        Assert.Equal("Project chưa có brand.", KitMktWorkspaceRules.OpenProductionRefusal(null, film, BrandA));
        Assert.Equal("Project chưa có Film Type.", KitMktWorkspaceRules.OpenProductionRefusal(BrandA, null, null));
        Assert.Equal(
            "Film Type không thuộc brand của project.",
            KitMktWorkspaceRules.OpenProductionRefusal(BrandA, film, BrandB));
        Assert.Null(KitMktWorkspaceRules.OpenProductionRefusal(BrandA, film, BrandA));
    }

    [Fact]
    public void Series_reports_a_missing_brand_before_narrative()
    {
        Assert.Equal(
            "Idea chưa gắn thương hiệu.",
            KitMktWorkspaceRules.SeriesCreateRefusal(null, BrandNarrativeFit.NarrativeFitRequired));
        Assert.Equal(
            "Idea chưa gắn thương hiệu.",
            KitMktWorkspaceRules.SeriesCreateRefusal(null, null));
        Assert.Contains(
            "NarrativeFitRequired",
            KitMktWorkspaceRules.SeriesCreateRefusal(BrandA, BrandNarrativeFit.NarrativeFitRequired));
        Assert.Contains("OFF-BRAND", KitMktWorkspaceRules.SeriesCreateRefusal(BrandA, BrandNarrativeFit.OffBrandStop));
        Assert.Null(KitMktWorkspaceRules.SeriesCreateRefusal(BrandA, null));
    }

    [Fact]
    public void Film_type_bind_distinguishes_a_missing_brand_from_the_wrong_brand()
    {
        Assert.Equal(
            "Project chưa có brand. Không gán brand hộ.",
            KitMktWorkspaceRules.FilmTypeBindRefusal(null, BrandA));
        Assert.Equal(
            "Film Type không thuộc brand của project.",
            KitMktWorkspaceRules.FilmTypeBindRefusal(BrandA, BrandB));
        Assert.Null(KitMktWorkspaceRules.FilmTypeBindRefusal(BrandA, BrandA));
    }

    [Fact]
    public void Workflow_and_format_lists_are_closed()
    {
        Assert.True(KitMktWorkspaceRules.KnownWorkflow("guided_production"));
        Assert.False(KitMktWorkspaceRules.KnownWorkflow("video_universe"));
        Assert.True(KitMktWorkspaceRules.KnownFormat("video"));
        Assert.False(KitMktWorkspaceRules.KnownFormat("short"));
    }
}
