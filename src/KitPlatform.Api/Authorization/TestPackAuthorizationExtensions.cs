using Microsoft.AspNetCore.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.TestPack;

namespace KitPlatform.Api.Authorization;

public static class TestPackPolicies
{
    public const string Read = "TestPackRead";
}

public static class TestPackAuthorizationExtensions
{
    public static void AddTestPackAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(TestPackPolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (ctx.User.IsInRole("ADMIN")
                    || CoreAuthority.HasPermission(ctx.User, TestPackDefinition.ReadPermission))));
    }
}
