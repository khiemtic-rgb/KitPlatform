using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Procurement;

namespace PharmaCore.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/vat-treatments")]
public sealed class VatTreatmentsController : ControllerBase
{
    private readonly IProcurementVatTreatmentService _treatments;

    public VatTreatmentsController(IProcurementVatTreatmentService treatments) => _treatments = treatments;

    [HttpGet]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<ProcurementVatTreatmentDto>>> List(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default) =>
        Ok(await _treatments.GetAllAsync(activeOnly, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<ProcurementVatTreatmentDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _treatments.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<ProcurementVatTreatmentDto>> Create(
        [FromBody] CreateProcurementVatTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _treatments.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<ProcurementVatTreatmentDto>> Update(
        Guid id,
        [FromBody] UpdateProcurementVatTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _treatments.UpdateAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _treatments.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
