namespace KitPlatform.Packs.Sales;

/// <summary>
/// Audit payloads for KIT Sales settings. Secrets never leave as values: only set/cleared/unchanged.
/// </summary>
public static class SalesSettingsAudit
{
    public const string AiEntity = "kit_sales.ai_settings";
    public const string FacebookEntity = "kit_sales.facebook_settings";
    public const string PsidEntity = "kit_sales.lead_psid";

    public static string SecretChange(string? previous, string? next)
    {
        var had = !string.IsNullOrWhiteSpace(previous);
        var has = !string.IsNullOrWhiteSpace(next);
        if (!has)
            return had ? "cleared" : "unchanged";
        if (!had)
            return "set";
        return string.Equals(previous!.Trim(), next!.Trim(), StringComparison.Ordinal) ? "unchanged" : "replaced";
    }

    public static IReadOnlyDictionary<string, object?> Ai(
        string? previousKey,
        string? nextKey,
        string? previousModel,
        string? nextModel) =>
        new Dictionary<string, object?>
        {
            ["api_key"] = SecretChange(previousKey, nextKey),
            ["text_model_before"] = Clean(previousModel),
            ["text_model_after"] = Clean(nextModel),
        };

    public static IReadOnlyDictionary<string, object?> Facebook(
        bool previousEnabled,
        bool nextEnabled,
        string? previousToken,
        string? nextToken,
        string? previousVerify,
        string? nextVerify,
        string? previousSecret,
        string? nextSecret,
        string? previousPageId,
        string? nextPageId) =>
        new Dictionary<string, object?>
        {
            ["enabled_before"] = previousEnabled,
            ["enabled_after"] = nextEnabled,
            ["page_access_token"] = SecretChange(previousToken, nextToken),
            ["verify_token"] = SecretChange(previousVerify, nextVerify),
            ["app_secret"] = SecretChange(previousSecret, nextSecret),
            ["page_id_changed"] = !string.Equals(Clean(previousPageId), Clean(nextPageId), StringComparison.Ordinal),
        };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
