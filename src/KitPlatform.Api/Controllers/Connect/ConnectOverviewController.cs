using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/overview")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectOverviewController : ControllerBase
{
    private readonly IConnectOverviewService _overview;

    public ConnectOverviewController(IConnectOverviewService overview) => _overview = overview;

    [HttpGet]
    [ProducesResponseType(typeof(ConnectOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConnectOverviewDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _overview.GetOverviewAsync(cancellationToken));
}
