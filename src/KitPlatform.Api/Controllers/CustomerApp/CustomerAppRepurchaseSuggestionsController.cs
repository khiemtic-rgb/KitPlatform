using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Api.Controllers.CustomerApp;

[ApiController]
[Route("api/customer-app/repurchase-suggestions")]
[Authorize(Policy = CustomerAppPolicies.Authenticated)]
[RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
[RequirePlatformModule(PlatformModuleCodes.Medication)]
public sealed class CustomerAppRepurchaseSuggestionsController : ControllerBase
{
    private readonly ICustomerRepurchaseService _repurchase;
    private readonly ICurrentCustomerAccessor _customer;

    public CustomerAppRepurchaseSuggestionsController(
        ICustomerRepurchaseService repurchase,
        ICurrentCustomerAccessor customer)
    {
        _repurchase = repurchase;
        _customer = customer;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CustomerRepurchaseSuggestionListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await _repurchase.ListAsync(
            _customer.TenantId,
            _customer.CustomerId,
            _customer.CustomerAccountId,
            cancellationToken));

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(CustomerRepurchaseSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept(
        Guid id,
        [FromBody] AcceptRepurchaseSuggestionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _repurchase.AcceptAsync(
                _customer.TenantId,
                _customer.CustomerId,
                _customer.CustomerAccountId,
                id,
                request,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reorder")]
    [ProducesResponseType(typeof(ReorderRepurchaseSuggestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _repurchase.ReorderAsync(
                _customer.TenantId,
                _customer.CustomerId,
                _customer.CustomerAccountId,
                id,
                cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType(typeof(CustomerRepurchaseSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        var updated = await _repurchase.DismissAsync(
            _customer.TenantId,
            _customer.CustomerId,
            _customer.CustomerAccountId,
            id,
            cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:guid}/snooze")]
    [ProducesResponseType(typeof(CustomerRepurchaseSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Snooze(
        Guid id,
        [FromBody] SnoozeRepurchaseSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _repurchase.SnoozeAsync(
                _customer.TenantId,
                _customer.CustomerId,
                _customer.CustomerAccountId,
                id,
                request.SnoozedUntil,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
