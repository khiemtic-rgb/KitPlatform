using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using KitPlatform.Api.Controllers.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class ProductionTenantActionFilterSharedSeriesTests
{
    private static readonly string[] SharedSeriesActions =
    [
        nameof(ContentController.ListSeriesBuilds),
        nameof(ContentController.GetSeriesBuild),
        nameof(ContentController.PutSeriesBuild),
        nameof(ContentController.DeleteSeriesBuild),
        nameof(ContentController.ListProductionProgress),
        nameof(ContentController.GetProductionProgress),
    ];

    [Fact]
    public void Six_series_actions_keep_the_filter_attribute()
    {
        foreach (var name in SharedSeriesActions)
        {
            var method = typeof(ContentController).GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);
            var filters = method!.GetCustomAttributes<ServiceFilterAttribute>(inherit: true);
            Assert.Contains(filters, filter => filter.ServiceType == typeof(ProductionTenantActionFilter));
        }
    }

    [Fact]
    public async Task Active_tenant_does_not_prove_ownership_of_a_series_build()
    {
        Assert.False(ProductionTenantActionFilter.EstablishesSeriesResourceOwnership);

        var filter = new ProductionTenantActionFilter();
        var buildId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var first = await Execute(filter, tenantA, buildId);
        var second = await Execute(filter, tenantB, buildId);
        var anonymous = await Execute(filter, tenantId: null, buildId);

        Assert.True(first.Continued);
        Assert.True(second.Continued);
        Assert.True(anonymous.Continued);
        Assert.Null(first.Context.Result);
        Assert.Null(second.Context.Result);
        Assert.Null(anonymous.Context.Result);
    }

    private static async Task<(bool Continued, ActionExecutingContext Context)> Execute(
        ProductionTenantActionFilter filter,
        Guid? tenantId,
        Guid buildId)
    {
        var http = new DefaultHttpContext();
        if (tenantId is Guid tid)
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("tenant_id", tid.ToString())],
                authenticationType: "TestAuth");
            http.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }

        var route = new RouteData();
        route.Values["id"] = buildId;
        var context = new ActionExecutingContext(
            new ActionContext(http, route, new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["id"] = buildId },
            controller: null!);
        var continued = false;

        await filter.OnActionExecutionAsync(
            context,
            () =>
            {
                continued = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!));
            });

        return (continued, context);
    }
}
