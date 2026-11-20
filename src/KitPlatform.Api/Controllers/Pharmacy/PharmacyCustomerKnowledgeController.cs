using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/pharmacy/customer-knowledge")]
public sealed class PharmacyCustomerKnowledgeController : ControllerBase
{
    private readonly IPharmacyCustomerKnowledgeService _knowledge;
    private readonly ITenantContext _tenant;
    private readonly ITenantPlatformSettings _platform;

    public PharmacyCustomerKnowledgeController(
        IPharmacyCustomerKnowledgeService knowledge,
        ITenantContext tenant,
        ITenantPlatformSettings platform)
    {
        _knowledge = knowledge;
        _tenant = tenant;
        _platform = platform;
    }

    [HttpGet]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<CustomerKnowledgeListResult>> List(
        [FromQuery] Guid customerId,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            return Ok(await _knowledge.ListAsync(_tenant.TenantId, customerId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<CustomerKnowledgeEntryDto>> Create(
        [FromBody] CustomerKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            var created = await _knowledge.CreateAsync(_tenant.TenantId, _tenant.UserId, request, cancellationToken);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            await _knowledge.RevokeAsync(_tenant.TenantId, _tenant.UserId, User.IsInRole("ADMIN"), id, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    private async Task<ActionResult?> RejectOutsidePharmacy(CancellationToken cancellationToken)
    {
        var settings = await _platform.GetAsync(cancellationToken);
        if (PharmacyKnowledgeTenantScope.AllowsPharmacyKnowledge(settings.Vertical))
            return null;
        return StatusCode(403, new { message = "Hồ sơ khách hàng chỉ dành cho tenant nhà thuốc." });
    }

    private ActionResult Map(InvalidOperationException ex)
    {
        if (ex.Message.Contains("chưa được bật", StringComparison.Ordinal))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        if (ex.Message.Contains("Không tìm thấy", StringComparison.Ordinal))
            return NotFound(new { message = ex.Message });
        if (ex.Message.Contains("Không có quyền", StringComparison.Ordinal))
            return StatusCode(403, new { message = ex.Message });
        return BadRequest(new { message = ex.Message });
    }
}
