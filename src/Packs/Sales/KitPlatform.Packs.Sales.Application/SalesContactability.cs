namespace KitPlatform.Packs.Sales;

/// <summary>
/// A lead is reachable only through a channel the business itself answers.
/// Map/OSM, legal and personal-profile identities do not count.
/// </summary>
public static class SalesContactability
{
    public const string SocialIdentityType = "facebook";

    public static readonly IReadOnlyList<string> BusinessSocialPlatforms = ["page", "messenger"];

    public static readonly string BusinessSocialIdentitySql =
        $"i.identity_type = '{SocialIdentityType}' AND i.platform IN ({string.Join(", ", BusinessSocialPlatforms.Select(p => $"'{p}'"))}) AND i.status = 'active'";

    public static bool IsBusinessSocialChannel(string? identityType, string? platform, string? status = "active") =>
        string.Equals(identityType?.Trim(), SocialIdentityType, StringComparison.OrdinalIgnoreCase)
        && BusinessSocialPlatforms.Contains(platform?.Trim() ?? "", StringComparer.OrdinalIgnoreCase)
        && string.Equals(status?.Trim(), "active", StringComparison.OrdinalIgnoreCase);

    public static bool IsReachable(string? phone, string? email, string? website, bool hasBusinessSocialChannel) =>
        !string.IsNullOrWhiteSpace(phone)
        || !string.IsNullOrWhiteSpace(email)
        || !string.IsNullOrWhiteSpace(website)
        || hasBusinessSocialChannel;
}
