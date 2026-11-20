namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Paid-network decision for an unprepared CREATE_VIDEO start.
/// Official film and tenant-owned EP01 both require AuthorizePaidNetwork.
/// Tenant-owned EP01 also requires an ISSUED execution permit.
/// The process-local sandbox permit does not open this gate.
/// </summary>
public static class ProductionPaidNetworkGate
{
    public static bool IsTenantEp01PaidVideo(Guid buildId, string? episodeCode) =>
        ProductionScopeAnchorRules.IsTenantOwnedEp01Build(buildId, episodeCode);

    public static bool AllowsUnpreparedPaidStart(
        Guid buildId,
        string? episodeCode,
        bool authorizePaidNetwork,
        bool tenantIssuedPermitMatches)
    {
        if (!authorizePaidNetwork)
            return false;
        if (ProductionScopeAnchorRules.IsOfficialFilm(buildId))
            return true;
        if (ProductionScopeAnchorRules.IsTenantOwnedEp01Build(buildId, episodeCode))
            return tenantIssuedPermitMatches;
        return false;
    }

    public static bool TransportUsesExecutionPermit(Guid buildId, string? episodeCode) =>
        ProductionScopeAnchorRules.IsOfficialFilm(buildId)
        || ProductionScopeAnchorRules.IsTenantOwnedEp01Build(buildId, episodeCode);
}
