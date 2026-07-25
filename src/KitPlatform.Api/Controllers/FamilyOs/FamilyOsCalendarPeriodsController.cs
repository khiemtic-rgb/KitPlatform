using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/calendar-periods")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsCalendarPeriodsController : ControllerBase
{
    private readonly IFamilyCalendarPeriodService _periods;

    public FamilyOsCalendarPeriodsController(IFamilyCalendarPeriodService periods) =>
        _periods = periods;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarPeriodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CalendarPeriodDto>>> List(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _periods.ListAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("resolve")]
    [ProducesResponseType(typeof(ResolvedCalendarRoutineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResolvedCalendarRoutineDto>> Resolve(
        Guid familyId,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _periods.ResolveAsync(familyId, date, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("{periodId:guid}")]
    [ProducesResponseType(typeof(CalendarPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalendarPeriodDto>> Get(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = await _periods.GetAsync(familyId, periodId, cancellationToken);
            return period is null ? NotFound() : Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(CalendarPeriodDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarPeriodDto>> Create(
        Guid familyId,
        [FromBody] CreateCalendarPeriodRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = await _periods.CreateAsync(familyId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { familyId, periodId = period.Id }, period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("{periodId:guid}")]
    [ProducesResponseType(typeof(CalendarPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarPeriodDto>> Update(
        Guid familyId,
        Guid periodId,
        [FromBody] UpdateCalendarPeriodRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _periods.UpdateAsync(familyId, periodId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("{periodId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _periods.DeleteAsync(familyId, periodId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
