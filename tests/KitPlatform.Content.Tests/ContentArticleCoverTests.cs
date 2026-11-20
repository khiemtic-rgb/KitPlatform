using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ContentArticleCoverTests
{
    [Fact]
    public void Render_png_from_title_and_excerpt_without_network()
    {
        var line = ContentArticleCoverComposer.Excerpt(
            "Khi đơn thuốc cũ còn trong nhà, nên hỏi dược sĩ trước khi dùng lại. Không tự chia liều cho trẻ.",
            "Đơn thuốc cũ, khi nào nên hỏi lại dược sĩ");
        Assert.Contains("hỏi dược sĩ", line);

        var png = ContentArticleCoverComposer.Render(
            "Nhà thuốc Xuân Hòa",
            "Đơn thuốc cũ, khi nào nên hỏi lại dược sĩ",
            line);

        Assert.True(png.Length > 1024);
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]);
        Assert.Equal(0x4E, png[2]);
        Assert.Equal(0x47, png[3]);
    }

    [Fact]
    public void Excerpt_skips_a_line_that_only_repeats_the_title()
    {
        var line = ContentArticleCoverComposer.Excerpt(
            "Đơn thuốc cũ, khi nào nên hỏi lại dược sĩ",
            "Đơn thuốc cũ, khi nào nên hỏi lại dược sĩ");
        Assert.Equal("", line);
    }
}
