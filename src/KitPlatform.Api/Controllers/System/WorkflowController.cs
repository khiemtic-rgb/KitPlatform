using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Packs.Pharmacy.Workflow;

namespace KitPlatform.Api.Controllers.Workflow;

[ApiController]
[Route("api/system/workflow")]
[Authorize(Roles = "ADMIN")]
public sealed class WorkflowController : ControllerBase
{
    private readonly IWorkflowEngine _workflow;

    public WorkflowController(IWorkflowEngine workflow) => _workflow = workflow;

    [HttpGet("pos-discount/pending")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowTaskListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowTaskListItemDto>>> ListPendingPosDiscount(
        CancellationToken cancellationToken) =>
        Ok(await _workflow.ListPendingPosDiscountTasksAsync(cancellationToken));

    [HttpGet("purchase-order/pending")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowTaskListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowTaskListItemDto>>> ListPendingPurchaseOrders(
        CancellationToken cancellationToken) =>
        Ok(await _workflow.ListPendingPurchaseOrderTasksAsync(cancellationToken));

    [HttpPost("tasks/{taskId:guid}/decide")]
    [ProducesResponseType(typeof(WorkflowTaskDecisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowTaskDecisionDto>> DecideTask(
        Guid taskId,
        [FromBody] WorkflowTaskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _workflow.ApprovePosDiscountTaskAsync(
                taskId,
                request.Approved,
                request.Notes,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed record WorkflowTaskDecisionRequest(bool Approved, string? Notes = null);
