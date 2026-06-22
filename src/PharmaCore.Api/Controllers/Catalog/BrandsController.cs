using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Catalog;

namespace PharmaCore.Api.Controllers.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog/brands")]
public sealed class BrandsController : ControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands) => _brands = brands;

    [HttpGet]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _brands.GetAllAsync(cancellationToken);
        if (activeOnly)
            items = items.Where(b => b.Status == 1).ToList();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<BrandDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _brands.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<BrandDto>> Create(
        [FromBody] CreateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _brands.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<BrandDto>> Update(
        Guid id,
        [FromBody] UpdateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _brands.UpdateAsync(id, request, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (ok, error) = await _brands.DeleteAsync(id, cancellationToken);
        if (ok) return NoContent();
        return error?.Contains("không tồn tại") == true ? NotFound(new { message = error }) : BadRequest(new { message = error });
    }
}
