namespace KitPlatform.Api.Authorization;

public static class CatalogPolicies
{
    public const string Read = "CatalogRead";
    public const string Write = "CatalogWrite";
    /// <summary>Merge duplicates, hide/restore/purge during cleanup (not granted with catalog.write).</summary>
    public const string Merge = "CatalogMerge";
}
