using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/pharmacy/local-regimens")]
public sealed class PharmacyLocalRegimenController : ControllerBase
{
    private readonly IPharmacyLocalRegimenService _regimens;
    private readonly ITenantContext _tenant;
    private readonly ITenantPlatformSettings _platform;

    public PharmacyLocalRegimenController(
        IPharmacyLocalRegimenService regimens,
        ITenantContext tenant,
        ITenantPlatformSettings platform)
    {
        _regimens = regimens;
        _tenant = tenant;
        _platform = platform;
    }

    [HttpGet]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public Task<ActionResult<IReadOnlyList<LocalRegimenSummaryDto>>> List(CancellationToken cancellationToken) =>
        Run(() => _regimens.ListAsync(_tenant.TenantId, cancellationToken), cancellationToken);

    [HttpGet("in-use")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public async Task<ActionResult<LocalRegimenInUseResult>> InUse(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        return Ok(await _regimens.ListInUseAsync(_tenant.TenantId, q, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public async Task<ActionResult<LocalRegimenDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            var detail = await _regimens.GetAsync(_tenant.TenantId, id, cancellationToken);
            return detail is null ? NotFound(new { message = "Không tìm thấy mẫu đơn." }) : Ok(detail);
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<LocalRegimenDetailDto>> Create(
        [FromBody] LocalRegimenWriteRequest request,
        CancellationToken cancellationToken) =>
        Run(() => _regimens.CreateAsync(_tenant.TenantId, _tenant.UserId, IsTenantAdmin, request, cancellationToken), cancellationToken);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<LocalRegimenDetailDto>> Update(
        Guid id,
        [FromBody] LocalRegimenWriteRequest request,
        CancellationToken cancellationToken) =>
        Run(() => _regimens.UpdateAsync(_tenant.TenantId, _tenant.UserId, IsTenantAdmin, id, request, cancellationToken), cancellationToken);

    [HttpPost("{id:guid}/{verb}")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Use)]
    public Task<ActionResult<LocalRegimenDetailDto>> Transition(
        Guid id,
        string verb,
        [FromBody] LocalRegimenTransitionRequest request,
        CancellationToken cancellationToken) =>
        Run(() => _regimens.TransitionAsync(_tenant.TenantId, _tenant.UserId, IsTenantAdmin, id, verb, request, cancellationToken), cancellationToken);

    [HttpPost("grants")]
    [Authorize(Policy = PharmacyOwnedKnowledgePolicies.Manage)]
    public async Task<IActionResult> Grant(
        [FromBody] LocalRegimenGrantRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            await _regimens.GrantAsync(_tenant.TenantId, _tenant.UserId, IsTenantAdmin, request, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Map(ex);
        }
    }

    private bool IsTenantAdmin => User.IsInRole("ADMIN");

    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        var denied = await RejectOutsidePharmacy(cancellationToken);
        if (denied is not null)
            return denied;
        try
        {
            return Ok(await work());
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
        return StatusCode(403, new { message = "Kho đơn tham khảo chỉ dành cho tenant nhà thuốc." });
    }

    private ActionResult Map(InvalidOperationException ex)
    {
        if (ex.Message.Contains("chưa được bật", StringComparison.Ordinal))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        if (ex.Message.Contains("Không tìm thấy", StringComparison.Ordinal))
            return NotFound(new { message = ex.Message });
        if (ex.Message.Contains("Không có quyền", StringComparison.Ordinal))
            return StatusCode(403, new { message = ex.Message });
        if (ex.Message.Contains("người khác", StringComparison.Ordinal))
            return Conflict(new { message = ex.Message });
        return BadRequest(new { message = ex.Message });
    }
}
