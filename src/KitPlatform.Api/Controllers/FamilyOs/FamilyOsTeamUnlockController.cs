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
public sealed class FamilyOsTeamUnlockController : ControllerBase
{
    private readonly IFamilyTeamUnlockService _team;

    public FamilyOsTeamUnlockController(IFamilyTeamUnlockService team) => _team = team;

    [HttpGet("team-day")]
    [ProducesResponseType(typeof(FamilyTeamDayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyTeamDayDto>> GetTeamDay(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _team.GetTeamDayAsync(familyId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("team-unlocks")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyTeamUnlockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyTeamUnlockDto>>> List(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        [FromQuery] bool ensure = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (ensure)
            {
                await _team.EnsurePendingAsync(familyId, flowDate, cancellationToken);
                await _team.EnsureSiblingComboPendingAsync(familyId, flowDate, cancellationToken);
            }
            return Ok(await _team.ListAsync(familyId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("team-unlocks/ensure")]
    [ProducesResponseType(typeof(FamilyTeamUnlockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyTeamUnlockDto>> Ensure(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _team.EnsurePendingAsync(familyId, flowDate, cancellationToken);
            if (dto is null) return NoContent();
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("team-unlocks/{unlockId:guid}/confirm")]
    [ProducesResponseType(typeof(FamilyTeamUnlockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyTeamUnlockDto>> Confirm(
        Guid familyId,
        Guid unlockId,
        [FromBody] FamilyTeamUnlockDecideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Force confirmed path for this route when status omitted
            var body = request with
            {
                Status = string.IsNullOrWhiteSpace(request.Status)
                    ? FamilyTeamUnlockStatuses.Confirmed
                    : request.Status,
            };
            return Ok(await _team.DecideAsync(familyId, unlockId, body, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
