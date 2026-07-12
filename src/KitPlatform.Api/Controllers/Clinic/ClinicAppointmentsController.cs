using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Api.Controllers.Clinic;

[ApiController]
[Route("api/clinic/appointments")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.ClinicAppointments)]
public sealed class ClinicAppointmentsController : ControllerBase
{
    private readonly IClinicAppointmentService _appointments;

    public ClinicAppointmentsController(IClinicAppointmentService appointments) =>
        _appointments = appointments;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicAppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ClinicAppointmentDto>>> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _appointments.ListAsync(from, to, status, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClinicAppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicAppointmentDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _appointments.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClinicAppointmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicAppointmentDto>> Create(
        [FromBody] CreateClinicAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _appointments.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(ClinicAppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicAppointmentDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateClinicAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _appointments.UpdateStatusAsync(id, request.AppointmentStatus, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/check-in")]
    [ProducesResponseType(typeof(ClinicVisitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicVisitDto>> CheckIn(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _appointments.CheckInAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
