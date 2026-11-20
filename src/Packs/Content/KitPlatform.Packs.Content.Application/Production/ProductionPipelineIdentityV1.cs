namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// One attempt has one verified output of a role. A second verified hit is ambiguous
/// and is not a success. A different role on the same attempt is not that output.
/// </summary>
public static class ProductionAttemptOutput
{
    public readonly record struct Selection(ProductionArtifact? Artifact, bool Ambiguous);

    public static Selection Select(IReadOnlyList<ProductionArtifact> artifacts, Guid attemptId, string role)
    {
        var hits = artifacts
            .Where(a => a.AttemptId == attemptId
                && a.Role == role
                && a.VerificationStatus == ProductionArtifactStatuses.Verified)
            .ToArray();
        if (hits.Length > 1) return new Selection(null, true);
        if (hits.Length == 1) return new Selection(hits[0], false);
        return new Selection(null, false);
    }
}

/// <summary>
/// File names under a tenant-owned storage folder. The folder itself is the tenant
/// boundary. A key that already names a media file keeps that extension. A key
/// without an extension still resolves to the .mp4 written by PersistNamed.
/// </summary>
public static class ProductionStorageFileNames
{
    public static string Stem(string? fileId) =>
        string.Concat((fileId ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch == '-'));

    public static IReadOnlyList<string> Candidates(string? fileId)
    {
        var raw = (fileId ?? "").Trim();
        var names = new List<string>();
        var ext = Extension(raw);
        if (ext is not null)
        {
            var stem = Stem(Path.GetFileNameWithoutExtension(raw));
            if (stem.Length >= 8) names.Add(stem + ext);
        }
        var bare = Stem(raw);
        if (bare.Length >= 8)
        {
            var mp4 = bare + ".mp4";
            if (!names.Contains(mp4, StringComparer.OrdinalIgnoreCase))
                names.Add(mp4);
        }
        return names;
    }

    static string? Extension(string raw)
    {
        var ext = Path.GetExtension(raw);
        return ext.ToLowerInvariant() switch
        {
            ".mp4" or ".webm" or ".mov" or ".wav" or ".mp3" or ".m4a" => ext.ToLowerInvariant(),
            _ => null,
        };
    }
}
