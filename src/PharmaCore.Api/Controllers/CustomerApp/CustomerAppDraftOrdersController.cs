using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Api.Sse;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Api.Controllers.CustomerApp;

[ApiController]
[Route("api/customer-app/draft-orders")]
[Authorize(Policy = CustomerAppPolicies.Authenticated)]
public sealed class CustomerAppDraftOrdersController : ControllerBase
{
    private readonly ICustomerDraftOrderService _draftOrders;
    private readonly ICurrentCustomerAccessor _customer;
    private readonly IDraftOrderEventHub _events;

    public CustomerAppDraftOrdersController(
        ICustomerDraftOrderService draftOrders,
        ICurrentCustomerAccessor customer,
        IDraftOrderEventHub events)
    {
        _draftOrders = draftOrders;
        _customer = customer;
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CustomerDraftOrderListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _draftOrders.ListForCustomerAsync(
            _customer.TenantId,
            _customer.CustomerId,
            cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDraftOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await _draftOrders.GetForCustomerAsync(
            _customer.TenantId,
            _customer.CustomerId,
            id,
            cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(CustomerDraftOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _draftOrders.ConfirmAsync(
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

    [HttpPost("{id:guid}/hide")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Hide(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _draftOrders.HideForCustomerAsync(
                _customer.TenantId,
                _customer.CustomerId,
                id,
                cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CustomerDraftOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _draftOrders.CancelForCustomerAsync(
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

    [HttpGet("events")]
    [Produces("text/event-stream")]
    public async Task Events(CancellationToken cancellationToken)
    {
        SseWriter.PrepareResponse(Response);

        using var keepAlive = new PeriodicTimer(TimeSpan.FromSeconds(25));
        await using var events = _events
            .WatchCustomerAsync(_customer.TenantId, _customer.CustomerId, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var nextEvent = events.MoveNextAsync().AsTask();
            var completed = await Task.WhenAny(nextEvent, keepAlive.WaitForNextTickAsync(cancellationToken).AsTask());

            if (completed == nextEvent)
            {
                if (!await nextEvent)
                    break;

                await SseWriter.WriteEventAsync(Response, events.Current, cancellationToken);
                continue;
            }

            await SseWriter.WriteKeepAliveAsync(Response, cancellationToken);
        }
    }
}
