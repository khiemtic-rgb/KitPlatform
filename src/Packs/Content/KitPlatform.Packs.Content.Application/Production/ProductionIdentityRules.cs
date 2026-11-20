namespace KitPlatform.Packs.Content.Production;

/// <summary>URL is transport only. Never production identity.</summary>
public static class ProductionIdentityRules
{
    public static void ForbidUrlIdentity(params string?[] values)
    {
        foreach (var raw in values)
        {
            var v = (raw ?? "").Trim();
            if (v.Length == 0) continue;
            if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || v.Contains("://", StringComparison.Ordinal)
                || v.Contains("takeUrl", StringComparison.OrdinalIgnoreCase)
                || v.Contains("previewUrl", StringComparison.OrdinalIgnoreCase)
                || v.Contains("lipsyncUrl", StringComparison.OrdinalIgnoreCase)
                || v.Contains("acceptedTake", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ProductionFailureReasons.UrlAuthorityForbidden);
        }
    }

    public static IReadOnlyList<Guid> RequireParents(IReadOnlyList<Guid>? ids, int min)
    {
        var list = ids?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (list.Length < min)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        return list;
    }
}
