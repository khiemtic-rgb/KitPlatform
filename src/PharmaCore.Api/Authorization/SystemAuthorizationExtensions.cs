using Microsoft.AspNetCore.Authorization;

namespace PharmaCore.Api.Authorization;

public static class SystemAuthorizationExtensions
{
    public static void AddSystemAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(SystemPolicies.DeletePermanent, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "system.delete_permanent") || ctx.User.IsInRole("ADMIN")));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
