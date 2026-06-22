using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Procurement;

namespace PharmaCore.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/goods-receipts")]
public sealed class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _receipts;

    public GoodsReceiptsController(IGoodsReceiptService receipts) => _receipts = receipts;

    [HttpGet]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<GoodsReceiptListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] short? status,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] Guid? purchaseOrderId,
        [FromQuery] Guid? productId,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _receipts.GetAllAsync(
            new GoodsReceiptListFilter(search, supplierId, warehouseId, status, dateFrom, dateTo, purchaseOrderId, productId, includeArchived),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<GoodsReceiptDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _receipts.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<GoodsReceiptDetailDto>> Create(
        [FromBody] CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _receipts.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<GoodsReceiptDetailDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var item = await _receipts.CompleteAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<GoodsReceiptDetailDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var item = await _receipts.CancelAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var archived = await _receipts.ArchiveAsync(id, cancellationToken);
        return archived ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}/purge")]
    [Authorize(Policy = SystemPolicies.DeletePermanent)]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken)
    {
        var purged = await _receipts.PurgeAsync(id, cancellationToken);
        return purged ? NoContent() : NotFound();
    }
}
