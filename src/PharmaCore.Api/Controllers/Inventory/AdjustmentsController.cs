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

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<AdjustmentDetailDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventory.ApproveAdjustmentAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
