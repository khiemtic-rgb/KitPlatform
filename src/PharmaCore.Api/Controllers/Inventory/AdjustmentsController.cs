using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers.Inventory;

[ApiController]
[Authorize]
[Route("api/inventory/adjustments")]
public sealed class AdjustmentsController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public AdjustmentsController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<AdjustmentListItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _inventory.GetAdjustmentsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<AdjustmentDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventory.GetAdjustmentAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<AdjustmentDetailDto>> Create(
        [FromBody] CreateAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.CreateAdjustmentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPost("counting-sessions")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<AdjustmentDetailDto>> CreateCountingSession(
        [FromBody] CreateCountingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.CreateCountingSessionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpGet("active-counting")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<AdjustmentListItemDto>> GetActiveCounting(
        [FromQuery] Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.GetActiveCountingSessionAsync(warehouseId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/count-preview")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<AdjustmentCountPreviewResultDto>> CountPreview(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _inventory.GetCountPreviewAsync(id, cancellationToken));

    [HttpGet("{id:guid}/count-entries")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<AdjustmentCountEntryDto>>> CountEntries(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _inventory.GetCountEntriesAsync(id, cancellationToken));

    [HttpPost("{id:guid}/count-entries")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<IReadOnlyList<AdjustmentCountEntryDto>>> AddCountEntries(
        Guid id,
        [FromBody] AddCountEntriesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _inventory.AddCountEntriesAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/count-entries/{entryId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<IActionResult> DeleteCountEntry(
        Guid id,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        await _inventory.DeleteCountEntryAsync(id, entryId, cancellationToken);
        return NoContent();
    }

    [HttpGet("resolve-barcode")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<InventoryBarcodeResolveDto>> ResolveBarcode(
        [FromQuery] Guid warehouseId,
        [FromQuery] string barcode,
        CancellationToken cancellationToken)
    {
        var result = await _inventory.ResolveInventoryBarcodeAsync(warehouseId, barcode, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<AdjustmentDetailDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventory.ApproveAdjustmentAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
