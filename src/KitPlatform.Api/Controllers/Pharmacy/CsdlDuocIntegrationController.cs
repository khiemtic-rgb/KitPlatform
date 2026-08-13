using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Route("api/pharmacy/integration/csdl-duoc")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.Inventory)]
public sealed class CsdlDuocIntegrationController : ControllerBase
{
    private readonly ICsdlDuocStockOutSyncService _sync;
    private readonly ITenantContext _tenant;

    public CsdlDuocIntegrationController(ICsdlDuocStockOutSyncService sync, ITenantContext tenant)
    {
        _sync = sync;
        _tenant = tenant;
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
