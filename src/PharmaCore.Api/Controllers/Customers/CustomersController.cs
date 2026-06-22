using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Customers;

namespace PharmaCore.Api.Controllers.Customers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerConsentService _consents;

    public CustomersController(ICustomerConsentService consents) => _consents = consents;

    [HttpGet("{customerId:guid}/consents")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<CustomerConsentDto>>> GetConsents(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (!await _consents.CustomerExistsAsync(customerId, cancellationToken))
            return NotFound();
        return Ok(await _consents.GetConsentsAsync(customerId, cancellationToken));
    }

    [HttpPut("{customerId:guid}/consents")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<IReadOnlyList<CustomerConsentDto>>> UpsertConsents(
        Guid customerId,
        [FromBody] UpsertCustomerConsentsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request?.Items is null || request.Items.Count == 0)
                return BadRequest(new { message = "Thêm ít nhất một dòng đồng ý." });

            var items = await _consents.UpsertConsentsAsync(customerId, request, cancellationToken);
            return Ok(items);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
