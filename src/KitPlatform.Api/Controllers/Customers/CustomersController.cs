using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Application.Customers;

namespace KitPlatform.Api.Controllers.Customers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerConsentService _consents;
    private readonly ICustomerAdminService _admin;
    private readonly ICustomerMergeService _merge;
    private readonly ICustomerImportService _import;
    private readonly ICustomerLoyaltyService _loyalty;
    private readonly ICustomerPilotOtpAdminService _pilotOtp;
    private readonly ITenantContext _tenant;

    public CustomersController(
        ICustomerConsentService consents,
        ICustomerAdminService admin,
        ICustomerMergeService merge,
        ICustomerImportService import,
        ICustomerLoyaltyService loyalty,
        ICustomerPilotOtpAdminService pilotOtp,
        ITenantContext tenant)
    {
        _consents = consents;
        _admin = admin;
        _merge = merge;
        _import = import;
        _loyalty = loyalty;
        _pilotOtp = pilotOtp;
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

    /// <summary>Near-duplicate customers: same digit-phone or name similarity ≥ threshold.</summary>
    [HttpGet("similar-clusters")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SimilarCustomerClustersResult>> SimilarClusters(
        [FromQuery] double threshold = 0.8,
        CancellationToken cancellationToken = default) =>
        Ok(await _admin.GetSimilarClustersAsync(threshold, cancellationToken));

    /// <summary>Soft-warn helper: names similar to an existing customer (≥ threshold, default 0.8).</summary>
    [HttpGet("check-name")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SimilarCustomerNamesResult>> CheckName(
        [FromQuery] string name,
        [FromQuery] Guid? excludeId = null,
        [FromQuery] double threshold = 0.8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Ok(new SimilarCustomerNamesResult([], false));

        return Ok(await _admin.FindSimilarNamesAsync(name, excludeId, threshold, cancellationToken));
    }

    /// <summary>Merge source customer into keeper (reassign orders/loyalty/etc, soft-delete source).</summary>
    [HttpPost("merge")]
    [Authorize(Policy = SalesPolicies.Customers)]
    public async Task<ActionResult<MergeCustomersResult>> Merge(
        [FromBody] MergeCustomersRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _merge.MergeAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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

    [HttpPost("import")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<CustomerImportResultDto>> Import(
        [FromBody] IReadOnlyList<CustomerImportRowRequest> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return BadRequest(new { message = "Không có dòng dữ liệu để import." });

        if (rows.Count > 2000)
            return BadRequest(new { message = "Tối đa 2000 dòng mỗi lần import." });

        try
        {
            return Ok(await _import.ImportCustomersAsync(rows, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

    [HttpGet("{customerId:guid}/pilot-otp")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<CustomerPilotOtpStatusDto>> GetPilotOtp(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var status = await _pilotOtp.GetStatusAsync(customerId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
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
