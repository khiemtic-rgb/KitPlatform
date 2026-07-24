using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/star-settings")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsStarSettingsController : ControllerBase
{
    private readonly IFamilyStarSettingsService _settings;

    public FamilyOsStarSettingsController(IFamilyStarSettingsService settings) =>
        _settings = settings;

    [HttpGet]
    [ProducesResponseType(typeof(FamilyStarSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyStarSettingsDto>> Get(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.GetSettingsAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut]
    [ProducesResponseType(typeof(FamilyStarSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyStarSettingsDto>> Upsert(
        Guid familyId,
        [FromBody] UpdateFamilyStarSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.UpsertSettingsAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
