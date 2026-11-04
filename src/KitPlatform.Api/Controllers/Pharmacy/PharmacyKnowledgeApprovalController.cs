using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/pharmacy/knowledge-rules")]
public sealed class PharmacyKnowledgeApprovalController : ControllerBase
{
    private readonly IPharmacyKnowledgeApprovalService _approval;
    private readonly IPharmacyKnowledgePublicationService _publication;
    private readonly ITenantContext _tenant;
    private readonly ITenantPlatformSettings _platform;

    public PharmacyKnowledgeApprovalController(
        IPharmacyKnowledgeApprovalService approval,
        IPharmacyKnowledgePublicationService publication,
        ITenantContext tenant,
        ITenantPlatformSettings platform)
    {
        _approval = approval;
        _publication = publication;
        _tenant = tenant;
        _platform = platform;
    }

    private async Task<ActionResult?> RejectOutsidePharmacy(CancellationToken cancellationToken)
    {
        var settings = await _platform.GetAsync(cancellationToken);
        if (PharmacyKnowledgeTenantScope.AllowsPharmacyKnowledge(settings.Vertical))
            return null;
        return StatusCode(403, new { message = "Tiếp nhận tri thức chỉ dành cho tenant nhà thuốc." });
    }

    [HttpGet]
    [Authorize(Policy = PharmacyKnowledgePolicies.Approve)]
    public async Task<ActionResult<PharmacyTenantKnowledgeCatalogDto>> Catalog(
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        return Ok(await _publication.ListForTenantAsync(_tenant.TenantId, cancellationToken));
    }

    [HttpGet("{ruleCode}/versions/{version}")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Approve)]
    public async Task<ActionResult<PharmacyKnowledgeVersionDto>> Version(
        string ruleCode,
        string version,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        var row = await _publication.GetVisibleToTenantAsync(
            _tenant.TenantId,
            ruleCode,
            version,
            cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("{ruleCode}/approval")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Approve)]
    public async Task<ActionResult<IReadOnlyList<PharmacyKnowledgeApprovalEventDto>>> History(
        string ruleCode,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        return Ok(await _approval.ListHistoryAsync(ruleCode, _tenant.TenantId, cancellationToken));
    }

    [HttpPost("{ruleCode}/approval")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Approve)]
    public async Task<ActionResult<PharmacyKnowledgeRuleApprovalDto>> Transition(
        string ruleCode,
        [FromBody] PharmacyKnowledgeApprovalRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu nội dung duyệt." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;

        try
        {
            var updated = await _approval.TransitionAsync(
                ruleCode,
                request,
                _tenant.TenantId,
                _tenant.UserId,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{ruleCode}/policy")]
    [Authorize(Policy = PharmacyKnowledgePolicies.Approve)]
    public async Task<ActionResult<PharmacyKnowledgePolicyDto>> SavePolicy(
        string ruleCode,
        [FromBody] PharmacyKnowledgePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu chính sách." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;

        try
        {
            return Ok(await _approval.SavePolicyAsync(
                ruleCode,
                request,
                _tenant.TenantId,
                _tenant.UserId,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
