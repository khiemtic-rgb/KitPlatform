using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/catalog/units")]
public sealed class MeasureUnitsController : ControllerBase
{
    private readonly IMeasureUnitService _units;

    public MeasureUnitsController(IMeasureUnitService units) => _units = units;

    [HttpGet]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<MeasureUnitDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _units.GetAllAsync(cancellationToken);
        if (activeOnly)
            items = items.Where(u => u.Status == 1).ToList();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<MeasureUnitDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _units.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<MeasureUnitDto>> Create(
        [FromBody] CreateMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _units.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<MeasureUnitDto>> Update(
        Guid id,
        [FromBody] UpdateMeasureUnitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _units.UpdateAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (ok, error) = await _units.DeleteAsync(id, cancellationToken);
        if (ok) return NoContent();
        return error?.Contains("không tồn tại") == true ? NotFound(new { message = error }) : BadRequest(new { message = error });
    }
}
