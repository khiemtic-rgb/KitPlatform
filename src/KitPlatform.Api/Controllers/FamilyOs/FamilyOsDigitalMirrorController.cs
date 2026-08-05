using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/mirror")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsDigitalMirrorController : ControllerBase
{
    private readonly IFamilyDigitalMirrorService _mirror;

    public FamilyOsDigitalMirrorController(IFamilyDigitalMirrorService mirror) => _mirror = mirror;

    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Heartbeat(
        Guid familyId,
        [FromBody] FamilyMirrorHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mirror.HeartbeatAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("usage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestUsage(
        Guid familyId,
        [FromBody] FamilyMirrorUsageIngestRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mirror.IngestUsageAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("day")]
    [ProducesResponseType(typeof(FamilyMirrorDayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMirrorDayDto>> GetDay(
        Guid familyId,
        [FromQuery] Guid? memberId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _mirror.GetDayAsync(familyId, memberId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("notes")]
    [ProducesResponseType(typeof(FamilyMirrorParentNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMirrorParentNoteDto>> PostNote(
        Guid familyId,
        [FromBody] FamilyMirrorParentNoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _mirror.PostParentNoteAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
