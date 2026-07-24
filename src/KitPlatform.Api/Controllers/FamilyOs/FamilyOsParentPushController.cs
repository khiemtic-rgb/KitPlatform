using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/parent-push")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsParentPushController : ControllerBase
{
    private readonly IFamilyOsParentPushService _push;

    public FamilyOsParentPushController(IFamilyOsParentPushService push) => _push = push;

    [HttpGet("status")]
    [ProducesResponseType(typeof(FamilyParentPushStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyParentPushStatusDto>> Status(
        Guid familyId,
        [FromQuery] Guid? membershipId,
        CancellationToken cancellationToken)
    {
        return Ok(await _push.GetStatusAsync(familyId, membershipId, cancellationToken));
    }

    [HttpPost("subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Subscribe(
        Guid familyId,
        [FromBody] FamilyParentPushSubscribeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _push.SubscribeAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unsubscribe(
        Guid familyId,
        [FromQuery] Guid membershipId,
        [FromQuery] string endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            await _push.UnsubscribeAsync(familyId, membershipId, endpoint, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
