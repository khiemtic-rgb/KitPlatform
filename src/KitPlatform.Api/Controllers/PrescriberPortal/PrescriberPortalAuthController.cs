using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Rx;

namespace KitPlatform.Api.Controllers.PrescriberPortal;

[ApiController]
[Route("api/prescriber-portal/auth")]
public sealed class PrescriberPortalAuthController : ControllerBase
{
    private readonly IPrescriberPortalAuthService _auth;

    public PrescriberPortalAuthController(IPrescriberPortalAuthService auth) => _auth = auth;

    [HttpPost("otp-request")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.PublicOtp)]
    public async Task<ActionResult<PrescriberOtpSentResponse>> RequestOtp(
        [FromBody] PrescriberOtpRequest request,
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

    [HttpPost("otp-verify")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.PublicOtp)]
    public async Task<ActionResult<PrescriberLoginResponse>> VerifyOtp(
        [FromBody] PrescriberOtpVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.VerifyOtpAsync(request, cancellationToken);
        return result is null ? Unauthorized(new { message = "Mã OTP không hợp lệ hoặc đã hết hạn." }) : Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Policy = PrescriberPortalPolicies.Authenticated)]
    public async Task<ActionResult<PrescriberProfileDto>> Me(CancellationToken cancellationToken)
    {
        var prescriberId = Guid.Parse(
            User.FindFirst(PrescriberPortalAuthConstants.PrescriberIdClaim)!.Value);
        var profile = await _auth.GetProfileAsync(prescriberId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }
}
