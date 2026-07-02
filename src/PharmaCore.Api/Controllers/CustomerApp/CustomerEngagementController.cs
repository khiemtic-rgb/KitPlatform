using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Api.Controllers.CustomerApp;

[ApiController]
[Authorize]
[Route("api/customer-engagement")]
public sealed class CustomerEngagementController : ControllerBase
{
    private readonly ICustomerEngagementAnalyticsService _analytics;

    public CustomerEngagementController(ICustomerEngagementAnalyticsService analytics) => _analytics = analytics;

    [HttpGet("overview")]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType(typeof(CustomerEngagementOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerEngagementOverviewDto>> Overview(
        [FromQuery] int periodDays = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await _analytics.GetOverviewAsync(periodDays, cancellationToken));

    [HttpGet("drill-down")]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType(typeof(CustomerEngagementDrillDownResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerEngagementDrillDownResultDto>> DrillDown(
        [FromQuery] string step,
        [FromQuery] int periodDays = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _analytics.GetDrillDownAsync(step, periodDays, page, pageSize, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
