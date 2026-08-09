using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/sales/customer-payments")]
public sealed class CustomerPaymentsController : ControllerBase
{
    private readonly ICustomerPaymentService _payments;

    public CustomerPaymentsController(ICustomerPaymentService payments) => _payments = payments;

    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<CustomerPaymentListItemDto>>> List(
        [FromQuery] string? search,
        [FromQuery] string? customerSearch,
        [FromQuery] string? documentSearch,
        [FromQuery] Guid? customerId,
        [FromQuery] short? status,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] short? paymentMethod,
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken = default) =>
        Ok(await _payments.GetAllAsync(
            new CustomerPaymentListFilter(
                search, customerSearch, documentSearch, customerId, status, dateFrom, dateTo, paymentMethod, warehouseId),
            cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<CustomerPaymentListItemDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _payments.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerPaymentListItemDto>> Create(
        [FromBody] CreateCustomerPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _payments.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerPaymentListItemDto>> Update(
        Guid id,
        [FromBody] UpdateCustomerPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _payments.UpdateAsync(id, request, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerPaymentListItemDto>> Post(Guid id, CancellationToken cancellationToken)
    {
        var item = await _payments.PostAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerPaymentListItemDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var item = await _payments.CancelAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
