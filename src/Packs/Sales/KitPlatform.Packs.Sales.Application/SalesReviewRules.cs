namespace KitPlatform.Packs.Sales;

public static class SalesReviewRules
{
    public static readonly string[] ReviewTypes = ["account_review", "need_triage", "autonomy_grant", "autonomy_incident"];

    public static bool OldEndpointAllows(string? taskType) => taskType == "accept_score";

    public static bool ReviewEndpointAllows(string? taskType) =>
        taskType is not null && ReviewTypes.Contains(taskType);

    public static readonly string[] AccountResolutions =
        ["distinct", "same_entity_no_merge", "fact_confirmed", "fact_rejected"];

    public static readonly string[] NeedResolutions = ["brief_opened"];

    public static bool ResolutionAllowed(string? taskType, string? resolution) => taskType switch
    {
        "account_review" => resolution is not null && AccountResolutions.Contains(resolution),
        "need_triage" => resolution is not null && NeedResolutions.Contains(resolution),
        _ => false,
    };
}

public static class SalesGrantIdentity
{
    public static bool SamePerson(string? requestedBy, Guid actor) =>
        !Guid.TryParse(requestedBy, out var requested) || requested == actor;
}
