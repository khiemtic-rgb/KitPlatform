using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Success;

namespace KitPlatform.Api.Controllers.Success;

[ApiController]
[Authorize]
[Route("api/success")]
public sealed class OwnerStaffTodayController : ControllerBase
{
    private readonly IOwnerStaffTodayService _staff;

    public OwnerStaffTodayController(IOwnerStaffTodayService staff) => _staff = staff;

    /// <summary>Owner "Nhân sự hôm nay": checklists, open tasks, 7-day cash variance by closer, learning gap. No revenue.</summary>
    [HttpGet("owner-cockpit/staff-today")]
    [Authorize(Policy = SuccessPolicies.Owner)]
    [ProducesResponseType(typeof(OwnerStaffTodayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnerStaffTodayDto>> Get(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _staff.GetAsync(cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
