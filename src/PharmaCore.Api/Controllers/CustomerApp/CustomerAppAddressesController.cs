using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Api.Controllers.CustomerApp;

[ApiController]
[Route("api/customer-app/addresses")]
[Authorize(Policy = CustomerAppPolicies.Authenticated)]
public sealed class CustomerAppAddressesController : ControllerBase
{
    private readonly ICustomerAddressService _addresses;
    private readonly ICurrentCustomerAccessor _customer;

    public CustomerAppAddressesController(
        ICustomerAddressService addresses,
        ICurrentCustomerAccessor customer)
    {
        _addresses = addresses;
        _customer = customer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CustomerAddressListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _addresses.ListAsync(_customer.TenantId, _customer.CustomerId, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _addresses.CreateAsync(
                _customer.TenantId,
                _customer.CustomerId,
                request,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpsertCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _addresses.UpdateAsync(
                _customer.TenantId,
                _customer.CustomerId,
                id,
                request,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _addresses.DeleteAsync(_customer.TenantId, _customer.CustomerId, id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
