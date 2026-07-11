using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Rx;
using KitPlatform.Packs.Pharmacy.Sales;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/pharmacy/prescriptions")]
public sealed class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _prescriptions;

    public PrescriptionsController(IPrescriptionService prescriptions) => _prescriptions = prescriptions;

    [HttpGet]
    [Authorize(Policy = RxPolicies.Read)]
    public async Task<ActionResult<PrescriptionPagedListResult>> List(
        [FromQuery] string? status,
        [FromQuery] string? phoneSearch,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _prescriptions.GetPrescriptionsAsync(
            new PrescriptionListFilter(status, phoneSearch, page, pageSize),
            cancellationToken));

    [HttpGet("dashboard")]
    [Authorize(Policy = RxPolicies.Read)]
    public async Task<ActionResult<TenantRxDashboardDto>> Dashboard(CancellationToken cancellationToken = default) =>
        Ok(await _prescriptions.GetTenantDashboardAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = RxPolicies.Read)]
    public async Task<ActionResult<PrescriptionDetailDto>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _prescriptions.GetPrescriptionAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = RxPolicies.Write)]
    public async Task<ActionResult<PrescriptionDetailDto>> Create(
        [FromBody] CreatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.CreatePrescriptionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = RxPolicies.Write)]
    public async Task<ActionResult<PrescriptionDetailDto>> Update(
        Guid id,
        [FromBody] UpdatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.UpdatePrescriptionAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = RxPolicies.Write)]
    public async Task<ActionResult<PrescriptionDetailDto>> Submit(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.SubmitPrescriptionAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Policy = RxPolicies.Verify)]
    public async Task<ActionResult<PrescriptionDetailDto>> Verify(
        Guid id,
        [FromBody] VerifyPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.VerifyPrescriptionAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = RxPolicies.Write)]
    public async Task<ActionResult<PrescriptionDetailDto>> Cancel(
        Guid id,
        [FromBody] CancelPrescriptionRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.CancelPrescriptionAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/attachments")]
    [Authorize(Policy = RxPolicies.Write)]
    public async Task<ActionResult<PrescriptionAttachmentDto>> AddAttachment(
        Guid id,
        [FromBody] AddPrescriptionAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.AddAttachmentAsync(id, request, cancellationToken);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/pos-load")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<PrescriptionPosLoadDto>> GetPosLoad(
        Guid id,
        [FromQuery] Guid warehouseId,
        [FromQuery] short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (warehouseId == Guid.Empty)
                return BadRequest(new { message = "warehouseId là bắt buộc." });
            var item = await _prescriptions.GetPosLoadAsync(id, warehouseId, priceType, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
