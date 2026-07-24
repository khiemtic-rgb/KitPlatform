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
public sealed class FamilyOsMoodController : ControllerBase
{
    private readonly IFamilyMoodService _moods;

    public FamilyOsMoodController(IFamilyMoodService moods) => _moods = moods;

    [HttpGet("moods")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyMemberMoodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyMemberMoodDto>>> ListFamilyMoods(
        Guid familyId,
        [FromQuery] DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _moods.ListFamilyMoodsAsync(familyId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("members/{memberId:guid}/mood")]
    [ProducesResponseType(typeof(FamilyMemberMoodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMemberMoodDto>> GetMemberMood(
        Guid familyId,
        Guid memberId,
        [FromQuery] DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _moods.GetMemberMoodAsync(familyId, memberId, flowDate, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("members/{memberId:guid}/mood")]
    [HttpPost("members/{memberId:guid}/mood")]
    [ProducesResponseType(typeof(FamilyMemberMoodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMemberMoodDto>> UpsertMemberMood(
        Guid familyId,
        Guid memberId,
        [FromBody] FamilyMemberMoodUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _moods.UpsertMemberMoodAsync(familyId, memberId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
