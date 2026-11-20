using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/pharmacy/owned-knowledge")]
public sealed class PharmacyOwnedKnowledgeController : ControllerBase
{
    private readonly IPharmacyOwnedKnowledgeService _knowledge;
    private readonly ITenantContext _tenant;
    private readonly ITenantPlatformSettings _platform;

    public PharmacyOwnedKnowledgeController(
        IPharmacyOwnedKnowledgeService knowledge,
        ITenantContext tenant,
        ITenantPlatformSettings platform)
    {
        _knowledge = knowledge;
        _tenant = tenant;
        _platform = platform;
    }

    [HttpGet]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public async Task<ActionResult<IReadOnlyList<PharmacyOwnedKnowledgeSummaryDto>>> List(
        [FromQuery] string? kind,
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? classification,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            return Ok(await _knowledge.ListAsync(
                _tenant.TenantId, kind, cancellationToken, q, status, classification));
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpGet("in-use")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<IReadOnlyList<PharmacyOwnedKnowledgeInUseDto>>> InUse(
        [FromQuery] string? kind,
        [FromQuery] string? q,
        [FromQuery] string? classification,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            return Ok(await _knowledge.ListInUseAsync(
                _tenant.TenantId, _tenant.UserId, IsTenantAdmin, kind, q, classification, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpGet("in-use/{id:guid}")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<PharmacyOwnedKnowledgeInUseDto>> InUseItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        var row = await _knowledge.GetInUseAsync(
            _tenant.TenantId, _tenant.UserId, IsTenantAdmin, id, cancellationToken);
        if (row is null)
            return NotFound();
        await _knowledge.RecordOpenAsync(_tenant.TenantId, _tenant.UserId, id, cancellationToken);
        return Ok(row);
    }

    [HttpPost("feedback")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<IActionResult> Feedback(
        [FromBody] PharmacyKnowledgeFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu nội dung phản hồi." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            await _knowledge.RecordFeedbackAsync(
                _tenant.TenantId, _tenant.UserId, IsTenantAdmin, request, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpGet("feedback/summary")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public async Task<ActionResult<PharmacyKnowledgeFeedbackSummaryDto>> FeedbackSummary(
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        return Ok(await _knowledge.GetFeedbackSummaryAsync(_tenant.TenantId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public async Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        var row = await _knowledge.GetAsync(_tenant.TenantId, id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Create(
        [FromBody] PharmacyOwnedKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu nội dung tri thức." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            var created = await _knowledge.CreateAsync(
                request, _tenant.TenantId, _tenant.UserId, cancellationToken, IsTenantAdmin);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Update(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu nội dung tri thức." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            var updated = await _knowledge.UpdateAsync(
                id, request, _tenant.TenantId, _tenant.UserId, cancellationToken, IsTenantAdmin);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Submit(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, at, _) => _knowledge.SubmitAsync(
            id, versionId, at, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Return(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, at, note) => _knowledge.ReturnAsync(
            id, versionId, at, note, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Approve(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, at, _) => _knowledge.ApproveAsync(
            id, versionId, at, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken, request?.ReviewDueOn), cancellationToken);

    [HttpPost("{id:guid}/next-draft")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> NextDraft(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, _, _) => _knowledge.StartNextDraftAsync(
            id, versionId, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Activate(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, _, _) => _knowledge.ActivateAsync(
            id, versionId, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken, request?.ReviewDueOn), cancellationToken);

    [HttpPost("{id:guid}/stop")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Stop(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, _, note) => _knowledge.StopAsync(
            id, versionId, note, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Resume(
        Guid id,
        [FromBody] PharmacyOwnedKnowledgeTransitionRequest request,
        CancellationToken cancellationToken) =>
        Transition(id, request, (versionId, _, note) => _knowledge.ResumeAsync(
            id, versionId, note, _tenant.TenantId, _tenant.UserId, IsTenantAdmin, cancellationToken), cancellationToken);

    [HttpPost("grants")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public async Task<IActionResult> Grant(
        [FromBody] PharmacyOwnedKnowledgeGrantRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "Thiếu người được cấp." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            await _knowledge.GrantAsync(
                _tenant.TenantId,
                _tenant.UserId,
                IsTenantAdmin,
                request.UserId,
                request.Action,
                request.Kind,
                cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    private bool IsTenantAdmin => User.IsInRole("ADMIN");

    private async Task<ActionResult<PharmacyOwnedKnowledgeDetailDto>> Transition(
        Guid id,
        PharmacyOwnedKnowledgeTransitionRequest? request,
        Func<Guid, DateTime, string?, Task<PharmacyOwnedKnowledgeDetailDto>> action,
        CancellationToken cancellationToken)
    {
        if (request is null || request.VersionId == Guid.Empty)
            return BadRequest(new { message = "Thiếu phiên bản." });
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            return Ok(await action(request.VersionId, request.ExpectedUpdatedAt, request.Note));
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
        return StatusCode(403, new { message = "Tri thức nhà thuốc chỉ dành cho tenant nhà thuốc." });
    }

    private ActionResult Map(InvalidOperationException ex)
    {
        if (ex.Message.Contains("Không tìm thấy", StringComparison.Ordinal))
            return NotFound(new { message = ex.Message });
        if (ex.Message.Contains("Không có quyền", StringComparison.Ordinal)
            || ex.Message.Contains("chỉ dành cho tenant", StringComparison.Ordinal))
            return StatusCode(403, new { message = ex.Message });
        if (ex.Message.Contains("người khác", StringComparison.Ordinal))
            return Conflict(new { message = ex.Message });
        return BadRequest(new { message = ex.Message });
    }
}
