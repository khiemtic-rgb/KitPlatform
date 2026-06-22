using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Catalog;

namespace PharmaCore.Api.Controllers.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog")]
public sealed class ProductCodeController : ControllerBase
{
    private readonly ICatalogService _catalog;

    public ProductCodeController(ICatalogService catalog) => _catalog = catalog;

    [HttpGet("product-next-code")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<object>> GetNext(CancellationToken cancellationToken)
    {
        var code = await _catalog.GetNextProductCodeAsync(cancellationToken);
        return Ok(new { productCode = code });
    }

    [HttpGet("product-check-name")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<SimilarProductNamesResult>> CheckName(
        [FromQuery] string name,
        [FromQuery] Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Ok(new SimilarProductNamesResult([], false));

        var result = await _catalog.FindSimilarProductNamesAsync(name, excludeId, 0.95, cancellationToken);
        return Ok(result);
    }

    [HttpPut("product-commercial")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductDetailDto>> SyncCommercial(
        [FromBody] SyncProductCommercialBodyRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalog.SyncProductCommercialAsync(
            request.ProductId,
            request,
            cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("product-commercial")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductDetailDto>> SyncCommercialPost(
        [FromBody] SyncProductCommercialBodyRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalog.SyncProductCommercialAsync(
            request.ProductId,
            request,
            cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("product-check-barcode")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<BarcodeCheckResult>> CheckBarcode(
        [FromQuery] string barcode,
        [FromQuery] Guid? excludeProductId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return Ok(new BarcodeCheckResult(true, null, null, null));

        var result = await _catalog.CheckBarcodeAsync(barcode, excludeProductId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("product-units")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductDetailDto>> SyncUnits(
        [FromBody] SyncProductUnitsBodyRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalog.SyncProductUnitsAsync(request.ProductId, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("product-units")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductDetailDto>> SyncUnitsPost(
        [FromBody] SyncProductUnitsBodyRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalog.SyncProductUnitsAsync(request.ProductId, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPut("product-ingredients")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductDetailDto>> SyncIngredients(
        [FromBody] SyncProductIngredientsBodyRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalog.SyncProductIngredientsAsync(request.ProductId, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("product-ingredients")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductDetailDto>> SyncIngredientsPost(
        [FromBody] SyncProductIngredientsBodyRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _catalog.SyncProductIngredientsAsync(request.ProductId, request, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }
}
