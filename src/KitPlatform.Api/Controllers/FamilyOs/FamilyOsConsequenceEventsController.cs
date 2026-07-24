using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/consequence-events")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsConsequenceEventsController : ControllerBase
{
    private readonly IFamilyConsequenceService _consequences;

    public FamilyOsConsequenceEventsController(IFamilyConsequenceService consequences) =>
        _consequences = consequences;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyConsequenceEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyConsequenceEventDto>>> List(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _consequences.ListAsync(familyId, flowDate, status, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("{eventId:guid}/decide")]
    [ProducesResponseType(typeof(FamilyConsequenceEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyConsequenceEventDto>> Decide(
        Guid familyId,
        Guid eventId,
        [FromBody] DecideConsequenceEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _consequences.DecideAsync(familyId, eventId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
