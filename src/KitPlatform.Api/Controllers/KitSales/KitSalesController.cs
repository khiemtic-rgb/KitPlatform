using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Api.Controllers.KitSales;

[ApiController]
[Route("api/kit-sales")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.KitSales)]
public sealed class KitSalesController : ControllerBase
{
    private readonly IKitSalesDeskService _desk;

    public KitSalesController(IKitSalesDeskService desk) => _desk = desk;

    [HttpGet("health")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesHealthDto>> Health(CancellationToken cancellationToken) =>
        Ok(await _desk.GetHealthAsync(cancellationToken));

    [HttpGet("products")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesProductDto>>> Products(
        CancellationToken cancellationToken) =>
        Ok(await _desk.ListProductsAsync(cancellationToken));

    [HttpGet("businesses")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesBusinessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesBusinessDto>>> Businesses(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListBusinessesAsync(limit, cancellationToken));

    [HttpPost("businesses")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesBusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesBusinessDto>> CreateBusiness(
        [FromBody] CreateKitSalesBusinessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateBusinessAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("leads")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<KitSalesLeadDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KitSalesLeadDto>>> Leads(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _desk.ListLeadsAsync(status, limit, cancellationToken));

    [HttpPost("leads")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesLeadDto>> CreateLead(
        [FromBody] CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateLeadAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("prospects")]
    [Authorize(Policy = KitSalesPolicies.Write)]
    [ProducesResponseType(typeof(KitSalesLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KitSalesLeadDto>> CreateProspect(
        [FromBody] CreateKitSalesProspectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.CreateProspectAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("pipeline/summary")]
    [Authorize(Policy = KitSalesPolicies.Read)]
    [ProducesResponseType(typeof(KitSalesPipelineSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<KitSalesPipelineSummaryDto>> PipelineSummary(
        CancellationToken cancellationToken) =>
        Ok(await _desk.GetPipelineSummaryAsync(cancellationToken));
}
