using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class ClinicAuthorizationExtensions
{
    public static void AddClinicAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(ClinicPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "clinic.read")
                    || HasPermission(ctx, "clinic.write")
                    || ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(ClinicPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "clinic.write") || ctx.User.IsInRole("ADMIN"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
