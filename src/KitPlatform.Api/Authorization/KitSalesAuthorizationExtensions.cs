using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class KitSalesPolicies
{
    public const string Read = "KitSalesRead";
    public const string Write = "KitSalesWrite";
    public const string ApproveOutbound = "KitSalesApproveOutbound";
    public const string ReleaseOffer = "KitSalesReleaseOffer";
    public const string AutonomyGrant = "KitSalesAutonomyGrant";
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

        options.AddPolicy(KitSalesPolicies.ApproveOutbound, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && HasPermission(ctx, "kit_sales.approve_outbound")));

        options.AddPolicy(KitSalesPolicies.ReleaseOffer, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && HasPermission(ctx, "kit_sales.release_offer")));

        options.AddPolicy(KitSalesPolicies.AutonomyGrant, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && HasPermission(ctx, "kit_sales.autonomy_grant")));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);

    private static bool HasAny(AuthorizationHandlerContext ctx, params string[] permissions) =>
        permissions.Any(p => HasPermission(ctx, p));
}
