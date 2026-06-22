using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Application.Catalog;
using PharmaCore.Api.Authorization;

namespace PharmaCore.Api.Controllers.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog/ingredients")]
public sealed class ActiveIngredientsController : ControllerBase
{
    private readonly IActiveIngredientService _ingredients;

    public ActiveIngredientsController(IActiveIngredientService ingredients) => _ingredients = ingredients;

    [HttpGet]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<ActiveIngredientDto>>> List(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _ingredients.GetAllAsync(cancellationToken);
        if (activeOnly)
            items = items.Where(i => i.Status == 1).ToList();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<ActiveIngredientDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _ingredients.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ActiveIngredientDto>> Create(
        [FromBody] CreateActiveIngredientRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _ingredients.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ActiveIngredientDto>> Update(
        Guid id,
        [FromBody] UpdateActiveIngredientRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _ingredients.UpdateAsync(id, request, cancellationToken);
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
        var (ok, error) = await _ingredients.DeleteAsync(id, cancellationToken);
        if (ok) return NoContent();
        return error?.Contains("không tồn tại") == true ? NotFound(new { message = error }) : BadRequest(new { message = error });
    }
}
