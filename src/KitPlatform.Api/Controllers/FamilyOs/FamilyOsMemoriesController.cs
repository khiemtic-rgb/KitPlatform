using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsMemoriesController : ControllerBase
{
    private readonly IFamilyMemoryService _memories;

    public FamilyOsMemoriesController(IFamilyMemoryService memories) => _memories = memories;

    [HttpGet("memories")]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyMemoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FamilyMemoryDto>>> List(
        Guid familyId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool favoritesOnly = false,
        [FromQuery] int limit = 60,
        [FromQuery] Guid? memberId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _memories.ListAsync(
                familyId, from, to, favoritesOnly, limit, memberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("memories")]
    [ProducesResponseType(typeof(FamilyMemoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMemoryDto>> Create(
        Guid familyId,
        [FromBody] FamilyMemoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memories.CreateAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("memories/{memoryId:guid}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetFavorite(
        Guid familyId,
        Guid memoryId,
        [FromQuery] bool value = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _memories.SetFavoriteAsync(familyId, memoryId, value, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpDelete("memories/{memoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        Guid familyId,
        Guid memoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _memories.DeleteAsync(familyId, memoryId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("memories/recap")]
    [ProducesResponseType(typeof(FamilyMemoryRecapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMemoryRecapDto>> Recap(
        Guid familyId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memories.GetRecapAsync(familyId, from, to, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
