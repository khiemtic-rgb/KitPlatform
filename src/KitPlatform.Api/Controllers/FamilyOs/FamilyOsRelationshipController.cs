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
public sealed class FamilyOsRelationshipController : ControllerBase
{
    private readonly IFamilyRelationshipService _relationship;

    public FamilyOsRelationshipController(IFamilyRelationshipService relationship) =>
        _relationship = relationship;

    [HttpGet("relationship/triggers")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyRelationshipTriggerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyRelationshipTriggerDto>>> ListTriggers(
        Guid familyId,
        [FromQuery] Guid forMemberId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        if (forMemberId == Guid.Empty)
            return BadRequest(new { code = "validation_error", message = "forMemberId bắt buộc." });
        try
        {
            return Ok(await _relationship.ListTriggersAsync(
                familyId, forMemberId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("relationship/trigger-states")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyRelationshipTriggerStateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyRelationshipTriggerStateDto>>> ListTriggerStates(
        Guid familyId,
        [FromQuery] Guid viewerMemberId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        if (viewerMemberId == Guid.Empty)
            return BadRequest(new { code = "validation_error", message = "viewerMemberId bắt buộc." });
        try
        {
            return Ok(await _relationship.ListTriggerStatesAsync(
                familyId, viewerMemberId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("relationship/trigger-states")]
    [ProducesResponseType(typeof(FamilyRelationshipTriggerStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyRelationshipTriggerStateDto>> UpsertTriggerState(
        Guid familyId,
        [FromBody] FamilyRelationshipTriggerStateUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _relationship.UpsertTriggerStateAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("parent-voice")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyParentVoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyParentVoiceDto>>> ListParentVoice(
        Guid familyId,
        [FromQuery] Guid? forMemberId,
        [FromQuery] Guid? fromMemberId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _relationship.ListParentVoiceAsync(
                familyId, forMemberId, fromMemberId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("parent-voice")]
    [ProducesResponseType(typeof(FamilyParentVoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyParentVoiceDto>> SendParentVoice(
        Guid familyId,
        [FromBody] FamilyParentVoiceSendRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _relationship.SendParentVoiceAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("parent-voice/{messageId:guid}/ack")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AckParentVoice(
        Guid familyId,
        Guid messageId,
        [FromBody] FamilyParentVoiceAckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _relationship.AckParentVoiceAsync(familyId, messageId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("relationship/evening-circle")]
    [ProducesResponseType(typeof(FamilyEveningCircleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyEveningCircleDto>> GetEveningCircle(
        Guid familyId,
        [FromQuery] Guid? forMemberId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _relationship.GetEveningCircleAsync(
                familyId, forMemberId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("relationship/evening-circle")]
    [ProducesResponseType(typeof(FamilyEveningCircleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyEveningCircleDto>> AnswerEveningCircle(
        Guid familyId,
        [FromBody] FamilyEveningCircleAnswerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _relationship.AnswerEveningCircleAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("relationship/weekly-story")]
    [ProducesResponseType(typeof(FamilyWeeklyStoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyWeeklyStoryDto>> GetWeeklyStory(
        Guid familyId,
        [FromQuery] DateOnly? asOf,
        [FromQuery] Guid? forMemberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _relationship.GetWeeklyStoryAsync(
                familyId, asOf, forMemberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
