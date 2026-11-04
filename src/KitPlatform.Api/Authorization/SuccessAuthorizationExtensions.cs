using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class SuccessPolicies
{
    /// <summary>Owner cockpit / loss prevention — store-wide KPIs, not cashier POS.</summary>
    public const string Owner = "SuccessOwner";

    /// <summary>Shift checklist — xem. success.read chỉ xem, không tick.</summary>
    public const string Checklist = "SuccessChecklist";

    /// <summary>Tick / hoàn thành checklist. Không gồm success.read hay sales.write.</summary>
    public const string ChecklistMutate = "SuccessChecklistMutate";
}

public static class SuccessChecklistAccess
{
    public static bool CanView(bool isAdminRole, IEnumerable<string> permissions)
    {
        if (isAdminRole)
            return true;
        foreach (var permission in permissions)
        {
            if (permission is "success.read" or "success.checklist" or "success.write" or "sales.pos" or "sales.write")
                return true;
        }

        return false;
    }

    public static bool CanMutate(bool isAdminRole, IEnumerable<string> permissions)
    {
        if (isAdminRole)
            return true;
        foreach (var permission in permissions)
        {
            if (permission is "success.checklist" or "success.write" or "sales.pos")
                return true;
        }

        return false;
    }
}

public static class SuccessAuthorizationExtensions
{
    public static void AddSuccessAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(SuccessPolicies.Owner, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                    || ctx.User.IsInRole("MANAGER")
                    || ctx.User.IsInRole("BRANCH_MANAGER")
                    || HasPermission(ctx, "success.read")
                    || HasPermission(ctx, "reports.read")
                    || HasPermission(ctx, "reports.write")
                    || HasPermission(ctx, "reports.export"))));

        options.AddPolicy(SuccessPolicies.Checklist, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && SuccessChecklistAccess.CanView(ctx.User.IsInRole("ADMIN"), PermissionValues(ctx))));

        options.AddPolicy(SuccessPolicies.ChecklistMutate, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && SuccessChecklistAccess.CanMutate(ctx.User.IsInRole("ADMIN"), PermissionValues(ctx))));
    }

    private static IEnumerable<string> PermissionValues(AuthorizationHandlerContext ctx) =>
        ctx.User.Claims.Where(c => c.Type == "permission").Select(c => c.Value);

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
