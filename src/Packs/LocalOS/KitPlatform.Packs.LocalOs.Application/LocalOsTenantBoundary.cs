namespace KitPlatform.Packs.LocalOs;

/// <summary>
/// Dedicated park. <see cref="LocalOsPackDefinition.DedicatedTenantCode"/> owns <c>pack_local</c>.
/// KIT_MKT may operate the same rows (migration 320). It is not a second data owner.
/// </summary>
public static class LocalOsTenantBoundary
{
    public const string OperatorMarketingCode = "KIT_MKT";

    public static readonly string[] AdminOperatorCodes =
    [
        LocalOsPackDefinition.DedicatedTenantCode,
        OperatorMarketingCode,
    ];

    public static bool IsAdminOperator(string? tenantCode)
    {
        if (string.IsNullOrWhiteSpace(tenantCode))
            return false;

        var code = tenantCode.Trim();
        return AdminOperatorCodes.Contains(code, StringComparer.OrdinalIgnoreCase);
    }
}
