using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Api.Authorization;

/// <summary>Admin Local OS routes: caller tenant comes from <see cref="ITenantContext"/>, then the tenant code is read from <c>tenants</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireLocalOsOperatorAttribute : TypeFilterAttribute
{
    public RequireLocalOsOperatorAttribute()
        : base(typeof(LocalOsOperatorFilter))
    {
    }
}

public sealed class LocalOsOperatorFilter : IAsyncActionFilter
{
    private readonly ITenantContext _tenant;
    private readonly IDbConnectionFactory _db;

    public LocalOsOperatorFilter(ITenantContext tenant, IDbConnectionFactory db)
    {
        _tenant = tenant;
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(context.HttpContext.RequestAborted);
        var code = await conn.QuerySingleOrDefaultAsync<string>(
            """
            SELECT tenant_code
            FROM tenants
            WHERE id = @Id AND deleted_at IS NULL
            """,
            new { Id = _tenant.TenantId });

        if (!LocalOsTenantBoundary.IsAdminOperator(code))
        {
            context.Result = new ObjectResult(new { message = "Local OS admin is only available to the dedicated park operators." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
