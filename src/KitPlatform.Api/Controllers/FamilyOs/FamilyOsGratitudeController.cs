using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsGratitudeController : ControllerBase
{
    private readonly IFamilyGratitudeService _gratitude;

    public FamilyOsGratitudeController(IFamilyGratitudeService gratitude) => _gratitude = gratitude;

    [HttpGet("gratitude")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyChildGratitudeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyChildGratitudeDto>>> List(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        [FromQuery] Guid? fromMemberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _gratitude.ListAsync(familyId, flowDate, fromMemberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("gratitude")]
    [ProducesResponseType(typeof(FamilyChildGratitudeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyChildGratitudeDto>> Send(
        Guid familyId,
        [FromBody] FamilyChildGratitudeSendRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _gratitude.SendAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("gratitude/{gratitudeId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkRead(
        Guid familyId,
        Guid gratitudeId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _gratitude.MarkReadAsync(familyId, gratitudeId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
