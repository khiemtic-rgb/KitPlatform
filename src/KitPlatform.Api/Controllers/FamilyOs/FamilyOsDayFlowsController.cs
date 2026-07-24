using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/day-flows")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsDayFlowsController : ControllerBase
{
    private readonly IFamilyDayFlowService _dayFlows;

    public FamilyOsDayFlowsController(IFamilyDayFlowService dayFlows) => _dayFlows = dayFlows;

    /// <summary>Materialize (or return) today's Daily Flow from the matching routine.</summary>
    [HttpPost("ensure")]
    [ProducesResponseType(typeof(DayFlowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DayFlowDto>> Ensure(
        Guid familyId,
        [FromBody] EnsureDayFlowRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var flow = await _dayFlows.EnsureDayFlowAsync(
                familyId,
                request ?? new EnsureDayFlowRequest(null, null),
                cancellationToken);
            return Ok(flow);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("{flowDate}")]
    [ProducesResponseType(typeof(DayFlowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DayFlowDto>> Get(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var flow = await _dayFlows.GetDayFlowAsync(familyId, flowDate, cancellationToken);
            return flow is null ? NotFound() : Ok(flow);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("~/api/family-os/families/{familyId:guid}/commitments/{commitmentId:guid}")]
    [ProducesResponseType(typeof(CommitmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentDto>> UpdateProgress(
        Guid familyId,
        Guid commitmentId,
        [FromBody] UpdateCommitmentProgressRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var commitment = await _dayFlows.UpdateCommitmentProgressAsync(
                familyId, commitmentId, request, cancellationToken);
            return Ok(commitment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Parent confirms stars for a done commitment (posts pending delta to ledger).</summary>
    [HttpPost("~/api/family-os/families/{familyId:guid}/commitments/{commitmentId:guid}/approve-stars")]
    [ProducesResponseType(typeof(CommitmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentDto>> ApproveStars(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var commitment = await _dayFlows.ApproveCommitmentStarsAsync(
                familyId, commitmentId, cancellationToken);
            return Ok(commitment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
