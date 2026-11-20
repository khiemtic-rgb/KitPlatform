namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Official ASSEMBLE freeze: assemblyId|buildId|order|shot|role|artifactId|sha|storageKey|duration.
/// One membership row per included shot. No schema change.
/// </summary>
public static class ProductionAssemblyFreeze
{
    public sealed record Shot(
        string ShotCode,
        string ArtifactRole,
        Guid ArtifactId,
        string Sha256,
        string StorageKey,
        decimal DurationSec);

    public static string Token(Guid assemblyId, Guid buildId, IReadOnlyList<Shot> shots)
    {
        if (shots is null || shots.Count == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);
        var rows = new List<string>
        {
            assemblyId.ToString("N"),
            buildId.ToString("N"),
        };
        for (var i = 0; i < shots.Count; i++)
        {
            var s = shots[i];
            ProductionIdentityRules.ForbidUrlIdentity(s.ShotCode, s.StorageKey);
            rows.Add(string.Join('|',
                i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (s.ShotCode ?? "").Trim(),
                (s.ArtifactRole ?? "").Trim(),
                s.ArtifactId.ToString("N"),
                (s.Sha256 ?? "").Trim().ToLowerInvariant(),
                (s.StorageKey ?? "").Trim(),
                s.DurationSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
        }
        return string.Join('|', rows);
    }

    public static bool TryParse(
        string? raw,
        out Guid assemblyId,
        out Guid buildId,
        out IReadOnlyList<Shot> shots)
    {
        assemblyId = Guid.Empty;
        buildId = Guid.Empty;
        shots = [];
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 9 || (parts.Length - 2) % 7 != 0)
            return false;
        if (!Guid.TryParseExact(parts[0].Trim(), "N", out assemblyId))
            return false;
        if (!Guid.TryParseExact(parts[1].Trim(), "N", out buildId))
            return false;
        var rows = new List<Shot>();
        for (var i = 2; i < parts.Length; i += 7)
        {
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var order)
                || order != rows.Count)
                return false;
            if (!Guid.TryParseExact(parts[i + 3].Trim(), "N", out var artifactId))
                return false;
            var sha = parts[i + 4].Trim().ToLowerInvariant();
            if (sha.Length != 64)
                return false;
            if (!decimal.TryParse(parts[i + 6], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var duration)
                || duration <= 0)
                return false;
            var storage = parts[i + 5].Trim();
            var shotCode = parts[i + 1].Trim();
            if (storage.Contains("://", StringComparison.Ordinal) || shotCode.Contains("://", StringComparison.Ordinal))
                return false;
            rows.Add(new Shot(shotCode, parts[i + 2].Trim(), artifactId, sha, storage, duration));
        }
        shots = rows;
        return rows.Count > 0;
    }
}
