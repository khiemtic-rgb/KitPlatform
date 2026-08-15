using System.Text.RegularExpressions;

namespace KitPlatform.Packs.LocalOs;

public static class LocalOsIndexLinks
{
    private static readonly Regex Href = new(
        @"href\s*=\s*[""'](?<h>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] SkipPath =
    [
        "/login", "/search", "/tag/", "/tags/", "/author/", "/feed",
        "/comment", "/#", "/mailto:",
    ];

    public static IReadOnlyList<Uri> Extract(string html, Uri pageUrl, int max = 15)
    {
        if (string.IsNullOrWhiteSpace(html) || max <= 0)
            return [];

        var pageHost = LocalOsSourceLink.NormalizeHost(pageUrl.Host);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Uri>();
        var pageKey = NormalizeKey(pageUrl);

        foreach (Match m in Href.Matches(html))
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

            list.Add(new Uri(uri.GetLeftPart(UriPartial.Query).TrimEnd('?')));
            if (list.Count >= max)
                break;
        }

        return list;
    }

    private static string NormalizeKey(Uri uri) =>
        uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}
