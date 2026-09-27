using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Inventory;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/inventory/stock/reviews")]
public sealed class InventoryStockReviewController : ControllerBase
{
    private readonly IInventoryStockReviewService _reviews;

    public InventoryStockReviewController(IInventoryStockReviewService reviews) => _reviews = reviews;

    [HttpGet]
    [Authorize(Policy = InventoryPolicies.ReviewRead)]
    public async Task<ActionResult<IReadOnlyList<InventoryStockReviewDto>>> List(
        CancellationToken cancellationToken) =>
        Ok(await _reviews.ListConfirmedAsync(cancellationToken));

    [HttpPost("confirm")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<InventoryStockReviewDto>> Confirm(
        [FromBody] ConfirmStockReviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reviews.ConfirmAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{productId:guid}/{warehouseId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<IActionResult> Unconfirm(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _reviews.UnconfirmAsync(productId, warehouseId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
