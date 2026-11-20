namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// A voice-byte import may land only on a caller-owned Novixa build that already lists the shot.
/// </summary>
public static class NovixaVoiceBytesGate
{
    public static void Require(Guid buildId, string? seriesCode, string? graphJson, string? shotCode)
    {
        if (buildId == Guid.Empty || ProductionScopeAnchorRules.IsOfficialFilm(buildId))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        var code = (seriesCode ?? "").Trim().ToUpperInvariant();
        if (!code.StartsWith("NOVIXA", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        var shot = (shotCode ?? "").Trim();
        if (shot.Length == 0 || shot != (shotCode ?? "").Trim())
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var graph = graphJson ?? "";
        if (!graph.Contains("\"" + shot + "\"", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
    }
}
