using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>Một bài trên YouTube hoặc Fanpage dùng để dựng khung website. Views null = chưa đo, không được kể là nhiều người xem.</summary>
public sealed record WatchedChannelPost(
    string Channel,
    string Title,
    int? Views,
    int? Engagements,
    string? Outline,
    bool Measured);

/// <summary>
/// Khung bài website lấy từ kênh tương ứng. Chỉ bài có lượt xem hoặc tương tác đã ghi mới được gọi là được xem nhiều.
/// </summary>
public static class ContentChannelFramework
{
    public const string Marker = "KHUNG BÀI TỪ KÊNH";

    public static readonly string[] WatchedChannels =
        ["youtube", "youtube_channel", "facebook", "facebook_page", "fanpage", "fb_page", "fb_short"];

    public static string Build(IReadOnlyList<WatchedChannelPost> posts)
    {
        var list = posts ?? [];
        var watched = list
            .Where(p => p.Measured && ((p.Views ?? 0) > 0 || (p.Engagements ?? 0) > 0))
            .OrderByDescending(p => p.Views ?? 0)
            .ThenByDescending(p => p.Engagements ?? 0)
            .Take(5)
            .ToList();
        var sb = new StringBuilder();
        sb.AppendLine(Marker);
        if (watched.Count > 0)
        {
            sb.AppendLine("Nguồn: bài YouTube và Fanpage của chính thương hiệu, xếp theo lượt xem đã ghi. Không suy ra số liệu chưa có.");
            var n = 1;
            foreach (var p in watched)
            {
                sb.Append(n++).Append(". ").Append(Label(p.Channel)).Append(" · ");
                if ((p.Views ?? 0) > 0) sb.Append(p.Views).Append(" lượt xem");
                else sb.Append(p.Engagements).Append(" tương tác");
                sb.Append(" · «").Append(OneLine(p.Title, 120)).AppendLine("»");
            }

            sb.AppendLine("Khung bài website:");
            sb.AppendLine("- Một luận điểm, cùng góc với bài được xem nhiều nhất ở trên. Không đổi thành bài giới thiệu phần mềm.");
            sb.AppendLine("- Đúng 3 mục ##: việc người xem đã dừng lại, một cách làm cụ thể, bước làm trong ngày.");
            sb.AppendLine("- Không bịa lượt xem, không chép nguyên caption Fanpage, không nhét hashtag.");
            return sb.ToString().Trim();
        }

        var samples = list.Where(p => !string.IsNullOrWhiteSpace(p.Title)).Take(4).ToList();
        if (samples.Count == 0)
        {
            sb.AppendLine("Chưa có bài YouTube hoặc Fanpage để đối chiếu. Viết một luận điểm, đúng 3 mục ##, theo Brand Angle của tập. Không bịa số người xem.");
            return sb.ToString().Trim();
        }

        sb.AppendLine("Chưa có số lượt xem YouTube/Fanpage. Khung chỉ dựa trên bài đã có của các kênh đó. Không xếp bài nào là phổ biến.");
        foreach (var p in samples)
            sb.Append("- ").Append(Label(p.Channel)).Append(": «").Append(OneLine(p.Title, 120)).AppendLine("»");
        sb.AppendLine("Khung bài website: một luận điểm gần các tiêu đề trên, đúng 3 mục ##, không bịa số người xem.");
        return sb.ToString().Trim();
    }

    public static string? Extract(string? outline)
    {
        if (string.IsNullOrWhiteSpace(outline)) return null;
        var i = outline.IndexOf(Marker, StringComparison.Ordinal);
        return i < 0 ? null : outline[i..].Trim();
    }

    private static string Label(string? channel)
    {
        var c = (channel ?? "").Trim().ToLowerInvariant();
        if (c.Contains("youtube", StringComparison.Ordinal)) return "YouTube";
        if (c.Contains("face", StringComparison.Ordinal) || c.StartsWith("fb", StringComparison.Ordinal) || c.Contains("fan", StringComparison.Ordinal))
            return "Fanpage";
        return string.IsNullOrWhiteSpace(channel) ? "Kênh" : channel.Trim();
    }

    private static string OneLine(string? text, int max)
    {
        var t = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (t.Contains("  ", StringComparison.Ordinal)) t = t.Replace("  ", " ", StringComparison.Ordinal);
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }
}
