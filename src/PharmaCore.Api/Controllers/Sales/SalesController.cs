using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Sales;

namespace PharmaCore.Api.Controllers.Sales;

[ApiController]
[Authorize]
[Route("api/sales")]
public sealed class SalesController : ControllerBase
{
    private readonly ISalesService _sales;

    public SalesController(ISalesService sales) => _sales = sales;

    [HttpGet("customers")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<CustomerListItemDto>>> Customers(
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(await _sales.SearchCustomersAsync(search, cancellationToken));

    [HttpGet("pos/lookup")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PosProductLookupDto>> Lookup(
        [FromQuery] string barcode,
        [FromQuery] Guid warehouseId,
        [FromQuery] short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return BadRequest(new { message = "Mã vạch hoặc mã sản phẩm không được để trống." });
        var item = await _sales.LookupProductAsync(barcode, warehouseId, priceType, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("pos/stock")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PosStockCheckDto>> GetPosStock(
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid productUnitId,
        CancellationToken cancellationToken = default)
    {
        var item = await _sales.GetPosStockByUnitAsync(warehouseId, productUnitId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("pos/stock/bulk")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PosStockCheckDto>>> GetPosStockBulk(
        [FromBody] PosBulkStockRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProductUnitIds.Count == 0)
            return Ok(Array.Empty<PosStockCheckDto>());
        return Ok(await _sales.GetPosStockBulkAsync(
            request.WarehouseId,
            request.ProductUnitIds,
            cancellationToken));
    }

    [HttpGet("pos/search")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PosProductSearchItemDto>>> SearchPosProducts(
        [FromQuery] string search,
        [FromQuery] Guid warehouseId,
        [FromQuery] short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
            return Ok(Array.Empty<PosProductSearchItemDto>());
        return Ok(await _sales.SearchPosProductsAsync(search, warehouseId, priceType, cancellationToken));
    }

    [HttpPost("pos/preview-allocation")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PosAllocationPreviewDto>> PreviewAllocation(
        [FromBody] PosAllocationPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            return BadRequest(new { message = "Giỏ hàng trống." });
        try
        {
            return Ok(await _sales.PreviewPosAllocationAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("orders")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<SalesOrderListItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _sales.GetOrdersAsync(cancellationToken));

    [HttpGet("orders/{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SalesOrderDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _sales.GetOrderAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("orders")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesOrderDetailDto>> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _sales.CreateSaleAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("orders/{id:guid}")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesOrderDetailDto>> UpdateDraft(
        Guid id,
        [FromBody] UpdateDraftSaleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _sales.UpdateDraftSaleAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id:guid}/complete")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesOrderDetailDto>> CompleteDraft(
        Guid id,
        [FromBody] CompleteDraftSaleRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _sales.CompleteDraftSaleAsync(id, request?.Payments, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id:guid}/cancel")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesOrderDetailDto>> CancelDraft(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var item = await _sales.CancelDraftSaleAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orders/{id:guid}/returns")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesReturnDetailDto>> CreateReturn(
        Guid id,
        [FromBody] CreateSaleReturnRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _sales.CreateSaleReturnAsync(id, request, cancellationToken);
            return CreatedAtAction(nameof(GetReturn), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("returns")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<SalesReturnListItemDto>>> ListReturns(
        [FromQuery] int limit = 50,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _sales.GetSaleReturnsAsync(limit, search, cancellationToken));

    [HttpGet("returns/{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SalesReturnDetailDto>> GetReturn(Guid id, CancellationToken cancellationToken)
    {
        var item = await _sales.GetSaleReturnAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("orders/{id:guid}/returns")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<SalesReturnListItemDto>>> ListOrderReturns(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _sales.GetSaleReturnsByOrderAsync(id, cancellationToken));

    [HttpGet("shift-summary")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SalesShiftSummaryDto>> ShiftSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        try
        {
            var end = to ?? DateTime.UtcNow;
            var start = from ?? end.Date;
            return Ok(await _sales.GetShiftSummaryAsync(start, end, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shifts")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<SalesShiftListItemDto>>> ListShifts(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _sales.GetShiftsAsync(limit, cancellationToken));

    [HttpGet("shifts/current")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SalesShiftDetailDto>> CurrentShift(
        [FromQuery] Guid warehouseId,
        CancellationToken cancellationToken)
    {
        if (warehouseId == Guid.Empty)
            return BadRequest(new { message = "warehouseId là bắt buộc." });
        var shift = await _sales.GetOpenShiftAsync(warehouseId, cancellationToken);
        return shift is null ? NotFound() : Ok(shift);
    }

    [HttpGet("shifts/{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SalesShiftDetailDto>> GetShift(Guid id, CancellationToken cancellationToken)
    {
        var shift = await _sales.GetShiftAsync(id, cancellationToken);
        return shift is null ? NotFound() : Ok(shift);
    }

    [HttpPost("shifts/open")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesShiftDetailDto>> OpenShift(
        [FromBody] OpenSalesShiftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var shift = await _sales.OpenShiftAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetShift), new { id = shift.Id }, shift);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("shifts/{id:guid}/close")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<SalesShiftDetailDto>> CloseShift(
        Guid id,
        [FromBody] CloseSalesShiftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sales.CloseShiftAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed record CompleteDraftSaleRequest(IReadOnlyList<CreateSalePaymentRequest>? Payments = null);
