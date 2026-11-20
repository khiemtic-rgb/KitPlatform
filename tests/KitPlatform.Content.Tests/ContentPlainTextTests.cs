using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentPlainTextTests
{
    [Fact]
    public void Hashtag_line_keeps_every_hash()
    {
        var text = ContentPlainText.FromMarkdown("Nội dung.\n\n#NguoiGiuVon #GiuVonTruoc #QuanLyVon");
        Assert.EndsWith("#NguoiGiuVon #GiuVonTruoc #QuanLyVon", text);
    }

    [Fact]
    public void Hashtag_at_start_of_text_keeps_hash()
    {
        Assert.Equal("#NguoiGiuVon mở đầu", ContentPlainText.FromMarkdown("#NguoiGiuVon mở đầu"));
    }

    [Fact]
    public void Markdown_heading_marker_is_removed()
    {
        Assert.Equal("Tiêu đề\nThân bài", ContentPlainText.FromMarkdown("## Tiêu đề\nThân bài"));
    }
}
