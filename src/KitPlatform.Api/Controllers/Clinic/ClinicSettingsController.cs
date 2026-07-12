using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Api.Controllers.Clinic;

[ApiController]
[Route("api/clinic/settings")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.ClinicEmrLite)]
public sealed class ClinicSettingsController : ControllerBase
{
    private readonly IClinicTenantSettingsService _settings;

    public ClinicSettingsController(IClinicTenantSettingsService settings) => _settings = settings;

    [HttpGet]
    [ProducesResponseType(typeof(ClinicTenantSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClinicTenantSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    [HttpPut]
    [ProducesResponseType(typeof(ClinicTenantSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicTenantSettingsDto>> Update(
        [FromBody] UpdateClinicTenantSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.UpdateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
