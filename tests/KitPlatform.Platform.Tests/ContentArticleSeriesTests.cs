using System.Text.Json;
using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class ContentArticleSeriesTests
{
    [Theory]
    [InlineData(10, 10)]
    [InlineData(7, 5)]
    [InlineData(12, 10)]
    [InlineData(100, 100)]
    [InlineData(80, 100)]
    public void Clamp_episode_count(int input, int expected) =>
        Assert.Equal(expected, ContentArticleSeriesRules.ClampEpisodeCount(input));

    [Fact]
    public void Series_and_episode_codes_are_deterministic()
    {
        Assert.Equal("NOVIXA-S001", ContentArticleSeriesRules.SeriesCode("novixa", 1));
        Assert.Equal("NOVIXA-S001-E001", ContentArticleSeriesRules.EpisodeCode("NOVIXA-S001", 1));
        Assert.Equal("NOVIXA-S001-E003", ContentArticleSeriesRules.EpisodeCode("NOVIXA-S001", 3));
    }

    [Fact]
    public void Generate_content_only_when_planned_or_active()
    {
        Assert.False(ContentArticleSeriesRules.CanGenerateContent(ContentArticleSeriesStatuses.Draft));
        Assert.True(ContentArticleSeriesRules.CanGenerateContent(ContentArticleSeriesStatuses.Planned));
        Assert.True(ContentArticleSeriesRules.CanGenerateContent(ContentArticleSeriesStatuses.Active));
        Assert.False(ContentArticleSeriesRules.CanGenerateContent(ContentArticleSeriesStatuses.Paused));
    }

    [Fact]
    public void Episode_brief_fills_objective_and_article_format()
    {
        var brief = ContentArticleSeriesRules.EpisodeBrief("Giữ quỹ nhà thuốc", "Chỉ ra chỗ tiền đang nằm", "FEFO");
        Assert.True(ContentQualityGate.HasMinimumBrief(brief));
        Assert.Equal("Chỉ ra chỗ tiền đang nằm", brief.Objective);
        Assert.Equal("article", brief.Format);
    }

    [Fact]
    public void Topic_status_maps_and_quality_gate_blocks_approve()
    {
        Assert.Equal(ContentArticleEpisodeStatuses.Review, ContentArticleSeriesRules.MapTopicToEpisodeStatus("Review"));
        Assert.Equal(ContentArticleEpisodeStatuses.Published, ContentArticleSeriesRules.MapTopicToEpisodeStatus("Published"));
        var gate = new ContentQualityGateDto(false, ["qua mong"], DateTimeOffset.UtcNow, ["qua mong"]);
        Assert.True(ContentArticleSeriesRules.QualityGateBlocksApprove(gate));
        Assert.False(ContentArticleSeriesRules.QualityGateBlocksApprove(new ContentQualityGateDto(true, [], DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Continuity_context_includes_previous_next_and_core_idea()
    {
        var empty = JsonDocument.Parse("{}").RootElement.Clone();
        var series = new ContentArticleSeriesDto(
            Guid.NewGuid(), Guid.NewGuid(), "novixa", "Novixa", Guid.NewGuid(), "Goc",
            Guid.NewGuid(), "Nha thuoc khong the van hanh bang tri nho", "NOVIXA-S001",
            "Nha thuoc khong the van hanh bang tri nho", null, "He thong", "Chu nha thuoc",
            "Bot nho viec nho", "PLANNED", 10, 3, 4, 1, null, null, empty,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var ep1 = Ep(series.Id, 1, "NOVIXA-S001-E001", "Nhan ra van de", empty);
        var ep2 = Ep(series.Id, 2, "NOVIXA-S001-E002", "Viec phai nho", empty);
        var ep3 = Ep(series.Id, 3, "NOVIXA-S001-E003", "Thong tin that lac", empty);
        var ep4 = Ep(series.Id, 4, "NOVIXA-S001-E004", "Tinh huong thuc te", empty);
        var core = new ContentCoreIdeaDto("He thong tot bot nho", "Qua nhieu viec nho", "Bot nho", [], null);
        var ctx = ContentArticleSeriesRules.BuildContinuityContext(series, ep3, ep2, ep4, core, "BRAND BRAIN", "goc");
        Assert.Contains("PREVIOUS EPISODES", ctx);
        Assert.Contains("NOVIXA-S001-E002", ctx);
        Assert.Contains("NEXT EPISODE", ctx);
        Assert.Contains("NOVIXA-S001-E004", ctx);
        Assert.Contains("CORE IDEA", ctx);
        Assert.Contains("SERIES BLUEPRINT", ctx);
    }

    [Fact]
    public void Parse_blueprint_accepts_string_version_from_gemini()
    {
        var bp = ContentArticleSeriesRules.ParseBlueprint(
            """{"version":"1","narrativeArc":[{"episodeNo":"1","title":"T1"}],"contentPillars":[],"episodeCount":"10","continuityRules":[],"avoidRepetition":[],"recommendedFormats":[],"recommendedChannels":[]}""");
        Assert.Equal(1, bp.Version);
        Assert.Equal(10, bp.EpisodeCount);
        Assert.Equal(1, bp.NarrativeArc[0].EpisodeNo);
        Assert.Equal("T1", bp.NarrativeArc[0].Title);
    }

    [Fact]
    public void Factory_rules_require_local_value()
    {
        Assert.Contains("standalone value", ContentArticleSeriesRules.FactorySystemRules);
        Assert.Contains(ContentArticleSeriesRules.OutlineMarker, ContentArticleSeriesRules.BuildOutlineBlock(
            new ContentArticleSeriesDto(
                Guid.NewGuid(), Guid.NewGuid(), "novixa", "Novixa", Guid.NewGuid(), "Goc",
                Guid.NewGuid(), "Idea", "NOVIXA-S001", "Series", null, null, null, null, "DRAFT",
                10, 1, 0, 0, null, null, JsonDocument.Parse("{}").RootElement.Clone(),
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            Ep(Guid.NewGuid(), 1, "NOVIXA-S001-E001", "EP1", JsonDocument.Parse("{}").RootElement.Clone()),
            null, null, "ctx"));
    }

    private static ContentArticleEpisodeDto Ep(Guid seriesId, int no, string code, string title, JsonElement empty) =>
        new(Guid.NewGuid(), seriesId, no, code, title, null, null, null, "PLANNED",
            null, null, null, null, empty, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
