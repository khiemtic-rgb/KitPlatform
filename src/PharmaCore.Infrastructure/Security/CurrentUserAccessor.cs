using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Infrastructure.Security;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public IReadOnlyList<string> Permissions =>
        _httpContextAccessor.HttpContext?.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .Distinct()
            .ToList()
        ?? [];

    public bool IsInRole(string role) =>
        _httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}
