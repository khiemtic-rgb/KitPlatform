using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Configuration;

namespace PharmaCore.Api.Controllers.Sales;

[ApiController]
[Authorize]
[Route("api/sales/settings")]
public sealed class SalesSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _settings;

    public SalesSettingsController(ITenantSettingsService settings) => _settings = settings;

    [HttpGet("receipt")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<TenantReceiptSettingsDto>> GetReceipt(CancellationToken cancellationToken) =>
        Ok(await _settings.GetReceiptSettingsAsync(cancellationToken));

    [HttpPut("receipt")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<TenantReceiptSettingsDto>> UpdateReceipt(
        [FromBody] UpdateTenantReceiptSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.UpdateReceiptSettingsAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("batch-mode")]
    [Authorize(Policy = SalesPolicies.Read)]
    public async Task<ActionResult<TenantBatchModeSettingsDto>> GetBatchMode(CancellationToken cancellationToken) =>
        Ok(await _settings.GetBatchModeSettingsAsync(cancellationToken));

    [HttpPut("batch-mode")]
    [Authorize(Policy = SalesPolicies.Write)]
    public async Task<ActionResult<TenantBatchModeSettingsDto>> UpdateBatchMode(
        [FromBody] UpdateTenantBatchModeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.UpdateBatchModeAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
