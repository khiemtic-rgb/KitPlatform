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
public sealed class FamilyOsRitualController : ControllerBase
{
    private readonly IFamilyRitualService _rituals;

    public FamilyOsRitualController(IFamilyRitualService rituals) => _rituals = rituals;

    [HttpGet("rituals")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyRitualDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyRitualDto>>> List(
        Guid familyId,
        [FromQuery] DateOnly? asOf,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _rituals.ListWeekAsync(familyId, asOf, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("rituals/checkin")]
    [ProducesResponseType(typeof(FamilyRitualDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyRitualDto>> Checkin(
        Guid familyId,
        [FromBody] FamilyRitualCheckinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _rituals.CheckinAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
