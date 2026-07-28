using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os")]
public sealed class FamilyOsCommercialController : ControllerBase
{
    private readonly IFamilyCommercialService _commercial;

    public FamilyOsCommercialController(IFamilyCommercialService commercial) =>
        _commercial = commercial;

    /// <summary>Self-serve: create tenant + family + guardian user + trial subscription.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("family-os-public")]
    [HttpPost("register")]
    [ProducesResponseType(typeof(FamilyRegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyRegisterResponse>> Register(
        [FromBody] FamilyRegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _commercial.RegisterAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("family-os-public")]
    [HttpPost("invites/accept")]
    [ProducesResponseType(typeof(FamilyInviteAcceptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyInviteAcceptResponse>> AcceptInvite(
        [FromBody] FamilyInviteAcceptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _commercial.AcceptInviteAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpPost("families/{familyId:guid}/invites")]
    [ProducesResponseType(typeof(FamilyInviteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyInviteDto>> CreateInvite(
        Guid familyId,
        [FromBody] FamilyInviteCreateRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _commercial.CreateInviteAsync(
                familyId,
                request ?? new FamilyInviteCreateRequest(),
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpGet("families/{familyId:guid}/subscription")]
    [ProducesResponseType(typeof(FamilySubscriptionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilySubscriptionDto>> GetSubscription(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _commercial.GetSubscriptionAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Commercial packaging v1 — effective tier + capability list.</summary>
    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpGet("families/{familyId:guid}/capabilities")]
    [ProducesResponseType(typeof(FamilyCapabilityPackDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyCapabilityPackDto>> GetCapabilities(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _commercial.GetCapabilityPackAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpPut("families/{familyId:guid}/parent-pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetParentPin(
        Guid familyId,
        [FromBody] SetParentPinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _commercial.SetParentPinAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpPost("families/{familyId:guid}/parent-pin/verify")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyParentPin(
        Guid familyId,
        [FromBody] VerifyParentPinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var ok = await _commercial.VerifyParentPinAsync(familyId, request, cancellationToken);
            return Ok(new { valid = ok });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
