using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers.Inventory;

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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _inventory.GetStockBatchesAsync(warehouseId, productId, search, page, pageSize, cancellationToken));

    [HttpGet("products")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<PagedStockProductsResult>> Products(
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _inventory.GetStockProductsAsync(warehouseId, search, page, pageSize, cancellationToken));
}
