using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Success;

namespace KitPlatform.Api.Controllers.Success;

[ApiController]
[Authorize]
[Route("api/success/loss")]
public sealed class LossPreventionController : ControllerBase
{
    private readonly ILossPreventionService _loss;

    public LossPreventionController(ILossPreventionService loss) => _loss = loss;

    /// <summary>
    /// AC2 — today's cash variance from existing sales_shifts (opening/closing/expected/cash_variance).
    /// Does not invent counted_* columns; closing_cash is the counted amount.
    /// </summary>
    [HttpGet("cash-variance")]
    [Authorize(Policy = DashboardPolicies.Read)]
    [ProducesResponseType(typeof(LossCashVarianceTodayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LossCashVarianceTodayDto>> GetCashVarianceToday(
        [FromQuery] decimal? threshold = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _loss.GetCashVarianceTodayAsync(threshold, cancellationToken));
}
