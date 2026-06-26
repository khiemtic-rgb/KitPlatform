using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Api.Controllers.CustomerApp;

[ApiController]
[Route("api/customer-app/purchases")]
[Authorize(Policy = CustomerAppPolicies.Authenticated)]
public sealed class CustomerAppPurchasesController : ControllerBase
{
    private readonly ICustomerPurchaseService _purchases;
    private readonly ICurrentCustomerAccessor _customer;

    public CustomerAppPurchasesController(
        ICustomerPurchaseService purchases,
        ICurrentCustomerAccessor customer)
    {
        _purchases = purchases;
        _customer = customer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CustomerPurchaseListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _purchases.ListForCustomerAsync(
            _customer.TenantId,
            _customer.CustomerId,
            cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerPurchaseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await _purchases.GetForCustomerAsync(
            _customer.TenantId,
            _customer.CustomerId,
            id,
            cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}
