using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/sales/pos/consultation")]
public sealed class PharmacyConsultationController : ControllerBase
{
    private readonly IPharmacyConsultationService _consultation;

    public PharmacyConsultationController(IPharmacyConsultationService consultation) =>
        _consultation = consultation;

    [HttpGet("symptom-options")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PharmacyConsultationSymptomCatalogDto>> SymptomOptions(
        CancellationToken cancellationToken) =>
        Ok(await _consultation.GetSymptomCatalogAsync(cancellationToken));

    [HttpPost("extract")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<ExtractPharmacyConsultationResultDto>> Extract(
        [FromBody] ExtractPharmacyConsultationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _consultation.ExtractAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("suggest")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<SuggestPharmacyConsultationResultDto>> Suggest(
        [FromBody] SuggestPharmacyConsultationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _consultation.SuggestAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("questions")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PharmacyConsultationQuestionDto>>> Questions(
        [FromQuery] string? symptoms,
        CancellationToken cancellationToken)
    {
        var codes = string.IsNullOrWhiteSpace(symptoms)
            ? Array.Empty<string>()
            : symptoms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(await _consultation.GetQuestionsAsync(codes, cancellationToken));
    }

    [HttpPost("sessions")]
    [Authorize(Policy = SalesPolicies.Pos)]
    public async Task<ActionResult<PharmacyConsultationSessionDto>> Confirm(
        [FromBody] ConfirmPharmacyConsultationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _consultation.ConfirmAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sessions/recent")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<PharmacyConsultationSessionSummaryDto>>> RecentSessions(
        [FromQuery] Guid customerId,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
            return BadRequest(new { message = "Thiếu customerId." });

        return Ok(await _consultation.ListRecentByCustomerAsync(customerId, limit, cancellationToken));
    }

    [HttpGet("sessions/{id:guid}")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<PharmacyConsultationSessionDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _consultation.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("sessions/{id:guid}/link-order")]
    [Authorize(Policy = SalesPolicies.Pos)]
    public async Task<ActionResult<PharmacyConsultationSessionDto>> LinkOrder(
        Guid id,
        [FromBody] LinkPharmacyConsultationOrderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _consultation.LinkOrderAsync(id, request.SalesOrderId, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
