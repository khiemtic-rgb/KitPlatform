using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Api.Controllers.Clinic;

[ApiController]
[Route("api/clinic/prescriptions")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.ClinicEmrLite)]
[Authorize(Policy = ClinicPolicies.Read)]
public sealed class ClinicPrescriptionsController : ControllerBase
{
    private readonly IClinicPrescriptionService _prescriptions;

    public ClinicPrescriptionsController(IClinicPrescriptionService prescriptions) =>
        _prescriptions = prescriptions;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicPrescriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ClinicPrescriptionDto>>> List(
        [FromQuery] Guid visitId,
        CancellationToken cancellationToken)
    {
        if (visitId == Guid.Empty)
            return BadRequest(new { message = "visitId bắt buộc." });
        return Ok(await _prescriptions.ListByVisitAsync(visitId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicPrescriptionDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _prescriptions.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicPrescriptionDto>> Create(
        [FromBody] CreateClinicPrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _prescriptions.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicPrescriptionDto>> Update(
        Guid id,
        [FromBody] UpdateClinicPrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _prescriptions.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/finalize")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicPrescriptionDto>> Finalize(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _prescriptions.FinalizeAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/sign")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicPrescriptionDto>> Sign(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _prescriptions.SignAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicPrescriptionDto>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _prescriptions.CancelAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/send-to-pharmacy")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicPrescriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicPrescriptionDto>> SendToPharmacy(
        Guid id,
        [FromBody] SendClinicPrescriptionToPharmacyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _prescriptions.SendToPharmacyAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _prescriptions.GetPdfAsync(id, cancellationToken);
            if (result is null) return NotFound();
            return File(result.Value.Pdf, "application/pdf", result.Value.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
