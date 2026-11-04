using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Core;

namespace KitPlatform.Api.Authorization;

/// <summary>
/// Làm mới role/permission từ DB cho JWT admin ERP trên mỗi request (cache 30s).
/// Đảm bảo thay đổi phân quyền có hiệu lực gần như ngay, không cần đăng nhập lại.
/// </summary>
public sealed class DbPermissionsClaimsTransformation : IClaimsTransformation
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IAuthService _auth;
    private readonly IMemoryCache _cache;

    public DbPermissionsClaimsTransformation(IAuthService auth, IMemoryCache cache)
    {
        _auth = auth;
        _cache = cache;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!AdminTokenRules.IsAdminPrincipal(principal))
            return principal;

        if (!CoreAuthority.TryResolveCaller(principal, out var caller))
        {
            var userIdClaim = principal.FindFirst(CoreAuthority.SubjectClaim)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out _))
                return principal;

            return StripPermissions(principal);
        }

        var user = await _cache.GetOrCreateAsync(
            $"auth-user-claims:{caller.TenantId:N}:{caller.UserId:N}",
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return _auth.GetUserAsync(caller.UserId, caller.TenantId);
            });

        var identity = (ClaimsIdentity)principal.Identity!;
        var refreshed = identity.Clone();
        var roleClaimType = refreshed.RoleClaimType;

        foreach (var stale in refreshed.Claims.Where(c => c.Type == CoreAuthority.PermissionClaim || c.Type == roleClaimType).ToList())
            refreshed.RemoveClaim(stale);

        // Missing, inactive, or cross-tenant: leave roles and permissions stripped.
        if (user is not null
            && user.Status == 1
            && CoreAuthority.AllowsSameTenant(caller.TenantId, user.TenantId))
        {
            foreach (var role in user.Roles)
                refreshed.AddClaim(new Claim(roleClaimType, role));
            foreach (var permission in user.Permissions)
                refreshed.AddClaim(new Claim(CoreAuthority.PermissionClaim, permission));
        }

        return new ClaimsPrincipal(refreshed);
    }

    private static ClaimsPrincipal StripPermissions(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var refreshed = identity.Clone();
        var roleClaimType = refreshed.RoleClaimType;
        foreach (var stale in refreshed.Claims.Where(c => c.Type == CoreAuthority.PermissionClaim || c.Type == roleClaimType).ToList())
            refreshed.RemoveClaim(stale);
        return new ClaimsPrincipal(refreshed);
    }
}
