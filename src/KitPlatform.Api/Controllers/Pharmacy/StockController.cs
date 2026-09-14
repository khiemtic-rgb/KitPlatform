using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Inventory;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/inventory/stock")]
public sealed class StockController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public StockController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet("batches")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<PagedStockBatchesResult>> Batches(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? productId,
        [FromQuery] string? search,
        [FromQuery] string? expiry,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _inventory.GetStockBatchesAsync(warehouseId, productId, search, expiry, page, pageSize, cancellationToken));

    [HttpGet("products")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<PagedStockProductsResult>> Products(
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? search,
        [FromQuery] string? expiry,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _inventory.GetStockProductsAsync(warehouseId, search, expiry, page, pageSize, cancellationToken));

    [HttpGet("low-stock")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<LowStockProductDto>>> LowStock(
        [FromQuery] Guid? warehouseId,
        [FromQuery] decimal defaultThreshold = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await _inventory.GetLowStockProductsAsync(warehouseId, defaultThreshold, cancellationToken));

    [HttpPatch("batches/{id:guid}/unit-cost")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<RevalueBatchCostResult>> RevalueUnitCost(
        Guid id,
        [FromBody] RevalueBatchCostRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _inventory.RevalueBatchCostAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
