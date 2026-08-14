using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Route("api/pharmacy/integration/csdl-duoc")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.Inventory)]
public sealed class CsdlDuocIntegrationController : ControllerBase
{
    private readonly ICsdlDuocStockOutSyncService _sync;
    private readonly ITenantCsdlDuocLinkService _link;
    private readonly ITenantContext _tenant;

    public CsdlDuocIntegrationController(
        ICsdlDuocStockOutSyncService sync,
        ITenantCsdlDuocLinkService link,
        ITenantContext tenant)
    {
        _sync = sync;
        _link = link;
        _tenant = tenant;
    }

    [HttpGet("link")]
    [Authorize(Policy = InventoryPolicies.Read)]
    [ProducesResponseType(typeof(TenantCsdlDuocLinkDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantCsdlDuocLinkDto>> GetLink(CancellationToken cancellationToken) =>
        Ok(await _link.GetAsync(_tenant.TenantId, cancellationToken));

    [HttpPut("link")]
    [Authorize(Policy = InventoryPolicies.Write)]
    [ProducesResponseType(typeof(TenantCsdlDuocLinkDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantCsdlDuocLinkDto>> UpdateLink(
        [FromBody] UpdateTenantCsdlDuocLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _link.UpdateAsync(
                _tenant.TenantId,
                request,
                _tenant.IsAuthenticated ? _tenant.UserId : null,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("link/test")]
    [Authorize(Policy = InventoryPolicies.Write)]
    [ProducesResponseType(typeof(TenantCsdlDuocLinkDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantCsdlDuocLinkDto>> TestLink(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _link.TestConnectionAsync(
                _tenant.TenantId,
                _tenant.IsAuthenticated ? _tenant.UserId : null,
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sync-log")]
    [Authorize(Policy = InventoryPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<CsdlDuocSyncLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CsdlDuocSyncLogDto>>> ListSyncLog(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _sync.ListRecentAsync(_tenant.TenantId, limit, cancellationToken);
        return Ok(items);
    }

    /// <summary>Đẩy lại / đẩy thủ công stock-out cho một đơn bán hoàn tất (idempotent nếu đã có log thành công).</summary>
    [HttpPost("stock-out/{orderId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SyncStockOut(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await _sync.SyncSalesOrderAsync(_tenant.TenantId, orderId, orderNumber: null, cancellationToken);
        return Accepted();
    }
}
