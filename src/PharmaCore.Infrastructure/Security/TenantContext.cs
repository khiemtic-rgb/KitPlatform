using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PharmaCore.Application.Abstractions;

namespace PharmaCore.Infrastructure.Security;

public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId => ParseGuid(
        GetClaim(ClaimTypes.NameIdentifier) ?? GetClaim("sub"),
        "User id claim missing.");

    public Guid TenantId => ParseGuid(GetClaim("tenant_id"), "Tenant id claim missing.");

    private string? GetClaim(string type) =>
        _httpContextAccessor.HttpContext?.User.FindFirst(type)?.Value;

    private static Guid ParseGuid(string? value, string error) =>
        Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException(error);
}
