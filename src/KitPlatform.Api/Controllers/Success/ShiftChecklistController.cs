using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Success;

namespace KitPlatform.Api.Controllers.Success;

[ApiController]
[Authorize]
[Route("api/success/shift-checklist")]
public sealed class ShiftChecklistController : ControllerBase
{
    private readonly IShiftChecklistService _service;

    public ShiftChecklistController(IShiftChecklistService service) => _service = service;

    [HttpGet("today")]
    [Authorize(Policy = SuccessPolicies.Checklist)]
    [ProducesResponseType(typeof(ShiftChecklistTodayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShiftChecklistTodayDto>> GetToday(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? salesShiftId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetTodayAsync(branchId, salesShiftId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("runs")]
    [Authorize(Policy = SuccessPolicies.ChecklistMutate)]
    [ProducesResponseType(typeof(ShiftChecklistRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShiftChecklistRunDto>> Start(
        [FromBody] StartShiftChecklistRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.StartOrGetAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPut("runs/{runId:guid}/items/{itemId:guid}")]
    [Authorize(Policy = SuccessPolicies.ChecklistMutate)]
    [ProducesResponseType(typeof(ShiftChecklistRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShiftChecklistRunDto>> SetItem(
        Guid runId,
        Guid itemId,
        [FromBody] SetShiftChecklistItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.SetItemCheckedAsync(runId, itemId, request.Checked, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("runs/{runId:guid}/complete")]
    [Authorize(Policy = SuccessPolicies.ChecklistMutate)]
    [ProducesResponseType(typeof(ShiftChecklistRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShiftChecklistRunDto>> Complete(
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.CompleteAsync(runId, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
