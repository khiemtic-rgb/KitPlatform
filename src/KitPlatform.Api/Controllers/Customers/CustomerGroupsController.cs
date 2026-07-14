using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Customers;

namespace KitPlatform.Api.Controllers.Customers;

[ApiController]
[Authorize]
[Route("api/customer-groups")]
public sealed class CustomerGroupsController : ControllerBase
{
    private readonly ICustomerGroupService _groups;

    public CustomerGroupsController(ICustomerGroupService groups) => _groups = groups;

    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<CustomerGroupDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _groups.ListAsync(activeOnly, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<CustomerGroupDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _groups.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerGroupDto>> Create(
        [FromBody] CreateCustomerGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _groups.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerGroupDto>> Update(
        Guid id,
        [FromBody] UpdateCustomerGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _groups.UpdateAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (ok, error) = await _groups.DeleteAsync(id, cancellationToken);
        if (ok) return NoContent();
        return error?.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase) == true
            ? NotFound(new { message = error })
            : BadRequest(new { message = error });
    }
}
