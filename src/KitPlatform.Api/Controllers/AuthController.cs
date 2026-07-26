using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Application.Auth;

namespace KitPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>
    /// Đăng nhập — tenant+username (cũ) hoặc email Kit (không gửi tenantCode).
    /// Nhiều workspace → { requiresWorkspaceChoice, selectionToken, workspaces }.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginWorkspaceChoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result is null)
            return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu." });

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
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _auth.GetUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }
}
