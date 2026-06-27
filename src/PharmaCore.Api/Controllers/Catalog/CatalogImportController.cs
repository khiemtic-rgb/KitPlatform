using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Api.Authorization;
using PharmaCore.Application.Catalog;

namespace PharmaCore.Api.Controllers.Catalog;

[ApiController]
[Authorize]
[Route("api/catalog/import")]
public sealed class CatalogImportController : ControllerBase
{
    private readonly ICatalogImportService _import;

    public CatalogImportController(ICatalogImportService import) => _import = import;

    [HttpPost("products")]
    [Authorize(Policy = CatalogPolicies.Write)]
    public async Task<ActionResult<ProductImportResultDto>> ImportProducts(
        [FromBody] IReadOnlyList<ProductImportRowRequest> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return BadRequest(new { message = "Không có dòng dữ liệu để import." });

        if (rows.Count > 2000)
            return BadRequest(new { message = "Tối đa 2000 dòng mỗi lần import." });

        return Ok(await _import.ImportProductsAsync(rows, cancellationToken));
    }
}
