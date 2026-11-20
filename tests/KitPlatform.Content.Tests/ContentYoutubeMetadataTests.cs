using System.Text.Json;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentYoutubeMetadataTests
{
    const string Body = "Trước khi hỏi mua gì, bán gì: số tiền bạn mang vào thị trường là tiền gì?\n\n" +
                        "Nội dung chỉ chia sẻ kiến thức.\n\n#NguoiGiuVon #GiuVonTruoc #QuanLyVon";

    [Fact]
    public void Description_from_markdown_keeps_first_hashtag()
    {
        var plain = ContentPlainText.FromMarkdown(Body);
        var description = ContentYoutubeMetadata.BuildDescription(plain, null, shorts: false, chapterBlock: null);
        Assert.EndsWith("#NguoiGiuVon #GiuVonTruoc #QuanLyVon", description);
    }

    [Fact]
    public void Chapters_go_before_the_hashtag_line_and_shorts_tag_last()
    {
        var block = ContentYoutubeMetadata.ChapterBlock(
            [new(0, "Mở đầu"), new(45, "Ba ngăn tiền"), new(120, "Tóm lại")], durationSeconds: 271.7);
        var description = ContentYoutubeMetadata.BuildDescription(Body, "https://example.vn", shorts: true, block);

        Assert.Equal("0:00 Mở đầu\n0:45 Ba ngăn tiền\n2:00 Tóm lại", block);
        var chapters = description.IndexOf("0:00 Mở đầu", StringComparison.Ordinal);
        var hashtags = description.IndexOf("#NguoiGiuVon", StringComparison.Ordinal);
        Assert.True(chapters > 0 && chapters < hashtags);
        Assert.EndsWith("https://example.vn\n\n#Shorts", description);
    }

    [Theory]
    [InlineData(5, 45, 120, null)]        // first chapter not at 0:00
    [InlineData(0, 5, 120, null)]         // a chapter shorter than 10 s
    [InlineData(0, 45, 265, 271.7)]       // last chapter shorter than 10 s before the end
    public void Invalid_chapters_produce_no_block(int a, int b, int c, double? duration)
    {
        Assert.Null(ContentYoutubeMetadata.ChapterBlock([new(a, "A"), new(b, "B"), new(c, "C")], duration));
    }

    [Fact]
    public void Fewer_than_three_chapters_produce_no_block()
    {
        Assert.Null(ContentYoutubeMetadata.ChapterBlock([new(0, "A"), new(60, "B")], null));
    }

    [Fact]
    public void Chapters_parse_from_strings_and_seconds()
    {
        var chapters = ContentYoutubeMetadata.ParseChapters(
            """{"chapters":[{"start":"0:00","title":"Mở đầu"},{"start":75,"title":"Phần 2"},{"time":"1:02:03","title":"Cuối"}]}""");
        Assert.Equal(new[] { 0, 75, 3723 }, chapters.Select(c => c.StartSeconds));
        Assert.Equal("1:02:03 Cuối", ContentYoutubeMetadata.ChapterBlock(chapters, null)!.Split('\n')[2]);
    }

    [Fact]
    public void Existing_chapter_block_is_not_added_twice()
    {
        const string withChapters = "Intro\n\n0:00 Mở đầu\n0:30 Hai\n1:00 Ba";
        var description = ContentYoutubeMetadata.BuildDescription(withChapters, null, false, "0:00 X\n0:20 Y\n0:40 Z");
        Assert.DoesNotContain("0:00 X", description);
    }

    [Fact]
    public void Tags_drop_hash_dedupe_and_stay_within_500_characters()
    {
        var hashtags = ContentYoutubeMetadata.Hashtags(Body).ToList();
        Assert.Equal(new[] { "NguoiGiuVon", "GiuVonTruoc", "QuanLyVon" }, hashtags);

        var many = Enumerable.Range(0, 80).Select(i => $"#tukhoa dai so {i:00}");
        var tags = ContentYoutubeMetadata.BuildTags(hashtags.Concat(["nguoigiuvon", "#Shorts", " ", "a,b<c>"]).Concat(many));

        Assert.Equal("NguoiGiuVon", tags[0]);
        Assert.DoesNotContain(tags, t => t.StartsWith('#'));
        Assert.DoesNotContain("Shorts", tags);
        Assert.Single(tags, t => t.Equals("nguoigiuvon", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("a bc", tags);
        var cost = tags.Sum(t => t.Length + (t.Contains(' ') ? 2 : 0)) + tags.Count - 1;
        Assert.InRange(cost, 1, ContentYoutubeMetadata.TagsMax);
    }

    [Fact]
    public void Variant_keywords_read_arrays_and_comma_lists()
    {
        var words = ContentYoutubeMetadata.VariantKeywords(
            """{"tags":["giữ vốn","quản lý vốn"],"keywords":"vốn sống, vốn giao dịch","focusKeyword":"giữ vốn"}""").ToList();
        Assert.Equal(new[] { "giữ vốn", "quản lý vốn", "vốn sống", " vốn giao dịch", "giữ vốn" }, words);
    }

    [Theory]
    [InlineData("""{"aiVoice":true}""", null, true)]
    [InlineData("""{"containsSyntheticMedia":false,"aiVoice":true}""", "true", false)]
    [InlineData("{}", "true", true)]
    [InlineData("{}", null, null)]
    public void Synthetic_media_flag_prefers_topic_then_channel(string topicExtra, string? channel, bool? expected)
    {
        Assert.Equal(expected, ContentYoutubeMetadata.SyntheticMedia(topicExtra, channel));
    }

    [Fact]
    public void Upload_metadata_carries_tags_and_ai_disclosure_only_when_set()
    {
        using var full = JsonDocument.Parse(ContentYoutubeClient.BuildUploadMetadata("T", "D", "public", ["giữ vốn"], true));
        Assert.Equal("giữ vốn", full.RootElement.GetProperty("snippet").GetProperty("tags")[0].GetString());
        Assert.True(full.RootElement.GetProperty("status").GetProperty("containsSyntheticMedia").GetBoolean());
        Assert.False(full.RootElement.GetProperty("status").GetProperty("selfDeclaredMadeForKids").GetBoolean());

        using var bare = JsonDocument.Parse(ContentYoutubeClient.BuildUploadMetadata("T", "D", "public", null, null));
        Assert.False(bare.RootElement.GetProperty("snippet").TryGetProperty("tags", out _));
        Assert.False(bare.RootElement.GetProperty("status").TryGetProperty("containsSyntheticMedia", out _));
    }

    [Fact]
    public void Redirect_uri_comes_from_configuration_and_localhost_only_in_development()
    {
        const string prod = "https://famixa.157.10.53.182.sslip.io/content/youtube/callback";
        Assert.Equal(prod, ContentYoutubeClient.ResolveRedirectUri(prod, null, isDevelopment: false));
        Assert.Equal(prod, ContentYoutubeClient.ResolveRedirectUri(" ", prod, isDevelopment: false));
        Assert.Null(ContentYoutubeClient.ResolveRedirectUri(null, null, isDevelopment: false));
        Assert.Equal(ContentYoutubeClient.DevelopmentRedirectUri, ContentYoutubeClient.ResolveRedirectUri(null, null, isDevelopment: true));
        Assert.Contains("youtube.force-ssl", ContentYoutubeClient.Scopes);
    }

    [Theory]
    [InlineData("tap01.vi.srt", "vi")]
    [InlineData("TAP01_EN.srt", "en")]
    [InlineData("vi.srt", "vi")]
    [InlineData("tap01-en.srt", "en")]
    [InlineData("tap01.fr.srt", null)]
    [InlineData("video.srt", null)]
    public void Subtitle_language_comes_from_file_name(string name, string? expected)
    {
        Assert.Equal(expected, ContentYoutubeSources.FileLanguage(name));
    }
}
