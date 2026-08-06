using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Pharmacy;

namespace KitPlatform.Api.Controllers.Pharmacy;

[ApiController]
[Authorize]
[Route("api/pharmacy/storefront-profile")]
public sealed class PharmacyStorefrontProfileController : ControllerBase
{
    private readonly IPharmacyStorefrontService _storefront;

    public PharmacyStorefrontProfileController(IPharmacyStorefrontService storefront) =>
        _storefront = storefront;

    [HttpGet]
    [Authorize(Policy = SalesPolicies.Read)]
    [ProducesResponseType(typeof(PharmacyStorefrontProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PharmacyStorefrontProfileDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _storefront.GetProfileAsync(cancellationToken));

    [HttpPut]
    [Authorize(Policy = SalesPolicies.Write)]
    [ProducesResponseType(typeof(PharmacyStorefrontProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PharmacyStorefrontProfileDto>> Upsert(
        [FromBody] UpdatePharmacyStorefrontProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _storefront.UpsertProfileAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
