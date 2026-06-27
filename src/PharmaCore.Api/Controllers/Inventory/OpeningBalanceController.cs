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
    private readonly IInventoryImportService _import;

    public OpeningBalanceController(IInventoryService inventory, IInventoryImportService import)
    {
        _inventory = inventory;
        _import = import;
    }

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

    [HttpPost("import")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<OpeningBalanceImportResultDto>> Import(
        [FromBody] OpeningBalanceImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Rows.Count == 0)
            return BadRequest(new { message = "Không có dòng dữ liệu để import." });

        if (request.Rows.Count > 2000)
            return BadRequest(new { message = "Tối đa 2000 dòng mỗi lần import." });

        var result = await _import.ImportOpeningBalanceAsync(
            request.WarehouseId,
            request.Notes,
            request.Rows,
            cancellationToken);
        return Ok(result);
    }
}

public sealed class OpeningBalanceImportRequest
{
    public Guid WarehouseId { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<OpeningBalanceImportRowRequest> Rows { get; init; } = [];
}
