using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Rx;

namespace KitPlatform.Api.Controllers.PrescriberPortal;

[ApiController]
[Authorize(Policy = PrescriberPortalPolicies.Authenticated)]
[Route("api/prescriber-portal/prescriptions")]
public sealed class PrescriberPortalPrescriptionsController : ControllerBase
{
    private readonly IPrescriberPortalPrescriptionService _prescriptions;
    private readonly ICurrentPrescriberAccessor _prescriber;

    public PrescriberPortalPrescriptionsController(
        IPrescriberPortalPrescriptionService prescriptions,
        ICurrentPrescriberAccessor prescriber)
    {
        _prescriptions = prescriptions;
        _prescriber = prescriber;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PortalPrescriptionSummaryDto>>> List(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        Ok(await _prescriptions.ListMyPrescriptionsAsync(_prescriber.PrescriberId, tenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PortalPrescriptionDetailDto>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _prescriptions.GetPrescriptionAsync(_prescriber.PrescriberId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PortalPrescriptionDetailDto>> Amend(
        Guid id,
        [FromBody] PortalAmendPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.AmendSignedPrescriptionAsync(
                _prescriber.PrescriberId,
                id,
                request,
                cancellationToken);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PortalPrescriptionDetailDto>> Cancel(
        Guid id,
        [FromBody] CancelPrescriptionRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.CancelPrescriptionAsync(
                _prescriber.PrescriberId,
                id,
                request?.Reason,
                cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PortalPrescriptionDetailDto>> Create(
        [FromBody] PortalCreatePrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _prescriptions.CreateSignedPrescriptionAsync(_prescriber.PrescriberId, request, cancellationToken);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/share")]
    public async Task<ActionResult<PortalPrescriptionShareDto>> Share(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _prescriptions.GetPrescriptionShareAsync(_prescriber.PrescriberId, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
