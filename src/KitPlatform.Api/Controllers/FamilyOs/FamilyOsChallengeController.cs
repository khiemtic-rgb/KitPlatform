using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/challenges")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsChallengeController : ControllerBase
{
    private readonly IFamilyChallengeService _challenges;

    public FamilyOsChallengeController(IFamilyChallengeService challenges) =>
        _challenges = challenges;

    [HttpGet("current")]
    [ProducesResponseType(typeof(FamilyChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyChallengeDto>> GetCurrent(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _challenges.GetCurrentAsync(familyId, cancellationToken);
            return row is null ? NoContent() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("accept")]
    [ProducesResponseType(typeof(FamilyChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyChallengeDto>> Accept(
        Guid familyId,
        [FromBody] AcceptFamilyChallengeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _challenges.AcceptAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("legs/{legId:guid}/checkin")]
    [ProducesResponseType(typeof(FamilyChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyChallengeDto>> Checkin(
        Guid familyId,
        Guid legId,
        [FromBody] FamilyChallengeCheckinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _challenges.CheckinLegAsync(familyId, legId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
