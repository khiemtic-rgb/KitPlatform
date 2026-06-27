using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Configuration;
using PharmaCore.Application.Inventory;

namespace PharmaCore.Api.Controllers.Inventory;

[ApiController]
[Authorize]
[Route("api/inventory/low-stock/settings")]
public sealed class LowStockSettingsController : ControllerBase
{
    private readonly ILowStockSettingsService _settings;

    public LowStockSettingsController(ILowStockSettingsService settings) => _settings = settings;

    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Read)]
    public async Task<ActionResult<LowStockSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetSettingsAsync(cancellationToken));

    [HttpPut("default")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<TenantDefaultMinStockDto>> UpdateDefault(
        [FromBody] UpdateTenantDefaultMinStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.UpdateDefaultAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("categories/{categoryId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<CategoryLowStockSettingDto>> UpdateCategory(
        Guid categoryId,
        [FromBody] UpdateCategoryMinStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _settings.UpdateCategoryMinStockAsync(categoryId, request.MinStockQty, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("warehouses/{warehouseId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<WarehouseLowStockSettingDto>> UpdateWarehouse(
        Guid warehouseId,
        [FromBody] UpdateWarehouseMinStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _settings.UpdateWarehouseMinStockAsync(warehouseId, request.MinStockQty, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("apply-all")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<ApplyLowStockResultDto>> ApplyAll(
        [FromBody] ApplyLowStockToProductsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.ApplyDefaultToProductsAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("apply-category/{categoryId:guid}")]
    [Authorize(Policy = InventoryPolicies.Write)]
    public async Task<ActionResult<ApplyLowStockResultDto>> ApplyCategory(
        Guid categoryId,
        [FromBody] ApplyLowStockToProductsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.ApplyCategoryToProductsAsync(categoryId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed class UpdateCategoryMinStockRequest
{
    public decimal? MinStockQty { get; init; }
}

public sealed class UpdateWarehouseMinStockRequest
{
    public decimal? MinStockQty { get; init; }
}
