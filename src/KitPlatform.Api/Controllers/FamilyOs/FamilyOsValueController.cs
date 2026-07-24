using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/value")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsValueController : ControllerBase
{
    private readonly IFamilyValueService _value;

    public FamilyOsValueController(IFamilyValueService value) => _value = value;

    [HttpGet("state")]
    [ProducesResponseType(typeof(FamilyValueStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyValueStateDto>> GetState(
        Guid familyId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _value.GetStateAsync(familyId, from, to, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("health-score")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertHealthScore(
        Guid familyId,
        [FromBody] FamilyHealthScoreUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _value.UpsertHealthScoreAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("nudges/increment")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> IncrementNudge(
        Guid familyId,
        [FromBody] FamilyNudgeIncrementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await _value.IncrementNudgeAsync(familyId, request, cancellationToken);
            return Ok(new { count });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("nudges")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetNudgeCount(
        Guid familyId,
        [FromBody] FamilyNudgeSetRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _value.SetNudgeCountAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("onboarding")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertOnboarding(
        Guid familyId,
        [FromBody] FamilyOnboardingUpsertRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _value.UpsertOnboardingAsync(familyId, request, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("onboarding")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearOnboarding(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _value.ClearOnboardingAsync(familyId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
