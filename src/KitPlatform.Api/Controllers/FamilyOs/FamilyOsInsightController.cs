using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/insight")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsInsightController : ControllerBase
{
    private readonly IFamilyInsightService _insight;

    public FamilyOsInsightController(IFamilyInsightService insight) =>
        _insight = insight;

    [HttpGet("weekly")]
    [ProducesResponseType(typeof(FamilyWeeklyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyWeeklyReportDto>> Weekly(
        Guid familyId,
        [FromQuery] DateOnly? asOf,
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _insight.GetWeeklyReportAsync(familyId, asOf, days, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
