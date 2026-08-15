namespace KitPlatform.Packs.LocalOs;

public static class LocalOsSourceMatch
{
    public static LocalSourceDto? Find(
        IEnumerable<LocalSourceDto> sources,
        Uri itemUri,
        Guid? preferredId)
    {
        var list = sources.ToList();
        if (preferredId is Guid id)
        {
            var picked = list.FirstOrDefault(s => s.Id == id);
            if (picked is not null)
                return picked;
        }

        if (LocalOsSourceLink.TryExtractFacebookGroupKey(itemUri, out var groupKey))
        {
            return list.FirstOrDefault(s =>
                s.Status == "active"
                && s.SourceKind == "facebook_group"
                && TryGroupKey(s.Url, out var sourceKey)
                && sourceKey.Equals(groupKey, StringComparison.OrdinalIgnoreCase));
        }

        if (LocalOsSourceLink.TryExtractFacebookPageKey(itemUri, out var pageKey))
        {
            return list.FirstOrDefault(s =>
                s.Status == "active"
                && s.SourceKind == "facebook_page"
                && TryPageKey(s.Url, out var sourceKey)
                && sourceKey.Equals(pageKey, StringComparison.OrdinalIgnoreCase));
        }

        if (LocalOsSourceLink.IsFacebookHost(LocalOsSourceLink.NormalizeHost(itemUri.Host)))
            return null;

        if (LocalOsSourceLink.TryExtractGoogleSheetId(itemUri, out var sheetId))
        {
            return list.FirstOrDefault(s =>
                s.Status == "active"
                && TrySheetId(s.Url, out var sourceSheet)
                && sourceSheet.Equals(sheetId, StringComparison.Ordinal));
        }

        if (LocalOsSourceLink.IsGenericDocumentHost(itemUri.Host))
            return null;

        var host = LocalOsSourceLink.NormalizeHost(itemUri.Host);
        return list.FirstOrDefault(s =>
            s.Status == "active"
            && s.SourceKind is "official_web" or "rss" or "partner"
            && TryHost(s.Url, out var sourceHost)
            && sourceHost == host
            && !LocalOsSourceLink.IsGenericDocumentHost(sourceHost));
    }

    private static bool TryGroupKey(string? url, out string key)
    {
        key = "";
        return LocalOsSourceLink.TryParse(url, out var uri, out _)
               && uri is not null
               && LocalOsSourceLink.TryExtractFacebookGroupKey(uri, out key);
    }

    private static bool TryPageKey(string? url, out string key)
    {
        key = "";
        return LocalOsSourceLink.TryParse(url, out var uri, out _)
               && uri is not null
               && LocalOsSourceLink.TryExtractFacebookPageKey(uri, out key);
    }

    private static bool TrySheetId(string? url, out string id)
    {
        id = "";
        return LocalOsSourceLink.TryParse(url, out var uri, out _)
               && uri is not null
               && LocalOsSourceLink.TryExtractGoogleSheetId(uri, out id);
    }

    private static bool TryHost(string? url, out string host)
    {
        host = "";
        if (!LocalOsSourceLink.TryParse(url, out var uri, out _) || uri is null)
            return false;
        host = LocalOsSourceLink.NormalizeHost(uri.Host);
        return host.Length > 0;
    }
}
