using Microsoft.AspNetCore.Authorization;

namespace PharmaCore.Api.Authorization;

public static class IdentityAuthorizationExtensions
{
    public static void AddIdentityAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(IdentityPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "system.read")
                || HasPermission(ctx, "system.write")
                || ctx.User.IsInRole("ADMIN")));

        options.AddPolicy(IdentityPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "system.write") || ctx.User.IsInRole("ADMIN")));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
