using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class VideoPipelineRulesTests
{
    private static readonly Guid BrandA = Guid.Parse("019fec28-506c-7c7c-907e-38e11152dcf7");
    private static readonly Guid BrandB = Guid.Parse("019ff475-4641-724c-ac3b-21c7eff6d784");
    private static readonly Guid PipelineA = Guid.Parse("01a17364-4836-707c-bd63-414942c753c9");
    private static readonly Guid PipelineB = Guid.Parse("01a17364-4836-707c-bd63-414942c753ca");

    [Fact]
    public void Config_round_trip_keeps_purpose_steps_and_default()
    {
        var json = VideoPipelineRules.WriteConfig(
            "Kiến thức vận hành",
            "Video chuyên môn",
            "9:16",
            true,
            ["script", "review", "export"]);
        var read = VideoPipelineRules.ReadConfig(json);
        Assert.Equal("Kiến thức vận hành", read.Purpose);
        Assert.Equal("Video chuyên môn", read.SeriesLabel);
        Assert.Equal("9:16", read.Aspect);
        Assert.True(read.IsDefault);
        Assert.Equal(["script", "review", "export"], read.Steps);
        Assert.Null(VideoPipelineRules.ConfigError("factory_short", "active", read.Steps, read.Aspect));
    }

    [Fact]
    public void Unknown_step_or_workflow_is_rejected()
    {
        Assert.NotNull(VideoPipelineRules.ConfigError("video_universe", "active", null, ""));
        Assert.NotNull(VideoPipelineRules.ConfigError("factory_short", "active", ["runway"], ""));
        Assert.NotNull(VideoPipelineRules.ConfigError("factory_short", "paused", null, ""));
    }

    [Fact]
    public void Pipelines_stay_on_their_own_brand()
    {
        Assert.Null(VideoPipelineRules.BindRefusal(BrandA, BrandA, "active", true));
        Assert.Equal(
            "Pipeline không thuộc brand của phim.",
            VideoPipelineRules.BindRefusal(BrandA, BrandB, "active", true));
        Assert.Equal(
            "Pipeline chưa bật. Không tạo phim mới.",
            VideoPipelineRules.BindRefusal(BrandA, BrandA, "archived", true));
        Assert.Null(VideoPipelineRules.BindRefusal(BrandA, BrandA, "archived", false));
    }

    [Fact]
    public void Workflow_stays_when_films_already_use_the_pipeline()
    {
        Assert.Null(VideoPipelineRules.WorkflowChangeRefusal("factory_short", "factory_short", 2, false));
        Assert.NotNull(VideoPipelineRules.WorkflowChangeRefusal("factory_short", "famixa_series", 1, false));
        Assert.NotNull(VideoPipelineRules.WorkflowChangeRefusal("factory_short", "guided_production", 0, true));
        Assert.Null(VideoPipelineRules.WorkflowChangeRefusal("factory_short", "guided_production", 0, false));
    }

    [Fact]
    public void Disabled_pipeline_is_not_offered_for_a_new_film()
    {
        Assert.True(VideoPipelineRules.OfferedForNewFilm("active"));
        Assert.False(VideoPipelineRules.OfferedForNewFilm("draft"));
        Assert.False(VideoPipelineRules.OfferedForNewFilm("archived"));
    }

    [Theory]
    [InlineData("famixa_series", "series_continuity")]
    [InlineData("factory_short", "short_factory")]
    [InlineData("guided_production", "guided")]
    public void Stored_workflow_selects_the_workspace(string workflow, string workspace)
    {
        Assert.Equal(workspace, VideoPipelineRules.ResolveWorkspace(PipelineA, workflow, "NOVIXA"));
    }

    [Fact]
    public void Two_pipelines_do_not_share_a_workspace_by_brand()
    {
        Assert.Equal("series_continuity", VideoPipelineRules.ResolveWorkspace(PipelineA, "famixa_series", "NOVIXA"));
        Assert.Equal("short_factory", VideoPipelineRules.ResolveWorkspace(PipelineB, "factory_short", "NOVIXA"));
    }

    [Fact]
    public void Existing_film_without_a_pipeline_opens_on_its_series_code()
    {
        Assert.Equal("series_continuity", VideoPipelineRules.ResolveWorkspace(null, null, "NOVIXA"));
        Assert.Equal("series_continuity", VideoPipelineRules.ResolveWorkspace(null, "famixa_series", "NOVIXA_OPS"));
        Assert.Equal("unconfigured", VideoPipelineRules.ResolveWorkspace(null, "famixa_series", null));
        Assert.Equal("unconfigured", VideoPipelineRules.ResolveWorkspace(null, "famixa_series", "  "));
    }

    [Fact]
    public void Legacy_catalog_without_a_pipeline_keeps_the_series_workspace()
    {
        Assert.Equal(
            "series_continuity",
            VideoPipelineRules.ResolveWorkspace(null, null, VideoPipelineRules.LegacySeriesCatalog));
    }

    [Fact]
    public void Nav_brand_cannot_retarget_a_stored_pipeline()
    {
        Assert.Equal(
            "Đổi pipeline của phim cần thao tác chuyển riêng.",
            VideoPipelineRules.RetargetRefusal(PipelineA, PipelineB));
        Assert.Null(VideoPipelineRules.RetargetRefusal(PipelineA, PipelineA));
        Assert.Null(VideoPipelineRules.RetargetRefusal(PipelineA, null));
    }

    [Fact]
    public void Series_line_uses_the_stored_label()
    {
        Assert.Equal(
            "Video chuyên môn",
            VideoPipelineRules.SeriesLine(PipelineA, "NOVIXA", "Video chuyên môn", "Nhà thuốc"));
        Assert.Equal(
            "Nhà thuốc",
            VideoPipelineRules.SeriesLine(PipelineA, "NOVIXA", "", "Nhà thuốc"));
        Assert.Null(VideoPipelineRules.SeriesLine(null, "FAMIXA", "", "Famixa"));
        Assert.Equal("NOVIXA", VideoPipelineRules.SeriesLine(null, "NOVIXA", "", ""));
        Assert.DoesNotContain("Video giáo dục", VideoPipelineRules.SeriesLine(null, "NOVIXA", "", "") ?? "");
        Assert.DoesNotContain("Video giáo dục", VideoPipelineRules.SeriesLine(PipelineB, "NOVIXA", "", "Nhà thuốc") ?? "");
    }

    [Fact]
    public void Pipeline_code_never_becomes_the_legacy_catalog()
    {
        Assert.Equal("NOVIXA_OPS", VideoPipelineRules.SeriesCodeForPipeline("novixa-ops"));
        Assert.Null(VideoPipelineRules.SeriesCodeForPipeline("!!!"));
        Assert.Null(VideoPipelineRules.SeriesCodeForPipeline(""));
    }
}
