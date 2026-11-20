namespace KitPlatform.Infrastructure.Configuration;

/// <summary>
/// Settings-path copy of <c>LocalOsTenantBoundary.AdminOperatorCodes</c>.
/// Infrastructure does not reference the Local OS project. Tests lock the two lists together.
/// </summary>
internal static class LocalOsDedicatedOperators
{
    public static bool Allows(string? tenantCode)
    {
        if (string.IsNullOrWhiteSpace(tenantCode))
            return false;

        var code = tenantCode.Trim();
        return code.Equals("KIT_LOCAL", StringComparison.OrdinalIgnoreCase)
            || code.Equals("KIT_MKT", StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> StripUnlessOperator(
        string? tenantCode,
        IReadOnlyList<string> modules,
        List<string> ignored)
    {
        if (Allows(tenantCode) || !modules.Any(IsLocalOs))
            return modules.ToList();

        ignored.Add("local_os");
        return modules.Where(module => !IsLocalOs(module)).ToList();
    }

    public static void RejectEntitlement(
        string? tenantCode,
        IReadOnlyList<string> allowed,
        IReadOnlyList<string> enabled)
    {
        if (Allows(tenantCode))
            return;

        if (allowed.Any(IsLocalOs) || enabled.Any(IsLocalOs))
        {
            throw new InvalidOperationException(
                "Module local_os chỉ được cấp cho KIT_LOCAL (chủ park) hoặc KIT_MKT (vận hành cùng dữ liệu).");
        }
    }

    private static bool IsLocalOs(string module) =>
        module.Equals("local_os", StringComparison.OrdinalIgnoreCase);
}
