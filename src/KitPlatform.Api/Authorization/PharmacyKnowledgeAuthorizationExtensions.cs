using Microsoft.AspNetCore.Authorization;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Api.Authorization;

public static class PharmacyKnowledgePolicies
{
    public const string Approve = "PharmacyKnowledgeApprove";
    public const string Publish = "PharmacyKnowledgePublish";
    public const string Review = "PharmacyKnowledgeReview";
    public const string VerifySource = "PharmacyKnowledgeVerifySource";
    public const string Read = "PharmacyKnowledgeRead";
}

public static class PharmacyOwnedKnowledgePolicies
{
    public const string Manage = "PharmacyOwnedKnowledgeManage";
    public const string Use = "PharmacyOwnedKnowledgeUse";
}

public static class PharmacyKnowledgeAuthorizationExtensions
{
    public static void AddPharmacyKnowledgeAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(PharmacyKnowledgePolicies.Approve, policy =>
            policy.RequireAssertion(ctx =>
                PharmacyKnowledgeApprovalAccess.CanApprove(
                    AdminTokenRules.IsAdminPrincipal(ctx.User),
                    HasPermission(ctx, "pharmacy.knowledge.approve"),
                    ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(PharmacyKnowledgePolicies.Publish, policy =>
            policy.RequireAssertion(ctx =>
                PharmacyKnowledgeApprovalAccess.CanPublish(
                    AdminTokenRules.IsAdminPrincipal(ctx.User),
                    HasPermission(ctx, PharmacyKnowledgeApprovalAccess.PlatformPublishPermission))));

        options.AddPolicy(PharmacyKnowledgePolicies.Review, policy =>
            policy.RequireAssertion(ctx =>
                PharmacyKnowledgeApprovalAccess.CanReview(
                    AdminTokenRules.IsAdminPrincipal(ctx.User),
                    HasPermission(ctx, PharmacyKnowledgeApprovalAccess.ReviewPermission))));

        options.AddPolicy(PharmacyKnowledgePolicies.VerifySource, policy =>
            policy.RequireAssertion(ctx =>
                PharmacyKnowledgeApprovalAccess.CanVerifySource(
                    AdminTokenRules.IsAdminPrincipal(ctx.User),
                    HasPermission(ctx, PharmacyKnowledgeApprovalAccess.VerifySourcePermission))));

        options.AddPolicy(PharmacyKnowledgePolicies.Read, policy =>
            policy.RequireAssertion(ctx =>
                PharmacyKnowledgeApprovalAccess.CanReadCatalog(
                    AdminTokenRules.IsAdminPrincipal(ctx.User),
                    HasPermission(ctx, PharmacyKnowledgeApprovalAccess.PlatformPublishPermission),
                    HasPermission(ctx, PharmacyKnowledgeApprovalAccess.ReviewPermission),
                    HasPermission(ctx, PharmacyKnowledgeApprovalAccess.VerifySourcePermission))));

        options.AddPolicy(PharmacyOwnedKnowledgePolicies.Manage, policy =>
            policy.RequireAssertion(ctx =>
                PharmacyOwnedKnowledgeAccess.CanManage(
                    AdminTokenRules.IsAdminPrincipal(ctx.User),
                    ctx.User.IsInRole("ADMIN"))));

        options.AddPolicy(PharmacyOwnedKnowledgePolicies.Use, policy =>
            policy.RequireAssertion(ctx =>
                AdminTokenRules.IsAdminPrincipal(ctx.User)));
    }

    private static bool HasPermission(AuthorizationHandlerContext ctx, string permission) =>
        ctx.User.Claims.Any(c => c.Type == "permission" && c.Value == permission);
}
