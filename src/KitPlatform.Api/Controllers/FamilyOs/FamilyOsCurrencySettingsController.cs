using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/currency-settings")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsCurrencySettingsController : ControllerBase
{
    private readonly IFamilyCurrencySettingsService _settings;

    public FamilyOsCurrencySettingsController(IFamilyCurrencySettingsService settings) =>
        _settings = settings;

    [HttpGet]
    [ProducesResponseType(typeof(FamilyCurrencySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyCurrencySettingsDto>> Get(
        Guid familyId,
        [FromQuery] Guid? memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _settings.GetSettingsAsync(familyId, memberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut]
    [ProducesResponseType(typeof(FamilyCurrencySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyCurrencySettingsDto>> Upsert(
        Guid familyId,
        [FromBody] UpdateFamilyCurrencySettingsRequest request,
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

    [HttpPost("apply-preset")]
    [ProducesResponseType(typeof(FamilyCurrencySettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyCurrencySettingsDto>> ApplyPreset(
        Guid familyId,
        [FromQuery] string presetId = FamilyCurrencyPreset.BalancedV1Id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _settings.ApplyPresetAsync(familyId, presetId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
