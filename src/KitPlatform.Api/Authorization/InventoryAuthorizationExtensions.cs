using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class InventoryAuthorizationExtensions
{
    public static void AddInventoryAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(InventoryPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "inventory.read") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(InventoryPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "inventory.write") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(InventoryPolicies.Approve, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "inventory.approve") || ctx.User.IsInRole("ADMIN"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
