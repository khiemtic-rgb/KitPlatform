using Microsoft.AspNetCore.Authorization;

namespace PharmaCore.Api.Authorization;

public static class CatalogAuthorizationExtensions
{
    public static void AddCatalogAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(CatalogPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "catalog.read") || ctx.User.IsInRole("ADMIN")));

        options.AddPolicy(CatalogPolicies.Write, policy =>
            policy.RequireAssertion(ctx =>
                HasPermission(ctx, "catalog.write") || ctx.User.IsInRole("ADMIN")));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
