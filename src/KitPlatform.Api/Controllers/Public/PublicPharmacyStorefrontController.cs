using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Application.Pharmacy;

namespace KitPlatform.Api.Controllers.Public;

[ApiController]
[Route("api/public/pharmacy-storefront")]
[AllowAnonymous]
public sealed class PublicPharmacyStorefrontController : ControllerBase
{
    private readonly IPharmacyStorefrontService _storefront;

    public PublicPharmacyStorefrontController(IPharmacyStorefrontService storefront) =>
        _storefront = storefront;

    [HttpGet]
    [ProducesResponseType(typeof(PublicPharmacyStorefrontDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] string? slug,
        [FromQuery] string? tenantCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var bySlug = await _storefront.GetPublishedBySlugAsync(slug, cancellationToken);
            return bySlug is null ? NotFound() : Ok(bySlug);
        }

        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            var byCode = await _storefront.GetPublishedByTenantCodeAsync(tenantCode, cancellationToken);
            return byCode is null ? NotFound() : Ok(byCode);
        }

        return BadRequest(new { message = "Cần slug hoặc tenantCode." });
    }
}
