using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.Pharmacy.Procurement;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/procurement/supplier-payables")]
public sealed class SupplierPayablesController : ControllerBase
{
    private readonly ISupplierPayablesService _payables;

    public SupplierPayablesController(ISupplierPayablesService payables) => _payables = payables;

    [HttpGet]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<SupplierPayablesRowDto>>> Summary(
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken) =>
        Ok(await _payables.GetSummaryAsync(warehouseId, cancellationToken));

    [HttpGet("{supplierId:guid}")]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<SupplierPayablesDetailDto>> Detail(
        Guid supplierId,
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        var detail = await _payables.GetDetailAsync(supplierId, warehouseId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }
}
