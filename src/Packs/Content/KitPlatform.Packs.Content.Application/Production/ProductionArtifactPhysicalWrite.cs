namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Physical bytes at a Production StorageKey. ArtifactId and ByteSha256 stay separate identities.
/// Same SHA is a no-op. A different SHA fails closed and does not replace the file.
/// </summary>
public static class ProductionArtifactPhysicalWrite
{
    public const string Collision = "PRODUCTION_ARTIFACT_IMMUTABLE_COLLISION";
    public const string RowLocked = "PRODUCTION_ARTIFACT_LOCKED: persisted artifact identity is immutable";
    public const string UnprefixedWriteClosed = "PRODUCTION_ARTIFACT_UNPREFIXED_WRITE_CLOSED";

    public static string CloseUnprefixedProductionWrite() =>
        throw new InvalidOperationException(UnprefixedWriteClosed);

    public static bool SameSha(byte[] existing, byte[] incoming) =>
        string.Equals(
            KitVideoIntegrityRules.Sha256Hex(existing),
            KitVideoIntegrityRules.Sha256Hex(incoming),
            StringComparison.OrdinalIgnoreCase);

    public static void RejectReplacement(byte[]? existing, byte[] incoming)
    {
        if (existing is null)
            return;
        if (SameSha(existing, incoming))
            return;
        throw new InvalidOperationException(Collision);
    }

    public static void CommitFile(string absolutePath, byte[] incoming)
    {
        if (File.Exists(absolutePath))
        {
            RejectReplacement(File.ReadAllBytes(absolutePath), incoming);
            return;
        }

        var dir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(absolutePath, incoming);
    }
}
