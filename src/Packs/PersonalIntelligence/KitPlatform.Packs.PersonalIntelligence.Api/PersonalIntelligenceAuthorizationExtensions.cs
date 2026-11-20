using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core;

namespace KitPlatform.Api.Authorization;

public static class PersonalIntelligencePolicies
{
    public const string Run = "PersonalIntelligenceTradingRun";
    public const string Approve = "PersonalIntelligenceTradingApprove";
    public const string ExchangeConnect = "PersonalIntelligenceExchangeConnect";
    public const string ExchangeRead = "PersonalIntelligenceExchangeRead";
    public const string ResearchRead = "PersonalIntelligenceResearchRead";
    public const string Intent = "PersonalIntelligenceIntent";
    public const string TaskRead = "PersonalIntelligenceTaskRead";
    public const string Permission = "personal_intelligence.trading.run";
    public const string ApprovePermission = "personal_intelligence.trading.approve";
    public const string ExchangeConnectPermission = "personal_intelligence.exchange.connect";
    public const string ExchangeReadPermission = "personal_intelligence.exchange.read";
    public const string ResearchReadPermission = "personal_intelligence.research.read";
    public const string PaperExecutePermission = "personal_intelligence.paper.execute";
    public const string PaperApprovePermission = "personal_intelligence.paper.approve";
    public const string IntentCreatePermission = "personal_intelligence.intent.create";
    public const string TaskReadPermission = "personal_intelligence.task.read";
    public const string AgentReadPermission = "agent.read";
    public const string ModuleCode = PlatformModuleCodes.PersonalIntelligence;
}

public static class PersonalIntelligenceAuthorizationExtensions
{
    public static void AddPersonalIntelligenceAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(PersonalIntelligencePolicies.Run, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx,
                PersonalIntelligencePolicies.Permission,
                PersonalIntelligencePolicies.PaperExecutePermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.Approve, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx,
                PersonalIntelligencePolicies.ApprovePermission,
                PersonalIntelligencePolicies.PaperApprovePermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.ExchangeConnect, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx, PersonalIntelligencePolicies.ExchangeConnectPermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.ExchangeRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx,
                PersonalIntelligencePolicies.ExchangeReadPermission,
                PersonalIntelligencePolicies.ExchangeConnectPermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.ResearchRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx, PersonalIntelligencePolicies.ResearchReadPermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.Intent, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx,
                PersonalIntelligencePolicies.IntentCreatePermission,
                PersonalIntelligencePolicies.Permission,
                PersonalIntelligencePolicies.PaperExecutePermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.TaskRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => Allows(ctx,
                PersonalIntelligencePolicies.TaskReadPermission,
                PersonalIntelligencePolicies.IntentCreatePermission,
                PersonalIntelligencePolicies.Permission,
                PersonalIntelligencePolicies.PaperExecutePermission,
                PersonalIntelligencePolicies.ApprovePermission,
                PersonalIntelligencePolicies.PaperApprovePermission));
        });
    }

    static bool Allows(AuthorizationHandlerContext context, params string[] permissions) =>
        context.User.IsInRole("ADMIN")
        || permissions.Any(permission => CoreAuthority.HasPermission(context.User, permission));
}

/// <summary>
/// Pack adapter for the platform module gate. The host filter type lives in KitPlatform.Api,
/// which cannot be referenced from this project. The check itself is <see cref="ITenantPlatformSettings"/>.
/// </summary>
public sealed class RequirePersonalIntelligenceModuleAttribute : TypeFilterAttribute
{
    public RequirePersonalIntelligenceModuleAttribute()
        : base(typeof(PersonalIntelligenceModuleFilter))
    {
    }
}

public sealed class PersonalIntelligenceModuleFilter : IAsyncActionFilter
{
    private readonly ITenantPlatformSettings _platform;

    public PersonalIntelligenceModuleFilter(ITenantPlatformSettings platform) => _platform = platform;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var enabled = await _platform.IsModuleEnabledAsync(
            PersonalIntelligencePolicies.ModuleCode,
            context.HttpContext.RequestAborted);
        if (!enabled)
        {
            context.Result = new ObjectResult(new { message = "Module is not enabled for this tenant." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
