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
public sealed class FamilyOsCooperationScoreController : ControllerBase
{
    private readonly IFamilyCooperationScoreService _scores;

    public FamilyOsCooperationScoreController(IFamilyCooperationScoreService scores) =>
        _scores = scores;

    [HttpGet("cooperation-score")]
    [ProducesResponseType(typeof(FamilyCooperationScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyCooperationScoreDto>> Get(
        Guid familyId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scores.GetAsync(familyId, period, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
