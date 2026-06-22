using Microsoft.AspNetCore.Authorization;

namespace PharmaCore.Api.Authorization;

public static class SalesAuthorizationExtensions
{
    public static void AddSalesAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(SalesPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "sales.read") || HasPermission(ctx, "sales.write") || ctx.User.IsInRole("ADMIN")));

        options.AddPolicy(SalesPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "sales.write") || ctx.User.IsInRole("ADMIN")));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
