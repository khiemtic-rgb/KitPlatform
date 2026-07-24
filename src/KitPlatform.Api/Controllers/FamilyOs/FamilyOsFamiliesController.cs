using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsFamiliesController : ControllerBase
{
    private readonly IFamilyGraphService _families;

    public FamilyOsFamiliesController(IFamilyGraphService families) => _families = families;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _families.ListFamiliesAsync(cancellationToken));

    [HttpGet("{familyId:guid}")]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamilyDto>> Get(Guid familyId, CancellationToken cancellationToken)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        return family is null ? NotFound() : Ok(family);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyDto>> Create(
        [FromBody] CreateFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var family = await _families.CreateFamilyAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { familyId = family.Id }, family);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("{familyId:guid}")]
    [ProducesResponseType(typeof(FamilyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FamilyDto>> Update(
        Guid familyId,
        [FromBody] UpdateFamilyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _families.UpdateFamilyAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("Không tìm thấy", StringComparison.Ordinal))
                return NotFound();
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("{familyId:guid}/members")]
    [ProducesResponseType(typeof(FamilyMembershipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMembershipDto>> AddMember(
        Guid familyId,
        [FromBody] AddMembershipRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var member = await _families.AddMemberAsync(familyId, request, cancellationToken);
            return Created($"api/family-os/families/{familyId}/members/{member.Id}", member);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPatch("{familyId:guid}/members/{memberId:guid}")]
    [ProducesResponseType(typeof(FamilyMembershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilyMembershipDto>> UpdateMember(
        Guid familyId,
        Guid memberId,
        [FromBody] UpdateMembershipRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _families.UpdateMemberAsync(familyId, memberId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
