using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Procurement;

namespace PharmaCore.Api.Controllers.Procurement;

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
        CancellationToken cancellationToken) =>
        Ok(await _payables.GetSummaryAsync(cancellationToken));

    [HttpGet("{supplierId:guid}")]
    [Authorize(Policy = ProcurementPolicies.Read)]
    public async Task<ActionResult<SupplierPayablesDetailDto>> Detail(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var detail = await _payables.GetDetailAsync(supplierId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }
}
