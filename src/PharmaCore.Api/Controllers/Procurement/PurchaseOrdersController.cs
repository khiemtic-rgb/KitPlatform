using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Procurement;

namespace PharmaCore.Api.Controllers.Procurement;

[ApiController]
[Authorize]
[Route("api/procurement/purchase-orders")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _orders;

    public PurchaseOrdersController(IPurchaseOrderService orders) => _orders = orders;

    [HttpGet]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] short? status,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] Guid? productId,
        [FromQuery] bool pendingReceiptOnly = false,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _orders.GetAllAsync(
            new PurchaseOrderListFilter(search, supplierId, warehouseId, status, dateFrom, dateTo, productId, pendingReceiptOnly, includeArchived),
            cancellationToken));

    [HttpGet("price-hint")]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<LastPurchasePriceHintDto>> PriceHint(
        [FromQuery] Guid supplierId,
        [FromQuery] Guid productId,
        CancellationToken cancellationToken) =>
        Ok(await _orders.GetLastPurchasePriceHintAsync(supplierId, productId, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _orders.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Create(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Update(
        Guid id,
        [FromBody] UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _orders.UpdateAsync(id, request, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var item = await _orders.ApproveAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var item = await _orders.CancelAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Close(Guid id, CancellationToken cancellationToken)
    {
        var item = await _orders.CloseAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ProcurementPolicies.Write)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var archived = await _orders.ArchiveAsync(id, cancellationToken);
        return archived ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}/purge")]
    [Authorize(Policy = SystemPolicies.DeletePermanent)]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken)
    {
        var purged = await _orders.PurgeAsync(id, cancellationToken);
        return purged ? NoContent() : NotFound();
    }
}
