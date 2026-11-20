using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Application.Learning;
using KitPlatform.Infrastructure.Learning;

namespace KitPlatform.Api.Controllers.Learning;

[ApiController]
[Authorize]
[Route("api/learning/operational-tasks")]
[RequirePlatformModule(PlatformModuleCodes.Learning)]
public sealed class LearningOperationalTasksController : ControllerBase
{
    private readonly OperationalTaskService _tasks;

    public LearningOperationalTasksController(OperationalTaskService tasks) => _tasks = tasks;

    [HttpGet]
    [Authorize(Policy = LearningPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<OperationalTaskDto>>> List(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? salesShiftId,
        CancellationToken cancellationToken,
        [FromQuery] bool overdue = false)
    {
        try
        {
            return Ok(await _tasks.ListAsync(CanManage(), branchId, salesShiftId, cancellationToken, overdue));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("open-shifts")]
    [Authorize(Policy = LearningPolicies.Write)]
    public async Task<ActionResult<IReadOnlyList<OperationalShiftOptionDto>>> OpenShifts(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _tasks.ListOpenShiftsAsync(cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = LearningPolicies.Write)]
    public async Task<ActionResult<OperationalTaskDto>> Create(
        [FromBody] CreateOperationalTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu nội dung việc." });

        try
        {
            var created = await _tasks.CreateAsync(request, cancellationToken);
            return Ok(created);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = LearningPolicies.Read)]
    public async Task<ActionResult<OperationalTaskDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateOperationalTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu trạng thái việc." });

        try
        {
            return Ok(await _tasks.UpdateStatusAsync(id, CanManage(), request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool CanManage() =>
        User.IsInRole("ADMIN")
        || User.Claims.Any(c => c.Type == "permission" && c.Value == "learning.write");
}
