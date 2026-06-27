using Microsoft.AspNetCore.Authorization;

namespace PharmaCore.Api.Authorization;

public static class ProcurementAuthorizationExtensions
{
    public static void AddProcurementAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(ProcurementPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "procurement.read") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(ProcurementPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "procurement.write") || ctx.User.IsInRole("ADMIN"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
