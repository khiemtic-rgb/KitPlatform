namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// AUTHORITY-STABILIZATION-01B — official preview streams only after physical bytes
/// match ProductionArtifact.ByteSha256. Does not repair disk, rebind, refreeze,
/// or rewrite ByteSha256 from current bytes.
/// </summary>
public static class OfficialPreviewIntegrityV1
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_PREVIEW_INTEGRITY_V1";

    public static byte[]? ReadVerifiedPhysicalBytes(
        IProductionArtifactFileStore files,
        string? storageKey,
        string? expectedByteSha256)
    {
        if (files is null
            || string.IsNullOrWhiteSpace(storageKey)
            || string.IsNullOrWhiteSpace(expectedByteSha256))
            return null;

        var bytes = files.Read(storageKey.Trim());
        if (bytes is null || bytes.Length < 32)
            return null;

        var live = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.Equals(live, expectedByteSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;

        return bytes;
    }
}
