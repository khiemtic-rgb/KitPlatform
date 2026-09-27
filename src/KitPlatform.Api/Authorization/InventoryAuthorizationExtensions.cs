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

        // Quầy bán cần chọn kho — không mở cả module Kho (inventory.read).
        options.AddPolicy(InventoryPolicies.WarehouseLookup, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                    || HasPermission(ctx, "inventory.read")
                    || HasPermission(ctx, "inventory.write")
                    || HasPermission(ctx, "sales.pos")
                    || HasPermission(ctx, "sales.write"))));

        options.AddPolicy(InventoryPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "inventory.write") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(InventoryPolicies.Approve, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "inventory.approve") || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(InventoryPolicies.ReviewRead, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                    || HasPermission(ctx, "reports.read")
                    || HasPermission(ctx, "reports.export")
                    || HasPermission(ctx, "inventory.read")
                    || HasPermission(ctx, "inventory.write"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
