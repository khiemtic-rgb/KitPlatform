using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/members/{memberId:guid}/badges")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsBadgeController : ControllerBase
{
    private readonly IFamilyBadgeService _badges;

    public FamilyOsBadgeController(IFamilyBadgeService badges) => _badges = badges;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FamilyBadgeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FamilyBadgeDto>>> List(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _badges.ListMemberBadgesAsync(familyId, memberId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
