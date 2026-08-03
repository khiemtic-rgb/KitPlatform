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

    /// <summary>Add a one-off mission for a day (child proposal approve / parent admin).</summary>
    [HttpPost("~/api/family-os/families/{familyId:guid}/commitments/ad-hoc")]
    [ProducesResponseType(typeof(CommitmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentDto>> AddAdHoc(
        Guid familyId,
        [FromBody] AddAdHocCommitmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _dayFlows.AddAdHocCommitmentAsync(familyId, request, cancellationToken));
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
            return BadRequest(MapDayFlowError(ex));
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
            return BadRequest(MapDayFlowError(ex));
        }
    }

    /// <summary>Parent confirms evidence for study_focus (marks satisfied + posts pending stars).</summary>
    [HttpPost("~/api/family-os/families/{familyId:guid}/commitments/{commitmentId:guid}/verify-evidence")]
    [ProducesResponseType(typeof(CommitmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentDto>> VerifyEvidence(
        Guid familyId,
        Guid commitmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var commitment = await _dayFlows.VerifyCommitmentEvidenceAsync(
                familyId, commitmentId, cancellationToken);
            return Ok(commitment);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(MapDayFlowError(ex));
        }
    }

    private static object MapDayFlowError(InvalidOperationException ex) =>
        string.Equals(ex.Message, FamilyEvidenceGate.EvidenceRequiredMessageVi, StringComparison.Ordinal)
            ? new { code = FamilyEvidenceGate.EvidenceRequiredCode, message = ex.Message }
            : new { code = "validation_error", message = ex.Message };
}
