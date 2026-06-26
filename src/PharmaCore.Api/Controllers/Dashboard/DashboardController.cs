using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Dashboard;

namespace PharmaCore.Api.Controllers.Dashboard;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("overview")]
    [Authorize(Policy = DashboardPolicies.Read)]
    [ProducesResponseType(typeof(DashboardOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(
        [FromQuery] int expiryDays = 30,
        [FromQuery] decimal lowStockThreshold = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await _dashboard.GetOverviewAsync(expiryDays, lowStockThreshold, cancellationToken));
}
