using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Pharmacy.Integration.Qd540;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Route("api/pharmacy/integration/qd540")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.Inventory)]
public sealed class Qd540IntegrationController : ControllerBase
{
    private readonly IQd540Table1Service _service;

    public Qd540IntegrationController(IQd540Table1Service service) => _service = service;

    [HttpGet("table1")]
    [Authorize(Policy = InventoryPolicies.Read)]
    [ProducesResponseType(typeof(Qd540Table1ExportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Qd540Table1ExportResult>> GetTable1(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.ExportTable1Async(new Qd540Table1Query(from, to, branchId), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("table1.csv")]
    [Authorize(Policy = InventoryPolicies.Read)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTable1Csv(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _service.ExportTable1CsvAsync(new Qd540Table1Query(from, to, branchId), cancellationToken);
            var fileName = $"qd540-table1-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
