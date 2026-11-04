using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Core;

namespace KitPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly LoginAttemptGuard _attempts;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService auth, LoginAttemptGuard attempts, ILogger<AuthController> logger)
    {
        _auth = auth;
        _attempts = attempts;
        _logger = logger;
    }

    /// <summary>
    /// Đăng nhập — tenant+username (cũ) hoặc email Kit (không gửi tenantCode).
    /// Nhiều workspace → { requiresWorkspaceChoice, selectionToken, workspaces }.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginWorkspaceChoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var accountKey = LoginAttemptGuard.AccountKey(request.TenantCode, request.Username);
        if (_attempts.GetLockRemaining(accountKey, ip) is TimeSpan locked)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(locked.TotalMinutes));
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                code = "login_locked",
                message = $"Đăng nhập sai nhiều lần. Thử lại sau {minutes} phút.",
            });
        }

        AuthLoginResult? result;
        try
        {
            result = await _auth.LoginAsync(request, ip, cancellationToken);
        }
        catch (AuthTenantRequiredException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (result is null)
        {
            _attempts.RecordFailure(accountKey, ip);
            _logger.LogWarning("Login failed for {AccountKey} from {Ip}", accountKey, ip);
            return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu." });
        }

        _attempts.RecordSuccess(accountKey, ip);

        if (result.RequiresWorkspaceChoice)
        {
            return Ok(new LoginWorkspaceChoiceResponse(
                true,
                result.SelectionToken!,
                result.Workspaces ?? []));
        }

        return Ok(result.Session);
    }

    [HttpPost("select-workspace")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SelectWorkspace(
        [FromBody] SelectWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.SelectWorkspaceAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return result is null
            ? Unauthorized(new { message = "Phiên chọn workspace không hợp lệ hoặc đã hết hạn." })
            : Ok(result);
    }

    [HttpGet("workspaces")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<AuthWorkspaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Workspaces(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var workspaces = await _auth.ListWorkspacesAsync(userId, cancellationToken);
        return Ok(workspaces);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.RefreshAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result is null ? Unauthorized(new { message = "Refresh token không hợp lệ." }) : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request.RefreshToken, cancellationToken);
        return Ok(new { message = "Đã đăng xuất." });
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!CoreAuthority.TryResolveCaller(User, out var caller))
            return Unauthorized();

        var user = await _auth.GetUserAsync(caller.UserId, caller.TenantId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }
}
