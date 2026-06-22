using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers.Inventory;

[ApiController]
[Authorize]
[Route("api/inventory/warehouses")]
public sealed class WarehousesController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public WarehousesController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet("branches")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<BranchLookupDto>>> Branches(CancellationToken cancellationToken) =>
        Ok(await _inventory.GetBranchLookupsAsync(cancellationToken));

    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _inventory.GetWarehousesAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<WarehouseDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventory.GetWarehouseAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<WarehouseDto>> Create(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.CreateWarehouseAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<WarehouseDto>> Update(
        Guid id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.UpdateWarehouseAsync(id, request, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (ok, error) = await _inventory.DeleteWarehouseAsync(id, cancellationToken);
        if (ok) return NoContent();
        return error?.Contains("không tồn tại") == true ? NotFound(new { message = error }) : BadRequest(new { message = error });
    }
}
