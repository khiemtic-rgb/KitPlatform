namespace KitPlatform.Packs.Sales;

public enum SalesAutonomyMode
{
    Off = 0,
    Shadow = 1,
    Supervised = 2,
    Autonomous = 3,
}

public sealed record SalesAutonomyGrant(
    string ProductCode,
    string Channel,
    string Action,
    SalesAutonomyMode Mode,
    int DailyCap,
    DateTimeOffset ExpiresAt,
    string RequestedBy);

public static class SalesAutonomy
{
    public const int ShadowSampleMinimum = 50;
    public const int AutonomousSampleMinimum = 200;
    public const int AutonomousDays = 28;
    public const int GrantDaysMaximum = 90;
    public const decimal OptOutThreshold = 0.02m;
    public const decimal ReviewFailThreshold = 0.05m;

    public static SalesAutonomyMode Effective(
        bool masterEnabled,
        bool profileActive,
        bool incidentPending,
        SalesAutonomyMode? incidentMode,
        SalesAutonomyGrant? grant,
        SalesAutonomyMode profileCeiling,
        bool sending)
    {
        if (!profileActive)
            return SalesAutonomyMode.Off;
        if (!masterEnabled)
            return grant?.Mode == SalesAutonomyMode.Off ? SalesAutonomyMode.Off : SalesAutonomyMode.Supervised;
        if (incidentPending)
            return incidentMode is SalesAutonomyMode.Off ? SalesAutonomyMode.Off : SalesAutonomyMode.Supervised;
        if (grant is null || grant.ExpiresAt <= DateTimeOffset.UtcNow)
            return sending ? SalesAutonomyMode.Off : SalesAutonomyMode.Shadow;
        var mode = grant.Mode > profileCeiling ? profileCeiling : grant.Mode;
        return mode;
    }

    public static bool CanPromote(
        SalesAutonomyMode from,
        SalesAutonomyMode to,
        int samples,
        int days,
        bool validatorClean,
        decimal usableRate,
        decimal unchangedApprovalRate,
        int complaints,
        decimal optOutRate,
        bool legalFlag,
        bool incidentIn14Days,
        string requestedBy,
        string resolvedBy,
        bool resolverIsHuman)
    {
        if (!resolverIsHuman || string.Equals(requestedBy, resolvedBy, StringComparison.Ordinal))
            return false;
        if (incidentIn14Days)
            return false;
        if ((int)to != (int)from + 1)
            return false;
        return to switch
        {
            SalesAutonomyMode.Shadow => true,
            SalesAutonomyMode.Supervised => samples >= ShadowSampleMinimum && validatorClean && usableRate >= 0.70m,
            SalesAutonomyMode.Autonomous => samples >= AutonomousSampleMinimum
                && days >= AutonomousDays
                && unchangedApprovalRate >= 0.90m
                && complaints == 0
                && optOutRate <= OptOutThreshold
                && legalFlag,
            _ => false,
        };
    }

    public static SalesAutonomyMode Demote(string condition, SalesAutonomyMode current)
    {
        return condition switch
        {
            "validator_severe" or "review_fail" or "opt_out" or "complaint" or "metrics_stale" => SalesAutonomyMode.Supervised,
            "delivery_errors" or "sent_after_opt_out" or "wrong_tenant" or "bad_claim_sent" => SalesAutonomyMode.Off,
            _ => current,
        };
    }
}
