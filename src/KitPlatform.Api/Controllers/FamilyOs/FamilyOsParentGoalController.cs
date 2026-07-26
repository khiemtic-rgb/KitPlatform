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
public sealed class FamilyOsParentGoalController : ControllerBase
{
    private readonly IFamilyParentGoalService _goals;

    public FamilyOsParentGoalController(IFamilyParentGoalService goals) => _goals = goals;

    /// <summary>A parent's own goals (includes private ones) with today/week/streak.</summary>
    [HttpGet("members/{memberId:guid}/parent-goals")]
    [ProducesResponseType(typeof(IReadOnlyList<ParentGoalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ParentGoalDto>>> ListForMember(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _goals.ListForMemberAsync(familyId, memberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("parent-goals")]
    [ProducesResponseType(typeof(ParentGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParentGoalDto>> Create(
        Guid familyId,
        [FromBody] CreateParentGoalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _goals.CreateAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("parent-goals/{goalId:guid}")]
    [ProducesResponseType(typeof(ParentGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParentGoalDto>> Update(
        Guid familyId,
        Guid goalId,
        [FromBody] UpdateParentGoalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _goals.UpdateAsync(familyId, goalId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("parent-goals/{goalId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        Guid familyId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _goals.DeleteAsync(familyId, goalId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("parent-goals/{goalId:guid}/checkin")]
    [ProducesResponseType(typeof(ParentGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParentGoalDto>> Checkin(
        Guid familyId,
        Guid goalId,
        [FromBody] ParentGoalCheckinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _goals.CheckinAsync(familyId, goalId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Household view — only opt-in shared parent goals (privacy-safe).</summary>
    [HttpGet("parent-goals/shared")]
    [ProducesResponseType(typeof(IReadOnlyList<SharedParentProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SharedParentProgressDto>>> ListShared(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _goals.ListSharedAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
