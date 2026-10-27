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
    public const string TaskCreate = "PersonalIntelligenceTaskCreate";
    public const string TaskRead = "PersonalIntelligenceTaskRead";
    public const string Permission = "personal_intelligence.trading.run";
    public const string ApprovePermission = "personal_intelligence.trading.approve";
    public const string ExchangeConnectPermission = "personal_intelligence.exchange.connect";
    public const string ExchangeReadPermission = "personal_intelligence.exchange.read";
    public const string ResearchReadPermission = "personal_intelligence.research.read";
    public const string EvidenceReadPermission = "personal_intelligence.evidence.read";
    public const string ModuleCode = PlatformModuleCodes.PersonalIntelligence;
}

public static class PersonalIntelligenceAuthorizationExtensions
{
    public static void AddPersonalIntelligenceAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(PersonalIntelligencePolicies.Run, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.Permission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.Approve, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.ApprovePermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.ExchangeConnect, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.ExchangeConnectPermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.ExchangeRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.ExchangeReadPermission)
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.ExchangeConnectPermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.ResearchRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.ResearchReadPermission)
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.Permission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.TaskCreate, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.Permission)
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.EvidenceReadPermission));
        });
        options.AddPolicy(PersonalIntelligencePolicies.TaskRead, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole("ADMIN")
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.ResearchReadPermission)
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.Permission)
                || CoreAuthority.HasPermission(ctx.User, PersonalIntelligencePolicies.EvidenceReadPermission));
        });
    }
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
