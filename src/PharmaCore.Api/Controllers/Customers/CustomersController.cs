using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Application.Customers;

namespace PharmaCore.Api.Controllers.Customers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerConsentService _consents;
    private readonly ICustomerAdminService _admin;
    private readonly ICustomerLoyaltyService _loyalty;
    private readonly ITenantContext _tenant;

    public CustomersController(
        ICustomerConsentService consents,
        ICustomerAdminService admin,
        ICustomerLoyaltyService loyalty,
        ITenantContext tenant)
    {
        _consents = consents;
        _admin = admin;
        _loyalty = loyalty;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PagedCustomersResult>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _admin.ListAsync(search, page, pageSize, cancellationToken));

    [HttpGet("next-code")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<NextCustomerCodeDto>> NextCode(CancellationToken cancellationToken) =>
        Ok(new NextCustomerCodeDto(await _admin.GetNextCustomerCodeAsync(cancellationToken)));

    [HttpGet("{customerId:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<CustomerDetailDto>> Get(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var item = await _admin.GetAsync(customerId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerDetailDto>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _admin.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { customerId = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{customerId:guid}")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerDetailDto>> Update(
        Guid customerId,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _admin.UpdateAsync(customerId, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{customerId:guid}/orders")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PagedCustomerOrdersResult>> GetOrders(
        Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _admin.GetOrdersAsync(customerId, page, pageSize, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{customerId:guid}/loyalty/summary")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<CustomerLoyaltySummaryDto>> GetLoyaltySummary(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (!await _consents.CustomerExistsAsync(customerId, cancellationToken))
            return NotFound();

        var summary = await _loyalty.GetSummaryAsync(_tenant.TenantId, customerId, cancellationToken);
        return Ok(summary ?? new CustomerLoyaltySummaryDto([]));
    }

    [HttpGet("{customerId:guid}/loyalty/transactions")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PagedLoyaltyTransactionsResult>> GetLoyaltyTransactions(
        Guid customerId,
        [FromQuery] Guid? programId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!await _consents.CustomerExistsAsync(customerId, cancellationToken))
            return NotFound();

        return Ok(await _loyalty.GetTransactionsAsync(
            _tenant.TenantId,
            customerId,
            programId,
            page,
            pageSize,
            cancellationToken));
    }

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
