using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Loại nội dung một Fanpage nhận, lưu ở configJson.accepts.
/// Không có khoá, JSON hỏng, hoặc không còn video/post hợp lệ = nhận cả hai.
/// </summary>
public static class ContentChannelAccepts
{
    public const string Video = "video";
    public const string Post = "post";

    public static IReadOnlyList<string>? Parse(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("accepts", out var raw)
                || raw.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<string>();
            foreach (var item in raw.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                var value = (item.GetString() ?? "").Trim().ToLowerInvariant();
                if (value is Video or Post && !list.Contains(value, StringComparer.Ordinal))
                    list.Add(value);
            }

            return list.Count == 0 ? null : list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool Allows(IReadOnlyList<string>? accepts, string kind) =>
        accepts is null || accepts.Contains(kind, StringComparer.OrdinalIgnoreCase);

    public static bool Allows(string? configJson, string kind) => Allows(Parse(configJson), kind);

    /// <summary>Video Studio (long/short) hoặc bài đã có file video. Còn lại là bài viết.</summary>
    public static string KindOf(ContentScheduleCandidate candidate)
    {
        if (string.Equals(candidate.Format, "long", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Format, "short", StringComparison.OrdinalIgnoreCase))
            return Video;
        if (candidate.VariantKinds.Any(k => string.Equals(k, "video_asset", StringComparison.OrdinalIgnoreCase)))
            return Video;
        return Post;
    }

    public static string SkipWarning(string label, string kind) =>
        kind == Video
            ? $"Fanpage «{label}» chỉ nhận bài viết, bỏ qua video."
            : $"Fanpage «{label}» chỉ nhận video, bỏ qua bài viết.";
}
