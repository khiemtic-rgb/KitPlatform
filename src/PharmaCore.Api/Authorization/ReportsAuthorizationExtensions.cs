using Microsoft.AspNetCore.Authorization;

namespace PharmaCore.Api.Authorization;

public static class ReportsPolicies
{
    public const string Read = "ReportsRead";
    public const string Export = "ReportsExport";
}

public static class ReportsAuthorizationExtensions
{
    public static void AddReportsAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(ReportsPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                || HasPermission(ctx, "reports.read")
                || HasPermission(ctx, "sales.read")
                || HasPermission(ctx, "sales.write")
                || HasPermission(ctx, "procurement.read")
                || HasPermission(ctx, "procurement.write")
                || HasPermission(ctx, "inventory.read")
                || HasPermission(ctx, "inventory.write"))));

        options.AddPolicy(ReportsPolicies.Export, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                || HasPermission(ctx, "reports.export")
                || HasPermission(ctx, "reports.read"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
