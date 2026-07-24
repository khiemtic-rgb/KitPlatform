using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/accountability-options")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsAccountabilityOptionsController : ControllerBase
{
    private readonly IFamilyAgreementService _agreements;

    public FamilyOsAccountabilityOptionsController(IFamilyAgreementService agreements) =>
        _agreements = agreements;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyAccountabilityOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyAccountabilityOptionDto>>> List(
        Guid familyId,
        [FromQuery] string? kind,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _agreements.ListOptionsAsync(familyId, kind, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(FamilyAccountabilityOptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyAccountabilityOptionDto>> Create(
        Guid familyId,
        [FromBody] CreateAccountabilityOptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _agreements.CreateOptionAsync(familyId, request, cancellationToken);
            return Created(
                $"api/family-os/families/{familyId}/accountability-options/{created.Id}",
                created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("{optionId:guid}")]
    [ProducesResponseType(typeof(FamilyAccountabilityOptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyAccountabilityOptionDto>> Update(
        Guid familyId,
        Guid optionId,
        [FromBody] UpdateAccountabilityOptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _agreements.UpdateOptionAsync(
                familyId, optionId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("{optionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        Guid familyId,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _agreements.DeleteOptionAsync(familyId, optionId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
