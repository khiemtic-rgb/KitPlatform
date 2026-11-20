namespace KitPlatform.Packs.Content;

/// <summary>
/// Chọn danh sách phát YouTube. Ô có tên thì dùng tên. Ô trống thì dùng lại playlist của series,
/// không có thì nhận danh sách chấm từ 0,7, không thì tạo mới.
/// </summary>
public static class ContentYoutubePlaylistRules
{
    public const double MatchThreshold = 0.7;
    public const int TitleMax = 60;

    public const string NgvDisclaimer =
        "Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn.";

    public enum Plan { Named, Reuse, Auto }

    public static Plan ChoosePlan(string? explicitName, string? savedPlaylistId)
    {
        if (!string.IsNullOrWhiteSpace(explicitName)) return Plan.Named;
        if (!string.IsNullOrWhiteSpace(savedPlaylistId)) return Plan.Reuse;
        return Plan.Auto;
    }

    public static bool Accept(string? playlistId, double score, IReadOnlyCollection<string> knownIds) =>
        score >= MatchThreshold
        && !string.IsNullOrWhiteSpace(playlistId)
        && knownIds.Contains(playlistId);

    public static (string Title, string Description) NormalizeNew(
        string? title,
        string? description,
        string fallbackTitle,
        string? brandCode,
        IReadOnlyList<string>? hashtags)
    {
        var name = string.IsNullOrWhiteSpace(title) ? fallbackTitle : title.Trim();
        name = name.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        if (name.Length > TitleMax) name = name[..TitleMax].TrimEnd();
        if (name.Length == 0) name = "Video";

        var text = (description ?? "").Trim();
        if (text.Length > 400) text = text[..400].TrimEnd();
        var tags = (hashtags ?? [])
            .Select(h => (h ?? "").Trim().TrimStart('#'))
            .Where(h => h.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(h => "#" + h)
            .ToList();
        if (tags.Count > 0)
        {
            var line = string.Join(' ', tags);
            if (!text.Contains(line, StringComparison.Ordinal))
                text = string.IsNullOrWhiteSpace(text) ? line : text + "\n" + line;
        }
        if (string.Equals(brandCode, "trading", StringComparison.OrdinalIgnoreCase)
            && !text.Contains(NgvDisclaimer, StringComparison.Ordinal))
            text = (text + "\n" + NgvDisclaimer).Trim();
        return (name, text);
    }
}
