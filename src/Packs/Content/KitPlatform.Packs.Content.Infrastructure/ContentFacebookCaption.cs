using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ContentFacebookCaption
{
    /// <summary>Dòng đánh dấu bình luận đầu trong bản viết fb_page (Video Studio bước 5, joinFacebookPost).</summary>
    private static readonly Regex CommentMark = new(@"\n-{3,}[ \t]*\n[ \t]*Bình luận đầu:[ \t]*\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Video posts use the same caption as TikTok (Video Studio step 5), unless step 5 approved a Fanpage text of its own
    /// (fb_page imported by hand: meta source manual_import). fb_page is otherwise a long text post written for a photo/feed post.
    /// Null means the caller falls back to the topic title.
    /// </summary>
    public static ContentRepository.VariantRow? Pick(IReadOnlyList<ContentRepository.VariantRow> variants, bool hasVideo)
    {
        if (hasVideo)
        {
            var approved = variants.FirstOrDefault(v => v.Kind is "fb_page" && IsManual(v) && !string.IsNullOrWhiteSpace(v.BodyMarkdown));
            if (approved is not null)
                return approved;
            var caption = variants.FirstOrDefault(v => v.Kind is "video_caption" or "tiktok" && !string.IsNullOrWhiteSpace(v.BodyMarkdown));
            if (caption is not null)
                return caption;
        }

        return variants.FirstOrDefault(v => v.Kind is "fb_page" or "fb_short")
               ?? variants.FirstOrDefault(v => v.Kind is "social_caption");
    }

    /// <summary>Tách thân bài và bình luận đầu (nguồn, link). Không có dòng đánh dấu thì bình luận rỗng.</summary>
    public static (string Post, string Comment) Split(string? body)
    {
        var text = (body ?? "").Replace("\r\n", "\n").Trim();
        var m = CommentMark.Match(text);
        if (!m.Success) return (text, "");
        return (text[..m.Index].Trim(), text[(m.Index + m.Length)..].Trim());
    }

    private static bool IsManual(ContentRepository.VariantRow v) =>
        v.MetaJson?.Contains("manual_import", StringComparison.OrdinalIgnoreCase) == true;
}
