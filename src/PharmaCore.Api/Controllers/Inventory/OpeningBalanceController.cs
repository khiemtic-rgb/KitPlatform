using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers.Inventory;

[ApiController]
[Authorize]
[Route("api/inventory/opening-balance")]
public sealed class OpeningBalanceController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public OpeningBalanceController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet("batches")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<OpeningBalanceBatchListItemDto>>> ListBatches(
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken) =>
        Ok(await _inventory.GetOpeningBalanceBatchesAsync(warehouseId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<OpeningBalanceResultDto>> Create(
        [FromBody] CreateOpeningBalanceRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _inventory.CreateOpeningBalanceAsync(request, cancellationToken));

    [HttpDelete("batches/{batchId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<IActionResult> VoidBatch(Guid batchId, CancellationToken cancellationToken)
    {
        await _inventory.VoidOpeningBalanceBatchAsync(batchId, cancellationToken);
        return NoContent();
    }
}
