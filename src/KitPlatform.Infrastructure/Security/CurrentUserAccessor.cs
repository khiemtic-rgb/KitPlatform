using Microsoft.AspNetCore.Http;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;

namespace KitPlatform.Infrastructure.Security;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public IReadOnlyList<string> Permissions =>
        _httpContextAccessor.HttpContext?.User.Claims
            .Where(c => c.Type == CoreAuthority.PermissionClaim)
            .Select(c => c.Value)
            .Distinct()
            .ToList()
        ?? [];

    public bool IsInRole(string role) =>
        _httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}
