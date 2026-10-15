using Microsoft.AspNetCore.Mvc.Filters;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Marker for six shared series routes. It is not a tenant-ownership check.
/// Decision Register section D-06 records that duty: customer ownership of an episode,
/// shot, or generation row is <c>ProductionCustomerOwner</c>, not this filter.
/// Does not query a tenant directory and does not decide whether the caller owns a build id.
/// <c>series_build</c> has no customer column in this phase, so these routes stay a shared series surface.
/// An active tenant is not ownership of the build id.
/// </summary>
public sealed class ProductionTenantActionFilter : IAsyncActionFilter
{
    public const bool EstablishesSeriesResourceOwnership = false;

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        => next();
}
