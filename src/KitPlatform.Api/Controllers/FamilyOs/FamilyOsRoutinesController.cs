using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/routines")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsRoutinesController : ControllerBase
{
    private readonly IFamilyRoutineService _routines;

    public FamilyOsRoutinesController(IFamilyRoutineService routines) => _routines = routines;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoutineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoutineDto>>> List(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _routines.ListRoutinesAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("{routineId:guid}")]
    [ProducesResponseType(typeof(RoutineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoutineDto>> Get(
        Guid familyId,
        Guid routineId,
        CancellationToken cancellationToken)
    {
        try
        {
            var routine = await _routines.GetRoutineAsync(familyId, routineId, cancellationToken);
            return routine is null ? NotFound() : Ok(routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoutineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoutineDto>> Create(
        Guid familyId,
        [FromBody] CreateRoutineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var routine = await _routines.CreateRoutineAsync(familyId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { familyId, routineId = routine.Id }, routine);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("{routineId:guid}")]
    [ProducesResponseType(typeof(RoutineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoutineDto>> Update(
        Guid familyId,
        Guid routineId,
        [FromBody] UpdateRoutineRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _routines.UpdateRoutineAsync(familyId, routineId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("{routineId:guid}/templates")]
    [ProducesResponseType(typeof(CommitmentTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentTemplateDto>> AddTemplate(
        Guid familyId,
        Guid routineId,
        [FromBody] AddCommitmentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await _routines.AddTemplateAsync(
                familyId, routineId, request, cancellationToken);
            return Created(
                $"api/family-os/families/{familyId}/routines/{routineId}/templates/{template.Id}",
                template);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("{routineId:guid}/templates/{templateId:guid}")]
    [ProducesResponseType(typeof(CommitmentTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommitmentTemplateDto>> UpdateTemplate(
        Guid familyId,
        Guid routineId,
        Guid templateId,
        [FromBody] UpdateCommitmentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _routines.UpdateTemplateAsync(
                familyId, routineId, templateId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("{routineId:guid}/templates/{templateId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveTemplate(
        Guid familyId,
        Guid routineId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _routines.RemoveTemplateAsync(familyId, routineId, templateId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
