using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/blueprint")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsBlueprintController : ControllerBase
{
    private readonly IFamilyBlueprintService _blueprint;

    public FamilyOsBlueprintController(IFamilyBlueprintService blueprint) => _blueprint = blueprint;

    [HttpGet]
    [ProducesResponseType(typeof(FamilyBlueprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyBlueprintDto>> Get(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _blueprint.GetAsync(familyId, cancellationToken);
            return dto is null ? NoContent() : Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("dna")]
    [ProducesResponseType(typeof(FamilyDnaCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyDnaCardDto>> GetDna(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _blueprint.GetDnaCardAsync(familyId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut]
    [ProducesResponseType(typeof(FamilyBlueprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyBlueprintDto>> Upsert(
        Guid familyId,
        [FromBody] FamilyBlueprintUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _blueprint.UpsertAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("hydrate")]
    [ProducesResponseType(typeof(FamilyBlueprintDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyBlueprintDto>> Hydrate(
        Guid familyId,
        [FromBody] FamilyBlueprintHydrateRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _blueprint.HydrateFromOnboardingAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("calibration")]
    [ProducesResponseType(typeof(FamilyDnaCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyDnaCardDto>> CaptureCalibration(
        Guid familyId,
        [FromBody] FamilyCalibrationCaptureRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _blueprint.CaptureCalibrationAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
