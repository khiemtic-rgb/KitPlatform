using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Api.Controllers.Clinic;

[ApiController]
[Route("api/clinic/visits")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.ClinicEmrLite)]
[Authorize(Policy = ClinicPolicies.Read)]
public sealed class ClinicVisitsController : ControllerBase
{
    private readonly IClinicVisitService _visits;

    public ClinicVisitsController(IClinicVisitService visits) => _visits = visits;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicVisitDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClinicVisitDto>>> List(
        [FromQuery] Guid? customerId,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        Ok(await _visits.ListAsync(customerId, status, from, to, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClinicVisitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicVisitDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var visit = await _visits.GetAsync(id, cancellationToken);
        return visit is null ? NotFound() : Ok(visit);
    }

    [HttpPost]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicVisitDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicVisitDto>> Create(
        [FromBody] CreateClinicVisitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _visits.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicVisitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicVisitDto>> Update(
        Guid id,
        [FromBody] UpdateClinicVisitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _visits.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/notes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicVisitNoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClinicVisitNoteDto>>> ListNotes(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _visits.ListNotesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicVisitNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicVisitNoteDto>> AddNote(
        Guid id,
        [FromBody] CreateClinicVisitNoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var note = await _visits.AddNoteAsync(id, request, cancellationToken);
            return CreatedAtAction(nameof(ListNotes), new { id }, note);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
