using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class SalesAuthorizationExtensions
{
    public static void AddSalesAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(SalesPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasAny(ctx, "sales.read", "sales.write", "sales.pos", "sales.customers", "sales.settings")
                    || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "sales.write") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.Pos, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasAny(ctx, "sales.pos", "sales.write") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.Customers, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasAny(ctx, "sales.customers", "sales.write") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.PosOrCustomers, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasAny(ctx, "sales.pos", "sales.customers", "sales.write")
                    || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.Settings, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasAny(ctx, "sales.settings", "sales.write") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.Cancel, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "sales.cancel") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(SalesPolicies.CustomerMerge, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "sales.customers.merge") || ctx.User.IsInRole("ADMIN"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

    private static bool HasAny(AuthorizationHandlerContext ctx, params string[] permissions) =>
        permissions.Any(p => HasPermission(ctx, p));
}
