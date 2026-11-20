using Microsoft.AspNetCore.Mvc.Filters;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Fails closed before a production action when the authenticated tenant claim is missing.
/// Does not read a body tenant id or a workspace id.
/// </summary>
public sealed class ProductionTenantActionFilter : IAsyncActionFilter
{
    readonly IProductionCallerTenant _caller;

    public ProductionTenantActionFilter(IProductionCallerTenant caller) => _caller = caller;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        _caller.RequireTenant();
        await next();
    }
}
