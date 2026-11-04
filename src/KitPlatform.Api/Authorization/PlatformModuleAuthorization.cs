using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KitPlatform.Application.Configuration;
using System.Security.Claims;

namespace KitPlatform.Api.Authorization;

public static class PlatformGatePolicies
{
    public const string ModulePrefix = "PlatformModule:";
    public const string FeaturePrefix = "PlatformFeature:";

    public static string Module(string moduleCode) => ModulePrefix + moduleCode;

    public static string Feature(string featureCode) => FeaturePrefix + featureCode;
}

/// <summary>
/// Solution pack module gate. Anonymous actions that carry a tenant are still checked.
/// AllowAnonymous with no tenant claim and no tenant code is not a module decision.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePlatformModuleAttribute : TypeFilterAttribute
{
    public RequirePlatformModuleAttribute(string moduleCode)
        : base(typeof(PlatformModuleGateFilter))
    {
        Arguments = [moduleCode];
    }
}

/// <summary>Tenant feature flag gate — runs for anonymous and authenticated actions.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePlatformFeatureAttribute : TypeFilterAttribute
{
    public RequirePlatformFeatureAttribute(string featureCode)
        : base(typeof(PlatformFeatureGateFilter))
    {
        Arguments = [featureCode];
    }
}

public sealed class PlatformModuleGateFilter : IAsyncActionFilter
{
    private readonly string _moduleCode;
    private readonly ITenantPlatformSettings _platform;

    public PlatformModuleGateFilter(string moduleCode, ITenantPlatformSettings platform)
    {
        _moduleCode = moduleCode;
        _platform = platform;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (IsAllowAnonymousWithoutTenant(context))
        {
            await next();
            return;
        }

        var enabled = await IsModuleEnabledAsync(context, cancellationToken: context.HttpContext.RequestAborted);
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

    private async Task<bool> IsModuleEnabledAsync(ActionExecutingContext context, CancellationToken cancellationToken)
    {
        if (PlatformGateTenantResolver.HasTenantClaim(context.HttpContext.User))
            return await _platform.IsModuleEnabledAsync(_moduleCode, cancellationToken);

        var tenantCode = PlatformGateTenantResolver.ResolveTenantCode(context);
        if (tenantCode is not null)
            return await _platform.IsModuleEnabledForTenantCodeAsync(tenantCode, _moduleCode, cancellationToken);

        return false;
    }

    private static bool IsAllowAnonymousWithoutTenant(ActionExecutingContext context)
    {
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null)
            return false;
        if (PlatformGateTenantResolver.HasTenantClaim(context.HttpContext.User))
            return false;
        return PlatformGateTenantResolver.ResolveTenantCode(context) is null;
    }
}

public sealed class PlatformFeatureGateFilter : IAsyncActionFilter
{
    private readonly string _featureCode;
    private readonly ITenantPlatformSettings _platform;

    public PlatformFeatureGateFilter(string featureCode, ITenantPlatformSettings platform)
    {
        _featureCode = featureCode;
        _platform = platform;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var enabled = await IsFeatureEnabledAsync(context, cancellationToken: context.HttpContext.RequestAborted);
        if (!enabled)
        {
            context.Result = new ObjectResult(new { message = "Feature is not enabled for this tenant." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }

    private async Task<bool> IsFeatureEnabledAsync(ActionExecutingContext context, CancellationToken cancellationToken)
    {
        if (PlatformGateTenantResolver.HasTenantClaim(context.HttpContext.User))
            return await _platform.IsFeatureEnabledAsync(_featureCode, cancellationToken);

        var tenantCode = PlatformGateTenantResolver.ResolveTenantCode(context);
        if (tenantCode is not null)
            return await _platform.IsFeatureEnabledForTenantCodeAsync(tenantCode, _featureCode, cancellationToken);

        return false;
    }
}

internal static class PlatformGateTenantResolver
{
    public static string? ResolveTenantCode(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Query.TryGetValue("tenantCode", out var queryValue)
            && !string.IsNullOrWhiteSpace(queryValue))
        {
            return queryValue.ToString().Trim();
        }

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var property = argument.GetType().GetProperty("TenantCode");
            if (property?.GetValue(argument) is string tenantCode && !string.IsNullOrWhiteSpace(tenantCode))
                return tenantCode.Trim();
        }

        return null;
    }

    public static bool HasTenantClaim(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("tenant_id")?.Value, out _);
}
