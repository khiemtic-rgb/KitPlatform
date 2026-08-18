using System.Text.RegularExpressions;

namespace KitPlatform.Packs.LocalOs;

public readonly record struct LocalOsIndexHit(Uri Uri, string Title);

public static class LocalOsIndexLinks
{
    private static readonly Regex Anchor = new(
        @"<a\b[^>]*href\s*=\s*[""'](?<h>[^""']+)[""'][^>]*>(?<t>[\s\S]*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] SkipPath =
    [
        "/login", "/search", "/tag/", "/tags/", "/author/", "/feed",
        "/comment", "/#", "/mailto:", "/gioi-thieu", "/lien-he", "/contact",
        "/about", "/sitemap", "/privacy", "/cookie", "/wp-admin", "/wp-login",
        "/chinh-sach", "/dieu-khoan",
    ];

    private static readonly string[] Prefer =
    [
        "tuyen-dung", "tuyendung", "viec-lam", "vieclam", "thuc-tap", "intern",
        "su-kien", "sukien", "su kien", "sự kiện", "le-hoi", "festival", "hoc-bong", "thong-bao",
        "ke-hoach", "chuong-trinh", "tuyen", "2026",
    ];

    public static IReadOnlyList<Uri> Extract(string html, Uri pageUrl, int max = 15) =>
        ExtractHits(html, pageUrl, max).Select(h => h.Uri).ToList();

    public static IReadOnlyList<LocalOsIndexHit> ExtractHits(string html, Uri pageUrl, int max = 15)
    {
        if (string.IsNullOrWhiteSpace(html) || max <= 0)
            return [];

        var pageHost = LocalOsSourceLink.NormalizeHost(pageUrl.Host);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(LocalOsIndexHit Hit, int Score)>();
        var pageKey = NormalizeKey(pageUrl);

        foreach (Match m in Anchor.Matches(html))
        {
            var raw = System.Net.WebUtility.HtmlDecode(m.Groups["h"].Value.Trim());
            if (raw.Length == 0 || raw.StartsWith('#') || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Uri.TryCreate(pageUrl, raw, out var uri)
                || uri.Scheme is not ("http" or "https"))
                continue;
            if (LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(uri.Host)))
                continue;
            if (LocalOsSourceLink.NormalizeHost(uri.Host) != pageHost)
                continue;

            var key = NormalizeKey(uri);
            if (key == pageKey || !seen.Add(key))
                continue;
            if (uri.AbsolutePath is "/" or "")
                continue;
            var path = uri.AbsolutePath.ToLowerInvariant();
            if (SkipPath.Any(s => path.Contains(s, StringComparison.Ordinal)))
                continue;

            var title = LocalOsTextExtract.StripHtml(m.Groups["t"].Value).Trim();
            if (title.Length > 160)
                title = title[..160].TrimEnd();
            var hit = new LocalOsIndexHit(new Uri(uri.GetLeftPart(UriPartial.Query).TrimEnd('?')), title);
            list.Add((hit, Score(hit)));
            if (list.Count >= 80)
                break;
        }

        return list
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Hit.Uri.AbsoluteUri.Length)
            .Take(max)
            .Select(x => x.Hit)
            .ToList();
    }

    private static int Score(LocalOsIndexHit hit)
    {
        var blob = $"{hit.Title} {hit.Uri.AbsolutePath}".ToLowerInvariant();
        var score = 0;
        foreach (var p in Prefer)
        {
            if (blob.Contains(p, StringComparison.Ordinal))
                score += 3;
        }
        if (blob.Contains("tin-tuc", StringComparison.Ordinal) || blob.Contains("tin-tức", StringComparison.Ordinal))
            score += 1;
        if (hit.Title.Length is >= 12 and <= 90)
            score += 1;
        return score;
    }

    private static string NormalizeKey(Uri uri) =>
        uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}
