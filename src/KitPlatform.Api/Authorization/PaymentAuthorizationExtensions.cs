using Microsoft.AspNetCore.Authorization;

namespace KitPlatform.Api.Authorization;

public static class PaymentPolicies
{
    public const string OpsActivate = "PaymentOpsActivate";
    public const string OpsRead = "PaymentOpsRead";
}

public static class PaymentAuthorizationExtensions
{
    public static void AddPaymentAuthorization(this AuthorizationOptions options)
    {
        // Strict: payment.ops.* only — Family OS self-serve ADMIN must NOT get these via platform.% wildcards.
        options.AddPolicy(PaymentPolicies.OpsActivate, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "payment.ops.activate")
                    || ctx.User.IsInRole("PLATFORM_OPS"))));

        options.AddPolicy(PaymentPolicies.OpsRead, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)
                && (HasPermission(ctx, "payment.ops.read")
                    || HasPermission(ctx, "payment.ops.activate")
                    || ctx.User.IsInRole("PLATFORM_OPS"))));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
