using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Success;

namespace KitPlatform.Api.Controllers.Success;

[ApiController]
[Authorize]
[Route("api/success")]
public sealed class OwnerCockpitController : ControllerBase
{
    private readonly IOwnerCockpitService _cockpit;

    public OwnerCockpitController(IOwnerCockpitService cockpit) => _cockpit = cockpit;

    /// <summary>Pharmacy owner health snapshot (Success P2 cockpit).</summary>
    [HttpGet("owner-cockpit")]
    [Authorize(Policy = SuccessPolicies.Owner)]
    [ProducesResponseType(typeof(OwnerCockpitDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnerCockpitDto>> Get(
        [FromQuery] int expiryDays = 30,
        [FromQuery] decimal lowStockThreshold = 10,
        [FromQuery] int dormantDays = 30,
        [FromQuery] int peakHoursWindowDays = 30,
        [FromQuery] int urgentExpiryDays = 7,
        CancellationToken cancellationToken = default) =>
        Ok(await _cockpit.GetAsync(
            expiryDays,
            lowStockThreshold,
            dormantDays,
            peakHoursWindowDays,
            urgentExpiryDays,
            cancellationToken));
}
