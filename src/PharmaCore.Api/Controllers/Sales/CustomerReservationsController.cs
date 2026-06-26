using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Api.Controllers.Sales;

[ApiController]
[Route("api/sales/customer-reservations")]
public sealed class CustomerReservationsController : ControllerBase
{
    private readonly ICustomerReservationService _reservations;
    private readonly ITenantContext _tenant;

    public CustomerReservationsController(
        ICustomerReservationService reservations,
        ITenantContext tenant)
    {
        _reservations = reservations;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType(typeof(CustomerReservationStaffListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] short[]? status,
        CancellationToken cancellationToken) =>
        Ok(await _reservations.ListForStaffAsync(_tenant.TenantId, status, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _reservations.GetForStaffAsync(_tenant.TenantId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken) =>
        await ActionAsync(() => _reservations.ConfirmAsync(_tenant.TenantId, id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken) =>
        await ActionAsync(() => _reservations.RejectAsync(_tenant.TenantId, id, cancellationToken));

    [HttpPost("{id:guid}/ready")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkReady(Guid id, CancellationToken cancellationToken) =>
        await ActionAsync(() => _reservations.MarkReadyAsync(_tenant.TenantId, id, cancellationToken));

    [HttpPost("{id:guid}/collected")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkCollected(Guid id, CancellationToken cancellationToken) =>
        await ActionAsync(() => _reservations.MarkCollectedAsync(_tenant.TenantId, id, cancellationToken));

    [HttpPut("{id:guid}/staff-notes")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(typeof(CustomerReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStaffNotes(
        Guid id,
        [FromBody] UpdateCustomerReservationStaffNotesRequest request,
        CancellationToken cancellationToken) =>
        await ActionAsync(() => _reservations.UpdateStaffNotesAsync(_tenant.TenantId, id, request, cancellationToken));

    [HttpGet("{id:guid}/pos-load")]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType(typeof(CustomerReservationPosLoadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PosLoad(Guid id, CancellationToken cancellationToken)
    {
        var payload = await _reservations.GetPosLoadAsync(_tenant.TenantId, id, cancellationToken);
        return payload is null ? NotFound() : Ok(payload);
    }

    [HttpPost("{id:guid}/link-sale")]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkSale(
        Guid id,
        [FromBody] LinkCustomerReservationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _reservations.LinkSalesOrderAsync(_tenant.TenantId, id, request.SalesOrderId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> ActionAsync(Func<Task<CustomerReservationDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
