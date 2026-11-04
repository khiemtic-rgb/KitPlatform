using Microsoft.AspNetCore.Authorization;
using KitPlatform.Application.Core;

namespace KitPlatform.Api.Authorization;

public static class IdentityAuthorizationExtensions
{
    public static void AddIdentityAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(IdentityPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "system.read")
                || HasPermission(ctx, "system.write")
                || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(IdentityPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "system.write") || ctx.User.IsInRole("ADMIN"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        CoreAuthority.HasPermission(ctx.User, permission);
}
