using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Catalog;

namespace PharmaCore.Api.Controllers.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories) => _categories = categories;

    [HttpGet]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _categories.GetAllAsync(cancellationToken);
        if (activeOnly)
            items = items.Where(c => c.Status == 1).ToList();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<CategoryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _categories.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _categories.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<CategoryDto>> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _categories.UpdateAsync(id, request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (ok, error) = await _categories.DeleteAsync(id, cancellationToken);
        if (ok) return NoContent();
        return error?.Contains("không tồn tại") == true ? NotFound(new { message = error }) : BadRequest(new { message = error });
    }
}
