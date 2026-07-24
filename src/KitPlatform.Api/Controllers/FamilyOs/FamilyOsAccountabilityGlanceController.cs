using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/accountability-glance")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsAccountabilityGlanceController : ControllerBase
{
    private readonly IFamilyAccountabilityGlanceService _glance;

    public FamilyOsAccountabilityGlanceController(IFamilyAccountabilityGlanceService glance) =>
        _glance = glance;

    [HttpGet]
    [ProducesResponseType(typeof(AccountabilityGlanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountabilityGlanceDto>> Get(
        Guid familyId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _glance.GetGlanceAsync(familyId, from, to, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
