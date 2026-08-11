using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Api.Controllers.CustomerApp;

[ApiController]
[Route("api/customer-app/auth")]
public sealed class CustomerAppAuthController : ControllerBase
{
    private readonly ICustomerAppAuthService _auth;

    public CustomerAppAuthController(ICustomerAppAuthService auth) => _auth = auth;

    /// <summary>Gửi OTP — demo: SĐT 0909123456 (Trần Thị Mai), tenant DEMO_PHARMACY.</summary>
    [HttpPost("request-otp")]
    [AllowAnonymous]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerOtpSentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestCustomerOtpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _auth.RequestOtpAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyCustomerOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.VerifyOtpAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        return result is null
            ? Unauthorized(new { message = "Mã OTP không đúng hoặc đã hết hạn." })
            : Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] CustomerRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.RefreshAsync(request, cancellationToken);
        return result is null ? Unauthorized(new { message = "Refresh token không hợp lệ." }) : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize(Policy = CustomerAppPolicies.Authenticated)]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    public async Task<IActionResult> Logout(
        [FromBody] CustomerRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request.RefreshToken, cancellationToken);
        return Ok(new { message = "Đã đăng xuất." });
    }

    [HttpGet("me")]
    [Authorize(Policy = CustomerAppPolicies.Authenticated)]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(accountIdClaim, out var accountId))
            return Unauthorized();

        var profile = await _auth.GetProfileAsync(accountId, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPatch("locale")]
    [Authorize(Policy = CustomerAppPolicies.Authenticated)]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLocale(
        [FromBody] UpdateCustomerPreferredLocaleRequest request,
        CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(accountIdClaim, out var accountId))
            return Unauthorized();

        try
        {
            var profile = await _auth.UpdatePreferredLocaleAsync(
                accountId,
                request.PreferredLocale,
                cancellationToken);
            return profile is null ? Unauthorized() : Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("profile")]
    [Authorize(Policy = CustomerAppPolicies.Authenticated)]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(accountIdClaim, out var accountId))
            return Unauthorized();

        try
        {
            var profile = await _auth.UpdateProfileAsync(accountId, request, cancellationToken);
            return profile is null ? Unauthorized() : Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("avatar")]
    [Authorize(Policy = CustomerAppPolicies.Authenticated)]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2 * 1024 * 1024)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadAvatar(
        IFormFile file,
        [FromServices] IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(accountIdClaim, out var accountId))
            return Unauthorized();

        try
        {
            var profile = await _auth.GetProfileAsync(accountId, cancellationToken);
            if (profile is null)
                return Unauthorized();

            var url = await SaveAvatarAsync(file, profile.TenantId, profile.AccountId, environment, cancellationToken);
            var updated = await _auth.UpdateAvatarUrlAsync(accountId, url, cancellationToken);
            return updated is null ? Unauthorized() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("pharmacy-link")]
    [Authorize(Policy = CustomerAppPolicies.Authenticated)]
    [RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
    [ProducesResponseType(typeof(CustomerProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmPharmacyLink(
        [FromBody] ConfirmCustomerPharmacyLinkRequest? request,
        CancellationToken cancellationToken)
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(accountIdClaim, out var accountId))
            return Unauthorized();

        try
        {
            var profile = await _auth.ConfirmPharmacyLinkAsync(
                accountId,
                request ?? new ConfirmCustomerPharmacyLinkRequest(),
                cancellationToken);
            return profile is null ? Unauthorized() : Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<string> SaveAvatarAsync(
        IFormFile file,
        Guid tenantId,
        Guid accountId,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        const long maxBytes = 2 * 1024 * 1024;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        if (file is null || file.Length == 0)
            throw new InvalidOperationException("Chọn ảnh để tải lên.");
        if (file.Length > maxBytes)
            throw new InvalidOperationException("Ảnh tối đa 2 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowed.Contains(extension))
            throw new InvalidOperationException("Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP.");

        var tenantFolder = tenantId.ToString("N");
        var accountFolder = accountId.ToString("N");
        var directory = Path.Combine(
            environment.ContentRootPath,
            "uploads",
            "avatars",
            tenantFolder,
            accountFolder);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(directory, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/avatars/{tenantFolder}/{accountFolder}/{fileName}";
    }
}
