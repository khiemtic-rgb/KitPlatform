namespace KitPlatform.Application.Identity;

/// <summary>
/// Platform-operator permissions. A tenant admin cannot grant or remove them, and new tenants never copy them.
/// They act on shared catalogs across every tenant.
/// </summary>
public static class ReservedPermissions
{
    public const string PlatformPrefix = "platform.";

    public static readonly IReadOnlyList<string> Codes =
    [
        "pharmacy.knowledge.review",
        "pharmacy.knowledge.verify-source",
    ];

    /// <summary>Predicate on <c>permissions p</c>: true for codes a tenant may hold through its own roles.</summary>
    public const string TenantAssignableSql =
        "(p.permission_code NOT LIKE 'platform.%' AND p.permission_code NOT IN ('pharmacy.knowledge.review', 'pharmacy.knowledge.verify-source'))";

    public static bool IsReserved(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && (code.Trim().StartsWith(PlatformPrefix, StringComparison.OrdinalIgnoreCase)
            || Codes.Contains(code.Trim(), StringComparer.OrdinalIgnoreCase));
}
