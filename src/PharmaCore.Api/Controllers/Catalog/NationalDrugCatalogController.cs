using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Catalog;

namespace PharmaCore.Api.Controllers.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog/national-drugs")]
public sealed class NationalDrugCatalogController : ControllerBase
{
    private readonly INationalDrugCatalogService _catalog;

    public NationalDrugCatalogController(INationalDrugCatalogService catalog) => _catalog = catalog;

    [HttpGet("connection-status")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<NationalDrugConnectionStatusDto>> ConnectionStatus(CancellationToken cancellationToken) =>
        Ok(await _catalog.GetConnectionStatusAsync(cancellationToken));

    [HttpGet("field-map")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public ActionResult<IReadOnlyList<NationalDrugFieldMapDto>> FieldMap() =>
        Ok(_catalog.GetFieldMap());

    [HttpGet]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<PagedNationalDrugListResult>> Search(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await _catalog.SearchAsync(search, page, pageSize, cancellationToken));

    [HttpGet("{drugId}/prefill")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<NationalDrugProductPrefillDto>> Prefill(string drugId, CancellationToken cancellationToken)
    {
        var prefill = await _catalog.BuildProductPrefillAsync(drugId, cancellationToken);
        return prefill is null ? NotFound() : Ok(prefill);
    }

    [HttpGet("{drugId}")]
    [Authorize(Policy = CatalogPolicies.Read)]
    public async Task<ActionResult<NationalDrugDetailDto>> Get(string drugId, CancellationToken cancellationToken)
    {
        var item = await _catalog.GetAsync(drugId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
