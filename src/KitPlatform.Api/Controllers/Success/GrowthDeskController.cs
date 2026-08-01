using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Success;

namespace KitPlatform.Api.Controllers.Success;

[ApiController]
[Authorize]
[Route("api/success/growth")]
public sealed class GrowthDeskController : ControllerBase
{
    private readonly IGrowthDeskService _growth;

    public GrowthDeskController(IGrowthDeskService growth) => _growth = growth;

    /// <summary>Growth Desk P0 — refill opportunities for today (VN calendar).</summary>
    [HttpGet("opportunities/today")]
    [Authorize(Policy = SuccessPolicies.Owner)]
    [ProducesResponseType(typeof(GrowthOpportunitiesTodayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GrowthOpportunitiesTodayDto>> OpportunitiesToday(
        CancellationToken cancellationToken) =>
        Ok(await _growth.GetOpportunitiesTodayAsync(cancellationToken));

    /// <summary>Staff "Chăm sóc ngay": create refill draft from suggestion + care_action log.</summary>
    [HttpPost("opportunities/{suggestionId:guid}/care-now")]
    [Authorize(Policy = SuccessPolicies.Owner)]
    [ProducesResponseType(typeof(GrowthCareNowResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GrowthCareNowResultDto>> CareNow(
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _growth.CareNowAsync(suggestionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Weekly refill funnel: due / notified / converted / attributed revenue.</summary>
    [HttpGet("reports/weekly-refill")]
    [Authorize(Policy = SuccessPolicies.Owner)]
    [ProducesResponseType(typeof(GrowthWeeklyRefillReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GrowthWeeklyRefillReportDto>> WeeklyRefill(
        [FromQuery] DateOnly? weekStart = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _growth.GetWeeklyRefillReportAsync(weekStart, cancellationToken));
}
