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
public sealed class FamilyOsBehaviorController : ControllerBase
{
    private readonly IFamilyBehaviorService _behavior;
    private readonly IFamilyDayFlowService _dayFlows;

    public FamilyOsBehaviorController(
        IFamilyBehaviorService behavior,
        IFamilyDayFlowService dayFlows)
    {
        _behavior = behavior;
        _dayFlows = dayFlows;
    }

    /// <summary>Wave 5 — Family Twin + Autonomy Gradient / Observe-only.</summary>
    [HttpGet("behavior/family-twin")]
    [ProducesResponseType(typeof(FamilyBehaviorTwinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyBehaviorTwinDto>> GetFamilyTwin(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.GetFamilyTwinAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("behavior/retirement-policy")]
    [ProducesResponseType(typeof(BehaviorRetirementPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BehaviorRetirementPolicyDto>> GetRetirementPolicy(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.GetRetirementPolicyAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("behavior/retirement-policy")]
    [ProducesResponseType(typeof(BehaviorRetirementPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BehaviorRetirementPolicyDto>> UpdateRetirementPolicy(
        Guid familyId,
        [FromBody] UpdateBehaviorRetirementPolicyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.UpdateRetirementPolicyAsync(
                familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Wave 4 — Behavior Twin + evening prediction lite.</summary>
    [HttpGet("behavior/twin")]
    [ProducesResponseType(typeof(BehaviorTwinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BehaviorTwinDto>> GetTwin(
        Guid familyId,
        [FromQuery] Guid? memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.GetTwinAsync(familyId, memberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Wave 3 — parent coach: observe vs nudge hints for today.</summary>
    [HttpGet("behavior/coach")]
    [ProducesResponseType(typeof(BehaviorCoachDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BehaviorCoachDto>> GetCoach(
        Guid familyId,
        [FromQuery] DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.GetTodayCoachAsync(familyId, flowDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Wave 1 — one-question reflection after commitment done.</summary>
    [HttpPost("commitments/{commitmentId:guid}/reflection")]
    [ProducesResponseType(typeof(CommitmentReflectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentReflectionDto>> SubmitReflection(
        Guid familyId,
        Guid commitmentId,
        [FromBody] SubmitCommitmentReflectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.SubmitReflectionAsync(
                familyId, commitmentId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("commitments/{commitmentId:guid}/reflection")]
    [ProducesResponseType(typeof(CommitmentReflectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitmentReflectionDto>> GetReflection(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        var row = await _behavior.GetReflectionAsync(familyId, commitmentId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    /// <summary>Wave 2 — illusion-of-learning retrieval check (learning missions).</summary>
    [HttpGet("commitments/{commitmentId:guid}/retrieval-check")]
    [ProducesResponseType(typeof(RetrievalCheckChallengeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RetrievalCheckChallengeDto>> GetRetrievalCheck(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        var row = await _behavior.GetRetrievalCheckAsync(familyId, commitmentId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("commitments/{commitmentId:guid}/retrieval-check")]
    [ProducesResponseType(typeof(RetrievalCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RetrievalCheckResultDto>> SubmitRetrievalCheck(
        Guid familyId,
        Guid commitmentId,
        [FromBody] SubmitRetrievalCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _behavior.SubmitRetrievalCheckAsync(
                familyId, commitmentId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Wave 3 — child self-start (autonomy signal → in_progress).</summary>
    [HttpPost("commitments/{commitmentId:guid}/self-start")]
    [ProducesResponseType(typeof(CommitmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentDto>> SelfStart(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _dayFlows.UpdateCommitmentProgressAsync(
                familyId,
                commitmentId,
                new UpdateCommitmentProgressRequest(
                    FamilyCommitmentStatuses.InProgress,
                    SkipReason: null),
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
