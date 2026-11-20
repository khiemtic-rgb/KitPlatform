using Microsoft.AspNetCore.Http;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;

namespace KitPlatform.Infrastructure.Security;

public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId => CoreAuthority.RequireUserId(Principal);

    public Guid TenantId => CoreAuthority.RequireTenantId(Principal);

    public Guid? WorkspaceId =>
        _httpContextAccessor.HttpContext?.Items.TryGetValue(
            WorkspaceResolutionMiddleware.WorkspaceIdItemKey,
            out var value) == true
        && value is Guid id
            ? id
            : null;

    private System.Security.Claims.ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;
}
