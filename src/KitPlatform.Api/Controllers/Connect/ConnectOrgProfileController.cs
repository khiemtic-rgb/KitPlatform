using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/org-profile")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectOrgProfileController : ControllerBase
{
    private readonly IConnectOrgProfileService _profiles;

    public ConnectOrgProfileController(IConnectOrgProfileService profiles) => _profiles = profiles;

    [HttpGet]
    [ProducesResponseType(typeof(ConnectOrgProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectOrgProfileDto>> GetMine(CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetMyProfileAsync(cancellationToken);
        return profile is null ? NotFound(new { message = "Chưa có Connect org profile." }) : Ok(profile);
    }
}
