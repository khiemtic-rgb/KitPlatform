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
public sealed class FamilyOsStarBalanceController : ControllerBase
{
    private readonly IFamilyStarService _stars;

    public FamilyOsStarBalanceController(IFamilyStarService stars) => _stars = stars;

    [HttpGet("members/{memberId:guid}/star-balance")]
    [ProducesResponseType(typeof(MemberStarBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MemberStarBalanceDto>> GetBalance(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        try
        {
            var balance = await _stars.GetMemberBalancesAsync(familyId, memberId, cancellationToken);
            return Ok(balance);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
