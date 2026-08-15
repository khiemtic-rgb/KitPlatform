namespace KitPlatform.Packs.LocalOs;

public enum LocalOsSourceLinkKind
{
    Invalid,
    FacebookGroupFeed,
    FacebookPost,
    PublicWeb,
}

public static class LocalOsSourceLink
{
    public static bool TryParse(string? raw, out Uri? uri, out LocalOsSourceLinkKind kind)
    {
        uri = null;
        kind = LocalOsSourceLinkKind.Invalid;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https"))
            return false;
        uri = parsed;
        kind = Classify(parsed);
        return kind != LocalOsSourceLinkKind.Invalid;
    }

    public static LocalOsSourceLinkKind Classify(Uri uri)
    {
        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        if (IsFacebookHost(host))
            return IsFacebookGroupFeed(uri) ? LocalOsSourceLinkKind.FacebookGroupFeed : LocalOsSourceLinkKind.FacebookPost;
        return LocalOsSourceLinkKind.PublicWeb;
    }

    public static bool IsFacebookHost(string host) =>
        host.Equals("facebook.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("m.facebook.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("web.facebook.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("fb.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("fb.watch", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".facebook.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>Group home / feed — not a single post permalink.</summary>
    public static bool IsFacebookGroupFeed(Uri uri)
    {
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.Contains("/groups/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Contains("/posts/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/permalink/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/reel/", StringComparison.OrdinalIgnoreCase))
            return false;
        var q = uri.Query;
        if (q.Contains("story_fbid", StringComparison.OrdinalIgnoreCase)
            || q.Contains("multi_permalinks", StringComparison.OrdinalIgnoreCase))
            return false;
        var afterGroups = path[(path.IndexOf("/groups/", StringComparison.OrdinalIgnoreCase) + "/groups/".Length)..];
        return !afterGroups.Contains('/');
    }

    public static string NormalizeHost(string host) =>
        host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).Trim().ToLowerInvariant();

    public static bool TryExtractFacebookGroupKey(Uri uri, out string key)
    {
        key = "";
        var path = uri.AbsolutePath;
        var i = path.IndexOf("/groups/", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return false;
        var rest = path[(i + "/groups/".Length)..].Trim('/');
        if (rest.Length == 0)
            return false;
        var slash = rest.IndexOf('/');
        key = slash < 0 ? rest : rest[..slash];
        return key.Length > 0;
    }

    /// <summary>Vanity slug of a Page (not a Group). Host www. is ignored; path www.tuaf.edu.vn is kept.</summary>
    public static bool TryExtractFacebookPageKey(Uri uri, out string key)
    {
        key = "";
        if (!IsFacebookHost(NormalizeHost(uri.Host)))
            return false;
        if (uri.AbsolutePath.Contains("/groups/", StringComparison.OrdinalIgnoreCase))
            return false;
        var first = uri.AbsolutePath.Trim('/').Split('/', 2)[0];
        if (first.Length == 0)
            return false;
        if (ReservedFacebookPaths.Contains(first))
            return false;
        key = first;
        return true;
    }

    public static bool IsGenericDocumentHost(string host)
    {
        var h = NormalizeHost(host);
        return h is "docs.google.com" or "drive.google.com" or "sheets.google.com"
            or "forms.google.com" or "sites.google.com";
    }

    public static bool TryExtractGoogleSheetId(Uri uri, out string id)
    {
        id = "";
        if (!IsGenericDocumentHost(uri.Host))
            return false;
        var path = uri.AbsolutePath;
        var marker = "/spreadsheets/d/";
        var i = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return false;
        var rest = path[(i + marker.Length)..].Trim('/');
        if (rest.Length == 0)
            return false;
        var slash = rest.IndexOf('/');
        id = slash < 0 ? rest : rest[..slash];
        return id.Length > 8;
    }

    private static readonly HashSet<string> ReservedFacebookPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "watch", "reel", "reels", "share", "permalink.php", "story.php", "photo.php",
        "photos", "login", "pages", "profile.php", "people", "events", "marketplace",
        "gaming", "stories", "live", "ads", "privacy", "help", "policies",
    };
}
