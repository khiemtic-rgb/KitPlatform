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
    private readonly ICustomerAppLoginAdminService _appLogin;
    private readonly ITenantContext _tenant;

    public CustomersController(
        ICustomerConsentService consents,
        ICustomerAdminService admin,
        ICustomerMergeService merge,
        ICustomerImportService import,
        ICustomerLoyaltyService loyalty,
        ICustomerPilotOtpAdminService pilotOtp,
        ICustomerAppLoginAdminService appLogin,
        ITenantContext tenant)
    {
        _consents = consents;
        _admin = admin;
        _merge = merge;
        _import = import;
        _loyalty = loyalty;
        _pilotOtp = pilotOtp;
        _appLogin = appLogin;
        _tenant = tenant;
    }

    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PagedCustomersResult>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pharmacyRelation = null,
        [FromQuery] string? phoneReadiness = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _admin.ListAsync(
            search,
            page,
            pageSize,
            cancellationToken,
            pharmacyRelation,
            phoneReadiness));

    /// <summary>App-login / pharmacy membership readiness counts for the current tenant.</summary>
    [HttpGet("mode-a-readiness")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<CustomerPharmacyRelationSummaryDto>> ModeAReadiness(
        CancellationToken cancellationToken = default) =>
        Ok(await _admin.GetPharmacyRelationSummaryAsync(cancellationToken));

    /// <summary>
    /// Bulk-mark active customers with a valid VN mobile as pharmacy members
    /// (skips revoked and invalid phones; does not send SMS).
    /// </summary>
    [HttpPost("bulk-mark-pharmacy-member")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<BulkMarkPharmacyMemberResult>> BulkMarkPharmacyMember(
        [FromBody] MarkPharmacyMemberRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _admin.BulkMarkValidPhoneAsMemberAsync(
                request?.VerifiedVia,
                _tenant.UserId,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Near-duplicate customers: same digit-phone or name similarity ≥ threshold.</summary>
    [HttpGet("similar-clusters")]
    [Authorize(Policy = SalesPolicies.CustomerMerge)]
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
    [Authorize(Policy = SalesPolicies.CustomerMerge)]
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

    /// <summary>Live staff-read OTPs currently waiting (counter Mode A) — no need to open each customer.</summary>
    [HttpGet("active-counter-otps")]
    [Authorize(Policy = SalesPolicies.PosOrCustomers)]
    public async Task<ActionResult<ActiveCounterOtpListDto>> ListActiveCounterOtps(
        CancellationToken cancellationToken = default) =>
        Ok(await _pilotOtp.ListActiveAsync(cancellationToken));

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
    [Authorize(Policy = SalesPolicies.PosOrCustomers)]
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

    [HttpGet("app-login-requests")]
    [Authorize(Policy = SalesPolicies.Customers)]
    public async Task<ActionResult<IReadOnlyList<CustomerAppLoginRequestDto>>> ListAppLoginRequests(
        [FromQuery] string? status = "pending",
        CancellationToken cancellationToken = default) =>
        Ok(await _appLogin.ListAsync(status, cancellationToken));

    [HttpPost("app-login-requests/{requestId:guid}/approve")]
    [Authorize(Policy = SalesPolicies.Customers)]
    public async Task<ActionResult<ApproveCustomerAppLoginResult>> ApproveAppLoginRequest(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _appLogin.ApproveAsync(requestId, _tenant.UserId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("app-login-requests/{requestId:guid}/reject")]
    [Authorize(Policy = SalesPolicies.Customers)]
    public async Task<IActionResult> RejectAppLoginRequest(
        Guid requestId,
        [FromBody] RejectCustomerAppLoginRequest? body,
        CancellationToken cancellationToken)
    {
        try
        {
            await _appLogin.RejectAsync(requestId, _tenant.UserId, body?.Reason, cancellationToken);
            return Ok(new { message = "Đã từ chối yêu cầu." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("app-auth-settings")]
    [Authorize(Policy = SalesPolicies.Settings)]
    public async Task<ActionResult<CustomerAppAuthSettingsDto>> GetAppAuthSettings(
        CancellationToken cancellationToken) =>
        Ok(await _appLogin.GetSettingsAsync(cancellationToken));

    [HttpPut("app-auth-settings")]
    [Authorize(Policy = SalesPolicies.Settings)]
    public async Task<ActionResult<CustomerAppAuthSettingsDto>> UpdateAppAuthSettings(
        [FromBody] UpdateCustomerAppAuthSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _appLogin.UpdateSettingsAsync(request, _tenant.UserId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("issue-counter-otp")]
    [Authorize(Policy = SalesPolicies.Pos)]
    public async Task<ActionResult<IssueCounterPilotOtpResult>> IssueCounterOtp(
        [FromBody] IssueCounterPilotOtpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _appLogin.IssueCounterOtpAsync(request, cancellationToken));
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

    /// <summary>Confirm prospect/revoked CRM link → pharmacy member (POS first sale / staff mark).</summary>
    [HttpPost("{customerId:guid}/pharmacy-member")]
    [Authorize(Policy = SalesPolicies.PosOrCustomers)]
    public async Task<ActionResult<CustomerDetailDto>> MarkPharmacyMember(
        Guid customerId,
        [FromBody] MarkPharmacyMemberRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _admin.MarkPharmacyMemberAsync(
                customerId,
                request?.VerifiedVia,
                _tenant.UserId,
                cancellationToken);
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
