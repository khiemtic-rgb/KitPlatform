using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class KitSalesPolicies
{
    public const string Read = "KitSalesRead";
    public const string Write = "KitSalesWrite";
}

public static class KitSalesAuthorizationExtensions
{
    public static void AddKitSalesAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(KitSalesPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                    || HasAny(ctx, "kit_sales.read", "kit_sales.write"))));

        options.AddPolicy(KitSalesPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN") || HasPermission(ctx, "kit_sales.write"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

    private static bool HasAny(AuthorizationHandlerContext ctx, params string[] permissions) =>
        permissions.Any(p => HasPermission(ctx, p));
}
