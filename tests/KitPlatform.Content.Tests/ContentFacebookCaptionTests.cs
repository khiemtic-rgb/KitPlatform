using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentFacebookCaptionTests
{
    static ContentRepository.VariantRow V(string kind, string body) => new() { Kind = kind, BodyMarkdown = body };

    [Fact]
    public void Video_post_uses_video_caption_before_fb_page()
    {
        var variants = new[] { V("fb_page", "long post"), V("video_caption", "tiktok caption"), V("youtube", "yt") };
        Assert.Equal("tiktok caption", ContentFacebookCaption.Pick(variants, hasVideo: true)?.BodyMarkdown);
    }

    [Fact]
    public void Photo_post_keeps_fb_page()
    {
        var variants = new[] { V("fb_page", "long post"), V("video_caption", "tiktok caption") };
        Assert.Equal("long post", ContentFacebookCaption.Pick(variants, hasVideo: false)?.BodyMarkdown);
    }

    [Fact]
    public void Video_without_caption_falls_back_to_fb_then_title()
    {
        Assert.Equal("long post", ContentFacebookCaption.Pick([V("video_caption", " "), V("fb_page", "long post")], true)?.BodyMarkdown);
        Assert.Null(ContentFacebookCaption.Pick([V("youtube", "yt only")], true));
    }

    [Fact]
    public void Video_post_prefers_fanpage_text_approved_in_step5()
    {
        var approved = new ContentRepository.VariantRow { Kind = "fb_page", BodyMarkdown = "fanpage", MetaJson = "{\"source\":\"manual_import\"}" };
        var variants = new[] { V("video_caption", "tiktok caption"), approved };
        Assert.Equal("fanpage", ContentFacebookCaption.Pick(variants, hasVideo: true)?.BodyMarkdown);
    }

    [Fact]
    public void Split_moves_first_comment_out_of_the_post()
    {
        var (post, comment) = ContentFacebookCaption.Split("Thân bài\n#a\n\n---\nBình luận đầu:\nNguồn: x https://a.b");
        Assert.Equal("Thân bài\n#a", post);
        Assert.Equal("Nguồn: x https://a.b", comment);
        Assert.Equal(("Chỉ thân", ""), ContentFacebookCaption.Split("Chỉ thân"));
    }
}
