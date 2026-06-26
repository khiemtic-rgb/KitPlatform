using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Api.Controllers.CustomerApp;

[ApiController]
[Route("api/customer-app/reservations")]
[Authorize(Policy = CustomerAppPolicies.Authenticated)]
public sealed class CustomerAppReservationsController : ControllerBase
{
    private readonly ICustomerReservationService _reservations;
    private readonly ICurrentCustomerAccessor _customer;

    public CustomerAppReservationsController(
        ICustomerReservationService reservations,
        ICurrentCustomerAccessor customer)
    {
        _reservations = reservations;
        _customer = customer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CustomerReservationListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _reservations.ListForCustomerAsync(
            _customer.TenantId,
            _customer.CustomerId,
            cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _reservations.GetForCustomerAsync(
            _customer.TenantId,
            _customer.CustomerId,
            id,
            cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerReservationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reservations.CreateForCustomerAsync(
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

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _reservations.CancelForCustomerAsync(
                _customer.TenantId,
                _customer.CustomerId,
                id,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
