using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/coach-insight")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsCoachInsightController : ControllerBase
{
    private readonly IFamilyCoachInsightService _coach;

    public FamilyOsCoachInsightController(IFamilyCoachInsightService coach) => _coach = coach;

    [HttpGet]
    [ProducesResponseType(typeof(FamilyCoachInsightDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyCoachInsightDto>> Get(
        Guid familyId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _coach.GetInsightAsync(familyId, date, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
