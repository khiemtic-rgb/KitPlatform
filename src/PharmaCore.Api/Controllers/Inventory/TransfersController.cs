using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers.Inventory;

[ApiController]
[Authorize]
[Route("api/inventory/transfers")]
public sealed class TransfersController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public TransfersController(IInventoryService inventory) => _inventory = inventory;

    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<TransferListItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _inventory.GetTransfersAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<TransferDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventory.GetTransferAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<TransferDetailDto>> Create(
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventory.CreateTransferAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<TransferDetailDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventory.CompleteTransferAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
