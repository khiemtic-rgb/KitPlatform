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
public sealed class FamilyOsTeamNudgeController : ControllerBase
{
    private readonly IFamilyTeamNudgeService _nudges;

    public FamilyOsTeamNudgeController(IFamilyTeamNudgeService nudges) => _nudges = nudges;

    [HttpGet("team-nudges")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyTeamNudgeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyTeamNudgeDto>>> List(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        [FromQuery] Guid? forMemberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _nudges.ListAsync(familyId, flowDate, forMemberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("team-nudges/from-candidates")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyTeamNudgeCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyTeamNudgeCandidateDto>>> FromCandidates(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _nudges.ListFromCandidatesAsync(familyId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("team-nudges")]
    [ProducesResponseType(typeof(FamilyTeamNudgeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyTeamNudgeDto>> Create(
        Guid familyId,
        [FromBody] FamilyTeamNudgeCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _nudges.CreateAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("team-nudges/{nudgeId:guid}/send")]
    [ProducesResponseType(typeof(FamilyTeamNudgeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyTeamNudgeDto>> Send(
        Guid familyId,
        Guid nudgeId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _nudges.SendAsync(familyId, nudgeId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("team-nudges/{nudgeId:guid}/ack")]
    [ProducesResponseType(typeof(FamilyTeamNudgeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyTeamNudgeDto>> Ack(
        Guid familyId,
        Guid nudgeId,
        [FromBody] FamilyTeamNudgeAckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _nudges.AckAsync(familyId, nudgeId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
